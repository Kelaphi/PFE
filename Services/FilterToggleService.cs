using System.IO;
using ProfanityFilterEditor.Models;

namespace ProfanityFilterEditor.Services;

public enum FilterState
{
    /// <summary>File is at &lt;install&gt;\data\profanity_filter.wlist - Minecraft will read it normally.</summary>
    EnabledInData,
    /// <summary>File has been moved up to &lt;install&gt;\profanity_filter.wlist - Minecraft won't find it there.</summary>
    DisabledInRoot,
    /// <summary>File isn't in either known location.</summary>
    NotFound
}

/// <summary>
/// Lets you "disable" the profanity filter without touching its contents, by relocating
/// the untouched file out of the data folder Minecraft reads from (and back again to
/// re-enable it). This preserves the original file if you don't want to edit it at all.
/// </summary>
public static class FilterToggleService
{
    private const string FileName = "profanity_filter.wlist";

    public static string GetRootPath(MinecraftInstance instance) =>
        Path.Combine(instance.InstallPath, FileName);

    public static FilterState GetState(MinecraftInstance instance)
    {
        if (File.Exists(instance.WordListPath)) return FilterState.EnabledInData;
        if (File.Exists(GetRootPath(instance))) return FilterState.DisabledInRoot;
        return FilterState.NotFound;
    }

    /// <summary>The path currently holding the file, based on whichever location it's actually in.</summary>
    public static string? GetActivePath(MinecraftInstance instance) => GetState(instance) switch
    {
        FilterState.EnabledInData => instance.WordListPath,
        FilterState.DisabledInRoot => GetRootPath(instance),
        _ => null
    };

    /// <summary>Moves the file between the data folder and the install root, flipping its state.</summary>
    public static FilterState Toggle(MinecraftInstance instance)
    {
        var state = GetState(instance);
        var dataPath = instance.WordListPath;
        var rootPath = GetRootPath(instance);

        switch (state)
        {
            case FilterState.EnabledInData:
                Directory.CreateDirectory(instance.InstallPath);
                File.Move(dataPath, rootPath, overwrite: true);
                return FilterState.DisabledInRoot;

            case FilterState.DisabledInRoot:
                Directory.CreateDirectory(instance.DataFolder);
                File.Move(rootPath, dataPath, overwrite: true);
                return FilterState.EnabledInData;

            default:
                throw new FileNotFoundException(
                    $"Couldn't find {FileName} in either the data folder or the install root.");
        }
    }
}
