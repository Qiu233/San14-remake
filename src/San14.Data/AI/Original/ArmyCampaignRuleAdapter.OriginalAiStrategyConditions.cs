using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>0C22C0 condition111 -> 0A9CB0. The resource selector order is
    /// gold, troops, food; it is independent of the three stored byte offsets.</summary>
    internal bool ReadOriginalAiStrategyDistrictResourceSetting(SourceWorldContext current,
        int district, int resource, int desired)
    {
        int requested = desired is 1 or 2 ? desired : 0;
        if (!StrategyConditionNeed(current.ReadDistrictNativeValidity(district), "111_District_validity")) return false;
        int offset = resource switch { 0 => 0x20, 1 => 0x22, 2 => 0x21, _ => -1 };
        if (offset < 0) return false;
        return StrategyConditionNeed(current.ReadDistrictRawByte(district, offset),
            "111_District" + offset.ToString("X")) == requested;
    }

    /// <summary>0AA280 -> 3EB240 reads the actual saved environment selector
    /// and its selected difficulty member, including the advanced member.</summary>
    internal bool ReadOriginalAiStrategyDifficulty(GameState state, int literal)
    {
        int selector = StrategyConditionNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "115_SettingsE58");
        int offset = selector == 0 ? 0x134 : 0x28;
        int difficulty = StrategyConditionNeed(BattleRemakeCurrentSettings.ReadInt32(state, offset),
            "115_Settings" + offset.ToString("X"));
        // The source callback returns true for only the five explicit cases.
        // Its decompiled upper return-register bits are not its bool result.
        return difficulty is >= 0 and <= 4 && difficulty == literal;
    }

    /// <summary>0AA540 tests the current raw personal doctrine byte after the
    /// native Person validity gate. The literal uses source uint32 addition.</summary>
    internal bool ReadOriginalAiStrategyDoctrine(GameState state, SourceWorldContext current,
        int officerId, int literal)
    {
        int physical = StrategyConditionNeed((officerId is >= 0 and <= 6000
            ? current.ReadPersonLookupReference(officerId) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
            "125_Person_lookup");
        if (!StrategyConditionNeed(current.ReadPersonNativeValidity(physical), "125_Person_validity")) return false;
        int identity = StrategyConditionNeed(current.ReadPersonIdentity(physical), "125_Person10");
        var person = state.Officers.GetValueOrDefault(identity) ?? throw StrategyConditionMissing("125_Person_owner." + identity);
        if (person.Id != identity) throw StrategyConditionMissing("125_Person_identity_binding");
        int raw = person.DoctrineId ?? person.AbilityState?.ObservedDoctrineId
            ?? throw StrategyConditionMissing("125_Person14C_doctrine." + identity);
        if (raw is < 0 or > byte.MaxValue) throw StrategyConditionMissing("125_Person14C_byte." + identity);
        return unchecked((uint)literal + 1u) == (uint)raw;
    }

    private static T StrategyConditionNeed<T>(T? value, string field) where T : struct =>
        value ?? throw StrategyConditionMissing(field);
    private static InvalidDataException StrategyConditionMissing(string field) => new("original-ai.strategy-condition." + field);
}
