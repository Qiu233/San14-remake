using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>2851B0's positional SE B3. The installed cue is an ordered
/// four-layer spatial group, not the single-sound territory4/5 family.</summary>
public static class BaseCaptureSoundSource
{
    public const uint SelectionBankId = MapTerritorySoundSource.SelectionBankId;
    public const int ManagerId = 0xB3;
    public const uint CueId = 0x5458C32D;

    public static BaseCaptureSoundPlayback ReadPlayback(AudioSelectionBank bank)
    {
        if (bank.Source.ResourceUri != GameResourceId.Rdb("RRPreview", SelectionBankId).ToString())
            throw new InvalidDataException("Base capture sound has a different source bank.");
        var cues = bank.Cues.Where(row => row.CueId == CueId).Take(2).ToArray();
        if (cues.Length != 1 || cues[0].Name is not { Length: > 0 } name)
            throw new InvalidDataException("Base capture sound has no unique source cue.");
        var cue = cues[0];
        byte[] bytes = bank.Source.Bytes;
        int begin = cue.SectionOffset, end = checked(begin + cue.SectionBytes);
        uint U(int at)
        {
            if (at < begin || at > end - 4)
                throw new InvalidDataException("Base capture cue field is outside its section.");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        }
        float F(int at)
        {
            float value = BitConverter.Int32BitsToSingle(unchecked((int)U(at)));
            return float.IsFinite(value) ? value : throw new InvalidDataException("Nonfinite base capture cue parameter.");
        }
        int Pointer(int origin, int field) => checked(origin + (int)U(origin + field));
        if (U(begin + 32) != 1)
            throw new NotSupportedException("Base capture cue needs its single spatial group.");
        int rootTable = Pointer(begin, 36), group = checked(begin + (int)U(rootTable));
        if (U(group) != 0x562FC4B9 || U(group + 12) != 0 || U(group + 16) != 4 ||
            F(group + 44) != 0 || F(group + 48) != 0 || F(group + 40) != 0)
            throw new NotSupportedException("Base capture cue requires an unsupported group scheduling branch.");
        float groupGain = F(group + 28);
        if (groupGain < 0) throw new InvalidDataException("Base capture group gain is negative.");
        var curve = new List<Vector2>();
        if (U(group + 80) != 1)
            throw new NotSupportedException("Base capture cue needs its camera-distance gain curve.");
        int parameterTable = Pointer(group, 84), parameter = checked(group + (int)U(parameterTable));
        if (U(parameter) != 0 || U(parameter + 4) != 0xC602882A || U(parameter + 8) != 0 || U(parameter + 12) != 16)
            throw new NotSupportedException("Base capture cue curve is not the source camera-distance gain curve.");
        int points = checked((int)U(parameter + 16));
        if (points is < 2 or > 64) throw new InvalidDataException("Base capture cue has an invalid curve point count.");
        for (int i = 0; i < points; i++)
        {
            var point = new Vector2(F(parameter + 20 + i * 8), F(parameter + 24 + i * 8));
            if (point.Y < 0 || curve.Count > 0 && point.X <= curve[^1].X)
                throw new InvalidDataException("Base capture distance curve points are invalid.");
            curve.Add(point);
        }
        var distanceGain = curve.AsReadOnly();
        var layers = new List<BaseCaptureSoundLayerPlayback>(4);
        int leafTable = Pointer(group, 20);
        for (int i = 0; i < 4; i++)
        {
            int leaf = checked(group + (int)U(leafTable + i * 4));
            if (U(leaf) != 0x941ECC07 || U(leaf + 4) != 0x4C || U(leaf + 12) != 0x10000 ||
                U(leaf + 16) != 0 || F(leaf + 28) != 0 || F(leaf + 36) != 1 ||
                F(leaf + 44) != 0 || F(leaf + 48) != 0 || F(leaf + 52) != 0 ||
                F(leaf + 56) != 0 || U(leaf + 60) != 0 || U(leaf + 64) != 0)
                throw new NotSupportedException("Base capture cue requires an unsupported sample scheduling branch.");
            float gain = groupGain * F(leaf + 32), rate = MathF.Pow(2, F(leaf + 40));
            if (!float.IsFinite(gain) || gain < 0 || !float.IsFinite(rate) || rate <= 0)
                throw new InvalidDataException("Base capture sample gain/rate is invalid.");
            //941ECC07 extends the older A5170472 declaration by two scalar
            //fields. Retain them explicitly instead of discarding them or
            //guessing a fade/stop interpretation in this resource reader.
            var tail = new Vector2(F(leaf + 68), F(leaf + 72));
            var input = MapTerritorySoundSource.SelectInternal(bank, U(leaf + 20));
            layers.Add(new(new(input, gain, rate, distanceGain), tail));
        }
        return new(new(SelectionBankId, null, name, AudioLoopPolicy.Disabled), layers.AsReadOnly());
    }

    /// <summary>405370/011470 tests the actual camera's projected point.
    /// A culled call is not replaced with a non-positional UI sound.</summary>
    public static bool IsInsideAudibleClip(Vector3 sourcePosition, Matrix4x4 worldToProjection)
    {
        var clip = Vector4.Transform(new Vector4(sourcePosition, 1), worldToProjection);
        float reciprocal = 1f / clip.W;
        float x = clip.X * reciprocal, y = clip.Y * reciprocal, z = clip.Z * reciprocal;
        return x > -1 && x < 1 && y > -1 && y < 1 && z > 0 && z < 1;
    }
}

public sealed record BaseCaptureSoundPlayback(AudioCueRequest Cue,
    IReadOnlyList<BaseCaptureSoundLayerPlayback> Layers);

/// <summary>One ordered layer of the actual cue. ResourceTailParameters are
/// the retained941ECC07 extension, not an inferred playback completion time.</summary>
public sealed record BaseCaptureSoundLayerPlayback(MapTerritorySoundPlayback Playback,
    Vector2 ResourceTailParameters)
{
    public float SourceLfeGain => ResourceTailParameters.X;
    public float StartOffsetSeconds => ResourceTailParameters.Y;
    public AudioDecodeInput Input => Playback.Input;
    public float PlaybackRate => Playback.PlaybackRate;
    public float GainAt(float cameraDistance) => Playback.GainAt(cameraDistance);
}
