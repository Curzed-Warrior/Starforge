using Godot;

namespace Starforge.Sim;

/// <summary>
/// The rules files (data/*.json), with mods: a file at user://mods/&lt;path&gt; is read in place of res://&lt;path&gt;.
/// An exported game packs res:// into its .pck, so the user folder is where a player can change the rules.
/// </summary>
public static class DataFiles
{
    public static string Read(string resPath)
    {
        string mod = "user://mods/" + resPath["res://".Length..];
        if (resPath.StartsWith("res://") && FileAccess.FileExists(mod))
        {
            GD.Print($"Mod: {mod} replaces {resPath}");
            return FileAccess.GetFileAsString(mod);
        }
        return FileAccess.GetFileAsString(resPath);
    }
}
