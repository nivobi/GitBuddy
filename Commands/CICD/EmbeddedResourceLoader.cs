using System.Reflection;
using Microsoft.Extensions.Logging;

namespace GitBuddy.Commands.CICD;

public interface IEmbeddedResourceLoader
{
    Task<string?> LoadTemplateAsync(string templateFileName, CancellationToken cancellationToken);
}

public class EmbeddedResourceLoader : IEmbeddedResourceLoader
{
    private readonly string _baseResourcePath = "GitBuddy.Templates.GitHubActions";
    private readonly ILogger<EmbeddedResourceLoader> _logger;

    public EmbeddedResourceLoader(ILogger<EmbeddedResourceLoader> logger)
    {
        _logger = logger;
    }

    public async Task<string?> LoadTemplateAsync(string templateFileName, CancellationToken cancellationToken)
    {
        var resourceName = $"{_baseResourcePath}.{templateFileName}";
        var assembly = Assembly.GetExecutingAssembly();
        
        _logger.LogDebug("Loading embedded template: {ResourceName}", resourceName);
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        
        _logger.LogWarning("Embedded template not found: {ResourceName}", resourceName);
        return null;
    }
}
