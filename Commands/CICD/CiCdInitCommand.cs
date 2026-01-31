using Spectre.Console;
using Spectre.Console.Cli;
using System.IO.Abstractions;

namespace GitBuddy.Commands.CICD;

public class CiCdInitCommand : AsyncCommand<CiCdInitCommand.Settings>
{
    private readonly IFileSystem _fileSystem;
    private readonly IEmbeddedResourceLoader _resourceLoader;
    private readonly TemplateManager _templateManager;

    public class Settings : CommandSettings
    {
        [CommandOption("-o|--output <PATH>")]
        public string? OutputPath { get; set; }
    }

    public CiCdInitCommand(IFileSystem fileSystem, IEmbeddedResourceLoader resourceLoader)
    {
        _fileSystem = fileSystem;
        _resourceLoader = resourceLoader;
        _templateManager = new TemplateManager(fileSystem);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold blue]🚀 GitBuddy CI/CD Interactive Setup[/]");
        AnsiConsole.WriteLine();

        // Step 1: Select project type
        var detectedType = _templateManager.DetectProjectType();
        var templates = _templateManager.GetAllTemplates().ToList();

        var selectedType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What type of project is this?")
                .PageSize(10)
                .HighlightStyle(new Style(foreground: Color.Blue))
                .AddChoices(templates.Select(t => $"{t.DisplayName} ({t.Key})")));

        var projectType = templates.First(t => selectedType.Contains(t.Key)).Key;

        // Step 2: Select workflow features
        var features = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("What features do you want in your workflow?")
                .PageSize(10)
                .InstructionsText("[grey](Press [space] to toggle, [enter] to accept)[/]")
                .AddChoices(new[]
                {
                    "Build",
                    "Test",
                    "Lint",
                    "Deploy",
                    "Security Scan"
                }));

        // Step 3: Branch configuration
        var targetBranches = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Which branches should trigger the workflow?")
                .PageSize(10)
                .Required()
                .InstructionsText("[grey](Press [space] to toggle, [enter] to accept)[/]")
                .AddChoices(new[] { "main", "master", "develop" }));

        // Step 4: Confirm before generating
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Configuration Summary:[/]");
        AnsiConsole.MarkupLine($"  Project Type: [cyan]{projectType}[/]");
        AnsiConsole.MarkupLine($"  Features: [cyan]{string.Join(", ", features)}[/]");
        AnsiConsole.MarkupLine($"  Branches: [cyan]{string.Join(", ", targetBranches)}[/]");
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("Generate workflow file?"))
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return 0;
        }

        // Generate the workflow
        var template = _templateManager.GetTemplate(projectType);
        var yamlContent = await _resourceLoader.LoadTemplateAsync(template!.TemplateFileName, cancellationToken);

        if (string.IsNullOrEmpty(yamlContent))
        {
            AnsiConsole.MarkupLine("[bold red]❌ Failed to load template.[/]");
            return 1;
        }

        // Customize the template based on selected features
        yamlContent = CustomizeTemplate(yamlContent, features, targetBranches);

        // Write the file
        var filePath = settings.OutputPath ?? Path.Combine(".github", "workflows", "ci.yml");
        
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

        return 0;
    }

    private string CustomizeTemplate(string yamlContent, List<string> features, List<string> branches)
    {
        // Replace branch names in the template
        var branchList = string.Join(", ", branches.Select(b => $"\"{b}\""));
        yamlContent = System.Text.RegularExpressions.Regex.Replace(
            yamlContent,
            @"branches: \[.*?\]",
            $"branches: [ {branchList} ]");

        return yamlContent;
    }
}
