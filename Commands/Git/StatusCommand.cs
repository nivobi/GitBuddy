using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Services;

namespace GitBuddy.Commands.Git
{
    public class StatusCommand : AsyncCommand<StatusCommand.Settings>
    {
        public class Settings : CommandSettings
        {
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[grey]Running git status...[/]");

            string gitOutput = GitService.Run("status -s");

            if (string.IsNullOrWhiteSpace(gitOutput))
            {
                AnsiConsole.MarkupLine("[green]✔ Clean workspace. Nothing to do.[/]");
            }
            else
            {
                var panel = new Panel(gitOutput);
                panel.Header = new PanelHeader("Current Changes");
                
                panel.BorderColor(Spectre.Console.Color.Yellow); 
                panel.Expand();

                AnsiConsole.Write(panel);
            }

            return Task.FromResult(0);
        }
    }
}