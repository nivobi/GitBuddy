using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Git
{
    public class SetupCommand : AsyncCommand<SetupCommand.Settings>
    {
        private readonly IGitService _gitService;

        public SetupCommand(IGitService gitService)
        {
            _gitService = gitService;
        }

        public class Settings : CommandSettings { }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            AnsiConsole.Write(new Rule("[yellow]GitBuddy Setup[/]"));

            // 1. Git Init
            string initOutput = _gitService.Run("init");
            AnsiConsole.MarkupLine(initOutput.Contains("Initialized") 
                ? "[green]✔ Git repository initialized.[/]" 
                : "[yellow]! Already a Git repository.[/]");

            // 2. .gitignore
            if (!File.Exists(".gitignore"))
            {
                AnsiConsole.MarkupLine("[grey]Creating .gitignore...[/]");
                _gitService.Run("new gitignore", "dotnet");
                AnsiConsole.MarkupLine("[green]✔ Added .gitignore[/]");
            }

            // 3. AUTO .buddycontext
            if (!File.Exists(".buddycontext"))
            {
                AnsiConsole.MarkupLine("[grey]Generating project context...[/]");
                
                string projectName = Path.GetFileName(Directory.GetCurrentDirectory()) ?? "Unknown Project";
                string techClue = Directory.GetFiles(".", "*.csproj").Length > 0 ? "a .NET C# project" : "a software project";

                string contextContent = $"Project: {projectName}\nGoal: {techClue}\nTone: Professional and concise.";
                File.WriteAllText(".buddycontext", contextContent);
                
                AnsiConsole.MarkupLine("[green]✔ Created .buddycontext[/]");
            }

            AnsiConsole.Write(new Panel("Project is ready. Run 'buddy save' to save work manually, or 'buddy save --ai' for help.")
                .BorderColor(Color.Green));

            return Task.FromResult(0);
        }
    }
}