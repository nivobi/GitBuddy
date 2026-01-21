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
- **Smart Sync:** `buddy sync` handles the heavy lifting of communicating with GitHub. It detects if a repository doesn't exist yet and offers to create it (Public or Private) and link it for you.
- **AI-Powered Saves:** `buddy save --ai` stages my work and uses an LLM to suggest professional commit messages based on what I actually changed.
- **AI Context Bridge:** `buddy describe` creates a `.buddycontext` file. This acts as a "briefing document" for the AI, providing the high-level project knowledge it needs to generate better commit messages.
- **Smart Branch Management:** `buddy branch` simplifies working with branches. Create branches with naming conventions (feature/, bugfix/), switch between branches interactively, list all branches with commit info, and clean up merged branches safely.
- **Clean Setup:** `buddy setup` gets a new folder ready with a solid `.gitignore` so I don't accidentally upload junk or build files.
- **Self-Updating:** `buddy update` checks NuGet for the latest version and updates itself automatically.
- **Version Check:** `buddy --version` instantly lets you know which version of the tool you are running.



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