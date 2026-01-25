using Spectre.Console;
using Spectre.Console.Cli;
using System.Threading;
using System.Threading.Tasks;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Utility
{
    public class UpdateCommand : AsyncCommand<UpdateCommand.Settings>
    {
        private readonly IProcessRunner _processRunner;

        public UpdateCommand(IProcessRunner processRunner)
        {
            _processRunner = processRunner;
        }

        public class Settings : CommandSettings
        {
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[bold blue]Checking for GitBuddy updates...[/]");

            ProcessResult result = null!;
            await AnsiConsole.Status().StartAsync("Checking for updates...", async ctx =>
            {
                result = await _processRunner.RunAsync("dotnet", "tool update -g Nivobi.GitBuddy", cancellationToken);
            });

            var output = result.Output + result.Error;

            if (result.ExitCode == 0 && output.Contains("was successfully updated"))
            {
                AnsiConsole.Progress()
                    .AutoClear(false)
                    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
                    .Start(ctx =>
                    {
                        var task = ctx.AddTask("[green]Updating Nivobi.GitBuddy[/]");
                        task.Increment(100);
                    });

                AnsiConsole.MarkupLine("[bold green]✨ GitBuddy has been updated to the latest version![/]");
            }
            else if (output.Contains("is already installed"))
            {
                AnsiConsole.MarkupLine("[bold green]✓[/] GitBuddy is already up to date!");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] Unable to check for updates. Please try again later.");
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    AnsiConsole.MarkupLine($"[grey]{result.Error}[/]");
                }
            }

            return 0;
        }
    }
}