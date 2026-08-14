using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EmbeddingService> _logger;

        public EmbeddingService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<EmbeddingService> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<List<float>> GenerateEmbeddingAsync(string text, string? customApiKey = null)
        {
            var apiKey = customApiKey ?? _configuration["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("OPENAI_API_KEY not set — using pseudo-embeddings (not suitable for production)");
                return GeneratePseudoEmbedding(text);
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var requestBody = new
                {
                    model = "text-embedding-3-small",
                    input = text
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://api.openai.com/v1/embeddings", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    var embeddingElement = doc.RootElement
                        .GetProperty("data")[0]
                        .GetProperty("embedding");

                    var result = new List<float>();
                    foreach (var item in embeddingElement.EnumerateArray())
                    {
                        result.Add(item.GetSingle());
                    }
                    return result;
                }
                else
                {
                    _logger.LogError("OpenAI embedding failed with status {StatusCode}, falling back to pseudo-embeddings", response.StatusCode);
                    return GeneratePseudoEmbedding(text);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI embedding generation threw exception, falling back to pseudo-embeddings");
                return GeneratePseudoEmbedding(text);
            }
        }

        private List<float> GeneratePseudoEmbedding(string text)
        {
            // Deterministic pseudo-embedding from text hash. Dev/testing only.
            var digest = ComputeSha512Hash(text);
            var extended = digest;
            while (extended.Length < 1536 * 2)
            {
                extended += ComputeSha512Hash(extended);
            }

            var raw = new List<float>();
            for (int i = 0; i < 1536; i++)
            {
                var byteStr = extended.Substring(i * 2, 2);
                var byteVal = Convert.ToInt32(byteStr, 16);
                raw.Add((byteVal / 255.0f) * 2.0f - 1.0f);
            }

            double sumOfSquares = 0;
            foreach (var val in raw)
            {
                sumOfSquares += val * val;
            }

            var magnitude = Math.Sqrt(sumOfSquares);
            if (magnitude > 0)
            {
                for (int i = 0; i < raw.Count; i++)
                {
                    raw[i] = (float)(raw[i] / magnitude);
                }
            }

            return raw;
        }

        private string ComputeSha512Hash(string text)
        {
            using var sha512 = SHA512.Create();
            var bytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = sha512.ComputeHash(bytes);
            var sb = new StringBuilder();
            foreach (var b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        public List<string> ChunkText(string text, int chunkSize = 500, int overlap = 50)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            text = text.Trim();

            if (text.Length <= chunkSize)
            {
                return new List<string> { text };
            }

            var chunks = new List<string>();
            int start = 0;

            while (start < text.Length)
            {
                int end = start + chunkSize;

                if (end < text.Length)
                {
                    string[] separators = { ". ", ".\n", "\n\n", "\n", " " };
                    foreach (var sep in separators)
                    {
                        int boundary = text.LastIndexOf(sep, Math.Min(text.Length - 1, start + chunkSize), chunkSize / 2);
                        if (boundary != -1 && boundary > start)
                        {
                            end = boundary + sep.Length;
                            break;
                        }
                    }
                }

                if (end > text.Length)
                {
                    end = text.Length;
                }

                var chunk = text.Substring(start, end - start).Trim();
                if (!string.IsNullOrEmpty(chunk))
                {
                    chunks.Add(chunk);
                }

                start = end - overlap;
                if (start >= text.Length || end >= text.Length)
                {
                    break;
                }
            }

            return chunks;
        }
    }
}
