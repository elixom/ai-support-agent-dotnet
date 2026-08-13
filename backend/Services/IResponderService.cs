using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Services
{
    public class ChatHistoryEntry
    {
        public string Role { get; set; } = string.Empty; // "customer" / "ai" / "agent"
        public string Content { get; set; } = string.Empty;
    }

    public class ResponderResult
    {
        public string Response { get; set; } = string.Empty;
        public double Confidence { get; set; } = 0.9;
    }

    public interface IResponderService
    {
        Task<ResponderResult> GenerateResponseAsync(
            string message,
            List<ChatHistoryEntry> conversationHistory,
            List<string> knowledgeChunks,
            string? customApiKey = null);
    }
}
