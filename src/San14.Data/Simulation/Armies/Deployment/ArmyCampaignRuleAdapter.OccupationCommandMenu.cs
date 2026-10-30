using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class UnitTargetMenuReader
    {
        // Same Area predicate as mode3. The map press belongs to the page;
        // the formal Area order must not invent a second press at its center,
        // whose terrain can differ from the player's original clicked Hex.
        // Do not evaluate unrelated menu options or capture another world.
        internal bool ReadOccupationCommand(int expectedAreaPhysical)
        {
            if (Unit.NativeArmyOrigin?.NativeSlotIndex is not int physical || Unit.NativeTacticRuntime?.Status10 != 1)
                return false;
            var selected = new ArmyUnitReference(physical);
            if (!Need(current.ReadArmyUnitNativeValidity(selected), "occupation_selected_validity") ||
                Need(current.ReadArmyUnitCommanderId(selected), "occupation_selected_Commander") != Unit.OfficerId)
                return false;
            own = current.ReadArmyUnitForceReference(selected);
            if (!ValidateSelectedSet()) return false;
            if (!TutorialTargetQualification.AllowsHex(package.ReadTutorialControlState(state), current, targetHex,
                route.HasActiveRouteGroup ? route.FirstActiveWaypointIndex : null))
                return false;
            return AreaIncluded(new AreaReference(expectedAreaPhysical));
        }
    }
}
