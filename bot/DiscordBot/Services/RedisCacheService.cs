using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using DiscordAutomation.Bot.Models;

namespace DiscordAutomation.Bot.Services
{
    public class RedisCacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly IDatabase _database;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);

        public RedisCacheService(
            IConnectionMultiplexer redis,
            ILogger<RedisCacheService> logger,
            IConfiguration configuration)
        {
            _redis = redis;
            _logger = logger;
            _database = redis.GetDatabase();
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
            
            var cacheMinutes = configuration.GetValue<int>("Modules:CacheDurationMinutes", 15);
            _defaultCacheDuration = TimeSpan.FromMinutes(cacheMinutes);
        }

        // Guild Configuration
        public async Task CacheGuildConfigAsync(ulong guildId, GuildConfig config)
        {
            try
            {
                var key = $"guild:{guildId}:config";
                var value = JsonSerializer.Serialize(config, _jsonOptions);
                
                await _database.StringSetAsync(key, value, _defaultCacheDuration);
                _logger.LogTrace("Cached guild config for {GuildId}", guildId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cache guild config for {GuildId}", guildId);
            }
        }

        public async Task<GuildConfig?> GetGuildConfigAsync(ulong guildId)
        {
            try
            {
                var key = $"guild:{guildId}:config";
                var value = await _database.StringGetAsync(key);
                
                if (value.IsNullOrEmpty)
                    return null;
                    
                return JsonSerializer.Deserialize<GuildConfig>(value.ToString(), _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get guild config for {GuildId}", guildId);
                return null;
            }
        }

        public async Task<bool> RemoveGuildConfigAsync(ulong guildId)
        {
            try
            {
                var key = $"guild:{guildId}:config";
                return await _database.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove guild config for {GuildId}", guildId);
                return false;
            }
        }

        // Rules
        public async Task CacheRulesAsync(ulong guildId, List<CachedRule> rules)
        {
            try
            {
                var key = $"guild:{guildId}:rules";
                var value = JsonSerializer.Serialize(rules, _jsonOptions);
                
                await _database.StringSetAsync(key, value, _defaultCacheDuration);
                _logger.LogTrace("Cached {RuleCount} rules for guild {GuildId}", rules.Count, guildId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cache rules for {GuildId}", guildId);
            }
        }

        public async Task<List<CachedRule>> GetRulesAsync(ulong guildId)
        {
            try
            {
                var key = $"guild:{guildId}:rules";
                var value = await _database.StringGetAsync(key);
                
                if (value.IsNullOrEmpty)
                    return new List<CachedRule>();
                    
                return JsonSerializer.Deserialize<List<CachedRule>>(value.ToString(), _jsonOptions) 
                    ?? new List<CachedRule>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get rules for {GuildId}", guildId);
                return new List<CachedRule>();
            }
        }

        // Utility methods
        public async Task SetValueAsync(string key, string value, TimeSpan? expiry = null)
        {
            try
            {
                await _database.StringSetAsync(key, value, expiry ?? _defaultCacheDuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set value for key {Key}", key);
            }
        }

        public async Task<string?> GetValueAsync(string key)
        {
            try
            {
                var value = await _database.StringGetAsync(key);
                return value.IsNullOrEmpty ? null : value.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get value for key {Key}", key);
                return null;
            }
        }

        public async Task<bool> KeyExistsAsync(string key)
        {
            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check key existence for {Key}", key);
                return false;
            }
        }

        public async Task<bool> DeleteKeyAsync(string key)
        {
            try
            {
                return await _database.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete key {Key}", key);
                return false;
            }
        }

        public async Task ClearCacheAsync()
        {
            try
            {
                var endpoints = _redis.GetEndPoints();
                foreach (var endpoint in endpoints)
                {
                    var server = _redis.GetServer(endpoint);
                    await server.FlushDatabaseAsync();
                }
                _logger.LogInformation("Redis cache cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear cache");
            }
        }
    }
}