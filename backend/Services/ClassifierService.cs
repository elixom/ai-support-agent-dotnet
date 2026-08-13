using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    public class ClassifierService : IClassifierService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ClassifierService> _logger;

        public ClassifierService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<ClassifierService> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<ClassificationResult> ClassifyTicketAsync(string message, string? customApiKey = null)
        {
            // Try to find Azure Foundry config first, then fall back to standard Anthropic config
            var azureUrl = _configuration["AZURE_AI_FOUNDRY_HAIKU_URL"] ?? Environment.GetEnvironmentVariable("AZURE_AI_FOUNDRY_HAIKU_URL");
            var azureKey = _configuration["AZURE_AI_FOUNDRY_HAIKU_KEY"] ?? Environment.GetEnvironmentVariable("AZURE_AI_FOUNDRY_HAIKU_KEY");

            var anthropicKey = customApiKey ?? _configuration["ANTHROPIC_API_KEY"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

            string systemPrompt = @"<role>
You are a customer support ticket classifier. Your job is to analyze incoming
messages and determine the correct category for routing.
</role>

<categories>
- billing: Payment issues, invoices, refunds, subscription changes, pricing questions
- technical: Bugs, errors, feature requests, integration issues, API problems
- account: Login issues, password resets, profile updates, account deletion, permissions
- general: General inquiries, feedback, complaints, anything that doesn't fit above
</categories>

<instructions>
Analyze the customer message and respond with ONLY a JSON object (no markdown fencing)
containing these fields:
- category: one of billing, technical, account, general
- confidence: a float between 0.0 and 1.0 indicating how confident you are
- reasoning: a brief explanation of why you chose this category
</instructions>";

            string userMessage = $"<message>{message}</message>";

            if (!string.IsNullOrEmpty(azureUrl) && !string.IsNullOrEmpty(azureKey))
            {
                _logger.LogInformation("Calling Azure AI Foundry Claude Haiku endpoint...");
                return await CallAzureFoundryAsync(azureUrl, azureKey, systemPrompt, userMessage);
            }
            else if (!string.IsNullOrEmpty(anthropicKey))
            {
                _logger.LogInformation("Calling direct Anthropic Claude Haiku API...");
                return await CallDirectAnthropicAsync(anthropicKey, systemPrompt, userMessage);
            }
            else
            {
                _logger.LogWarning("No AI configuration found for classification. Defaulting to 'general'.");
                return new ClassificationResult
                {
                    Category = "general",
                    Confidence = 0.0,
                    Reasoning = "AI config missing."
                };
            }
        }

        private async Task<ClassificationResult> CallAzureFoundryAsync(string url, string apiKey, string systemPrompt, string userMessage)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("api-key", apiKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                object requestBody;

                // Support both OpenAI-style Chat Completions or custom model inference format
                if (url.Contains("/chat/completions"))
                {
                    requestBody = new
                    {
                        messages = new[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = userMessage }
                        },
                        max_tokens = 256,
                        temperature = 0.0
                    };
                }
                else
                {
                    // Anthropic format deployed via Azure MaaS (Model-as-a-Service)
                    requestBody = new
                    {
                        model = _configuration["CLAUDE_HAIKU_MODEL"] ?? "claude-3-haiku-20240307",
                        max_tokens = 256,
                        system = systemPrompt,
                        messages = new[]
                        {
                            new { role = "user", content = userMessage }
                        }
                    };
                }

                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Azure AI Foundry classification failed: {StatusCode} - {Error}", response.StatusCode, err);
                    return Fallback("Classification failed on Azure endpoint.");
                }

                var responseString = await response.Content.ReadAsStringAsync();
                return ParseClassificationResponse(responseString, url.Contains("/chat/completions"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CallAzureFoundryAsync");
                return Fallback(ex.Message);
            }
        }

        private async Task<ClassificationResult> CallDirectAnthropicAsync(string apiKey, string systemPrompt, string userMessage)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");

                var requestBody = new
                {
                    model = _configuration["CLAUDE_HAIKU_MODEL"] ?? "claude-3-haiku-20240307",
                    max_tokens = 256,
                    system = systemPrompt,
                    messages = new[]
                    {
                        new { role = "user", content = userMessage }
                    }
                };

                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Anthropic API classification failed: {StatusCode} - {Error}", response.StatusCode, err);
                    return Fallback("Classification failed on Direct Anthropic.");
                }

                var responseString = await response.Content.ReadAsStringAsync();
                return ParseClassificationResponse(responseString, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CallDirectAnthropicAsync");
                return Fallback(ex.Message);
            }
        }

        private ClassificationResult ParseClassificationResponse(string json, bool isOpenAiStyle)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                string textContent = string.Empty;

                if (isOpenAiStyle)
                {
                    textContent = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? string.Empty;
                }
                else
                {
                    // Anthropic direct response format
                    textContent = doc.RootElement
                        .GetProperty("content")[0]
                        .GetProperty("text")
                        .GetString() ?? string.Empty;
                }

                textContent = textContent.Trim();

                // Strip markdown fencing if present (```json ... ```)
                if (textContent.StartsWith("```"))
                {
                    int firstLineBreak = textContent.IndexOf('\n');
                    if (firstLineBreak != -1)
                    {
                        textContent = textContent.Substring(firstLineBreak + 1);
                    }
                    else
                    {
                        textContent = textContent.Substring(3);
                    }

                    int lastFence = textContent.LastIndexOf("```");
                    if (lastFence != -1)
                    {
                        textContent = textContent.Substring(0, lastFence);
                    }
                    textContent = textContent.Trim();
                }

                using var parsedDoc = JsonDocument.Parse(textContent);
                var category = parsedDoc.RootElement.TryGetProperty("category", out var catProp) ? catProp.GetString() : "general";
                var confidence = parsedDoc.RootElement.TryGetProperty("confidence", out var confProp) ? (confProp.ValueKind == JsonValueKind.Number ? confProp.GetDouble() : double.Parse(confProp.GetString() ?? "0.5")) : 0.5;
                var reasoning = parsedDoc.RootElement.TryGetProperty("reasoning", out var reasonProp) ? reasonProp.GetString() : "";

                return new ClassificationResult
                {
                    Category = category ?? "general",
                    Confidence = confidence,
                    Reasoning = reasoning ?? ""
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse classification payload. Raw json: {Raw}", json);
                return Fallback("Payload parsing failed.");
            }
        }

        private ClassificationResult Fallback(string errMessage)
        {
            return new ClassificationResult
            {
                Category = "general",
                Confidence = 0.0,
                Reasoning = $"Classification error: {errMessage}. Defaulting to general."
            };
        }
    }
}
