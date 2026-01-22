using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DiscordAutomation.API.Models
{
    public enum RuleActionType
    {
        Warn = 1,
        Mute = 2,
        Kick = 3,
        Ban = 4,
        DeleteMessage = 5,
        AddRole = 6,
        RemoveRole = 7,
        LogOnly = 8
    }

    public enum RuleTriggerType
    {
        MessageContent = 1,
        UserJoin = 2,
        UserLeave = 3,
        ReactionAdded = 4,
        Username = 5,
        InvitePosted = 6,
        SpamDetection = 7,
        AI_Moderation = 8
    }

    public class Rule
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public ulong GuildId { get; set; }

        [Required]
        public int ModuleId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsEnabled { get; set; } = true;
        
        public bool IsPremiumOnly { get; set; } = false;

        [Required]
        public RuleTriggerType TriggerType { get; set; }

        [Column(TypeName = "jsonb")]
        public string TriggerConditions { get; set; } = "{}";

        [Required]
        public RuleActionType ActionType { get; set; }

        [Column(TypeName = "jsonb")]
        public string ActionParameters { get; set; } = "{}";

        public int? CooldownSeconds { get; set; }

        public int Priority { get; set; } = 100;

        // Navigation properties
        [ForeignKey("GuildId")]
        public virtual Guild Guild { get; set; } = null!;

        [ForeignKey("ModuleId")]
        public virtual Module Module { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Helper methods for JSON fields
        public T? GetTriggerConditions<T>() where T : class
        {
            return string.IsNullOrEmpty(TriggerConditions) 
                ? null 
                : JsonSerializer.Deserialize<T>(TriggerConditions);
        }

        public void SetTriggerConditions<T>(T conditions) where T : class
        {
            TriggerConditions = JsonSerializer.Serialize(conditions);
        }

        public T? GetActionParameters<T>() where T : class
        {
            return string.IsNullOrEmpty(ActionParameters) 
                ? null 
                : JsonSerializer.Deserialize<T>(ActionParameters);
        }

        public void SetActionParameters<T>(T parameters) where T : class
        {
            ActionParameters = JsonSerializer.Serialize(parameters);
        }
    }
}