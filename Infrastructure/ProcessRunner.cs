using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GitBuddy.Infrastructure
{
    public class ProcessRunner : IProcessRunner
    {
        private const int DefaultTimeoutMs = 30000; // 30 seconds
        private readonly ILogger<ProcessRunner> _logger;

        public ProcessRunner(ILogger<ProcessRunner> logger)
        {
            _logger = logger;
        }

        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            return RunAsync(fileName, arguments, DefaultTimeoutMs, cancellationToken);
        }

        public async Task<ProcessResult> RunAsync(string fileName, string arguments, int timeoutMs, CancellationToken cancellationToken = default)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo)
                ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            try
            {
                var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

                await process.WaitForExitAsync(cts.Token);

                var output = await outputTask;
                var error = await errorTask;

                return new ProcessResult(process.ExitCode, output.Trim(), error.Trim());
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    _logger.LogWarning("Process '{FileName}' was killed due to timeout after {TimeoutMs}ms",
                        fileName, timeoutMs);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to kill process '{FileName}' (may have already exited)", fileName);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Process execution was cancelled.", cancellationToken);
                }

                throw new TimeoutException($"Process '{fileName}' timed out after {timeoutMs}ms");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Process '{FileName}' failed with exception", fileName);
                throw;
            }
        }
    }
}
