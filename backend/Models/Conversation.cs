using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("conversations")]
    public class Conversation
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(20)]
        [Column("channel")]
        public string Channel { get; set; } = "webchat"; // "whatsapp", "email", "webchat", "telegram", "messenger", "instagram"

        [Required]
        [MaxLength(255)]
        [Column("sender_id")]
        public string SenderId { get; set; } = string.Empty;

        [MaxLength(255)]
        [Column("sender_name")]
        public string SenderName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "active"; // "active", "escalated", "resolved"

        [MaxLength(255)]
        [Column("assigned_agent")]
        public string AssignedAgent { get; set; } = string.Empty;

        [Column("human_only")]
        public bool HumanOnly { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public ICollection<ConversationTag> ConversationTags { get; set; } = new List<ConversationTag>();
        public ICollection<InternalNote> InternalNotes { get; set; } = new List<InternalNote>();
        public ICollection<Escalation> Escalations { get; set; } = new List<Escalation>();
    }
}
