using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public class EscalationController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;

        public EscalationController(ApplicationDbContext db, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
        }

        // ---------------------------------------------------------------------------
        // List Escalations: GET /api/escalations/
        // ---------------------------------------------------------------------------

        [HttpGet("escalations")]
        public async Task<IActionResult> ListEscalations([FromQuery] string? resolved)
        {
            var query = _db.Escalations.Include(e => e.Conversation).AsQueryable();

            if (!string.IsNullOrEmpty(resolved))
            {
                bool isResolved = resolved.ToLower() == "true" || resolved == "1" || resolved.ToLower() == "yes";
                query = query.Where(e => e.Resolved == isResolved);
            }

            var list = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();

            var result = list.Select(e => new
            {
                id = e.Id,
                reason = e.Reason,
                details = e.Details,
                ai_summary = e.AiSummary,
                suggested_response = e.SuggestedResponse,
                resolved = e.Resolved,
                resolved_by = e.ResolvedBy,
                created_at = e.CreatedAt,
                resolved_at = e.ResolvedAt,
                conversation = e.Conversation != null ? new
                {
                    id = e.Conversation.Id,
                    channel = e.Conversation.Channel,
                    sender_id = e.Conversation.SenderId,
                    sender_name = e.Conversation.SenderName,
                    status = e.Conversation.Status
                } : null
            });

            return Ok(result);
        }

        // ---------------------------------------------------------------------------
        // Get Escalation Detail: GET /api/escalations/{id}/
        // ---------------------------------------------------------------------------

        [HttpGet("escalations/{id}")]
        public async Task<IActionResult> GetEscalationDetail(Guid id)
        {
            var escalation = await _db.Escalations
                .Include(e => e.Conversation)
                    .ThenInclude(c => c!.Messages)
                .Include(e => e.Conversation)
                    .ThenInclude(c => c!.ConversationTags)
                        .ThenInclude(ct => ct.Tag)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (escalation == null)
            {
                return NotFound(new { error = "Escalation not found." });
            }

            var c = escalation.Conversation;

            return Ok(new
            {
                id = escalation.Id,
                reason = escalation.Reason,
                details = escalation.Details,
                ai_summary = escalation.AiSummary,
                suggested_response = escalation.SuggestedResponse,
                resolved = escalation.Resolved,
                resolved_by = escalation.ResolvedBy,
                created_at = escalation.CreatedAt,
                resolved_at = escalation.ResolvedAt,
                conversation = c != null ? new
                {
                    id = c.Id,
                    channel = c.Channel,
                    sender_id = c.SenderId,
                    sender_name = c.SenderName,
                    status = c.Status,
                    messages = c.Messages.OrderBy(m => m.CreatedAt).Select(m => new
                    {
                        id = m.Id,
                        role = m.Role,
                        content = m.Content,
                        metadata = string.IsNullOrEmpty(m.MetadataJson) ? new object() : JsonSerializer.Deserialize<object>(m.MetadataJson),
                        created_at = m.CreatedAt
                    }),
                    tags = c.ConversationTags.Select(ct => new { id = ct.Tag!.Id, name = ct.Tag.Name, color = ct.Tag.Color })
                } : null
            });
        }

        // ---------------------------------------------------------------------------
        // Resolve Escalation: POST /api/escalations/{id}/resolve/
        // ---------------------------------------------------------------------------

        public class ResolveRequest
        {
            public string Agent_Name { get; set; } = string.Empty;
            public string Response { get; set; } = string.Empty;
        }

        [HttpPost("escalations/{id}/resolve")]
        public async Task<IActionResult> ResolveEscalation(Guid id, [FromBody] ResolveRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Agent_Name) || string.IsNullOrWhiteSpace(req.Response))
            {
                return BadRequest(new { error = "Agent name and response are required." });
            }

            var escalation = await _db.Escalations
                .Include(e => e.Conversation)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (escalation == null)
            {
                return NotFound(new { error = "Escalation not found." });
            }

            if (escalation.Resolved)
            {
                return BadRequest(new { error = "This escalation has already been resolved." });
            }

            var now = DateTime.UtcNow;

            escalation.Resolved = true;
            escalation.ResolvedBy = req.Agent_Name.Trim();
            escalation.ResolvedAt = now;

            // Save the agent message in the conversation
            var message = new Message
            {
                ConversationId = escalation.ConversationId,
                Role = "agent",
                Content = req.Response.Trim(),
                MetadataJson = JsonSerializer.Serialize(new { agent_name = req.Agent_Name.Trim(), escalation_id = escalation.Id.ToString() }),
                CreatedAt = now
            };
            _db.Messages.Add(message);

            // Update conversation status
            if (escalation.Conversation != null)
            {
                escalation.Conversation.Status = "resolved";
                escalation.Conversation.AssignedAgent = req.Agent_Name.Trim();
                escalation.Conversation.UpdatedAt = now;
            }

            await _db.SaveChangesAsync();

            // Send reply to original channel (async background process / task)
            if (escalation.Conversation != null)
            {
                _ = SendToCustomerChannelAsync(escalation.Conversation, req.Response.Trim());
            }

            return Ok(new
            {
                message = "Escalation resolved successfully.",
                escalation_id = escalation.Id,
                resolved_by = req.Agent_Name.Trim(),
                resolved_at = now
            });
        }

        // ---------------------------------------------------------------------------
        // Manual Reply: POST /api/conversations/{id}/reply/
        // ---------------------------------------------------------------------------

        public class ReplyRequest
        {
            public string Message { get; set; } = string.Empty;
            public string Agent_Name { get; set; } = "Dashboard Agent";
        }

        [HttpPost("conversations/{id}/reply")]
        public async Task<IActionResult> ConversationReply(Guid id, [FromBody] ReplyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Message))
            {
                return BadRequest(new { error = "Message is required." });
            }

            var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found." });
            }

            var now = DateTime.UtcNow;

            var msg = new Message
            {
                ConversationId = conversation.Id,
                Role = "agent",
                Content = req.Message.Trim(),
                MetadataJson = JsonSerializer.Serialize(new { agent_name = req.Agent_Name }),
                CreatedAt = now
            };
            _db.Messages.Add(msg);

            conversation.UpdatedAt = now;
            await _db.SaveChangesAsync();

            // Send to channel
            bool sent = await SendToCustomerChannelAsync(conversation, req.Message.Trim());

            return Ok(new
            {
                message_id = msg.Id,
                sent = sent,
                content = msg.Content,
                created_at = msg.CreatedAt
            });
        }

        // ---------------------------------------------------------------------------
        // Dashboard Stats: GET /api/escalations/dashboard/stats/
        // ---------------------------------------------------------------------------

        [HttpGet("escalations/dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var now = DateTime.UtcNow;
            var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            var todayConversations = await _db.Conversations
                .Where(c => c.CreatedAt >= todayStart)
                .ToListAsync();

            int totalTicketsToday = todayConversations.Count;
            int escalatedToday = todayConversations.Count(c => c.Status == "escalated");
            int resolvedToday = todayConversations.Count(c => c.Status == "resolved");

            // AI Resolved: resolved today with NO escalations
            var resolvedIdsToday = todayConversations.Where(c => c.Status == "resolved").Select(c => c.Id).ToList();
            var escalatedConvIdsToday = await _db.Escalations
                .Where(e => resolvedIdsToday.Contains(e.ConversationId))
                .Select(e => e.ConversationId)
                .Distinct()
                .ToListAsync();

            int aiResolvedToday = resolvedIdsToday.Count(id => !escalatedConvIdsToday.Contains(id));
            int humanResolvedToday = escalatedConvIdsToday.Count;

            // Overall All-Time stats
            var allConversations = await _db.Conversations.ToListAsync();
            int totalTickets = allConversations.Count;
            int totalOpen = allConversations.Count(c => c.Status == "active" || c.Status == "escalated");
            int totalEscalated = allConversations.Count(c => c.Status == "escalated");
            int totalResolved = allConversations.Count(c => c.Status == "resolved");

            var allEscalatedConvIds = await _db.Escalations
                .Select(e => e.ConversationId)
                .Distinct()
                .ToListAsync();

            int allAiResolved = allConversations.Count(c => c.Status == "resolved" && !allEscalatedConvIds.Contains(c.Id));
            int allHumanResolved = allConversations.Count(c => c.Status == "resolved" && allEscalatedConvIds.Contains(c.Id));

            // Channel breakdowns
            var channelBreakdown = allConversations
                .GroupBy(c => c.Channel)
                .ToDictionary(g => g.Key, g => g.Count());

            // Average response time
            double? avgResponseTime = await CalculateAverageResponseTimeAsync(todayStart);

            // Week stats
            var weekStart = todayStart.AddDays(-7);
            int totalWeek = await _db.Conversations.CountAsync(c => c.CreatedAt >= weekStart);

            // Recent escalations
            var recentEscalated = await _db.Escalations
                .Include(e => e.Conversation)
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .ToListAsync();

            var recentEscalationsFormatted = recentEscalated.Select(e => new
            {
                id = e.Id,
                reason = e.Reason,
                status = e.Resolved ? "resolved" : "pending",
                created_at = e.CreatedAt,
                conversation_id = e.ConversationId,
                customer_name = e.Conversation != null ? e.Conversation.SenderName : ""
            });

            return Ok(new
            {
                total_tickets_today = totalTicketsToday,
                total_tickets = totalTickets,
                total_open = totalOpen,
                total_escalated = totalEscalated,
                total_resolved = totalResolved,
                ai_resolved = allAiResolved,
                human_resolved = allHumanResolved,
                escalated = escalatedToday,
                avg_response_time = avgResponseTime,
                avg_response_time_ai = avgResponseTime,
                avg_response_time_human = (double?)null,
                channel_breakdown = channelBreakdown,
                channels = channelBreakdown,
                total_today = totalTicketsToday,
                total_week = totalWeek,
                recent_escalations = recentEscalationsFormatted
            });
        }

        // ---------------------------------------------------------------------------
        // Background Sender & Helper Methods
        // ---------------------------------------------------------------------------

        private async Task<double?> CalculateAverageResponseTimeAsync(DateTime since)
        {
            try
            {
                var customerMessages = await _db.Messages
                    .Include(m => m.Conversation)
                    .Where(m => m.Role == "customer" && m.Conversation!.CreatedAt >= since)
                    .OrderBy(m => m.ConversationId)
                    .ThenBy(m => m.CreatedAt)
                    .ToListAsync();

                double totalSeconds = 0;
                int count = 0;

                foreach (var msg in customerMessages)
                {
                    var nextResponse = await _db.Messages
                        .Where(m => m.ConversationId == msg.ConversationId && m.CreatedAt > msg.CreatedAt && (m.Role == "ai" || m.Role == "agent"))
                        .OrderBy(m => m.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (nextResponse != null)
                    {
                        var delta = (nextResponse.CreatedAt - msg.CreatedAt).TotalSeconds;
                        totalSeconds += delta;
                        count++;
                    }
                }

                if (count == 0) return null;
                return Math.Round(totalSeconds / count, 2);
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> SendToCustomerChannelAsync(Conversation conversation, string messageText)
        {
            try
            {
                if (conversation.Channel == "whatsapp")
                {
                    var config = await _db.TeamWhatsAppConfigs.FirstOrDefaultAsync();
                    if (config != null)
                    {
                        var client = _httpClientFactory.CreateClient();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
                        var body = new
                        {
                            messaging_product = "whatsapp",
                            to = conversation.SenderId,
                            type = "text",
                            text = new { body = messageText }
                        };
                        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
                        var response = await client.PostAsync($"https://graph.facebook.com/v22.0/{config.PhoneNumberId}/messages", content);
                        return response.IsSuccessStatusCode;
                    }
                }
                else if (conversation.Channel == "telegram")
                {
                    var config = await _db.TeamTelegramConfigs.FirstOrDefaultAsync();
                    if (config != null)
                    {
                        var client = _httpClientFactory.CreateClient();
                        var body = new { chat_id = conversation.SenderId, text = messageText };
                        var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
                        var response = await client.PostAsync($"https://api.telegram.org/bot{config.BotToken}/sendMessage", content);
                        return response.IsSuccessStatusCode;
                    }
                }
                // Email / Messenger stubs
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
