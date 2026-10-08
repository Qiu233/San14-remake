namespace San14.Data;

/// <summary>Locates the explicit decoder configuration, exported runtime or local
/// project tool in that order. No media or decoder is downloaded by the client.</summary>
public static class OriginalAudioRuntime
{
    public static string FindManifest(string applicationDirectory, string? projectDirectory = null)
    {
        string? configured = Environment.GetEnvironmentVariable("SAN14_AUDIO_DECODER_MANIFEST");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string path = Path.GetFullPath(configured);
            if (!File.Exists(path)) throw new FileNotFoundException("Configured audio decoder manifest is missing.", path);
            return path;
        }
        string exported = Path.Combine(Path.GetFullPath(applicationDirectory), "native", "windows-x64", "audio", "PROVENANCE.json");
        if (File.Exists(exported)) return exported;
        string legacyExport = Path.Combine(Path.GetFullPath(applicationDirectory), "native", "audio", "vgmstream-r2117", "PROVENANCE.json");
        if (File.Exists(legacyExport)) return legacyExport;
        if (projectDirectory is not null)
        {
            string runtime = Path.Combine(Path.GetFullPath(projectDirectory), "runtime", "windows-x64", "audio", "PROVENANCE.json");
            if (File.Exists(runtime)) return runtime;
            string tool = Path.Combine(Path.GetFullPath(projectDirectory), "tools", "audio", "vgmstream-r2117", "PROVENANCE.json");
            if (File.Exists(tool)) return tool;
        }
        throw new FileNotFoundException("The selected original-audio decoder runtime is not installed. Supply SAN14_AUDIO_DECODER_MANIFEST or the exported native/windows-x64/audio runtime.");
    }
}
