namespace GitBuddy.Infrastructure
{
    public interface IConfigManager
    {
        void SaveConfig(string provider, string model, string rawKey);
        (string Provider, string Model, string ApiKey) LoadConfig();
    }
}
