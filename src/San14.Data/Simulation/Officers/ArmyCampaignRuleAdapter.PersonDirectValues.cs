using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The actual base value, not the derived ability with officer/post bonuses.
    private int ReadDirectPersonParameterCurrent(GameState state, SourceWorldContext current, int physical, int kind)
    {
        if (kind == 6)
            return DirectPersonValueNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, physical,
                _governorInputs), "loyalty_byte");
        if (kind is < 1 or > 5) return 0;
        var person = DirectPersonValueOwner(state, current, physical);
        var basic = person.BaseAbilities ?? throw DirectPersonValueMissing("base_abilities");
        int before = kind switch { 1 => basic.Leadership, 2 => basic.Strength, 3 => basic.Intelligence,
            4 => basic.Politics, _ => basic.Charm };
        return before is >= 0 and <= 255 ? before : throw DirectPersonValueMissing("base_ability_byte");
    }

    //Shared3E1300. The ordinary and edited callers own their different gates.
    //There is no growth notification or event-manager summary in this setter.
    private GameState ApplyDirectPersonParameterCurrent(GameState state, SourceWorldContext current,
        int physical, int kind, int requested)
    {
        if (kind == 6) return WriteDirectPersonLoyaltyCurrent(state, current, physical, requested);
        if (kind is < 1 or > 5) return state;
        var person = DirectPersonValueOwner(state, current, physical);
        var basic = person.BaseAbilities ?? throw DirectPersonValueMissing("base_abilities");
        int after = Math.Clamp(requested, 1, 100);
        var changed = kind switch
        {
            1 => basic with { Leadership = after }, 2 => basic with { Strength = after },
            3 => basic with { Intelligence = after }, 4 => basic with { Politics = after },
            _ => basic with { Charm = after }
        };
        //An absent channel remains unknown. Only the reached source channel
        //is reset; clone this five-element owner instead of mutating the input.
        ulong?[] experience = person.Growth?.Experience is { } prior ? (ulong?[])prior.Clone() : new ulong?[5];
        if (experience.Length != 5) throw DirectPersonValueMissing("growth_channels");
        experience[kind - 1] = _growthSource.ReadColumn(after, kind - 1);
        var published = state with { Officers = new(state.Officers) { [person.Id] = person with
            { BaseAbilities = changed, Growth = new(experience, person.Growth?.Notifications) } } };
        //Condition queries share this same capture. Replace the one reader
        //with the newly published owner; do not retain an old-state closure or
        //build a chain of reader overlays for every subsequent parameter write.
        current.ConditionBaseAbilityReader = (slot, index) =>
        {
            if (current.ReadPersonIdentity(slot) is not ushort id || current.ResolveOfficerSource(id) != slot ||
                published.Officers.GetValueOrDefault(id)?.BaseAbilities is not { } value) return null;
            int raw = index switch { 0 => value.Leadership, 1 => value.Strength, 2 => value.Intelligence,
                3 => value.Politics, 4 => value.Charm, _ => -1 };
            return raw is >= 0 and <= 255 ? (byte)raw : null;
        };
        return published;
    }

    //Both3E1300(kind6) and ordinary160 store the low byte, independently of
    //native validity. Flag13 writes255, without a0..100 loyalty clamp.
    private static GameState WriteDirectPersonLoyaltyCurrent(GameState state, SourceWorldContext current,
        int physical, int requested)
    {
        ushort flags = DirectPersonValueNeed(current.ReadOwnedReturnPersonFlags(physical), "Person_behavior_flags");
        byte after = (flags & 0x2000) != 0 ? byte.MaxValue : unchecked((byte)requested);
        current.SetPersonDestructionLoyaltyE8(physical, after);
        return ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, physical, after);
    }

    private static OfficerState DirectPersonValueOwner(GameState state, SourceWorldContext current, int physical)
    {
        ushort id = DirectPersonValueNeed(current.ReadPersonIdentity(physical), "Person_identity");
        return current.ResolveOfficerSource(id) == physical && state.Officers.TryGetValue(id, out var person)
            ? person : throw DirectPersonValueMissing("Person_owner");
    }
    private static T DirectPersonValueNeed<T>(T? value, string owner) where T : struct
        => value ?? throw DirectPersonValueMissing(owner);
    private static InvalidDataException DirectPersonValueMissing(string owner) => new("person.direct_value.current." + owner);
}
