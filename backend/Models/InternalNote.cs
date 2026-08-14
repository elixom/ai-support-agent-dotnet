using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("internal_notes")]
    public class InternalNote
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("conversation_id")]
        public Guid ConversationId { get; set; }

        [ForeignKey(nameof(ConversationId))]
        public Conversation? Conversation { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("author_name")]
        public string AuthorName { get; set; } = string.Empty;

        [Required]
        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
