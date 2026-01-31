using System.Reflection;

namespace GitBuddy.Commands.CICD;

public interface IEmbeddedResourceLoader
{
    Task<string?> LoadTemplateAsync(string templateFileName, CancellationToken cancellationToken);
}

public class EmbeddedResourceLoader : IEmbeddedResourceLoader
{
    private readonly string _baseResourcePath = "GitBuddy.Templates.GitHubActions";

    public async Task<string?> LoadTemplateAsync(string templateFileName, CancellationToken cancellationToken)
    {
        var resourceName = $"{_baseResourcePath}.{templateFileName}";
        var assembly = Assembly.GetExecutingAssembly();
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        
        return null;
    }
}
