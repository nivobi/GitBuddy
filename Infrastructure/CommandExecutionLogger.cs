using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GitBuddy.Infrastructure
{
    /// <summary>
    /// Helper to log command execution start/end consistently.
    /// Generic type parameter allows type-safe logging.
    /// </summary>
    public class CommandExecutionLogger<T> : IDisposable
    {
        private readonly ILogger<T> _logger;
        private readonly string _commandName;
        private readonly Stopwatch _stopwatch;
        private bool _completed;

        public CommandExecutionLogger(ILogger<T> logger, string commandName, object? settings = null)
        {
            _logger = logger;
            _commandName = commandName;
            _stopwatch = Stopwatch.StartNew();

            if (settings != null)
            {
                _logger.LogInformation("Command started: {CommandName} with settings: {@Settings}",
                    commandName, settings);
            }
            else
            {
                _logger.LogInformation("Command started: {CommandName}", commandName);
            }
        }

        public void Complete(int exitCode = 0)
        {
            _stopwatch.Stop();
            _completed = true;

            _logger.LogInformation("Command completed: {CommandName} in {Duration}ms with exit code: {ExitCode}",
                _commandName, _stopwatch.ElapsedMilliseconds, exitCode);
        }

        public void Dispose()
        {
            if (!_completed)
            {
                _stopwatch.Stop();
                _logger.LogWarning("Command ended without explicit completion: {CommandName} after {Duration}ms",
                    _commandName, _stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
