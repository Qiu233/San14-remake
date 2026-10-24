using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed record WithdrawalSavedProjection(IReadOnlyList<int> WaypointHexIds, int? LastPointPhysical);

    /// <summary>7C47D0 updates the retained +560 groups, and DA2B0 reads those
    /// same groups. Fresh +578 groups are not the final point source.</summary>
    private Dictionary<int, WithdrawalSavedProjection> ProjectWithdrawalSavedGroups(GameState state,
        IReadOnlyList<UnitState> armies, IReadOnlyList<ArmyWithdrawalSavedGroup>? savedGroups)
    {
        if (savedGroups is null)
        {
            if (armies.Count != 1) throw Missing("withdraw_page_saved_groups");
            //A direct single-unit command has a newly opened one-row page.
            //A real multi-row page must supply its retained provenance even
            //when its current selected subset contains only one member.
            var row = QueryUnitSelectionPageRows(state, [armies[0].Id]).Rows.SingleOrDefault();
            var group = row?.SavedGroup ?? throw Missing("withdraw_single_saved_group");
            savedGroups = [new(group.OrderedUnitIds, group.PhysicalPoints,
                group.ActiveWaypointIndex, group.HasFinalTarget)];
        }
        var selected = armies.ToDictionary(army => army.Id);
        var result = new Dictionary<int, WithdrawalSavedProjection>(armies.Count);
        SourceWorldContext? current = null;
        foreach (var group in savedGroups)
        {
            if (group is null || group.OrderedUnitIds is not { Count: > 0 } members ||
                group.PhysicalPoints is null || members.Any(id => id < 0) || members.Distinct().Count() != members.Count)
                throw Missing("withdraw_saved_group_shape");
            if (!members.Any(selected.ContainsKey)) continue;
            int index = group.ActiveWaypointIndex;
            bool nonempty = group.PhysicalPoints.Count != 0;
            if (index is < 0 or > 5 || group.PhysicalPoints.Any(point => point is < 0 or >= 48400) ||
                (nonempty
                    ? group.PhysicalPoints.Count != index + (group.HasFinalTarget ? 2 : 1) || group.PhysicalPoints[0] is not int
                    : index != 0 || group.HasFinalTarget))
                throw Missing("withdraw_saved_group_points");

            int? last = nonempty ? group.PhysicalPoints[^1] : null;
            IReadOnlyList<int> points;
            if (nonempty && selected.TryGetValue(members[0], out var first) &&
                first.NativeTacticRuntime is { Abnormal23: 0, Abnormal24: 0, Abnormal25: 0 })
            {
                //E7A00(...,1) keeps the first position, appends this first
                //row's Home Hex and resets the group's intermediate index.
                if (current is null)
                {
                    current = CaptureWritableTransportRouteCurrent(state);
                    OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
                }
                last = ReadWithdrawalSavedGroupHome(current, first);
                points = Array.Empty<int>();
            }
            else
            {
                var retained = new int[index];
                for (int i = 0; i < index; i++)
                    retained[i] = group.PhysicalPoints[i + 1]
                        ?? throw Missing("withdraw_saved_waypoint_pointer_identity");
                //22DEF0 is a pointer difference. A known null pointer has no
                //portable numeric identity; do not invent zero or BD10.
                points = Array.AsReadOnly(retained);
            }
            var projection = new WithdrawalSavedProjection(points, last);
            foreach (int id in members)
                if (selected.ContainsKey(id) && !result.TryAdd(id, projection))
                    throw Missing("withdraw_duplicate_selected_group");
        }
        if (selected.Keys.Any(id => !result.ContainsKey(id))) throw Missing("withdraw_selected_group_absent");
        return result;
    }

    private static int ReadWithdrawalSavedGroupHome(SourceWorldContext current, UnitState army)
    {
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("withdraw_group_Army_receiver");
        ushort commander = K(current.ReadArmyUnitCommanderId(new(physical)), "withdraw_group_Commander12");
        var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
            ?? throw Missing("withdraw_group_Commander_pointer");
        ushort home = person.PhysicalSlot is int slot && K(current.ReadPersonNativeValidity(slot), "withdraw_group_Commander_validity")
            ? K(current.ReadPersonFirstLocation(slot), "withdraw_group_Commander_E2") : (ushort)0;
        var basis = BaseLookup.Find(home, current) ?? throw Missing("withdraw_group_Base_pointer");
        if (!K(current.ReadBaseNativeValidity(basis), "withdraw_group_Base_validity"))
            throw Missing("withdraw_group_Base_validity");
        int hex = K(CurrentGateMapQueries.FindHex(basis, current).PhysicalSlot, "withdraw_group_Hex_pointer");
        if (hex is < 0 or >= 48400) throw Missing("withdraw_group_Hex_pointer");
        return hex;
    }
}
