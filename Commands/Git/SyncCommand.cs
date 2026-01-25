using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Git
{
    public class SyncCommand : AsyncCommand<SyncCommand.Settings>
    {
        private readonly IGitService _gitService;

        public SyncCommand(IGitService gitService)
        {
            _gitService = gitService;
        }

        public class Settings : CommandSettings { }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            AnsiConsole.Write(new Rule("[blue]Cloud Sync[/]"));

            // 1. Check if a remote is linked
            string remotes = _gitService.Run("remote");
            
            if (string.IsNullOrWhiteSpace(remotes))
            {
                return await HandleNewRepoFlow();
            }

            // 2. Standard Sync Flow with Error Handling
            bool syncFailed = false;
            string errorDetails = "";

            // Get current branch name
            string currentBranch = _gitService.Run("branch --show-current");
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                AnsiConsole.MarkupLine("[red]✗[/] Could not determine current branch.");
                return 1;
            }

            // Get remote URL for display
            string remoteUrl = _gitService.Run("remote get-url origin");
            string repoDisplay = remoteUrl.Replace("https://github.com/", "").Replace(".git", "").Trim();
            if (repoDisplay.Contains("git@github.com:"))
            {
                repoDisplay = repoDisplay.Replace("git@github.com:", "");
            }

            AnsiConsole.MarkupLine($"[grey]Branch:[/] [blue]{currentBranch}[/]");
            AnsiConsole.MarkupLine($"[grey]Remote:[/] [blue]{repoDisplay}[/]");
            AnsiConsole.WriteLine();

            await AnsiConsole.Status().StartAsync("Syncing with GitHub...", async ctx =>
            {
                await Task.Run(() => {
                    ctx.Status($"Checking connection to {repoDisplay}...");
                    // Test the remote connection by trying to fetch
                    string testResult = _gitService.Run("ls-remote origin");

                    if (testResult.Contains("not found") || testResult.Contains("fatal"))
                    {
                        syncFailed = true;
                        errorDetails = testResult;
                        return;
                    }

                    ctx.Status($"Pulling latest changes from origin/{currentBranch}...");
                    // Try to pull, but don't fail if the branch doesn't exist on remote yet
                    string pullOutput = _gitService.Run($"pull origin {currentBranch} --rebase");

                    // It's okay if pull fails because the branch doesn't exist remotely yet
                    bool isNewBranch = pullOutput.Contains("couldn't find remote ref", StringComparison.OrdinalIgnoreCase);

                    if (pullOutput.Contains("fatal") && !isNewBranch)
                    {
                        // This is a real error, not just "branch doesn't exist yet"
                        syncFailed = true;
                        errorDetails = pullOutput;
                        return;
                    }

                    if (isNewBranch)
                    {
                        ctx.Status($"Creating new branch {currentBranch} on GitHub...");
                    }
                    else
                    {
                        ctx.Status($"Pushing changes to origin/{currentBranch}...");
                    }

                    string pushOutput = _gitService.Run($"push -u origin {currentBranch}");

                    if (pushOutput.Contains("error") || pushOutput.Contains("fatal"))
                    {
                        syncFailed = true;
                        errorDetails = pushOutput;
                    }
                });
            });

            if (syncFailed)
            {
                AnsiConsole.Write(new Panel(errorDetails).Header("Sync Failed").BorderColor(Color.Red));

                if (errorDetails.Contains("not found"))
                {
                    if (AnsiConsole.Confirm("[yellow]The remote repository seems to be missing. Remove the dead link and re-create it?[/]"))
                    {
                        _gitService.Run("remote remove origin");
                        return await HandleNewRepoFlow();
                    }
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✔ Sync Complete![/]");
                AnsiConsole.MarkupLine($"[grey]→[/] [blue]{currentBranch}[/] is now synced with [blue]{repoDisplay}[/]");

                // Show GitHub URL
                string repoUrl = $"https://github.com/{repoDisplay}";
                AnsiConsole.MarkupLine($"[grey]→[/] View on GitHub: [link]{repoUrl}[/]");

                // Check for merged branches to clean up
                await CheckForMergedBranches(currentBranch);
            }

            return 0;
        }

        private async Task CheckForMergedBranches(string currentBranch)
        {
            await Task.Run(() =>
            {
                // Get merged branches (excluding current and main/master)
                var mergedOutput = _gitService.Run("branch --merged");
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
                    return; // No cleanup needed
                }

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[yellow]ℹ[/] Found {mergedBranches.Count} merged branch(es):");

                foreach (var branch in mergedBranches)
                {
                    AnsiConsole.MarkupLine($"  [grey]•[/] [blue]{branch}[/]");
                }

                AnsiConsole.WriteLine();

                foreach (var branch in mergedBranches)
                {
                    if (AnsiConsole.Confirm($"Delete [blue]{branch}[/] (local + remote)?", true))
                    {
                        // Delete local branch - use -D since we already verified it's merged to HEAD
                        // The -d flag checks remote tracking which may not be updated yet
                        var localResult = _gitService.Run($"branch -D {branch}");
                        if (localResult.Contains("Deleted branch"))
                        {
                            AnsiConsole.MarkupLine($"  [green]✓[/] Deleted local branch [grey]{branch}[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"  [yellow]⚠[/] Could not delete local: {localResult}");
                        }

                        // Delete remote branch
                        var remoteResult = _gitService.Run($"push origin --delete {branch}");
                        if (remoteResult.Contains("deleted") || remoteResult.Contains("remote ref does not exist"))
                        {
                            AnsiConsole.MarkupLine($"  [green]✓[/] Deleted remote branch [grey]origin/{branch}[/]");
                        }
                        else if (remoteResult.Contains("error") || remoteResult.Contains("fatal"))
                        {
                            // Remote branch might not exist, that's okay
                            if (!remoteResult.Contains("remote ref does not exist"))
                            {
                                AnsiConsole.MarkupLine($"  [grey]Note: Remote branch may not exist[/]");
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"  [grey]Note: {remoteResult}[/]");
                        }
                    }
                }
            });
        }

        private async Task<int> HandleNewRepoFlow()
        {
            AnsiConsole.MarkupLine("[yellow]! No valid GitHub repository linked.[/]");
            
            if (!AnsiConsole.Confirm("Would you like me to create a GitHub repository for you?"))
            {
                return 0;
            }

            string folderName = Path.GetFileName(Directory.GetCurrentDirectory()) ?? "my-new-project";
            string repoName = AnsiConsole.Ask<string>($"[white]Repo Name:[/]", folderName);

            var visibility = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select visibility:")
                    .AddChoices(new[] { "Public", "Private" }));

            string description = AnsiConsole.Ask<string>("[grey]Description (optional):[/]", "");

            string repoUrl = "";

            await AnsiConsole.Status().StartAsync("Creating repository on GitHub...", async ctx => 
            {
                await Task.Run(() => {
                    string visibilityFlag = visibility == "Private" ? "--private" : "--public";
                    string descriptionFlag = !string.IsNullOrEmpty(description) ? $"--description \"{description}\"" : "";
                    
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "gh",
                        Arguments = $"repo create {repoName} {visibilityFlag} {descriptionFlag} --source=. --remote=origin --push",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    process?.WaitForExit();
                    
                    repoUrl = GetRepoUrl();
                });
            });

            if (!string.IsNullOrEmpty(repoUrl))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Panel(new Rows(
                    new Text("✔ Repository Created Successfully", new Style(Color.Green)),
                    new Text($"URL: {repoUrl}", new Style(Color.Blue, decoration: Decoration.Underline))
                )).Header("GitHub Summary").BorderColor(Color.Green).Padding(1, 1, 1, 1));

                if (AnsiConsole.Confirm("Open in browser?"))
                {
                    Process.Start("open", repoUrl);
                }
            }

            return 0;
        }

        private string GetRepoUrl()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "gh",
                    Arguments = "repo view --json url",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return "";
                using var reader = process.StandardOutput;
                string result = reader.ReadToEnd();
                using var doc = JsonDocument.Parse(result);
                return doc.RootElement.GetProperty("url").GetString() ?? "";
            }
            catch { return ""; }
        }
    }
}