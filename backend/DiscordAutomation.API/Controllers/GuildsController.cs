using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DiscordAutomation.API.Data;
using DiscordAutomation.API.Models;
using DiscordAutomation.API.DTOs.Requests;
using DiscordAutomation.API.DTOs.Responses;

namespace DiscordAutomation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GuildsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GuildsController> _logger;

        public GuildsController(
            ApplicationDbContext context,
            ILogger<GuildsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/guilds
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<Guild>>>> GetGuilds(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Guilds
                    .Where(g => g.IsActive)
                    .OrderByDescending(g => g.CreatedAt);

                var totalCount = await query.CountAsync();
                var guilds = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var response = new PaginatedResponse<Guild>
                {
                    Items = guilds,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

                return Ok(ApiResponse<PaginatedResponse<Guild>>.Ok(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting guilds");
                return StatusCode(500, ApiResponse<List<Guild>>.Fail("Internal server error"));
            }
        }

        // GET: api/guilds/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<Guild>>> GetGuild(ulong id)
        {
            try
            {
                var guild = await _context.Guilds
                    .Include(g => g.Modules)
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (guild == null)
                {
                    return NotFound(ApiResponse<Guild>.Fail("Guild not found"));
                }

                return Ok(ApiResponse<Guild>.Ok(guild));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting guild {GuildId}", id);
                return StatusCode(500, ApiResponse<Guild>.Fail("Internal server error"));
            }
        }

        // POST: api/guilds
        [HttpPost]
        public async Task<ActionResult<ApiResponse<Guild>>> CreateGuild(CreateGuildRequest request)
        {
            try
            {
                if (await _context.Guilds.AnyAsync(g => g.Id == request.Id))
                {
                    return Conflict(ApiResponse<Guild>.Fail("Guild already exists"));
                }

                var guild = new Guild
                {
                    Id = request.Id,
                    Name = request.Name,
                    OwnerId = request.OwnerId,
                    IconUrl = request.IconUrl
                };

                _context.Guilds.Add(guild);
                await _context.SaveChangesAsync();

                // Create default modules for the guild
                await CreateDefaultModulesForGuild(guild.Id);

                return CreatedAtAction(
                    nameof(GetGuild),
                    new { id = guild.Id },
                    ApiResponse<Guild>.Ok(guild, "Guild created successfully")
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating guild");
                return StatusCode(500, ApiResponse<Guild>.Fail("Internal server error"));
            }
        }

        // PUT: api/guilds/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<Guild>>> UpdateGuild(
            ulong id,
            UpdateGuildRequest request)
        {
            try
            {
                var guild = await _context.Guilds.FindAsync(id);
                if (guild == null)
                {
                    return NotFound(ApiResponse<Guild>.Fail("Guild not found"));
                }

                guild.Name = request.Name;
                guild.IsActive = request.IsActive;
                guild.PremiumTier = request.PremiumTier;
                guild.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(ApiResponse<Guild>.Ok(guild, "Guild updated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating guild {GuildId}", id);
                return StatusCode(500, ApiResponse<Guild>.Fail("Internal server error"));
            }
        }

        // DELETE: api/guilds/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteGuild(ulong id)
        {
            try
            {
                var guild = await _context.Guilds.FindAsync(id);
                if (guild == null)
                {
                    return NotFound(ApiResponse<object>.Fail("Guild not found"));
                }

                guild.IsActive = false;
                guild.LeftAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<object>.Ok(null, "Guild marked as inactive"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting guild {GuildId}", id);
                return StatusCode(500, ApiResponse<object>.Fail("Internal server error"));
            }
        }

        private async Task CreateDefaultModulesForGuild(ulong guildId)
        {
            var defaultModules = new[]
            {
                new Module
                {
                    GuildId = guildId,
                    Type = ModuleType.Moderation,
                    Name = "Moderation",
                    IsEnabled = true,
                    Configuration = "{\"enabled\": true}"
                },
                new Module
                {
                    GuildId = guildId,
                    Type = ModuleType.Tickets,
                    Name = "Tickets",
                    IsEnabled = false,
                    Configuration = "{\"enabled\": false, \"categoryId\": null}"
                },
                new Module
                {
                    GuildId = guildId,
                    Type = ModuleType.Welcome,
                    Name = "Welcome",
                    IsEnabled = false,
                    Configuration = "{\"enabled\": false, \"channelId\": null}"
                }
            };

            _context.Modules.AddRange(defaultModules);
            await _context.SaveChangesAsync();
        }
    }
}