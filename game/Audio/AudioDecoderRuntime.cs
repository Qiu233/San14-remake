using Godot;
using San14.Data;

/// <summary>Independent audio decoder availability. A missing or mismatched
/// audio runtime has an explicit diagnostic and does not stop map creation.</summary>
public static class AudioDecoderRuntime
{
    public static VgmstreamAudioDecoder Create(string? projectDirectory = null)
    {
        string executable = OS.GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathRooted(executable))
            executable = System.Environment.ProcessPath ?? throw new InvalidDataException("Client executable path is unavailable.");
        string applicationDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))
            ?? throw new InvalidDataException("Client executable has no runtime directory.");
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            projectDirectory = null;
            // An exported PCK has no physical res:// directory. Keep that virtual
            // root out of Path.GetFullPath; the application-native manifest remains
            // discoverable independently from the source project's folder.
            string resourcePath = ProjectSettings.GlobalizePath("res://");
            string? parent = !string.IsNullOrWhiteSpace(resourcePath) && Path.IsPathRooted(resourcePath) &&
                Directory.Exists(resourcePath)
                ? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(resourcePath)))
                : null;
            if (parent is not null && Directory.Exists(Path.Combine(parent, "runtime", "windows-x64", "audio")))
                projectDirectory = parent;
            else if (Directory.Exists(Path.Combine(System.Environment.CurrentDirectory, "tools", "audio")))
                projectDirectory = System.Environment.CurrentDirectory;
        }
        string manifest = AudioRuntime.FindManifest(applicationDirectory, projectDirectory);
        string folder = Path.GetDirectoryName(manifest)!;
        if (!File.Exists(Path.Combine(folder, "COPYING")) || !Directory.Exists(Path.Combine(folder, "licenses")))
            throw new FileNotFoundException("Audio runtime is missing its retained third-party license notices.", folder);
        string? multichannel = System.Environment.GetEnvironmentVariable("SAN14_MULTICHANNEL_AUDIO_DECODER");
        if (string.IsNullOrWhiteSpace(multichannel))
        {
            string installed = Path.Combine(folder, "San14.Audio.Decoder.exe");
            string? built = projectDirectory is null ? null : Path.Combine(projectDirectory,
                "native", "San14.Audio.Decoder", "bin", "x64", "Release", "San14.Audio.Decoder.exe");
            multichannel = File.Exists(installed) ? installed : built is not null && File.Exists(built) ? built : null;
        }
        return VgmstreamAudioDecoder.FromManifest(manifest, multichannelExecutablePath: multichannel);
    }

    public static bool TryCreate(out VgmstreamAudioDecoder? decoder, out string? unavailableReason,
        string? projectDirectory = null)
    {
        try { decoder = Create(projectDirectory); unavailableReason = null; return true; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            System.Text.Json.JsonException or KeyNotFoundException or ArgumentException or NotSupportedException)
        {
            decoder = null; unavailableReason = "Original audio unavailable: " + error.Message; return false;
        }
    }
}
