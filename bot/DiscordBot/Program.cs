using DiscordAutomation.Bot;
using DiscordAutomation.Bot.EventHandlers;
using DiscordAutomation.Bot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

try
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            config.SetBasePath(Directory.GetCurrentDirectory());
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            var configuration = context.Configuration;
            
            // Configure Redis
            services.AddSingleton<IConnectionMultiplexer>(sp => 
                ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis") 
                    ?? configuration["Redis:ConnectionString"] 
                    ?? "localhost:6379"));
            
            // Register services
            services.AddSingleton<DiscordBotService>();
            services.AddSingleton<RedisCacheService>();
            services.AddSingleton<ApiClientService>();
            services.AddSingleton<RuleProcessorService>();
            services.AddSingleton<OpenAIModerationService>();
            
            // Event handlers
            services.AddSingleton<MessageEventHandler>();
            services.AddSingleton<UserEventHandler>();
            services.AddSingleton<ReactionEventHandler>();
            
            // Hosted service (the bot)
            services.AddHostedService<BotBackgroundService>();
            
            // HTTP Client Factory for ApiClientService
            services.AddHttpClient();
            
            // Logging
            services.AddLogging(logging =>
            {
                logging.AddConsole();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
            });
        })
        .ConfigureLogging((context, logging) =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddConfiguration(context.Configuration.GetSection("Logging"));
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}