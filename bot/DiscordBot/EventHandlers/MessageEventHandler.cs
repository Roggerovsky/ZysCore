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
    public class MessageEventHandler
    {
        private readonly ILogger<MessageEventHandler> _logger;
        private readonly RedisCacheService _cacheService;
        private readonly ApiClientService _apiClient;
        private readonly RuleProcessorService _ruleProcessor;
        private readonly OpenAIModerationService _openAIService;
        private readonly RoleManagementService _roleManagementService;

        public MessageEventHandler(
            ILogger<MessageEventHandler> logger,
            RedisCacheService cacheService,
            ApiClientService apiClient,
            RuleProcessorService ruleProcessor,
            OpenAIModerationService openAIService,
            RoleManagementService roleManagementService)
        {
            _logger = logger;
            _cacheService = cacheService;
            _apiClient = apiClient;
            _ruleProcessor = ruleProcessor;
            _openAIService = openAIService;
            _roleManagementService = roleManagementService;
        }

        public async Task HandleAsync(DiscordClient client, MessageCreateEventArgs e)
        {
            // Check if bot role is at the top
            var isAtTop = await _roleManagementService.IsBotRoleAtTopAsync(e.Guild, client);
            if (!isAtTop)
            {
                // Bot role not at top - ignore messages (functions disabled)
                return;
            }

            // Ignore messages from bots
            if (e.Author.IsBot)
                return;

            _logger.LogDebug("Message from {User} in {Channel}: {Content}", 
                e.Author.Username, e.Channel.Name, e.Message.Content);
            
            // Check if moderation module is enabled for this guild
            var guildConfig = await _cacheService.GetGuildConfigAsync(e.Guild.Id);
            
            if (guildConfig != null && guildConfig.Modules.ContainsKey("Moderation") && guildConfig.Modules["Moderation"])
            {
                // Process message for moderation
                await ProcessMessageForModerationAsync(e);
            }
            
            // Log message to database (for premium servers)
            if (guildConfig?.PremiumTier == "Premium" || guildConfig?.PremiumTier == "Enterprise")
            {
                await LogMessageToBackendAsync(e);
            }
            
            await Task.CompletedTask;
        }

        public async Task HandleUpdatedAsync(DiscordClient client, MessageUpdateEventArgs e)
        {
            if (e.Author?.IsBot == true)
                return;

            _logger.LogDebug("Message updated by {User} in {Channel}", 
                e.Author?.Username, e.Channel.Name);
                
            // Check if message content changed
            if (!string.IsNullOrEmpty(e.MessageBefore?.Content) && 
                e.MessageBefore.Content != e.Message.Content)
            {
                // Process edited message for moderation
                await ProcessMessageForModerationAsync(e);
            }
            
            await Task.CompletedTask;
        }

        public async Task HandleDeletedAsync(DiscordClient client, MessageDeleteEventArgs e)
        {
            _logger.LogDebug("Message deleted in {Channel}", e.Channel.Name);
            
            // Log deletion to backend if enabled
            if (e.Message != null)
            {
                var guildConfig = await _cacheService.GetGuildConfigAsync(e.Guild.Id);
                if (guildConfig?.PremiumTier == "Premium" || guildConfig?.PremiumTier == "Enterprise")
                {
                    await LogDeletionToBackendAsync(e);
                }
            }
            
            await Task.CompletedTask;
        }

        private async Task ProcessMessageForModerationAsync(MessageCreateEventArgs e)
        {
            try
            {
                // Get cached rules for this guild
                var rules = await _cacheService.GetRulesAsync(e.Guild.Id);
                
                // Check for banned words first
                bool hasBannedWords = false;
                
                foreach (var rule in rules)
                {
                    if (rule.Module == "Moderation" && rule.IsEnabled)
                    {
                        if (rule.TriggerType == "MessageContent" && 
                            rule.TriggerConditions.ContainsKey("bannedWords"))
                        {
                            if (rule.TriggerConditions["bannedWords"] is List<string> bannedWords)
                            {
                                foreach (var word in bannedWords)
                                {
                                    if (e.Message.Content.ToLower().Contains(word.ToLower()))
                                    {
                                        hasBannedWords = true;
                                        await ApplyRuleActionAsync(e, rule);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                
                // If no banned words found, check with AI moderation (if enabled)
                if (!hasBannedWords)
                {
                    var guildConfig = await _cacheService.GetGuildConfigAsync(e.Guild.Id);
                    if (guildConfig?.Settings?.ContainsKey("enableAIModeration") == true && 
                        (bool)guildConfig.Settings["enableAIModeration"])
                    {
                        var aiResult = await _openAIService.ModerateContentAsync(e.Message.Content);
                        if (aiResult.Flagged)
                        {
                            await ApplyAIModerationActionAsync(e);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message for moderation");
            }
        }

        private async Task ProcessMessageForModerationAsync(MessageUpdateEventArgs e)
        {
            // Similar logic for updated messages
            try
            {
                var guildConfig = await _cacheService.GetGuildConfigAsync(e.Guild.Id);
                if (guildConfig?.Modules.ContainsKey("Moderation") == true && 
                    guildConfig.Modules["Moderation"])
                {
                    // Check edited message for moderation
                    var rules = await _cacheService.GetRulesAsync(e.Guild.Id);
                    
                    foreach (var rule in rules)
                    {
                        if (rule.Module == "Moderation" && rule.IsEnabled)
                        {
                            if (rule.TriggerType == "MessageContent" && 
                                rule.TriggerConditions.ContainsKey("bannedWords"))
                            {
                                if (rule.TriggerConditions["bannedWords"] is List<string> bannedWords)
                                {
                                    foreach (var word in bannedWords)
                                    {
                                        if (e.Message.Content.ToLower().Contains(word.ToLower()))
                                        {
                                            await ApplyRuleActionAsync(e, rule);
                                            break;
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
                _logger.LogError(ex, "Error processing updated message for moderation");
            }
        }

        private async Task ApplyRuleActionAsync(MessageCreateEventArgs e, Models.CachedRule rule)
        {
            _logger.LogInformation("Applying rule {RuleName} to message from {User}", 
                rule.Name, e.Author.Username);
            
            try
            {
                if (rule.ActionType == "DeleteMessage")
                {
                    await e.Message.DeleteAsync();
                    await e.Channel.SendMessageAsync($"{e.Author.Mention}, your message was removed for violating server rules.");
                }
                else if (rule.ActionType == "Warn")
                {
                    await e.Channel.SendMessageAsync($"{e.Author.Mention}, your message violates our rules. Please review the server rules.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying rule action");
            }
        }

        private async Task ApplyRuleActionAsync(MessageUpdateEventArgs e, Models.CachedRule rule)
        {
            _logger.LogInformation("Applying rule {RuleName} to updated message from {User}", 
                rule.Name, e.Author?.Username);
            
            try
            {
                if (rule.ActionType == "DeleteMessage" && e.Message != null)
                {
                    await e.Message.DeleteAsync();
                    await e.Channel.SendMessageAsync($"{e.Author.Mention}, your edited message was removed for violating server rules.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying rule action to updated message");
            }
        }

        private async Task ApplyAIModerationActionAsync(MessageCreateEventArgs e)
        {
            _logger.LogInformation("Applying AI moderation to message from {User}", e.Author.Username);
            
            try
            {
                await e.Message.DeleteAsync();
                await e.Channel.SendMessageAsync($"{e.Author.Mention}, your message was flagged by our AI moderation system and has been removed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying AI moderation action");
            }
        }

        private async Task LogMessageToBackendAsync(MessageCreateEventArgs e)
        {
            try
            {
                // This would send to backend API for logging
                // For now, just log to console
                _logger.LogDebug("Logging message to backend: {MessageId}", e.Message.Id);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging message to backend");
            }
        }

        private async Task LogDeletionToBackendAsync(MessageDeleteEventArgs e)
        {
            try
            {
                _logger.LogDebug("Logging message deletion to backend: {MessageId}", e.Message.Id);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging message deletion to backend");
            }
        }
    }
}