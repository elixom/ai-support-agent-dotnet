using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("team_api_keys")]
    public class TeamAPIKey
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
        [MaxLength(64)]
        [Column("key_hash")]
        public string KeyHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(8)]
        [Column("prefix")]
        public string Prefix { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("last_used_at")]
        public DateTime? LastUsedAt { get; set; }
    }
}
