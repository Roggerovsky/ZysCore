using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordAutomation.Bot.Services
{
    public class BotBackgroundService : BackgroundService
    {
        private readonly DiscordBotService _botService;
        private readonly ILogger<BotBackgroundService> _logger;

        public BotBackgroundService(
            DiscordBotService botService,
            ILogger<BotBackgroundService> logger)
        {
            _botService = botService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Discord Bot Background Service starting...");

            try
            {
                await _botService.StartAsync(stoppingToken);
                
                // Keep the service running
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Bot service is stopping...");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Bot service crashed!");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Discord Bot Background Service stopping...");
            await _botService.StopAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}