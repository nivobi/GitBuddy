using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Utility
{
    public class LogsCommand : AsyncCommand<LogsCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandOption("--tail <COUNT>")]
            [Description("Number of log entries to show (default: 20)")]
            public int Tail { get; set; } = 20;

            [CommandOption("--level <LEVEL>")]
            [Description("Filter by log level: error, warning, info, debug")]
            public string? Level { get; set; }

            [CommandOption("--path")]
            [Description("Show log directory path")]
            public bool ShowPath { get; set; }

            [CommandOption("--clear")]
            [Description("Delete all logs")]
            public bool Clear { get; set; }

            [CommandOption("--follow")]
            [Description("Follow logs in real-time (tail -f)")]
            public bool Follow { get; set; }

            [CommandOption("--export <PATH>")]
            [Description("Export logs to a file")]
            public string? ExportPath { get; set; }
        }

        public override async Task<int> ExecuteAsync(
            CommandContext context,
            Settings settings,
            CancellationToken cancellationToken)
        {
            var logDir = LoggingHelper.GetLogDirectory();

            // Handle --path
            if (settings.ShowPath)
            {
                AnsiConsole.MarkupLine($"[blue]📂 Logs location:[/] {logDir}");
                return 0;
            }

            // Handle --clear
            if (settings.Clear)
            {
                return ClearLogs(logDir);
            }

            // Handle --export
            if (!string.IsNullOrEmpty(settings.ExportPath))
            {
                return ExportLogs(logDir, settings.ExportPath);
            }

            // Handle --follow
            if (settings.Follow)
            {
                return await FollowLogs(logDir, cancellationToken);
            }

            // Default: show recent logs
            return ShowRecentLogs(logDir, settings.Tail, settings.Level);
        }

        private int ShowRecentLogs(string logDir, int count, string? levelFilter)
        {
            var logFile = GetLatestLogFile(logDir);
            if (logFile == null)
            {
                AnsiConsole.MarkupLine("[yellow]No logs found.[/]");
                AnsiConsole.MarkupLine($"[grey]Logs will be created in: {logDir}[/]");
                return 0;
            }

            var parser = new LogFileParser();
            var entries = parser.ParseJsonLines(logFile);
            var filtered = parser.FilterByLevel(entries, levelFilter).ToList();
            var recent = filtered.TakeLast(count).ToList();

            if (!recent.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No logs match the filter.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[blue]📋 Recent GitBuddy Logs[/] (last {recent.Count} entries):");
            AnsiConsole.WriteLine();

            foreach (var entry in recent)
            {
                AnsiConsole.MarkupLine(parser.FormatLogEntry(entry));
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]💡 Tip: Use 'buddy logs --tail 50' to see more entries[/]");
            return 0;
        }

        private int ClearLogs(string logDir)
        {
            if (!Directory.Exists(logDir))
            {
                AnsiConsole.MarkupLine("[yellow]No logs to clear.[/]");
                return 0;
            }

            var logFiles = Directory.GetFiles(logDir, "*.log");
            if (logFiles.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No logs to clear.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[yellow]⚠️  This will delete {logFiles.Length} log file(s) in {logDir}[/]");
            if (!AnsiConsole.Confirm("Are you sure?", false))
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return 0;
            }

            try
            {
                foreach (var file in logFiles)
                {
                    File.Delete(file);
                }
                AnsiConsole.MarkupLine("[green]✓ All logs deleted[/]");
                return 0;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error deleting logs:[/] {ex.Message}");
                return 1;
            }
        }

        private int ExportLogs(string logDir, string exportPath)
        {
            var logFile = GetLatestLogFile(logDir);
            if (logFile == null)
            {
                AnsiConsole.MarkupLine("[yellow]No logs to export.[/]");
                return 1;
            }

            try
            {
                // Expand ~ to home directory
                if (exportPath.StartsWith("~"))
                {
                    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    exportPath = exportPath.Replace("~", homeDir);
                }

                File.Copy(logFile, exportPath, overwrite: true);
                AnsiConsole.MarkupLine($"[green]✓ Logs exported to:[/] {exportPath}");
                AnsiConsole.MarkupLine("[grey]💡 Attach this file when reporting issues[/]");
                return 0;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Error exporting logs:[/] {ex.Message}");
                return 1;
            }
        }

        private async Task<int> FollowLogs(string logDir, CancellationToken cancellationToken)
        {
            var logFile = GetLatestLogFile(logDir);
            if (logFile == null)
            {
                AnsiConsole.MarkupLine("[yellow]No logs to follow.[/]");
                AnsiConsole.MarkupLine("[grey]Waiting for logs to be created...[/]");
            }

            AnsiConsole.MarkupLine("[blue]📋 Following logs[/] (press Ctrl+C to stop):");
            AnsiConsole.WriteLine();

            var parser = new LogFileParser();
            var lastPosition = 0L;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (File.Exists(logFile))
                    {
                        using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        stream.Seek(lastPosition, SeekOrigin.Begin);

                        using var reader = new StreamReader(stream);
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(line);
                                var root = doc.RootElement;

                                var entry = new LogEntry
                                {
                                    Timestamp = root.TryGetProperty("@t", out var tProp)
                                        ? DateTime.Parse(tProp.GetString()!)
                                        : DateTime.MinValue,
                                    Level = root.TryGetProperty("@l", out var lProp)
                                        ? lProp.GetString()!
                                        : "Unknown",
                                    Message = root.TryGetProperty("@mt", out var mtProp)
                                        ? mtProp.GetString()!
                                        : "",
                                    Exception = root.TryGetProperty("@x", out var xProp)
                                        ? xProp.GetString()
                                        : null
                                };

                                // Parse additional properties
                                foreach (var prop in root.EnumerateObject())
                                {
                                    if (!prop.Name.StartsWith("@"))
                                    {
                                        entry.Properties[prop.Name] = prop.Value.ToString();
                                    }
                                }

                                AnsiConsole.MarkupLine(parser.FormatLogEntry(entry));
                            }
                            catch
                            {
                                // Skip malformed lines
                            }
                        }

                        lastPosition = stream.Position;
                    }

                    await Task.Delay(500, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[grey]Stopped following logs.[/]");
            }

            return 0;
        }

        private static string? GetLatestLogFile(string logDir)
        {
            if (!Directory.Exists(logDir)) return null;
            
            return Directory.GetFiles(logDir, "gitbuddy*.log")
                .OrderByDescending(f => f) // Lexicographical sort works for YYYYMMDD
                .FirstOrDefault();
        }
    }
}
