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
                config.SetApplicationName("git-buddy");

                config.AddCommand<StatusCommand>("status")
                    .WithDescription("Check the current state of the repo.");
                    
                config.AddCommand<SaveCommand>("save")
                    .WithDescription("Stage and commit all changes.");

                config.AddCommand<SetupCommand>("setup")
                    .WithDescription("Initialize the repository for the first time.");

                config.AddCommand<SyncCommand>("sync")
                    .WithDescription("Pull latest changes and push your work.");

                config.AddCommand<UndoCommand>("undo")
                    .WithDescription("Go back in time (safely).");

                config.AddCommand<ConfigCommand>("config")
                    .WithDescription("Setup your AI provider and keys.");

                config.AddCommand<DescribeCommand>("describe")
                    .WithDescription("Analyze the project and update .buddycontext.");

            });

            return app.Run(args);
        }
    }
}