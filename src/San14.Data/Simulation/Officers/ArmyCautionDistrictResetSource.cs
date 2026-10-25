using San14.Core;

namespace San14.Data;

/// <summary>District reorganization's caution-group cancellation. All writes
/// use the caller's live list pool, including duplicate registry occurrences.</summary>
internal static class ArmyCautionDistrictResetSource
{
    internal static GameState ResetForDistrict(GameState state, SourceWorldContext current, int district)
    {
        var arena = state.ReadArmyLifecycle() ?? throw Missing("lifecycle");
        var cautions = arena.ArmyCautions ?? throw Missing("cautions");
        ArmyGroupsState? groups = arena.ArmyGroups;
        WorldIndexContext? pool = null;
        bool changed = false;

        //293490 visits every current caution pointer, checking validity before
        //its stored District14. It does not traverse the registered list here.
        for (int slot = 0; slot < 501; slot++)
        {
            var reference = current.ReadRegistryTableReference(RegistryKind.CautionTroops, slot)
                ?? throw Missing("caution_table");
            if (!(current.ReadRegistryObjectNativeValidity(reference) ?? throw Missing("caution_validity"))) continue;
            int physical = reference.PhysicalSlot ?? throw Missing("caution_pointer");
            int storedDistrict = cautions.GetGroupDistrictId(physical) ?? throw Missing("caution_District14");
            if (storedDistrict != district) continue;

            if (!changed)
            {
                cautions = cautions.Copy();
                groups = (groups ?? throw Missing("military_groups")).Copy();
                pool = current.ReadOwnedCurrentListPool() ?? throw Missing("current_pool");
                ArmyCautionSourceAdapter.InstallEligibility(current, cautions);
                changed = true;
            }

            //32B570 reads CTroops kind14/target16 even on inactive rows.
            //Keep kind1 when clearing its target, rather than resetting the
            //military group's leader, members or unrelated objective fields.
            for (int groupSlot = 0; groupSlot < 501; groupSlot++)
            {
                int group = current.ReadStaticTableReference(0x7F8D8, groupSlot)?.PhysicalSlot
                    ?? throw Missing("military_group_table");
                var objective = groups!.GetCautionTarget(group);
                if (!objective.IsKnown) throw Missing("military_group_caution_objective");
                if (objective.CautionGroupId == physical)
                    groups.CautionTargets![group] = new(true, 0);
            }

            //226BB0 retains the member descriptor while clearing its nodes.
            var members = pool!.ExistingCautionGroupHandle(physical) ?? throw Missing("caution_members");
            if (current.ReadTemporaryHexListCount(members) is null) throw Missing("caution_members");
            cautions.GroupLabels.Remove(physical);
            cautions.GroupDistrictIds![physical] = 0;
            current.ClearTemporaryHexList(members);

            //The initial search in32B570 gates3DFB50; that callee removes all
            //matching pointers. Preserve every unrelated node and its order.
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.CautionTroops)
                ?? throw Missing("caution_registry");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("caution_registry_head");
            while (node.Identity.HasValue)
            {
                var next = current.ReadRegistryNodeNext(node) ?? throw Missing("caution_registry_next");
                var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("caution_registry_payload");
                if (payload == reference) current.UnlinkArmyReturnTraitNode(descriptor, node);
                node = next;
            }
        }
        return changed ? state.WithArmyLifecycle(arena with
        {
            ArmyCautions = cautions, ArmyGroups = groups, Indexes = pool!.ProjectOwnedRoles()
        }) : state;
    }

    private static InvalidDataException Missing(string field) => new("district.caution_reset." + field);
}
