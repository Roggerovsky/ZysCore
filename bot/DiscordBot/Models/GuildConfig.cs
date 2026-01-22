using System.Text.Json.Serialization;

namespace DiscordAutomation.Bot.Models
{
    public class GuildConfig
    {
        [JsonPropertyName("guildId")]
        public ulong GuildId { get; set; }
        
        [JsonPropertyName("guildName")]
        public string GuildName { get; set; } = string.Empty;
        
        [JsonPropertyName("premiumTier")]
        public string PremiumTier { get; set; } = "Free";
        
        [JsonPropertyName("modules")]
        public Dictionary<string, bool> Modules { get; set; } = new();
        
        [JsonPropertyName("rules")]
        public List<CachedRule> Rules { get; set; } = new();
        
        [JsonPropertyName("settings")]
        public Dictionary<string, object> Settings { get; set; } = new();
        
        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        [JsonPropertyName("cacheExpiry")]
        public DateTime CacheExpiry { get; set; } = DateTime.UtcNow.AddMinutes(15);
    }

    public class CachedRule
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("module")]
        public string Module { get; set; } = string.Empty;
        
        [JsonPropertyName("triggerType")]
        public string TriggerType { get; set; } = string.Empty;
        
        [JsonPropertyName("triggerConditions")]
        public Dictionary<string, object> TriggerConditions { get; set; } = new();
        
        [JsonPropertyName("actionType")]
        public string ActionType { get; set; } = string.Empty;
        
        [JsonPropertyName("actionParameters")]
        public Dictionary<string, object> ActionParameters { get; set; } = new();
        
        [JsonPropertyName("isEnabled")]
        public bool IsEnabled { get; set; } = true;
        
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 100;
        
        [JsonPropertyName("cooldownSeconds")]
        public int? CooldownSeconds { get; set; }
        
        [JsonPropertyName("lastTriggered")]
        public DateTime? LastTriggered { get; set; }
    }

    public class DiscordEventContext
    {
        public ulong GuildId { get; set; }
        public ulong? ChannelId { get; set; }
        public ulong? UserId { get; set; }
        public ulong? MessageId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}