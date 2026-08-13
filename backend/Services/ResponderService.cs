using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    public class ResponderService : IResponderService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ResponderService> _logger;

        public ResponderService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<ResponderService> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<ResponderResult> GenerateResponseAsync(
            string message,
            List<ChatHistoryEntry> conversationHistory,
            List<string> knowledgeChunks,
            string? customApiKey = null)
        {
            var azureUrl = _configuration["AZURE_AI_FOUNDRY_SONNET_URL"] ?? Environment.GetEnvironmentVariable("AZURE_AI_FOUNDRY_SONNET_URL");
            var azureKey = _configuration["AZURE_AI_FOUNDRY_SONNET_KEY"] ?? Environment.GetEnvironmentVariable("AZURE_AI_FOUNDRY_SONNET_KEY");

            var anthropicKey = customApiKey ?? _configuration["ANTHROPIC_API_KEY"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

            string systemPrompt = @"<role>
You are a helpful, professional customer support agent. You represent the company
and must provide accurate, empathetic responses.
</role>

<rules>
- ONLY answer based on the provided knowledge base context below.
- If the knowledge base context does not contain enough information to answer
  confidently, say so honestly and offer to connect the customer with a human agent.
- Never fabricate policies, prices, guarantees, or technical details.
- Be concise but thorough. Use a warm, professional tone.
- If the customer seems frustrated, acknowledge their feelings before solving.
- Always end with a clear next step or offer for further help.
</rules>";

            var kbContext = knowledgeChunks != null && knowledgeChunks.Count > 0
                ? string.Join("\n---\n", knowledgeChunks)
                : "No relevant knowledge base entries found.";

            string userContent = $@"<knowledge_base_context>
{kbContext}
</knowledge_base_context>

<customer_message>
{message}
</customer_message>";

            if (!string.IsNullOrEmpty(azureUrl) && !string.IsNullOrEmpty(azureKey))
            {
                _logger.LogInformation("Calling Azure AI Foundry Claude Sonnet endpoint...");
                return await CallAzureFoundryAsync(azureUrl, azureKey, systemPrompt, userContent, conversationHistory);
            }
            else if (!string.IsNullOrEmpty(anthropicKey))
            {
                _logger.LogInformation("Calling direct Anthropic Claude Sonnet API...");
                return await CallDirectAnthropicAsync(anthropicKey, systemPrompt, userContent, conversationHistory);
            }
            else
            {
                _logger.LogWarning("No AI configuration found for responder. Falling back to default human offer.");
                return DefaultHandoffResponse();
            }
        }

        private async Task<ResponderResult> CallAzureFoundryAsync(
            string url,
            string apiKey,
            string systemPrompt,
            string userContent,
            List<ChatHistoryEntry> conversationHistory)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("api-key", apiKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                object requestBody;

                if (url.Contains("/chat/completions"))
                {
                    // OpenAI style Chat Completions
                    var messagesList = new List<object>
                    {
                        new { role = "system", content = systemPrompt }
                    };

                    foreach (var entry in conversationHistory)
                    {
                        var role = entry.Role.ToLower() == "ai" ? "assistant" : "user";
                        messagesList.Add(new { role = role, content = entry.Content });
                    }

                    messagesList.Add(new { role = "user", content = userContent });

                    requestBody = new
                    {
                        messages = messagesList,
                        max_tokens = 1024,
                        temperature = 0.2
                    };
                }
                else
                {
                    // Direct Anthropic format on Azure AI Foundry
                    var messagesList = new List<object>();
                    foreach (var entry in conversationHistory)
                    {
                        var role = entry.Role.ToLower() == "ai" ? "assistant" : "user";
                        messagesList.Add(new { role = role, content = entry.Content });
                    }
                    messagesList.Add(new { role = "user", content = userContent });

                    requestBody = new
                    {
                        model = _configuration["CLAUDE_SONNET_MODEL"] ?? "claude-3-5-sonnet-20241022",
                        max_tokens = 1024,
                        system = systemPrompt,
                        messages = messagesList
                    };
                }

                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Azure AI Foundry Sonnet call failed: {StatusCode} - {Error}", response.StatusCode, err);
                    return DefaultHandoffResponse();
                }

                var responseString = await response.Content.ReadAsStringAsync();
                return ParseResponse(responseString, url.Contains("/chat/completions"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Responder AzureFoundry call");
                return DefaultHandoffResponse();
            }
        }

        private async Task<ResponderResult> CallDirectAnthropicAsync(
            string apiKey,
            string systemPrompt,
            string userContent,
            List<ChatHistoryEntry> conversationHistory)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");

                var messagesList = new List<object>();
                foreach (var entry in conversationHistory)
                {
                    var role = entry.Role.ToLower() == "ai" ? "assistant" : "user";
                    messagesList.Add(new { role = role, content = entry.Content });
                }
                messagesList.Add(new { role = "user", content = userContent });

                var requestBody = new
                {
                    model = _configuration["CLAUDE_SONNET_MODEL"] ?? "claude-3-5-sonnet-20241022",
                    max_tokens = 1024,
                    system = systemPrompt,
                    messages = messagesList
                };

                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Direct Anthropic Sonnet call failed: {StatusCode} - {Error}", response.StatusCode, err);
                    return DefaultHandoffResponse();
                }

                var responseString = await response.Content.ReadAsStringAsync();
                return ParseResponse(responseString, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Responder DirectAnthropic call");
                return DefaultHandoffResponse();
            }
        }

        private ResponderResult ParseResponse(string json, bool isOpenAiStyle)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                string textContent = string.Empty;
                string stopReason = "end_turn";

                if (isOpenAiStyle)
                {
                    textContent = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? string.Empty;

                    if (doc.RootElement.GetProperty("choices")[0].TryGetProperty("finish_reason", out var finProp))
                    {
                        stopReason = finProp.GetString() ?? "stop";
                    }
                }
                else
                {
                    // Anthropic format
                    textContent = doc.RootElement
                        .GetProperty("content")[0]
                        .GetProperty("text")
                        .GetString() ?? string.Empty;

                    if (doc.RootElement.TryGetProperty("stop_reason", out var stopProp))
                    {
                        stopReason = stopProp.GetString() ?? "end_turn";
                    }
                }

                textContent = textContent.Trim();

                // Compute heuristic confidence
                double confidence = 0.9;
                var hedgePhrases = new[]
                {
                    "i'm not sure",
                    "i don't have enough information",
                    "connect you with",
                    "human agent",
                    "i cannot confirm"
                };

                foreach (var phrase in hedgePhrases)
                {
                    if (textContent.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    {
                        confidence = 0.5;
                        break;
                    }
                }

                if (stopReason != "end_turn" && stopReason != "stop" && stopReason != "completed")
                {
                    confidence = Math.Max(confidence - 0.2, 0.1);
                }

                return new ResponderResult
                {
                    Response = textContent,
                    Confidence = confidence
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse responder response. Raw JSON: {Raw}", json);
                return DefaultHandoffResponse();
            }
        }

        private ResponderResult DefaultHandoffResponse()
        {
            return new ResponderResult
            {
                Response = "I apologize, but I'm experiencing a temporary issue. Let me connect you with a human agent who can help right away.",
                Confidence = 0.0
            };
        }
    }
}
