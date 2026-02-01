using Spectre.Console;
using Spectre.Console.Cli;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Utility
{
    public class UpdateCommand : AsyncCommand<UpdateCommand.Settings>
    {
        private readonly IProcessRunner _processRunner;
        private readonly ILogger<UpdateCommand> _logger;

        public UpdateCommand(IProcessRunner processRunner, ILogger<UpdateCommand> logger)
        {
            _processRunner = processRunner;
            _logger = logger;
        }

        public class Settings : CommandSettings
        {
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            using var execLog = new CommandExecutionLogger<UpdateCommand>(_logger, "update", settings);

            AnsiConsole.MarkupLine("[bold blue]Checking for GitBuddy updates...[/]");

            ProcessResult result = null!;
            await AnsiConsole.Status().StartAsync("Updating GitBuddy...", async ctx =>
            {
                // Use 120 second timeout for update operations (network dependent)
                result = await _processRunner.RunAsync("dotnet", "tool update -g Nivobi.GitBuddy", 120000, cancellationToken);
            });

            var output = result.Output + result.Error;

            if (result.ExitCode == 0 && output.Contains("was successfully updated"))
            {
                AnsiConsole.MarkupLine("[bold green]✨ GitBuddy has been updated to the latest version![/]");
            }
            else if (output.Contains("is already installed"))
            {
                AnsiConsole.MarkupLine("[bold green]✓[/] GitBuddy is already up to date!");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] Unable to check for updates. Please try again later.");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    AnsiConsole.MarkupLine($"[grey]{output}[/]");
                }
            }

            execLog.Complete(0);
            return 0;
        }
    }
}