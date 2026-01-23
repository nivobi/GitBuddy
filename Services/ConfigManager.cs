using System;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace GitBuddy.Services
{
    public class AppConfig
    {
        public string AiProvider { get; set; } = "openai";
        public string AiModel { get; set; } = "gpt-4o-mini"; 
        public string EncryptedApiKey { get; set; } = string.Empty;
    }

    public static class ConfigManager
    {
        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "GitBuddy", 
            "config.json");

        public static void SaveConfig(string provider, string model, string rawKey)
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var config = new AppConfig
            {
                AiProvider = provider,
                AiModel = model,
                EncryptedApiKey = Encrypt(rawKey)
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        public static (string Provider, string Model, string ApiKey) LoadConfig()
        {
            if (!File.Exists(ConfigPath)) return ("openai", "gpt-4o-mini", "");

            try 
            {
                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                
                string provider = config?.AiProvider ?? "openai";
                string model = config?.AiModel ?? "gpt-4o-mini";
                string decryptedKey = Decrypt(config?.EncryptedApiKey ?? "");

                return (provider, model, decryptedKey);
            }
            catch
            {
                return ("openai", "gpt-4o-mini", "");
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

        private static string Decrypt(string encryptedText)
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
            catch { return string.Empty; }
        }
    }
}