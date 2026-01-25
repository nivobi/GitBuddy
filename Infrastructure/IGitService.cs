namespace GitBuddy.Infrastructure
{
    public interface IGitService
    {
        string Run(string args, string fileName = "git");
    }
}
