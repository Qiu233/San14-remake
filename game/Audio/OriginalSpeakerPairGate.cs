using Godot;
using San14.Data;

/// <summary>Only the small native AudioEffect buffer virtual is implemented
/// outside managed code. Every instance is bound using the actual bus channel
/// returned by AudioServer; construction order never identifies a speaker.</summary>
internal static class OriginalSpeakerPairGate
{
    private static readonly StringName GateClass = "San14SpeakerPairGate";

    internal static SpeakerLayoutAvailability Availability()
    {
        if (AudioServer.GetSpeakerMode() is not (AudioServer.SpeakerMode.Surround31 or
            AudioServer.SpeakerMode.Surround51 or AudioServer.SpeakerMode.Surround71))
            return new(false, "audio.output.surround51_requires_multichannel_host");
        return BridgeAvailability();
    }

    private static SpeakerLayoutAvailability BridgeAvailability()
    {
        if (ClassDB.ClassExists(GateClass)) return new(true, null);
        string executable = OS.GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathRooted(executable))
            executable = System.Environment.ProcessPath ?? throw new InvalidDataException("Client executable path is unavailable.");
        string application = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        var directories = new List<string> { Path.Combine(application, "native", "windows-x64", "audio") };
        // An exported PCK has no physical res:// directory. Use the same
        // executable/source distinction as the mature decoder runtime locator.
        string resourcePath = ProjectSettings.GlobalizePath("res://");
        if (!string.IsNullOrWhiteSpace(resourcePath) && Path.IsPathRooted(resourcePath) && Directory.Exists(resourcePath))
        {
            string? project = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(resourcePath)));
            if (project is not null) directories.Add(Path.Combine(project, "native", "San14.Audio.Speakers", "bin", "x64", "Release"));
        }
        string? path = directories.Select(directory => Path.Combine(directory, "San14.Audio.Speakers.gdextension"))
            .FirstOrDefault(File.Exists);
        if (path is null) return new(false, "audio.output.speaker_pair_bridge_missing");
        var status = GDExtensionManager.LoadExtension(path);
        if (status is not (GDExtensionManager.LoadStatus.Ok or GDExtensionManager.LoadStatus.AlreadyLoaded) ||
            !ClassDB.ClassExists(GateClass) || !ClassDB.IsParentClass(GateClass, "AudioEffect"))
            return new(false, "audio.output.speaker_pair_bridge_registration");
        return new(true, null);
    }

    internal static SpeakerLayoutAvailability MatrixAvailability(GameAudioSpeakerLayout layout)
    {
        if (!Enum.IsDefined(layout)) throw new ArgumentOutOfRangeException(nameof(layout));
        if (layout == GameAudioSpeakerLayout.Surround51) return Availability();
        // Stereo application output uses only its first explicit pair even
        // on a wider current device. The gate clears every other actual pair.
        if (!Enum.IsDefined(AudioServer.GetSpeakerMode())) return new(false,"audio.spatial.host_speaker_mode");
        return BridgeAvailability();
    }

    internal static AudioEffect InstallMatrix(int bus, int targetPair, GameAudioSpeakerLayout layout)
    {
        var availability = MatrixAvailability(layout);
        if (!availability.Available) throw new NotSupportedException(availability.UnresolvedOwner);
        int pairs = AudioServer.GetBusChannels(bus);
        if (pairs is < 1 or > 4 || (uint)targetPair >= (uint)pairs)
            throw new InvalidOperationException("audio.spatial.matrix_host_pairs");
        return InstallBound(bus,targetPair,pairs);
    }

    internal static AudioEffectInstance AcquireMatrixInstance(int bus,int targetPair)
    {
        var instance=AudioServer.GetBusEffectInstance(bus,0,targetPair)
            ?? throw new InvalidOperationException("audio.spatial.matrix_instance");
        if(instance.HasMethod("set_matrix"))return instance;
        instance.Dispose();throw new NotSupportedException("audio.spatial.matrix_bridge_version");
    }

    internal static void UpdateMatrix(AudioEffectInstance instance,float leftLeft,float leftRight,
        float rightLeft,float rightRight) =>
        instance.Call("set_matrix",leftLeft,leftRight,rightLeft,rightRight);

    internal static AudioEffect Install(int bus, int targetPair)
    {
        var availability = Availability();
        if (!availability.Available) throw new NotSupportedException(availability.UnresolvedOwner);
        if ((uint)targetPair >= 3 || AudioServer.GetBusChannels(bus) != 3)
            throw new InvalidOperationException("audio.output.speaker_pair_host_changed");
        return InstallBound(bus,targetPair,3);
    }

    private static AudioEffect InstallBound(int bus,int targetPair,int pairs)
    {
        using var created = ClassDB.Instantiate(GateClass);
        var resource = created.AsGodotObject() as AudioEffect
            ?? throw new InvalidOperationException("audio.output.speaker_pair_effect_type");
        try
        {
            resource.Call("set_target_pair", targetPair);
            int effect = AudioServer.GetBusEffectCount(bus);
            AudioServer.AddBusEffect(bus, resource);
            for (int pair = 0; pair < pairs; pair++)
            {
                using var instance = AudioServer.GetBusEffectInstance(bus, effect, pair)
                    ?? throw new InvalidOperationException("audio.output.speaker_pair_instance");
                if (!instance.HasMethod("bind_output_pair"))
                    throw new InvalidOperationException("audio.output.speaker_pair_instance_type");
                instance.Call("bind_output_pair", pair);
            }
            return resource;
        }
        catch { resource.Dispose(); throw; }
    }
}
