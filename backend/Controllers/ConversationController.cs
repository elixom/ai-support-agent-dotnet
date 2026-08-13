using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;

namespace backend.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public class ConversationController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IConversationService _conversationService;

        public ConversationController(ApplicationDbContext db, IConversationService conversationService)
        {
            _db = db;
            _conversationService = conversationService;
        }

        // ---------------------------------------------------------------------------
        // List Conversations
        // ---------------------------------------------------------------------------

        [HttpGet("conversations")]
        public async Task<IActionResult> ListConversations()
        {
            var list = await _db.Conversations
                .Include(c => c.ConversationTags)
                    .ThenInclude(ct => ct.Tag)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            var result = list.Select(c => new
            {
                id = c.Id,
                channel = c.Channel,
                sender_id = c.SenderId,
                sender_name = c.SenderName,
                status = c.Status,
                assigned_agent = c.AssignedAgent,
                human_only = c.HumanOnly,
                created_at = c.CreatedAt,
                updated_at = c.UpdatedAt,
                tags = c.ConversationTags.Select(ct => new { id = ct.Tag!.Id, name = ct.Tag.Name, color = ct.Tag.Color })
            });

            return Ok(result);
        }

        // ---------------------------------------------------------------------------
        // Get Conversation Detail with Messages, Tags, Internal Notes
        // ---------------------------------------------------------------------------

        [HttpGet("conversations/{id}")]
        public async Task<IActionResult> GetConversationDetail(Guid id)
        {
            var conversation = await _db.Conversations
                .Include(c => c.Messages)
                .Include(c => c.InternalNotes)
                .Include(c => c.ConversationTags)
                    .ThenInclude(ct => ct.Tag)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found." });
            }

            var messagesOrdered = conversation.Messages.OrderBy(m => m.CreatedAt).Select(m => new
            {
                id = m.Id,
                role = m.Role,
                content = m.Content,
                metadata = string.IsNullOrEmpty(m.MetadataJson) ? new object() : JsonSerializer.Deserialize<object>(m.MetadataJson),
                created_at = m.CreatedAt
            });

            var notesOrdered = conversation.InternalNotes.OrderByDescending(n => n.CreatedAt).Select(n => new
            {
                id = n.Id,
                author_name = n.AuthorName,
                content = n.Content,
                created_at = n.CreatedAt
            });

            var tags = conversation.ConversationTags.Select(ct => new
            {
                id = ct.Tag!.Id,
                name = ct.Tag.Name,
                color = ct.Tag.Color
            });

            return Ok(new
            {
                id = conversation.Id,
                channel = conversation.Channel,
                sender_id = conversation.SenderId,
                sender_name = conversation.SenderName,
                status = conversation.Status,
                assigned_agent = conversation.AssignedAgent,
                human_only = conversation.HumanOnly,
                created_at = conversation.CreatedAt,
                updated_at = conversation.UpdatedAt,
                messages = messagesOrdered,
                internal_notes = notesOrdered,
                tags = tags
            });
        }

        // ---------------------------------------------------------------------------
        // Toggle Human Only Mode
        // ---------------------------------------------------------------------------

        public class ToggleHumanOnlyRequest
        {
            public bool? Human_Only { get; set; }
        }

        [HttpPost("conversations/{id}/toggle-human-only")]
        [AllowAnonymous]
        public async Task<IActionResult> ToggleHumanOnly(Guid id, [FromBody] ToggleHumanOnlyRequest? req)
        {
            var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found." });
            }

            if (req != null && req.Human_Only.HasValue)
            {
                conversation.HumanOnly = req.Human_Only.Value;
            }
            else
            {
                conversation.HumanOnly = !conversation.HumanOnly;
            }

            conversation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                conversation_id = conversation.Id,
                human_only = conversation.HumanOnly
            });
        }

        // ---------------------------------------------------------------------------
        // Internal Notes: Create
        // ---------------------------------------------------------------------------

        public class InternalNoteRequest
        {
            public string Author_Name { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }

        [HttpPost("conversations/{id}/notes")]
        public async Task<IActionResult> CreateInternalNote(Guid id, [FromBody] InternalNoteRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Content))
            {
                return BadRequest(new { error = "Content is required." });
            }

            var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id);
            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found." });
            }

            var authorName = string.IsNullOrWhiteSpace(req.Author_Name) ? "Agent" : req.Author_Name.Trim();

            var note = new InternalNote
            {
                ConversationId = conversation.Id,
                AuthorName = authorName,
                Content = req.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _db.InternalNotes.Add(note);
            await _db.SaveChangesAsync();

            return StatusCode(201, new
            {
                id = note.Id,
                author_name = note.AuthorName,
                content = note.Content,
                created_at = note.CreatedAt
            });
        }

        // ---------------------------------------------------------------------------
        // Tags: List, Create, Delete
        // ---------------------------------------------------------------------------

        [HttpGet("tags")]
        public async Task<IActionResult> ListTags()
        {
            var tags = await _db.Tags.OrderBy(t => t.Name).ToListAsync();
            return Ok(tags.Select(t => new { id = t.Id, name = t.Name, color = t.Color, created_at = t.CreatedAt }));
        }

        public class TagRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Color { get; set; } = "#6366f1";
        }

        [HttpPost("tags")]
        public async Task<IActionResult> CreateTag([FromBody] TagRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return BadRequest(new { error = "Tag name is required." });
            }

            var nameNormalized = req.Name.Trim();
            var existing = await _db.Tags.FirstOrDefaultAsync(t => t.Name == nameNormalized);
            if (existing != null)
            {
                return BadRequest(new { error = "Tag with this name already exists." });
            }

            var tag = new Tag
            {
                Name = nameNormalized,
                Color = string.IsNullOrWhiteSpace(req.Color) ? "#6366f1" : req.Color.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            return StatusCode(201, new { id = tag.Id, name = tag.Name, color = tag.Color, created_at = tag.CreatedAt });
        }

        [HttpDelete("tags/{id}")]
        public async Task<IActionResult> DeleteTag(Guid id)
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id);
            if (tag == null)
            {
                return NotFound(new { error = "Tag not found." });
            }

            _db.Tags.Remove(tag);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------------------------------------------------------------------------
        // Canned Responses: List, Create, Update, Delete
        // ---------------------------------------------------------------------------

        [HttpGet("canned-responses")]
        public async Task<IActionResult> ListCannedResponses()
        {
            var responses = await _db.CannedResponses
                .Where(r => r.IsActive)
                .OrderBy(r => r.Title)
                .ToListAsync();

            return Ok(responses.Select(r => new
            {
                id = r.Id,
                title = r.Title,
                content = r.Content,
                category = r.Category,
                shortcut = r.Shortcut,
                is_active = r.IsActive,
                created_at = r.CreatedAt,
                updated_at = r.UpdatedAt
            }));
        }

        public class CannedResponseRequest
        {
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Shortcut { get; set; } = string.Empty;
        }

        [HttpPost("canned-responses")]
        public async Task<IActionResult> CreateCannedResponse([FromBody] CannedResponseRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Content))
            {
                return BadRequest(new { error = "Title and Content are required." });
            }

            if (!string.IsNullOrEmpty(req.Shortcut))
            {
                var exists = await _db.CannedResponses.AnyAsync(r => r.Shortcut == req.Shortcut.Trim());
                if (exists) return BadRequest(new { error = "A canned response with this shortcut already exists." });
            }

            var canned = new CannedResponse
            {
                Title = req.Title.Trim(),
                Content = req.Content.Trim(),
                Category = req.Category?.Trim() ?? string.Empty,
                Shortcut = req.Shortcut?.Trim() ?? string.Empty,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.CannedResponses.Add(canned);
            await _db.SaveChangesAsync();

            return StatusCode(201, canned);
        }

        [HttpGet("canned-responses/{id}")]
        public async Task<IActionResult> GetCannedResponse(Guid id)
        {
            var canned = await _db.CannedResponses.FirstOrDefaultAsync(r => r.Id == id);
            if (canned == null) return NotFound();
            return Ok(canned);
        }

        [HttpPut("canned-responses/{id}")]
        public async Task<IActionResult> UpdateCannedResponse(Guid id, [FromBody] CannedResponseRequest req)
        {
            var canned = await _db.CannedResponses.FirstOrDefaultAsync(r => r.Id == id);
            if (canned == null) return NotFound();

            if (!string.IsNullOrEmpty(req.Shortcut) && req.Shortcut.Trim() != canned.Shortcut)
            {
                var exists = await _db.CannedResponses.AnyAsync(r => r.Shortcut == req.Shortcut.Trim() && r.Id != id);
                if (exists) return BadRequest(new { error = "A canned response with this shortcut already exists." });
            }

            canned.Title = req.Title?.Trim() ?? canned.Title;
            canned.Content = req.Content?.Trim() ?? canned.Content;
            canned.Category = req.Category?.Trim() ?? canned.Category;
            canned.Shortcut = req.Shortcut?.Trim() ?? canned.Shortcut;
            canned.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(canned);
        }

        [HttpDelete("canned-responses/{id}")]
        public async Task<IActionResult> DeleteCannedResponse(Guid id)
        {
            var canned = await _db.CannedResponses.FirstOrDefaultAsync(r => r.Id == id);
            if (canned == null) return NotFound();

            _db.CannedResponses.Remove(canned);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ---------------------------------------------------------------------------
        // Bulk Actions: Tag or Resolve
        // ---------------------------------------------------------------------------

        public class BulkActionRequest
        {
            public List<Guid> Conversation_Ids { get; set; } = new List<Guid>();
            public string Action { get; set; } = string.Empty; // "resolve" or "tag"
            public Guid? Tag_Id { get; set; }
        }

        [HttpPost("bulk-actions")]
        public async Task<IActionResult> BulkAction([FromBody] BulkActionRequest req)
        {
            if (req.Conversation_Ids == null || req.Conversation_Ids.Count == 0)
            {
                return BadRequest(new { error = "conversation_ids is required." });
            }

            if (req.Action != "resolve" && req.Action != "tag")
            {
                return BadRequest(new { error = "action must be 'resolve' or 'tag'." });
            }

            var conversations = await _db.Conversations
                .Where(c => req.Conversation_Ids.Contains(c.Id))
                .ToListAsync();

            if (conversations.Count == 0)
            {
                return NotFound(new { error = "No matching conversations found." });
            }

            if (req.Action == "resolve")
            {
                foreach (var c in conversations)
                {
                    c.Status = "resolved";
                    c.UpdatedAt = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync();
                return Ok(new { message = $"Resolved {conversations.Count} conversation(s)." });
            }

            if (req.Action == "tag")
            {
                if (!req.Tag_Id.HasValue)
                {
                    return BadRequest(new { error = "tag_id is required for 'tag' action." });
                }

                var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == req.Tag_Id.Value);
                if (tag == null)
                {
                    return NotFound(new { error = "Tag not found." });
                }

                int createdCount = 0;
                foreach (var c in conversations)
                {
                    var exists = await _db.ConversationTags.AnyAsync(ct => ct.ConversationId == c.Id && ct.TagId == tag.Id);
                    if (!exists)
                    {
                        _db.ConversationTags.Add(new ConversationTag { ConversationId = c.Id, TagId = tag.Id });
                        createdCount++;
                    }
                }

                await _db.SaveChangesAsync();
                return Ok(new
                {
                    message = $"Tagged {createdCount} conversation(s) with '{tag.Name}'.",
                    already_tagged = conversations.Count - createdCount
                });
            }

            return BadRequest();
        }

        // ---------------------------------------------------------------------------
        // Search Conversations
        // ---------------------------------------------------------------------------

        [HttpGet("search")]
        public async Task<IActionResult> SearchConversations([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { error = "Search query parameter 'q' is required." });
            }

            var queryLower = q.Trim().ToLower();

            var matchedConversationIdsByMessage = await _db.Messages
                .Where(m => m.Content.Contains(queryLower))
                .Select(m => m.ConversationId)
                .Distinct()
                .ToListAsync();

            var matchedConversationIdsBySender = await _db.Conversations
                .Where(c => c.SenderName.Contains(queryLower))
                .Select(c => c.Id)
                .Distinct()
                .ToListAsync();

            var allMatchedIds = matchedConversationIdsByMessage.Union(matchedConversationIdsBySender).ToList();

            var conversations = await _db.Conversations
                .Where(c => allMatchedIds.Contains(c.Id))
                .Include(c => c.ConversationTags)
                    .ThenInclude(ct => ct.Tag)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            var result = conversations.Select(c => new
            {
                id = c.Id,
                channel = c.Channel,
                sender_id = c.SenderId,
                sender_name = c.SenderName,
                status = c.Status,
                assigned_agent = c.AssignedAgent,
                human_only = c.HumanOnly,
                created_at = c.CreatedAt,
                updated_at = c.UpdatedAt,
                tags = c.ConversationTags.Select(ct => new { id = ct.Tag!.Id, name = ct.Tag.Name, color = ct.Tag.Color })
            });

            return Ok(result);
        }

        // ---------------------------------------------------------------------------
        // Process Message Pipeline (Main orchestrator endpoint)
        // ---------------------------------------------------------------------------

        public class ProcessMessageRequest
        {
            public string Message { get; set; } = string.Empty;
            public string Sender_Id { get; set; } = string.Empty;
            public string Channel { get; set; } = "webchat";
            public string? Sender_Name { get; set; }
        }

        [HttpPost("process")]
        [AllowAnonymous]
        public async Task<IActionResult> ProcessMessage([FromBody] ProcessMessageRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Message) || string.IsNullOrWhiteSpace(req.Sender_Id))
            {
                return BadRequest(new { error = "message and sender_id are required." });
            }

            var result = await _conversationService.ProcessMessageAsync(req.Message, req.Sender_Id, req.Sender_Name, req.Channel);
            return Ok(result);
        }
    }
}
