#nullable disable

using DSharpPlus;
using DSharpPlus.EventArgs;
using DiscordAutomation.Bot.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordAutomation.Bot.EventHandlers
{
    public class UserEventHandler
    {
        private readonly ILogger<UserEventHandler> _logger;
        private readonly RedisCacheService _cacheService;
        private readonly ApiClientService _apiClient;

        public UserEventHandler(
            ILogger<UserEventHandler> logger,
            RedisCacheService cacheService,
            ApiClientService apiClient)
        {
            _logger = logger;
            _cacheService = cacheService;
            _apiClient = apiClient;
        }

        public async Task HandleGuildMemberAddedAsync(DiscordClient client, GuildMemberAddEventArgs e)
        {
            _logger.LogInformation("User {User} joined guild {Guild}", e.Member.Username, e.Guild.Name);
            
            // Get guild configuration
            var guildConfig = await _cacheService.GetGuildConfigAsync(e.Guild.Id);
            
            // Check if welcome module is enabled
            if (guildConfig != null && guildConfig.Modules.ContainsKey("Welcome") && guildConfig.Modules["Welcome"])
            {
                await ProcessWelcomeUserAsync(e, guildConfig);
            }
            
            // Check for auto-role assignment
            if (guildConfig != null && guildConfig.Modules.ContainsKey("AutoRoles") && guildConfig.Modules["AutoRoles"])
            {
                await ProcessAutoRolesAsync(e, guildConfig);
            }
            
            await Task.CompletedTask;
        }

        public async Task HandleGuildMemberRemovedAsync(DiscordClient client, GuildMemberRemoveEventArgs e)
        {
            _logger.LogInformation("User {User} left guild {Guild}", e.Member.Username, e.Guild.Name);
            
            // Get guild configuration
            var guildConfig = await _cacheService.GetGuildConfigAsync(e.Guild.Id);
            
            // Check if goodbye module is enabled
            if (guildConfig != null && guildConfig.Modules.ContainsKey("Welcome") && guildConfig.Modules["Welcome"])
            {
                await ProcessGoodbyeUserAsync(e, guildConfig);
            }
            
            await Task.CompletedTask;
        }

        public async Task HandleUserUpdatedAsync(DiscordClient client, UserUpdateEventArgs e)
        {
            _logger.LogDebug("User updated: {User}", e.UserAfter.Username);
            
            // Check for username changes that might need moderation
            if (e.UserBefore.Username != e.UserAfter.Username)
            {
                await ProcessUsernameChangeAsync(client, e);
            }
            
            await Task.CompletedTask;
        }

        private async Task ProcessWelcomeUserAsync(GuildMemberAddEventArgs e, Models.GuildConfig guildConfig)
        {
            try
            {
                // Check if welcome channel is configured
                if (guildConfig.Settings.ContainsKey("welcomeChannelId"))
                {
                    var channelIdStr = guildConfig.Settings["welcomeChannelId"] as string;
                    if (ulong.TryParse(channelIdStr, out ulong channelId))
                    {
                        var channel = e.Guild.GetChannel(channelId);
                        if (channel != null)
                        {
                            string welcomeMessage = GetWelcomeMessage(e.Member, guildConfig);
                            await channel.SendMessageAsync(welcomeMessage);
                            _logger.LogDebug("Sent welcome message for {User} in {Channel}", 
                                e.Member.Username, channel.Name);
                        }
                    }
                }
                else
                {
                    // Default welcome message in system channel or first text channel
                    var defaultChannel = e.Guild.SystemChannel ?? e.Guild.Channels.Values
                        .FirstOrDefault(c => c.Type == DSharpPlus.ChannelType.Text);
                    
                    if (defaultChannel != null)
                    {
                        string welcomeMessage = GetWelcomeMessage(e.Member, guildConfig);
                        await defaultChannel.SendMessageAsync(welcomeMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing welcome for user {User}", e.Member.Username);
            }
        }

        private async Task ProcessGoodbyeUserAsync(GuildMemberRemoveEventArgs e, Models.GuildConfig guildConfig)
        {
            try
            {
                if (guildConfig.Settings.ContainsKey("goodbyeChannelId"))
                {
                    var channelIdStr = guildConfig.Settings["goodbyeChannelId"] as string;
                    if (ulong.TryParse(channelIdStr, out ulong channelId))
                    {
                        var channel = e.Guild.GetChannel(channelId);
                        if (channel != null)
                        {
                            string goodbyeMessage = GetGoodbyeMessage(e.Member, guildConfig);
                            await channel.SendMessageAsync(goodbyeMessage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing goodbye for user {User}", e.Member.Username);
            }
        }

        private async Task ProcessAutoRolesAsync(GuildMemberAddEventArgs e, Models.GuildConfig guildConfig)
        {
            try
            {
                if (guildConfig.Settings.ContainsKey("autoRoles"))
                {
                    if (guildConfig.Settings["autoRoles"] is List<string> roleIds)
                    {
                        foreach (var roleIdStr in roleIds)
                        {
                            if (ulong.TryParse(roleIdStr, out ulong roleId))
                            {
                                var role = e.Guild.GetRole(roleId);
                                if (role != null)
                                {
                                    await e.Member.GrantRoleAsync(role);
                                    _logger.LogDebug("Assigned auto-role {RoleName} to {User}", 
                                        role.Name, e.Member.Username);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning auto-roles to user {User}", e.Member.Username);
            }
        }

        private async Task ProcessUsernameChangeAsync(DiscordClient client, UserUpdateEventArgs e)
        {
            try
            {
                // Check for inappropriate usernames in all guilds the user is in
                foreach (var guild in client.Guilds.Values)
                {
                    var guildConfig = await _cacheService.GetGuildConfigAsync(guild.Id);
                    if (guildConfig?.Modules.ContainsKey("Moderation") == true && guildConfig.Modules["Moderation"])
                    {
                        var rules = await _cacheService.GetRulesAsync(guild.Id);
                        foreach (var rule in rules)
                        {
                            if (rule.Module == "Moderation" && rule.IsEnabled && rule.TriggerType == "Username")
                            {
                                if (rule.TriggerConditions.ContainsKey("bannedWords"))
                                {
                                    if (rule.TriggerConditions["bannedWords"] is List<string> bannedWords)
                                    {
                                        foreach (var word in bannedWords)
                                        {
                                            if (e.UserAfter.Username.ToLower().Contains(word.ToLower()))
                                            {
                                                // Apply action (e.g., kick, warn, etc.)
                                                _logger.LogWarning("User {User} has inappropriate username in guild {Guild}: {Username}", 
                                                    e.UserAfter.Username, guild.Name, e.UserAfter.Username);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing username change for user {User}", e.UserAfter.Username);
            }
        }

        private string GetWelcomeMessage(DSharpPlus.Entities.DiscordMember member, Models.GuildConfig guildConfig)
        {
            string defaultMessage = $"Welcome {member.Mention} to {member.Guild.Name}! 🎉";
            
            if (guildConfig.Settings.ContainsKey("welcomeMessage"))
            {
                var messageTemplate = guildConfig.Settings["welcomeMessage"] as string;
                if (!string.IsNullOrEmpty(messageTemplate))
                {
                    return messageTemplate
                        .Replace("{user}", member.Mention)
                        .Replace("{username}", member.Username)
                        .Replace("{server}", member.Guild.Name)
                        .Replace("{memberCount}", member.Guild.MemberCount.ToString());
                }
            }
            
            return defaultMessage;
        }

        private string GetGoodbyeMessage(DSharpPlus.Entities.DiscordMember member, Models.GuildConfig guildConfig)
        {
            string defaultMessage = $"{member.Username} has left the server. 👋";
            
            if (guildConfig.Settings.ContainsKey("goodbyeMessage"))
            {
                var messageTemplate = guildConfig.Settings["goodbyeMessage"] as string;
                if (!string.IsNullOrEmpty(messageTemplate))
                {
                    return messageTemplate
                        .Replace("{user}", member.Username)
                        .Replace("{server}", member.Guild.Name);
                }
            }
            
            return defaultMessage;
        }
    }
}