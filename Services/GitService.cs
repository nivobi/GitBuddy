using System.Diagnostics;
using Microsoft.Extensions.Logging;
using GitBuddy.Infrastructure;

namespace GitBuddy.Services
{
    public class GitService : IGitService
    {
        private readonly IProcessRunner _processRunner;
        private readonly ILogger<GitService> _logger;

        public GitService(IProcessRunner processRunner, ILogger<GitService> logger)
        {
            _processRunner = processRunner;
            _logger = logger;
        }

        public async Task<ProcessResult> RunAsync(string args, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogDebug("Executing git command: {GitCommand}", $"git {args}");

            try
            {
                var result = await _processRunner.RunAsync("git", args, cancellationToken);
                stopwatch.Stop();

                if (result.ExitCode == 0)
                {
                    _logger.LogDebug("Git command succeeded: {GitCommand} in {Duration}ms",
                        $"git {args}", stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogWarning("Git command failed: {GitCommand}, ExitCode: {ExitCode}, StdErr: {StdErr}",
                        $"git {args}", result.ExitCode, LoggingHelper.Truncate(result.Error));
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Git command threw exception: {GitCommand} after {Duration}ms",
                    $"git {args}", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        public Task<ProcessResult> RunAsync(string args, string fileName, CancellationToken cancellationToken = default)
        {
            return _processRunner.RunAsync(fileName, args, cancellationToken);
        }

        public async Task<ProcessResult> RunAsync(string args, int timeoutMs, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogDebug("Executing git command: {GitCommand} with timeout: {Timeout}ms",
                $"git {args}", timeoutMs);

            try
            {
                var result = await _processRunner.RunAsync("git", args, timeoutMs, cancellationToken);
                stopwatch.Stop();

                if (result.ExitCode == 0)
                {
                    _logger.LogDebug("Git command succeeded: {GitCommand} in {Duration}ms",
                        $"git {args}", stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogWarning("Git command failed: {GitCommand}, ExitCode: {ExitCode}, StdErr: {StdErr}",
                        $"git {args}", result.ExitCode, LoggingHelper.Truncate(result.Error));
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Git command threw exception: {GitCommand} after {Duration}ms",
                    $"git {args}", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        public async Task<bool> IsGitRepositoryAsync(CancellationToken cancellationToken = default)
        {
            var result = await RunAsync("rev-parse --is-inside-work-tree", cancellationToken);
            return result.Output.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string?> GetCurrentBranchAsync(CancellationToken cancellationToken = default)
        {
            var result = await RunAsync("branch --show-current", cancellationToken);
            return string.IsNullOrWhiteSpace(result.Output) ? null : result.Output;
        }

        public async Task<bool> HasUncommittedChangesAsync(CancellationToken cancellationToken = default)
        {
            var result = await RunAsync("status --porcelain", cancellationToken);
            return !string.IsNullOrWhiteSpace(result.Output);
        }

        public async Task<string?> GetRemoteUrlAsync(string remoteName = "origin", CancellationToken cancellationToken = default)
        {
            var result = await RunAsync($"remote get-url {remoteName}", cancellationToken);
            return string.IsNullOrWhiteSpace(result.Output) ? null : result.Output;
        }

        public async Task<List<string>> GetAllBranchesAsync(CancellationToken cancellationToken = default)
        {
            var result = await RunAsync("branch -a", cancellationToken);
            if (string.IsNullOrWhiteSpace(result.Output))
            {
                return new List<string>();
            }

            return result.Output
                .Split('\n')
                .Select(b => b.Trim().TrimStart('*').Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToList();
        }
    }
}