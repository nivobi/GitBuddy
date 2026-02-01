using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Git
{
    public class StashCommand : AsyncCommand<StashCommand.Settings>
    {
        private readonly IGitService _gitService;
        private readonly ILogger<StashCommand> _logger;

        public StashCommand(IGitService gitService, ILogger<StashCommand> logger)
        {
            _gitService = gitService;
            _logger = logger;
        }

        public class Settings : CommandSettings
        {
            [CommandArgument(0, "[action]")]
            [Description("Action: push, pop, apply, list")]
            public string? Action { get; set; }

            [CommandArgument(1, "[index]")]
            [Description("Stash index (e.g., 0 for stash@{0})")]
            public int? Index { get; set; }

            [CommandOption("-m|--message <MESSAGE>")]
            [Description("Message for new stash (push action)")]
            public string? Message { get; set; }

            [CommandOption("-u|--include-untracked")]
            [Description("Include untracked files (push action)")]
            public bool IncludeUntracked { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            using var execLog = new CommandExecutionLogger<StashCommand>(_logger, "stash", settings);

            // Check if we're in a git repository
            if (!await _gitService.IsGitRepositoryAsync(cancellationToken))
            {
                AnsiConsole.MarkupLine("[red]✗ Error:[/] Not in a git repository.");
                AnsiConsole.MarkupLine("[grey]Try running this command from inside a git repository.[/]");
                return 1;
            }

            var action = settings.Action?.ToLower() ?? "list";

            var result = await (action switch
            {
                "push" => PushStash(settings, cancellationToken),
                "pop" => PopStash(settings.Index, cancellationToken),
                "apply" => ApplyStash(settings.Index, cancellationToken),
                "list" => ListStashes(cancellationToken),
                _ => ListStashes(cancellationToken) // Default to list for unknown actions
            });

            execLog.Complete(result);
            return result;
        }

        private async Task<int> PushStash(Settings settings, CancellationToken cancellationToken)
        {
            // Check if there are changes to stash
            if (!await _gitService.HasUncommittedChangesAsync(cancellationToken))
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] No changes to stash.");
                return 0;
            }

            // Get or prompt for message
            string? message = settings.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                message = AnsiConsole.Ask<string>("Enter stash [yellow]message[/]:");
            }

            // Build the stash command
            var stashArgs = settings.IncludeUntracked
                ? $"stash push -u -m \"{message}\""
                : $"stash push -m \"{message}\"";

            // Execute stash
            ProcessResult result = null!;
            await AnsiConsole.Status().StartAsync("Stashing changes...", async ctx =>
            {
                result = await _gitService.RunAsync(stashArgs, cancellationToken);
            });

            if (result.Output.Contains("Saved working directory") || result.Output.Contains("No local changes to save"))
            {
                if (result.Output.Contains("No local changes"))
                {
                    AnsiConsole.MarkupLine("[yellow]ℹ[/] No local changes to save.");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] Stashed changes: [blue]{message}[/]");
                    if (settings.IncludeUntracked)
                    {
                        AnsiConsole.MarkupLine("[grey]Included untracked files[/]");
                    }
                }
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Failed to stash: {result.Output}");
                return 1;
            }
        }

        private async Task<int> ListStashes(CancellationToken cancellationToken)
        {
            var result = await _gitService.RunAsync("stash list", cancellationToken);
            var stashOutput = result.Output;

            if (string.IsNullOrWhiteSpace(stashOutput))
            {
                AnsiConsole.MarkupLine("[yellow]No stashes found.[/]");
                AnsiConsole.MarkupLine("[grey]Use[/] [blue]buddy stash push[/] [grey]to create a stash.[/]");
                return 0;
            }

            var stashes = ParseStashList(stashOutput);

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn(new TableColumn("[bold]Index[/]").Centered());
            table.AddColumn(new TableColumn("[bold]Branch[/]"));
            table.AddColumn(new TableColumn("[bold]Message[/]"));

            foreach (var stash in stashes)
            {
                table.AddRow(
                    $"[blue]{stash.Index}[/]",
                    $"[grey]{stash.Branch}[/]",
                    stash.Message
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]Total:[/] {stashes.Count} stash(es)");

            return 0;
        }

        private static List<StashEntry> ParseStashList(string stashOutput)
        {
            var stashes = new List<StashEntry>();

            foreach (var line in stashOutput.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Format: stash@{0}: WIP on branch-name: abc1234 commit message
                // Or: stash@{0}: On branch-name: message

                var match = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"stash@\{(\d+)\}:\s+(?:WIP on|On)\s+([^:]+):\s*(.*)");

                if (match.Success)
                {
                    var index = int.Parse(match.Groups[1].Value);
                    var branch = match.Groups[2].Value.Trim();
                    var message = match.Groups[3].Value.Trim();

                    // Remove commit hash if present (format: "abc1234 message")
                    var messageParts = message.Split(new[] { ' ' }, 2);
                    if (messageParts.Length > 1 && messageParts[0].Length == 7)
                    {
                        message = messageParts[1];
                    }

                    stashes.Add(new StashEntry
                    {
                        Index = index,
                        Reference = $"stash@{{{index}}}",
                        Branch = branch,
                        Message = message
                    });
                }
            }

            return stashes;
        }

        private async Task<int> PopStash(int? index, CancellationToken cancellationToken)
        {
            // Check if there are any stashes
            var listResult = await _gitService.RunAsync("stash list", cancellationToken);
            if (string.IsNullOrWhiteSpace(listResult.Output))
            {
                AnsiConsole.MarkupLine("[yellow]No stashes found.[/]");
                return 0;
            }

            // Get index interactively if not provided
            if (!index.HasValue)
            {
                index = await SelectStashInteractively(cancellationToken);
                if (!index.HasValue)
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                    return 0;
                }
            }

            var stashRef = $"stash@{{{index.Value}}}";

            // Verify stash exists
            var verifyResult = await _gitService.RunAsync($"stash list {stashRef}", cancellationToken);
            if (string.IsNullOrWhiteSpace(verifyResult.Output))
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] Stash [blue]{stashRef}[/] does not exist.");
                return 1;
            }

            // Check for uncommitted changes
            if (await _gitService.HasUncommittedChangesAsync(cancellationToken))
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Warning:[/] You have uncommitted changes.");
                if (!AnsiConsole.Confirm("Apply stash anyway? (may cause conflicts)", false))
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                    return 0;
                }
            }

            // Confirm pop operation
            if (!AnsiConsole.Confirm($"Pop [blue]{stashRef}[/]? (applies and removes from stash list)", true))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                return 0;
            }

            // Perform pop
            ProcessResult result = null!;
            await AnsiConsole.Status().StartAsync($"Popping [blue]{stashRef}[/]...", async ctx =>
            {
                result = await _gitService.RunAsync($"stash pop {stashRef}", cancellationToken);
            });

            if (result.Output.Contains("CONFLICT") || result.Output.Contains("error:") || result.Output.Contains("fatal:"))
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Failed to pop stash:");
                AnsiConsole.MarkupLine($"[grey]{result.Output}[/]");

                if (result.Output.Contains("CONFLICT"))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[yellow]⚠[/] Conflicts detected. Resolve conflicts and:");
                    AnsiConsole.MarkupLine($"  1. Run [blue]buddy save[/] to commit resolved changes");
                    AnsiConsole.MarkupLine($"  2. Run [blue]git stash drop {stashRef}[/] to remove stash");
                }
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Popped [blue]{stashRef}[/] successfully!");
            return 0;
        }

        private async Task<int> ApplyStash(int? index, CancellationToken cancellationToken)
        {
            // Check if there are any stashes
            var listResult = await _gitService.RunAsync("stash list", cancellationToken);
            if (string.IsNullOrWhiteSpace(listResult.Output))
            {
                AnsiConsole.MarkupLine("[yellow]No stashes found.[/]");
                return 0;
            }

            // Get index interactively if not provided
            if (!index.HasValue)
            {
                index = await SelectStashInteractively(cancellationToken);
                if (!index.HasValue)
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                    return 0;
                }
            }

            var stashRef = $"stash@{{{index.Value}}}";

            // Verify stash exists
            var verifyResult = await _gitService.RunAsync($"stash list {stashRef}", cancellationToken);
            if (string.IsNullOrWhiteSpace(verifyResult.Output))
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] Stash [blue]{stashRef}[/] does not exist.");
                return 1;
            }

            // Check for uncommitted changes (warning only)
            if (await _gitService.HasUncommittedChangesAsync(cancellationToken))
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Warning:[/] You have uncommitted changes.");
                if (!AnsiConsole.Confirm("Apply stash anyway? (may cause conflicts)", false))
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                    return 0;
                }
            }

            // Perform apply
            ProcessResult result = null!;
            await AnsiConsole.Status().StartAsync($"Applying [blue]{stashRef}[/]...", async ctx =>
            {
                result = await _gitService.RunAsync($"stash apply {stashRef}", cancellationToken);
            });

            if (result.Output.Contains("CONFLICT") || result.Output.Contains("error:") || result.Output.Contains("fatal:"))
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Failed to apply stash:");
                AnsiConsole.MarkupLine($"[grey]{result.Output}[/]");

                if (result.Output.Contains("CONFLICT"))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[yellow]⚠[/] Conflicts detected. Resolve conflicts and run [blue]buddy save[/]");
                }
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Applied [blue]{stashRef}[/] successfully!");
            AnsiConsole.MarkupLine($"[grey]Stash kept in list. Use[/] [blue]git stash drop {index}[/] [grey]to remove it.[/]");
            return 0;
        }

        private async Task<int?> SelectStashInteractively(CancellationToken cancellationToken)
        {
            var result = await _gitService.RunAsync("stash list", cancellationToken);
            var stashOutput = result.Output;

            if (string.IsNullOrWhiteSpace(stashOutput))
            {
                return (int?)null;
            }

            var stashes = ParseStashList(stashOutput);
            if (!stashes.Any())
            {
                return (int?)null;
            }

            // Create selection choices with formatted display
            var choices = stashes.Select(s =>
                $"[{s.Index}] {s.Branch}: {s.Message}").ToList();

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Which [green]stash[/] do you want to use?")
                    .PageSize(10)
                    .AddChoices(choices));

            // Extract index from selection
            var indexStr = selected.Substring(1, selected.IndexOf(']') - 1);
            return int.Parse(indexStr);
        }
    }

    internal class StashEntry
    {
        public int Index { get; set; }
        public string Reference { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
