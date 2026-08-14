using System;
using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Team> Teams { get; set; } = null!;
        public DbSet<TeamMembership> TeamMemberships { get; set; } = null!;
        public DbSet<TeamWhatsAppConfig> TeamWhatsAppConfigs { get; set; } = null!;
        public DbSet<TeamGmailConfig> TeamGmailConfigs { get; set; } = null!;
        public DbSet<TeamTelegramConfig> TeamTelegramConfigs { get; set; } = null!;
        public DbSet<TeamMessengerConfig> TeamMessengerConfigs { get; set; } = null!;
        public DbSet<TeamAPIKey> TeamAPIKeys { get; set; } = null!;
        public DbSet<KnowledgeBase> KnowledgeBases { get; set; } = null!;
        public DbSet<Conversation> Conversations { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<InternalNote> InternalNotes { get; set; } = null!;
        public DbSet<CannedResponse> CannedResponses { get; set; } = null!;
        public DbSet<Tag> Tags { get; set; } = null!;
        public DbSet<ConversationTag> ConversationTags { get; set; } = null!;
        public DbSet<Escalation> Escalations { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ConversationTag composite key
            modelBuilder.Entity<ConversationTag>()
                .HasKey(ct => new { ct.ConversationId, ct.TagId });

            modelBuilder.Entity<ConversationTag>()
                .HasOne(ct => ct.Conversation)
                .WithMany(c => c.ConversationTags)
                .HasForeignKey(ct => ct.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConversationTag>()
                .HasOne(ct => ct.Tag)
                .WithMany(t => t.ConversationTags)
                .HasForeignKey(ct => ct.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            // Filtered Unique Index on Conversation: Only active or escalated conversations are unique per sender/channel
            modelBuilder.Entity<Conversation>()
                .HasIndex(c => new { c.SenderId, c.Channel })
                .HasFilter("[status] IN ('active', 'escalated')")
                .IsUnique();

            // Additional standard indexes for fast lookup
            modelBuilder.Entity<Conversation>()
                .HasIndex(c => new { c.SenderId, c.Channel, c.Status });

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Team>()
                .HasIndex(t => t.Slug)
                .IsUnique();

            modelBuilder.Entity<TeamMembership>()
                .HasIndex(tm => new { tm.UserId, tm.TeamId })
                .IsUnique();

            modelBuilder.Entity<TeamWhatsAppConfig>()
                .HasIndex(tc => tc.TeamId)
                .IsUnique();

            modelBuilder.Entity<TeamGmailConfig>()
                .HasIndex(tc => tc.TeamId)
                .IsUnique();

            modelBuilder.Entity<TeamTelegramConfig>()
                .HasIndex(tc => tc.TeamId)
                .IsUnique();

            modelBuilder.Entity<TeamMessengerConfig>()
                .HasIndex(tc => tc.TeamId)
                .IsUnique();

            modelBuilder.Entity<TeamAPIKey>()
                .HasIndex(tk => tk.KeyHash)
                .IsUnique();

            modelBuilder.Entity<CannedResponse>()
                .HasIndex(cr => cr.Shortcut)
                .HasFilter("[shortcut] IS NOT NULL AND [shortcut] <> ''")
                .IsUnique();

            modelBuilder.Entity<Tag>()
                .HasIndex(t => t.Name)
                .IsUnique();
        }
    }
}
