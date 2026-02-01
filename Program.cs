using System;
using System.IO;
using System.IO.Abstractions;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
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
            // 1. Load configuration early to get logging settings
            var appConfig = LoadAppConfig();

            // 2. Initialize Serilog with error handling
            try
            {
                Log.Logger = LoggerSetup.CreateLogger(appConfig.Logging);
            }
            catch (Exception ex)
            {
                // Fallback to console-only logging if file logging fails
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .CreateLogger();
                Log.Warning("Could not initialize file logging, using console only: {Error}", ex.Message);
            }

            try
            {
                Log.Information("GitBuddy started, version: {Version}", GetVersion());

                // Create service collection
                var services = new ServiceCollection();

                // Register Serilog with Microsoft.Extensions.Logging
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog(Log.Logger, dispose: true);
                });

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
            services.AddSingleton<TemplateManager>();
            // IAiService is registered via AddHttpClient above

            // Create registrar and app
            var registrar = new TypeRegistrar(services);
            var app = new CommandApp(registrar);

            app.Configure(config =>
            {

                var version = Assembly.GetExecutingAssembly()
                          .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                          .InformationalVersion ?? "1.0.0";

                var cleanVersion = version.Split('+')[0];

                config.SetApplicationName("git-buddy");
                config.SetApplicationVersion(cleanVersion);

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

                config.AddCommand<LogsCommand>("logs")
                    .WithDescription("View and manage GitBuddy logs.");

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

                // Show first-run logging notice
                ShowFirstRunLoggingNoticeIfNeeded();
            });

                var exitCode = app.Run(args);

                Log.Information("GitBuddy exited with code: {ExitCode}", exitCode);
                return exitCode;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "GitBuddy crashed with unhandled exception");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush(); // Ensure all logs are written before exit
            }
        }

        /// <summary>
        /// Loads application configuration directly from file, avoiding circular dependency.
        /// This is called BEFORE DI container is set up.
        /// </summary>
        private static AppConfig LoadAppConfig()
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gitbuddy",
                "config.json");

            if (!File.Exists(configPath))
            {
                // Return default config
                return new AppConfig
                {
                    Logging = new LoggingConfig()
                };
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Ensure Logging config exists (migration for existing users)
                if (config != null)
                {
                    config.Logging ??= new LoggingConfig();
                    return config;
                }

                return new AppConfig { Logging = new LoggingConfig() };
            }
            catch (Exception)
            {
                // If config is corrupted, use defaults
                return new AppConfig { Logging = new LoggingConfig() };
            }
        }

        /// <summary>
        /// Shows logging notice on first run after upgrade to v1.3.0
        /// </summary>
        private static void ShowFirstRunLoggingNoticeIfNeeded()
        {
            var noticeFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gitbuddy",
                ".logging_notice_shown");

            if (!File.Exists(noticeFilePath))
            {
                try
                {
                    var logDir = LoggingHelper.GetLogDirectory();

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[blue]ℹ️  GitBuddy now includes diagnostic logging to help troubleshoot issues.[/]");
                    AnsiConsole.MarkupLine($"[grey]📂 Logs are stored in: {logDir}[/]");
                    AnsiConsole.MarkupLine("[grey]🔒 Your privacy is protected - no secrets are logged.[/]");
                    AnsiConsole.MarkupLine("[grey]💡 Use[/] [yellow]buddy logs[/] [grey]to view logs or[/] [yellow]buddy logs --clear[/] [grey]to delete them.[/]");
                    AnsiConsole.WriteLine();

                    // Create notice file
                    Directory.CreateDirectory(Path.GetDirectoryName(noticeFilePath)!);
                    File.WriteAllText(noticeFilePath, DateTime.UtcNow.ToString());
                }
                catch
                {
                    // Silently fail if we can't write the notice file
                }
            }
        }

        private static string GetVersion()
        {
            var version = Assembly.GetExecutingAssembly()
                      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                      .InformationalVersion ?? "1.0.0";
            return version.Split('+')[0];
        }
    }
}