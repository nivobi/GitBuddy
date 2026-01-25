namespace GitBuddy.Infrastructure
{
    public interface IProcessRunner
    {
        string Run(string fileName, string arguments);
    }
}
