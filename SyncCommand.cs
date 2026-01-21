using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace GitBuddy
{
    public class SyncCommand : AsyncCommand<SyncCommand.Settings>
    {
        public class Settings : CommandSettings { }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            AnsiConsole.Write(new Rule("[blue]Cloud Sync[/]"));

            // 1. Check if a remote is linked
            string remotes = GitHelper.Run("remote");
            
            if (string.IsNullOrWhiteSpace(remotes))
            {
                return await HandleNewRepoFlow();
            }

            // 2. Standard Sync Flow with Error Handling
            bool syncFailed = false;
            string errorDetails = "";

            // Get current branch name
            string currentBranch = GitHelper.Run("branch --show-current");
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                AnsiConsole.MarkupLine("[red]✗[/] Could not determine current branch.");
                return 1;
            }

            await AnsiConsole.Status().StartAsync("Syncing with GitHub...", async ctx =>
            {
                await Task.Run(() => {
                    ctx.Status("Checking connection...");
                    // Test the remote connection by trying to fetch
                    string testResult = GitHelper.Run("ls-remote origin");

                    if (testResult.Contains("not found") || testResult.Contains("fatal"))
                    {
                        syncFailed = true;
                        errorDetails = testResult;
                        return;
                    }

                    ctx.Status($"Pulling latest from {currentBranch}...");
                    // Try to pull, but don't fail if the branch doesn't exist on remote yet
                    string pullOutput = GitHelper.Run($"pull origin {currentBranch} --rebase");

                    // It's okay if pull fails because the branch doesn't exist remotely yet
                    if (!pullOutput.Contains("Couldn't find remote ref"))
                    {
                        // Check for actual pull errors (not "branch doesn't exist")
                        if (pullOutput.Contains("fatal") && !pullOutput.Contains("Couldn't find remote ref"))
                        {
                            syncFailed = true;
                            errorDetails = pullOutput;
                            return;
                        }
                    }

                    ctx.Status($"Pushing {currentBranch}...");
                    string pushOutput = GitHelper.Run($"push -u origin {currentBranch}");

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
                        GitHelper.Run("remote remove origin");
                        return await HandleNewRepoFlow();
                    }
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[green]✔ Sync Complete![/]");
            }

            return 0;
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