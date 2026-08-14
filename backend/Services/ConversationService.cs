using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Services
{
    public class ConversationService : IConversationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IClassifierService _classifier;
        private readonly IResponderService _responder;
        private readonly IEmbeddingService _embedding;
        private readonly IGuardrailsService _guardrails;
        private readonly IHttpClientFactory _httpClientFactory;

        public ConversationService(
            ApplicationDbContext db,
            IClassifierService classifier,
            IResponderService responder,
            IEmbeddingService embedding,
            IGuardrailsService guardrails,
            IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _classifier = classifier;
            _responder = responder;
            _embedding = embedding;
            _guardrails = guardrails;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<object> ProcessMessageAsync(string messageText, string senderId, string? senderName, string channel)
        {
            // 1. Find existing open conversation or create a new one
            var conversation = await _db.Conversations
                .Where(c => c.SenderId == senderId && c.Channel == channel && c.Status != "resolved")
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    SenderId = senderId,
                    Channel = channel,
                    SenderName = !string.IsNullOrEmpty(senderName) ? senderName : senderId,
                    Status = "active"
                };
                _db.Conversations.Add(conversation);
                await _db.SaveChangesAsync();
            }

            // 2. Save customer message
            var customerMsg = new Message
            {
                ConversationId = conversation.Id,
                Role = "customer",
                Content = messageText,
                CreatedAt = DateTime.UtcNow
            };
            _db.Messages.Add(customerMsg);
            await _db.SaveChangesAsync();

            // 3. If human-only mode, skip AI pipeline
            if (conversation.HumanOnly)
            {
                return new
                {
                    conversation_id = conversation.Id.ToString(),
                    classification = new { category = "human_only", confidence = 1.0, reasoning = "Human-only mode enabled" },
                    escalated = false,
                    human_only = true,
                    response = (string?)null
                };
            }

            // 4. Classify with Haiku
            var classification = await _classifier.ClassifyTicketAsync(messageText);

            // 5. Check for escalation
            var escalationResult = ShouldEscalate(messageText, classification);

            if (escalationResult.ShouldEscalate)
            {
                conversation.Status = "escalated";
                conversation.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                // Create complete escalation handoff package
                await CreateHandoffPackageAsync(conversation.Id, escalationResult.Reason, escalationResult.Details);

                var aiResponse = "I understand this needs special attention. I'm connecting you with a human agent who can help further. Please hold on.";
                var aiMsg = new Message
                {
                    ConversationId = conversation.Id,
                    Role = "ai",
                    Content = aiResponse,
                    MetadataJson = JsonSerializer.Serialize(new { escalated = true, reason = escalationResult.Reason }),
                    CreatedAt = DateTime.UtcNow
                };
                _db.Messages.Add(aiMsg);
                await _db.SaveChangesAsync();

                return new
                {
                    conversation_id = conversation.Id.ToString(),
                    classification = classification,
                    escalated = true,
                    escalation_reason = escalationResult.Reason,
                    response = aiResponse
                };
            }

            // 6. Generate embedding and retrieve KB context
            var queryEmbedding = await _embedding.GenerateEmbeddingAsync(messageText);
            var knowledgeChunks = await SearchKnowledgeBaseAsync(queryEmbedding, classification.Category, 3);

            // 7. Build conversation history
            var history = await _db.Messages
                .Where(m => m.ConversationId == conversation.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatHistoryEntry { Role = m.Role, Content = m.Content })
                .ToListAsync();

            // Exclude the message we just saved
            if (history.Count > 0)
            {
                history.RemoveAt(history.Count - 1);
            }

            // 8. Generate response with Sonnet
            var responseResult = await _responder.GenerateResponseAsync(messageText, history, knowledgeChunks);

            // 9. Run guardrails
            var guardrailResult = _guardrails.CheckResponse(responseResult.Response, knowledgeChunks);

            // 10. Save AI response
            var finalAiMsg = new Message
            {
                ConversationId = conversation.Id,
                Role = "ai",
                Content = responseResult.Response,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    confidence = responseResult.Confidence,
                    guardrails = guardrailResult,
                    classification = classification
                }),
                CreatedAt = DateTime.UtcNow
            };
            _db.Messages.Add(finalAiMsg);

            conversation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return new
            {
                conversation_id = conversation.Id.ToString(),
                classification = classification,
                escalated = false,
                response = responseResult.Response,
                confidence = responseResult.Confidence,
                guardrails = guardrailResult
            };
        }

        // ---------------------------------------------------------------------------
        // Escalation Logic & Helpers
        // ---------------------------------------------------------------------------

        private static readonly string[] NegativeSentimentKeywords = {
            "frustrated", "angry", "ridiculous", "terrible", "worst", "unacceptable",
            "furious", "lawsuit", "complaint", "disappointed", "horrible", "awful",
            "incompetent", "useless", "scam", "fraud", "steal"
        };

        private static readonly string[] HumanRequestPhrases = {
            "talk to a human", "speak to a person", "real person", "speak to manager",
            "human agent", "customer service representative", "talk to someone",
            "speak to a human", "real agent", "talk to a manager", "speak with a manager",
            "speak with a human", "connect me to a human", "transfer to agent"
        };

        private class EscalationCheckResult
        {
            public bool ShouldEscalate { get; set; }
            public string Reason { get; set; } = string.Empty;
            public string Details { get; set; } = string.Empty;
        }

        private EscalationCheckResult ShouldEscalate(string message, ClassificationResult classification)
        {
            var messageLower = message.ToLower();

            // Check 1: Low confidence
            if (classification.Confidence < 0.7)
            {
                return new EscalationCheckResult
                {
                    ShouldEscalate = true,
                    Reason = "low_confidence",
                    Details = $"AI classification confidence is {classification.Confidence:F2}, below the 0.7 threshold. Category: {classification.Category}."
                };
            }

            // Check 2: Negative sentiment keywords
            var foundKeywords = NegativeSentimentKeywords.Where(kw => messageLower.Contains(kw)).ToList();
            if (foundKeywords.Count > 0)
            {
                return new EscalationCheckResult
                {
                    ShouldEscalate = true,
                    Reason = "negative_sentiment",
                    Details = $"Negative sentiment detected. Keywords found: {string.Join(", ", foundKeywords)}."
                };
            }

            // Check 3: Explicit request
            var foundPhrase = HumanRequestPhrases.FirstOrDefault(ph => messageLower.Contains(ph));
            if (foundPhrase != null)
            {
                return new EscalationCheckResult
                {
                    ShouldEscalate = true,
                    Reason = "customer_request",
                    Details = $"Customer explicitly requested a human agent: '{foundPhrase}'."
                };
            }

            return new EscalationCheckResult { ShouldEscalate = false };
        }

        private async Task<List<string>> SearchKnowledgeBaseAsync(List<float> queryEmbedding, string category, int limit)
        {
            var items = await _db.KnowledgeBases
                .Where(kb => kb.Category == category)
                .ToListAsync();

            if (items.Count == 0)
            {
                items = await _db.KnowledgeBases.ToListAsync();
            }

            var scoredList = new List<(KnowledgeBase Item, double Score)>();
            foreach (var item in items)
            {
                var itemVector = item.Embedding;
                if (itemVector.Count == queryEmbedding.Count)
                {
                    double dotProduct = 0;
                    for (int i = 0; i < itemVector.Count; i++)
                    {
                        dotProduct += itemVector[i] * queryEmbedding[i];
                    }
                    scoredList.Add((item, dotProduct));
                }
            }

            return scoredList
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => x.Item.Content)
                .ToList();
        }

        private async Task CreateHandoffPackageAsync(Guid conversationId, string reason, string details)
        {
            var conversation = await _db.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null) return;

            var historyStr = string.Join("\n", conversation.Messages.OrderBy(m => m.CreatedAt).Select(m => $"[{m.Role}] {m.Content}"));

            var summaryResult = "The customer requested assistance regarding their issue. Handoff triggered due to: " + reason + ".";
            var suggestedResponseResult = "Hello " + (!string.IsNullOrEmpty(conversation.SenderName) ? conversation.SenderName : "there") + ", I'm a human agent stepping in to assist you. Let me review your request and help you right away.";

            try
            {
                var responderResultSummary = await _responder.GenerateResponseAsync("Summarize the issue concisely.", new List<ChatHistoryEntry>(), new List<string> { historyStr });
                if (!string.IsNullOrEmpty(responderResultSummary.Response) && responderResultSummary.Response.Length > 10)
                {
                    summaryResult = responderResultSummary.Response;
                }

                var responderResultSuggested = await _responder.GenerateResponseAsync("Draft a suggested response for the human agent.", new List<ChatHistoryEntry>(), new List<string> { historyStr });
                if (!string.IsNullOrEmpty(responderResultSuggested.Response) && responderResultSuggested.Response.Length > 10)
                {
                    suggestedResponseResult = responderResultSuggested.Response;
                }
            }
            catch
            {
                // Use default if AI fails
            }

            var escalation = new Escalation
            {
                ConversationId = conversation.Id,
                Reason = reason,
                Details = details,
                AiSummary = summaryResult,
                SuggestedResponse = suggestedResponseResult,
                Resolved = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Escalations.Add(escalation);
            await _db.SaveChangesAsync();
        }
    }
}
