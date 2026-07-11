namespace ProfanityFilterEditor.Models;

/// <summary>
/// A discovered Minecraft Bedrock (Windows/UWP) install.
/// </summary>
public record MinecraftInstance(string InstallPath, string ExePath, string DataFolder, string WordListPath)
{
    /// <summary>The trailing package folder name, e.g. Microsoft.MinecraftUWP_1.26.3301.0_x64__8wekyb3d8bbwe</summary>
    public string PackageFolderName => System.IO.Path.GetFileName(InstallPath.TrimEnd('\\', '/'));

    public bool WordListExists => System.IO.File.Exists(WordListPath);
}
