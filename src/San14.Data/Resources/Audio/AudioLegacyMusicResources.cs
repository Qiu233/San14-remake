using System.Buffers.Binary;

namespace San14.Data;

public sealed record AudioLegacyMusicGroup(int Offset, uint Flags, uint ClassificationId,
    uint PropagatedKey, float PrimaryGain, float SecondaryGain, float AdditiveRate,
    float DelaySeconds, float StartEnvelopeSeconds, float StopReleaseSeconds,
    AudioCueNonSpatialFilterSource FilterSource);
/// <summary>ResourceField18 preserves the old source word. Its meaning is not
/// borrowed from a different opcode's duration or the runtime channel-mask byte.</summary>
public sealed record AudioLegacyMusicLeaf(int Offset, uint Flags, uint ClassificationId,
    uint SoundId, uint ResourceField18, float LinearGain, float MixMultiplier,
    float AdditiveRate, float DelaySeconds, float Envelope2C, float Envelope30, float Envelope34);
/// <summary>Original external-header values before decoder delay/padding handling.
/// DecodeInput and decoded PCM retain their separate actual playable intervals.</summary>
public sealed record AudioLegacyMusicSample(AudioNamedSample Sample, AudioSampleHeader Header,
    uint SourceChannelMask, int EncoderDelaySamples);
public sealed record AudioLegacyMusicReference(AudioCueSoundReference Sound,
    AudioLegacyMusicGroup Group, AudioLegacyMusicLeaf Leaf, AudioLegacyMusicSample Sample);

/// <summary>The finite old 51D/6282 static resource graph. Parameter values and
/// the propagated child key are preserved; neither a host nor live overrides,
/// classification binding, stop behavior or decoder lane order are invented.</summary>
public static class AudioLegacyMusicResources
{
    public static AudioLegacyMusicReference ReadStaticReference(AudioSelectionBank bank, string cueName)
    {
        AudioCueGraph graph = AudioCueGraphResources.Read(bank, cueName);
        if (graph.RootOffsets.Count != 1 || graph.Nodes.Count != 2)
            throw new NotSupportedException("Legacy static music requires one group and one sound reference.");
        var groupNode = graph.Nodes.Single(node => node.Offset == graph.RootOffsets[0]);
        if (groupNode.Opcode != AudioCueGraphResources.DirectGroupOpcode || groupNode.ChildOffsets.Count != 1)
            throw new NotSupportedException("Legacy static music has an unsupported root graph.");
        var leafNode = graph.Nodes.Single(node => node.Offset == groupNode.ChildOffsets[0]);
        if (leafNode.Opcode != AudioCueGraphResources.SoundReferenceOpcode ||
            groupNode.DeclaredHeaderBytes < 0x5C || leafNode.DeclaredHeaderBytes < 0x40)
            throw new NotSupportedException("Legacy music has an unsupported parameter header.");
        int begin = graph.Cue.SectionOffset, end = checked(begin + graph.Cue.SectionBytes);
        byte[] bytes = bank.Source.Bytes;
        uint U32(int at)
        {
            if (at < begin || at > end - 4) throw new InvalidDataException("Legacy music field is outside its cue.");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        }
        float Scalar(int at)
        {
            float value = BitConverter.Int32BitsToSingle(unchecked((int)U32(at)));
            if (!float.IsFinite(value)) throw new InvalidDataException("Legacy music scalar is not finite.");
            return value;
        }
        int group = groupNode.Offset, leaf = leafNode.Offset;
        if ((groupNode.Flags & ~0xC00000u) != 0 || leafNode.Flags != 0)
            throw new NotSupportedException("Legacy static music does not implement dynamic flags or selectors.");
        if (U32(group + 0x50) != 0 || U32(leaf + 0x38) != 0)
            throw new NotSupportedException("Legacy static music does not implement parameter curves.");
        if (U32(leaf + 0x10) != 0)
            throw new NotSupportedException("Legacy static music does not implement a leaf classification override.");
        if (leaf < group + (long)groupNode.DeclaredHeaderBytes)
            throw new InvalidDataException("Legacy sound reference overlaps its group header.");
        var groupParameters = new AudioLegacyMusicGroup(group, groupNode.Flags, U32(group + 0x18), U32(group + 0x1C),
            Scalar(group + 0x20), Scalar(group + 0x24), Scalar(group + 0x28), Scalar(group + 0x2C),
            Scalar(group + 0x30), Scalar(group + 0x34),
            new(Scalar(group + 0x38), Scalar(group + 0x3C), Scalar(group + 0x40), Scalar(group + 0x44), U32(group + 0x48), Scalar(group + 0x4C)));
        var leafParameters = new AudioLegacyMusicLeaf(leaf, leafNode.Flags, U32(leaf + 0x10), U32(leaf + 0x14),
            U32(leaf + 0x18), Scalar(leaf + 0x1C), Scalar(leaf + 0x20), Scalar(leaf + 0x24),
            Scalar(leaf + 0x28), Scalar(leaf + 0x2C), Scalar(leaf + 0x30), Scalar(leaf + 0x34));
        if (groupParameters.PrimaryGain < 0 || groupParameters.SecondaryGain < 0 || groupParameters.StopReleaseSeconds < 0 ||
            leafParameters.LinearGain < 0 || leafParameters.MixMultiplier < 0)
            throw new InvalidDataException("Legacy static gain or release duration is negative.");
        //6282CA29 looks up the external registry. A bank can also contain an
        //internal sample with the same key, which belongs to a different pool.
        AudioNamedSample[] matches = bank.Samples.Where(sample => sample.SampleId == leafParameters.SoundId &&
            (sample.Flags & 0x10000) != 0).ToArray();
        if (matches.Length != 1 || matches[0].Headers.Count != 1)
            throw new NotSupportedException("Legacy music must resolve one sample with one external header.");
        AudioSampleHeader header = matches[0].Headers[0];
        if (!header.IsExternal || header.TypeId != 0x7D43D038 || header.HeaderBytes < 0x40)
            throw new NotSupportedException("Legacy music external sample format is unsupported.");
        uint mask = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(header.HeaderOffset + 0x28, 4));
        int delay = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(header.HeaderOffset + 0x20, 4));
        if (delay < 0) throw new InvalidDataException("Legacy music source delay is negative.");
        return new(new(graph.Cue.CueId, cueName, leafParameters.SoundId, begin, leaf, bank.Source),
            groupParameters, leafParameters, new(matches[0], header, mask, delay));
    }
}
