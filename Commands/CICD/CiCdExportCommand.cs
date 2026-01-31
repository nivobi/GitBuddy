using Spectre.Console;
using Spectre.Console.Cli;
using System.IO.Abstractions;

namespace GitBuddy.Commands.CICD;

public class CiCdExportCommand : AsyncCommand<CiCdExportCommand.Settings>
{
    private readonly IFileSystem _fileSystem;
    private readonly IEmbeddedResourceLoader _resourceLoader;
    private readonly TemplateManager _templateManager;

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[TYPE]")]
        public string? TemplateType { get; set; }

        [CommandOption("-o|--output <PATH>")]
        public string? OutputPath { get; set; }

        [CommandOption("-a|--all")]
        public bool ExportAll { get; set; }
    }

    public CiCdExportCommand(IFileSystem fileSystem, IEmbeddedResourceLoader resourceLoader)
    {
        _fileSystem = fileSystem;
        _resourceLoader = resourceLoader;
        _templateManager = new TemplateManager(fileSystem);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold blue]📦 Export CI/CD Template[/]");
        AnsiConsole.WriteLine();

        if (settings.ExportAll)
        {
            return await ExportAllTemplatesAsync(settings, cancellationToken);
        }

        // Select template type
        string templateType;
        if (string.IsNullOrEmpty(settings.TemplateType))
        {
            var templates = _templateManager.GetAllTemplates().ToList();
            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Which template do you want to export?")
                    .PageSize(10)
                    .HighlightStyle(new Style(foreground: Color.Blue))
                    .AddChoices(templates.Select(t => $"{t.DisplayName} ({t.Key})")));

            templateType = templates.First(t => selection.Contains(t.Key)).Key;
        }
        else
        {
            templateType = settings.TemplateType.ToLowerInvariant();
        }

        var template = _templateManager.GetTemplate(templateType);
        if (template == null)
        {
            AnsiConsole.MarkupLine($"[bold red]❌ Unknown template type: {templateType}[/]");
            return 1;
        }

        // Determine output path
        var outputPath = settings.OutputPath ?? $"{templateType}.yml";

        // Load and export
        var content = await _resourceLoader.LoadTemplateAsync(template.TemplateFileName, cancellationToken);
        if (string.IsNullOrEmpty(content))
        {
            AnsiConsole.MarkupLine("[bold red]❌ Failed to load template.[/]");
            return 1;
        }

        try
        {
            await _fileSystem.File.WriteAllTextAsync(outputPath, content, cancellationToken);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold green]✨ Template exported to: {outputPath}[/]");
            AnsiConsole.MarkupLine("[grey]You can now customize this template and use it with --type.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[bold red]❌ Error:[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }

        return 0;
    }

    private async Task<int> ExportAllTemplatesAsync(Settings settings, CancellationToken cancellationToken)
    {
        var outputDir = settings.OutputPath ?? "./buddy-templates";
        
        if (!_fileSystem.Directory.Exists(outputDir))
        {
            _fileSystem.Directory.CreateDirectory(outputDir);
        }

        var templates = _templateManager.GetAllTemplates();
        int exported = 0;

        foreach (var template in templates)
        {
            var content = await _resourceLoader.LoadTemplateAsync(template.TemplateFileName, cancellationToken);
            if (!string.IsNullOrEmpty(content))
            {
                var outputPath = Path.Combine(outputDir, template.TemplateFileName);
                await _fileSystem.File.WriteAllTextAsync(outputPath, content, cancellationToken);
                exported++;
                AnsiConsole.MarkupLine($"  [green]✓[/] {template.DisplayName}");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold green]✨ Exported {exported} templates to: {outputDir}[/]");
        return 0;
    }
}
