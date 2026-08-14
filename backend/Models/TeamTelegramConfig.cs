using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("team_telegram_configs")]
    public class TeamTelegramConfig
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("team_id")]
        public Guid TeamId { get; set; }

        [ForeignKey(nameof(TeamId))]
        public Team? Team { get; set; }

        [Required]
        [Column("bot_token")]
        public string BotToken { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("bot_username")]
        public string BotUsername { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
