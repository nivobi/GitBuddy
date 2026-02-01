using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GitBuddy.Infrastructure
{
    /// <summary>
    /// Helper methods for privacy-first logging.
    /// Ensures no secrets or sensitive data are logged.
    /// </summary>
    public static class LoggingHelper
    {
        /// <summary>
        /// Sanitizes file paths by replacing username with ~
        /// Example: /Users/john/projects/app → ~/projects/app
        /// </summary>
        public static string SanitizePath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return path ?? "";

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith(homeDir, StringComparison.OrdinalIgnoreCase))
            {
                return "~" + path.Substring(homeDir.Length);
            }

            return path;
        }

        /// <summary>
        /// Creates a stable hash for repository paths (for analytics)
        /// Example: /Users/john/projects/myapp → repo:a1b2c3d4
        /// </summary>
        public static string HashRepoPath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return "unknown";

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(path));
            var hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();
            return $"repo:{hashString.Substring(0, 8)}";
        }

        /// <summary>
        /// Sanitizes API key for logging (shows only prefix)
        /// Example: sk-abc123xyz → sk-ab***
        /// </summary>
        public static string SanitizeApiKey(string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return "none";

            if (apiKey.Length <= 6)
                return "***";

            return $"{apiKey.Substring(0, 5)}***";
        }

        /// <summary>
        /// Truncates long text for logging
        /// Example: Long diff → first 200 chars + "... (truncated)"
        /// </summary>
        public static string Truncate(string? text, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";

            if (text.Length <= maxLength)
                return text;

            return $"{text.Substring(0, maxLength)}... (truncated, total: {text.Length} chars)";
        }

        /// <summary>
        /// Gets the standardized log directory path.
        /// Accounts for environment variables and CI/CD environments.
        /// </summary>
        public static string GetLogDirectory()
        {
            // Check environment variable first (for testing/CI)
            var envPath = Environment.GetEnvironmentVariable("GITBUDDY_LOGS_PATH");
            if (!string.IsNullOrEmpty(envPath))
                return envPath;

            // Check if running in CI/CD environment
            if (IsRunningInCI())
            {
                // Use temp directory for CI/CD to avoid permission issues
                return Path.Combine(Path.GetTempPath(), "gitbuddy-logs");
            }

            // Use standard location: ~/.gitbuddy/logs
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".gitbuddy", "logs");
        }

        private static bool IsRunningInCI()
        {
            return Environment.GetEnvironmentVariable("CI") == "true" ||
                   Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ||
                   Environment.GetEnvironmentVariable("GITLAB_CI") == "true" ||
                   Environment.GetEnvironmentVariable("CIRCLECI") == "true";
        }
    }
}
