using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Git
{
    public class BranchCommand : AsyncCommand<BranchCommand.Settings>
    {
        private readonly IGitService _gitService;

        public BranchCommand(IGitService gitService)
        {
            _gitService = gitService;
        }

        public class Settings : CommandSettings
        {
            [CommandArgument(0, "[action]")]
            [Description("Action: create, switch, list, clean, delete, or rename")]
            public string? Action { get; set; }

            [CommandArgument(1, "[name]")]
            [Description("Branch name (for create/delete) or old name (for rename)")]
            public string? Name { get; set; }

            [CommandArgument(2, "[newname]")]
            [Description("New branch name (for rename action)")]
            public string? NewName { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            // Check if we're in a git repository
            if (!await _gitService.IsGitRepositoryAsync(cancellationToken))
            {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] Not in a git repository.");
                AnsiConsole.MarkupLine("[grey]Try running this command from inside a git repository.[/]");
                return 1;
            }

            var action = settings.Action?.ToLower() ?? "list";

            return action switch
            {
                "create" => await CreateBranch(settings.Name, cancellationToken),
                "switch" => await SwitchBranch(cancellationToken),
                "list" => await ListBranches(cancellationToken),
                "clean" => await CleanBranches(cancellationToken),
                "delete" => await DeleteBranch(settings.Name, cancellationToken),
                "rename" => await RenameBranch(settings.Name, settings.NewName, cancellationToken),
                _ => await ShowHelp(cancellationToken)
            };
        }

        private async Task<int> CreateBranch(string? name, CancellationToken cancellationToken)
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

            ProcessResult result = null!;
            await AnsiConsole.Status().StartAsync($"Creating branch [blue]{fullBranchName}[/]...", async ctx =>
            {
                result = await _gitService.RunAsync($"checkout -b {fullBranchName}", cancellationToken);
            });

            if (result.ExitCode == 0)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Created and switched to branch [blue]{fullBranchName}[/]");
            }
            else
            {
                var errorMsg = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                if (string.IsNullOrWhiteSpace(errorMsg))
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Failed to create branch (exit code {result.ExitCode})");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Failed to create branch: {errorMsg}");
                }
            }

            return 0;
        }

        private async Task<int> SwitchBranch(CancellationToken cancellationToken)
        {
            // Get all branches
            var allBranches = await _gitService.GetAllBranchesAsync(cancellationToken);
            if (!allBranches.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No branches found.[/]");
                return 0;
            }

            // Parse branches
            var branches = allBranches
                .Where(b => !b.Contains("HEAD ->"))
                .Select(b => b.Replace("remotes/origin/", ""))
                .Distinct()
                .OrderBy(b => b)
                .ToList();

            if (!branches.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No branches available.[/]");
                return 0;
            }

            // Show selection menu
            var selectedBranch = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Switch to which [green]branch[/]?")
                    .PageSize(10)
                    .AddChoices(branches));

            // Check for uncommitted changes first
            if (await _gitService.HasUncommittedChangesAsync(cancellationToken))
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] You have uncommitted changes.");
                AnsiConsole.MarkupLine("[grey]Please commit or stash your changes before switching branches.[/]");
                AnsiConsole.MarkupLine($"\nTry: [blue]buddy save[/] to commit your changes");
                return 1;
            }

            // Switch to selected branch
            ProcessResult result = null!;
            await AnsiConsole.Status().StartAsync($"Switching to [blue]{selectedBranch}[/]...", async ctx =>
            {
                result = await _gitService.RunAsync($"checkout {selectedBranch}", cancellationToken);
            });

            // Check for actual errors rather than specific success messages
            if (result.Output.Contains("error:") || result.Output.Contains("fatal:"))
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Failed to switch: {result.Output}");
                return 1;
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Switched to branch [blue]{selectedBranch}[/]");
            }

            return 0;
        }

        private async Task<int> ListBranches(CancellationToken cancellationToken)
        {
            var currentBranch = await _gitService.RunAsync("branch --show-current", cancellationToken);
            var branchOutput = await _gitService.RunAsync("branch -vv", cancellationToken);

            if (string.IsNullOrWhiteSpace(branchOutput.Output))
            {
                AnsiConsole.MarkupLine("[yellow]No branches found.[/]");
                return 0;
            }

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn(new TableColumn("[bold]Branch[/]").Centered());
            table.AddColumn(new TableColumn("[bold]Commit[/]"));
            table.AddColumn(new TableColumn("[bold]Message[/]"));

            foreach (var line in branchOutput.Output.Split('\n'))
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

            return 0;
        }

        private async Task<int> CleanBranches(CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[blue]Checking for merged branches...[/]");

            // Get current branch
            var currentBranch = await _gitService.RunAsync("branch --show-current", cancellationToken);

            // Get merged branches (excluding current and main/master)
            var mergedOutput = await _gitService.RunAsync("branch --merged", cancellationToken);
            var mergedBranches = mergedOutput.Output
                .Split('\n')
                .Select(b => b.Trim().TrimStart('*').Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b) &&
                            b != currentBranch.Output.Trim() &&
                            b != "main" &&
                            b != "master")
                .ToList();

            if (!mergedBranches.Any())
            {
                AnsiConsole.MarkupLine("[green]✓[/] No merged branches to clean up!");
                return 0;
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
                return 0;
            }

            // Delete branches
            var deletedCount = 0;
            foreach (var branch in mergedBranches)
            {
                var result = await _gitService.RunAsync($"branch -d {branch}", cancellationToken);
                if (result.Output.Contains("Deleted branch"))
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] Deleted [grey]{branch}[/]");
                    deletedCount++;
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Failed to delete {branch}: {result.Output}");
                }
            }

            AnsiConsole.MarkupLine($"\n[green]✓[/] Cleaned up {deletedCount} branch(es)!");

            return 0;
        }

        private async Task<int> DeleteBranch(string? name, CancellationToken cancellationToken)
        {
            var currentBranch = await _gitService.RunAsync("branch --show-current", cancellationToken);
            var currentBranchName = currentBranch.Output.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                // Interactive selection - include ALL branches including current
                var branchOutput = await _gitService.RunAsync("branch", cancellationToken);

                if (string.IsNullOrWhiteSpace(branchOutput.Output))
                {
                    AnsiConsole.MarkupLine("[yellow]No branches found.[/]");
                    return 0;
                }

                var branches = branchOutput.Output
                    .Split('\n')
                    .Select(b => b.Trim().TrimStart('*').Trim())
                    .Where(b => !string.IsNullOrWhiteSpace(b))
                    .ToList();

                if (!branches.Any())
                {
                    AnsiConsole.MarkupLine("[yellow]No branches to delete.[/]");
                    return 0;
                }

                name = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Which branch do you want to [red]delete[/]?")
                        .AddChoices(branches));
            }

            // Check if deleting current branch
            bool isDeletingCurrent = name == currentBranchName;

            if (isDeletingCurrent)
            {
                AnsiConsole.MarkupLine($"[yellow]ℹ[/] You're currently on [blue]{name}[/]");

                // Find a branch to switch to (prefer master/main)
                var allBranchesResult = await _gitService.RunAsync("branch", cancellationToken);
                var allBranches = allBranchesResult.Output
                    .Split('\n')
                    .Select(b => b.Trim().TrimStart('*').Trim())
                    .Where(b => !string.IsNullOrWhiteSpace(b) && b != currentBranchName)
                    .ToList();

                if (!allBranches.Any())
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Cannot delete the only branch.");
                    return 1;
                }

                string targetBranch = allBranches.Contains("master") ? "master" :
                                     allBranches.Contains("main") ? "main" :
                                     allBranches.First();

                if (!AnsiConsole.Confirm($"Switch to [blue]{targetBranch}[/] first?", true))
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                    return 0;
                }

                // Switch to target branch
                var switchResult = await _gitService.RunAsync($"checkout {targetBranch}", cancellationToken);
                if (switchResult.Output.Contains("error:") || switchResult.Output.Contains("fatal:"))
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Failed to switch: {switchResult.Output}");
                    return 1;
                }

                AnsiConsole.MarkupLine($"[green]✓[/] Switched to [blue]{targetBranch}[/]");
            }

            // Confirm deletion
            if (!AnsiConsole.Confirm($"Delete branch [red]{name}[/]?", false))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                return 0;
            }

            // Try safe delete first (-d), which prevents deleting unmerged branches
            var result = await _gitService.RunAsync($"branch -d {name}", cancellationToken);

            if (result.Output.Contains("Deleted branch"))
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Deleted branch [grey]{name}[/]");
            }
            else if (result.Output.Contains("not fully merged"))
            {
                AnsiConsole.MarkupLine($"[yellow]⚠[/] Branch [blue]{name}[/] is not fully merged.");

                if (AnsiConsole.Confirm("Force delete anyway? [red](You may lose commits!)[/]", false))
                {
                    result = await _gitService.RunAsync($"branch -D {name}", cancellationToken);
                    if (result.Output.Contains("Deleted branch"))
                    {
                        AnsiConsole.MarkupLine($"[green]✓[/] Force deleted branch [grey]{name}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] Failed to delete: {result.Output}");
                    }
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Failed to delete: {result.Output}");
            }

            return 0;
        }

        private async Task<int> RenameBranch(string? oldName, string? newName, CancellationToken cancellationToken)
        {
            var currentBranch = await _gitService.RunAsync("branch --show-current", cancellationToken);
            var currentBranchName = currentBranch.Output.Trim();

            // If no old name specified, assume renaming current branch
            if (string.IsNullOrWhiteSpace(oldName))
            {
                oldName = currentBranchName;
            }

            // If no new name specified, ask for it
            if (string.IsNullOrWhiteSpace(newName))
            {
                newName = AnsiConsole.Ask<string>($"Rename [blue]{oldName}[/] to:");
            }

            // Check if we're renaming the current branch
            bool isCurrentBranch = oldName == currentBranchName;

            string moveFlag = isCurrentBranch ? "-m" : "-m";
            var result = await _gitService.RunAsync($"branch {moveFlag} {oldName} {newName}", cancellationToken);

            if (result.Output.Contains("Renamed") || result.Output.Contains("renamed") || string.IsNullOrWhiteSpace(result.Output))
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Renamed [grey]{oldName}[/] to [blue]{newName}[/]");

                if (isCurrentBranch)
                {
                    AnsiConsole.MarkupLine($"[grey]You are now on branch[/] [blue]{newName}[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Failed to rename: {result.Output}");
            }

            return 0;
        }

        private Task<int> ShowHelp(CancellationToken cancellationToken)
        {
            var panel = new Panel(
                "[bold]buddy branch[/] - Smart branch management\n\n" +
                "[yellow]Actions:[/]\n" +
                "  [blue]create[/] [[name]]     - Create a new branch with naming conventions\n" +
                "  [blue]switch[/]            - Interactive branch switcher\n" +
                "  [blue]list[/]              - Show all branches with commit info\n" +
                "  [blue]delete[/] [[name]]     - Delete a branch (interactive if no name)\n" +
                "  [blue]rename[/] [[old]] [[new]] - Rename a branch (uses current if no old name)\n" +
                "  [blue]clean[/]             - Remove merged branches\n\n" +
                "[grey]Examples:[/]\n" +
                "  buddy branch list\n" +
                "  buddy branch create dark-mode\n" +
                "  buddy branch switch\n" +
                "  buddy branch delete old-feature\n" +
                "  buddy branch 