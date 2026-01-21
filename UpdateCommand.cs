using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;

namespace GitBuddy
{
    public class UpdateCommand : Command<UpdateCommand.Settings>
    {
        public class Settings : CommandSettings 
        { 
        }

        public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[bold blue]Checking for GitBuddy updates...[/]");

            var (success, output) = CheckForUpdate();

            if (success && output.Contains("was successfully updated"))
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
            }

            return 0;
        }

        private static (bool success, string output) CheckForUpdate()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "tool update -g Nivobi.GitBuddy",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null) return (false, string.Empty);

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return (process.ExitCode == 0, output + error);
            }
            catch
            {
                return (false, string.Empty);
            }
        }
    }
}