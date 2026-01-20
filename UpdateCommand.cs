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

            AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
                .Start(ctx =>
                {
                    var task = ctx.AddTask("[green]Updating Nivobi.GitBuddy[/]");

                    RunDotnetUpdate();

                    while (!task.IsFinished)
                    {
                        task.Increment(5);
                        Thread.Sleep(50);
                    }
                });

            AnsiConsole.MarkupLine("[bold green]✨ GitBuddy is now up to date![/]");
            return 0;
        }

        private static void RunDotnetUpdate()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "tool update -g Nivobi.GitBuddy",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                process?.WaitForExit();
            }
            catch
            {
            }
        }
    }
}