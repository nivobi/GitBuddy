using System.Threading.Tasks;

namespace GitBuddy.Infrastructure
{
    public interface IAiService
    {
        Task<string?> GenerateCommitMessage(string diff);
        Task<string?> DescribeProject(string projectData);
    }
}
