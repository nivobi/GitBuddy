using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Config
{
    public class DescribeCommand : AsyncCommand<DescribeCommand.Settings>
    {
        private readonly IAiService _aiService;

        public DescribeCommand(IAiService aiService)
        {
            _aiService = aiService;
        }

        public class Settings : CommandSettings { }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            AnsiConsole.Write(new Rule("[yellow]AI Deep Analysis[/]"));

            // 1. Collect real data by reading key files
            string projectData = "";
            
            await AnsiConsole.Status().StartAsync("Reading project files...", async ctx => 
            {

                await Task.Run(() => {
                    var importantFiles = Directory.GetFiles(".", "*.*", SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileName)
                        .Where(f => f != null && (f.EndsWith(".cs") || f.EndsWith(".csproj") || f.EndsWith(".json")))
                        .Where(f => !f.StartsWith(".")) 
                        .Take(6) 
                        .ToList();

                    foreach (var file in importantFiles)
                    {
                        if (file == null) continue;
                        var lines = File.ReadLines(file).Take(50);
                        projectData += $"\n--- File: {file} ---\n{string.Join("\n", lines)}\n";
                    }
                });
            });

            if (string.IsNullOrWhiteSpace(projectData))
            {
                AnsiConsole.MarkupLine("[red]! No relevant project files found to analyze.[/]");
                return 1;
            }

            // 2. Ask AI to describe the project authoritatively
            string? description = null;
            await AnsiConsole.Status().StartAsync("AI is architecting a summary...", async ctx => {
                description = await _aiService.DescribeProject(projectData);
            });

            if (string.IsNullOrEmpty(description))
            {
                AnsiConsole.MarkupLine("[red]! AI failed to analyze the code snippets.[/]");
                return 1;
            }

            // 3. Display the result
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(new Text(description, new Style(Color.Blue)))
                .Header("AI Authoritative Context")
                .BorderColor(Color.Blue)
                .Padding(1, 1, 1, 1));

            // 4. Confirmation
            if (AnsiConsole.Confirm("Save this authoritative description to [blue].buddycontext[/]?"))
            {
                await File.WriteAllTextAsync(".buddycontext", description);
                AnsiConsole.MarkupLine("[green]✔ Project context updated successfully![/]");
            }

            return 0;
        }
    }
}