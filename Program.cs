using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitBuddy
{
    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandApp();

            app.Configure(config =>
            {

                var version = Assembly.GetExecutingAssembly()
                          .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                          .InformationalVersion ?? "1.0.0";

                config.SetApplicationName("git-buddy");
                config.SetApplicationVersion(version);

                config.AddCommand<StatusCommand>("status")
                    .WithDescription("Check the current state of the repo.");

                config.AddCommand<SaveCommand>("save")
                    .WithDescription("Stage and commit all changes.");

                config.AddCommand<SetupCommand>("setup")
                    .WithDescription("Initialize the repository for the first time.");

                config.AddCommand<SyncCommand>("sync")
                    .WithDescription("Pull latest changes and push your work.");

                config.AddCommand<BranchCommand>("branch")
                    .WithDescription("Smart branch management (create, switch, list, clean).");

                config.AddCommand<UndoCommand>("undo")
                    .WithDescription("Go back in time (safely).");

                config.AddCommand<ConfigCommand>("config")
                    .WithDescription("Setup your AI provider and keys.");

                config.AddCommand<DescribeCommand>("describe")
                    .WithDescription("Analyze the project and update .buddycontext.");

                config.AddCommand<UpdateCommand>("update")
                    .WithDescription("Updates GitBuddy to the latest version from NuGet");

                // --- NEW WELCOME LOGIC START ---
                string welcomeFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitbuddy_welcome");

                if (!File.Exists(welcomeFilePath))
                {
                    AnsiConsole.Write(new FigletText("GitBuddy").Color(Color.Blue));
                    AnsiConsole.MarkupLine("[bold blue]Welcome to GitBuddy![/] Your AI-powered Git companion.");
                    AnsiConsole.MarkupLine("Try typing [yellow]buddy --help[/] to see what I can do.");
                    AnsiConsole.WriteLine();
                    
                    File.WriteAllText(welcomeFilePath, DateTime.Now.ToString());
                }
                // --- NEW WELCOME LOGIC END ---
            });

            return app.Run(args);
        }
    }
}