using San14.Data;

/// <summary>Immutable source PCM stems prepared without any Godot object.
/// The process audio service retains these buffers; each player gets its own
/// playback from the corresponding immutable stream resources.</summary>
internal sealed record PreparedStereoAudio(DecodedAudio Source, bool UsesSourceLoop,
    int? LoopBegin, int? LoopEnd, IReadOnlyList<PreparedStereoAudio.Term> Terms)
{
    internal sealed record Term(float Gain, byte[] Pcm16);

    internal static PreparedStereoAudio Prepare(DecodedAudio decoded, StereoAudioRoute route,
        AudioLoopPolicy loopPolicy)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        ArgumentNullException.ThrowIfNull(route);
        if (!Enum.IsDefined(loopPolicy)) throw new ArgumentOutOfRangeException(nameof(loopPolicy));
        var wave = decoded.Wave;
        if (wave.Channels != 6 || wave.SampleRate is < 1 or > 192000 ||
            wave.Pcm16.Length == 0 || wave.Pcm16.Length % 12 != 0)
            throw new InvalidDataException("Explicit six-channel routing requires complete source PCM16 frames.");
        bool loop = loopPolicy == AudioLoopPolicy.RequireSource ||
            (loopPolicy == AudioLoopPolicy.SourceIfPresent &&
             (decoded.LoopStartSample is not null || decoded.LoopEndSample is not null));
        if (loop && (decoded.LoopStartSample is null || decoded.LoopEndSample is null ||
            decoded.LoopStartSample < 0 || decoded.LoopStartSample >= decoded.LoopEndSample ||
            decoded.LoopEndSample > wave.SampleFrames))
            throw new InvalidDataException("Selected audio has no valid original loop interval.");
        var terms = new List<(int Input, int Output, float Gain)>();
        for (int output = 0; output < 2; output++)
            for (int input = 0; input < 6; input++)
                if (route.Gain(input, output) is > 0 and var gain) terms.Add((input, output, gain));
        // Preserve the selected source's duration and loop clock for a zero matrix.
        if (terms.Count == 0) terms.Add((0, 0, 0));
        var prepared = new Term[terms.Count];
        for (int term = 0; term < terms.Count; term++)
        {
            var (input, output, gain) = terms[term];
            var pcm = new byte[checked(wave.SampleFrames * 4)];
            if (gain != 0)
                for (int frame = 0; frame < wave.SampleFrames; frame++)
                {
                    int source = frame * 12 + input * 2, target = frame * 4 + output * 2;
                    pcm[target] = wave.Pcm16[source]; pcm[target + 1] = wave.Pcm16[source + 1];
                }
            prepared[term] = new(gain, pcm);
        }
        return new(decoded, loop,
            loop ? checked((int)decoded.LoopStartSample!.Value) : null,
            loop ? checked((int)decoded.LoopEndSample!.Value) : null, prepared);
    }
}

internal enum ResidentStereoOutputProfile { WindowsWithoutLfe, WindowsWithLfe }
