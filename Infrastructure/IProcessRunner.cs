namespace GitBuddy.Infrastructure
{
    public record ProcessResult(int ExitCode, string Output, string Error);

    public interface IProcessRunner
    {
        Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
        Task<ProcessResult> RunAsync(string fileName, string arguments, int timeoutMs, CancellationToken cancellationToken = default);
    }
}
