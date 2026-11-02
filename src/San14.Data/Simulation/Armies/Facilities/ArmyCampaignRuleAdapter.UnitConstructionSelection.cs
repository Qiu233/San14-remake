using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>7E17B0 mode3. These are actual selected ordinary Armies in
    /// retained packet order, independent of the route-group representative.
    /// The child owns one captured current until its real World changes.</summary>
    public DeploymentFacilitySelectionQuery CreateUnitFacilitySelectionQuery(GameState state,
        IReadOnlyList<int> orderedUnitIds, bool? tutorialRestrictsFacilities,
        IReadOnlyList<int>? retainedUnitIds = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(orderedUnitIds);
        var ids = orderedUnitIds.ToArray();
        SourceWorldContext? current = null;
        string? captureMissing = null;
        int?[]? people = null;
        int?[]? selectionTroops = null;
        int?[]? retained = retainedUnitIds is null ? null : new int?[retainedUnitIds.Count];
        string? peopleMissing = null;
        try
        {
            current = CaptureFacilityConstructionCurrent(state);
            if (ids.Length < 1 || ids.Distinct().Count() != ids.Length)
                people = [];
            else
            {
                people = new int?[ids.Length];
                selectionTroops = new int?[ids.Length];
                int? force = null;
                for (int index = 0; index < ids.Length; index++)
                {
                    if (!state.Units.TryGetValue(ids[index], out var army) ||
                        army.NativeTacticRuntime is { Status10: not 1 })
                    { people = []; break; }
                    if (army.NativeTacticRuntime is null) throw Missing("selection_Army_runtime");
                    int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("selection_Army_receiver");
                    var reference = new ArmyUnitReference(physical);
                    if (!K(current.ReadArmyUnitNativeValidity(reference), "selection_Army_validity"))
                    { people = []; break; }
                    if (K(current.ReadArmyUnitCommanderId(reference), "selection_Army_commander") != army.OfficerId)
                        throw Missing("selection_Army_commander_identity");
                    if (force.HasValue && force.Value != army.ForceId)
                    { people = []; break; }
                    force = army.ForceId;
                    int person = current.ResolveOfficerSource(army.OfficerId) ?? throw Missing("selection_Person_pointer");
                    // Like7E17B0's selected-People producer, a known-invalid
                    // Person contributes null; it is never replaced by Person0.
                    selectionTroops[index] = current.ReadArmyReturnResourceFields(reference)?.Ready16;
                    people[index] = K(current.ReadPersonNativeValidity(person), "selection_Person_validity") ? person : null;

                }
            }
        }
        catch (InvalidDataException error)
        {
            if (current is null) captureMissing = error.Message;
            else { peopleMissing = error.Message; people = null; }
        }
        // The source preselection walks the complete retained vector from
        // its second row, independently of active selection and mode. Unknown
        // ownership of that vector affects only preselection, not eligibility.
        if (current is not null && retainedUnitIds is not null)
        {
            for (int index = 0; index < retainedUnitIds.Count; index++)
            {
                try
                {
                    if (!state.Units.TryGetValue(retainedUnitIds[index], out var army))
                        throw Missing("selection_retained_Army");
                    var runtime = army.NativeTacticRuntime ?? throw Missing("selection_retained_status");
                    if (runtime.Status10 == 2)
                    {
                        //7E17B0 tests tag0 before the definition field. This
                        //entry cannot suppress the seed; no Army46 is read.
                        retained![index] = 0;
                        continue;
                    }
                    if (runtime.Status10 != 1) throw Missing("selection_retained_packet_kind");
                    var receiver = state.ReadArmyLifecycle()?.Receivers.FindUnit(army.Id)
                        ?? throw Missing("selection_retained_Army_owner");
                    var intent = receiver.FacilityConstruction ?? throw Missing("selection_retained_definition");
                    if (intent.DefinitionKey is null)
                    {
                        // Assign initializes None, preserving the actual fresh
                        // Army's zero definition field. Null intent is unknown.
                        retained![index] = 0;
                    }
                    else
                    {
                        var definition = current.ReadObjectDefinitionReference(
                            MapObjectSourceDirectory.DefinitionSlot(intent.DefinitionKey))
                            ?? throw Missing("selection_retained_definition_pointer");
                        retained![index] = K(definition.PhysicalSlot, "selection_retained_definition_identity");
                    }
                }
                catch (InvalidDataException) { retained![index] = null; }
            }
        }
        return CreateConstructionPeopleSelectionQuery(state, current, captureMissing, people,
            peopleMissing,selectionTroops,retained,tutorialRestrictsFacilities);
    }
}
