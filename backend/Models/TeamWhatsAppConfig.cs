using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("team_whatsapp_configs")]
    public class TeamWhatsAppConfig
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
        [MaxLength(255)]
        [Column("phone_number_id")]
        public string PhoneNumberId { get; set; } = string.Empty;

        [Required]
        [Column("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("verify_token")]
        public string VerifyToken { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
