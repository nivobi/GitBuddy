using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Git
{
    public class ReleaseCommand : AsyncCommand<ReleaseCommand.Settings>
    {
        private readonly IGitService _gitService;

        public ReleaseCommand(IGitService gitService)
        {
            _gitService = gitService;
        }

        public class Settings : CommandSettings
        {
            [CommandArgument(0, "[BUMP]")]
            [Description("Version bump type: patch, minor, or major")]
            public string? Bump { get; set; }

            [CommandOption("--push|-p")]
            [Description("Push tag to remote immediately")]
            public bool Push { get; set; }

            [CommandOption("--dry-run|-n")]
            [Description("Show what would happen without making changes")]
            public bool DryRun { get; set; }

            [CommandOption("--message|-m <MESSAGE>")]
            [Description("Custom tag message")]
            public string? Message { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            if (!await _gitService.IsGitRepositoryAsync(cancellationToken))
            {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] Not in a git repository.");
                return 1;
            }

            var bump = settings.Bump?.ToLower();

            if (string.IsNullOrWhiteSpace(bump))
            {
                return await ShowStatus(cancellationToken);
            }

            if (bump != "patch" && bump != "minor" && bump != "major")
            {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] Invalid bump type. Use [yellow]patch[/], [yellow]minor[/], or [yellow]major[/].");
                return 1;
            }

            return await CreateRelease(bump, settings, cancellationToken);
        }

        private async Task<int> ShowStatus(CancellationToken cancellationToken)
        {
            var versionResult = await _gitService.RunAsync("describe --tags --abbrev=0", cancellationToken);

            if (versionResult.ExitCode != 0 || string.IsNullOrWhiteSpace(versionResult.Output))
            {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] No tags found in this repository.");
                AnsiConsole.MarkupLine("[grey]Create an initial tag first: git tag -a v1.0.0 -m \"Initial release\"[/]");
                return 1;
            }

            var currentVersion = versionResult.Output.Trim();
            var currentVersionNumber = currentVersion.TrimStart('v');

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new FigletText("GitBuddy").Color(Color.Blue));
            AnsiConsole.MarkupLine("[bold blue]Release Manager[/]");
            AnsiConsole.WriteLine();

            var grid = new Grid();
            grid.AddColumn();
            grid.AddColumn();
            grid.AddRow(new Markup("[bold]Current Version:[/]"), new Markup($"[blue]{currentVersionNumber}[/]"));
            grid.AddRow(new Markup("[bold]Last Tag:[/]"), new Markup($"[blue]{currentVersion}[/]"));
            AnsiConsole.Write(grid);
            AnsiConsole.WriteLine();

            var commitsResult = await _gitService.RunAsync($"log {currentVersion}..HEAD --oneline", cancellationToken);
            var commits = commitsResult.Output
                .Split('\n')
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (commits.Any())
            {
            var displayCount = Math.Min(commits.Count, 10);
            var remaining = commits.Count - displayCount;

            AnsiConsole.MarkupLine($"[bold]Commits since last release[/] ({commits.Count}):");

            foreach (var commit in commits.Take(displayCount))
            {
                var parts = commit.Split(new[] { ' ' }, 2);
                if (parts.Length >= 2)
                {
                    var hash = $"[grey]{parts[0].Substring(0, 7)}[/]";
                    var message = Markup.Escape(parts[1]);
                    AnsiConsole.MarkupLine($"  {hash} {message}");
                }
            }

            if (remaining > 0)
            {
                AnsiConsole.MarkupLine($"  [grey]...and {remaining} more[/]");
            }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No commits since last release.[/]");
            }

            AnsiConsole.WriteLine();

            if (TryParseVersion(currentVersionNumber, out var major, out var minor, out var patch))
            {
                AnsiConsole.MarkupLine("[bold]Suggested versions:[/]");
                AnsiConsole.MarkupLine($"  [blue]patch[/] → [green]{major}.{minor}.{patch + 1}[/] (bug fixes)");
                AnsiConsole.MarkupLine($"  [blue]minor[/] → [green]{major}.{minor + 1}.0[/] (new features)");
                AnsiConsole.MarkupLine($"  [blue]major[/] → [green]{major + 1}.0.0[/] (breaking changes)");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Usage: [yellow]buddy release <patch|minor|major>[/] [[--push]]");

            return 0;
        }

        private async Task<int> CreateRelease(string bump, Settings settings, CancellationToken cancellationToken)
        {
            var versionResult = await _gitService.RunAsync("describe --tags --abbrev=0", cancellationToken);

            if (versionResult.ExitCode != 0 || string.IsNullOrWhiteSpace(versionResult.Output))
            {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] No tags found in this repository.");
                return 1;
            }

            var currentVersion = versionResult.Output.TrimStart('v');

            if (!TryParseVersion(currentVersion, out var major, out var minor, out var patch))
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] Unable to parse version '{currentVersion}'.");
                return 1;
            }

            var (nextMajor, nextMinor, nextPatch) = bump switch
            {
                "patch" => (major, minor, patch + 1),
                "minor" => (major, minor + 1, 0),
                "major" => (major + 1, 0, 0),
                _ => (major, minor, patch)
            };

            var nextVersion = $"{nextMajor}.{nextMinor}.{nextPatch}";
            var tag = $"v{nextVersion}";
            var message = settings.Message ?? $"Release {nextVersion}";

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold blue]Release Plan:[/]");
            var grid = new Grid();
            grid.AddColumn();
            grid.AddColumn();
            grid.AddRow(new Markup("[bold]Type:[/]"), new Markup($"[blue]{bump}[/]"));
            grid.AddRow(new Markup("[bold]Version:[/]"), new Markup($"[green]{nextVersion}[/]"));
            grid.AddRow(new Markup("[bold]Tag:[/]"), new Markup($"[yellow]{tag}[/]"));
            grid.AddRow(new Markup("[bold]Push:[/]"), new Markup(settings.Push ? "[green]Yes[/]" : "[grey]No[/]"));
            grid.AddRow(new Markup("[bold]Message:[/]"), new Markup($"[grey]{Markup.Escape(message)}[/]"));
            AnsiConsole.Write(grid);
            AnsiConsole.WriteLine();

            if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[yellow]Dry run:[/] No changes were made.");
                return 0;
            }

            if (!AnsiConsole.Confirm("Create this release?", false))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                return 0;
            }

            try
            {
                await AnsiConsole.Status().StartAsync("Creating tag...", async ctx =>
            {
                var result = await _gitService.RunAsync($"tag -a {tag} -m \"{message}\"", cancellationToken);
                if (result.ExitCode != 0)
                {
                    throw new Exception($"Failed to create tag: {result.Error}");
                }
            });

            AnsiConsole.MarkupLine("[green]✓[/] Tag created");

            if (settings.Push)
            {
                await AnsiConsole.Status().StartAsync($"Pushing {tag}...", async ctx =>
                {
                    var result = await _gitService.RunAsync($"push origin {tag}", cancellationToken);
                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Failed to push tag: {result.Error}");
                    }
                });

                AnsiConsole.MarkupLine("[green]✓[/] Pushed to remote");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[green]✨ Released {nextVersion}![/]");
                AnsiConsole.MarkupLine("[grey]CI/CD should trigger automatically. Check your GitHub Actions.[/]");
            }
            else
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[green]✨ Released {nextVersion}![/]");
                AnsiConsole.MarkupLine($"[grey]Run 'git push origin {tag}' to push the tag and trigger CI/CD.[/]");
            }

            return 0;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] {ex.Message}");
                return 1;
            }
        }

        private bool TryParseVersion(string version, out int major, out int minor, out int patch)
        {
            major = 0;
            minor = 0;
            patch = 0;

            var parts = version.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            return int.TryParse(parts[0], out major) &&
                   int.TryParse(parts[1], out minor) &&
                   int.TryParse(parts[2], out patch);
        }
    }
}
