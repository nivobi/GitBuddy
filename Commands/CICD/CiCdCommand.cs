using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GitBuddy.Commands.CICD
{
    public class CiCdCommand : AsyncCommand<CiCdCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            // We can add options like --force, --type, etc. later
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[bold blue]🤖 GitBuddy CI/CD Setup[/]");
            AnsiConsole.MarkupLine("[grey]I will analyze your project and generate a GitHub Actions workflow for you.[/]");
            AnsiConsole.WriteLine();

            // 1. Analyze Project Type
            var projectType = DetectProjectType();
            
            if (projectType == ProjectType.Unknown)
            {
                AnsiConsole.MarkupLine("[bold red]❌ Could not auto-detect project type.[/]");
                AnsiConsole.MarkupLine("Currently, I only support [green].NET[/] and [green]Node.js[/] projects automatically.");
                return 1;
            }

            AnsiConsole.MarkupLine($"[bold green]✓[/] Detected project type: [bold cyan]{projectType}[/]");

            // 2. Generate Template content
            var yamlContent = GenerateTemplate(projectType);
            var workflowDir = Path.Combine(".github", "workflows");
            var filePath = Path.Combine(workflowDir, "ci.yml");

            // 3. Confirm with user
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"I am ready to create the workflow file at: [blue]{filePath}[/]");
            
            if (File.Exists(filePath))
            {
                AnsiConsole.MarkupLine("[bold yellow]⚠ Warning: A CI file already exists at this location.[/]");
            }

            if (!AnsiConsole.Confirm("Do you want me to generate the file?"))
            {
                AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
                return 0;
            }

            // 4. Write File
            try
            {
                if (!Directory.Exists(workflowDir))
                {
                    Directory.CreateDirectory(workflowDir);
                }

                await File.WriteAllTextAsync(filePath, yamlContent, cancellationToken);
                
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold green]✨ Success! CI/CD workflow generated.[/]");
                AnsiConsole.MarkupLine("Commit and push this file to GitHub to trigger your first build:");
                AnsiConsole.MarkupLine($"[grey]git add {filePath} && git commit -m \"ci: add github actions workflow\" && git push[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[bold red]❌ Error writing file:[/]");
                AnsiConsole.WriteException(ex);
                return 1;
            }

            return 0;
        }

        private enum ProjectType
        {
            DotNet,
            NodeJs,
            Unknown
        }

        private ProjectType DetectProjectType()
        {
            var currentDir = Directory.GetCurrentDirectory();

            if (Directory.GetFiles(currentDir, "*.csproj", SearchOption.TopDirectoryOnly).Any() ||
                Directory.GetFiles(currentDir, "*.sln", SearchOption.TopDirectoryOnly).Any())
            {
                return ProjectType.DotNet;
            }

            if (File.Exists(Path.Combine(currentDir, "package.json")))
            {
                return ProjectType.NodeJs;
            }

            return ProjectType.Unknown;
        }

        private string GenerateTemplate(ProjectType type)
        {
            return type switch
            {
                ProjectType.DotNet => GetDotNetTemplate(),
                ProjectType.NodeJs => GetNodeJsTemplate(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        private string GetDotNetTemplate()
        {
            return @"name: CI

on:
  push:
    branches: [ ""main"", ""master"" ]
  pull_request:
    branches: [ ""main"", ""master"" ]

jobs:
  build:

    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 8.0.x
        
    - name: Restore dependencies
      run: dotnet restore
      
    - name: Build
      run: dotnet build --no-restore
      
    - name: Test
      run: dotnet test --no-build --verbosity normal";
        }

        private string GetNodeJsTemplate()
        {
            return @"name: Node.js CI

on:
  push:
    branches: [ ""main"", ""master"" ]
  pull_request:
    branches: [ ""main"", ""master"" ]

jobs:
  build:

    runs-on: ubuntu-latest

    strategy:
      matrix:
        node-version: [18.x, 20.x]

    steps:
    - uses: actions/checkout@v4
    
    - name: Use Node.js ${{ matrix.node-version }}
      uses: actions/setup-node@v4
      with:
        node-version: ${{ matrix.node-version }}
        cache: 'npm'
        
    - run: npm ci
    - run: npm run build --if-present
    - run: npm test";
        }
    }
}
