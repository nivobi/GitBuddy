using System.IO.Abstractions;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Commands.Git;
using GitBuddy.Commands.Config;
using GitBuddy.Commands.Utility;
using GitBuddy.Commands.CICD;
using GitBuddy.Infrastructure;
using GitBuddy.Services;

namespace GitBuddy
{
    class Program
    {
        static int Main(string[] args)
        {
            // Create service collection
            var services = new ServiceCollection();

            // Register infrastructure services
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddHttpClient<IAiService, AiService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "GitBuddy/1.2");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Allow automatic decompression
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                // Use system proxy settings
                UseProxy = true,
                // Don't use default credentials for proxy (can cause issues)
                UseDefaultCredentials = false
            });
            services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);

            // Register application services
            services.AddSingleton<IConfigManager, ConfigManager>();
            services.AddSingleton<IGitService, GitService>();
            services.AddSingleton<IEmbeddedResourceLoader, EmbeddedResourceLoader>();
            // IAiService is registered via AddHttpClient above

            // Create registrar and app
            var registrar = new TypeRegistrar(services);
            var app = new CommandApp(registrar);

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

                config.AddCommand<MergeCommand>("merge")
                    .WithDescription("Merge branches with conflict detection and AI-powered messages.");

                config.AddCommand<StashCommand>("stash")
                    .WithDescription("Manage stashes (push, pop, apply, list).");

                config.AddCommand<UndoCommand>("undo")
                    .WithDescription("Go back in time (safely).");

                config.AddCommand<ConfigCommand>("config")
                    .WithDescription("Setup your AI provider and keys.");

                config.AddCommand<DescribeCommand>("describe")
                    .WithDescription("Analyze the project and update .buddycontext.");

                config.AddCommand<UpdateCommand>("update")
                    .WithDescription("Updates GitBuddy to the latest version from NuGet");

                config.AddCommand<CiCdCommand>("cicd")
                    .WithDescription("Generate a CI/CD workflow for your project.");
                
                config.AddCommand<CiCdInitCommand>("cicd-init")
                    .WithDescription("Interactive CI/CD setup wizard.");
                
                config.AddCommand<CiCdExportCommand>("cicd-export")
                    .WithDescription("Export CI/CD templates for customization.");

                config.AddCommand<ReleaseCommand>("release")
                    .WithDescription("Create a new release (manage version tags).");

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