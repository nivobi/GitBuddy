using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitBuddy
{
    public class SaveCommand : AsyncCommand<SaveCommand.Settings>
    {
        public class Settings : CommandSettings 
        {
            [CommandOption("-a|--ai")]
            [Description("Use AI to suggest a commit message.")]
            public bool UseAi { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            // 1. Stage changes
            AnsiConsole.Status().Start("Staging files...", ctx => { 
                GitHelper.Run("add ."); 
            });

            string? commitMessage = null;

            // 2. AI Logic
            if (settings.UseAi)
            {
                var (provider, model, apiKey) = ConfigManager.LoadConfig();

                if (string.IsNullOrEmpty(apiKey))
                {
                    AnsiConsole.MarkupLine("[red]! AI Key missing.[/] Run [yellow]buddy config[/] first.");
                }
                else
                {
                    string diff = GitHelper.Run("diff --cached");
                    
                    if (string.IsNullOrWhiteSpace(diff))
                    {
                        AnsiConsole.MarkupLine("[yellow]! No staged changes found to analyze.[/]");
                    }
                    else
                    {
                        await AnsiConsole.Status().StartAsync("[blue]AI is thinking...[/]", async ctx => {
                            commitMessage = await AiHelper.GenerateCommitMessage(diff);
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
                    return 0;
                }
                
                if (choice == "✎ Edit") 
                {
                    commitMessage = AnsiConsole.Ask<string>("Edit message:", commitMessage);
                }
            }

            // 4. Final Save
            AnsiConsole.MarkupLine("[grey]Saving...[/]");
            string result = GitHelper.Run($"commit -m \"{commitMessage}\"");
            
            AnsiConsole.Write(new Panel(new Text(result))
                .Header("✔ Work Saved")
                .BorderColor(Color.Green));

            return 0;
        }
    }
}