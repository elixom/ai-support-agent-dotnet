using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("team_memberships")]
    public class TeamMembership
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [Required]
        [Column("team_id")]
        public Guid TeamId { get; set; }

        [ForeignKey(nameof(TeamId))]
        public Team? Team { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("role")]
        public string Role { get; set; } = "agent"; // "owner", "admin", "agent"

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
