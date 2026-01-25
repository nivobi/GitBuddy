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

        public async Task<bool> IsGitRepositoryAsync(CancellationToken cancellationToken = default)
        {
            var result = await RunAsync("rev-parse --is-inside-work-tree", cancellationToken);
            return result.Output.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}