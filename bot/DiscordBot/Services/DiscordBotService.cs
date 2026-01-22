#nullable disable

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DiscordAutomation.Bot.EventHandlers;
using DiscordAutomation.Bot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordAutomation.Bot.Services
{
    public class DiscordBotService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DiscordBotService> _logger;
        private readonly MessageEventHandler _messageEventHandler;
        private readonly UserEventHandler _userEventHandler;
        private readonly ReactionEventHandler _reactionEventHandler;
        private readonly RedisCacheService _cacheService;
        private readonly ApiClientService _apiClient;
        private readonly RoleManagementService _roleManagementService;
        
        private DiscordClient _client;
        private bool _isRunning = false;
        private Timer _roleCheckTimer;

        public DiscordBotService(
            IConfiguration configuration,
            ILogger<DiscordBotService> logger,
            MessageEventHandler messageEventHandler,
            UserEventHandler userEventHandler,
            ReactionEventHandler reactionEventHandler,
            RedisCacheService cacheService,
            ApiClientService apiClient,
            RoleManagementService roleManagementService)
        {
            _configuration = configuration;
            _logger = logger;
            _messageEventHandler = messageEventHandler;
            _userEventHandler = userEventHandler;
            _reactionEventHandler = reactionEventHandler;
            _cacheService = cacheService;
            _apiClient = apiClient;
            _roleManagementService = roleManagementService;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning || _client != null) return;

            _logger.LogInformation("=== DISCORD BOT STARTUP INITIATED ===");
            _logger.LogInformation("Time: {Time}", DateTime.UtcNow);
            _logger.LogInformation("Environment: {Environment}", 
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");

            try
            {
                var token = _configuration["Discord:BotToken"];
                
                // Validate token
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogCritical("❌ DISCORD TOKEN IS EMPTY!");
                    _logger.LogCritical("Add 'Discord:BotToken' to appsettings.json or environment variables");
                    throw new InvalidOperationException("Discord Bot Token is not configured.");
                }
                
                if (token.Contains("YOUR_") || token.Contains("placeholder"))
                {
                    _logger.LogCritical("❌ USING PLACEHOLDER TOKEN!");
                    _logger.LogCritical("Replace 'YOUR_DISCORD_BOT_TOKEN_HERE' with actual token");
                    throw new InvalidOperationException("Please replace placeholder token with actual bot token.");
                }

                _logger.LogInformation("Token validation:");
                _logger.LogInformation("  Length: {Length} characters", token.Length);
                _logger.LogInformation("  Starts with: {Prefix}", 
                    token.Substring(0, Math.Min(10, token.Length)));
                _logger.LogInformation("  Ends with: {Suffix}", 
                    token.Substring(Math.Max(0, token.Length - 10)));

                // Create Discord configuration
                var discordConfig = new DiscordConfiguration
                {
                    Token = token,
                    TokenType = TokenType.Bot,
                    AutoReconnect = true,
                    MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                    Intents = DiscordIntents.AllUnprivileged | 
                               DiscordIntents.MessageContents |
                               DiscordIntents.GuildMembers |
                               DiscordIntents.Guilds |
                               DiscordIntents.GuildMessages |
                               DiscordIntents.GuildMessageReactions |
                               DiscordIntents.GuildPresences,
                    MessageCacheSize = 512,
                    GatewayCompressionLevel = GatewayCompressionLevel.Stream,
                    ReconnectIndefinitely = true,
                    AlwaysCacheMembers = true,
                    LargeThreshold = 250
                };

                _logger.LogInformation("Creating Discord client with configuration...");

                // Create client
                _client = new DiscordClient(discordConfig);

                // Register event handlers
                RegisterEventHandlers();

                // Add Ready event handler for startup logging
                _client.Ready += OnClientReady;

                // Add error event handlers
                _client.ClientErrored += HandleClientErrorAsync;
                _client.SocketErrored += HandleSocketErrorAsync;

                _logger.LogInformation("Connecting to Discord gateway...");

                // Connect to Discord
                await _client.ConnectAsync();
                _isRunning = true;

                _logger.LogInformation("✅ Connection successful!");
                
                // Start role check timer (checks every 5 minutes)
                _roleCheckTimer = new Timer(
                    async _ => await CheckAllGuildRolesAsync(), 
                    null, 
                    TimeSpan.Zero, 
                    TimeSpan.FromMinutes(5));
                
                _logger.LogInformation("Role check timer started (every 5 minutes)");

                // Wait a moment for guilds to load
                await Task.Delay(1000, cancellationToken);

            }
            catch (DSharpPlus.Exceptions.UnauthorizedException ex)
            {
                _logger.LogCritical(ex, "❌ 401 UNAUTHORIZED - INVALID DISCORD TOKEN!");
                _logger.LogCritical("The Discord bot token is incorrect or expired.");
                _logger.LogCritical("Please reset your token at: https://discord.com/developers/applications");
                _logger.LogCritical("Steps:");
                _logger.LogCritical("1. Go to Discord Developer Portal");
                _logger.LogCritical("2. Select your application → Bot");
                _logger.LogCritical("3. Click 'Reset Token'");
                _logger.LogCritical("4. Copy new token to appsettings.json");
                throw;
            }
            catch (DSharpPlus.Exceptions.BadRequestException ex)
            {
                _logger.LogCritical(ex, "❌ BAD REQUEST - CHECK INTENTS!");
                _logger.LogCritical("Make sure Privileged Gateway Intents are enabled:");
                _logger.LogCritical("1. Go to: https://discord.com/developers/applications");
                _logger.LogCritical("2. Select your app → Bot → Privileged Gateway Intents");
                _logger.LogCritical("3. ENABLE: ✓ PRESENCE INTENT");
                _logger.LogCritical("           ✓ SERVER MEMBERS INTENT");
                _logger.LogCritical("           ✓ MESSAGE CONTENT INTENT");
                _logger.LogCritical("4. Click 'Save Changes'");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "❌ FAILED TO START DISCORD BOT");
                throw;
            }
        }

        private async Task OnClientReady(DiscordClient client, ReadyEventArgs e)
        {
            _logger.LogInformation("=== DISCORD BOT READY ===");
            _logger.LogInformation("Logged in as: {Username} ({UserId})", 
                client.CurrentUser.Username, 
                client.CurrentUser.Id);
            _logger.LogInformation("Bot is in {GuildCount} guild(s):", client.Guilds.Count);

            // Immediately check roles for all guilds
            await CheckAllGuildRolesAsync();

            if (client.Guilds.Count == 0)
            {
                _logger.LogWarning("⚠️ Bot is not in any servers!");
                _logger.LogWarning("To invite the bot, use this URL (replace CLIENT_ID):");
                _logger.LogWarning("https://discord.com/api/oauth2/authorize?client_id=CLIENT_ID&permissions=268435558&scope=bot");
                _logger.LogWarning("Find your CLIENT_ID at: https://discord.com/developers/applications");
            }
            else
            {
                foreach (var guild in client.Guilds.Values)
                {
                    try
                    {
                        // Get full guild info
                        var fullGuild = await client.GetGuildAsync(guild.Id, true);
                        
                        _logger.LogInformation("  • {GuildName} ({GuildId}) - {MemberCount} members", 
                            fullGuild?.Name ?? guild.Name ?? "Unknown Server", 
                            guild.Id, 
                            fullGuild?.MemberCount ?? guild.MemberCount);
                        
                        // Register guild in backend
                        try
                        {
                            var registered = await _apiClient.RegisterGuildAsync(fullGuild ?? guild);
                            if (registered)
                            {
                                _logger.LogDebug("✅ Registered guild {GuildId} with backend", guild.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to register guild {GuildId} with backend", guild.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get details for guild {GuildId}", guild.Id);
                        _logger.LogInformation("  • Guild {GuildId} - could not fetch details", guild.Id);
                    }
                }
            }
        }

        private async Task CheckAllGuildRolesAsync()
        {
            if (_client == null) return;
            
            _logger.LogDebug("Starting periodic role check for all guilds");
            
            foreach (var guild in _client.Guilds.Values)
            {
                try
                {
                    await _roleManagementService.CheckAndSetupBotRoleAsync(guild, _client);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking bot role in guild: {GuildId}", guild.Id);
                }
            }
            
            _logger.LogDebug("Periodic role check completed");
        }

        public async Task StopAsync()
        {
            if (!_isRunning || _client == null) return;

            try
            {
                _logger.LogInformation("Disconnecting bot...");
                
                // Stop timer
                _roleCheckTimer?.Dispose();
                _roleCheckTimer = null;
                
                // Clear cache
                await _cacheService.ClearCacheAsync();
                
                // Disconnect
                await _client.DisconnectAsync();
                _client.Dispose();
                _client = null;
                _isRunning = false;
                
                _logger.LogInformation("Bot disconnected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping bot");
            }
        }

        private void RegisterEventHandlers()
        {
            if (_client == null) return;

            // Message events (will be conditionally enabled based on role position)
            _client.MessageCreated += _messageEventHandler.HandleAsync;
            _client.MessageUpdated += _messageEventHandler.HandleUpdatedAsync;
            _client.MessageDeleted += _messageEventHandler.HandleDeletedAsync;

            // User events
            _client.GuildMemberAdded += _userEventHandler.HandleGuildMemberAddedAsync;
            _client.GuildMemberRemoved += _userEventHandler.HandleGuildMemberRemovedAsync;
            _client.UserUpdated += _userEventHandler.HandleUserUpdatedAsync;

            // Reaction events
            _client.MessageReactionAdded += _reactionEventHandler.HandleReactionAddedAsync;
            _client.MessageReactionRemoved += _reactionEventHandler.HandleReactionRemovedAsync;

            // Guild events
            _client.GuildCreated += HandleGuildCreatedAsync;
            _client.GuildDeleted += HandleGuildDeletedAsync;
            _client.GuildAvailable += HandleGuildAvailableAsync;
            _client.GuildUnavailable += HandleGuildUnavailableAsync;
        }

        private async Task HandleGuildCreatedAsync(DiscordClient client, GuildCreateEventArgs e)
        {
            _logger.LogInformation("✅ Joined new guild: {GuildName} ({GuildId})", e.Guild.Name, e.Guild.Id);
            
            try
            {
                // Immediately check bot role position in new guild
                await _roleManagementService.CheckAndSetupBotRoleAsync(e.Guild, client);
                
                // Register guild in backend
                await _apiClient.RegisterGuildAsync(e.Guild);
                
                // Cache guild configuration
                await CacheGuildConfiguration(e.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle guild creation for {GuildId}", e.Guild.Id);
            }
        }

        private async Task HandleGuildDeletedAsync(DiscordClient client, GuildDeleteEventArgs e)
        {
            _logger.LogInformation("❌ Left guild: {GuildName} ({GuildId})", e.Guild.Name, e.Guild.Id);
            
            try
            {
                // Remove from cache
                await _cacheService.RemoveGuildConfigAsync(e.Guild.Id);
                
                // Update backend (mark as inactive)
                await _apiClient.UpdateGuildStatusAsync(e.Guild.Id, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle guild deletion for {GuildId}", e.Guild.Id);
            }
        }

        private async Task HandleGuildAvailableAsync(DiscordClient client, GuildCreateEventArgs e)
        {
            _logger.LogDebug("Guild available: {GuildName} ({GuildId})", e.Guild.Name, e.Guild.Id);
            
            try
            {
                // Check bot role position when guild becomes available
                await _roleManagementService.CheckAndSetupBotRoleAsync(e.Guild, client);
                
                // Cache guild configuration
                await CacheGuildConfiguration(e.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check bot role in guild: {GuildId}", e.Guild.Id);
            }
        }

        private Task HandleGuildUnavailableAsync(DiscordClient client, GuildDeleteEventArgs e)
        {
            _logger.LogDebug("Guild unavailable: {GuildId}", e.Guild.Id);
            return Task.CompletedTask;
        }

        // Error handlers
        private Task HandleClientErrorAsync(DiscordClient client, ClientErrorEventArgs e)
        {
            _logger.LogError(e.Exception, "Discord client error in {EventName}", e.EventName);
            return Task.CompletedTask;
        }

        private Task HandleSocketErrorAsync(DiscordClient client, SocketErrorEventArgs e)
        {
            _logger.LogError(e.Exception, "Discord socket error");
            return Task.CompletedTask;
        }

        private async Task CacheGuildConfiguration(ulong guildId)
        {
            try
            {
                // Fetch guild config from backend
                var guildConfig = await _apiClient.GetGuildConfigAsync(guildId);
                if (guildConfig != null)
                {
                    await _cacheService.CacheGuildConfigAsync(guildId, guildConfig);
                    _logger.LogDebug("Cached configuration for guild {GuildId}", guildId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache configuration for guild {GuildId}", guildId); // Było guild.Id, jest guildId
            }
        }

        // Public methods
        public DiscordClient GetClient() => _client;
        public bool IsRunning => _isRunning;
        public RoleManagementService GetRoleManagementService() => _roleManagementService;
    }
}