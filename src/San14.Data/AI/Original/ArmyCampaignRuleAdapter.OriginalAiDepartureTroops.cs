using System.Collections.Immutable;
using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

internal sealed record OriginalAiDepartureTroopsApplication(GameState State, int NativeReturn,
    OriginalAiTroopsReference? Group);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal UnitState ReadOriginalAiDepartureArmyImage(GameState state, int unitId)
    {
        if (state.Units.TryGetValue(unitId, out var unit)) return unit;
        return state.ReadArmyLifecycle()?.Receivers.FindUnit(unitId)?.PendingImage
            ?? throw DepartureTroopsMissing("Army_image." + unitId);
    }

    private int DepartureTroopsPhysical(GameState state, int id) => id == 0 ? 0 :
        DepartureTroopsNeed(ReadOriginalAiDepartureArmyImage(state, id).NativeArmyOrigin?.NativeSlotIndex, "Army_receiver");

    internal bool CanCreateOriginalAiDepartureTroopsCurrent(GameState state, IReadOnlyList<int> orderedArmyUnitIds)
    {
        if (orderedArmyUnitIds.Count == 0) return false;
        var lifecycle = state.ReadArmyLifecycle() ?? throw DepartureTroopsMissing("Army_lifecycle");
        foreach (int id in orderedArmyUnitIds)
        {
            int physical = DepartureTroopsPhysical(state, id);
            byte membership = DepartureTroopsNeed((id == 0 ? null : ReadOriginalAiDepartureArmyImage(state, id).NativeMapUiByte13C)
                ?? lifecycle.Slots[physical].MapUiByte13C, "Army13C");
            if (membership != 0) return false;
        }
        return true;
    }

    internal OriginalAiDepartureTroopsApplication ApplyOriginalAiDepartureTroopsCurrent(GameState state,
        SourceWorldContext current, IReadOnlyList<int> orderedArmyUnitIds, int rawPurpose, ArmyGroupPurposeTarget? target)
    {
        if (!CanCreateOriginalAiDepartureTroopsCurrent(state, orderedArmyUnitIds)) return new(state, 0, null);
        var arena = state.ReadArmyLifecycle() ?? throw DepartureTroopsMissing("Army_lifecycle");
        var groups = (arena.ArmyGroups ?? throw DepartureTroopsMissing("group_owner")).Copy();
        var pool = current.ReadOwnedCurrentListPool() ?? throw DepartureTroopsMissing("current_pool");
        int[] roster = ArmyGroupSourceAdapter.Roster(arena, current);
        var physicalById = new Dictionary<int, int>();
        var excluded = new HashSet<int>();
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw DepartureTroopsMissing("Army_registry"))
        {
            int physical = DepartureTroopsNeed(entry?.PhysicalSlot, "registry_Army_pointer");
            int id = ArmyGroupSourceAdapter.UnitId(arena, physical);
            physicalById[id] = physical;
            if (DepartureTroopsNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "Army_exclusion")) excluded.Add(id);
        }
        var commanders = new Dictionary<int, ArmyGroupCommander>();
        ArmyGroupCommander Commander(int id)
        {
            if (!commanders.TryGetValue(id, out var facts))
            {
                int physical = physicalById.TryGetValue(id, out int at) ? at
                    : DepartureTroopsPhysical(state, id);
                commanders[id] = facts = ArmyGroupSourceAdapter.Commander(current, physical);
            }
            return facts;
        }
        int[] Members(int group) => ReadOriginalAiTroopsMembersCurrent(current, new(group))
            .Select(member => ArmyGroupSourceAdapter.UnitId(arena, DepartureTroopsNeed(member.PhysicalSlot, "member_pointer"))).ToArray();
        int Person(int id) => DepartureTroopsNeed(current.ReadPersonLookupReference(id is >= 0 and <= 6000 ? id : 0)?.PhysicalSlot, "Person_pointer");
        int? LeaderPointer(ArmyGroupOfficerReference leader) => leader.Kind switch
        {
            ArmyGroupOfficerKind.NoOfficer => null,
            ArmyGroupOfficerKind.DefaultOfficer => Person(0),
            ArmyGroupOfficerKind.Officer => Person(leader.OfficerId ?? throw DepartureTroopsMissing("leader_identity")),
            _ => throw DepartureTroopsMissing("leader_kind")
        };
        int CommanderPointer(int id) => Person(DepartureTroopsNeed(current.ReadArmyUnitCommanderId(
            new(DepartureTroopsPhysical(state, id))), "Commander12"));
        void Assign(int id, int group)
        {
            groups.UnitAssignments[id] = new(true, group == 0 ? null : group);
            int physical = DepartureTroopsPhysical(state, id);
            current.SetStartupArmyTroopsSlot58(physical, checked((ushort)group));
        }

        foreach (int id in orderedArmyUnitIds)
        {
            int physical = DepartureTroopsPhysical(state, id);
            int oldGroup = current.ReadOriginalAiArmyTroopsCurrent(new(physical)).GroupId;
            var members = Members(oldGroup);
            if (members.Length == 1)
            {
                //22A3D0 is member COUNT, not purpose kind. 282990 clears
                //all reached members, unregisters the AI context and227F00.
                if (oldGroup != 0)
                {
                    foreach (int member in members) Assign(member, 0);
                    groups.Leaders[oldGroup] = new(true, ArmyGroupSourceAdapter.Officer(current, 0));
                    groups.CautionTargets?.Remove(oldGroup);
                    groups = groups.ResetObjective(oldGroup);
                }
                continue;
            }
            var leader = groups.GetLeader(oldGroup);
            if (!leader.IsKnown) throw DepartureTroopsMissing("old_group_leader");
            if (LeaderPointer(leader.Officer) == CommanderPointer(id))
            {
                //035990 takes the first DIFFERENT Person pointer, then stops
                //even if its raw ID cannot be written. No first-eligible rule.
                foreach (int member in members)
                {
                    int commander = CommanderPointer(member);
                    if (commander == LeaderPointer(leader.Officer)) continue;
                    ushort raw = DepartureTroopsNeed(current.ReadArmyUnitCommanderId(new(
                        DepartureTroopsPhysical(state, member))), "replacement_Commander12");
                    if (raw < 6000) groups.Leaders[oldGroup] = new(true, ArmyGroupSourceAdapter.Officer(current, raw));
                    break;
                }
            }
            //2E7DE0 does not remove a singleton or its retained leader. The
            //leader replacement above is intentionally before this fresh read.
            members = Members(oldGroup);
            var reachedLeader = groups.GetLeader(oldGroup);
            if (!reachedLeader.IsKnown) throw DepartureTroopsMissing("reached_old_group_leader");
            if (members.Length != 1 && members.Contains(id) && LeaderPointer(reachedLeader.Officer) != CommanderPointer(id)) Assign(id, 0);
        }

        //2D9630 selects the first actual nonzero native-invalid group. It
        //does not reset the selected receiver's retained purpose/relay/context.
        int selected = 0;
        for (int group = 1; group <= 500; group++)
        {
            var district = ArmyGroupRules.GetDistrict(groups, group, roster, excluded, id => Commander(id))
                ?? throw DepartureTroopsMissing("allocation_Group_District");
            if (!district.IsRecognized) { selected = group; break; }
        }
        if (selected == 0) throw DepartureTroopsMissing("allocation_free_group");
        foreach (int id in orderedArmyUnitIds)
        {
            int physical = DepartureTroopsPhysical(state, id);
            if (DepartureTroopsNeed(current.ReadArmyUnitNativeValidity(new(physical)), "selected_Army_validity")) Assign(id, selected);
        }
        if (groups.OriginalAiContexts is null || !groups.OriginalAiContexts.ContainsKey(selected) &&
            groups.OriginalAiContextPrototype is null)
            throw DepartureTroopsMissing("AI_registration_owner");
        groups = groups.RegisterOriginalAiContext(selected);

        int firstPhysical = DepartureTroopsPhysical(state, orderedArmyUnitIds[0]);
        if (DepartureTroopsNeed(current.ReadArmyUnitNativeValidity(new(firstPhysical)), "first_Army_validity"))
        {
            ushort commander = DepartureTroopsNeed(current.ReadArmyUnitCommanderId(new(firstPhysical)), "first_Commander12");
            if (commander < 6000) groups.Leaders[selected] = new(true, ArmyGroupSourceAdapter.Officer(current, commander));
        }
        var rows = groups.Objectives ?? ImmutableDictionary<int, ArmyGroupObjectiveState>.Empty;
        var old = rows.GetValueOrDefault(selected);
        var actualTarget = target ?? new(ArmyGroupPurposeTargetKind.Retained,
            old?.Purpose?.Target.Id ?? throw DepartureTroopsMissing("retained_target16"));
        var purpose = new ArmyGroupPurpose((ArmyGroupPurposeKind)Math.Clamp(rawPurpose, 0, ushort.MaxValue), actualTarget);
        groups = groups with { Objectives = rows.SetItem(selected, new(purpose, old?.RelayHexIds)) };
        var result = state.WithArmyLifecycle(arena with { ArmyGroups = groups, Indexes = pool.ProjectOwnedRoles() });
        var finalDistrict = ArmyGroupRules.GetDistrict(groups, selected, roster, excluded, id => Commander(id))
            ?? throw DepartureTroopsMissing("created_Group_District");
        return new(result, 1, finalDistrict.IsRecognized ? new(selected) : null);
    }

    internal ArmyGroupPurposeTarget? PrepareOriginalAiDepartureTroopsTargetCurrent(GameState state, SourceWorldContext current,
        int purpose, OriginalAiEntity? entity, OriginalAiHex? hex)
    {
        if (purpose is 0 or 2 or 8 or 9 or 10 or 11 or 12 or 22)
        {
            if (entity is not { Kind: WorldEntityKind.City or WorldEntityKind.Gate }) throw DepartureTroopsMissing("target_Foothold_variable");
            var selected = entity.Value.Kind == WorldEntityKind.City
                ? current.ReadCityTableReference(entity.Value.Id is >= 0 and <= 51 ? entity.Value.Id : 0)
                : current.ReadGateTableReference(entity.Value.Id is >= 52 and <= 61 ? entity.Value.Id - 51 : 0);
            return new(ArmyGroupPurposeTargetKind.Foothold, DepartureTroopsNeed(current.ReadBaseObjectLink(
                selected ?? throw DepartureTroopsMissing("target_Foothold_pointer")), "target_Foothold4E"));
        }
        if (purpose is 1 or 19)
        {
            if (entity is null) return new(ArmyGroupPurposeTargetKind.CautionGroup, 0); //local pointer starts null.
            if (entity.Value.Kind != WorldEntityKind.ArmyCautionGroup) throw DepartureTroopsMissing("target_Caution_variable");
            int slot = DepartureTroopsNeed(current.ReadStaticTableReference(0x80BA8,
                entity.Value.Id is >= 0 and <= 500 ? entity.Value.Id : 0)?.PhysicalSlot, "target_Caution_pointer");
            bool valid = DepartureTroopsNeed(current.ReadRegistryObjectNativeValidity(new(RegistryKind.CautionTroops, slot)), "target_Caution_validity");
            return new(ArmyGroupPurposeTargetKind.CautionGroup, valid ? slot : 0);
        }
        if (purpose is 3 or 13 or 14 or 15 or 16 or 21 or 23)
        {
            if (entity is not { Kind: WorldEntityKind.Area }) throw DepartureTroopsMissing("target_Area_variable");
            return new(ArmyGroupPurposeTargetKind.Area, DepartureTroopsNeed(current.ReadAreaTableReference(
                entity.Value.Id is >= 0 and <= 500 ? entity.Value.Id : 0)?.PhysicalSlot, "target_Area_pointer"));
        }
        if (purpose is 4 or 20)
        {
            if (entity is not { Kind: WorldEntityKind.MapObject }) throw DepartureTroopsMissing("target_Object_variable");
            int physical = entity.Value.Id == 0 ? 0 : (current.CurrentMapObjectDirectory
                ?? throw DepartureTroopsMissing("target_Object_directory")).PhysicalOf(new(entity.Value.Id));
            return new(ArmyGroupPurposeTargetKind.MapObject, Math.Clamp(physical, 0, ushort.MaxValue));
        }
        if (purpose is 5 or 6 or 7)
        {
            if (hex is null) throw DepartureTroopsMissing("target_Hex_variable");
            int physical = DepartureTroopsNeed(current.ReadHexTableReference(hex.Value.Id is >= 0 and < 48400 ? hex.Value.Id : 0)?.PhysicalSlot,
                "target_Hex_pointer");
            return new(ArmyGroupPurposeTargetKind.Hex, Math.Clamp(physical, 0, ushort.MaxValue));
        }
        return null; //17/18 and every other raw purpose do not write16.
    }

    private static T DepartureTroopsNeed<T>(T? value, string field) where T : struct => value ?? throw DepartureTroopsMissing(field);
    private static InvalidDataException DepartureTroopsMissing(string field) => new("original_ai.departure_troops." + field);
}
