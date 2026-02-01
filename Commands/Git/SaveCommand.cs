using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Git
{
    public class SaveCommand : AsyncCommand<SaveCommand.Settings>
    {
        private readonly IGitService _gitService;
        private readonly IConfigManager _configManager;
        private readonly IAiService _aiService;
        private readonly ILogger<SaveCommand> _logger;

        public SaveCommand(
            IGitService gitService,
            IConfigManager configManager,
            IAiService aiService,
            ILogger<SaveCommand> logger)
        {
            _gitService = gitService;
            _configManager = configManager;
            _aiService = aiService;
            _logger = logger;
        }

        public class Settings : CommandSettings
        {
            [CommandOption("-a|--ai")]
            [Description("Use AI to suggest a commit message.")]
            public bool UseAi { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            using var execLog = new CommandExecutionLogger<SaveCommand>(_logger, "save", settings);

            // Check if we're in a git repository
            if (!await _gitService.IsGitRepositoryAsync(cancellationToken))
            {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] Not in a git repository.");
                AnsiConsole.MarkupLine("[grey]Try running this command from inside a git repository.[/]");
                return 1;
            }

            // 1. Stage changes
            await AnsiConsole.Status().StartAsync("Staging files...", async ctx => {
                await _gitService.RunAsync("add .", cancellationToken);
            });

            string? commitMessage = null;

            // 2. AI Logic
            if (settings.UseAi)
            {
                var (provider, model, apiKey) = _configManager.LoadConfig();

                if (string.IsNullOrEmpty(apiKey))
                {
                    AnsiConsole.MarkupLine("[red]! AI Key missing.[/] Run [yellow]buddy config[/] first.");
                }
                else
                {
                    var diffResult = await _gitService.RunAsync("diff --cached", cancellationToken);
                    string diff = diffResult.Output;

                    if (string.IsNullOrWhiteSpace(diff))
                    {
                        AnsiConsole.MarkupLine("[yellow]! No staged changes found to analyze.[/]");
                    }
                    else
                    {
                        await AnsiConsole.Status().StartAsync("[blue]AI is thinking...[/]", async ctx => {
                            commitMessage = await _aiService.GenerateCommitMessage(diff);
                        });

                        if (string.IsNullOrEmpty(commitMessage))
                        {
                            AnsiConsole.MarkupLine("[grey]AI couldn't come up with a message. Falling back to manual.[/]");
                        }
                    }
                }
            }

            // 3. The Decision Point
            if (string.IsNullOrEmpty(commitMessage))
            {
                commitMessage = AnsiConsole.Ask<string>("[yellow]Enter commit message:[/]");
            }
            else
            {
                AnsiConsole.Write(new Panel(new Text(commitMessage, new Style(Color.Blue)))
                    .Header("AI Suggestion")
                    .BorderColor(Color.Blue));

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .AddChoices(new[] { "✔ Accept", "✎ Edit", "✖ Cancel" }));

                if (choice == "✖ Cancel")
                {
                    AnsiConsole.MarkupLine("[red]Save cancelled.[/]");
                    execLog.Complete(0);
                    return 0;
                }

                if (choice == "✎ Edit")
                {
                    commitMessage = AnsiConsole.Ask<string>("Edit message:", commitMessage);
                }
            }

            // 4. Final Save
            AnsiConsole.MarkupLine("[grey]Saving...[/]");
            var result = await _gitService.RunAsync($"commit -m \"{commitMessage}\"", cancellationToken);

            AnsiConsole.Write(new Panel(new Text(result.Output))
                .Header("✔ Work Saved")
                .BorderColor(Color.Green));

            execLog.Complete(0);
            return 0;
        }
    }
}