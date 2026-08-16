using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    public class KnowledgeBaseController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmbeddingService _embedding;

        public KnowledgeBaseController(ApplicationDbContext db, IEmbeddingService embedding)
        {
            _db = db;
            _embedding = embedding;
        }

        // ---------------------------------------------------------------------------
        // List Knowledge Base: GET /api/knowledge/
        // ---------------------------------------------------------------------------

        [HttpGet("knowledge")]
        public async Task<IActionResult> ListKnowledge()
        {
            var list = await _db.KnowledgeBases.OrderByDescending(kb => kb.CreatedAt).ToListAsync();
            return Ok(list.Select(kb => new
            {
                id = kb.Id,
                content = kb.Content,
                category = kb.Category,
                source_file = TryGetSourceFile(kb.MetadataJson),
                metadata = string.IsNullOrEmpty(kb.MetadataJson) ? new object() : JsonSerializer.Deserialize<object>(kb.MetadataJson),
                created_at = kb.CreatedAt,
                updated_at = kb.UpdatedAt
            }));
        }

        [HttpDelete("knowledge/{id}")]
        public async Task<IActionResult> DeleteKnowledge(Guid id)
        {
            var kb = await _db.KnowledgeBases.FirstOrDefaultAsync(x => x.Id == id);
            if (kb == null)
            {
                return NotFound(new { error = "Knowledge base entry not found." });
            }

            _db.KnowledgeBases.Remove(kb);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------------------------------------------------------------------------
        // Create Knowledge Base: POST /api/knowledge/
        // ---------------------------------------------------------------------------

        public class KnowledgeCreateRequest
        {
            public string Content { get; set; } = string.Empty;
            public string Category { get; set; } = "general";
        }

        [HttpPost("knowledge")]
        public async Task<IActionResult> CreateKnowledge([FromBody] KnowledgeCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Content))
            {
                return BadRequest(new { error = "Content is required." });
            }

            var embedding = await _embedding.GenerateEmbeddingAsync(req.Content);

            var kb = new KnowledgeBase
            {
                Content = req.Content.Trim(),
                Category = string.IsNullOrWhiteSpace(req.Category) ? "general" : req.Category.Trim().ToLower(),
                Embedding = embedding,
                MetadataJson = "{}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.KnowledgeBases.Add(kb);
            await _db.SaveChangesAsync();

            return StatusCode(201, new
            {
                id = kb.Id,
                content = kb.Content,
                category = kb.Category,
                created_at = kb.CreatedAt,
                updated_at = kb.UpdatedAt
            });
        }

        // ---------------------------------------------------------------------------
        // Upload File (chunk, embed, save): POST /api/knowledge-base/upload/
        // ---------------------------------------------------------------------------

        [HttpPost("knowledge-base/upload")]
        [AllowAnonymous] // allow upload from CLI/scripts too
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] string? category)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file provided." });
            }

            var categoryValue = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim().ToLower();
            string[] validCategories = { "billing", "technical", "account", "general" };

            if (!validCategories.Contains(categoryValue))
            {
                return BadRequest(new { error = $"category must be one of: {string.Join(", ", validCategories)}" });
            }

            try
            {
                string text;
                using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
                {
                    text = await reader.ReadToEndAsync();
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    return BadRequest(new { error = "File contains no extractable text." });
                }

                var chunks = _embedding.ChunkText(text);
                int entriesCreated = 0;

                foreach (var chunk in chunks)
                {
                    var embedding = await _embedding.GenerateEmbeddingAsync(chunk);
                    var kb = new KnowledgeBase
                    {
                        Content = chunk,
                        Category = categoryValue,
                        Embedding = embedding,
                        MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { source_file = file.FileName }),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.KnowledgeBases.Add(kb);
                    entriesCreated++;
                }

                await _db.SaveChangesAsync();

                return StatusCode(201, new
                {
                    message = $"Successfully processed '{file.FileName}'.",
                    chunks_created = entriesCreated,
                    category = categoryValue
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Failed to read file: {ex.Message}" });
            }
        }

        private static string? TryGetSourceFile(string? metadataJson)
        {
            if (string.IsNullOrWhiteSpace(metadataJson))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(metadataJson);
                if (doc.RootElement.TryGetProperty("source_file", out var sourceProp) && sourceProp.ValueKind == JsonValueKind.String)
                {
                    return sourceProp.GetString();
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}
