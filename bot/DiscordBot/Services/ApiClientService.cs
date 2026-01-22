#nullable disable

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using DSharpPlus.Entities;

namespace DiscordAutomation.Bot.Services
{
    public class ApiClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiClientService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly bool _ignoreSslErrors;

        public ApiClientService(
            IHttpClientFactory httpClientFactory,
            ILogger<ApiClientService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            _ignoreSslErrors = configuration.GetValue<bool>("BackendApi:IgnoreSslErrors", true);
            
            // Tworzymy HttpClient z obsługą ignorowania błędów SSL
            var handler = new HttpClientHandler();
            if (_ignoreSslErrors)
            {
                handler.ServerCertificateCustomValidationCallback = 
                    (message, cert, chain, errors) => true;
            }
            
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(configuration["BackendApi:BaseUrl"] ?? "http://localhost:5000"),
                Timeout = TimeSpan.FromSeconds(configuration.GetValue<int>("BackendApi:TimeoutSeconds", 30))
            };
            
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DiscordAutomationBot/1.0");
            
            _logger.LogInformation("ApiClientService initialized with BaseUrl: {BaseUrl}, IgnoreSslErrors: {IgnoreSslErrors}", 
                _httpClient.BaseAddress, _ignoreSslErrors);
        }

        public async Task<bool> RegisterGuildAsync(DiscordGuild guild)
        {
            try
            {
                _logger.LogDebug("Registering guild {GuildName} ({GuildId}) with backend", guild.Name, guild.Id);
                
                var response = await _httpClient.PostAsJsonAsync("/api/guilds", new
                {
                    Id = guild.Id,
                    Name = guild.Name ?? "Unknown Server",
                    OwnerId = guild.OwnerId.ToString() ?? "0",
                    IconUrl = guild.IconUrl ?? ""
                });

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Registered guild {GuildName} ({GuildId}) with backend", guild.Name, guild.Id);
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to register guild {GuildId}. Status: {StatusCode}, Error: {Error}", 
                        guild.Id, response.StatusCode, error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering guild {GuildId} with backend", guild.Id);
                return false;
            }
        }

        public async Task<bool> UpdateGuildStatusAsync(ulong guildId, bool isActive)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/guilds/{guildId}", new
                {
                    Name = "Updated via bot",
                    IsActive = isActive
                });

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating guild status for {GuildId}", guildId);
                return false;
            }
        }

        public async Task<Models.GuildConfig> GetGuildConfigAsync(ulong guildId)
        {
            try
            {
                _logger.LogDebug("Fetching guild config for {GuildId}", guildId);
                
                var response = await _httpClient.GetAsync($"/api/guilds/{guildId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<BackendGuild>>(content, _jsonOptions);
                    
                    if (apiResponse?.Success == true && apiResponse.Data != null)
                    {
                        _logger.LogDebug("✅ Successfully fetched guild config for {GuildId}", guildId);
                        
                        return new Models.GuildConfig
                        {
                            GuildId = apiResponse.Data.Id,
                            GuildName = apiResponse.Data.Name,
                            PremiumTier = apiResponse.Data.PremiumTier ?? "Free",
                            Modules = new Dictionary<string, bool>(),
                            Rules = new List<Models.CachedRule>(),
                            Settings = new Dictionary<string, object>(),
                            LastUpdated = DateTime.UtcNow,
                            CacheExpiry = DateTime.UtcNow.AddMinutes(15)
                        };
                    }
                }

                _logger.LogWarning("Failed to get guild config for {GuildId}. Status: {StatusCode}", 
                    guildId, response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting guild config for {GuildId}", guildId);
                return null;
            }
        }

        public async Task<ApiResponse<BackendRule[]>> GetRulesAsync(ulong guildId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/guilds/{guildId}/rules");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ApiResponse<BackendRule[]>>(content, _jsonOptions) 
                        ?? new ApiResponse<BackendRule[]> { Success = false, Message = "Failed to parse response" };
                }

                return new ApiResponse<BackendRule[]> 
                { 
                    Success = false, 
                    Message = $"Failed to get rules: {response.StatusCode}" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rules for guild {GuildId}", guildId);
                return new ApiResponse<BackendRule[]> 
                { 
                    Success = false, 
                    Message = $"Exception: {ex.Message}" 
                };
            }
        }

        // Helper classes
        public class ApiResponse<T>
        {
            public bool Success { get; set; }
            public T Data { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        private class BackendGuild
        {
            public ulong Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string PremiumTier { get; set; } = "Free";
        }

        public class BackendRule
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Module { get; set; } = string.Empty;
            public string TriggerType { get; set; } = string.Empty;
            public Dictionary<string, object> TriggerConditions { get; set; } = new();
            public string ActionType { get; set; } = string.Empty;
            public Dictionary<string, object> ActionParameters { get; set; } = new();
            public bool IsEnabled { get; set; }
            public int Priority { get; set; }
            public int? CooldownSeconds { get; set; }
        }
    }
}