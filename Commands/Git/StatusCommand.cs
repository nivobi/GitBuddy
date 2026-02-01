using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Git
{
    public class StatusCommand : AsyncCommand<StatusCommand.Settings>
    {
        private readonly IGitService _gitService;
        private readonly ILogger<StatusCommand> _logger;

        public StatusCommand(IGitService gitService, ILogger<StatusCommand> logger)
        {
            _gitService = gitService;
            _logger = logger;
        }

        public class Settings : CommandSettings
        {
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            using var execLog = new CommandExecutionLogger<StatusCommand>(_logger, "status", settings);
            
            AnsiConsole.MarkupLine("[grey]Running git status...[/]");

            var result = await _gitService.RunAsync("status -s", cancellationToken);

            if (string.IsNullOrWhiteSpace(result.Output))
            {
                AnsiConsole.MarkupLine("[green]✔ Clean workspace. Nothing to do.[/]");
            }
            else
            {
                var panel = new Panel(result.Output);
                panel.Header = new PanelHeader("Current Changes");

                panel.BorderColor(Spectre.Console.Color.Yellow);
                panel.Expand();

                AnsiConsole.Write(panel);
            }

            execLog.Complete();
            return 0;
        }
    }
}