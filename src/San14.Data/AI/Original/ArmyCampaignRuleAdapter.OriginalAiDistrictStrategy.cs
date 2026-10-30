using System.Collections.Immutable;
using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal IReadOnlyList<OriginalAiEntity> CaptureOriginalAiDistrictCautionGroupsCurrent(
        GameState state, SourceWorldContext current, int district)
    {
        var owner = state.ReadArmyLifecycle()?.ArmyCautions ?? throw AiDistrictStrategyMissing("cautions_owner");
        var selected = new List<OriginalAiEntity>();
        for (int slot = 0; slot < 501; slot++)
        {
            var at = current.ReadRegistryTableReference(RegistryKind.CautionTroops, slot)
                ?? throw AiDistrictStrategyMissing("caution_table_pointer");
            if (!AiDistrictStrategyKnown(current.ReadRegistryObjectNativeValidity(at), "caution_validity")) continue;
            int id = AiDistrictStrategyKnown(at.PhysicalSlot, "caution_identity");
            if (AiDistrictStrategyKnown(owner.GetGroupDistrictId(id), "caution_stored_District") == district)
                selected.Add(new(WorldEntityKind.ArmyCautionGroup, id));
        }
        return selected;
    }

    internal GameState CompleteOriginalAiDistrictStrategyCurrent(GameState state, SourceWorldContext current,
        int district, ImmutableArray<int>? retainedGroupIds)
    {
        // The source only runs cancellation when the actual blackboard object
        // remains present. A known absent object is not a generated empty list.
        if (retainedGroupIds is not { } retained) return state;
        var kept = retained.ToHashSet();
        var selected = new List<int>();
        var seen = new HashSet<int>();
        var cautions = state.ReadArmyLifecycle()?.ArmyCautions ?? throw AiDistrictStrategyMissing("post_cautions_owner");
        for (int slot = 0; slot < 501; slot++)
        {
            var at = current.ReadRegistryTableReference(RegistryKind.CautionTroops, slot)
                ?? throw AiDistrictStrategyMissing("post_caution_table_pointer");
            int id = AiDistrictStrategyKnown(at.PhysicalSlot, "post_caution_identity");
            // B5350's second pass has no native-validity test.
            if (AiDistrictStrategyKnown(cautions.GetGroupDistrictId(id), "post_caution_stored_District") != district || kept.Contains(id))
                continue;
            if (seen.Add(id)) selected.Add(id);
        }
        return CancelOriginalAiCautionGroupsStrategyCurrent(state, current, selected);
    }

    internal GameState CancelOriginalAiCautionGroupStrategyCurrent(GameState state, SourceWorldContext current, int cautionGroup)
        => CancelOriginalAiCautionGroupsStrategyCurrent(state, current, [cautionGroup]);

    private GameState CancelOriginalAiCautionGroupsStrategyCurrent(GameState state, SourceWorldContext current, IReadOnlyList<int> selected)
    {
        if (selected.Count == 0) return state;
        var arena = state.ReadArmyLifecycle() ?? throw AiDistrictStrategyMissing("cancel_lifecycle");
        var cautions = (arena.ArmyCautions ?? throw AiDistrictStrategyMissing("cancel_cautions")).Copy();
        var groups = (arena.ArmyGroups ?? throw AiDistrictStrategyMissing("cancel_military_groups")).Copy();
        var objectives = groups.Objectives;
        var pool = current.ReadOwnedCurrentListPool() ?? throw AiDistrictStrategyMissing("cancel_current_pool");
        var groupDistrictIds = cautions.GroupDistrictIds ?? throw AiDistrictStrategyMissing("cancel_District_owner");
        ArmyCautionSourceAdapter.InstallEligibility(current, cautions);
        foreach (int cautionGroup in selected)
        {
            for (int slot = 0; slot < 501; slot++)
            {
                int group = current.ReadStaticTableReference(0x7F8D8, slot)?.PhysicalSlot
                    ?? throw AiDistrictStrategyMissing("cancel_military_group_pointer");
                var target = groups.GetCautionTarget(group);
                if (!target.IsKnown) throw AiDistrictStrategyMissing("cancel_military_group_target");
                if (target.CautionGroupId != cautionGroup) continue;
                if (groups.CautionTargets is null) throw AiDistrictStrategyMissing("cancel_military_group_targets_owner");
                groups.CautionTargets[group] = new(true, 0);
                // Preserve the purpose kind and independent relay plan. The typed
                // objective is authoritative when present, so clear that owner too.
                if (objectives?.TryGetValue(group, out var objective) == true &&
                    objective.Purpose is { Kind: ArmyGroupPurposeKind.InterceptCautionGroup } purpose)
                    objectives = objectives.SetItem(group, objective with { Purpose = purpose with { Target = purpose.Target with { Id = 0 } } });
            }
            var members = pool.ExistingCautionGroupHandle(cautionGroup) ?? throw AiDistrictStrategyMissing("cancel_members_descriptor");
            if (current.ReadTemporaryHexListCount(members) is null) throw AiDistrictStrategyMissing("cancel_members_owner");
            current.ClearTemporaryHexList(members);
            cautions.GroupLabels.Remove(cautionGroup);
            groupDistrictIds[cautionGroup] = 0;
            cautions = ArmyCautionGroupPlanningSource.Reset(cautions, cautionGroup);

            var descriptor = current.ReadRegistryDescriptor(RegistryKind.CautionTroops)
                ?? throw AiDistrictStrategyMissing("cancel_registry");
            var node = current.ReadRegistryHead(descriptor) ?? throw AiDistrictStrategyMissing("cancel_registry_head");
            while (node.Identity.HasValue)
            {
                var next = current.ReadRegistryNodeNext(node) ?? throw AiDistrictStrategyMissing("cancel_registry_next");
                var payload = current.ReadRegistryNodePayload(node) ?? throw AiDistrictStrategyMissing("cancel_registry_payload");
                if (payload.Kind == RegistryKind.CautionTroops && payload.PhysicalSlot == cautionGroup)
                    current.UnlinkArmyReturnTraitNode(descriptor, node);
                node = next;
            }
        }
        groups = groups with { Objectives = objectives };
        // This actual business publication exports its changed current indexes
        // once for the complete District tail, not after every cancelled group.
        return state.WithArmyLifecycle(arena with { ArmyCautions = cautions, ArmyGroups = groups, Indexes = pool.ProjectOwnedRoles() });
    }

    private static T AiDistrictStrategyKnown<T>(T? value, string field) where T : struct =>
        value ?? throw AiDistrictStrategyMissing(field);
    private static InvalidDataException AiDistrictStrategyMissing(string field) => new("original_ai.district_strategy." + field);
}
