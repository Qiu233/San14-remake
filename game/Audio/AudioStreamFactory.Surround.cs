using Godot;
using San14.Data;

public static partial class AudioStreamFactory
{
    internal static AudioClip CreateSurroundRouted(DecodedAudio decoded, AudioLoopPolicy policy, AudioDeviceSpeakerMatrix deviceRoute)
    {
        var wave = decoded.Wave;
        if (wave.Channels != 6 || wave.Pcm16.Length != checked(wave.SampleFrames * 12))
            throw new InvalidDataException("audio.output.surround_pcm_frames");
        bool loop = SourceLoop(decoded, policy);
        var streams = new AudioStreamWav[3];
        AudioClip? clip = null;
        try
        {
            for (int pair = 0; pair < 3; pair++)
            {
                byte[] pcm = new byte[checked(wave.SampleFrames * 4)];
                for (int frame = 0; frame < wave.SampleFrames; frame++)
                    wave.Pcm16.AsSpan(frame * 12 + pair * 4, 4).CopyTo(pcm.AsSpan(frame * 4));
                streams[pair] = SourceWave(decoded, pcm, loop);
            }
            clip = new(streams[0], decoded.Provenance, wave.SampleRate, 6, wave.SampleFrames, loop)
            { OutputChannels = 6, SpeakerPairs = streams, SpeakerDeviceRoute = deviceRoute,
                LoopBegin = loop ? checked((int)decoded.LoopStartSample!.Value) : null,
                LoopEnd = loop ? checked((int)decoded.LoopEndSample!.Value) : null };
            clip.OwnChild(streams[1]); clip.OwnChild(streams[2]);
            return clip;
        }
        catch { if (clip is not null) clip.Dispose(); else foreach (var stream in streams) stream?.Dispose(); throw; }
    }

    internal static AudioClip CreateSynchronousSurroundSpeakerPair(DecodedAudio first,
        AudioCueStaticPlaybackReference firstSource, DecodedAudio second, AudioCueStaticPlaybackReference secondSource,
        AudioDeviceSpeakerMatrix deviceRoute)
    {
        var components = new[] { (Audio: first, Source: firstSource), (Audio: second, Source: secondSource) };
        foreach (var component in components)
        {
            uint mask = component.Source.SourceChannelMask ?? throw new InvalidDataException("audio.output.source_speaker_mask");
            if (mask == 0 || (mask & ~0x3Fu) != 0 || System.Numerics.BitOperations.PopCount(mask) != component.Audio.Wave.Channels)
                throw new InvalidDataException("audio.output.source_speaker_lanes");
            _ = SourceLoop(component.Audio, AudioLoopPolicy.SourceIfPresent);
        }
        var pairs = Enumerable.Range(0, 3).Select(_ => new AudioStreamSynchronized { StreamCount = 2 }).ToArray();
        int frames = checked((int)Math.Ceiling(components.Max(component =>
            (double)component.Audio.Wave.SampleFrames / component.Audio.Wave.SampleRate) * first.Wave.SampleRate));
        var clip = new AudioClip(pairs[0], first.Provenance, first.Wave.SampleRate,
            first.Wave.Channels + second.Wave.Channels, frames, components.Any(component => component.Audio.LoopStartSample is not null))
            { OutputChannels = 6, SpeakerPairs = pairs, SpeakerDeviceRoute = deviceRoute };
        clip.OwnChild(pairs[1]); clip.OwnChild(pairs[2]);
        try
        {
            for (int pair = 0; pair < 3; pair++)
                for (int component = 0; component < 2; component++)
                {
                    var (audio, source) = components[component];
                    var wave = audio.Wave;
                    byte[] pcm = new byte[checked(wave.SampleFrames * 4)];
                    uint mask = source.SourceChannelMask!.Value;
                    for (int output = 0; output < 2; output++)
                    {
                        int speaker = pair * 2 + output;
                        if ((mask & (1u << speaker)) == 0) continue;
                        int input = System.Numerics.BitOperations.PopCount(mask & ((1u << speaker) - 1));
                        for (int frame = 0; frame < wave.SampleFrames; frame++)
                            wave.Pcm16.AsSpan((frame * wave.Channels + input) * 2, 2).CopyTo(pcm.AsSpan(frame * 4 + output * 2));
                    }
                    var stream = SourceWave(audio, pcm, audio.LoopStartSample is not null);
                    clip.OwnChild(stream); pairs[pair].SetSyncStream(component, stream);
                    pairs[pair].SetSyncStreamVolume(component, source.Leaf.LinearGain == 0 ? float.NegativeInfinity : Mathf.LinearToDb(source.Leaf.LinearGain));
                }
            return clip;
        }
        catch { clip.Dispose(); throw; }
    }

    private static bool SourceLoop(DecodedAudio audio, AudioLoopPolicy policy)
    {
        if (!Enum.IsDefined(policy) || audio.Wave.SampleRate is < 1 or > 192000 ||
            audio.Wave.SampleFrames <= 0 || audio.Wave.Pcm16.Length != checked(audio.Wave.SampleFrames * audio.Wave.Channels * 2))
            throw new InvalidDataException("audio.output.source_pcm");
        bool loop = policy == AudioLoopPolicy.RequireSource || policy == AudioLoopPolicy.SourceIfPresent &&
            (audio.LoopStartSample is not null || audio.LoopEndSample is not null);
        if (loop && (audio.LoopStartSample is null || audio.LoopEndSample is null || audio.LoopStartSample < 0 ||
            audio.LoopStartSample >= audio.LoopEndSample || audio.LoopEndSample > audio.Wave.SampleFrames))
            throw new InvalidDataException("audio.output.source_loop");
        return loop;
    }
    private static AudioStreamWav SourceWave(DecodedAudio audio, byte[] pcm, bool loop) => new()
    {
        Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = audio.Wave.SampleRate, Stereo = true, Data = pcm,
        LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
        LoopBegin = loop ? checked((int)audio.LoopStartSample!.Value) : 0,
        LoopEnd = loop ? checked((int)audio.LoopEndSample!.Value) : 0
    };
}
