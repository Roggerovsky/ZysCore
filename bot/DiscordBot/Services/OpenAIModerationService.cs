using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace DiscordAutomation.Bot.Services
{
    public class OpenAIModerationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OpenAIModerationService> _logger;
        private readonly IConfiguration _configuration;

        public OpenAIModerationService(
            IHttpClientFactory httpClientFactory,
            ILogger<OpenAIModerationService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<ModerationResult> ModerateContentAsync(string content)
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("OpenAI API key not configured");
                return new ModerationResult { Flagged = false };
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = await client.PostAsJsonAsync("https://api.openai.com/v1/moderations", new
                {
                    input = content,
                    model = _configuration["OpenAI:ModerationModel"] ?? "text-moderation-latest"
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
                    return new ModerationResult
                    {
                        Flagged = result?.Results?[0]?.Flagged ?? false
                    };
                }

                _logger.LogError("OpenAI API error: {StatusCode}", response.StatusCode);
                return new ModerationResult { Flagged = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                return new ModerationResult { Flagged = false };
            }
        }

        public class ModerationResult
        {
            public bool Flagged { get; set; }
        }

        private class OpenAIResponse
        {
            public OpenAIResult[] Results { get; set; } = Array.Empty<OpenAIResult>();
        }

        private class OpenAIResult
        {
            public bool Flagged { get; set; }
        }
    }
}