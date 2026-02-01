using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace GitBuddy.Infrastructure
{
    /// <summary>
    /// Logging configuration class
    /// </summary>
    public class LoggingConfig
    {
        /// <summary>
        /// Enable or disable logging globally (default: true)
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Minimum log level: Error, Warning, Information, Debug (default: Information)
        /// </summary>
        public string Level { get; set; } = "Information";

        /// <summary>
        /// How many days to keep logs before auto-deletion (default: 30)
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Maximum file size in MB before rotation (default: 50)
        /// </summary>
        public int MaxFileSizeMB { get; set; } = 50;
    }

    /// <summary>
    /// Sets up Serilog logger with file sink and proper configuration
    /// </summary>
    public static class LoggerSetup
    {
        public static ILogger CreateLogger(LoggingConfig config)
        {
            // Get log directory path
            var logDir = LoggingHelper.GetLogDirectory();

            try
            {
                Directory.CreateDirectory(logDir);
            }
            catch
            {
                // If we can't create log directory, fall back to console-only logging
                return new LoggerConfiguration()
                    .MinimumLevel.Is(ParseLogLevel(config.Level))
                    .WriteTo.Console()
                    .CreateLogger();
            }

            // Determine log level
            var logLevel = ParseLogLevel(config.Level);

            // Create logger configuration
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId();

            // Add file sink if logging is enabled
            if (config.Enabled)
            {
                var logPath = Path.Combine(logDir, "gitbuddy.log");

                try
                {
                    loggerConfig.WriteTo.File(
                        new Serilog.Formatting.Json.JsonFormatter(),
                        path: logPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: config.RetentionDays,
                        fileSizeLimitBytes: config.MaxFileSizeMB * 1024 * 1024
                    );
                }
                catch
                {
                    // If file sink fails, continue with console-only
                }
            }

            return loggerConfig.CreateLogger();
        }

        private static LogEventLevel ParseLogLevel(string level)
        {
            // Check environment variable override first
            var envLevel = Environment.GetEnvironmentVariable("GITBUDDY_LOGGING_LEVEL");
            if (!string.IsNullOrEmpty(envLevel))
            {
                level = envLevel;
            }

            return level.ToLowerInvariant() switch
            {
                "debug" => LogEventLevel.Debug,
                "information" or "info" => LogEventLevel.Information,
                "warning" or "warn" => LogEventLevel.Warning,
                "error" => LogEventLevel.Error,
                _ => LogEventLevel.Information
            };
        }
    }
}
