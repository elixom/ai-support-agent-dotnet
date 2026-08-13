using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace backend.Models
{
    [Table("knowledge_base")]
    public class KnowledgeBase
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Column("embedding_json")]
        public string EmbeddingJson { get; set; } = string.Empty;

        [NotMapped]
        public List<float> Embedding
        {
            get
            {
                if (string.IsNullOrEmpty(EmbeddingJson))
                    return new List<float>();
                try
                {
                    return JsonSerializer.Deserialize<List<float>>(EmbeddingJson) ?? new List<float>();
                }
                catch
                {
                    return new List<float>();
                }
            }
            set
            {
                EmbeddingJson = JsonSerializer.Serialize(value);
            }
        }

        [Required]
        [MaxLength(20)]
        [Column("category")]
        public string Category { get; set; } = "general"; // "billing", "technical", "account", "general"

        [Column("metadata_json")]
        public string MetadataJson { get; set; } = "{}";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
