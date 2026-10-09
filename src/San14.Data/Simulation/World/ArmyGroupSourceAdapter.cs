using San14.Core;

namespace San14.Data;

/// <summary>Projects current source lookup facts into military group rules.
/// Source receiver slots never become saved group member identities.</summary>
internal static class ArmyGroupSourceAdapter
{
    internal static int[] Roster(WorldIndexesState indexes) =>
        (indexes.Registries.GetValueOrDefault(WorldEntityKind.Army)
            ?? throw new InvalidDataException("Current army roster is unknown."))
        .Select(x => x.Kind == WorldEntityKind.Army && x.Id is int id ? id
            : throw new InvalidDataException("Current army roster member is unknown.")).ToArray();

    internal static int UnitId(NativeArmyLifecycleState arena, int physical) =>
        arena.Slots[physical].ActiveUnitId ?? arena.Slots[physical].PendingImage?.Id
        ?? throw new InvalidDataException("Current group member has no logical unit identity.");

    internal static int[] Roster(NativeArmyLifecycleState arena, SourceWorldContext current) =>
        ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current)
        .Select(x => x.Kind == RegistryKind.Army && x.PhysicalSlot is int physical
            ? UnitId(arena, physical) : throw new InvalidDataException("Current army roster member is unknown.")).ToArray();

    internal static ArmyGroupOfficerReference Officer(SourceWorldContext current, ushort sourceId)
    {
        var lookup = (sourceId <= 6000 ? current.ReadPersonLookupReference(sourceId)
            : current.ReadTransferPersonTableReference(0))
            ?? throw new InvalidDataException("Current group commander lookup is unknown.");
        if (lookup.PhysicalSlot is not int physical) return ArmyGroupOfficerReference.None;
        if (physical == 0) return ArmyGroupOfficerReference.Default;
        int officerId = current.ReadPersonIdentity(physical)
            ?? throw new InvalidDataException("Current group commander identity is unknown.");
        return new(ArmyGroupOfficerKind.Officer, officerId);
    }

    internal static ArmyGroupCommander Commander(NativeArmyLifecycleState arena,
        SourceWorldContext current, int unitId)
    {
        int physical = arena.Slots.First(x => x.ActiveUnitId == unitId || x.PendingImage?.Id == unitId).PhysicalSlot;
        ushort sourceId = current.ReadArmyUnitCommanderId(new(physical))
            ?? throw new InvalidDataException("Current group commander is unknown.");
        var reference = Officer(current, sourceId);
        var person = (sourceId <= 6000 ? current.ReadPersonLookupReference(sourceId)
            : current.ReadTransferPersonTableReference(0))!;
        bool valid = person.PhysicalSlot is int personPhysical &&
            (current.ReadPersonNativeValidity(personPhysical)
                ?? throw new InvalidDataException("Current group commander validity is unknown."));
        int? district = valid ? current.ReadPersonDistrictSlot(person.PhysicalSlot!.Value)
            ?? throw new InvalidDataException("Current group commander district is unknown.") : null;
        bool recognizedDistrict = district is >= 1 and <= 51;
        return new(reference, valid, recognizedDistrict ? district : null, recognizedDistrict, sourceId < 6000);
    }

    internal static HashSet<int> Excluded(GameState state, NativeArmyLifecycleState arena,
        SourceWorldContext current, IReadOnlyList<int> roster)
    {
        var excluded = new HashSet<int>();
        foreach (int unitId in roster)
        {
            int physical = arena.Slots.First(x => x.ActiveUnitId == unitId || x.PendingImage?.Id == unitId).PhysicalSlot;
            if (OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical)
                ?? throw new InvalidDataException("Current group exclusion is unknown.")) excluded.Add(unitId);
        }
        return excluded;
    }

    internal static void RestoreAssignments(SourceWorldContext current, NativeArmyLifecycleState arena)
    {
        var groups = arena.ArmyGroups ?? throw new InvalidDataException("Current military groups are unknown.");
        foreach (var slot in arena.Slots)
        {
            int? unitId = slot.ActiveUnitId ?? slot.PendingImage?.Id;
            ushort? assignment;
            if (unitId is int id)
            {
                var member = groups.UnitAssignments.GetValueOrDefault(id)
                    ?? throw new InvalidDataException("Current group assignment is missing.");
                assignment = !member.IsKnown ? null : member.GroupId is null ? (ushort)0
                    : member.GroupId is >= 1 and <= 500 ? checked((ushort)member.GroupId.Value)
                    : throw new InvalidDataException("Group cannot be represented by the source presentation profile.");
            }
            else assignment = slot.GroupAssignmentReady switch
            {
                true => 0,
                null => null,
                false => null
            };
            current.SetStartupArmyTroopsSlot58(slot.PhysicalSlot, assignment);
        }
    }
}
