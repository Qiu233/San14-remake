namespace San14.Data;

/// <summary>The installed definition inputs shared by the ordinary tactic
/// beam, name animation and Army spatial-voice callers. Reading this declaration
/// neither allocates a visual instance nor admits or selects a voice.</summary>
public sealed record OrdinaryTacticPresentationSource(int TacticId, string Name,
    byte Category, int ArmySpatialVoiceId, bool UsesNeutralNamePreset)
{
    public int BeamEffectKind => 9;
    public string BeamEffectName => "map_unit_effect_013";
    public string NameAnimation => "2_senpou";
    public int FirstNameNode => 12;
    public int SecondNameNode => 13;

    /// <summary>16DB50 with its actual caster/player relation and variant.
    /// The caller must obtain that relation from its real current owners.</summary>
    public int NamePreset(bool casterBelongsToPlayer, bool alternateVariant = false)
        => UsesNeutralNamePreset ? 0x2E : alternateVariant ? 0x34 : casterBelongsToPlayer ? 0x2F : 0x30;
}

/// <summary>Fixed-loader fields of the normal, override-aware tactic definition.
/// Native22E1F0 in the voice dispatch is the tactic's physical identity; it is
/// not an instruction opcode or a translated name.</summary>
public static class TacticPresentationSource
{
    public static OrdinaryTacticPresentationSource Read(TacticSourceRow row, bool usesNeutralNamePreset)
    {
        ArgumentNullException.ThrowIfNull(row);
        byte category = row.ReadNativeFixedByte(0x40);
        return new(row.Slot, row.Name, category, ReadArmyVoiceEvent(row.Slot, category), usesNeutralNamePreset);
    }

    /// <summary>16E6C0's current classification and physical tactic identity.
    /// Both the fixed ordinary declaration and Direction's current owner use
    /// this mapping; neither method selects a variant or admits a voice.</summary>
    public static int ReadArmyVoiceEvent(int tacticId, byte category) => category switch
        {
            2 => 15, 3 => 16, 4 => 17,
            5 => tacticId == 13 ? 18 : tacticId is 14 or 15 ? 19 : 20,
            6 => tacticId == 18 ? 21 : tacticId == 19 ? 22 : 23,
            7 => tacticId == 21 ? 24 : tacticId == 22 ? 25 : 26,
            8 => 27,
            _ => 14
        };
}
