using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("teams")]
    public class Team
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(255)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Column("plan")]
        public string Plan { get; set; } = "free"; // "free", "pro", "enterprise"

        [Column("anthropic_api_key")]
        public string AnthropicApiKey { get; set; } = string.Empty;

        [Column("openai_api_key")]
        public string OpenaiApiKey { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
