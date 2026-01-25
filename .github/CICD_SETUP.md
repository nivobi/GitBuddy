# CI/CD Setup Guide

## Overview

Your GitBuddy project now has automated CI/CD pipelines that:

1. **CI (Continuous Integration)** - Automatically tests every commit
2. **CD (Continuous Deployment)** - Automatically publishes to NuGet when you create a release tag

---

## CI Pipeline (`ci.yml`)

### What it does:
- Runs on every push and pull request
- Tests on 3 platforms: Windows, macOS, Linux
- Runs all unit tests
- Checks code formatting
- Creates NuGet package (on master branch only)

### How to use:
Just push code! GitHub Actions runs automatically.

```bash
git add .
git commit -m "Your changes"
git push
```

Then check: https://github.com/nivobi/GitBuddy/actions

### What you'll see:
✅ **Green checkmark** = Everything passed
❌ **Red X** = Something failed (tests, build, or formatting)

---

## CD Pipeline (`publish.yml`)

### What it does:
- Runs when you create a version tag (e.g., `v1.2.0`)
- Builds and tests the code
- Publishes to NuGet.org
- Creates a GitHub Release with release notes

### Setup Required (ONE TIME):

#### 1. Get NuGet API Key
1. Go to https://www.nuget.org/account/apikeys
2. Click "Create"
3. Name: `GitBuddy GitHub Actions`
4. Glob Pattern: `Nivobi.GitBuddy`
5. Select scopes: `Push`
6. Copy the key (you'll only see it once!)

#### 2. Add API Key to GitHub Secrets
1. Go to: https://github.com/nivobi/GitBuddy/settings/secrets/actions
2. Click "New repository secret"
3. Name: `NUGET_API_KEY`
4. Value: Paste your NuGet API key
5. Click "Add secret"

### How to publish a new version:

#### Step 1: Update version in `.csproj`
Edit `GitBuddy.csproj`:
```xml
<Version>1.2.0</Version>
```

#### Step 2: Commit and push
```bash
git add GitBuddy.csproj
git commit -m "Bump version to 1.2.0"
git push
```

#### Step 3: Create and push a tag
```bash
git tag v1.2.0
git push origin v1.2.0
```

That's it! The pipeline will:
1. Build and test
2. Package as NuGet
3. Publish to NuGet.org
4. Create GitHub Release

### Check progress:
- Actions: https://github.com/nivobi/GitBuddy/actions
- Releases: https://github.com/nivobi/GitBuddy/releases
- NuGet: https://www.nuget.org/packages/Nivobi.GitBuddy

---

## Troubleshooting

### CI fails with "dotnet format" error
The code formatting check is currently set to `continue-on-error: true`, so it won't fail the build.

To fix formatting locally:
```bash
dotnet format
```

### CD fails with "401 Unauthorized"
- Check that `NUGET_API_KEY` secret is set correctly
- Verify the API key hasn't expired
- Make sure the key has `Push` permissions

### Tests fail on a specific platform
Check the test results in GitHub Actions to see which platform failed and what the error was.

---

## Viewing Results

### Build Status Badge
The README now shows a CI badge that updates automatically:
[![CI](https://github.com/nivobi/GitBuddy/actions/workflows/ci.yml/badge.svg)](https://github.com/nivobi/GitBuddy/actions/workflows/ci.yml)

### Test Results
Click on any workflow run to see:
- Build logs
- Test results by platform
- Code quality checks
- Package artifacts

---

## Advanced: Customizing Workflows

### Running CI on different branches
Edit `.github/workflows/ci.yml`:
```yaml
on:
  push:
    branches: [ master, main, develop, feature/* ]
```

### Skip CI for specific commits
Add `[skip ci]` to commit message:
```bash
git commit -m "Update README [skip ci]"
```

### Add code coverage
Add this to `ci.yml` after the test step:
```yaml
- name: Code Coverage
  run: dotnet test --collect:"XPlat Code Coverage"
```

---

## Quick Reference

| Action | Command |
|--------|---------|
| Check CI status | Visit https://github.com/nivobi/GitBuddy/actions |
| Fix formatting | `dotnet format` |
| Test locally | `dotnet test` |
| Build package | `dotnet pack --configuration Release` |
| Publish v1.2.0 | `git tag v1.2.0 && git push origin v1.2.0` |

---

## What Happens When

| Event | CI Pipeline | CD Pipeline |
|-------|-------------|-------------|
| Push to any branch | ✅ Runs | ❌ Doesn't run |
| Pull request | ✅ Runs | ❌ Doesn't run |
| Push to master | ✅ Runs + creates package | ❌ Doesn't run |
| Create tag `v1.2.0` | ❌ Doesn't run | ✅ Publishes to NuGet |

---

## Benefits

✅ Catch bugs before they reach production
✅ Ensure code works on Windows, Mac, and Linux
✅ Automated testing on every commit
✅ One command to publish: `git tag v1.2.0 && git push origin v1.2.0`
✅ Automatic release notes
✅ Professional GitHub presence with badges
