using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The manager's registration order: registered Forces, their
    /// registered Districts, then the fixed group table. This captures existing
    /// owners only; it does not install a strategy or schedule group updates.</summary>
    internal IReadOnlyList<OriginalAiTroopsReference> CaptureOriginalAiTroopsActorsCurrent(
        GameState state, SourceWorldContext current, int forceId)
    {
        var result = new List<OriginalAiTroopsReference>();
        foreach (var forceReference in current.ReadStartupForces() ?? throw AiTroopsMissing("Force_registry"))
        {
            int force = AiTroopsIdentity(forceReference, StartupModelKind.Force, 52);
            if (force != forceId) continue;
            foreach (var district in current.ReadStartupDistricts(new(StartupModelKind.Force, force)))
            {
                int districtId = AiTroopsIdentity(district, StartupModelKind.District, 52);
                foreach (var group in current.ReadStartupTroops(new(StartupModelKind.District, districtId)))
                    result.Add(new(AiTroopsIdentity(group, StartupModelKind.Troops, 501)));
            }
        }
        return result;
    }

    /// <summary>ForceInfo34 collects the first occurrence of each member's
    /// group in the Force's Army iterator order. It differs from registration
    /// order and deliberately preserves a real group-zero placeholder.</summary>
    internal IReadOnlyList<OriginalAiTroopsReference> ReadOriginalAiForceTroopsCurrent(
        GameState state, SourceWorldContext current, int forceId)
    {
        var force = current.ReadForceTableReference(forceId is >= 0 and <= 51 ? forceId : 0)
            ?? throw AiTroopsMissing("Force_pointer");
        int physicalForce = AiTroopsKnown(force.PhysicalSlot, "Force_identity");
        var result = new List<OriginalAiTroopsReference>();
        var seen = new HashSet<int>();
        if (physicalForce is < 1 or > 51) return result;
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw AiTroopsMissing("Army_registry"))
        {
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                throw AiTroopsMissing("Army_pointer");
            var army = new ArmyUnitReference(physical);
            if (AiTroopsKnown(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army)), "Army_Force") != physicalForce)
                continue;
            if (AiTroopsKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "Army_exclusion"))
                continue;
            var group = current.ReadOriginalAiArmyTroopsCurrent(army);
            if (seen.Add(group.GroupId)) result.Add(group);
        }
        return result;
    }

    internal IReadOnlyList<ArmyUnitReference> ReadOriginalAiTroopsMembersCurrent(
        SourceWorldContext current, OriginalAiTroopsReference group)
    {
        AiTroopsCheck(group);
        var result = new List<ArmyUnitReference>();
        foreach (var army in current.ReadStartupArmyUnits(new(StartupModelKind.Troops, group.GroupId)))
            result.Add(new(AiTroopsIdentity(army, StartupModelKind.ArmyUnit, 501)));
        return result;
    }

    internal int ReadOriginalAiTroopsDistrictCurrent(SourceWorldContext current, OriginalAiTroopsReference group)
    {
        AiTroopsCheck(group);
        int raw = current.ReadStartupTroopsDistrict(new(StartupModelKind.Troops, group.GroupId));
        return AiTroopsDistrict(current, raw);
    }

    internal int ReadOriginalAiTroopsForceCurrent(SourceWorldContext current, OriginalAiTroopsReference group)
    {
        int district = ReadOriginalAiTroopsDistrictCurrent(current, group);
        return AiTroopsDistrictForce(current, district);
    }

    internal bool ReadOriginalAiTroopsValidityCurrent(SourceWorldContext current, OriginalAiTroopsReference group)
    {
        AiTroopsCheck(group);
        if (group.GroupId == 0) return false;
        int raw = current.ReadStartupTroopsDistrict(new(StartupModelKind.Troops, group.GroupId));
        return raw is >= 1 and <= 51;
    }

    internal OriginalAiActor? CreateOriginalAiTroopsActorCurrent(SourceWorldContext current, OriginalAiTroopsReference group)
    {
        AiTroopsCheck(group);
        if (group.GroupId == 0) return null;
        int raw = current.ReadStartupTroopsDistrict(new(StartupModelKind.Troops, group.GroupId));
        if (raw is < 1 or > 51) return null;
        int district = AiTroopsDistrict(current, raw);
        return new OriginalAiActor(AiTroopsDistrictForce(current, district), district,
            OriginalAiAgentKind.Troops) { Troops = group };
    }

    /// <summary>The ForceInfo personality owner is the commander with greatest
    /// full leadership, first in current group order on ties. It is unrelated
    /// to the group's separately maintained leader field.</summary>
    internal int ReadOriginalAiTroopsDispositionCurrent(GameState state, SourceWorldContext current,
        OriginalAiTroopsReference group)
    {
        int? selected = null;
        int maximum = int.MinValue;
        bool bound = false;
        foreach (var army in ReadOriginalAiTroopsMembersCurrent(current, group))
        {
            int raw = AiTroopsKnown(current.ReadArmyUnitCommanderId(army), "Commander_identity");
            int physical = AiTroopsKnown((raw <= 6000 ? current.ReadPersonLookupReference(raw)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Commander_pointer");
            int leadership = 0;
            if (AiTroopsKnown(current.ReadPersonNativeValidity(physical), "Commander_validity"))
            {
                if (!bound) { _baseCapacityInputs.BindCurrent(state, current); bound = true; }
                leadership = ReadOriginalAiCityDutyAbility(state, current,
                    AiTroopsKnown(current.ReadPersonIdentity(physical), "Commander_logical_identity"), 0);
            }
            if (leadership <= maximum) continue;
            maximum = leadership;
            selected = physical;
        }
        return selected is int person ? AiTroopsKnown(current.ReadAllegianceDisposition(person), "Commander_disposition") : 0;
    }

    private static int AiTroopsIdentity(StartupDataReference? reference, StartupModelKind kind, int count)
    {
        if (reference is not { Identity: int identity } || reference.Kind != kind || (uint)identity >= (uint)count)
            throw AiTroopsMissing(kind + "_identity");
        return identity;
    }
    private static void AiTroopsCheck(OriginalAiTroopsReference group)
    {
        if ((uint)group.GroupId > 500) throw AiTroopsMissing("Group_identity");
    }
    private static int AiTroopsDistrict(SourceWorldContext current, int raw) => AiTroopsKnown(
        (current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
            ?? throw AiTroopsMissing("District_pointer")).PhysicalSlot, "District_identity");
    private static int AiTroopsDistrictForce(SourceWorldContext current, int district)
    {
        int raw = AiTroopsKnown(current.ReadDistrictForceSlot(new(district)), "District_Force");
        return AiTroopsKnown((current.ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw AiTroopsMissing("Force_pointer")).PhysicalSlot, "Force_identity");
    }
    private static T AiTroopsKnown<T>(T? value, string field) where T : struct => value ?? throw AiTroopsMissing(field);
    private static InvalidDataException AiTroopsMissing(string field) => new("original_ai.troops." + field);
}

public sealed partial class SourceWorldContext
{
    /// <summary>The current Army-to-group lookup. The existing group assignment
    /// owner supplies Army58; out-of-range values select the real table head.</summary>
    internal OriginalAiTroopsReference ReadOriginalAiArmyTroopsCurrent(ArmyUnitReference army)
    {
        int physical = army.PhysicalSlot ?? throw new InvalidDataException("original_ai.troops.Army_identity");
        Check(physical, 501, "AI group Army physical slot");
        int raw = _startupArmyTroops58.GetValueOrDefault(physical)
            ?? throw new InvalidDataException("original_ai.troops.Army_group_assignment");
        var group = ReadStaticTableReference(0x7F8D8, raw <= 500 ? raw : 0)
            ?? throw new InvalidDataException("original_ai.troops.Group_pointer");
        return new(group.PhysicalSlot ?? throw new InvalidDataException("original_ai.troops.Group_identity"));
    }
}
