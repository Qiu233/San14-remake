using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal bool IsOriginalAiCautionInformationReceiverCurrent(SourceWorldContext current, OriginalAiEntity receiver)
    {
        RegistryKind kind;
        int count;
        if (receiver.Kind == WorldEntityKind.ArmyCaution) { kind = RegistryKind.Table81B50; count = 1001; }
        else if (receiver.Kind == WorldEntityKind.ArmyCautionGroup) { kind = RegistryKind.CautionTroops; count = 501; }
        else throw CautionInformationMissing("receiver_type");
        if ((uint)receiver.Id >= (uint)count) throw CautionInformationMissing("receiver_identity");
        return CautionInformationKnown(current.ReadRegistryObjectNativeValidity(new(kind, receiver.Id)), "receiver_validity");
    }

    internal int ReadOriginalAiCautionArmyStrengthCurrent(GameState state, SourceWorldContext current, int relation)
    {
        var at = CautionInformationArmyReference(state, relation);
        return BattleUnitSourceMath.TruncateSingleToInt32(ReadOriginalAiArmyCommonCurrent(state, current, at));
    }

    internal OriginalAiEntity ReadOriginalAiCautionArmyBaseCurrent(GameState state, SourceWorldContext current, int relation)
    {
        var target = CautionInformationRelation(state, relation).Base;
        if (!target.IsKnown) throw CautionInformationMissing("relation_Base");
        // A known cleared target selects the normal Base table-head receiver.
        var at = BaseLookup.Find(target.MapObjectId ?? 0, current);
        int id = CautionInformationKnown(current.ReadBaseObjectLink(at), "relation_Base_identity");
        return new(at.Kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate, id);
    }

    internal OriginalAiEntity ReadOriginalAiCautionArmyTargetCurrent(GameState state, int relation)
    {
        var target = CautionInformationRelation(state, relation).Unit;
        return target.Kind switch
        {
            ArmyCautionUnitKind.Unknown => throw CautionInformationMissing("relation_Army"),
            ArmyCautionUnitKind.Unit => new(WorldEntityKind.Army, target.UnitId
                ?? throw CautionInformationMissing("relation_Army_identity")),
            ArmyCautionUnitKind.None or ArmyCautionUnitKind.Expired => new(WorldEntityKind.Army, 0),
            _ => throw CautionInformationMissing("relation_Army_kind")
        };
    }

    internal IReadOnlyList<OriginalAiEntity> ReadOriginalAiCautionGroupArmyListCurrent(GameState state, int group)
        => CautionInformationMembers(state, group).Select(id => new OriginalAiEntity(WorldEntityKind.Army,
            id ?? throw CautionInformationMissing("group_member_pointer"))).ToArray();

    internal int ReadOriginalAiCautionGroupTroopsCurrent(GameState state, SourceWorldContext current, int group)
    {
        uint total = 0;
        foreach (int? member in CautionInformationMembers(state, group))
        {
            int id = member ?? throw CautionInformationMissing("group_member_pointer");
            int physical = WorldIndexContext.FindArmyPhysical(state, id)
                ?? throw CautionInformationMissing("group_member_Army_receiver");
            ushort troops = CautionInformationKnown(current.ReadArmyReturnResourceFields(new(physical))?.Ready16, "group_member_Ready16");
            total = unchecked(total + troops);
        }
        return unchecked((int)total);
    }

    internal OriginalAiEntity ReadOriginalAiCautionGroupForceCurrent(GameState state, SourceWorldContext current, int group)
    {
        var members = CautionInformationMembers(state, group);
        int districtRaw = 0;
        // The source's District getter reads the FIRST current member's actual
        // commander, not the separately stored caution-group District field.
        if (members.Count != 0)
        {
            int id = members[0] ?? throw CautionInformationMissing("group_first_member_pointer");
            int physical = WorldIndexContext.FindArmyPhysical(state, id)
                ?? throw CautionInformationMissing("group_first_Army_receiver");
            int commanderRaw = CautionInformationKnown(current.ReadArmyUnitCommanderId(new(physical)), "group_first_Army_commander");
            int person = CautionInformationKnown((commanderRaw <= 6000
                ? current.ReadPersonLookupReference(commanderRaw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
                "group_first_commander_pointer");
            if (CautionInformationKnown(current.ReadPersonNativeValidity(person), "group_first_commander_validity"))
                districtRaw = CautionInformationKnown(current.ReadPersonDistrictSlot(person), "group_first_commander_District");
        }
        int district = CautionInformationKnown(current.ReadDistrictTableReference(districtRaw < 52 ? districtRaw : 0)?.PhysicalSlot,
            "group_District_pointer");
        int forceRaw = CautionInformationKnown(current.ReadDistrictForceSlot(new(district)), "group_District_Force");
        int force = CautionInformationKnown(current.ReadForceTableReference(forceRaw < 52 ? forceRaw : 0)?.PhysicalSlot,
            "group_Force_pointer");
        return new(WorldEntityKind.Force, force);
    }

    private static ArmyCautionRelation CautionInformationRelation(GameState state, int relation)
    {
        if ((uint)relation >= 1001) throw CautionInformationMissing("relation_identity");
        return (state.ReadArmyLifecycle()?.ArmyCautions ?? throw CautionInformationMissing("relations_owner")).GetRelation(relation);
    }

    private static ArmyUnitReference CautionInformationArmyReference(GameState state, int relation)
    {
        var target = CautionInformationRelation(state, relation).Unit;
        if (target.Kind == ArmyCautionUnitKind.Unknown) throw CautionInformationMissing("relation_Army");
        if (target.Kind is ArmyCautionUnitKind.None or ArmyCautionUnitKind.Expired) return new(0);
        int id = target.UnitId ?? throw CautionInformationMissing("relation_Army_identity");
        // A retired domain target never binds to a new unit reusing its old slot.
        return new(WorldIndexContext.FindArmyPhysical(state, id) ?? 0);
    }

    private static IReadOnlyList<int?> CautionInformationMembers(GameState state, int group)
    {
        if ((uint)group >= 501) throw CautionInformationMissing("group_identity");
        var indexes = state.ReadArmyLifecycle()?.Indexes ?? throw CautionInformationMissing("indexes_owner");
        return indexes.CautionGroupMembers.TryGetValue(group, out var members) && members is not null
            ? members : throw CautionInformationMissing("group_members_owner");
    }

    private static T CautionInformationKnown<T>(T? value, string field) where T : struct =>
        value ?? throw CautionInformationMissing(field);
    private static InvalidDataException CautionInformationMissing(string field) => new("original_ai.caution_information." + field);
}
