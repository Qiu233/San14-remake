namespace San14.Core;

/// <summary>The same current commander district governs both military command
/// adapters. This checks the existing session scope, not original command costs.</summary>
internal static class PlayableUnitControl
{
    internal static bool TryResolve(GameState state, ForceState force, int id, CommandContext context,
        out UnitState? unit, out CommandResult? denied)
    {
        denied = null;
        if (!state.Units.TryGetValue(id, out unit) || unit.ForceId != force.Id)
        {
            denied = CommandResult.Reject("Unit does not belong to this force.", "unit.control");
            return false;
        }
        if (context.DelegatedDistrictId is int district &&
            (!state.Officers.TryGetValue(unit.OfficerId, out OfficerState? officer) || officer.DistrictId != district))
        {
            denied = CommandResult.Reject("Unit is outside the authorized district.", "unit.district_scope");
            return false;
        }
        return true;
    }
}
