using System;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using GitBuddy.Infrastructure;
using Spectre.Console;

namespace GitBuddy.Services
{
    public class AppConfig
    {
        public string AiProvider { get; set; } = "openai";
        public string AiModel { get; set; } = "gpt-4o-mini";
        public string EncryptedApiKey { get; set; } = string.Empty;
    }

    public class ConfigManager : IConfigManager
    {
        private readonly IFileSystem _fileSystem;
        private readonly IAnsiConsole _console;
        private (string Provider, string Model, string ApiKey)? _cachedConfig;

        private string ConfigPath => _fileSystem.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GitBuddy",
            "config.json");

        public ConfigManager(IFileSystem fileSystem, IAnsiConsole console)
        {
            _fileSystem = fileSystem;
            _console = console;
        }

        public void SaveConfig(string provider, string model, string rawKey)
        {
            var directory = _fileSystem.Path.GetDirectoryName(ConfigPath);
            if (!_fileSystem.Directory.Exists(directory))
                _fileSystem.Directory.CreateDirectory(directory);

            var config = new AppConfig
            {
                AiProvider = provider,
                AiModel = model,
                EncryptedApiKey = Encrypt(rawKey)
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            _fileSystem.File.WriteAllText(ConfigPath, json);

            // Invalidate cache after saving new config
            _cachedConfig = null;
        }

        public (string Provider, string Model, string ApiKey) LoadConfig()
        {
            // Return cached config if available
            if (_cachedConfig.HasValue)
            {
                return _cachedConfig.Value;
            }

            if (!_fileSystem.File.Exists(ConfigPath))
            {
                var defaultConfig = ("openai", "gpt-4o-mini", "");
                _cachedConfig = defaultConfig;
                return defaultConfig;
            }

            try
            {
                string json = _fileSystem.File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);

                string provider = config?.AiProvider ?? "openai";
                string model = config?.AiModel ?? "gpt-4o-mini";
                string decryptedKey = Decrypt(config?.EncryptedApiKey ?? "");

                var loadedConfig = (provider, model, decryptedKey);
                _cachedConfig = loadedConfig;
                return loadedConfig;
            }
            catch (JsonException ex)
            {
                _console.MarkupLine("[yellow]⚠ Warning:[/] Config file is corrupted.");
                _console.MarkupLine($"[grey]Location: {ConfigPath}[/]");
                _console.MarkupLine($"[grey]Error: {ex.Message.EscapeMarkup()}[/]");
                _console.MarkupLine("[grey]Using default config. Run[/] [blue]buddy config[/] [grey]to reconfigure.[/]");
                var defaultConfig = ("openai", "gpt-4o-mini", "");
                _cachedConfig = defaultConfig;
                return defaultConfig;
            }
            catch (IOException ex)
            {
                _console.MarkupLine("[yellow]⚠ Warning:[/] Could not read config file.");
                _console.MarkupLine($"[grey]Location: {ConfigPath}[/]");
                _console.MarkupLine($"[grey]Error: {ex.Message.EscapeMarkup()}[/]");
                _console.MarkupLine("[grey]Using default config. Run[/] [blue]buddy config[/] [grey]to reconfigure.[/]");
                var defaultConfig = ("openai", "gpt-4o-mini", "");
                _cachedConfig = defaultConfig;
                return defaultConfig;
            }
            catch (UnauthorizedAccessException)
            {
                _console.MarkupLine("[yellow]⚠ Warning:[/] Permission denied reading config file.");
                _console.MarkupLine($"[grey]Location: {ConfigPath}[/]");
                _console.MarkupLine("[grey]Check file permissions. Using default config.[/]");
                var defaultConfig = ("openai", "gpt-4o-mini", "");
                _cachedConfig = defaultConfig;
                return defaultConfig;
            }
            catch (Exception ex)
            {
                _console.MarkupLine("[yellow]⚠ Warning:[/] Unexpected error loading config.");
                _console.MarkupLine($"[grey]Error: {ex.Message.EscapeMarkup()}[/]");
                _console.MarkupLine("[grey]Using default config. Run[/] [blue]buddy config[/] [grey]to reconfigure.[/]");
                var defaultConfig = ("openai", "gpt-4o-mini", "");
                _cachedConfig = defaultConfig;
                return defaultConfig;
            }
        }

        private static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                var bytes = Encoding.UTF8.GetBytes(plainText);
                return "BASE64_FALLBACK:" + Convert.ToBase64String(bytes);
            }
        }

        private string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return string.Empty;
            if (encryptedText.StartsWith("BASE64_FALLBACK:"))
            {
                var base64 = encryptedText.Replace("BASE64_FALLBACK:", "");
                return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            }
            try
            {
                byte[] data = Convert.FromBase64String(encryptedText);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (CryptographicException)
            {
                _console.MarkupLine("[yellow]⚠ Warning:[/] Failed to decrypt API key. It may have been encrypted on a different machine.");
                _console.MarkupLine("[grey]Run[/] [blue]buddy config[/] [grey]to reset your API key.[/]");
                return string.Empty;
            }
            catch (FormatException)
            {
                _console.MarkupLine("[yellow]⚠ Warning:[/] Config file contains invalid encrypted data.");
                _console.MarkupLine("[grey]Run[/] [blue]buddy config[/] [grey]to reconfigure.[/]");
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}