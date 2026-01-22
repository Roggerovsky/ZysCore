using System.ComponentModel.DataAnnotations;

namespace DiscordAutomation.API.DTOs.Requests
{
    public class CreateGuildRequest
    {
        [Required]
        public ulong Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        public string? OwnerId { get; set; }
        
        public string? IconUrl { get; set; }
    }

    public class UpdateGuildRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        
        public string PremiumTier { get; set; } = "Free";
    }

    public class CreateModuleRequest
    {
        [Required]
        public int Type { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;
        
        public string Configuration { get; set; } = "{}";
    }

    public class CreateRuleRequest
    {
        [Required]
        public int ModuleId { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public int TriggerType { get; set; }

        [Required]
        public string TriggerConditions { get; set; } = "{}";

        [Required]
        public int ActionType { get; set; }

        [Required]
        public string ActionParameters { get; set; } = "{}";

        public bool IsEnabled { get; set; } = true;
        
        public bool IsPremiumOnly { get; set; } = false;
        
        public int? CooldownSeconds { get; set; }
        
        public int Priority { get; set; } = 100;
    }
}