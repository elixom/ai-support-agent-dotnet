using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("team_messenger_configs")]
    public class TeamMessengerConfig
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
        [Column("page_access_token")]
        public string PageAccessToken { get; set; } = string.Empty;

        [MaxLength(255)]
        [Column("page_id")]
        public string PageId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("verify_token")]
        public string VerifyToken { get; set; } = string.Empty;

        [Column("instagram_enabled")]
        public bool InstagramEnabled { get; set; } = false;

        [Column("is_active")]
        public bool IsActive { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
