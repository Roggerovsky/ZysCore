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
    public class ReactionEventHandler
    {
        private readonly ILogger<ReactionEventHandler> _logger;
        private readonly RedisCacheService _cacheService;
        private readonly ApiClientService _apiClient;

        public ReactionEventHandler(
            ILogger<ReactionEventHandler> logger,
            RedisCacheService cacheService,
            ApiClientService apiClient)
        {
            _logger = logger;
            _cacheService = cacheService;
            _apiClient = apiClient;
        }

        public async Task HandleReactionAddedAsync(DiscordClient client, MessageReactionAddEventArgs e)
        {
            if (e.User.IsBot)
                return;

            _logger.LogDebug("Reaction {Emoji} added by {User} in {Channel}", 
                e.Emoji, e.User.Username, e.Channel.Name);
            
            // Check if tickets module is enabled
            var guildConfig = await _cacheService.GetGuildConfigAsync(e.Guild.Id);
            
            if (guildConfig != null && guildConfig.Modules.ContainsKey("Tickets") && guildConfig.Modules["Tickets"])
            {
                await ProcessTicketCreationAsync(e, guildConfig);
            }
            
            // Check for reaction roles
            if (guildConfig != null && guildConfig.Modules.ContainsKey("ReactionRoles") && guildConfig.Modules["ReactionRoles"])
            {
                await ProcessReactionRoleAsync(e, guildConfig);
            }
            
            await Task.CompletedTask;
        }

        public async Task HandleReactionRemovedAsync(DiscordClient client, MessageReactionRemoveEventArgs e)
        {
            _logger.LogDebug("Reaction {Emoji} removed in {Channel}", e.Emoji, e.Channel.Name);
            
            // Process reaction removal for reaction roles
            var guildConfig = await _cacheService.GetGuildConfigAsync(e.Guild.Id);
            
            if (guildConfig != null && guildConfig.Modules.ContainsKey("ReactionRoles") && guildConfig.Modules["ReactionRoles"])
            {
                await ProcessReactionRoleRemovalAsync(e, guildConfig);
            }
            
            await Task.CompletedTask;
        }

        private async Task ProcessTicketCreationAsync(MessageReactionAddEventArgs e, Models.GuildConfig guildConfig)
        {
            try
            {
                // Check if this is a ticket creation reaction
                if (guildConfig.Settings.ContainsKey("ticketCreationEmoji"))
                {
                    var ticketEmoji = guildConfig.Settings["ticketCreationEmoji"] as string;
                    if (e.Emoji.Name == ticketEmoji)
                    {
                        // Check if this message is the ticket creation message
                        if (guildConfig.Settings.ContainsKey("ticketCreationMessageId"))
                        {
                            var messageIdStr = guildConfig.Settings["ticketCreationMessageId"] as string;
                            if (ulong.TryParse(messageIdStr, out ulong messageId))
                            {
                                if (e.Message.Id == messageId)
                                {
                                    await CreateTicketAsync(e);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ticket creation reaction");
            }
        }

        private async Task CreateTicketAsync(MessageReactionAddEventArgs e)
        {
            try
            {
                var guildConfig = await _cacheService.GetGuildConfigAsync(e.Guild.Id);
                
                // Get ticket category
                ulong? categoryId = null;
                if (guildConfig.Settings.ContainsKey("ticketCategoryId"))
                {
                    var categoryIdStr = guildConfig.Settings["ticketCategoryId"] as string;
                    if (ulong.TryParse(categoryIdStr, out ulong catId))
                    {
                        categoryId = catId;
                    }
                }
                
                // Create ticket channel
                var channelName = $"ticket-{e.User.Username.ToLower()}";
                var channel = await e.Guild.CreateChannelAsync(
                    channelName,
                    DSharpPlus.ChannelType.Text,
                    parent: categoryId.HasValue ? e.Guild.GetChannel(categoryId.Value) : null
                );
                
                // Get the member for permission overwrites
                var member = await e.Guild.GetMemberAsync(e.User.Id);
                
                // Set permissions
                await channel.AddOverwriteAsync(member, 
                    DSharpPlus.Permissions.AccessChannels | 
                    DSharpPlus.Permissions.SendMessages | 
                    DSharpPlus.Permissions.ReadMessageHistory);
                
                // Add support roles if configured
                if (guildConfig.Settings.ContainsKey("supportRoleIds"))
                {
                    if (guildConfig.Settings["supportRoleIds"] is List<string> roleIds)
                    {
                        foreach (var roleIdStr in roleIds)
                        {
                            if (ulong.TryParse(roleIdStr, out ulong roleId))
                            {
                                var role = e.Guild.GetRole(roleId);
                                if (role != null)
                                {
                                    await channel.AddOverwriteAsync(role,
                                        DSharpPlus.Permissions.AccessChannels |
                                        DSharpPlus.Permissions.SendMessages |
                                        DSharpPlus.Permissions.ReadMessageHistory);
                                }
                            }
                        }
                    }
                }
                
                // Send welcome message
                string welcomeMessage = GetTicketWelcomeMessage(e.User, guildConfig);
                await channel.SendMessageAsync(welcomeMessage);
                
                _logger.LogInformation("Created ticket channel {ChannelName} for user {User}", 
                    channel.Name, e.User.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ticket for user {User}", e.User.Username);
            }
        }

        private async Task ProcessReactionRoleAsync(MessageReactionAddEventArgs e, Models.GuildConfig guildConfig)
        {
            try
            {
                if (guildConfig.Settings.ContainsKey("reactionRoleMessages"))
                {
                    if (guildConfig.Settings["reactionRoleMessages"] is Dictionary<string, Dictionary<string, string>> reactionRoles)
                    {
                        if (reactionRoles.ContainsKey(e.Message.Id.ToString()))
                        {
                            var messageReactions = reactionRoles[e.Message.Id.ToString()];
                            
                            if (messageReactions.ContainsKey(e.Emoji.Name))
                            {
                                var roleIdStr = messageReactions[e.Emoji.Name];
                                if (ulong.TryParse(roleIdStr, out ulong roleId))
                                {
                                    var role = e.Guild.GetRole(roleId);
                                    if (role != null)
                                    {
                                        var member = await e.Guild.GetMemberAsync(e.User.Id);
                                        await member.GrantRoleAsync(role);
                                        
                                        _logger.LogDebug("Assigned role {RoleName} to {User} via reaction {Emoji}", 
                                            role.Name, e.User.Username, e.Emoji);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing reaction role for user {User}", e.User.Username);
            }
        }

        private async Task ProcessReactionRoleRemovalAsync(MessageReactionRemoveEventArgs e, Models.GuildConfig guildConfig)
        {
            try
            {
                if (guildConfig.Settings.ContainsKey("reactionRoleMessages"))
                {
                    if (guildConfig.Settings["reactionRoleMessages"] is Dictionary<string, Dictionary<string, string>> reactionRoles)
                    {
                        if (reactionRoles.ContainsKey(e.Message.Id.ToString()))
                        {
                            var messageReactions = reactionRoles[e.Message.Id.ToString()];
                            
                            if (messageReactions.ContainsKey(e.Emoji.Name))
                            {
                                var roleIdStr = messageReactions[e.Emoji.Name];
                                if (ulong.TryParse(roleIdStr, out ulong roleId))
                                {
                                    var role = e.Guild.GetRole(roleId);
                                    if (role != null)
                                    {
                                        var member = await e.Guild.GetMemberAsync(e.User.Id);
                                        await member.RevokeRoleAsync(role);
                                        
                                        _logger.LogDebug("Removed role {RoleName} from {User} via reaction removal {Emoji}", 
                                            role.Name, e.User.Username, e.Emoji);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing reaction role removal for user {User}", e.User.Username);
            }
        }

        private string GetTicketWelcomeMessage(DSharpPlus.Entities.DiscordUser user, Models.GuildConfig guildConfig)
        {
            string defaultMessage = $"Hello {user.Mention}! 👋\n" +
                                   "Support staff will be with you shortly.\n" +
                                   "Please describe your issue in detail.";
            
            if (guildConfig.Settings.ContainsKey("ticketWelcomeMessage"))
            {
                var messageTemplate = guildConfig.Settings["ticketWelcomeMessage"] as string;
                if (!string.IsNullOrEmpty(messageTemplate))
                {
                    return messageTemplate.Replace("{user}", user.Mention);
                }
            }
            
            return defaultMessage;
        }
    }
}