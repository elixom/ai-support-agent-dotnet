using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using backend.Services;

namespace backend.Middleware
{
    public class WebSocketChatMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WebSocketChatMiddleware> _logger;

        public WebSocketChatMiddleware(RequestDelegate next, ILogger<WebSocketChatMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";
            if (path.StartsWith("/ws/chat", StringComparison.OrdinalIgnoreCase))
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    _logger.LogInformation("Incoming WebSocket request on path: {Path}", path);
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await HandleWebSocketLoopAsync(context, webSocket);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Expected a WebSocket request.");
                }
            }
            else
            {
                await _next(context);
            }
        }

        private async Task HandleWebSocketLoopAsync(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            var conversationService = context.RequestServices.GetRequiredService<IConversationService>();

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("WebSocket close message received.");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using var ms = new MemoryStream();
                        await ms.WriteAsync(buffer, 0, result.Count);
                        while (!result.EndOfMessage)
                        {
                            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            await ms.WriteAsync(buffer, 0, result.Count);
                        }

                        ms.Seek(0, SeekOrigin.Begin);
                        using var reader = new StreamReader(ms, Encoding.UTF8);
                        var rawMessage = await reader.ReadToEndAsync();
                        _logger.LogInformation("Received WebSocket payload: {Raw}", rawMessage);

                        try
                        {
                            using var doc = JsonDocument.Parse(rawMessage);
                            var root = doc.RootElement;

                            var messageText = root.TryGetProperty("message", out var mProp) ? mProp.GetString() ?? "" : "";
                            var senderName = root.TryGetProperty("sender_name", out var snProp) ? snProp.GetString() : "Anonymous";
                            var senderId = root.TryGetProperty("sender_id", out var siProp) ? siProp.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();

                            if (string.IsNullOrWhiteSpace(messageText))
                            {
                                var errPayload = JsonSerializer.Serialize(new { type = "error", message = "Empty message received." });
                                await SendTextAsync(webSocket, errPayload);
                                continue;
                            }

                            // Process message through AI pipeline
                            var pipelineResult = await conversationService.ProcessMessageAsync(messageText, senderId, senderName, "webchat");

                            // Serialize response
                            using var parsedResult = JsonDocument.Parse(JsonSerializer.Serialize(pipelineResult));
                            string? finalResponseText = null;
                            object? classificationObj = null;
                            string? conversationId = null;

                            if (parsedResult.RootElement.TryGetProperty("response", out var respProp) && respProp.ValueKind == JsonValueKind.String)
                            {
                                finalResponseText = respProp.GetString();
                            }

                            if (parsedResult.RootElement.TryGetProperty("classification", out var classProp))
                            {
                                classificationObj = JsonSerializer.Deserialize<object>(classProp.GetRawText());
                            }

                            if (parsedResult.RootElement.TryGetProperty("conversation_id", out var convProp) && convProp.ValueKind == JsonValueKind.String)
                            {
                                conversationId = convProp.GetString();
                            }

                            var successPayload = JsonSerializer.Serialize(new
                            {
                                type = "ai_response",
                                message = finalResponseText,
                                classification = classificationObj,
                                conversation_id = conversationId
                            });

                            await SendTextAsync(webSocket, successPayload);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error parsing or processing WebSocket payload.");
                            var errPayload = JsonSerializer.Serialize(new { type = "error", message = "An error occurred while processing your message." });
                            await SendTextAsync(webSocket, errPayload);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in WebSocket loop.");
            }
        }

        private async Task SendTextAsync(WebSocket webSocket, string text)
        {
            if (webSocket.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(text);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
