using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordAutomation.API.Models
{
    public enum ModuleType
    {
        Moderation = 1,
        Tickets = 2,
        Welcome = 3,
        Logging = 4,
        AutoRoles = 5
    }

    public class Module
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public ulong GuildId { get; set; }

        [Required]
        public ModuleType Type { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        [Column(TypeName = "jsonb")]
        public string Configuration { get; set; } = "{}";

        // Navigation property
        [ForeignKey("GuildId")]
        public virtual Guild Guild { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}