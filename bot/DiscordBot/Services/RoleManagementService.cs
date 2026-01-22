using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordAutomation.Bot.Services
{
    public class RoleManagementService
    {
        private readonly ILogger<RoleManagementService> _logger;
        private readonly IConfiguration _configuration;

        public RoleManagementService(
            ILogger<RoleManagementService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<bool> IsBotRoleAtTopAsync(DiscordGuild guild, DiscordClient client)
        {
            try
            {
                var botMember = await guild.GetMemberAsync(client.CurrentUser.Id);
                if (botMember == null) return false;

                var botRoles = botMember.Roles
                    .Where(r => r.IsManaged)
                    .OrderByDescending(r => r.Position)
                    .ToList();

                if (!botRoles.Any()) return false;

                var highestBotRole = botRoles.First();
                var allRoles = guild.Roles.Values
                    .OrderByDescending(r => r.Position)
                    .ToList();

                var currentTopRole = allRoles.FirstOrDefault();
                
                return currentTopRole?.Id == highestBotRole.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if bot role is at top in guild: {GuildId}", guild.Id);
                return false;
            }
        }

        public async Task CheckAndSetupBotRoleAsync(DiscordGuild guild, DiscordClient client)
        {
            try
            {
                _logger.LogInformation("Checking bot role position in guild: {GuildName} ({GuildId})", 
                    guild.Name, guild.Id);

                bool isAtTop = await IsBotRoleAtTopAsync(guild, client);
                
                if (isAtTop)
                {
                    // Bot role is at the top - enable features
                    await RemoveSetupChannelAsync(guild);
                    await UpdateBotStatusAsync(client, true);
                    await CreateZysCoreRoleAsync(guild, client);
                    
                    _logger.LogInformation("Bot role is at the top in guild: {GuildName}. Features enabled.", guild.Name);
                }
                else
                {
                    // Bot role is not at the top - disable features and create setup channel
                    await CreateSetupChannelAsync(guild, client);
                    await UpdateBotStatusAsync(client, false);
                    
                    _logger.LogWarning("Bot role is NOT at the top in guild: {GuildName}. Features disabled.", guild.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check and setup bot role in guild: {GuildId}", guild.Id);
            }
        }

        private async Task CreateSetupChannelAsync(DiscordGuild guild, DiscordClient client)
        {
            try
            {
                var botMember = await guild.GetMemberAsync(client.CurrentUser.Id);
                if (botMember == null) return;

                // Check if bot has Manage Channels permission
                var permissions = botMember.PermissionsIn(guild);
                if (!permissions.HasPermission(Permissions.ManageChannels))
                {
                    _logger.LogWarning("Bot lacks Manage Channels permission in guild: {GuildName}. Cannot create setup channel.", guild.Name);
                    return;
                }

                // Check if category already exists
                var category = guild.Channels.Values
                    .FirstOrDefault(c => c.Type == ChannelType.Category && 
                       c.Name.Equals("⚠️ ZysCore Setup Required", StringComparison.OrdinalIgnoreCase));

                if (category == null)
                {
                    // Create new category at the very top
                    category = await guild.CreateChannelCategoryAsync("⚠️ ZysCore Setup Required", position: 0);
                    _logger.LogInformation("Created setup category in guild: {GuildName}", guild.Name);
                }

                // Check if channel already exists
                var existingChannel = guild.Channels.Values
                    .FirstOrDefault(c => c.Type == ChannelType.Text && 
                       c.Parent?.Id == category.Id && 
                       c.Name.Equals("bot-setup-help", StringComparison.OrdinalIgnoreCase));

                DiscordChannel channel;
                if (existingChannel == null)
                {
                    // Create text channel in category
                    channel = await guild.CreateTextChannelAsync("bot-setup-help", parent: category);
                    
                    // Create permission overwrites
                    var overwrites = new List<DiscordOverwriteBuilder>
                    {
                        new DiscordOverwriteBuilder(guild.EveryoneRole)
                            .Deny(Permissions.AccessChannels),
                        new DiscordOverwriteBuilder(botMember)
                            .Allow(Permissions.AccessChannels | Permissions.SendMessages | Permissions.ReadMessageHistory)
                    };

                    // Allow server owner
                    if (guild.Owner != null)
                    {
                        overwrites.Add(new DiscordOverwriteBuilder(guild.Owner)
                            .Allow(Permissions.AccessChannels | Permissions.SendMessages | Permissions.ReadMessageHistory));
                    }

                    // Apply all overwrites at once
                    await channel.ModifyAsync(x => x.PermissionOverwrites = overwrites);
                    
                    _logger.LogInformation("Created setup channel in guild: {GuildName}", guild.Name);
                }
                else
                {
                    channel = existingChannel;
                }

                // Send help message
                var owner = guild.Owner;
                var message = $"**URGENT: ZysCore Bot Setup Required**\n\n" +
                              "**🚨 PROBLEM:** The ZysCore bot's role is NOT at the top of the role hierarchy.\n" +
                              "**📊 STATUS:** ❌ **BOT FUNCTIONS ARE DISABLED** ❌\n\n" +
                              "**🔧 SOLUTION REQUIRED:**\n" +
                              "1. Go to **Server Settings** → **Roles**\n" +
                              "2. Find the role named `ZysCore` (this is the bot's role)\n" +
                              "3. **DRAG THIS ROLE TO THE VERY TOP** of the roles list\n" +
                              "4. Make sure the role has **'Manage Roles'** permission enabled\n" +
                              "5. Save changes\n\n" +
                              "**⚡ AUTOMATIC FIX:** Once you move the bot role to the top:\n" +
                              "• This channel will be automatically deleted\n" +
                              "• Bot will change status to normal\n" +
                              "• All bot features will be unlocked\n" +
                              "• ZysCore.xyz role will be created automatically\n\n" +
                              "**🔁 MONITORING:** The bot checks every 5 minutes. If the role is moved down again, this channel will reappear.\n\n" +
                              "*This is a safety feature to ensure the bot can function properly.*";

                // Add owner mention if available
                if (owner != null)
                {
                    message = $"{owner.Mention} " + message;
                }

                // Delete old messages from bot
                var existingMessages = await channel.GetMessagesAsync(10);
                foreach (var msg in existingMessages)
                {
                    if (msg.Author.Id == client.CurrentUser.Id)
                    {
                        await msg.DeleteAsync();
                    }
                }

                await channel.SendMessageAsync(message);
                _logger.LogDebug("Sent setup instructions in guild: {GuildName}", guild.Name);
            }
            catch (UnauthorizedException)
            {
                _logger.LogWarning("Bot lacks permissions to create setup channel in guild: {GuildName}", guild.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating setup channel in guild: {GuildId}", guild.Id);
            }
        }

        private async Task RemoveSetupChannelAsync(DiscordGuild guild)
        {
            try
            {
                var category = guild.Channels.Values
                    .FirstOrDefault(c => c.Type == ChannelType.Category && 
                       c.Name.Equals("⚠️ ZysCore Setup Required", StringComparison.OrdinalIgnoreCase));

                if (category != null)
                {
                    // Delete all channels in category
                    foreach (var channel in category.Children.ToList())
                    {
                        await channel.DeleteAsync();
                    }
                    
                    // Delete category
                    await category.DeleteAsync();
                    _logger.LogInformation("Removed setup category and channels in guild: {GuildName}", guild.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing setup channel in guild: {GuildId}", guild.Id);
            }
        }

        private async Task UpdateBotStatusAsync(DiscordClient client, bool isAtTop)
        {
            try
            {
                if (isAtTop)
                {
                    // Normal status
                    var statusTemplate = _configuration["Discord:Status"] ?? "Watching {server_count} servers";
                    var status = statusTemplate.Replace("{server_count}", client.Guilds.Count.ToString());
                    await client.UpdateStatusAsync(new DiscordActivity(status, ActivityType.Watching), UserStatus.Online);
                    _logger.LogDebug("Bot status updated to normal: {Status}", status);
                }
                else
                {
                    // Error status
                    await client.UpdateStatusAsync(
                        new DiscordActivity($"⚠️ Role not at top | Check #bot-setup-help", ActivityType.Playing), 
                        UserStatus.DoNotDisturb);
                    _logger.LogDebug("Bot status updated to error state");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating bot status");
            }
        }

        private async Task CreateZysCoreRoleAsync(DiscordGuild guild, DiscordClient client)
        {
            try
            {
                var botMember = await guild.GetMemberAsync(client.CurrentUser.Id);
                if (botMember == null) return;

                // Check permissions
                var permissions = botMember.PermissionsIn(guild);
                if (!permissions.HasPermission(Permissions.ManageRoles))
                {
                    _logger.LogWarning("Bot does not have Manage Roles permission in guild: {GuildName}. Cannot create ZysCore.xyz role.", guild.Name);
                    return;
                }

                // Check if role already exists
                var existingRole = guild.Roles.Values
                    .FirstOrDefault(r => r.Name.Equals("ZysCore.xyz", StringComparison.OrdinalIgnoreCase));

                if (existingRole != null)
                {
                    // If exists, ensure proper settings
                    await UpdateZysCoreRoleAsync(existingRole);
                    _logger.LogInformation("ZysCore.xyz role already exists in guild: {GuildName}", guild.Name);
                    
                    // Assign role to bot if not already assigned
                    if (!botMember.Roles.Any(r => r.Id == existingRole.Id))
                    {
                        await botMember.GrantRoleAsync(existingRole);
                        _logger.LogDebug("Assigned existing ZysCore.xyz role to bot in guild: {GuildName}", guild.Name);
                    }
                    return;
                }

                // Find bot's highest role for positioning
                var botRoles = botMember.Roles
                    .Where(r => r.IsManaged)
                    .OrderByDescending(r => r.Position)
                    .ToList();

                int targetPosition = 1; // Default below @everyone
                
                if (botRoles.Any())
                {
                    var highestBotRole = botRoles.First();
                    // New role will be 1 position below bot's role
                    targetPosition = highestBotRole.Position - 1;
                    
                    // Ensure position is non-negative
                    if (targetPosition < 1) targetPosition = 1;
                }

                // Create new role
                var role = await guild.CreateRoleAsync("ZysCore.xyz", 
                    reason: "Automatic role setup by ZysCore bot");

                // Configure role
                await role.ModifyAsync(roleProperties =>
                {
                    // Color: Blue (#1E90FF) - DodgeBlue
                    roleProperties.Color = new DiscordColor(30, 144, 255);
                    
                    // Display separately (hoist)
                    roleProperties.Hoist = true;
                    
                    // Allow mentioning
                    roleProperties.Mentionable = true;
                    
                    // Set reasonable permissions
                    roleProperties.Permissions = Permissions.None;
                });

                _logger.LogInformation("Created ZysCore.xyz role in guild: {GuildName}", guild.Name);

                // Try to set position (may fail if bot role is not high enough)
                try
                {
                    var newPositions = new Dictionary<int, DiscordRole>();
                    newPositions[targetPosition] = role;
                    await guild.ModifyRolePositionsAsync(newPositions);
                    _logger.LogDebug("Positioned ZysCore.xyz role at position {Position} in guild: {GuildName}", 
                        targetPosition, guild.Name);
                }
                catch (UnauthorizedException)
                {
                    _logger.LogWarning("Cannot position ZysCore.xyz role - bot role is too low. Role created at default position.");
                }
                catch (BadRequestException)
                {
                    _logger.LogWarning("Cannot position ZysCore.xyz role at requested position. Role created at default position.");
                }

                // Assign role to bot
                await botMember.GrantRoleAsync(role);
                _logger.LogDebug("Assigned ZysCore.xyz role to bot in guild: {GuildName}", guild.Name);

            }
            catch (UnauthorizedException)
            {
                _logger.LogWarning("Bot lacks permissions to create roles in guild: {GuildName}", 
                    guild.Name);
            }
            catch (BadRequestException ex)
            {
                _logger.LogError(ex, "Bad request when creating ZysCore.xyz role in guild: {GuildName}", 
                    guild.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ZysCore.xyz role in guild: {GuildId}", guild.Id);
            }
        }

        private async Task UpdateZysCoreRoleAsync(DiscordRole role)
        {
            try
            {
                bool needsUpdate = false;
                
                // Check color
                var currentColor = role.Color;
                var desiredColor = new DiscordColor(30, 144, 255);
                
                if (currentColor.R != desiredColor.R || 
                    currentColor.G != desiredColor.G || 
                    currentColor.B != desiredColor.B)
                {
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    await role.ModifyAsync(roleProperties =>
                    {
                        roleProperties.Color = new DiscordColor(30, 144, 255);
                        roleProperties.Hoist = true;
                        roleProperties.Mentionable = true;
                    });
                    _logger.LogDebug("Updated ZysCore.xyz role settings in guild");
                }
            }
            catch (UnauthorizedException)
            {
                _logger.LogWarning("Bot lacks permissions to update ZysCore.xyz role in guild. Role may be above bot's highest role.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ZysCore.xyz role");
            }
        }

        public async Task EnsureRolesOnAllGuildsAsync(DiscordClient client)
        {
            try
            {
                if (client == null)
                {
                    _logger.LogWarning("Cannot setup roles: Discord client is null");
                    return;
                }

                _logger.LogInformation("Setting up roles on all {GuildCount} guilds", client.Guilds.Count);
                
                foreach (var guild in client.Guilds.Values)
                {
                    await CheckAndSetupBotRoleAsync(guild, client);
                }
                
                _logger.LogInformation("Role setup completed for all guilds");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring roles on all guilds");
            }
        }
    }
}