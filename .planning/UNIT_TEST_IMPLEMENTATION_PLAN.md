# Unit Testing Implementation Plan for GitBuddy

## Executive Summary

This plan outlines adding comprehensive unit tests to GitBuddy with a focus on testability, maintainability, and industry best practices. Phase 1 targets the three core services with proper dependency injection and mocking strategies.

**Estimated Time:** 2-3 hours for Phase 1
**Testing Framework:** xUnit + Moq + System.IO.Abstractions

---

## Phase 1: Service Layer Testing (Priority)

### 1.1 Test Project Setup

**Step 1: Create Test Project Structure**

```bash
cd /Users/nicolaibirk/Documents/coding/Git-Buddy
mkdir -p tests/GitBuddy.Tests/Services
cd tests
dotnet new xunit -n GitBuddy.Tests
cd GitBuddy.Tests
```

**Step 2: Add NuGet Packages**

```bash
dotnet add package Moq --version 4.20.70
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Spectre.Console.Testing --version 0.49.1
dotnet add package System.IO.Abstractions --version 20.0.4
dotnet add package System.IO.Abstractions.TestingHelpers --version 20.0.4
```

**Step 3: Add Project Reference**

```bash
dotnet add reference ../../GitBuddy/GitBuddy.csproj
```

**Step 4: Update Solution** (if solution exists)

```bash
cd ../..
dotnet sln add tests/GitBuddy.Tests/GitBuddy.Tests.csproj
```

---

### 1.2 Code Refactoring for Testability

#### Problem Areas in Current Code

1. **Static AnsiConsole calls** - Cannot be mocked
2. **Direct File.*/Directory.* calls** - Cannot be mocked
3. **Static HttpClient in AiService** - Already good!
4. **Process execution in GitService** - Needs abstraction
5. **Static ConfigManager methods** - Hard to test

#### Refactoring Strategy

**Priority 1: AiService (Highest Impact)**

Current issues:
- Direct `File.Exists()` and `File.ReadAllText()` calls (line 26)
- Static `AnsiConsole.MarkupLine()` calls (lines 21, 50, 109-145)
- Static `ConfigManager.LoadConfig()` calls

**Changes needed:**
```csharp
// Before (current)
public static class AiService
{
    public static async Task<string?> GenerateCommitMessage(string diff)
    {
        var (provider, model, apiKey) = ConfigManager.LoadConfig();
        if (string.IsNullOrEmpty(apiKey))
        {
            AnsiConsole.MarkupLine("[yellow]⚠ No AI configured.[/]");
            return null;
        }

        string projectContext = "a software project";
        if (File.Exists(".buddycontext"))
            projectContext = File.ReadAllText(".buddycontext").Trim();
    }
}

// After (testable)
public class AiService
{
    private readonly IAnsiConsole _console;
    private readonly IFileSystem _fileSystem;
    private readonly IConfigManager _configManager;
    private readonly HttpClient _httpClient;

    public AiService(
        IAnsiConsole console,
        IFileSystem fileSystem,
        IConfigManager configManager,
        HttpClient httpClient)
    {
        _console = console;
        _fileSystem = fileSystem;
        _configManager = configManager;
        _httpClient = httpClient;
    }

    public async Task<string?> GenerateCommitMessage(string diff)
    {
        var (provider, model, apiKey) = _configManager.LoadConfig();
        if (string.IsNullOrEmpty(apiKey))
        {
            _console.MarkupLine("[yellow]⚠ No AI configured.[/]");
            return null;
        }

        string projectContext = "a software project";
        if (_fileSystem.File.Exists(".buddycontext"))
            projectContext = _fileSystem.File.ReadAllText(".buddycontext").Trim();
    }
}
```

**Priority 2: ConfigManager**

Current issues:
- Static methods with File I/O
- Direct `File.*` and `Directory.*` calls
- Hard to test encryption/decryption

**Changes needed:**
```csharp
// Before
public static class ConfigManager
{
    public static void SaveConfig(string provider, string model, string rawKey)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(ConfigPath, json);
    }
}

// After
public interface IConfigManager
{
    void SaveConfig(string provider, string model, string rawKey);
    (string Provider, string Model, string ApiKey) LoadConfig();
}

public class ConfigManager : IConfigManager
{
    private readonly IFileSystem _fileSystem;

    public ConfigManager(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void SaveConfig(string provider, string model, string rawKey)
    {
        var directory = _fileSystem.Path.GetDirectoryName(ConfigPath);
        if (!_fileSystem.Directory.Exists(directory))
            _fileSystem.Directory.CreateDirectory(directory);
        _fileSystem.File.WriteAllText(ConfigPath, json);
    }
}
```

**Priority 3: GitService**

Current issues:
- Direct Process.Start() calls
- No way to mock git command output

**Changes needed:**
```csharp
// Create abstraction
public interface IProcessRunner
{
    string Run(string fileName, string arguments);
}

public class ProcessRunner : IProcessRunner
{
    public string Run(string fileName, string arguments)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process == null) return "Error: Could not start process";

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return string.IsNullOrWhiteSpace(output) ? error.Trim() : output.Trim();
    }
}

// Update GitService
public interface IGitService
{
    string Run(string args, string fileName = "git");
}

public class GitService : IGitService
{
    private readonly IProcessRunner _processRunner;

    public GitService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public string Run(string args, string fileName = "git")
    {
        return _processRunner.Run(fileName, args);
    }
}
```

---

### 1.3 Test Implementation

#### Test 1: GitService Tests

**File:** `tests/GitBuddy.Tests/Services/GitServiceTests.cs`

```csharp
using Xunit;
using Moq;
using FluentAssertions;
using GitBuddy.Services;

namespace GitBuddy.Tests.Services
{
    public class GitServiceTests
    {
        private readonly Mock<IProcessRunner> _mockProcessRunner;
        private readonly GitService _sut;

        public GitServiceTests()
        {
            _mockProcessRunner = new Mock<IProcessRunner>();
            _sut = new GitService(_mockProcessRunner.Object);
        }

        [Fact]
        public void Run_WithValidCommand_ReturnsOutput()
        {
            // Arrange
            var expectedOutput = "branch: main";
            _mockProcessRunner
                .Setup(x => x.Run("git", "branch --show-current"))
                .Returns(expectedOutput);

            // Act
            var result = _sut.Run("branch --show-current");

            // Assert
            result.Should().Be(expectedOutput);
            _mockProcessRunner.Verify(x => x.Run("git", "branch --show-current"), Times.Once);
        }

        [Fact]
        public void Run_WithCustomFileName_UsesCorrectExecutable()
        {
            // Arrange
            _mockProcessRunner
                .Setup(x => x.Run("dotnet", "build"))
                .Returns("Build succeeded");

            // Act
            var result = _sut.Run("build", "dotnet");

            // Assert
            result.Should().Be("Build succeeded");
            _mockProcessRunner.Verify(x => x.Run("dotnet", "build"), Times.Once);
        }

        [Fact]
        public void Run_WithError_ReturnsErrorMessage()
        {
            // Arrange
            var errorMessage = "fatal: not a git repository";
            _mockProcessRunner
                .Setup(x => x.Run("git", "status"))
                .Returns(errorMessage);

            // Act
            var result = _sut.Run("status");

            // Assert
            result.Should().Contain("fatal");
        }
    }
}
```

#### Test 2: ConfigManager Tests

**File:** `tests/GitBuddy.Tests/Services/ConfigManagerTests.cs`

```csharp
using Xunit;
using FluentAssertions;
using GitBuddy.Services;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;

namespace GitBuddy.Tests.Services
{
    public class ConfigManagerTests
    {
        private readonly MockFileSystem _mockFileSystem;
        private readonly ConfigManager _sut;

        public ConfigManagerTests()
        {
            _mockFileSystem = new MockFileSystem();
            _sut = new ConfigManager(_mockFileSystem);
        }

        [Fact]
        public void SaveConfig_CreatesDirectoryIfNotExists()
        {
            // Arrange
            var configPath = _sut.GetConfigPath(); // Will need to expose this
            var directory = _mockFileSystem.Path.GetDirectoryName(configPath);

            // Act
            _sut.SaveConfig("openai", "gpt-4", "test-key");

            // Assert
            _mockFileSystem.Directory.Exists(directory).Should().BeTrue();
        }

        [Fact]
        public void SaveConfig_WritesJsonFile()
        {
            // Arrange & Act
            _sut.SaveConfig("openai", "gpt-4", "test-key");

            // Assert
            var configPath = _sut.GetConfigPath();
            _mockFileSystem.File.Exists(configPath).Should().BeTrue();

            var json = _mockFileSystem.File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json);

            config.Should().NotBeNull();
            config!.AiProvider.Should().Be("openai");
            config.AiModel.Should().Be("gpt-4");
        }

        [Fact]
        public void LoadConfig_WhenFileNotExists_ReturnsDefaults()
        {
            // Act
            var (provider, model, apiKey) = _sut.LoadConfig();

            // Assert
            provider.Should().Be("openai");
            model.Should().Be("gpt-4o-mini");
            apiKey.Should().BeEmpty();
        }

        [Fact]
        public void LoadConfig_WhenFileExists_ReturnsStoredValues()
        {
            // Arrange
            _sut.SaveConfig("deepseek", "deepseek-chat", "my-api-key");

            // Act
            var (provider, model, apiKey) = _sut.LoadConfig();

            // Assert
            provider.Should().Be("deepseek");
            model.Should().Be("deepseek-chat");
            apiKey.Should().Be("my-api-key");
        }

        [Theory]
        [InlineData("openai")]
        [InlineData("openrouter")]
        [InlineData("deepseek")]
        public void SaveConfig_WithDifferentProviders_StoresCorrectly(string provider)
        {
            // Act
            _sut.SaveConfig(provider, "test-model", "test-key");
            var (loadedProvider, _, _) = _sut.LoadConfig();

            // Assert
            loadedProvider.Should().Be(provider);
        }
    }
}
```

#### Test 3: AiService Tests

**File:** `tests/GitBuddy.Tests/Services/AiServiceTests.cs`

```csharp
using Xunit;
using Moq;
using Moq.Protected;
using FluentAssertions;
using GitBuddy.Services;
using Spectre.Console.Testing;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GitBuddy.Tests.Services
{
    public class AiServiceTests
    {
        private readonly TestConsole _testConsole;
        private readonly MockFileSystem _mockFileSystem;
        private readonly Mock<IConfigManager> _mockConfigManager;
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly HttpClient _httpClient;
        private readonly AiService _sut;

        public AiServiceTests()
        {
            _testConsole = new TestConsole();
            _mockFileSystem = new MockFileSystem();
            _mockConfigManager = new Mock<IConfigManager>();
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object);

            _sut = new AiService(
                _testConsole,
                _mockFileSystem,
                _mockConfigManager.Object,
                _httpClient);
        }

        [Fact]
        public async Task GenerateCommitMessage_WithNoApiKey_ReturnsNull()
        {
            // Arrange
            _mockConfigManager
                .Setup(x => x.LoadConfig())
                .Returns(("openai", "gpt-4", ""));

            // Act
            var result = await _sut.GenerateCommitMessage("test diff");

            // Assert
            result.Should().BeNull();
            _testConsole.Output.Should().Contain("No AI configured");
        }

        [Fact]
        public async Task GenerateCommitMessage_WithBuddyContext_UsesProjectContext()
        {
            // Arrange
            _mockConfigManager
                .Setup(x => x.LoadConfig())
                .Returns(("openai", "gpt-4", "test-key"));

            _mockFileSystem.AddFile(".buddycontext", new MockFileData("GitBuddy CLI tool"));

            var responseJson = @"{
                ""choices"": [{
                    ""message"": {
                        ""content"": ""Add error handling""
                    }
                }]
            }";

            _mockHttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson)
                });

            // Act
            var result = await _sut.GenerateCommitMessage("diff content");

            // Assert
            result.Should().Be("Add error handling");
        }

        [Fact]
        public async Task GenerateCommitMessage_WithApiError401_ShowsInvalidKeyMessage()
        {
            // Arrange
            _mockConfigManager
                .Setup(x => x.LoadConfig())
                .Returns(("openai", "gpt-4", "invalid-key"));

            _mockHttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Unauthorized,
                    Content = new StringContent("Invalid API key")
                });

            // Act
            var result = await _sut.GenerateCommitMessage("test diff");

            // Assert
            result.Should().BeNull();
            _testConsole.Output.Should().Contain("Invalid API key");
            _testConsole.Output.Should().Contain("buddy config");
        }

        [Fact]
        public async Task GenerateCommitMessage_WithRateLimit_ShowsRateLimitMessage()
        {
            // Arrange
            _mockConfigManager
                .Setup(x => x.LoadConfig())
                .Returns(("openai", "gpt-4", "test-key"));

            _mockHttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.TooManyRequests
                });

            // Act
            var result = await _sut.GenerateCommitMessage("test diff");

            // Assert
            result.Should().BeNull();
            _testConsole.Output.Should().Contain("Rate limit exceeded");
        }

        [Fact]
        public async Task DescribeProject_WithValidResponse_ReturnsDescription()
        {
            // Arrange
            _mockConfigManager
                .Setup(x => x.LoadConfig())
                .Returns(("openai", "gpt-4", "test-key"));

            var responseJson = @"{
                ""choices"": [{
                    ""message"": {
                        ""content"": ""This is a .NET CLI tool for Git automation.""
                    }
                }]
            }";

            _mockHttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson)
                });

            // Act
            var result = await _sut.DescribeProject("project data");

            // Assert
            result.Should().Contain(".NET CLI tool");
        }

        [Theory]
        [InlineData("openai")]
        [InlineData("openrouter")]
        [InlineData("deepseek")]
        public async Task GenerateCommitMessage_WithDifferentProviders_UsesCorrectUrl(string provider)
        {
            // Arrange
            _mockConfigManager
                .Setup(x => x.LoadConfig())
                .Returns((provider, "model", "key"));

            HttpRequestMessage? capturedRequest = null;
            _mockHttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(@"{""choices"":[{""message"":{""content"":""test""}}]}")
                });

            // Act
            await _sut.GenerateCommitMessage("diff");

            // Assert
            capturedRequest.Should().NotBeNull();
            capturedRequest!.RequestUri.Should().NotBeNull();

            var expectedHost = provider switch
            {
                "openai" => "api.openai.com",
                "openrouter" => "openrouter.ai",
                "deepseek" => "api.deepseek.com",
                _ => "api.openai.com"
            };

            capturedRequest.RequestUri!.Host.Should().Be(expectedHost);
        }
    }
}
```

---

### 1.4 Dependency Injection Setup

To make all this work, we need to set up DI in Program.cs:

**File:** `GitBuddy/Program.cs`

```csharp
using System.IO.Abstractions;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using GitBuddy.Commands.Git;
using GitBuddy.Commands.Config;
using GitBuddy.Commands.Utility;
using GitBuddy.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GitBuddy
{
    class Program
    {
        static int Main(string[] args)
        {
            // Create service collection
            var services = new ServiceCollection();

            // Register services
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<IConfigManager, ConfigManager>();
            services.AddSingleton<IGitService, GitService>();
            services.AddSingleton<HttpClient>();
            services.AddSingleton<IAiService, AiService>();

            // Register commands will be done via registrar
            var registrar = new TypeRegistrar(services);
            var app = new CommandApp(registrar);

            app.Configure(config =>
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "1.0.0";

                config.SetApplicationName("git-buddy");
                config.SetApplicationVersion(version);

                // Commands will auto-resolve dependencies
                config.AddCommand<StatusCommand>("status")
                    .WithDescription("Check the current state of the repo.");

                config.AddCommand<SaveCommand>("save")
                    .WithDescription("Stage and commit all changes.");

                // ... rest of commands
            });

            return app.Run(args);
        }
    }
}
```

**Create TypeRegistrar:**

**File:** `GitBuddy/Infrastructure/TypeRegistrar.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace GitBuddy.Infrastructure
{
    public sealed class TypeRegistrar : ITypeRegistrar
    {
        private readonly IServiceCollection _services;

        public TypeRegistrar(IServiceCollection services)
        {
            _services = services;
        }

        public ITypeResolver Build()
        {
            return new TypeResolver(_services.BuildServiceProvider());
        }

        public void Register(Type service, Type implementation)
        {
            _services.AddSingleton(service, implementation);
        }

        public void RegisterInstance(Type service, object implementation)
        {
            _services.AddSingleton(service, implementation);
        }

        public void RegisterLazy(Type service, Func<object> factory)
        {
            _services.AddSingleton(service, _ => factory());
        }
    }

    public sealed class TypeResolver : ITypeResolver
    {
        private readonly IServiceProvider _provider;

        public TypeResolver(IServiceProvider provider)
        {
            _provider = provider;
        }

        public object? Resolve(Type? type)
        {
            return type == null ? null : _provider.GetService(type);
        }
    }
}
```

---

### 1.5 Running Tests

```bash
# From repository root
dotnet test

# With detailed output
dotnet test --logger "console;verbosity=detailed"

# With code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Run specific test class
dotnet test --filter "FullyQualifiedName~AiServiceTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~AiServiceTests.GenerateCommitMessage_WithNoApiKey_ReturnsNull"
```

---

## Implementation Order

### Step-by-Step Execution Plan

1. **Setup (15 min)**
   - Create test project
   - Add NuGet packages
   - Verify project compiles

2. **Create Interfaces (30 min)**
   - Create `IProcessRunner` and `ProcessRunner`
   - Create `IConfigManager` interface
   - Create `IAiService` and `IGitService` interfaces
   - Build to verify no errors

3. **Refactor ConfigManager (30 min)**
   - Update ConfigManager to use IFileSystem
   - Convert to instance class with interface
   - Test manually that config still works

4. **Refactor GitService (20 min)**
   - Update GitService to use IProcessRunner
   - Convert to instance class with interface
   - Verify git commands still work

5. **Refactor AiService (45 min)**
   - Update AiService to use IAnsiConsole, IFileSystem, IConfigManager
   - Convert to instance class with interface
   - Test AI features still work

6. **Setup DI (30 min)**
   - Create TypeRegistrar/TypeResolver
   - Update Program.cs to register services
   - Update all commands to accept dependencies via constructor
   - Test all commands still work

7. **Write Tests (45 min)**
   - Write GitService tests
   - Write ConfigManager tests
   - Write AiService tests
   - Run all tests and verify they pass

**Total Estimated Time: 3-4 hours**

---

## Success Criteria

- ✅ All tests pass (`dotnet test` returns 0)
- ✅ Code coverage >70% for service layer
- ✅ All existing functionality still works
- ✅ No breaking changes to command interface
- ✅ Build succeeds with 0 errors

---

## Risks and Mitigation

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Breaking changes to commands | High | Test manually after each refactor step |
| DI complexity | Medium | Use simple registrar pattern, thoroughly test |
| HttpClient mocking complexity | Medium | Use HttpMessageHandler mock pattern |
| Encryption testing | Medium | Focus on happy path, keep encryption logic separate |

---

## Phase 2 Preview (Future)

After Phase 1 completes, we can add:
- Command layer tests (SaveCommand, BranchCommand, etc.)
- Integration tests using CommandAppTester
- CI/CD pipeline with automated test runs
- Code coverage reporting in GitHub Actions

---

## File Changes Summary

### New Files
- `tests/GitBuddy.Tests/Services/GitServiceTests.cs`
- `tests/GitBuddy.Tests/Services/ConfigManagerTests.cs`
- `tests/GitBuddy.Tests/Services/AiServiceTests.cs`
- `GitBuddy/Infrastructure/TypeRegistrar.cs`
- `GitBuddy/Infrastructure/TypeResolver.cs`

### Modified Files
- `GitBuddy/Services/GitService.cs` - Convert to instance class with IProcessRunner
- `GitBuddy/Services/ConfigManager.cs` - Convert to instance class with IFileSystem
- `GitBuddy/Services/AiService.cs` - Convert to instance class with dependencies
- `GitBuddy/Program.cs` - Add DI setup
- `GitBuddy/Commands/Git/*.cs` - Add constructor injection for services
- `GitBuddy/Commands/Config/*.cs` - Add constructor injection for services

---

## Next Steps After Plan Approval

1. Create feature branch: `feature/add-unit-tests-phase1`
2. Follow implementation order step-by-step
3. Commit after each major step
4. Run tests continuously
5. Merge when all tests pass
