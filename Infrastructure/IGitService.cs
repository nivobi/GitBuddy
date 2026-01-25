namespace GitBuddy.Infrastructure
{
    public interface IGitService
    {
        Task<ProcessResult> RunAsync(string args, CancellationToken cancellationToken = default);
        Task<ProcessResult> RunAsync(string args, string fileName, CancellationToken cancellationToken = default);
        Task<bool> IsGitRepositoryAsync(CancellationToken cancellationToken = default);
    }
}
