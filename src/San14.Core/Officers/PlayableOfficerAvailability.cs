namespace San14.Core;

public static class PlayableOfficerAvailability
{
    // Custody overrides the coarse role without requiring a source-layout owner.
    // Unknown custody knowledge retains existing command eligibility.
    public static bool IsCaptive(OfficerState officer) => OfficerServiceRules.IsCaptive(officer.ServiceStatus);

    public static bool IsSourceBusy(GameState state, OfficerState officer)
    {
        if (IsCaptive(officer)) return true;
        return state.PersonnelCommands?.IsOfficerBusy(officer.Id) == true;
    }
}
