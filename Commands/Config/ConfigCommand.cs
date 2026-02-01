using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Config
{
    public class ConfigCommand : AsyncCommand<ConfigCommand.Settings>
    {
        private readonly IConfigManager _configManager;
        private readonly ILogger<ConfigCommand> _logger;

        public ConfigCommand(IConfigManager configManager, ILogger<ConfigCommand> logger)
        {
            _configManager = configManager;
            _logger = logger;
        }

        public class Settings : CommandSettings
        {
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            using var execLog = new CommandExecutionLogger<ConfigCommand>(_logger, "config", settings);

            AnsiConsole.Write(new FigletText("Git Buddy").Color(Spectre.Console.Color.Blue));

            // 1. Load existing config to see if we have a key saved
            var existingConfig = _configManager.LoadConfig();
            string? apiKey = null;

            // 2. SELECT PROVIDER
            var provider = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose your [green]AI Provider[/]:")
                    .AddChoices(new[] { 
                        "OpenAI (GPT-4o, etc)", 
                        "OpenRouter (DeepSeek, Claude, Gemini, etc)",
                        "DeepSeek (Direct)"
                    }));

            string providerCode = provider switch
            {
                "OpenAI (GPT-4o, etc)" => "openai",
                "OpenRouter (DeepSeek, Claude, Gemini, etc)" => "openrouter",
                "DeepSeek (Direct)" => "deepseek",
                _ => "openai"
            };

            // 3. SELECT MODEL
            string modelCode = providerCode switch {
                "openai" => "gpt-4o-mini",
                "deepseek" => "deepseek-chat",
                _ => "google/gemini-2.0-flash-lite-preview-02-05:free"
            };

            if (providerCode == "openrouter")
            {
                modelCode = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Choose your [blue]OpenRouter Model[/]:")
                        .AddChoices(new[] { 
                            "google/gemini-2.0-flash-exp:free",
                            "deepseek/deepseek-chat",
                            "anthropic/claude-3-haiku"
                        }));
            }

            // 4. SMART KEY LOGIC: Reuse existing key?
            if (!string.IsNullOrEmpty(existingConfig.ApiKey))
            {
                if (AnsiConsole.Confirm("A key is already saved. [yellow]Keep using the existing key?[/]"))
                {
                    apiKey = existingConfig.ApiKey;
                }
            }

            // If no existing key or user wants to enter a new one
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter your [yellow]API Key[/]:").Secret());
            }

            // 5. SAVE
            AnsiConsole.Status().Start("Updating configuration...", ctx => 
            {
                _configManager.SaveConfig(providerCode, modelCode, apiKey);
                Thread.Sleep(500);
            });

            AnsiConsole.MarkupLine("[green]✔ Configuration updated successfully![/]");
            execLog.Complete(0);
            return Task.FromResult(0);
        }
    }
}