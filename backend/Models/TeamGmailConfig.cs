using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("team_gmail_configs")]
    public class TeamGmailConfig
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("team_id")]
        public Guid TeamId { get; set; }

        [ForeignKey(nameof(TeamId))]
        public Team? Team { get; set; }

        [MaxLength(255)]
        [Column("google_client_id")]
        public string GoogleClientId { get; set; } = string.Empty;

        [Column("google_client_secret")]
        public string GoogleClientSecret { get; set; } = string.Empty;

        [Column("credentials_json")]
        public string CredentialsJson { get; set; } = string.Empty;

        [MaxLength(255)]
        [Column("watch_email")]
        public string WatchEmail { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = false;

        [Column("last_poll_at")]
        public DateTime? LastPollAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
