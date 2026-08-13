using System;
using System.Collections.Generic;
using Xunit;
using backend.Services;

namespace backend.Tests
{
    public class GuardrailsServiceTests
    {
        [Fact]
        public void CheckResponse_ShouldMarkSafe_WhenNoSensitiveTermsMatched()
        {
            var service = new GuardrailsService();
            var response = "Hello! Welcome to our store. How can I help you today?";
            var chunks = new List<string> { "General welcome text" };

            var result = service.CheckResponse(response, chunks);

            Assert.True(result.IsSafe);
            Assert.Empty(result.FlaggedTerms);
        }

        [Fact]
        public void CheckResponse_ShouldFlagTerm_WhenSensitiveTermNotGrounded()
        {
            var service = new GuardrailsService();
            var response = "We offer a 30-day moneyback refund guarantee on all plans.";
            var chunks = new List<string> { "Standard subscription content" };

            var result = service.CheckResponse(response, chunks);

            Assert.False(result.IsSafe);
            Assert.Contains("refund policy", result.FlaggedTerms);
            Assert.Contains("guarantee/warranty", result.FlaggedTerms);
        }

        [Fact]
        public void CheckResponse_ShouldMarkSafe_WhenSensitiveTermIsGrounded()
        {
            var service = new GuardrailsService();
            var response = "We offer a 30-day moneyback refund guarantee on all plans.";
            var chunks = new List<string> { "Our policy: We offer a 30-day moneyback refund guarantee on all plans." };

            var result = service.CheckResponse(response, chunks);

            Assert.True(result.IsSafe);
            Assert.Empty(result.FlaggedTerms);
        }
    }

    public class EmbeddingServiceTests
    {
        [Fact]
        public void ChunkText_ShouldSplitTextCorrectly()
        {
            // Injecting mocks for IConfiguration, IHttpClientFactory, and ILogger is not needed for testing ChunkText
            var service = new EmbeddingService(null!, null!, null!);
            var text = "This is some long text that should be split up. " +
                       "We want to make sure it gets split correctly by the system. " +
                       "Overlaps should occur exactly as configured.";

            var chunks = service.ChunkText(text, chunkSize: 40, overlap: 5);

            Assert.NotEmpty(chunks);
            foreach (var chunk in chunks)
            {
                Assert.True(chunk.Length <= 40);
            }
        }
    }
}
