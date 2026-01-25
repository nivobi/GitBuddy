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

        public string Run(string args, string fileName = "git")
        {
            return _processRunner.Run(fileName, args);
        }
    }
}