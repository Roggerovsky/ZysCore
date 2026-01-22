using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordAutomation.API.Models
{
    public class Guild
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public ulong Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? OwnerId { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime? LeftAt { get; set; }
        
        [MaxLength(50)]
        public string? IconUrl { get; set; }
        
        [MaxLength(20)]
        public string PremiumTier { get; set; } = "Free"; // Free, Premium, Enterprise
        
        public DateTime? PremiumExpiresAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<Module> Modules { get; set; } = new List<Module>();
        public virtual ICollection<Rule> Rules { get; set; } = new List<Rule>();
        public virtual ICollection<LogEntry> Logs { get; set; } = new List<LogEntry>();
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}