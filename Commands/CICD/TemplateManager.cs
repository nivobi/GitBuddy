using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace GitBuddy.Commands.CICD;

public class TemplateManager
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<TemplateManager>? _logger;
    
    private readonly Dictionary<string, ProjectTemplate> _templates = new()
    {
        ["dotnet"] = new("dotnet", ".NET", new[] { "*.csproj", "*.sln" }, "dotnet.yml"),
        ["nodejs"] = new("nodejs", "Node.js", new[] { "package.json" }, "nodejs.yml"),
        ["python"] = new("python", "Python", new[] { "requirements.txt", "pyproject.toml", "setup.py", "Pipfile", "poetry.lock" }, "python.yml"),
        ["go"] = new("go", "Go", new[] { "go.mod" }, "go.yml"),
        ["docker"] = new("docker", "Docker", new[] { "Dockerfile", "docker-compose.yml" }, "docker.yml"),
        ["generic"] = new("generic", "Generic", Array.Empty<string>(), "generic.yml")
    };

    public TemplateManager(IFileSystem fileSystem, ILogger<TemplateManager>? logger = null)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public string? DetectProjectType()
    {
        _logger?.LogDebug("Attempting to detect project type...");
        var currentDir = _fileSystem.Directory.GetCurrentDirectory();
        var files = _fileSystem.Directory.GetFiles(currentDir, "*.*");
        var fileSet = new HashSet<string>(files.Select(f => Path.GetFileName(f) ?? f), StringComparer.OrdinalIgnoreCase);

        foreach (var template in _templates.Values.Where(t => t.DetectionFiles.Any()))
        {
            foreach (var pattern in template.DetectionFiles)
            {
                if (!pattern.Contains('*'))
                {
                    if (fileSet.Contains(pattern))
                    {
                        _logger?.LogInformation("Detected project type: {ProjectType} (matched {File})", template.Key, pattern);
                        return template.Key;
                    }
                }
                else
                {
                    var ext = pattern.TrimStart('*');
                    if (files.Any(f => Path.GetFileName(f).EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger?.LogInformation("Detected project type: {ProjectType} (matched extension {Extension})", template.Key, ext);
                        return template.Key;
                    }
                }
            }
        }

        _logger?.LogWarning("Could not detect project type in {Directory}", currentDir);
        return null;
    }

    public ProjectTemplate? GetTemplate(string key) => 
        _templates.TryGetValue(key.ToLowerInvariant(), out var t) ? t : null;

    public IEnumerable<ProjectTemplate> GetAllTemplates() => _templates.Values;
}

public record ProjectTemplate(string Key, string DisplayName, string[] DetectionFiles, string TemplateFileName);
