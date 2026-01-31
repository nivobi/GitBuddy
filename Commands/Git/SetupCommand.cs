using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Infrastructure;

namespace GitBuddy.Commands.Git
{
    public class SetupCommand : AsyncCommand<SetupCommand.Settings>
    {
        private readonly IGitService _gitService;

        public SetupCommand(IGitService gitService)
        {
            _gitService = gitService;
        }

        public class Settings : CommandSettings { }

        private const string ComprehensiveGitignore = @"# GitBuddy - Comprehensive .gitignore
# Covers: .NET, Node.js, Python, Java, Go, Rust, IDEs, OS files

#########################################
# Operating System Files
#########################################

# macOS
.DS_Store
.AppleDouble
.LSOverride
._*
.Spotlight-V100
.Trashes

# Windows
Thumbs.db
ehthumbs.db
Desktop.ini
$RECYCLE.BIN/
*.lnk

# Linux
*~
.fuse_hidden*
.directory
.Trash-*

#########################################
# IDEs and Editors
#########################################

# Visual Studio Code
.vscode/
!.vscode/settings.json
!.vscode/tasks.json
!.vscode/launch.json
!.vscode/extensions.json
*.code-workspace

# Visual Studio
.vs/
*.suo
*.user
*.userosscache
*.sln.docstates
*.userprefs
[Dd]ebug/
[Dd]ebugPublic/
[Rr]elease/
[Rr]eleases/
x64/
x86/
[Aa][Rr][Mm]/
[Aa][Rr][Mm]64/
bld/
[Bb]in/
[Oo]bj/
[Ll]og/
[Ll]ogs/

# JetBrains IDEs (IntelliJ, WebStorm, PyCharm, Rider, etc.)
.idea/
*.iml
*.ipr
*.iws
.idea_modules/

# Vim
*.swp
*.swo
*~
.netrwhist

# Emacs
*~
\#*\#
/.emacs.desktop
/.emacs.desktop.lock
*.elc

#########################################
# .NET / C#
#########################################

# Build results
[Dd]ebug/
[Dd]ebugPublic/
[Rr]elease/
[Rr]eleases/
x64/
x86/
[Ww][Ii][Nn]32/
[Aa][Rr][Mm]/
[Aa][Rr][Mm]64/
bld/
[Bb]in/
[Oo]bj/

# .NET Core
project.lock.json
project.fragment.lock.json
artifacts/

# NuGet
*.nupkg
*.snupkg
**/packages/*
!**/packages/build/
*.nuget.props
*.nuget.targets

# Test Results
[Tt]est[Rr]esult*/
[Bb]uild[Ll]og.*
*.trx
*.coverage
*.coveragexml

#########################################
# Node.js / JavaScript / TypeScript
#########################################

# Dependencies
node_modules/
jspm_packages/
bower_components/

# Build outputs
dist/
build/
out/
.cache/
.parcel-cache/

# Next.js
.next/
next-env.d.ts
*.tsbuildinfo
.vercel/
.turbo/

# Nuxt.js
.nuxt/
.output/

# Gatsby
.cache/
public/

# Logs
npm-debug.log*
yarn-debug.log*
yarn-error.log*
lerna-debug.log*
pnpm-debug.log*

# Test coverage
coverage/
.nyc_output/

# Package manager files
.pnp.*
.yarn/
!.yarn/patches
!.yarn/plugins
!.yarn/releases
!.yarn/sdks
!.yarn/versions

# Environment variables
.env
.env.local
.env.development.local
.env.test.local
.env.production.local

#########################################
# Python
#########################################

# Byte-compiled / optimized
__pycache__/
*.py[cod]
*$py.class
*.so

# Virtual environments
venv/
env/
ENV/
.venv/
.env/
pip-log.txt

# Distribution / packaging
build/
develop-eggs/
dist/
eggs/
.eggs/
lib/
lib64/
parts/
sdist/
var/
wheels/
*.egg-info/
.installed.cfg
*.egg

# Jupyter Notebook
.ipynb_checkpoints/
*.ipynb

# PyCharm
.idea/

#########################################
# Java
#########################################

# Compiled class files
*.class

# Package Files
*.jar
*.war
*.nar
*.ear
*.zip
*.tar.gz
*.rar

# Build tools
target/
.gradle/
build/
.mvn/
mvnw
mvnw.cmd

# IntelliJ
.idea/
*.iml

#########################################
# Go
#########################################

# Binaries
*.exe
*.exe~
*.dll
*.so
*.dylib

# Test binary
*.test

# Output of the go coverage tool
*.out

# Dependency directories
vendor/

#########################################
# Rust
#########################################

# Compiled files
target/
Cargo.lock

# Backup files
**/*.rs.bk

#########################################
# Ruby
#########################################

*.gem
*.rbc
/.config
/coverage/
/InstalledFiles
/pkg/
/spec/reports/
/spec/examples.txt
/test/tmp/
/test/version_tmp/
/tmp/
.bundle/
vendor/bundle

#########################################
# Secrets & Credentials
#########################################

# Environment variables
.env
.env.*
!.env.example
!.env.sample

# Credentials
*.pem
*.key
*.cert
*.crt
*credentials*
*secret*
*.p12
*.pfx

# Config files with secrets
appsettings.Development.json
appsettings.Local.json
secrets.json
.secrets

# SSH keys
id_rsa
id_dsa
id_ecdsa
id_ed25519

#########################################
# Database
#########################################

*.sqlite
*.sqlite3
*.db
*.db-shm
*.db-wal

#########################################
# Logs
#########################################

*.log
logs/
*.log.*

#########################################
# Temporary Files
#########################################

*.tmp
*.temp
*.bak
*.swp
*.swo
*~

#########################################
# Archives
#########################################

*.7z
*.dmg
*.gz
*.iso
*.rar
*.tar
*.zip

#########################################
# GitBuddy Specific
#########################################

# Keep .buddycontext for project context
# .buddycontext
";


        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            AnsiConsole.Write(new Rule("[yellow]GitBuddy Setup[/]"));

            // 1. Git Init
            var initResult = await _gitService.RunAsync("init", cancellationToken);
            AnsiConsole.MarkupLine(initResult.Output.Contains("Initialized")
                ? "[green]✔ Git repository initialized.[/]"
                : "[yellow]! Already a Git repository.[/]");

            // 2. .gitignore
            if (!File.Exists(".gitignore"))
            {
                AnsiConsole.MarkupLine("[grey]Creating comprehensive .gitignore...[/]");
                await File.WriteAllTextAsync(".gitignore", ComprehensiveGitignore, cancellationToken);
                AnsiConsole.MarkupLine("[green]✔ Added comprehensive .gitignore (covers .NET, Node, Python, Java, Go, Rust, and more)[/]");
            }

            // 3. AUTO .buddycontext
            if (!File.Exists(".buddycontext"))
            {
                AnsiConsole.MarkupLine("[grey]Generating project context...[/]");

                string projectName = Path.GetFileName(Directory.GetCurrentDirectory()) ?? "Unknown Project";
                string techClue = Directory.GetFiles(".", "*.csproj").Length > 0 ? "a .NET C# project" : "a software project";

                string contextContent = $"Project: {projectName}\nGoal: {techClue}\nTone: Professional and concise.";
                File.WriteAllText(".buddycontext", contextContent);

                AnsiConsole.MarkupLine("[green]✔ Created .buddycontext[/]");
            }

            AnsiConsole.Write(new Panel("Project is ready. Run 'buddy save' to save work manually, or 'buddy save --ai' for help.")
                .BorderColor(Color.Green));

            return 0;
        }
    }
}