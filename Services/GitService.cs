using GitBuddy.Infrastructure;

namespace GitBuddy.Services
{
    public class GitService : IGitService
    {
        private readonly IProcessRunner _processRunner;

        public GitService(IProcessRunner processRunner)
        {
            _processRunner = processRunner;
        }

        public Task<ProcessResult> RunAsync(string args, CancellationToken cancellationToken = default)
        {
            return _processRunner.RunAsync("git", args, cancellationToken);
        }

        public Task<ProcessResult> RunAsync(string args, string fileName, CancellationToken cancellationToken = default)
        {
            return _processRunner.RunAsync(fileName, args, cancellationToken);
        }

        public Task<ProcessResult> RunAsync(string args, int timeoutMs, CancellationToken cancellationToken = default)
        {
            return _processRunner.RunAsync("git", args, timeoutMs, cancellationToken);
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