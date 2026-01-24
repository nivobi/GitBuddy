using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;

namespace GitBuddy.Services
{
    public static class AiService
    {
        // Reuse a single HttpClient instance to prevent socket exhaustion
        private static readonly HttpClient _httpClient = new HttpClient();
        public static async Task<string?> GenerateCommitMessage(string diff)
        {
            var (provider, model, apiKey) = ConfigManager.LoadConfig();
            if (string.IsNullOrEmpty(apiKey))
            {
                AnsiConsole.MarkupLine("[yellow]⚠ No AI configured.[/] Run [blue]buddy config[/] to set up AI features.");
                return null;
            }

            string projectContext = "a software project";
            if (File.Exists(".buddycontext")) projectContext = File.ReadAllText(".buddycontext").Trim();

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

        public static async Task<string?> DescribeProject(string projectData)
        {
            var (provider, model, apiKey) = ConfigManager.LoadConfig();
            if (string.IsNullOrEmpty(apiKey))
            {
                AnsiConsole.MarkupLine("[yellow]⚠ No AI configured.[/] Run [blue]buddy config[/] to set up AI features.");
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

        private static async Task<string?> SendAiRequest(string apiUrl, string provider, string apiKey, object requestData)
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

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        AnsiConsole.MarkupLine("[red]✗ AI Error:[/] Invalid API key. Run [yellow]buddy config[/] to update your credentials.");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        AnsiConsole.MarkupLine("[red]✗ AI Error:[/] Rate limit exceeded. Please try again in a few moments.");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]✗ AI Error:[/] {provider} API returned {response.StatusCode}");
                        if (!string.IsNullOrWhiteSpace(errorBody) && errorBody.Length < 200)
                        {
                            AnsiConsole.MarkupLine($"[grey]Details: {errorBody.EscapeMarkup()}[/]");
                        }
                    }
                    return null;
                }

                string responseString = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(responseString);
                return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
            }
            catch (HttpRequestException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Network Error:[/] Unable to connect to {provider}. Check your internet connection.");
                AnsiConsole.MarkupLine($"[grey]{ex.Message.EscapeMarkup()}[/]");
                return null;
            }
            catch (JsonException ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ AI Error:[/] Received invalid response from {provider}.");
                AnsiConsole.MarkupLine($"[grey]{ex.Message.EscapeMarkup()}[/]");
                return null;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Unexpected Error:[/] {ex.Message.EscapeMarkup()}");
                AnsiConsole.MarkupLine("[grey]Please report this issue if it persists.[/]");
                return null;
            }
        }
    }
}