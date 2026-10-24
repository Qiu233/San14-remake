using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The complete initial batch may contain several accepted actions. Recheck
    //each actual Fire selection's common press/endpoint/index before allocating
    //any Army; the source inclusion leaf consumes these Persons, not fake Units.
    private CommandResult? OCValidateInitialFireTargets(GameState state, SourceWorldContext current,
        int force, IReadOnlyList<DeploymentUnitRequest> rows)
    {
        var groups = rows.Where(row => row.Actions is [{ ActionKey: "fire" }]).GroupBy(row =>
            (ClickedHexId: row.Actions[0].ClickedHexId, TargetHex: row.Actions[0].Target.Id,
                Index: row.WaypointHexIds.Count)).ToArray();
        if (groups.Length == 0) return null;
        //The Person-selection capture does not bind Hex16. Like the actual
        //map menu, this command reads the saved current fire owner once.
        BaseCaptureCurrentInputs.BindCurrent(state, current);
        foreach (var group in groups)
        {
            int clicked = group.Key.ClickedHexId ?? throw OCMissing("initial_fire_original_click");
            var officers = group.Select(row => row.OfficerId).ToArray();
            var reader = new UnitTargetMenuReader(this, state, officers, clicked, force,
                new(true, group.Key.Index), current);
            if (!reader.ReadDeploymentFireCommand(group.Key.TargetHex))
                return CommandResult.Reject("Fire is unavailable at the current initial target.", "deployment.fire_target");
        }
        return null;
    }

    private int ReadInitialFireRate(GameState state,
        SourceWorldContext current, IReadOnlyList<int> officerIds, HexReference target)
    {
        _baseCapacityInputs.BindCurrent(state, current);
        CaptureBaseThreatInputs.BindCurrent(state, current);
        int maximum = 0;
        foreach (int id in officerIds)
        {
            var person = current.ReadPersonLookupReference(id <= 6000 ? id : 0)
                ?? throw FirePreviewMissing("initial_selected_person_pointer");
            int rate = person.PhysicalSlot is int physical &&
                FirePreviewKnown(current.ReadPersonNativeValidity(physical), "initial_selected_person_validity")
                ? ReadFireProbability(state, current, physical, target) : 0;
            maximum = Math.Max(maximum, Math.Clamp(rate, 0, 100));
        }
        return maximum;
    }

    private sealed partial class UnitTargetMenuReader
    {
        private void AddInitialFire()
        {
            int ordinal = options.Count;
            Add("fire", 0x113, 0x56A9, FireIncluded,
                () => InitialAction("fire", new(DeploymentTargetKind.Hex, targetHex)));
            if (options.Count == ordinal || options[ordinal].Included != true) return;
            try
            {
                int rate = package.ReadInitialFireRate(state, current, selectedOfficerIds, hex);
                options[ordinal] = options[ordinal] with { SuccessRate = rate, PresentationUnresolvedInput = null };
            }
            catch (InvalidDataException error)
            {
                options[ordinal] = options[ordinal] with { PresentationUnresolvedInput = error.Message };
            }
        }

        //This formal command leaf deliberately does not build the menu, read
        //probability, find a route or capture another World. It follows the
        //same mode0 press -> footprint snap -> Fire inclusion as the page.
        internal bool ReadDeploymentFireCommand(int expectedTargetHex)
        {
            foreach (int id in selectedOfficerIds)
            {
                int person = Need(current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot,
                    "initial_fire_Person_pointer");
                if (!Need(current.ReadPersonNativeValidity(person), "initial_fire_Person_validity") ||
                    current.ReadPersonForce(person) != own) return false;
            }
            var pressed = new HexReference(clicked);
            return PressedTerrainAllowed(pressed) && PrepareMapTarget(pressed) &&
                targetHex == expectedTargetHex && FireIncluded();
        }
    }
}
