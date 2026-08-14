using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("escalations")]
    public class Escalation
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
        [MaxLength(30)]
        [Column("reason")]
        public string Reason { get; set; } = string.Empty; // "low_confidence", "negative_sentiment", "customer_request", "guardrail_flag"

        [Column("details")]
        public string Details { get; set; } = string.Empty;

        [Column("ai_summary")]
        public string AiSummary { get; set; } = string.Empty;

        [Column("suggested_response")]
        public string SuggestedResponse { get; set; } = string.Empty;

        [Column("resolved")]
        public bool Resolved { get; set; } = false;

        [MaxLength(100)]
        [Column("resolved_by")]
        public string ResolvedBy { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("resolved_at")]
        public DateTime? ResolvedAt { get; set; }
    }
}
