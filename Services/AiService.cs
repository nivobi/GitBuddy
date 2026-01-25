using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;
using GitBuddy.Infrastructure;

namespace GitBuddy.Services
{
    public class AiService : IAiService
    {
        private readonly IAnsiConsole _console;
        private readonly IFileSystem _fileSystem;
        private readonly IConfigManager _configManager;
        private readonly HttpClient _httpClient;

        public AiService(
            IAnsiConsole console,
            IFileSystem fileSystem,
            IConfigManager configManager,
            HttpClient httpClient)
        {
            _console = console;
            _fileSystem = fileSystem;
            _configManager = configManager;
            _httpClient = httpClient;
        }

        public async Task<string?> GenerateCommitMessage(string diff)
        {
            var (provider, model, apiKey) = _configManager.LoadConfig();
            if (string.IsNullOrEmpty(apiKey))
            {
                _console.MarkupLine("[yellow]⚠ No AI configured.[/] Run [blue]buddy config[/] to set up AI features.");
                return null;
            }

            string projectContext = "a software project";
            if (_fileSystem.File.Exists(".buddycontext"))
                projectContext = _fileSystem.File.ReadAllText(".buddycontext").Trim();

            string apiUrl = GetApiUrl(provider);

            var requestData = new
            {
                model = model,
                messages = new[]
                {
                    new { 
                        role = "system", 
                        content = $"You are an expert developer assistant for: {projectContext}. " +
                                  "Analyze the git diff and write a concise, professional commit message (max 50 chars). " +
                                  "Use imperative mood. Output ONLY the text."
                    },
                    new { role = "user", content = $"Diff:\n{diff}" }
                },
                temperature = 0.3
            };

            return await SendAiRequest(apiUrl, provider, apiKey, requestData);
        }

        public async Task<string?> DescribeProject(string projectData)
        {
            var (provider, model, apiKey) = _configManager.LoadConfig();
            if (string.IsNullOrEmpty(apiKey))
            {
                _console.MarkupLine("[yellow]⚠ No AI configured.[/] Run [blue]buddy config[/] to set up AI features.");
                return null;
            }

            string apiUrl = GetApiUrl(provider);

            var requestData = new
            {
                model = model,
                messages = new[]
                {
                    new { 
                        role = "system", 
                        content = "You are a lead software architect. Analyze the provided code snippets. " +
                                  "Write a definitive, 2-sentence summary describing exactly what this project IS and its primary tech stack. " +
                                  "Do NOT use hedge phrases like 'This appears to be' or 'It seems'. Speak with authority. " +
                                  "Start immediately with 'This is...' or 'This project is...'."
                    },
                    new { role = "user", content = $"Project Code Snippets:\n{projectData}" }
                },
                temperature = 0.2 
            };

            return await SendAiRequest(apiUrl, provider, apiKey, requestData);
        }

        private static string GetApiUrl(string provider)
        {
            return provider.ToLower() switch
            {
                "openai" => "https://api.openai.com/v1/chat/completions",
                "openrouter" => "https://openrouter.ai/api/v1/chat/completions",
                "deepseek" => "https://api.deepseek.com/chat/completions",
                _ => "https://api.openai.com/v1/chat/completions"
            };
        }

        private async Task<string?> SendAiRequest(string apiUrl, string provider, string apiKey, object requestData)
        {
            try
            {
                string jsonContent = JsonSerializer.Serialize(requestData);

                // Create request with headers set per-request instead of on the client
                using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                if (provider == "openrouter")
                {
                    request.Headers.Add("HTTP-Referer", "http://localhost");
                    request.Headers.Add("X-Title", "GitBuddy");
                }

                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Add 30-second timeout to prevent hanging on slow AI API calls
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.SendAsync(request, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _console.MarkupLine("[red]✗ AI Error:[/] Invalid API key. Run [yellow]buddy config[/] to update your credentials.");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _console.MarkupLine("[red]✗ AI Error:[/] Rate limit exceeded. Please try again in a few moments.");
                    }
                    else
                    {
                        _console.MarkupLine($"[red]✗ AI Error:[/] {provider} API returned {response.StatusCode}");
                        if (!string.IsNullOrWhiteSpace(errorBody) && errorBody.Length < 200)
                        {
                            _console.MarkupLine($"[grey]Details: {errorBody.EscapeMarkup()}[/]");
                        }
                    }
                    return null;
                }

                string responseString = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(responseString);
                return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
            }
            catch (TaskCanceledException)
            {
                _console.MarkupLine($"[red]✗ Timeout Error:[/] {provider} API did not respond within 30 seconds.");
                _console.MarkupLine("[grey]The API might be overloaded. Please try again.[/]");
                return null;
            }
            catch (HttpRequestException ex)
            {
                _console.MarkupLine($"[red]✗ Network Error:[/] Unable to connect to {provider}. Check your internet connection.");
                _console.MarkupLine($"[grey]{ex.Message.EscapeMarkup()}[/]");
                return null;
            }
            catch (JsonException ex)
            {
                _console.MarkupLine($"[red]✗ AI Error:[/] Received invalid response from {provider}.");
                _console.MarkupLine($"[grey]{ex.Message.EscapeMarkup()}[/]");
                return null;
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"[red]✗ Unexpected Error:[/] {ex.Message.EscapeMarkup()}");
                _console.MarkupLine("[grey]Please report this issue if it persists.[/]");
                return null;
            }
        }
    }
}