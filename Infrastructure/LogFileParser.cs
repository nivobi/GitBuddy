using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Spectre.Console;

namespace GitBuddy.Infrastructure
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
        public string? Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class LogFileParser
    {
        /// <summary>
        /// Parses JSON Lines log files and returns log entries
        /// </summary>
        public List<LogEntry> ParseJsonLines(string filePath)
        {
            var entries = new List<LogEntry>();

            if (!File.Exists(filePath))
                return entries;

            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    var entry = new LogEntry();

                    // Timestamp
                    if (root.TryGetProperty("@t", out var tProp) || root.TryGetProperty("Timestamp", out tProp))
                    {
                         if (DateTime.TryParse(tProp.GetString(), out var dt))
                            entry.Timestamp = dt;
                    }

                    // Level
                    if (root.TryGetProperty("@l", out var lProp) || root.TryGetProperty("Level", out lProp))
                    {
                        entry.Level = lProp.GetString() ?? "Unknown";
                    }

                    // Message
                    if (root.TryGetProperty("@mt", out var mtProp) || root.TryGetProperty("MessageTemplate", out mtProp))
                    {
                        entry.Message = mtProp.GetString() ?? "";
                    }

                    // Exception
                    if (root.TryGetProperty("@x", out var xProp) || root.TryGetProperty("Exception", out xProp))
                    {
                        entry.Exception = xProp.GetString();
                    }

                    // Parse additional properties
                    foreach (var prop in root.EnumerateObject())
                    {
                        var name = prop.Name;
                        if (!name.StartsWith("@") && name != "Timestamp" && name != "Level" && name != "MessageTemplate" && name != "Exception" && name != "Properties" && name != "Renderings")
                        {
                            entry.Properties[name] = prop.Value.ToString();
                        }
                    }

                    // Handle nested Properties object from standard JsonFormatter
                    if (root.TryGetProperty("Properties", out var propsProp) && propsProp.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in propsProp.EnumerateObject())
                        {
                            entry.Properties[prop.Name] = prop.Value.ToString();
                        }
                    }

                    entries.Add(entry);
                }
                catch (JsonException)
                {
                    // Skip malformed lines
                    continue;
                }
            }

            return entries;
        }

        /// <summary>
        /// Formats a log entry for human-readable display
        /// </summary>
        public string FormatLogEntry(LogEntry entry, bool includeProperties = false)
        {
            var levelColor = GetLevelColor(entry.Level);
            var timestamp = entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            var level = entry.Level.ToUpper().PadRight(7);

            var formatted = $"[[{timestamp}]] [{levelColor}]{level}[/] {entry.Message.EscapeMarkup()}";

            if (!string.IsNullOrEmpty(entry.Exception))
            {
                formatted += $"\n  [red]Exception:[/] {entry.Exception.Split('\n').First()}";
            }

            if (includeProperties && entry.Properties.Any())
            {
                formatted += "\n  [grey]Properties:[/]";
                foreach (var prop in entry.Properties.Take(3))
                {
                    formatted += $"\n    {prop.Key}: {prop.Value}";
                }
            }

            return formatted;
        }

        /// <summary>
        /// Filters entries by minimum log level
        /// </summary>
        public IEnumerable<LogEntry> FilterByLevel(List<LogEntry> entries, string? levelFilter)
        {
            if (string.IsNullOrEmpty(levelFilter))
                return entries;

            var levels = new[] { "Debug", "Information", "Warning", "Error" };
            var minLevel = levelFilter switch
            {
                "debug" => 0,
                "info" or "information" => 1,
                "warning" or "warn" => 2,
                "error" => 3,
                _ => 0
            };

            return entries.Where(e =>
            {
                var entryLevel = Array.IndexOf(levels, e.Level);
                return entryLevel >= minLevel;
            });
        }

        private static string GetLevelColor(string level) => level.ToLower() switch
        {
            "debug" => "grey",
            "information" => "blue",
            "warning" => "yellow",
            "error" => "red",
            _ => "white"
        };
    }
}
