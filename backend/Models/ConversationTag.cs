using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("conversation_tags")]
    public class ConversationTag
    {
        [Column("conversation_id")]
        public Guid ConversationId { get; set; }

        [ForeignKey(nameof(ConversationId))]
        public Conversation? Conversation { get; set; }

        [Column("tag_id")]
        public Guid TagId { get; set; }

        [ForeignKey(nameof(TagId))]
        public Tag? Tag { get; set; }
    }
}
