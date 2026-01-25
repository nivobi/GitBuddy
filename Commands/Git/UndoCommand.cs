using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Git
{
    public class UndoCommand : AsyncCommand<UndoCommand.Settings>
    {
        private readonly IGitService _gitService;

        public UndoCommand(IGitService gitService)
        {
            _gitService = gitService;
        }

        public class Settings : CommandSettings
        {
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            // 1. Ask the user what they want to do
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]What would you like to undo?[/]")
                    .AddChoices(new[] {
                        "Cancel (Do nothing)",
                        "Undo last save (Keep your changes)",
                        "Discard current changes (DANGER: Deletes unsaved work)"
                    }));

            // 2. Handle the choice
            if (choice == "Cancel (Do nothing)")
            {
                AnsiConsole.MarkupLine("[grey]Operation cancelled.[/]");
                return 0;
            }

            if (choice == "Undo last save (Keep your changes)")
            {
                AnsiConsole.MarkupLine("[grey]Undoing last commit...[/]");
                await _gitService.RunAsync("reset --soft HEAD~1", cancellationToken);
                AnsiConsole.MarkupLine("[green]✔ Last save undone. Your files are still here, just unstaged.[/]");
            }
            else if (choice == "Discard current changes (DANGER: Deletes unsaved work)")
            {
                if (!AnsiConsole.Confirm("[red]Are you sure? This will delete all unsaved work permanently.[/]"))
                {
                    AnsiConsole.MarkupLine("[grey]Phew. Cancelled.[/]");
                    return 0;
                }

                AnsiConsole.MarkupLine("[grey]Restoring files to last known good state...[/]");
                await _gitService.RunAsync("restore .", cancellationToken);
                await _gitService.RunAsync("clean -fd", cancellationToken);
                AnsiConsole.MarkupLine("[green]✔ Changes discarded. You are back to your last save.[/]");
            }

            return 0;
        }
    }
}