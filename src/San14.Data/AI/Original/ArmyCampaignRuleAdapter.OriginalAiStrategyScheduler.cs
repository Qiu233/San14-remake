using System.Collections.Immutable;
using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

internal readonly record struct OriginalAiPlayerGroupMaintenance(GameState State, ImmutableArray<int> ResetGroupIds);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal int ReadOriginalAiStrategyPlayerForceCurrent(GameState state, SourceWorldContext current)
        => AiStrategyKnown(OwnedCurrentPlayerForce.ReadCurrentReference(state, current)?.PhysicalSlot, "current_player_Force");

    internal int ReadOriginalAiStrategyPrimaryDistrictCurrent(SourceWorldContext current, int force)
        => AiStrategyKnown(ForcePrimaryDistrictSource.Resolve(current,
            current.ReadForceTableReference(force < 52 ? force : 0) ?? throw AiStrategyMissing("Force_pointer")).PhysicalSlot,
            "primary_District");

    internal bool ReadOriginalAiStrategyWholeControlCurrent(SourceWorldContext current)
        => (AiStrategyKnown(current.ReadWorldTransferServiceFlags(), "World_whole_control_flag") & 0x100) != 0;

    internal IReadOnlyList<int> CaptureOriginalAiStrategyDistrictsCurrent(SourceWorldContext current, int force)
        => current.ReadStartupDistricts(new(StartupModelKind.Force, force))
            .Select(row => AiStrategyIdentity(row, StartupModelKind.District)).ToArray();

    internal OriginalAiTroopsReference? ReadOriginalAiStrategyGroupAtCurrent(SourceWorldContext current, int district, int slot)
    {
        var pointer = current.ReadStaticTableReference(0x7F8D8, slot) ?? throw AiStrategyMissing("military_group_table");
        int group = AiStrategyKnown(pointer.PhysicalSlot, "military_group_identity");
        return current.ReadStartupTroopsDistrict(new(StartupModelKind.Troops, group)) == district ? new(group) : null;
    }

    internal bool ReadOriginalAiStrategyHasActiveMemberCurrent(GameState state, SourceWorldContext current, OriginalAiTroopsReference group)
    {
        var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        foreach (var member in ReadOriginalAiTroopsMembersCurrent(current, group))
        {
            int physical = AiStrategyKnown(member.PhysicalSlot, "member_identity");
            if (!AiStrategyKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "member_exclusion") &&
                !AiStrategyKnown(grouping.IsAtHome2C96E0(physical), "member_at_home")) return true;
        }
        return false;
    }

    internal bool ShouldSkipOriginalAiStrategyGroupCurrent(GameState state, SourceWorldContext current,
        int force, OriginalAiTroopsReference group, bool playerDelegation)
    {
        var members = ReadOriginalAiTroopsMembersCurrent(current, group);
        if (playerDelegation || ReadOriginalAiStrategyWholeControlCurrent(current))
        {
            foreach (var member in members)
            {
                if (AiStrategyKnown(current.ReadArmyReturnUnitStatus10(member), "group_member_status") != 2) continue;
                int unitId = ArmyDestructionQueue.ResolveUnitId(state, member.PhysicalSlot)
                    ?? throw AiStrategyMissing("group_member_domain_identity");
                var actor = new OriginalAiActor(force, null, OriginalAiAgentKind.Army)
                { ArmyId = unitId, ArmyPhysicalSlot = member.PhysicalSlot };
                int district = (ReadOriginalAiMyselfDistrict(current, actor) ?? throw AiStrategyMissing("member_District_pointer")).Id;
                int primary = ReadOriginalAiStrategyPrimaryDistrictCurrent(current, force);
                var objective = (state.ReadArmyLifecycle()?.ArmyGroups ?? throw AiStrategyMissing("group_objective_owner"))
                    .Objectives?.GetValueOrDefault(group.GroupId)?.Purpose ?? throw AiStrategyMissing("group_purpose");
                if (district != primary && objective.Kind == ArmyGroupPurposeKind.Withdraw && objective.Target.Id == 0) return true;
            }
        }
        // Source checks the actual member13C bytes in a separate pass after
        // the transport/primary-District skip, not a guessed generic busy flag.
        return TroopsInfoExpeditionMember(state, current, group);
    }

    internal bool IsOriginalAiStrategyArmyEligibleCurrent(GameState state, SourceWorldContext current, int physical)
    {
        if (AiStrategyKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "Army_exclusion")) return false;
        var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        return !AiStrategyKnown(grouping.IsAtHome2C96E0(physical), "Army_at_home");
    }

    internal bool IsOriginalAiStrategyPrimaryDistrictBlockedCurrent(GameState state, SourceWorldContext current, int district)
    {
        int player = ReadOriginalAiStrategyPlayerForceCurrent(state, current);
        return district == ReadOriginalAiStrategyPrimaryDistrictCurrent(current, player) &&
            !ReadOriginalAiStrategyWholeControlCurrent(current);
    }

    internal OriginalAiPlayerGroupMaintenance MaintainOriginalAiPlayerStrategyGroupsCurrent(GameState state,
        SourceWorldContext current, int force)
    {
        var working = state;
        var reset = ImmutableArray.CreateBuilder<int>();
        foreach (int district in CaptureOriginalAiStrategyDistrictsCurrent(current, force))
        {
            bool managed = IsOriginalAiPlayerManagedDistrictCurrent(working, current, force, district);
            for (int slot = 0; slot < 501; slot++)
            {
                if (ReadOriginalAiStrategyGroupAtCurrent(current, district, slot) is not { } group) continue;
                if (TroopsInfoExpeditionMember(working, current, group)) continue;
                var arena = working.ReadArmyLifecycle() ?? throw AiStrategyMissing("player_lifecycle");
                var groups = arena.ArmyGroups ?? throw AiStrategyMissing("player_groups");
                if (managed)
                {
                    // 440120 writes the known default lookup person, which is
                    // distinct from the normal empty-group no-officer reset.
                    var next = groups.Copy(); next.Leaders[group.GroupId] = new(true, ArmyGroupSourceAdapter.Officer(current, 0));
                    working = working.WithArmyLifecycle(arena with { ArmyGroups = next });
                    continue;
                }
                var leader = groups.GetLeader(group.GroupId);
                if (!leader.IsKnown) throw AiStrategyMissing("player_group_leader");
                if (AiStrategyGroupLeaderValid(current, leader.Officer)) continue;
                var members = ReadOriginalAiTroopsMembersCurrent(current, group);
                if (members.Count == 0) throw AiStrategyMissing("player_group_first_member");
                var first = members[0];
                ushort commander = AiStrategyKnown(current.ReadArmyUnitCommanderId(first), "player_first_commander");
                int person = AiStrategyKnown((commander <= 6000 ? current.ReadPersonLookupReference(commander)
                    : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "player_first_person_pointer");
                if (!AiStrategyKnown(current.ReadPersonNativeValidity(person), "player_first_person_validity") ||
                    AiStrategyKnown(current.ReadArmyReturnUnitStatus10(first), "player_first_status") == 2) continue;
                var changed = groups.Copy();
                changed.Leaders[group.GroupId] = new(true, ArmyGroupSourceAdapter.Officer(current, commander));
                var objective = changed.Objectives?.GetValueOrDefault(group.GroupId) ?? new(null, null);
                int hex = AiStrategyKnown(current.ReadArmyUnitRawHex2A(first), "player_first_Hex");
                changed = changed with { Objectives = (changed.Objectives ?? ImmutableDictionary<int, ArmyGroupObjectiveState>.Empty)
                    .SetItem(group.GroupId, objective with { Purpose = new(ArmyGroupPurposeKind.HoldAtHex,
                        new(ArmyGroupPurposeTargetKind.Hex, hex)) }) };
                working = working.WithArmyLifecycle(arena with { ArmyGroups = changed });
                reset.Add(group.GroupId);
            }
        }
        return new(working, reset.ToImmutable());
    }

    private bool IsOriginalAiPlayerManagedDistrictCurrent(GameState state, SourceWorldContext current, int playerForce, int district)
    {
        if (!AiStrategyKnown(current.ReadForceNativeValidity(new(playerForce)), "player_Force_validity")) return false;
        if (AiStrategyKnown(current.ReadDistrictForceSlot(new(district)), "managed_District_Force") != playerForce) return false;
        int raw = AiStrategyKnown(current.ReadDistrictLeader(district), "managed_District_leader");
        int person = AiStrategyKnown((raw <= 6000 ? current.ReadPersonLookupReference(raw)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "managed_leader_pointer");
        return AiStrategyKnown(current.ReadPersonNativeValidity(person), "managed_leader_validity") &&
            AiStrategyKnown(current.ReadPersonStatus(person), "managed_leader_status") == 1;
    }

    private static bool AiStrategyGroupLeaderValid(SourceWorldContext current, ArmyGroupOfficerReference leader)
    {
        int? person = leader.Kind switch
        {
            ArmyGroupOfficerKind.NoOfficer => null,
            ArmyGroupOfficerKind.DefaultOfficer => 0,
            ArmyGroupOfficerKind.Officer when leader.OfficerId is int id => current.ReadPersonLookupReference(id)?.PhysicalSlot,
            _ => throw AiStrategyMissing("group_leader_kind")
        };
        return person is int physical && AiStrategyKnown(current.ReadPersonNativeValidity(physical), "group_leader_validity");
    }

    private static int AiStrategyIdentity(StartupDataReference? reference, StartupModelKind kind)
        => reference is { Identity: int id } && reference.Kind == kind ? id : throw AiStrategyMissing(kind + "_identity");
    private static T AiStrategyKnown<T>(T? value, string field) where T : struct => value ?? throw AiStrategyMissing(field);
    private static InvalidDataException AiStrategyMissing(string field) => new("original_ai.strategy_scheduler." + field);
}
