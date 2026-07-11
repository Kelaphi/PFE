using System.Diagnostics;
using System.IO;
using ProfanityFilterEditor.Models;

namespace ProfanityFilterEditor.Services;

/// <summary>
/// Finds installed copies of Minecraft Bedrock (Minecraft.Windows.exe) on this PC.
/// </summary>
public static class MinecraftLocator
{
    private const string ExeName = "Minecraft.Windows.exe";

    public static List<MinecraftInstance> FindInstances()
    {
        var found = new Dictionary<string, MinecraftInstance>(StringComparer.OrdinalIgnoreCase);

        // Primary method: ask Windows for installed Appx packages. This works even though
        // the WindowsApps folder itself is normally locked down to regular directory listing.
        foreach (var installLocation in QueryAppxInstallLocations())
        {
            TryAddInstance(installLocation, found);
        }

        // Fallback method: scan Program Files\WindowsApps directly for matching folder names.
        // This may fail silently with UnauthorizedAccessException on some systems - that's fine,
        // the Appx query above is the primary path.
        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var windowsApps = Path.Combine(programFiles, "WindowsApps");
        foreach (var dir in SafeEnumerateDirectories(windowsApps, "Microsoft.MinecraftUWP_*"))
        {
            TryAddInstance(dir, found);
        }

        return found.Values.OrderByDescending(i => i.PackageFolderName).ToList();
    }

    private static void TryAddInstance(string installPath, Dictionary<string, MinecraftInstance> found)
    {
        try
        {
            var exePath = Path.Combine(installPath, ExeName);
            if (!File.Exists(exePath)) return;

            var dataFolder = Path.Combine(installPath, "data");
            var wordListPath = Path.Combine(dataFolder, "profanity_filter.wlist");

            found[installPath] = new MinecraftInstance(installPath, exePath, dataFolder, wordListPath);
        }
        catch
        {
            // Skip folders we can't inspect (permissions, etc.)
        }
    }

    private static List<string> SafeEnumerateDirectories(string root, string pattern)
    {
        try
        {
            if (!Directory.Exists(root)) return new List<string>();
            return Directory.EnumerateDirectories(root, pattern).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Uses PowerShell's Get-AppxPackage to resolve install locations for any package
    /// with "Minecraft" in its name (covers Microsoft.MinecraftUWP and regional variants).
    /// </summary>
    private static List<string> QueryAppxInstallLocations()
    {
        var results = new List<string>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command " +
                            "\"Get-AppxPackage *Minecraft* | Select-Object -ExpandProperty InstallLocation\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return results;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(8000);

            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length > 0 && Directory.Exists(line))
                {
                    results.Add(line);
                }
            }
        }
        catch
        {
            // PowerShell unavailable or blocked - fall back to directory scan only.
        }

        return results;
    }
}
