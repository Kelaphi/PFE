namespace ProfanityFilterEditor.Models;

/// <summary>
/// Persisted settings for the app. Stored as JSON in %AppData%\ProfanityFilterEditor\config.json.
/// </summary>
public class AppConfig
{
    /// <summary>
    /// The install folder of the Minecraft instance the user picked last time,
    /// e.g. C:\Program Files\WindowsApps\Microsoft.MinecraftUWP_1.26.3301.0_x64__8wekyb3d8bbwe
    /// </summary>
    public string? MinecraftInstallPath { get; set; }
}
