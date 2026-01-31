# GitBuddy 🤖

[![CI](https://github.com/nivobi/GitBuddy/actions/workflows/ci.yml/badge.svg)](https://github.com/nivobi/GitBuddy/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Nivobi.GitBuddy.svg)](https://www.nuget.org/packages/Nivobi.GitBuddy/)
[![Downloads](https://img.shields.io/nuget/dt/Nivobi.GitBuddy.svg)](https://www.nuget.org/packages/Nivobi.GitBuddy/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Focus on coding, let GitBuddy handle the chores.**

Git is an amazing tool, but I've always found it to be a lot to manage during a busy day. I built GitBuddy to act as a personal assistant—handling the repetitive parts of the workflow so I can stay focused on my code.

> ⚠️ **Note:** This project is currently **in development**. I'm building it to refine my own workflow, and I'm adding new features as I learn.

---

## 💡 The Vision
The goal of GitBuddy isn't to replace Git, but to make it feel more intuitive. I wanted a tool that:
* **Asks the right questions:** Like offering to create a GitHub repo for me if it notices one is missing.
* **Handles the repetitive stuff:** Combining pulling, rebasing, and pushing into a single "Sync."
* **Works smarter:** Using AI to analyze code changes so I don't have to spend time thinking about commit messages when I'm in the flow.

## ✨ Current Features

### Core Commands
- **Smart Sync:** `buddy sync` handles the heavy lifting of communicating with GitHub. It detects if a repository doesn't exist yet and offers to create it (Public or Private) and link it for you. After syncing, it automatically detects merged branches and offers to clean them up (both local and remote).

- **AI-Powered Saves:** `buddy save --ai` stages my work and uses an LLM to suggest professional commit messages based on what I actually changed.

- **AI Context Bridge:** `buddy describe` creates a `.buddycontext` file. This acts as a "briefing document" for the AI, providing the high-level project knowledge it needs to generate better commit messages.

### Branch Management
- **Smart Branch Operations:** `buddy branch` provides intuitive branch management:
  - `buddy branch create <name>` - Create branches with automatic naming conventions (feature/, bugfix/, hotfix/)
  - `buddy branch switch` - Interactive branch switcher with uncommitted change detection
  - `buddy branch list` - Display all branches with commit info in a formatted table
  - `buddy branch delete` - Safely delete branches with automatic switching if needed
  - `buddy branch rename` - Rename branches easily (current or any other)
  - `buddy branch clean` - Remove all merged branches at once

### Merge Command
- **Intelligent Merging:** `buddy merge` simplifies the merge workflow:
  - Interactive branch selection for merging
  - `buddy merge --into` - Reverse merge: merge current branch into another without switching
  - `buddy merge --ai` - AI-generated merge commit messages that summarize changes
  - Automatic conflict detection with helpful resolution guidance
  - Preview commits before merging
  - Fast-forward detection

### Stash Management
- **Smart Stash Operations:** `buddy stash` provides intuitive stash management:
  - `buddy stash push [message]` - Stash current changes with optional message
  - `buddy stash pop [index]` - Apply and remove stash interactively
  - `buddy stash apply [index]` - Apply stash without removing it
  - `buddy stash list` - Display all stashes in a formatted table
  - `buddy stash push -u` - Include untracked files when stashing

### Undo Operations
- **Safe Undo:** `buddy undo` helps you recover from mistakes:
  - Undo last commit (soft reset - keeps your changes)
  - Discard all changes (hard reset - destructive)
  - Interactive confirmation to prevent accidents

### Repository Status
- **Quick Status:** `buddy status` shows a clean, formatted view of your current changes

### Configuration
- **AI Provider Setup:** `buddy config` configures your AI integration:
  - Choose from OpenAI, OpenRouter, or DeepSeek
  - Select models (GPT-4o, Claude, Gemini, DeepSeek, etc.)
  - Securely store and encrypt your API keys

### CI/CD Generation
- **Workflow Generator:** `buddy cicd` automatically generates GitHub Actions workflows:
  - Auto-detects project type (.NET, Node.js, Python, Go, Docker, or Generic)
  - Creates `.github/workflows/ci.yml` tailored to your project
  - Options: `--type`, `--output`, `--force`, `--dry-run`
  
- **Interactive Setup:** `buddy cicd-init` guided wizard:
  - Step-by-step project configuration
  - Select features (build, test, lint, security scan, deploy)
  - Choose target branches
  
- **Template Export:** `buddy cicd-export` for customization:
  - Export single template: `buddy cicd-export python -o my-template.yml`
  - Export all templates: `buddy cicd-export --all -o ./templates/`
  - Modify and use your own customized workflows

### Release Management
- **Semantic Versioning:** `buddy release` simplifies creating version tags:
  - `buddy release` - Show current version, commits since last release, and suggested next versions
  - `buddy release patch` - Create patch release (e.g., 1.2.3 → 1.2.4)
  - `buddy release minor` - Create minor release (e.g., 1.2.3 → 1.3.0)
  - `buddy release major` - Create major release (e.g., 1.2.3 → 2.0.0)
  - `buddy release patch --push` - Create and push tag to trigger CI/CD
  - `buddy release minor --message "Breaking: New API" --push` - Custom message with auto-push
  - `buddy release minor --dry-run` - Preview changes without creating tags
  - Works with any project using semantic versioning
  - Confirmation prompt before creating tags
  - Shows commits since last release to help decide version bump type

### Other Tools
- **Clean Setup:** `buddy setup` gets a new folder ready with a solid `.gitignore` so I don't accidentally upload junk or build files.

- **Self-Updating:** `buddy update` checks NuGet for the latest version and updates itself automatically.

- **Version Check:** `buddy --version` instantly lets you know which version of the tool you are running.



## 📖 Quick Start Examples

### Daily Workflow
```bash
# Check current status
buddy status

# Create a new feature branch
buddy branch create dark-mode
# Creates: feature/dark-mode

# Make your changes, then save with AI
buddy save --ai
# AI analyzes your changes and suggests a commit message

# Need to switch to another branch? Stash your work first
buddy stash push "WIP: dark mode styling"

# Later, come back and restore your work
buddy stash pop

# Merge your feature into master
buddy merge --into master --ai
# Merges current branch into master with AI-generated merge message

# Push and cleanup
buddy sync
# Pushes changes and offers to delete merged branches
```

### Other Common Tasks
```bash
# Switch between branches
buddy branch switch
# Interactive selection of available branches

# Oops, made a mistake? Undo the last commit
buddy undo
# Options: undo last commit (keep changes) or discard all changes

# Clean up merged branches
buddy branch clean
# Removes all local branches that have been merged

# Generate CI/CD workflow (auto-detects project type)
buddy cicd
# Creates GitHub Actions workflow for your project

# Use interactive wizard for guided setup
buddy cicd-init

# Export templates to customize them
buddy cicd-export python -o my-python-ci.yml

# Create a new release
buddy release
# Shows current version, commits since last release, and suggested next versions

# Create a patch release and push it
buddy release patch --push
# Creates v1.2.3 → v1.2.4 tag and pushes to trigger CI/CD

# Preview a minor release without creating it
buddy release minor --dry-run
# Shows what would be created without making changes
```

## 🛠 Installation

You can now install GitBuddy directly as a global tool!

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
* [GitHub CLI (gh)](https://cli.github.com/) (Required for the auto-repo creation feature)

### Standard Install (Recommended)
```bash
dotnet tool install -g Nivobi.GitBuddy
```
### Manual Build (For Contributors)
If you want to modify the code yourself:
```bash
git clone [https://github.com/nivobi/GitBuddy.git](https://github.com/nivobi/GitBuddy.git)
cd GitBuddy
dotnet build -c Release
```

---

## 🤖 AI Setup (Optional)

To use AI-powered features like `buddy save --ai` and `buddy merge --ai`:

### 1. Configure your AI provider
```bash
buddy config
```

### 2. Choose a provider and model
GitBuddy supports three AI providers:

| Provider | Models Available | Setup |
|----------|-----------------|-------|
| **OpenAI** | GPT-4o, GPT-4o-mini, etc. | Get API key from [platform.openai.com](https://platform.openai.com/api-keys) |
| **OpenRouter** | DeepSeek, Claude, Gemini, and more | Get API key from [openrouter.ai](https://openrouter.ai/keys) |
| **DeepSeek** | DeepSeek Chat | Get API key from [platform.deepseek.com](https://platform.deepseek.com) |

The configuration is stored securely with encrypted API keys.

### 3. Set up project context (Recommended)
```bash
buddy describe
```
This creates a `.buddycontext` file that helps the AI understand your project for better commit messages.

---

## 🚀 CI/CD Setup

GitBuddy can auto-generate GitHub Actions workflows for multiple project types:

### Supported Project Types
- **.NET** (`.csproj`, `.sln` files) - Build, restore, test on Ubuntu
- **Node.js** (`package.json`) - Test on Node 18.x, 20.x, 22.x with npm
- **Python** (`requirements.txt`, `pyproject.toml`, `poetry.lock`) - Supports pip and Poetry
- **Go** (`go.mod`) - Build, test, and lint with golangci-lint
- **Docker** (`Dockerfile`) - Multi-stage builds, security scanning with Trivy
- **Generic** - Template for custom projects

### Quick Setup

**Auto-detect and generate:**
```bash
buddy cicd
```

**Specify project type manually:**
```bash
buddy cicd --type python
buddy cicd --type go --output .github/workflows/go-ci.yml
```

**Preview without creating files:**
```bash
buddy cicd --type docker --dry-run
```

**Force overwrite existing workflow:**
```bash
buddy cicd --force
```

### Interactive Setup Wizard

For a guided experience with customization options:
```bash
buddy cicd-init
```

This interactive wizard will:
1. Let you select your project type
2. Choose workflow features (build, test, lint, security scan, deploy)
3. Configure target branches
4. Generate a customized workflow

### Export Templates for Customization

Want to customize the templates? Export them first:

**Export a single template:**
```bash
buddy cicd-export python -o my-custom-python.yml
```

**Export all templates:**
```bash
buddy cicd-export --all -o ./my-templates/
```

After customizing, you can use your templates directly or reference them when creating workflows.

For detailed publishing instructions, see [CICD_SETUP.md](.github/CICD_SETUP.md).

---

## 🌱 Roadmap & Learning
This is my first open-source project, and I'm using it to explore:

- Building CLI tools with C# and Spectre.Console.

- Bridging the gap between local development and AI intelligence.

- Automating Git processes to reduce cognitive load.

If you have ideas or want to follow along with the development, feel free to check out the code!