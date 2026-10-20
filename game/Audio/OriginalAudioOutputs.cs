using Godot;
using San14.Data;

/// <summary>Current host configuration, independently of source speaker order.</summary>
internal sealed record OriginalAudioDeviceProfile(float StereoLfeGain)
{
    // The actual Windows application setup leaves the stereo LFE contribution
    // disabled. This is independent of the selected sample's six speaker lanes.
    internal static OriginalAudioDeviceProfile WindowsStereo { get; } = new(0);
}

/// <summary>The actually used single-send music and UI routes. The original
/// fresh basic processors pass PCM; independent meters retain no audible source.
/// A current explicit processor update requires its own reached consumer.</summary>
internal sealed class OriginalAudioOutputs : IDisposable
{
    private readonly Dictionary<int, string> buses = [];
    private readonly List<string> creationOrder = [];
    private readonly AudioServer.SpeakerMode speakerMode;
    private readonly string device;
    private readonly int mixRate;
    internal string MusicBus { get; }
    internal string EffectsBus { get; }
    internal StereoAudioRoute MusicStereoRoute { get; }
    internal AudioMusicOutputGraph MusicGraph { get; }
    internal AudioMusicOutputGraph EffectsGraph { get; }

    internal OriginalAudioOutputs(ulong owner, AudioMusicOutputGraph music, AudioMusicOutputGraph effects,
        OriginalAudioDeviceProfile profile)
    {
        if (!ReferenceEquals(music.Source, effects.Source) || music.MixChannels != 6 || effects.MixChannels != 6)
            throw new NotSupportedException("Selected original output routes must share their six-channel source graph.");
        if (profile.StereoLfeGain is not (0 or 1))
            throw new NotSupportedException("Original stereo output needs its actual LFE configuration.");
        speakerMode = AudioServer.GetSpeakerMode(); device = AudioServer.OutputDevice;
        mixRate = checked((int)AudioServer.GetMixRate());
        if (speakerMode != AudioServer.SpeakerMode.ModeStereo)
            throw new NotSupportedException("Original multichannel music has no bound route for this host speaker layout yet.");
        MusicGraph = music; EffectsGraph = effects;
        var musicPath = ReadPath(music, 0xCDB1D87E);
        var effectsPath = ReadPath(effects, 0xF4E09144);
        float lfe = .7071f * profile.StereoLfeGain;
        MusicStereoRoute = new([1, 0, .7071f, lfe, .7071f, 0], [0, 1, .7071f, lfe, 0, .7071f]);
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

    public void Dispose()
    {
        foreach (string name in creationOrder.AsEnumerable().Reverse())
        {
            int bus = AudioServer.GetBusIndex(name);
            if (bus >= 0) AudioServer.RemoveBus(bus);
        }
        creationOrder.Clear(); buses.Clear();
    }
}
