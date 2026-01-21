using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitBuddy
{
    public class MergeCommand : AsyncCommand<MergeCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandArgument(0, "[branch]")]
            [Description("Branch to merge from (interactive if not specified)")]
            public string? Branch { get; set; }

            [CommandOption("--ai")]
            [Description("Use AI to generate merge commit message")]
            public bool UseAi { get; set; }
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

            // Get current branch
            string currentBranch = GitHelper.Run("branch --show-current");
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] Could not determine current branch.");
                return 1;
            }

            // Get branch to merge from
            string? branchToMerge = settings.Branch;

            if (string.IsNullOrWhiteSpace(branchToMerge))
            {
                branchToMerge = await SelectBranch(currentBranch);
                if (string.IsNullOrWhiteSpace(branchToMerge))
                {
                    return 0; // User cancelled
                }
            }

            // Check if branch exists
            var branchCheck = GitHelper.Run($"rev-parse --verify {branchToMerge}");
            if (branchCheck.Contains("fatal"))
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] Branch [blue]{branchToMerge}[/] does not exist.");
                return 1;
            }

            // Show what will be merged
            AnsiConsole.Write(new Rule($"[blue]Merging {branchToMerge} into {currentBranch}[/]"));
            AnsiConsole.WriteLine();

            // Check for uncommitted changes
            var statusCheck = GitHelper.Run("status --porcelain");
            if (!string.IsNullOrWhiteSpace(statusCheck))
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Warning:[/] You have uncommitted changes.");
                if (!AnsiConsole.Confirm("Continue with merge anyway?", false))
                {
                    AnsiConsole.MarkupLine("[yellow]Merge cancelled.[/]");
                    return 0;
                }
            }

            // Preview commits that will be merged
            await ShowMergePreview(currentBranch, branchToMerge);

            // Confirm merge
            if (!AnsiConsole.Confirm($"\nMerge [blue]{branchToMerge}[/] into [blue]{currentBranch}[/]?", true))
            {
                AnsiConsole.MarkupLine("[yellow]Merge cancelled.[/]");
                return 0;
            }

            // Attempt the merge
            return await PerformMerge(branchToMerge, currentBranch, settings.UseAi);
        }

        private async Task<string?> SelectBranch(string currentBranch)
        {
            return await Task.Run(() =>
            {
                var branchOutput = GitHelper.Run("branch -a");
                if (string.IsNullOrWhiteSpace(branchOutput))
                {
                    AnsiConsole.MarkupLine("[yellow]No branches found.[/]");
                    return null;
                }

                var branches = branchOutput
                    .Split('\n')
                    .Select(b => b.Trim().TrimStart('*').Trim())
                    .Where(b => !string.IsNullOrWhiteSpace(b) &&
                                b != currentBranch &&
                                !b.Contains("HEAD ->"))
                    .Select(b => b.Replace("remotes/origin/", ""))
                    .Distinct()
                    .OrderBy(b => b)
                    .ToList();

                if (!branches.Any())
                {
                    AnsiConsole.MarkupLine("[yellow]No other branches available to merge.[/]");
                    return null;
                }

                return AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Which branch do you want to [green]merge[/]?")
                        .PageSize(10)
                        .AddChoices(branches));
            });
        }

        private async Task ShowMergePreview(string currentBranch, string branchToMerge)
        {
            await Task.Run(() =>
            {
                // Check if it's a fast-forward merge
                var mergeBase = GitHelper.Run($"merge-base {currentBranch} {branchToMerge}");
                var currentCommit = GitHelper.Run($"rev-parse {currentBranch}");

                bool isFastForward = mergeBase.Trim() == currentCommit.Trim();

                if (isFastForward)
                {
                    AnsiConsole.MarkupLine("[green]✓[/] This will be a fast-forward merge (clean history).");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]ℹ[/] This will create a merge commit.");
                }

                // Show commits that will be merged
                var commits = GitHelper.Run($"log {currentBranch}..{branchToMerge} --oneline --max-count=10");

                if (!string.IsNullOrWhiteSpace(commits))
                {
                    var table = new Table();
                    table.Border(TableBorder.Rounded);
                    table.AddColumn("[bold]Commits to Merge[/]");

                    foreach (var commit in commits.Split('\n').Where(c => !string.IsNullOrWhiteSpace(c)))
                    {
                        table.AddRow($"[grey]{commit}[/]");
                    }

                    AnsiConsole.Write(table);

                    // Check if there are more commits
                    var totalCommits = GitHelper.Run($"rev-list --count {currentBranch}..{branchToMerge}");
                    if (int.TryParse(totalCommits.Trim(), out int count) && count > 10)
                    {
                        AnsiConsole.MarkupLine($"[grey]... and {count - 10} more commit(s)[/]");
                    }
                }
            });
        }

        private async Task<int> PerformMerge(string branchToMerge, string currentBranch, bool useAi)
        {
            string mergeResult = "";
            bool hasConflicts = false;

            await AnsiConsole.Status().StartAsync("Merging...", async ctx =>
            {
                await Task.Run(() =>
                {
                    mergeResult = GitHelper.Run($"merge {branchToMerge} --no-edit");
                    hasConflicts = mergeResult.Contains("CONFLICT") || mergeResult.Contains("Automatic merge failed");
                });
            });

            if (hasConflicts)
            {
                return await HandleConflicts(branchToMerge);
            }

            // Check if it was a fast-forward merge
            bool wasFastForward = mergeResult.Contains("Fast-forward");

            if (wasFastForward)
            {
                AnsiConsole.MarkupLine($"[green]✓ Fast-forward merge complete![/]");
                AnsiConsole.MarkupLine($"[grey]{mergeResult}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✓ Merge complete![/]");

                // If AI was requested and there's a merge commit, we could regenerate the message
                // but git has already created it, so we'll just show it
                var lastCommit = GitHelper.Run("log -1 --pretty=%B");
                AnsiConsole.MarkupLine($"[grey]Merge commit:[/] {lastCommit}");
            }

            // Ask if they want to delete the merged branch
            if (AnsiConsole.Confirm($"\nDelete branch [blue]{branchToMerge}[/] now that it's merged?", false))
            {
                var deleteResult = GitHelper.Run($"branch -d {branchToMerge}");
                if (deleteResult.Contains("Deleted branch"))
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] Deleted branch [grey]{branchToMerge}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Note:[/] {deleteResult}");
                }
            }

            return 0;
        }

        private async Task<int> HandleConflicts(string branchToMerge)
        {
            await Task.Run(() =>
            {
                AnsiConsole.Write(new Rule("[red]Merge Conflicts Detected[/]"));
                AnsiConsole.WriteLine();

                // Get list of conflicted files
                var conflictedFiles = GitHelper.Run("diff --name-only --diff-filter=U");

                if (!string.IsNullOrWhiteSpace(conflictedFiles))
                {
                    var table = new Table();
                    table.Border(TableBorder.Rounded);
                    table.AddColumn("[bold]Files with Conflicts[/]");

                    foreach (var file in conflictedFiles.Split('\n').Where(f => !string.IsNullOrWhiteSpace(f)))
                    {
                        table.AddRow($"[red]{file}[/]");
                    }

                    AnsiConsole.Write(table);
                }

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]What would you like to do?[/]");

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .AddChoices(new[] {
                            "Abort merge (go back to before merge)",
                            "Open files manually to resolve conflicts",
                            "Show me the conflicts"
                        }));

                if (choice.StartsWith("Abort"))
                {
                    GitHelper.Run("merge --abort");
                    AnsiConsole.MarkupLine("[yellow]Merge aborted. Your branch is back to its previous state.[/]");
                }
                else if (choice.StartsWith("Show"))
                {
                    var conflicts = GitHelper.Run("diff --diff-filter=U");
                    var panel = new Panel(conflicts.Length > 1000 ? conflicts.Substring(0, 1000) + "\n..." : conflicts);
                    panel.Header = new PanelHeader("Conflict Details");
                    panel.BorderColor(Color.Red);
                    AnsiConsole.Write(panel);

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[grey]To resolve:[/]");
                    AnsiConsole.MarkupLine("  1. Edit the conflicted files");
                    AnsiConsole.MarkupLine("  2. Run [blue]buddy save[/] when done");
                }
                else
                {
                    AnsiConsole.MarkupLine("[grey]To resolve conflicts:[/]");
                    AnsiConsole.MarkupLine("  1. Open and edit the conflicted files");
                    AnsiConsole.MarkupLine("  2. Look for [red]<<<<<<<[/], [yellow]=======[/], and [red]>>>>>>>[/] markers");
                    AnsiConsole.MarkupLine("  3. Choose which changes to keep");
                    AnsiConsole.MarkupLine("  4. Run [blue]buddy save[/] to complete the merge");
                }
            });

            return 1;
        }

        private static bool IsGitRepository()
        {
            var result = GitHelper.Run("rev-parse --is-inside-work-tree");
            return result.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
