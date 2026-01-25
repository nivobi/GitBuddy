namespace GitBuddy.Infrastructure
{
    public interface IGitService
    {
        // Low-level git command execution
        Task<ProcessResult> RunAsync(string args, CancellationToken cancellationToken = default);
        Task<ProcessResult> RunAsync(string args, string fileName, CancellationToken cancellationToken = default);

        // Common git operations
        Task<bool> IsGitRepositoryAsync(CancellationToken cancellationToken = default);
        Task<string?> GetCurrentBranchAsync(CancellationToken cancellationToken = default);
        Task<bool> HasUncommittedChangesAsync(CancellationToken cancellationToken = default);
        Task<string?> GetRemoteUrlAsync(string remoteName = "origin", CancellationToken cancellationToken = default);
        Task<List<string>> GetAllBranchesAsync(CancellationToken cancellationToken = default);
    }
}
