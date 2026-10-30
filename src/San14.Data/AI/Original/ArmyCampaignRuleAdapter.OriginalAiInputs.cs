using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal OriginalAiEntity? ReadOriginalAiMyselfDistrict(SourceWorldContext current, OriginalAiActor actor)
    {
        if (actor.AgentKind is null) throw OriginalAiInputMissing("actor_AgentType");
        if (actor.AgentKind == OriginalAiAgentKind.Army)
        {
            int physical = actor.ArmyPhysicalSlot ?? throw OriginalAiInputMissing("actor_Army_physical_identity");
            int raw = current.ReadArmyUnitCommanderId(new(physical)) ?? throw OriginalAiInputMissing("actor_Army_Commander12");
            int person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
                ?? throw OriginalAiInputMissing("actor_Army_Commander_pointer");
            int district = (current.ReadPersonNativeValidity(person) ?? throw OriginalAiInputMissing("actor_Army_Commander_validity"))
                ? current.ReadPersonDistrictSlot(person) ?? throw OriginalAiInputMissing("actor_Army_District") : 0;
            return OriginalAiDistrictEntity(current, district);
        }
        if (actor.AgentKind == OriginalAiAgentKind.Troops)
            return OriginalAiDistrictEntity(current, ReadOriginalAiTroopsDistrictCurrent(current,
                actor.Troops ?? throw OriginalAiInputMissing("actor_Troops_reference")));
        if (actor.AgentKind == OriginalAiAgentKind.Force) return null;
        if (actor.AgentKind != OriginalAiAgentKind.District) throw OriginalAiInputMissing("actor_AgentType_" + actor.AgentKind);
        int requested = actor.DistrictId ?? throw OriginalAiInputMissing("actor_District_identity");
        return OriginalAiDistrictEntity(current, requested);
    }

    internal OriginalAiEntity? ReadOriginalAiMyselfForce(SourceWorldContext current, OriginalAiActor actor)
    {
        if (actor.AgentKind is null) throw OriginalAiInputMissing("actor_AgentType");
        int requested;
        if (actor.AgentKind == OriginalAiAgentKind.Army)
        {
            int physical = actor.ArmyPhysicalSlot ?? throw OriginalAiInputMissing("actor_Army_physical_identity");
            return new(WorldEntityKind.Force, current.ReadArmyUnitForceReference(new(physical))?.PhysicalSlot
                ?? throw OriginalAiInputMissing("actor_Army_Force_pointer"));
        }
        if (actor.AgentKind == OriginalAiAgentKind.Troops)
            requested = ReadOriginalAiTroopsForceCurrent(current,
                actor.Troops ?? throw OriginalAiInputMissing("actor_Troops_reference"));
        else if (actor.AgentKind == OriginalAiAgentKind.Force) requested = actor.ForceId;
        else if (actor.AgentKind == OriginalAiAgentKind.District)
        {
            var district = ReadOriginalAiMyselfDistrict(current, actor)
                ?? throw OriginalAiInputMissing("actor_District_pointer");
            // B5E30 reads District10 without a district native-validity check.
            requested = current.ReadDistrictForceId(district.Id) ?? throw OriginalAiInputMissing("actor_District_Force");
        }
        else throw OriginalAiInputMissing("actor_AgentType_" + actor.AgentKind);
        return OriginalAiForceEntity(current, requested);
    }

    internal int ReadOriginalAiGameInformation(GameState state, int mode)
    {
        if (mode == 3)
            return unchecked((state.CampaignEventWorld ?? throw OriginalAiInputMissing("GameInfo3_World454_owner")).ElapsedPeriodOffset +
                (int)(state.NativeMapSeason ?? throw OriginalAiInputMissing("GameInfo3_advanced_periods")).AdvancedPeriods);
        var date = (state.NativeMapSeason ?? throw OriginalAiInputMissing("GameInfo_World34_date")).ThirdDate;
        return mode switch
        {
            0 => unchecked((short)date.Year), 1 => unchecked((sbyte)date.Month), 2 => unchecked((sbyte)date.Day),
            _ => throw OriginalAiInputMissing("GameInfo_mode_" + mode)
        };
    }

    internal bool ReadOriginalAiDistrictValidity(SourceWorldContext current, int district)
        => current.ReadDistrictNativeValidity(district) ?? throw OriginalAiInputMissing("District_native_validity");
    internal bool ReadOriginalAiForceValidity(SourceWorldContext current, int force)
        => current.ReadForceNativeValidity(new(force)) ?? throw OriginalAiInputMissing("Force_native_validity");

    internal int ReadOriginalAiDistrictScalar(SourceWorldContext current, int district, int mode)
    {
        // 22DD20 is the physical ordinal in the district table, not its force-local number.
        if ((uint)district >= 52) throw OriginalAiInputMissing("DistrictInfo_physical_identity");
        return mode switch
        {
            0 => district,
            1 => unchecked((sbyte)(current.ReadDistrictRawByte(district, 0x16) ?? throw OriginalAiInputMissing("DistrictInfo1_number"))),
            5 => current.ReadDistrictRawByte(district, 0x14) ?? throw OriginalAiInputMissing("DistrictInfo5_books"),
            _ => throw OriginalAiInputMissing("DistrictInfo_scalar_mode_" + mode)
        };
    }

    internal OriginalAiEntity? ReadOriginalAiDistrictEntity(SourceWorldContext current, int district, int mode)
    {
        if ((uint)district >= 52) throw OriginalAiInputMissing("DistrictInfo_physical_identity");
        return mode switch
        {
            2 => OriginalAiForceEntity(current, current.ReadDistrictForceId(district) ?? throw OriginalAiInputMissing("DistrictInfo2_Force")),
            4 => OriginalAiPersonEntity(current, current.ReadDistrictLeader(district) ?? throw OriginalAiInputMissing("DistrictInfo4_leader")),
            _ => throw OriginalAiInputMissing("DistrictInfo_entity_mode_" + mode)
        };
    }

    internal int ReadOriginalAiForceIdentity(SourceWorldContext current, int force)
        => (uint)force < 52 ? force : throw OriginalAiInputMissing("ForceInfo0_physical_identity");
    internal OriginalAiEntity? ReadOriginalAiForceRuler(SourceWorldContext current, int force)
    {
        int physical = ReadOriginalAiForceIdentity(current, force);
        ushort raw = current.ReadForceRecord(physical)?.RawRuler10 ?? throw OriginalAiInputMissing("ForceInfo2_ruler");
        return OriginalAiPersonEntity(current, raw);
    }

    internal IReadOnlyList<OriginalAiEntity> ReadOriginalAiForceAssociationPeople(SourceWorldContext current, int force)
    {
        int physical = ReadOriginalAiForceIdentity(current, force);
        var people = new List<OriginalAiEntity>(3);
        for (int index = 0; index < 3; ++index)
        {
            ushort raw = current.ReadForceAssociationPersonId(new(physical), index)
                ?? throw OriginalAiInputMissing("ForceInfo37_association_person_" + index);
            if (raw > 6000) throw OriginalAiInputMissing("ForceInfo37_out_of_range_person_receiver");
            var reference = current.ReadPersonLookupReference(raw) ?? throw OriginalAiInputMissing("ForceInfo37_person_pointer");
            if (reference.PhysicalSlot is not int person) continue;
            if (!(current.ReadPersonNativeValidity(person) ?? throw OriginalAiInputMissing("ForceInfo37_person_validity"))) continue;
            int identity = current.ReadPersonIdentity(person) ?? throw OriginalAiInputMissing("ForceInfo37_person_identity");
            people.Add(new(WorldEntityKind.Officer, identity));
        }
        return people;
    }

    internal int ReadOriginalAiRandom(GameState state, SourceWorldContext current, int bound,
        int first, int second, int third, int fourth)
    {
        if (bound < 2) return 0;
        var world = ReadArmyDestructionSampler(state, current) ?? throw OriginalAiInputMissing("Random_World450_45C");
        return TacticEffectSourceSamplerMath.Sample(bound, 1, first, second, third, fourth, world);
    }

    private static OriginalAiEntity? OriginalAiDistrictEntity(SourceWorldContext current, int requested)
    {
        var reference = current.ReadDistrictTableReference(requested is >= 0 and <= 51 ? requested : 0)
            ?? throw OriginalAiInputMissing("District_table_pointer");
        return reference.PhysicalSlot is int physical ? new(WorldEntityKind.District, physical) : null;
    }
    private static OriginalAiEntity? OriginalAiForceEntity(SourceWorldContext current, int requested)
    {
        var reference = current.ReadForceTableReference(requested is >= 0 and <= 51 ? requested : 0)
            ?? throw OriginalAiInputMissing("Force_table_pointer");
        return reference.PhysicalSlot is int physical ? new(WorldEntityKind.Force, physical) : null;
    }
    private static OriginalAiEntity? OriginalAiPersonEntity(SourceWorldContext current, ushort requested)
    {
        // The alternate original out-of-range receiver is not interchangeable with lookup0.
        if (requested > 6000) throw OriginalAiInputMissing("Person_out_of_range_receiver");
        var reference = current.ReadPersonLookupReference(requested)
            ?? throw OriginalAiInputMissing("Person_lookup_pointer");
        if (reference.PhysicalSlot is not int physical) return null;
        int identity = current.ReadPersonIdentity(physical) ?? throw OriginalAiInputMissing("Person_identity");
        return new(WorldEntityKind.Officer, identity);
    }
    private static InvalidDataException OriginalAiInputMissing(string key) => new("original_ai.ancestor_input." + key);
}
