using San14.Core;

namespace San14.Data;

public sealed record OrdinaryArmyMapEntryRow(UnitSelectionRow Army, UnitSelectionPageRow Page);

/// <summary>The ordinary map's source-ordered initial mode3 vector. This is
/// separate from the already open page's Ctrl/box selection qualifications.</summary>
public sealed record OrdinaryArmyMapEntry(bool? CanOpen, int? PlayerPrimaryDistrictId,
    int? PageDistrictId, bool InitiallySelected, IReadOnlyList<OrdinaryArmyMapEntryRow> Rows,
    string? UnresolvedInput);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Read one actual Hex release, using the grouping publication
    /// belonging to the accepted map. No grouping recompute, World write,
    /// selection cap, route search or command is performed here.</summary>
    public OrdinaryArmyMapEntry QueryOrdinaryArmyMapEntry(GameState state, int hexId,
        int cameraLevel, ArmyMapGroupingFrame? acceptedGroups)
    {
        int? primary = null;
        if (state.Phase != TurnPhase.Strategy || (uint)hexId >= 48400 || (uint)cameraLevel >= 6)
            return Rejected();
        try
        {
            var tutorial = ReadTutorialControlState(state);
            if (Need(TacticRemakeCurrentBattleControl.Read(tutorial, 0x3C), "tutorial_entry_control"))
                return Rejected();
            var current = CaptureWritableTransportRouteCurrent(state);
            var hex = current.ReadHexTableReference(hexId) ?? throw Missing("Hex_pointer");
            if (hex.PhysicalSlot is not int actualHex) return Rejected();
            if (Need(TacticRemakeCurrentBattleControl.Read(tutorial, 0x39), "tutorial_Hex_filter") &&
                !TutorialTargetQualification.AllowsHex(tutorial, current, actualHex)) return Rejected();

            //The Scene dispatches the Hex, even when a label supplied it.
            //An actual City/Gate footprint has priority over its Army occupant.
            ushort link = Need(current.ReadHexObjectLink(hex), "Hex_Object1A");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
                ?? throw Missing("Object_pointer");
            if (Need(current.ReadObjectNativeValidity(obj), "Object_validity") &&
                obj.PhysicalSlot is >= 1 and <= 61) return Rejected();

            current.BindArmyMapOccupancy(state);
            int physical = unchecked((short)Need(current.ReadHexRaw1C(hex), "Hex_Army1C"));
            if (physical is < 1 or > 500) return Rejected();
            var slots = (state.ReadArmyLifecycle() ?? throw Missing("Army_lifecycle")).ReceiverSlots();
            int clickedId = slots[physical].ActiveUnitId ?? throw Missing("clicked_active_Army");
            var clicked = ReadSelectionRow(state, current, clickedId, null);
            if (clicked.NativeValid == false) return Rejected();
            if (clicked.NativeValid != true || clicked.DistrictPhysicalSlot is not int clickedDistrict)
                throw Missing(clicked.UnresolvedInput ?? "clicked_District");
            if (!TutorialTargetQualification.AllowsArmy(tutorial, physical, clicked.ActionCode)) return Rejected();

            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("current_player_Force");
            primary = Need(ForcePrimaryDistrictSource.Resolve(current, player).PhysicalSlot, "primary_District");
            var members = new List<int>();
            if (cameraLevel >= 3)
            {
                //ReadMembers implements the actual root-first Hex-ring list,
                //including the invalid-root empty result. Do not substitute a
                //Force scan, nearest-Army sort or a single clicked root.
                var group = acceptedGroups ?? throw Missing("accepted_grouping_owner");
                foreach (var member in group.ReadMembers(physical, cameraLevel - 3)
                    ?? throw Missing("accepted_group_members"))
                    members.Add(member.UnitId ?? throw Missing("group_active_Army"));
            }
            if (members.Count == 0) members.Add(clickedId);

            var rows = members.Select(id => ReadSelectionRow(state, current, id, null)).ToArray();
            var permitted = new bool[rows.Length];
            for (int index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                if (row.NativeValid != true || !state.Units.TryGetValue(row.UnitId, out var army))
                    throw Missing(row.UnresolvedInput ?? "member_current_fields");
                byte ui = Need(army.NativeMapUiByte13C, "Army_MapUi");
                permitted[index] = ui == 0 && DistrictOne(row) &&
                    Need(ArmySavedCommandKnowledge.ReadControl(state, army), "Army_command_control") == 0;
            }
            if (clickedDistrict != primary || !permitted.Any(value => value)) return Rejected();
            if (permitted.Any(value => !value))
                //The original mixed-list rebuild is intentionally asymmetric:
                //the inspected machine branch retains NONZERO MapUi here.
                rows = rows.Where(row => Need(state.Units[row.UnitId].NativeMapUiByte13C, "Army_MapUi") != 0 &&
                    DistrictOne(row) && Need(ArmySavedCommandKnowledge.ReadControl(state, state.Units[row.UnitId]),
                        "Army_command_control") == 0).ToArray();
            if (rows.Length == 0) return Rejected(); //The source opener requires a nonempty list.

            //The initial vector is not the drag selector. Mode3 selects every
            //initial row; only the independent tutorial control clears them.
            var pageRows = QueryUnitSelectionPageRows(state, rows.Select(row => row.UnitId).ToArray());
            if (pageRows.UnresolvedInput is { } pageMissing) throw Missing(pageMissing);
            var initial = new OrdinaryArmyMapEntryRow[rows.Length];
            for (int index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                var page = pageRows.Rows[index];
                if (row.UnresolvedInput is not null || row.CurrentHexId is null ||
                    page.UnresolvedInput is not null || page.Partition is null ||
                    page.GroupKey is null || page.SavedGroup is null)
                    throw Missing(row.UnresolvedInput ?? page.UnresolvedInput ?? "initial_row_fields");
                initial[index] = new(row, page);
            }
            bool selected = !Need(TacticRemakeCurrentBattleControl.Read(tutorial, 0x4A), "tutorial_initial_selection");
            return new(true, primary, rows[0].DistrictPhysicalSlot, selected, Array.AsReadOnly(initial), null);

            bool DistrictOne(UnitSelectionRow row) => Need(current.ReadDistrictNumber(
                row.DistrictPhysicalSlot ?? throw Missing("member_District_pointer")), "District_number") == 1;
        }
        catch (InvalidDataException error) { return new(null, primary, null, false, [], error.Message); }

        OrdinaryArmyMapEntry Rejected() => new(false, primary, null, false, [], null);
        static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("ordinary-Army-entry." + field);
    }
}
