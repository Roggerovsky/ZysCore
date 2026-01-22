using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordAutomation.API.Models
{
    public enum LogSeverity
    {
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    public class LogEntry
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public ulong GuildId { get; set; }

        [MaxLength(100)]
        public string? UserId { get; set; }

        [MaxLength(100)]
        public string? ChannelId { get; set; }

        [Required]
        public LogSeverity Severity { get; set; }

        [Required]
        [MaxLength(100)]
        public string Source { get; set; } = string.Empty; // "Moderation", "Tickets", etc.

        [Required]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        [Column(TypeName = "jsonb")]
        public string? Details { get; set; }

        public Guid? RelatedRuleId { get; set; }

        [MaxLength(50)]
        public string? ActionTaken { get; set; }

        // Navigation property
        [ForeignKey("GuildId")]
        public virtual Guild Guild { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}