# GitBuddy 🤖

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

### Other Tools
- **Clean Setup:** `buddy setup` gets a new folder ready with a solid `.gitignore` so I don't accidentally upload junk or build files.

- **Self-Updating:** `buddy update` checks NuGet for the latest version and updates itself automatically.

- **Version Check:** `buddy --version` instantly lets you know which version of the tool you are running.



## 📖 Quick Start Examples

```bash
# Create a new feature branch
buddy branch create dark-mode
# Creates: feature/dark-mode

# Make your changes, then save with AI
buddy save --ai
# AI analyzes your changes and suggests a commit message

# Merge your feature into master
buddy merge --into master --ai
# Merges current branch into master with AI-generated merge message

# Push and cleanup
buddy sync
# Pushes changes and offers to delete merged branches

# Switch between branches
buddy branch switch
# Interactive selection of available branches
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

## 🌱 Roadmap & Learning
This is my first open-source project, and I'm using it to explore:

- Building CLI tools with C# and Spectre.Console.

- Bridging the gap between local development and AI intelligence.

- Automating Git processes to reduce cognitive load.

If you have ideas or want to follow along with the development, feel free to check out the code!