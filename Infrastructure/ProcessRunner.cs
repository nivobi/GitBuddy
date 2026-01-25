using System.Diagnostics;

namespace GitBuddy.Infrastructure
{
    public class ProcessRunner : IProcessRunner
    {
        private const int DefaultTimeoutMs = 30000; // 30 seconds

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
                }
                catch
                {
                    // Process may have already exited
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Process execution was cancelled.", cancellationToken);
                }

                throw new TimeoutException($"Process '{fileName}' timed out after {timeoutMs}ms");
            }
        }
    }
}
