using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public sealed record TacticSoundPlayback(AudioCueRequest Cue, IReadOnlyList<SpatialEffectSoundLayer> Layers);

/// <summary>The installed positional SE180..187 declarations. Manager keys
/// come from the shared offline-imported SE table; layers, distance curves,
/// gains and sample extension fields come from the normal spatial bank.</summary>
public static class TacticSoundSource
{
    public const uint SelectionBankId = MapTerritorySoundSource.SelectionBankId;

    public static TacticSoundPlayback ReadPlayback(AudioSelectionBank bank, int managerId)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (managerId is < 180 or > 187)
            throw new InvalidDataException("tactic.sound.manager_index");
        return ReadSingleLayerPlayback(bank, managerId, 1);
    }

    // The reached facility callers share this resource graph. Their exact
    // source declaration selects zero or one camera-distance curve; no
    // multi-child selector or unknown scheduling branch is inferred here.
    internal static TacticSoundPlayback ReadSingleLayerPlayback(AudioSelectionBank bank, int managerId,
        uint distanceCurveCount)
        => ReadLayer(bank, managerId, distanceCurveCount, 0, false, out _);

    internal static TacticSoundPlayback ReadArmyDamageVariant(AudioSelectionBank bank, int managerId,
        int childIndex, out float leafAdditiveRate)
    {
        if (managerId is not (161 or 162) || (uint)childIndex >= 3)
            throw new InvalidDataException("army_damage.sound.variant");
        return ReadLayer(bank, managerId, 1, childIndex, true, out leafAdditiveRate);
    }

    private static TacticSoundPlayback ReadLayer(AudioSelectionBank bank, int managerId,
        uint distanceCurveCount, int childIndex, bool armyDamage, out float leafAdditiveRate)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (bank.Source.ResourceUri != GameResourceId.Rdb("RRPreview", SelectionBankId).ToString())
            throw new InvalidDataException("tactic.sound.selection_bank");
        uint key = NormalSoundSource.CueHash(managerId) ?? throw new InvalidDataException("tactic.sound.cue_key");
        var matches = bank.Cues.Where(cue => cue.CueId == key).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Name is not { Length: > 0 } name)
            throw new InvalidDataException("tactic.sound.unique_cue");
        var cue = matches[0];
        byte[] bytes = bank.Source.Bytes;
        int begin = cue.SectionOffset, end = checked(begin + cue.SectionBytes);
        uint U(int at)
        {
            if (begin < 0 || at < begin || at > end - 4 || end > bytes.Length)
                throw new InvalidDataException("tactic.sound.section_bounds");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        }
        float F(int at)
        {
            float value = BitConverter.Int32BitsToSingle(unchecked((int)U(at)));
            return float.IsFinite(value) ? value : throw new InvalidDataException("tactic.sound.nonfinite_parameter");
        }
        int Pointer(int origin, int field) => checked(origin + (int)U(origin + field));
        if (U(begin + 32) != 1)
            throw new NotSupportedException("tactic.sound.group_count");
        int rootTable = Pointer(begin, 36), group = checked(begin + (int)U(rootTable));
        if (U(group) != 0x562FC4B9 || U(group + 12) != (armyDamage ? 0x10010u : 0u) ||
            U(group + 16) != (armyDamage ? 3u : 1u) ||
            (!armyDamage && F(group + 40) != 0) || F(group + 44) != 0 || F(group + 48) != 0)
            throw new NotSupportedException("tactic.sound.group_scheduling");
        float groupGain = F(group + 28);
        if (groupGain < 0) throw new InvalidDataException("tactic.sound.negative_gain");
        if (distanceCurveCount > 1 || U(group + 80) != distanceCurveCount)
            throw new NotSupportedException("tactic.sound.distance_curve_count");
        var curve = new List<Vector2>();
        if (distanceCurveCount != 0)
        {
            int parameterTable = Pointer(group, 84), parameter = checked(group + (int)U(parameterTable));
            if (U(parameter) != 0 || U(parameter + 4) != 0xC602882A ||
                U(parameter + 8) != 0 || U(parameter + 12) != 16)
                throw new NotSupportedException("tactic.sound.distance_curve_kind");
            int pointCount = checked((int)U(parameter + 16));
            if (pointCount is < 2 or > 64) throw new InvalidDataException("tactic.sound.distance_curve_points");
            for (int index = 0; index < pointCount; index++)
            {
                var point = new Vector2(F(parameter + 20 + index * 8), F(parameter + 24 + index * 8));
                if (point.Y < 0 || curve.Count > 0 && point.X <= curve[^1].X)
                    throw new InvalidDataException("tactic.sound.distance_curve_order");
                curve.Add(point);
            }
        }
        int leafTable = Pointer(group, 20), leaf = checked(group + (int)U(checked(leafTable + childIndex * 4)));
        uint opcode = U(leaf);
        bool extended = opcode == 0x941ECC07;
        if ((!extended && opcode != 0xA5170472) || U(leaf + 4) != (extended ? 76u : 68u) ||
            (U(leaf + 12) & ~0x7000u) != 0x10000 || U(leaf + 16) != 0 || F(leaf + 28) != 0 || F(leaf + 36) != 1 ||
            F(leaf + 44) != 0 || F(leaf + 48) != 0 || F(leaf + 52) != 0 || F(leaf + 56) != 0 ||
            U(leaf + 60) != 0 || U(leaf + 64) != 0)
            throw new NotSupportedException("tactic.sound.sample_scheduling");
        leafAdditiveRate = F(leaf + 40);
        float gain = groupGain * F(leaf + 32), rate = MathF.Pow(2, leafAdditiveRate);
        if (!float.IsFinite(gain) || gain < 0 || !float.IsFinite(rate) || rate <= 0)
            throw new InvalidDataException("tactic.sound.sample_gain_rate");
        //Older A5170472 leaves have no LFE/start overrides.941ECC07's two
        //additional scalars feed the actual shared spatial player directly.
        Vector2? tail = extended ? new Vector2(F(leaf + 68), F(leaf + 72)) : null;
        var input = MapTerritorySoundSource.SelectInternal(bank, U(leaf + 20));
        SpatialEffectSoundLayer layer = new(new(input, gain, rate, curve.AsReadOnly())
            { SourceCenterGain = Math.Clamp(F(leaf + 56), 0f, 1f),
                SourceChannelMask = MapTerritorySoundSource.ReadSpatialChannelMask(bank, U(leaf + 20), U(leaf + 24)),
                    SourceSpatialModeFlags = U(leaf + 12) & 0x7000u }, tail);
        return new(new(SelectionBankId, null, name, AudioLoopPolicy.Disabled), Array.AsReadOnly(new[] { layer }));
    }
}
