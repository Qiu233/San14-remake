using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public sealed record SpatialEffectSoundLayer(MapTerritorySoundPlayback Playback, Vector2? ResourceTailParameters)
{
    public AudioDecodeInput Input => Playback.Input;
    public float Gain => Playback.LinearGain;
    public float PlaybackRate => Playback.PlaybackRate;
    public float SourceLfeGain => ResourceTailParameters?.X ?? 0;
    public float StartOffsetSeconds => ResourceTailParameters?.Y ?? 0;
    public float GainAt(float cameraDistance) => Playback.GainAt(cameraDistance);
}
public sealed record SpatialEffectSoundPlayback(int ManagerId, AudioCueRequest Cue,
    IReadOnlyList<SpatialEffectSoundLayer> Layers, AudioCueStaticGroupParameters Group,
    AudioSpatialOutputPlan OutputPlan);

/// <summary>The actual AD/AE territory, B3 foothold and180..187 tactic cues share the SE
/// manager but retain their own ordered layers, parameter curves and output
/// classifications. One cue uses one manager slot, not one slot per layer.</summary>
public static class SpatialEffectSoundSource
{
    public static SpatialEffectSoundPlayback Prepare(AudioSelectionBank bank, LoadedRdbAsset categories, int managerId)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(categories);
        AudioCueRequest cue;
        IReadOnlyList<SpatialEffectSoundLayer> layers;
        if (managerId is 0xAD or 0xAE)
        {
            int kind = managerId == 0xAD ? 5 : 4;
            cue = MapTerritorySoundSource.Resolve(bank, kind);
            layers = new[] { new SpatialEffectSoundLayer(MapTerritorySoundSource.ReadPlayback(bank, kind), null) };
        }
        else if (managerId == BaseCaptureSoundSource.ManagerId)
        {
            var selected = BaseCaptureSoundSource.ReadPlayback(bank);
            cue = selected.Cue;
            layers = selected.Layers.Select(layer => new SpatialEffectSoundLayer(layer.Playback,
                layer.ResourceTailParameters)).ToArray();
        }
        else if (managerId is >= 180 and <= 187)
        {
            var selected = TacticSoundSource.ReadPlayback(bank, managerId);
            cue = selected.Cue;
            layers = selected.Layers;
        }
        else if (managerId is 194 or 195 or 196 or >= 198 and <= 201)
        {
            // Construct start/finish have no distance parameter. Destruction
            // and the reached trap notices carry one authored distance curve.
            var selected = TacticSoundSource.ReadSingleLayerPlayback(bank, managerId,
                managerId is 194 or 195 ? 0u : 1u);
            cue = selected.Cue;
            layers = selected.Layers;
        }
        else throw new InvalidDataException("Spatial effect sound manager identity is unsupported.");
        var group = ReadGroup(bank, cue.ExactCueName);
        if (categories.ResourceUri != GameResourceId.Rdb("RRPreview", NarrationVoiceSelection.OutputResourceId).ToString())
            throw new InvalidDataException("Spatial capture sound has a different output resource.");
        var outputs = AudioMusicOutputResources.Read(categories, group.ClassificationId);
        return new(managerId, cue, layers, group, AudioSpatialOutputResources.ReadFreshEffectsPlan(outputs));
    }

    private static AudioCueStaticGroupParameters ReadGroup(AudioSelectionBank bank, string name)
        => ReadGroup(bank, name, 0, null);

    internal static AudioCueStaticGroupParameters ReadArmyDamageGroup(AudioSelectionBank bank, string name,
        float additiveRate) => ReadGroup(bank, name, 0x10010, additiveRate);

    private static AudioCueStaticGroupParameters ReadGroup(AudioSelectionBank bank, string name,
        uint expectedFlags, float? selectedAdditiveRate)
    {
        var matches = bank.Cues.Where(cue => cue.Name == name).Take(2).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Spatial capture sound has no unique cue.");
        var cue = matches[0];
        byte[] bytes = bank.Source.Bytes;
        int begin = cue.SectionOffset, end = checked(begin + cue.SectionBytes);
        uint U(int at)
        {
            if (at < begin || at > end - 4 || end > bytes.Length)
                throw new InvalidDataException("Spatial capture group field is outside its section.");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        }
        float F(int at)
        {
            float value = BitConverter.Int32BitsToSingle(unchecked((int)U(at)));
            return float.IsFinite(value) ? value : throw new InvalidDataException("Nonfinite spatial capture group field.");
        }
        if (U(begin + 32) != 1) throw new NotSupportedException("Spatial capture cue needs one group.");
        int table = checked(begin + (int)U(begin + 36)), group = checked(begin + (int)U(table));
        if (U(group) != 0x562FC4B9 || U(group + 12) != expectedFlags)
            throw new NotSupportedException("Spatial capture cue group is unsupported.");
        return new(group, U(group + 24), F(group + 28), F(group + 32), F(group + 36), selectedAdditiveRate ?? F(group + 40),
            F(group + 44), F(group + 48), F(group + 52),
            new(F(group + 56), F(group + 60), F(group + 64), F(group + 68), U(group + 72), F(group + 76)));
    }
}
