using System.Buffers.Binary;

namespace San14.Data;

public sealed record AudioCueGraphNode(int Offset, uint Opcode, int DeclaredHeaderBytes,
    uint Flags, IReadOnlyList<int> ChildOffsets, uint? SoundId);
public sealed record AudioCueGraph(AudioNamedCue Cue, LoadedRdbAsset Source,
    IReadOnlyList<int> RootOffsets, IReadOnlyList<AudioCueGraphNode> Nodes);
public sealed record AudioCueSoundReference(uint CueId, string CueName, uint SoundId,
    int CueSectionOffset, int LeafOffset, LoadedRdbAsset Source);

/// <summary>
/// Parses the original direct group/sample-reference subset of KTSR cue graphs. Original
/// opcode dispatch and relative pointer reads are confirmed in both installed CE executables.
/// Only group opcode 51D25082 with selector bits 16..19 zero and leaf opcode 6282CA29 are
/// supported. Stateful/random selectors, gain, scheduling and current playback are not inferred.
/// </summary>
public static class AudioCueGraphResources
{
    public const uint DirectGroupOpcode = 0x51D25082;
    public const uint SoundReferenceOpcode = 0x6282CA29;

    public static AudioCueGraph Read(AudioSelectionBank bank, string cueName)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentException.ThrowIfNullOrEmpty(cueName);
        AudioNamedCue[] cues = bank.Cues.Where(c => c.Name == cueName).ToArray();
        if (cues.Length != 1) throw new InvalidDataException("A cue graph name must identify exactly one original cue section.");
        AudioNamedCue cue = cues[0];
        byte[] b = bank.Source.Bytes;
        int start = cue.SectionOffset, end = checked(start + cue.SectionBytes);
        var nodes = new Dictionary<int, AudioCueGraphNode>();
        var visiting = new HashSet<int>();
        uint U32(int at)
        {
            Range(at, 4);
            return BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at, 4));
        }
        void Range(int at, int size)
        {
            if (size < 0 || at < start || at > end || size > end - at)
                throw new InvalidDataException("Audio cue graph pointer extends outside its original cue section.");
        }
        int Offset(int baseOffset, uint relative)
        {
            long at = (long)baseOffset + relative;
            if (at > int.MaxValue) throw new InvalidDataException("Audio cue relative pointer exceeds int32.");
            Range((int)at, 4);
            return (int)at;
        }
        IReadOnlyList<int> Children(int baseOffset, int countField, int pointerField)
        {
            uint count = U32(baseOffset + countField);
            if (count > 1024) throw new InvalidDataException("Audio cue child count exceeds its bounded graph budget.");
            if (count == 0) return Array.Empty<int>();
            int table = Offset(baseOffset, U32(baseOffset + pointerField));
            Range(table, checked((int)count * 4));
            var children = new int[count];
            for (int i = 0; i < children.Length; i++) children[i] = Offset(baseOffset, U32(table + 4 * i));
            return Array.AsReadOnly(children);
        }
        void Visit(int offset, int depth)
        {
            if (depth > 64 || visiting.Contains(offset))
                throw new InvalidDataException("Audio cue graph is cyclic or exceeds the recursion budget.");
            if (nodes.ContainsKey(offset)) return;
            if (nodes.Count >= 1024) throw new InvalidDataException("Audio cue graph exceeds the node budget.");
            Range(offset, 24);
            uint opcode = U32(offset), rawSize = U32(offset + 4);
            if (rawSize is < 24 or > int.MaxValue)
                throw new InvalidDataException("Audio cue node header length is invalid.");
            // This length covers the node header, not its recursively referenced children.
            Range(offset, (int)rawSize);
            uint flags = U32(offset + 12);
            visiting.Add(offset);
            if (opcode == DirectGroupOpcode)
            {
                if ((flags & 0xF0000) != 0)
                    throw new NotSupportedException("Audio cue group uses an unimplemented stateful/random selector.");
                IReadOnlyList<int> children = Children(offset, 16, 20);
                foreach (int child in children) Visit(child, depth + 1);
                if (nodes.Count >= 1024) throw new InvalidDataException("Audio cue graph exceeds the node budget.");
                nodes.Add(offset, new(offset, opcode, (int)rawSize, flags, children, null));
            }
            else if (opcode == SoundReferenceOpcode)
                nodes.Add(offset, new(offset, opcode, (int)rawSize, flags, Array.Empty<int>(), U32(offset + 20)));
            else
                throw new NotSupportedException($"Audio cue opcode {opcode:X8} is outside the verified direct graph subset.");
            visiting.Remove(offset);
        }
        Range(start, 48);
        IReadOnlyList<int> roots = Children(start, 32, 36);
        foreach (int root in roots) Visit(root, 0);
        return new(cue, bank.Source, roots, Array.AsReadOnly(nodes.Values.OrderBy(n => n.Offset).ToArray()));
    }

    /// <summary>Returns the SoundID read by a single static reference chain. It is not a claim
    /// that the original runtime's registry, filters or mixer actually played this sample.</summary>
    public static AudioCueSoundReference ReadSingleSoundReference(AudioSelectionBank bank, string cueName)
    {
        AudioCueGraph graph = Read(bank, cueName);
        if (graph.RootOffsets.Count != 1)
            throw new NotSupportedException("Multiple cue roots require an explicit playback graph.");
        var nodes = graph.Nodes.ToDictionary(n => n.Offset);
        AudioCueGraphNode node = nodes[graph.RootOffsets[0]];
        while (node.SoundId is null)
        {
            if (node.ChildOffsets.Count != 1)
                throw new NotSupportedException("A cue with multiple/empty group children has no single static sound reference.");
            node = nodes[node.ChildOffsets[0]];
        }
        return new(graph.Cue.CueId, cueName, node.SoundId.Value, graph.Cue.SectionOffset, node.Offset, graph.Source);
    }

    /// <summary>
    /// Reads one static 562FC4B9/A5170472 chain for the non-spatial A0 consumer.
    /// No manager admission, decoder readiness or spatial-request choice is inferred here.
    /// Static additive rate and authored group attack/release curves are retained; delays, duration limits and dynamic fields are rejected.
    /// Category keys remain explicit: resolve them against the caller's current CGRS asset.
    /// </summary>
    public static AudioCueStaticPlaybackReference ReadSingleStaticNonSpatialReference(
        AudioSelectionBank bank, string cueName)
        => ReadSingleStaticReference(bank, cueName, false);

    public static AudioCueStaticPlaybackReference ReadSingleStaticNonSpatialReference(
        AudioSelectionBank bank, string cueName, AudioSampleStorage storage)
        => ReadSingleStaticReference(bank, cueName, false, storage);

    /// <summary>One ordinary static sound, or two authored synchronous sample
    /// components under 3244E2DF. Each retains its own actual speaker mask;
    /// a pair is one playback object, never two manager slots.</summary>
    public static IReadOnlyList<AudioCueStaticPlaybackReference> ReadStaticNonSpatialReferences(
        AudioSelectionBank bank, string cueName)
    {
        var first = ReadSingleStaticReference(bank, cueName, false, stereoChannel: 0);
        return first.SynchronousComponentIndex is null ? new[] { first } :
            new[] { first, ReadSingleStaticReference(bank, cueName, false, stereoChannel: 1) };
    }

    /// <summary>Retains the actual spatial mix/filter parameter block. It does
    /// not interpret middleware direction/attenuation fields as a Godot curve.</summary>
    public static AudioCueStaticPlaybackReference ReadSingleStaticSpatialReference(
        AudioSelectionBank bank, string cueName)
        => ReadSingleStaticReference(bank, cueName, true);

    public static AudioCueStaticPlaybackReference ReadSingleStaticSpatialReference(
        AudioSelectionBank bank, string cueName, AudioSampleStorage storage)
        => ReadSingleStaticReference(bank, cueName, true, storage);

    private static AudioCueStaticPlaybackReference ReadSingleStaticReference(
        AudioSelectionBank bank, string cueName, bool spatial, AudioSampleStorage? storage = null,
        int? stereoChannel = null)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentException.ThrowIfNullOrEmpty(cueName);
        AudioNamedCue[] cues = bank.Cues.Where(c => c.Name == cueName).ToArray();
        if (cues.Length != 1)
            throw new InvalidDataException("A static playback cue name must identify exactly one section.");
        AudioNamedCue cue = cues[0];
        byte[] bytes = bank.Source.Bytes;
        int start = cue.SectionOffset;
        long end64 = (long)start + cue.SectionBytes;
        if (start < 0 || cue.SectionBytes < 48 || end64 > bytes.Length)
            throw new InvalidDataException("Static playback cue section is outside its source.");
        int end = (int)end64;
        void Range(int at, int count)
        {
            if (count < 0 || at < start || at > end || count > end - at)
                throw new InvalidDataException("Static playback field extends outside its cue section.");
        }
        uint U32(int at)
        {
            Range(at, 4);
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        }
        float Scalar(int at)
        {
            float value = BitConverter.Int32BitsToSingle(unchecked((int)U32(at)));
            if (!float.IsFinite(value)) throw new InvalidDataException("Static playback scalar is not finite.");
            return value;
        }
        int Relative(int at, uint offset)
        {
            long value = (long)at + offset;
            if (value > int.MaxValue)
                throw new InvalidDataException("Static playback pointer exceeds int32.");
            Range((int)value, 4);
            return (int)value;
        }
        int SingleChild(int at, int countField, int pointerField)
        {
            if (U32(at + countField) != 1)
                throw new NotSupportedException("Static playback requires exactly one root and one child.");
            int table = Relative(at, U32(at + pointerField));
            return Relative(at, U32(table));
        }
        void Header(int at, uint opcode, int minimum, uint allowedFlags = 0)
        {
            Range(at, minimum);
            if (U32(at) != opcode)
                throw new NotSupportedException("Static playback requires a parameter group and a parameter sound reference.");
            uint size = U32(at + 4);
            if (size < minimum || size > int.MaxValue)
                throw new InvalidDataException("Static playback node header length is invalid.");
            Range(at, (int)size);
            if ((U32(at + 12) & ~allowedFlags) != 0)
                throw new NotSupportedException("Static playback does not implement node flags/functions/selectors.");
        }
        void Neutral(float value, float expected, string field)
        {
            if (value != expected)
                throw new NotSupportedException($"Static non-spatial playback does not implement nonneutral {field}.");
        }

        if (U32(start) != 0x368C88BD || U32(start + 8) != cue.CueId || U32(start + 4) != cue.SectionBytes)
            throw new InvalidDataException("Static playback cue metadata differs from its section.");
        if ((U32(start + 12) & 0xF0000) != 0)
            throw new NotSupportedException("Static playback does not implement cue selectors.");
        int root = SingleChild(start, 32, 36);
        Header(root, 0x562FC4B9, 0x58, 0xF00000);
        int leaf = SingleChild(root, 16, 20);
        int parameters = leaf;
        int? componentIndex = null;
        bool requireRegisteredMemorySample = false;
        float componentGain = 1;
        uint soundId;
        if (U32(leaf) == 0x3244E2DF && stereoChannel is int channel)
        {
            // 125E7D0/1260AD0 and 1273D90 start all children at the same
            // cursor. Each component retains its real source speaker lanes.
            Header(parameters, 0x3244E2DF, 0x4C);
            if (U32(parameters + 0x14) != 2 || U32(parameters + 0x44) != 0 ||
                U32(parameters + 0x48) != 0 || U32(parameters + 0x3C) != 0 ||
                U32(parameters + 0x40) != 0)
                throw new NotSupportedException("Static stereo sound requires two children without curves, LFE supplement or seek.");
            int table = Relative(parameters, U32(parameters + 0x18));
            Range(table, 8);
            leaf = Relative(parameters, U32(table + channel * 4));
            Header(leaf, 0x308E2D83, 0x28);
            if (leaf < parameters + (long)U32(parameters + 4) ||
                U32(leaf + 0x10) != 0 || U32(leaf + 0x18) != 0 ||
                U32(leaf + 0x20) != 0 || U32(leaf + 0x24) != 0)
                throw new NotSupportedException("Static stereo component has an unsupported classification, routing or parameter override.");
            componentGain = Scalar(leaf + 0x1C);
            soundId = U32(leaf + 0x14);
            componentIndex = channel;
        }
        else
        {
            Header(leaf, 0xA5170472, 0x44, 0x10000);
            // This flag selects a sample registry entry carrying flag2. It
            // does not select a looping policy; that belongs to the sample header.
            requireRegisteredMemorySample = (U32(leaf + 12) & 0x10000) != 0;
            if (U32(leaf + 0x3C) != 0 || U32(leaf + 0x40) != 0)
                throw new NotSupportedException("Static playback does not implement parameter curves.");
            soundId = U32(leaf + 0x14);
        }
        if (leaf < root + (long)U32(root + 4))
            throw new InvalidDataException("Static sound reference overlaps its group header.");
        if (U32(root + 0x50) != 0 || U32(root + 0x54) != 0)
            throw new NotSupportedException("Static playback does not implement parameter curves.");
        if (U32(parameters + 0x10) != 0)
            throw new NotSupportedException("Static playback does not implement a leaf classification override.");

        var group = new AudioCueStaticGroupParameters(root, U32(root + 0x18),
            Scalar(root + 0x1C), Scalar(root + 0x20), Scalar(root + 0x24),
            Scalar(root + 0x28), Scalar(root + 0x2C), Scalar(root + 0x30), Scalar(root + 0x34),
            new(Scalar(root + 0x38), Scalar(root + 0x3C), Scalar(root + 0x40),
                Scalar(root + 0x44), U32(root + 0x48), Scalar(root + 0x4C)))
        {
            AttackCurve = (AudioCueEnvelopeCurve)((U32(root + 0xC) >> 20) & 3),
            ReleaseCurve = (AudioCueEnvelopeCurve)((U32(root + 0xC) >> 22) & 3)
        };
        var sound = new AudioCueStaticSoundParameters(leaf, soundId, U32(parameters + 0x10),
            Scalar(parameters + 0x1C), Scalar(parameters + 0x20) * componentGain, Scalar(parameters + 0x24),
            Scalar(parameters + 0x28), Scalar(parameters + 0x2C), Scalar(parameters + 0x30),
            Scalar(parameters + 0x34), Scalar(parameters + 0x38));
        if (group.LinearGain < 0 || sound.LinearGain < 0 || group.StopReleaseSeconds < 0 || group.StartEnvelopeSeconds < 0)
            throw new InvalidDataException("Static playback gain or release duration is negative.");
        Neutral(group.MixMultiplier1, 1, "group mix multiplier1");
        // Zero side-route input is used by the two UI feedback cues; one
        // preserves the existing direct-output 3F consumer. Other routing
        // values require an explicit auxiliary-output mixer.
        if (!spatial && group.MixMultiplier2 is not (0 or 1))
            throw new NotSupportedException("Static playback requires zero or unity side-route input.");
        if (spatial && group.MixMultiplier2 < 0)
            throw new InvalidDataException("Static spatial playback side-route gain is negative.");
        Neutral(group.InitialDelaySeconds, 0, "group delay");
        AudioCueEnvelope.DurationTicks(group.StartEnvelopeSeconds);
        AudioCueEnvelope.DurationTicks(group.StopReleaseSeconds);
        Neutral(sound.MaximumDurationSeconds, 0, "sound maximum duration");
        Neutral(sound.MixMultiplier, 1, "sound mix multiplier");
        Neutral(sound.InitialDelaySeconds, 0, "sound delay");
        Neutral(sound.EnvelopeScalar30, 0, "sound envelope scalar30");
        Neutral(sound.EnvelopeScalar34, 0, "sound envelope scalar34");
        Neutral(sound.EnvelopeScalar38, 0, "sound envelope scalar38");
        float gain = group.LinearGain * sound.LinearGain;
        if (!float.IsFinite(gain)) throw new InvalidDataException("Static playback combined gain is not finite.");
        // The non-spatial setter adds the group and leaf values, resets
        // the rate multiplier to one, and the player consumes exp2(sum).
        float rate = MathF.Pow(2, group.AdditiveRate + sound.AdditiveRate);
        if (!float.IsFinite(rate) || rate <= 0)
            throw new NotSupportedException("Static playback rate exceeds the positive finite player range.");

        AudioNamedSample[] samples = bank.Samples.Where(s => s.SampleId == sound.SoundId &&
            (storage is null || ((s.Flags & 0x10000) != 0) == (storage == AudioSampleStorage.External))).ToArray();
        if (samples.Length != 1 || samples[0].Headers.Count != 1)
            throw new NotSupportedException("Static playback sound must identify exactly one sample and header.");
        AudioSampleHeader header = samples[0].Headers[0];
        if (requireRegisteredMemorySample && (samples[0].Flags & 2) == 0)
            throw new InvalidDataException("Audio cue requires a registered memory sample with its actual flag2.");
        if (componentIndex is not null && (header.NativeChannelMask == 0 ||
            System.Numerics.BitOperations.PopCount(header.NativeChannelMask) != header.Channels ||
            (header.NativeChannelMask & ~0x3Fu) != 0))
            throw new NotSupportedException("Synchronous sound components require explicit source speaker lanes.");
        if (header.TypeId is not (0xE96FD86A or 0x2EB6CA6F or 0x7D43D038))
            throw new NotSupportedException("Static playback sample metadata format is unsupported.");
        if (header.Channels <= 0 || header.SampleRate <= 0 || header.DeclaredSamples <= 0 ||
            header.LoopStartSample < -1 || header.LoopStartSample >= header.DeclaredSamples)
            throw new InvalidDataException("Static playback sample timing is invalid.");
        return new(new(cue.CueId, cueName, sound.SoundId, start, leaf, bank.Source), group, sound,
            new(samples[0].SectionOffset, header.TypeId, header.IsExternal, header.Channels,
                header.SampleRate, header.DeclaredSamples, header.LoopStartSample), gain, rate)
            { SourceChannelMask = header.NativeChannelMask, SynchronousComponentIndex = componentIndex };
    }

    /// <summary>Reads the ordered gain keys of one classification in an explicit current CGRS
    /// asset. This is not a gain registry or a category-name guess. Overrides use their real bytes.</summary>
    public static IReadOnlyList<uint> ReadStaticGainCategoryIds(LoadedRdbAsset source, uint classificationId)
    {
        ArgumentNullException.ThrowIfNull(source);
        byte[] bytes = source.Bytes;
        uint U32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        if (bytes.Length < 80 || !bytes.AsSpan(0, 8).SequenceEqual("CGRS\0\0\0\0"u8) ||
            BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(8, 8)) != (ulong)bytes.Length ||
            !bytes.AsSpan(16, 4).SequenceEqual("KTSR"u8) || U32(20) != 0x9EE638C6 ||
            U32(40) != bytes.Length - 16 || U32(44) != bytes.Length - 16)
            throw new InvalidDataException("Static playback classification container is invalid.");
        uint[]? found = null;
        int section = 80, records = 0;
        while (section < bytes.Length)
        {
            if (++records > 65536 || bytes.Length - section < 8)
                throw new InvalidDataException("Static playback classification section is incomplete.");
            uint size = U32(section + 4);
            if (size < 8 || size > bytes.Length - section)
                throw new InvalidDataException("Static playback classification section length is invalid.");
            if (U32(section) == 0xF96787ED)
            {
                if (size < 0x24)
                    throw new InvalidDataException("Static playback classification header is incomplete.");
                if (U32(section + 8) == classificationId)
                {
                    if (found is not null)
                        throw new InvalidDataException("Static playback classification key is ambiguous.");
                    uint count = U32(section + 0x1C), relative = U32(section + 0x20);
                    if (count > 1024 || (count != 0 && (relative < 0x24 || relative > size || count > (size - relative) / 4)))
                        throw new InvalidDataException("Static playback classification gain list is invalid.");
                    found = new uint[count];
                    for (int i = 0; i < found.Length; i++)
                        found[i] = U32(checked(section + (int)relative + i * 4));
                }
            }
            section += (int)size;
        }
        if (found is null) throw new InvalidDataException("Static playback classification key is missing.");
        return Array.AsReadOnly(found);
    }

}

/// <summary>Original filter/direction values are retained. The non-spatial A0 consumer's
/// coefficient gate skips this filter; these values must not be ignored by a spatial consumer.</summary>
public sealed record AudioCueNonSpatialFilterSource(float Direction, float Spread,
    float Parameter40, float Parameter44, uint Parameter48, float Parameter4C);
public sealed record AudioCueStaticGroupParameters(int Offset, uint ClassificationId,
    float LinearGain, float MixMultiplier1, float MixMultiplier2, float AdditiveRate,
    float InitialDelaySeconds, float StartEnvelopeSeconds, float StopReleaseSeconds,
    AudioCueNonSpatialFilterSource FilterSource)
{
    public AudioCueEnvelopeCurve AttackCurve { get; init; }
    public AudioCueEnvelopeCurve ReleaseCurve { get; init; }
}
/// <summary>The three neutral envelope scalars retain their actual source positions;
/// this reader does not assign unproven timing units to them.</summary>
public sealed record AudioCueStaticSoundParameters(int Offset, uint SoundId, uint ClassificationId,
    float MaximumDurationSeconds, float LinearGain, float MixMultiplier, float AdditiveRate,
    float InitialDelaySeconds, float EnvelopeScalar30, float EnvelopeScalar34, float EnvelopeScalar38);
public sealed record AudioCueStaticSampleTiming(int SampleSectionOffset, uint HeaderTypeId,
    bool IsExternal, int Channels, int SampleRate, int DeclaredSamples, int LoopStartSample)
{
    public bool HasSourceLoop => LoopStartSample >= 0;
}
/// <summary>One static source reference, not proof of admission, decoding or playback.
/// StopReleaseSeconds is used on explicit ordinary stop, never baked onto the natural tail.</summary>
public sealed record AudioCueStaticPlaybackReference(AudioCueSoundReference Sound,
    AudioCueStaticGroupParameters Group, AudioCueStaticSoundParameters Leaf,
    AudioCueStaticSampleTiming Sample, float LinearGain, float PlaybackRate)
{
    public uint? SourceChannelMask { get; init; }
    public int? SynchronousComponentIndex { get; init; }
}
