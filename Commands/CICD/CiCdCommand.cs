using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.CICD;

public class CiCdCommand : AsyncCommand<CiCdCommand.Settings>
{
    private readonly IFileSystem _fileSystem;
    private readonly IEmbeddedResourceLoader _resourceLoader;
    private readonly TemplateManager _templateManager;
    private readonly ILogger<CiCdCommand> _logger;

    public class Settings : CommandSettings
    {
        [CommandOption("-t|--type <TYPE>")]
        [Description("Project type (dotnet, nodejs, python, go, docker, generic)")]
        public string? ProjectType { get; set; }

        [CommandOption("-o|--output <PATH>")]
        [Description("Output file path (default: .github/workflows/ci.yml)")]
        public string? OutputPath { get; set; }

        [CommandOption("-f|--force")]
        [Description("Overwrite existing workflow file without prompting")]
        public bool Force { get; set; }

        [CommandOption("-n|--dry-run")]
        [Description("Show what would be generated without writing files")]
        public bool DryRun { get; set; }
    }

    public CiCdCommand(
        IFileSystem fileSystem,
        IEmbeddedResourceLoader resourceLoader,
        TemplateManager templateManager,
        ILogger<CiCdCommand> logger)
    {
        _fileSystem = fileSystem;
        _resourceLoader = resourceLoader;
        _templateManager = templateManager;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        using var execLog = new CommandExecutionLogger<CiCdCommand>(_logger, "cicd", settings);

        AnsiConsole.MarkupLine("[bold blue]🤖 GitBuddy CI/CD Setup[/]");
        AnsiConsole.WriteLine();

        // Detect or use specified project type
        string? projectType = settings.ProjectType?.ToLowerInvariant();
        
        if (string.IsNullOrEmpty(projectType))
        {
            projectType = _templateManager.DetectProjectType();
        }

        if (string.IsNullOrEmpty(projectType))
        {
            AnsiConsole.MarkupLine("[bold red]❌ Could not auto-detect project type.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Supported types:");
            foreach (var t in _templateManager.GetAllTemplates())
            {
                AnsiConsole.MarkupLine($"  [green]• {t.DisplayName}[/] ({t.Key})");
            }
            return 1;
        }

        var template = _templateManager.GetTemplate(projectType);

        // Load template content
        var yamlContent = await _resourceLoader.LoadTemplateAsync(template.TemplateFileName, cancellationToken);
        if (string.IsNullOrEmpty(yamlContent))
        {
            AnsiConsole.MarkupLine("[bold red]❌ Failed to load template.[/]");
            return 1;
        }

        // Determine output path
        var filePath = settings.OutputPath ?? Path.Combine(".github", "workflows", "ci.yml");

        if (settings.DryRun)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold yellow]📄 Dry Run - Would generate:[/]");
            AnsiConsole.WriteLine(yamlContent);
            return 0;
        }

        // Check if file exists
        if (_fileSystem.File.Exists(filePath) && !settings.Force)
        {
            AnsiConsole.MarkupLine("[bold yellow]⚠ Warning: A CI file already exists.[/]");
            if (!AnsiConsole.Confirm("Overwrite it?"))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                return 0;
            }
        }

        // Write file
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !_fileSystem.Directory.Exists(dir))
            {
                _fileSystem.Directory.CreateDirectory(dir);
            }

            await _fileSystem.File.WriteAllTextAsync(filePath, yamlContent, cancellationToken);
            
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]✨ Success! Workflow generated.[/]");
            AnsiConsole.MarkupLine($"[grey]git add {filePath} && git commit -m \"ci: add github actions workflow\"[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[bold red]❌ Error:[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }

        execLog.Complete(0);
        return 0;
    }
}

// Branch settings for cicd command
public class CiCdSettings : CommandSettings { }
