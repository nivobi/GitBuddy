using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitBuddy
{
    public class BranchCommand : AsyncCommand<BranchCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandArgument(0, "[action]")]
            [Description("Action: create, switch, list, or clean")]
            public string? Action { get; set; }

            [CommandArgument(1, "[name]")]
            [Description("Branch name (for create action)")]
            public string? Name { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            // Check if we're in a git repository
            if (!IsGitRepository())
            {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] Not in a git repository.");
                AnsiConsole.MarkupLine("[grey]Try running this command from inside a git repository.[/]");
                return 1;
            }

            var action = settings.Action?.ToLower() ?? "list";

            return action switch
            {
                "create" => await CreateBranch(settings.Name),
                "switch" => await SwitchBranch(),
                "list" => await ListBranches(),
                "clean" => await CleanBranches(),
                _ => await ShowHelp()
            };
        }

        private static bool IsGitRepository()
        {
            var result = GitHelper.Run("rev-parse --is-inside-work-tree");
            return result.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<int> CreateBranch(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Please provide a branch name.");
                AnsiConsole.MarkupLine("Usage: [yellow]buddy branch create <name>[/]");
                return 1;
            }

            // Ask for branch type
            var branchType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What [green]type[/] of branch?")
                    .AddChoices(new[] { "feature", "bugfix", "hotfix", "experiment", "none (just use the name)" }));

            string fullBranchName;
            if (branchType == "none (just use the name)")
            {
                fullBranchName = name;
            }
            else
            {
                fullBranchName = $"{branchType}/{name}";
            }

            await Task.Run(() =>
            {
                AnsiConsole.Status().Start($"Creating branch [blue]{fullBranchName}[/]...", ctx =>
                {
                    var result = GitHelper.Run($"checkout -b {fullBranchName}");

                    if (result.Contains("Switched to a new branch") || result.Contains("switched to a new branch"))
                    {
                        AnsiConsole.MarkupLine($"[green]✓[/] Created and switched to branch [blue]{fullBranchName}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] Failed to create branch: {result}");
                    }
                });
            });

            return 0;
        }

        private async Task<int> SwitchBranch()
        {
            await Task.Run(() =>
            {
                // Get all branches
                var branchOutput = GitHelper.Run("branch -a");
                if (string.IsNullOrWhiteSpace(branchOutput))
                {
                    AnsiConsole.MarkupLine("[yellow]No branches found.[/]");
                    return;
                }

                // Parse branches
                var branches = branchOutput
                    .Split('\n')
                    .Select(b => b.Trim().TrimStart('*').Trim())
                    .Where(b => !string.IsNullOrWhiteSpace(b) && !b.Contains("HEAD ->"))
                    .Select(b => b.Replace("remotes/origin/", ""))
                    .Distinct()
                    .OrderBy(b => b)
                    .ToList();

                if (!branches.Any())
                {
                    AnsiConsole.MarkupLine("[yellow]No branches available.[/]");
                    return;
                }

                // Show selection menu
                var selectedBranch = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Switch to which [green]branch[/]?")
                        .PageSize(10)
                        .AddChoices(branches));

                // Check for uncommitted changes first
                var statusCheck = GitHelper.Run("status --porcelain");
                if (!string.IsNullOrWhiteSpace(statusCheck))
                {
                    AnsiConsole.MarkupLine("[yellow]⚠[/] You have uncommitted changes.");
                    AnsiConsole.MarkupLine("[grey]Please commit or stash your changes before switching branches.[/]");
                    AnsiConsole.MarkupLine($"\nTry: [blue]buddy save[/] to commit your changes");
                    return;
                }

                // Switch to selected branch
                AnsiConsole.Status().Start($"Switching to [blue]{selectedBranch}[/]...", ctx =>
                {
                    var result = GitHelper.Run($"checkout {selectedBranch}");

                    if (result.Contains("Switched to") || result.Contains("Already on"))
                    {
                        AnsiConsole.MarkupLine($"[green]✓[/] Switched to branch [blue]{selectedBranch}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] Failed to switch: {result}");
                    }
                });
            });

            return 0;
        }

        private async Task<int> ListBranches()
        {
            await Task.Run(() =>
            {
                var currentBranch = GitHelper.Run("branch --show-current");
                var branchOutput = GitHelper.Run("branch -vv");

                if (string.IsNullOrWhiteSpace(branchOutput))
                {
                    AnsiConsole.MarkupLine("[yellow]No branches found.[/]");
                    return;
                }

                var table = new Table();
                table.Border(TableBorder.Rounded);
                table.AddColumn(new TableColumn("[bold]Branch[/]").Centered());
                table.AddColumn(new TableColumn("[bold]Commit[/]"));
                table.AddColumn(new TableColumn("[bold]Message[/]"));

                foreach (var line in branchOutput.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var isCurrent = line.StartsWith("*");
                    var cleanLine = line.TrimStart('*').Trim();
                    var parts = cleanLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 2) continue;

                    var branchName = parts[0];
                    var commitHash = parts[1];
                    var message = string.Join(" ", parts.Skip(2));

                    // Remove tracking info in brackets
                    if (message.Contains('['))
                    {
                        var bracketIndex = message.IndexOf('[');
                        var endBracketIndex = message.IndexOf(']', bracketIndex);
                        if (endBracketIndex > bracketIndex)
                        {
                            message = message[(endBracketIndex + 1)..].Trim();
                        }
                    }

                    var branchDisplay = isCurrent ? $"[green]* {branchName}[/]" : $"  {branchName}";
                    table.AddRow(branchDisplay, $"[grey]{commitHash}[/]", message);
                }

                AnsiConsole.Write(table);
            });

            return 0;
        }

        private async Task<int> CleanBranches()
        {
            await Task.Run(() =>
            {
                AnsiConsole.MarkupLine("[blue]Checking for merged branches...[/]");

                // Get current branch
                var currentBranch = GitHelper.Run("branch --show-current");

                // Get merged branches (excluding current and main/master)
                var mergedOutput = GitHelper.Run("branch --merged");
                var mergedBranches = mergedOutput
                    .Split('\n')
                    .Select(b => b.Trim().TrimStart('*').Trim())
                    .Where(b => !string.IsNullOrWhiteSpace(b) &&
                                b != currentBranch &&
                                b != "main" &&
                                b != "master")
                    .ToList();

                if (!mergedBranches.Any())
                {
                    AnsiConsole.MarkupLine("[green]✓[/] No merged branches to clean up!");
                    return;
                }

                // Show branches that will be deleted
                var table = new Table();
                table.Border(TableBorder.Rounded);
                table.AddColumn("[bold]Branches to Delete[/]");

                foreach (var branch in mergedBranches)
                {
                    table.AddRow($"[red]{branch}[/]");
                }

                AnsiConsole.Write(table);

                // Confirm deletion
                var confirm = AnsiConsole.Confirm($"Delete these [red]{mergedBranches.Count}[/] merged branches?", false);

                if (!confirm)
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                    return;
                }

                // Delete branches
                var deletedCount = 0;
                foreach (var branch in mergedBranches)
                {
                    var result = GitHelper.Run($"branch -d {branch}");
                    if (result.Contains("Deleted branch"))
                    {
                        AnsiConsole.MarkupLine($"[green]✓[/] Deleted [grey]{branch}[/]");
                        deletedCount++;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] Failed to delete {branch}: {result}");
                    }
                }

                AnsiConsole.MarkupLine($"\n[green]✓[/] Cleaned up {deletedCount} branch(es)!");
            });

            return 0;
        }

        private async Task<int> ShowHelp()
        {
            await Task.Run(() =>
            {
                var panel = new Panel(
                    "[bold]buddy branch[/] - Smart branch management\n\n" +
                    "[yellow]Actions:[/]\n" +
                    "  [blue]create <name>[/]  - Create a new branch with naming conventions\n" +
                    "  [blue]switch[/]         - Interactive branch switcher\n" +
                    "  [blue]list[/]           - Show all branches with commit info\n" +
                    "  [blue]clean[/]          - Remove merged branches\n\n" +
                    "[grey]Examples:[/]\n" +
                    "  buddy branch list\n" +
                    "  buddy branch create dark-mode\n" +
                    "  buddy branch switch\n" +
                    "  buddy branch clean"
                );
                panel.Header = new PanelHeader("Branch Command Help");
                panel.BorderColor(Color.Blue);

                AnsiConsole.Write(panel);
            });

            return 0;
        }
    }
}
