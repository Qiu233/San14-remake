using Godot;
using San14.Data;

/// <summary>Current host configuration, independently of source speaker order.</summary>
internal sealed record OriginalAudioDeviceProfile(float StereoLfeGain, GameAudioSpeakerLayout Layout = GameAudioSpeakerLayout.Stereo)
{
    // The actual Windows application setup leaves the stereo LFE contribution
    // disabled. This is independent of the selected sample's six speaker lanes.
    internal static OriginalAudioDeviceProfile WindowsStereo { get; } = new(0);
}

/// <summary>The actually used single-send music and UI routes. The original
/// fresh basic processors pass PCM; independent meters retain no audible source.
/// A current explicit processor update requires its own reached consumer.</summary>
internal sealed partial class OriginalAudioOutputs : IDisposable
{
    private readonly Dictionary<int, string> buses = [];
    private readonly List<string> creationOrder = [];
    private readonly AudioServer.SpeakerMode speakerMode;
    private readonly string device;
    private readonly int mixRate;
    internal string MusicBus { get; }
    internal string EffectsBus { get; }
    internal StereoAudioRoute MusicStereoRoute { get; }
    // Both six-lane master paths use the same actual Windows device matrix.
    internal StereoAudioRoute EffectsStereoRoute => MusicStereoRoute;
    internal AudioMusicOutputGraph MusicGraph { get; }
    internal AudioMusicOutputGraph EffectsGraph { get; }
    internal GameAudioSpeakerLayout SpeakerLayout { get; private set; }
    private readonly AudioDeviceSpeakerMatrix? surroundDeviceRoute;
    private readonly string? surroundDeviceError;
    internal AudioDeviceSpeakerMatrix SurroundDeviceRoute => surroundDeviceRoute ??
        throw new NotSupportedException(surroundDeviceError ?? "audio.output.device_matrix");

    internal OriginalAudioOutputs(ulong owner, AudioMusicOutputGraph music, AudioMusicOutputGraph effects,
        OriginalAudioDeviceProfile profile)
    {
        if (!ReferenceEquals(music.Source, effects.Source) || music.MixChannels != 6 || effects.MixChannels != 6)
            throw new NotSupportedException("Selected original output routes must share their six-channel source graph.");
        if (profile.StereoLfeGain is not (0 or 1))
            throw new NotSupportedException("Original stereo output needs its actual LFE configuration.");
        speakerMode = AudioServer.GetSpeakerMode(); device = AudioServer.OutputDevice;
        mixRate = checked((int)AudioServer.GetMixRate());
        if (!Enum.IsDefined(speakerMode)) throw new NotSupportedException("audio.output.host_speaker_layout");
        (surroundDeviceRoute,surroundDeviceError)=CaptureSurroundDeviceRoute(speakerMode,device,profile.StereoLfeGain);
        SetSpeakerLayout(profile.Layout);
        MusicGraph = music; EffectsGraph = effects;
        var musicPath = ReadPath(music, 0xCDB1D87E);
        var effectsPath = ReadPath(effects, 0xF4E09144);
        MusicStereoRoute = StereoAudioRoute.WindowsDevice(profile.StereoLfeGain);
        // The actual resource routes have unity classification send amplitudes;
        // current category percentages are owned by the existing playback voice.
        if (music.Classification.Routes[0].Gain != 1 || effects.Classification.Routes[0].Gain != 1)
            throw new NotSupportedException("Nonunity classification sends need their separate source gain stage.");
        AudioServer.Lock();
        try
        {
            foreach (int index in musicPath.AsEnumerable().Reverse().Concat(effectsPath.AsEnumerable().Reverse()).Distinct())
            {
                var node = music.Outputs[index];
                string name = $"OriginalAudio{owner}_{node.Key:X8}";
                if (AudioServer.GetBusIndex(name) >= 0) throw new InvalidOperationException("Original audio output already exists.");
                int bus = AudioServer.BusCount;
                AudioServer.AddBus(); AudioServer.SetBusName(bus, name);
                buses.Add(index, name); creationOrder.Add(name);
                AudioServer.SetBusVolumeLinear(bus, node.StaticGain);
                if (node.DownstreamIndices.Count == 1) AudioServer.SetBusSend(bus, buses[node.DownstreamIndices[0]]);
            }
            MusicBus = buses[musicPath[0]]; EffectsBus = buses[effectsPath[0]];
            InitializeSpatialOutputs(owner);
        }
        catch { Dispose(); throw; }
        finally { AudioServer.Unlock(); }
    }

    private static IReadOnlyList<int> ReadPath(AudioMusicOutputGraph graph, uint categoryKey)
    {
        if (graph.Classification.Routes.Count != 1 || graph.Classification.Routes[0].Kind != 0 ||
            !graph.Classification.GainKeys.SequenceEqual(new[] { categoryKey }))
            throw new NotSupportedException("Original audio requires its actual single output and category gain binding.");
        uint key = graph.Classification.Routes[0].OutputKey;
        var starts = graph.Outputs.Where(node => node.Key == key).ToArray();
        if (starts.Length != 1) throw new InvalidDataException("Original audio route has no unique output.");
        var result = new List<int>(); int index = starts[0].Index;
        while (true)
        {
            if ((uint)index >= graph.Outputs.Count || result.Contains(index))
                throw new InvalidDataException("Original audio send graph has a cycle or invalid target.");
            var node = graph.Outputs[index]; result.Add(index);
            if (node.NodeKind is not (0 or 2 or 5 or 7) || node.Variant > 1 ||
                (node.Flags & ~1u) != 0 || node.GainKeys.Count != 0 || node.DownstreamIndices.Count > 1)
                throw new NotSupportedException("Original audio route contains an unimplemented processor or send.");
            if (node.DownstreamIndices.Count == 0)
            {
                if (node.NodeKind != 7) throw new NotSupportedException("Original audio route does not terminate at its master.");
                return result;
            }
            if (node.NodeKind == 7) throw new NotSupportedException("Original master unexpectedly sends to another source node.");
            index = node.DownstreamIndices[0];
        }
    }

    internal void ValidateHost()
    {
        if (AudioServer.GetSpeakerMode() != speakerMode || AudioServer.OutputDevice != device ||
            AudioServer.GetMixRate() != mixRate || creationOrder.Any(name => AudioServer.GetBusIndex(name) < 0))
            throw new InvalidOperationException("Original audio output configuration changed; its routes must be rebound.");
    }

    internal void SetSpeakerLayout(GameAudioSpeakerLayout layout)
    {
        if (!Enum.IsDefined(layout)) throw new ArgumentOutOfRangeException(nameof(layout));
        var supported=QuerySpeakerLayoutAvailability(layout);
        if(!supported.Available)throw new NotSupportedException(supported.UnresolvedOwner);
        SpeakerLayout = layout;
    }

    internal SpeakerLayoutAvailability QuerySpeakerLayoutAvailability(GameAudioSpeakerLayout layout)
    {
        if(!Enum.IsDefined(layout))throw new ArgumentOutOfRangeException(nameof(layout));
        if(layout==GameAudioSpeakerLayout.Stereo)return new(true,null);
        if(surroundDeviceRoute is null)return new(false,surroundDeviceError);
        return OriginalSpeakerPairGate.Availability();
    }

    internal static SpeakerLayoutAvailability QuerySurroundAvailability()
    {
        var bridge=OriginalSpeakerPairGate.Availability();
        if(!bridge.Available)return bridge;
        var route=CaptureSurroundDeviceRoute(AudioServer.GetSpeakerMode(),AudioServer.OutputDevice,0);
        return new(route.Route is not null,route.Error);
    }
    private static (AudioDeviceSpeakerMatrix? Route,string? Error) CaptureSurroundDeviceRoute(
        AudioServer.SpeakerMode mode,string deviceName,float lfeGain)
    {
        int channels=mode switch {AudioServer.SpeakerMode.Surround31=>4,
            AudioServer.SpeakerMode.Surround51=>6,AudioServer.SpeakerMode.Surround71=>8,_=>0};
        if(channels==0)return (null,"audio.output.surround51_requires_multichannel_host");
        if(!WindowsAudioDeviceFormat.TryRead(deviceName,out var format,out var error))return (null,error);
        // Godot rounds odd physical counts upward and averages the last pair.
        // That separate converter cannot stand in for an exact 4/6/8 device.
        if(format!.Channels!=channels)return (null,"audio.output.physical_host_channels");
        try{return (AudioDeviceSpeakerMatrix.PrepareWindows(6,channels,format.NativeChannelMask,lfeGain),null);}
        catch(Exception failure) when(failure is InvalidDataException or NotSupportedException)
        {return (null,failure.Message);}
    }

    public void Dispose()
    {
        DisposeSpatialOutputs();
        foreach (string name in creationOrder.AsEnumerable().Reverse())
        {
            int bus = AudioServer.GetBusIndex(name);
            if (bus >= 0) AudioServer.RemoveBus(bus);
        }
        creationOrder.Clear(); buses.Clear();
    }
}
