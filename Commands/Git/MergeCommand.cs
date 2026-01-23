using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Services;

namespace GitBuddy.Commands.Git
{
    public class MergeCommand : AsyncCommand<MergeCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandArgument(0, "[branch]")]
            [Description("Branch name - source in normal mode, target in --into mode")]
            public string? Branch { get; set; }

            [CommandOption("--into")]
            [Description("Reverse merge: merge current branch into target (interactive if branch not specified)")]
            public bool Into { get; set; }

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
            string currentBranch = GitService.Run("branch --show-current");
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] Could not determine current branch.");
                return 1;
            }

            string sourceBranch;
            string targetBranch;

            // Determine merge direction based on --into flag
            if (settings.Into)
            {
                // --into mode: merge current branch INTO another branch
                sourceBranch = currentBranch;

                // If branch argument not provided, interactively select target
                if (string.IsNullOrWhiteSpace(settings.Branch))
                {
                    targetBranch = await SelectBranch(currentBranch, $"Which branch do you want to merge [blue]{currentBranch}[/] into?");
                    if (string.IsNullOrWhiteSpace(targetBranch))
                    {
                        return 0; // User cancelled
                    }
                }
                else
                {
                    targetBranch = settings.Branch;
                }
            }
            else
            {
                // Normal mode: merge another branch INTO current branch
                targetBranch = currentBranch;

                // If branch argument not provided, interactively select source
                if (string.IsNullOrWhiteSpace(settings.Branch))
                {
                    sourceBranch = await SelectBranch(currentBranch, "Which branch do you want to [green]merge[/]?");
                    if (string.IsNullOrWhiteSpace(sourceBranch))
                    {
                        return 0; // User cancelled
                    }
                }
                else
                {
                    sourceBranch = settings.Branch;
                }
            }

            // Check if branches exist
            var sourceCheck = GitService.Run($"rev-parse --verify {sourceBranch}");
            if (sourceCheck.Contains("fatal"))
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] Branch [blue]{sourceBranch}[/] does not exist.");
                return 1;
            }

            var targetCheck = GitService.Run($"rev-parse --verify {targetBranch}");
            if (targetCheck.Contains("fatal"))
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] Branch [blue]{targetBranch}[/] does not exist.");
                return 1;
            }

            // Show what will be merged
            AnsiConsole.Write(new Rule($"[blue]Merging {sourceBranch} into {targetBranch}[/]"));
            AnsiConsole.WriteLine();

            // Check for uncommitted changes
            var statusCheck = GitService.Run("status --porcelain");
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
            await ShowMergePreview(targetBranch, sourceBranch);

            // Confirm merge
            if (!AnsiConsole.Confirm($"\nMerge [blue]{sourceBranch}[/] into [blue]{targetBranch}[/]?", true))
            {
                AnsiConsole.MarkupLine("[yellow]Merge cancelled.[/]");
                return 0;
            }

            // If target is not current branch, switch to it first
            if (targetBranch != currentBranch)
            {
                AnsiConsole.Status().Start($"Switching to [blue]{targetBranch}[/]...", ctx =>
                {
                    var switchResult = GitService.Run($"checkout {targetBranch}");
                    if (switchResult.Contains("error:") || switchResult.Contains("fatal:"))
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] Failed to switch: {switchResult}");
                        return;
                    }
                });
            }

            // Attempt the merge
            return await PerformMerge(sourceBranch, targetBranch, settings.UseAi);
        }

        private async Task<string?> SelectBranch(string currentBranch, string? title = null)
        {
            return await Task.Run(() =>
            {
                var branchOutput = GitService.Run("branch -a");
                if (string.IsNullOrWhiteSpace(branchOutput))
                {
                    AnsiConsole.MarkupLine("[yellow]No branches found.[/]");
                    return null;
                }

                var branches = branchOutput
                    .Split('\n')
                    .Select(b => b.Trim().TrimStart('*').Trim())
                    .Where(b => !string.IsNullOrWhiteSpace(b) && !b.Contains("HEAD ->"))
                    .Select(b => b.Replace("remotes/origin/", ""))
                    .Distinct()
                    .Where(b => b != currentBranch)  // Filter current branch AFTER cleaning names
                    .OrderBy(b => b)
                    .ToList();

                if (!branches.Any())
                {
                    AnsiConsole.MarkupLine("[yellow]No other branches available to merge.[/]");
                    return null;
                }

                return AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title(title ?? "Which branch do you want to [green]merge[/]?")
                        .PageSize(10)
                        .AddChoices(branches));
            });
        }

        private async Task ShowMergePreview(string currentBranch, string branchToMerge)
        {
            await Task.Run(() =>
            {
                // Check if it's a fast-forward merge
                var mergeBase = GitService.Run($"merge-base {currentBranch} {branchToMerge}");
                var currentCommit = GitService.Run($"rev-parse {currentBranch}");

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
                var commits = GitService.Run($"log {currentBranch}..{branchToMerge} --oneline --max-count=10");

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
                    var totalCommits = GitService.Run($"rev-list --count {currentBranch}..{branchToMerge}");
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
            bool wasFastForward = false;

            // Use --no-commit if AI is requested so we can generate a custom message
            string mergeFlags = useAi ? "--no-commit --no-ff" : "--no-edit";

            await AnsiConsole.Status().StartAsync("Merging...", async ctx =>
            {
                await Task.Run(() =>
                {
                    mergeResult = GitService.Run($"merge {branchToMerge} {mergeFlags}");
                    hasConflicts = mergeResult.Contains("CONFLICT") || mergeResult.Contains("Automatic merge failed");
                    wasFastForward = mergeResult.Contains("Fast-forward") && !useAi;
                });
            });

            if (hasConflicts)
            {
                return await HandleConflicts(branchToMerge);
            }

            // If fast-forward and not using AI, we're done
            if (wasFastForward)
            {
                AnsiConsole.MarkupLine($"[green]✓ Fast-forward merge complete![/]");
                AnsiConsole.MarkupLine($"[grey]{mergeResult}[/]");
            }
            else if (useAi)
            {
                // Generate AI commit message
                string? aiMessage = await GenerateAiMergeMessage(branchToMerge, currentBranch);

                if (!string.IsNullOrWhiteSpace(aiMessage))
                {
                    // Show AI suggestion
                    AnsiConsole.Write(new Panel(new Text(aiMessage, new Style(Color.Blue)))
                        .Header("AI Merge Message")
                        .BorderColor(Color.Blue));

                    var choice = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .AddChoices(new[] { "✔ Accept", "✎ Edit", "✖ Cancel" }));

                    if (choice == "✖ Cancel")
                    {
                        GitService.Run("merge --abort");
                        AnsiConsole.MarkupLine("[red]Merge cancelled.[/]");
                        return 0;
                    }

                    if (choice == "✎ Edit")
                    {
                        aiMessage = AnsiConsole.Ask<string>("Edit message:", aiMessage);
                    }

                    // Commit with the AI message
                    GitService.Run($"commit -m \"{aiMessage.Replace("\"", "\\\"")}\"");
                    AnsiConsole.MarkupLine($"[green]✓ Merge complete with AI-generated message![/]");
                }
                else
                {
                    // Fallback: commit with default message
                    GitService.Run($"commit --no-edit");
                    AnsiConsole.MarkupLine($"[green]✓ Merge complete![/]");
                    AnsiConsole.MarkupLine("[grey]AI message generation failed, used default message.[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✓ Merge complete![/]");
                var lastCommit = GitService.Run("log -1 --pretty=%B");
                AnsiConsole.MarkupLine($"[grey]Merge commit:[/] {lastCommit}");
            }

            // Remind to sync and clean up
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]Next:[/] Run [blue]buddy sync[/] to push changes and clean up merged branches.");

            return 0;
        }

        private async Task<string?> GenerateAiMergeMessage(string sourceBranch, string targetBranch)
        {
            try
            {
                var (provider, model, apiKey) = ConfigManager.LoadConfig();

                if (string.IsNullOrEmpty(apiKey))
                {
                    AnsiConsole.MarkupLine("[yellow]⚠[/] AI Key missing. Run [yellow]buddy config[/] first.");
                    return null;
                }

                // Get the diff of what's being merged
                string diff = GitService.Run($"diff {targetBranch}...{sourceBranch}");

                if (string.IsNullOrWhiteSpace(diff))
                {
                    return null;
                }

                // Get list of commits being merged
                string commits = GitService.Run($"log {targetBranch}..{sourceBranch} --oneline");

                string context = $"Commits being merged:\n{commits}\n\nChanges:\n{diff}";

                string? aiMessage = null;

                await AnsiConsole.Status().StartAsync("[blue]AI is generating merge message...[/]", async ctx =>
                {
                    aiMessage = await AiService.GenerateCommitMessage(context);
                });

                return aiMessage;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠[/] AI generation failed: {ex.Message}");
                return null;
            }
        }

        private async Task<int> HandleConflicts(string branchToMerge)
        {
            await Task.Run(() =>
            {
                AnsiConsole.Write(new Rule("[red]Merge Conflicts Detected[/]"));
                AnsiConsole.WriteLine();

                // Get list of conflicted files
                var conflictedFiles = GitService.Run("diff --name-only --diff-filter=U");

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
                    GitService.Run("merge --abort");
                    AnsiConsole.MarkupLine("[yellow]Merge aborted. Your branch is back to its previous state.[/]");
                }
                else if (choice.StartsWith("Show"))
                {
                    var conflicts = GitService.Run("diff --diff-filter=U");
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
            var result = GitService.Run("rev-parse --is-inside-work-tree");
            return result.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
