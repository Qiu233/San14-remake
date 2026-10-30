using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Numerics;

namespace San14.Data;

/// <summary>The two source-selected positional occupation cues. Runtime reads
/// installed audio resources; the original SE index table was extracted offline.</summary>
public static class MapTerritorySoundSource
{
    public const uint SelectionBankId = 0x9D8A36A1;

    public static AudioCueRequest Resolve(AudioSelectionBank bank, int effectKind)
    {
        uint key = effectKind switch
        {
            4 => 0x5D781D48, //SE AE: SE_2000_OCCUPATION_CAPITAL
            5 => 0x4F8E873B, //SE AD: SE_2000_OCCUPATION_HEX
            _ => throw new InvalidDataException("Invalid territory feedback kind.")
        };
        if (bank.Source.ResourceUri != GameResourceId.Rdb("RRPreview", SelectionBankId).ToString())
            throw new InvalidDataException("Territory sound has a different source bank.");
        var cues = bank.Cues.Where(cue => cue.CueId == key).Take(2).ToArray();
        if (cues.Length != 1 || cues[0].Name is not { Length: > 0 } name)
            throw new InvalidDataException("Territory sound has no unique source cue.");
        return new(SelectionBankId, null, name, AudioLoopPolicy.Disabled);
    }

    public static MapTerritorySoundPlayback ReadPlayback(AudioSelectionBank bank, int effectKind)
    {
        var request = Resolve(bank, effectKind);
        var cue = bank.Cues.Single(row => row.Name == request.ExactCueName);
        byte[] bytes = bank.Source.Bytes;
        int begin = cue.SectionOffset, end = checked(begin + cue.SectionBytes);
        uint U(int at)
        {
            if (at < begin || at > end - 4) throw new InvalidDataException("Spatial cue field is outside its section.");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        }
        float F(int at)
        {
            float value = BitConverter.Int32BitsToSingle(unchecked((int)U(at)));
            return float.IsFinite(value) ? value : throw new InvalidDataException("Nonfinite spatial cue parameter.");
        }
        int Pointer(int origin, int field) => checked(origin + (int)U(origin + field));
        if (U(begin + 32) != 1) throw new NotSupportedException("Spatial occupation cue needs its single group.");
        int rootTable = Pointer(begin, 36), group = checked(begin + (int)U(rootTable));
        if (U(group) != 0x562FC4B9 || U(group + 12) != 0 || U(group + 16) != 1)
            throw new NotSupportedException("Spatial occupation group declaration changed.");
        int leafTable = Pointer(group, 20), leaf = checked(group + (int)U(leafTable));
        if (U(leaf) != 0xA5170472 || (U(leaf + 12) & ~0x7000u) != 0x10000 || U(leaf + 16) != 0 ||
            U(leaf + 60) != 0 || F(group + 44) != 0 || F(group + 48) != 0 ||
            F(leaf + 28) != 0 || F(leaf + 44) != 0)
            throw new NotSupportedException("Spatial occupation cue requires an unsupported scheduling branch.");
        float gain = F(group + 28) * F(leaf + 32), rate = MathF.Pow(2, F(group + 40) + F(leaf + 40));
        if (gain < 0 || !float.IsFinite(rate) || rate <= 0)
            throw new InvalidDataException("Spatial occupation gain/rate is invalid.");
        var curve = new List<Vector2>();
        uint count = U(group + 80);
        if (count > 1) throw new NotSupportedException("Spatial occupation cue has multiple parameter curves.");
        if (count == 1)
        {
            int table = Pointer(group, 84), at = checked(group + (int)U(table));
            if (U(at) != 0 || U(at + 4) != 0xC602882A || U(at + 8) != 0 || U(at + 12) != 16)
                throw new NotSupportedException("Spatial occupation parameter curve is not the camera-distance gain curve.");
            int points = checked((int)U(at + 16));
            if (points is < 2 or > 64)
                throw new InvalidDataException("Spatial occupation curve has an invalid point count.");
            for (int i = 0; i < points; i++)
            {
                var point = new Vector2(F(at + 20 + i * 8), F(at + 24 + i * 8));
                if (point.Y < 0 || curve.Count > 0 && point.X <= curve[^1].X)
                    throw new InvalidDataException("Spatial occupation curve points are invalid.");
                curve.Add(point);
            }
        }
        uint sound = U(leaf + 20);
        return new(SelectInternal(bank, sound), gain, rate, curve.AsReadOnly())
            { SourceCenterGain = Math.Clamp(F(leaf + 56), 0f, 1f),
                SourceChannelMask = ReadSpatialChannelMask(bank, sound, U(leaf + 24)),
                SourceSpatialModeFlags = U(leaf + 12) & 0x7000u };
    }

    internal static uint ReadSpatialChannelMask(AudioSelectionBank bank, uint soundId, uint declared)
    {
        uint mask = declared & 0xffu;
        if (mask != 0) return mask;
        var matches = bank.Samples.Where(sample => sample.SampleId == soundId &&
            (sample.Flags & 0x10000) == 0).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Headers.Count != 1)
            throw new InvalidDataException("Spatial sound channel mask has no unique source header.");
        // These are exactly the admitted internal headers whose native mask is
        // read by the original sample preparation path.
        var header = matches[0].Headers[0];
        if (header.TypeId is not (0xE96FD86A or 0x2EB6CA6F))
            throw new NotSupportedException("spatial.sound.channel_mask_header");
        return header.NativeChannelMask & 0xffu;
    }

    /// <summary>Keep the original sound subheader and encoded channels in a
    /// single-sound container. The existing decoder owns codec interpretation;
    /// unrecognized source subheaders are never relabelled as MSADPCM.</summary>
    public static AudioDecodeInput SelectInternal(AudioSelectionBank bank, uint soundId)
    {
        var matches = bank.Samples.Where(sample => sample.SampleId == soundId &&
            (sample.Flags & 0x10000) == 0).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Headers.Count != 1)
            throw new InvalidDataException("Spatial cue needs one internal source sound.");
        var sample = matches[0];
        byte[] bytes = bank.Source.Bytes;
        if (sample.SectionOffset < 80 || sample.SectionBytes < 32 ||
            sample.SectionOffset > bytes.Length - sample.SectionBytes)
            throw new InvalidDataException("Spatial sound section is outside its bank.");
        byte[] selected = new byte[checked(80 + sample.SectionBytes)];
        bytes.AsSpan(0, 80).CopyTo(selected);
        bytes.AsSpan(sample.SectionOffset, sample.SectionBytes).CopyTo(selected.AsSpan(80));
        BinaryPrimitives.WriteUInt64LittleEndian(selected.AsSpan(8), (ulong)selected.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(selected.AsSpan(40), (uint)selected.Length - 16);
        BinaryPrimitives.WriteUInt32LittleEndian(selected.AsSpan(44), (uint)selected.Length - 16);
        var header = sample.Headers[0];
        AudioSampleFormat? format = header.SampleRate > 0 && header.Channels > 0 && header.DeclaredSamples > 0
            ? new(header.SampleRate, header.Channels, header.DeclaredSamples) : null;
        var source = new SourceProvenance(bank.Source.SourceContainerPath +
            $"#decoded-range={sample.SectionOffset},{sample.SectionBytes}", sample.SectionBytes,
            Convert.ToHexString(SHA256.HashData(bytes.AsSpan(sample.SectionOffset, sample.SectionBytes))))
        { ResourceUri = bank.Source.ResourceUri + $"#sound={soundId:X8}",
            PhysicalPath = bank.PhysicalPath ?? bank.Source.OverrideSource?.PhysicalPath ?? "",
            IsOverride = bank.Source.OverrideSource is not null };
        return new(selected, ".srsa", "SRSA-KTSR", Convert.ToHexString(SHA256.HashData(selected)), source, format)
            { IndexSource = bank.Source.IndexSource };
    }
}

public sealed record MapTerritorySoundPlayback(AudioDecodeInput Input, float LinearGain,
    float PlaybackRate, IReadOnlyList<Vector2> CameraDistanceGain)
{
    /// <summary>The actual sample center coefficient after its source clamp.
    /// Null preserves an unknown origin rather than inventing a center value.</summary>
    public float? SourceCenterGain { get; init; }
    /// <summary>Actual declared or decoded-header speaker mask, before any voice override.</summary>
    public uint? SourceChannelMask { get; init; }
    /// <summary>The actual sample spatial-mode bits, before the ordinary voice override.</summary>
    public uint? SourceSpatialModeFlags { get; init; }

    public float GainAt(float cameraDistance)
    {
        if (!float.IsFinite(cameraDistance)) throw new InvalidDataException("Spatial audio camera distance is nonfinite.");
        if (CameraDistanceGain.Count == 0) return LinearGain;
        if (cameraDistance <= CameraDistanceGain[0].X) return LinearGain * CameraDistanceGain[0].Y;
        for (int i = 1; i < CameraDistanceGain.Count; i++)
        {
            var right = CameraDistanceGain[i];
            if (cameraDistance > right.X) continue;
            var left = CameraDistanceGain[i - 1];
            float t = (cameraDistance - left.X) / (right.X - left.X);
            return LinearGain * (left.Y + (right.Y - left.Y) * t);
        }
        return LinearGain * CameraDistanceGain[^1].Y;
    }
}
