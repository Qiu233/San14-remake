namespace San14.Core;

public static class PlayableOfficerAvailability
{
    //Original custody status6 overrides the coarse first-playable Officer role.
    //Legacy states without native fields retain their existing eligibility.
    public static bool IsCaptive(OfficerState officer) => officer.NativeAbilityRuntime?.Status11E == 6;

    public static bool IsSourceBusy(GameState state, OfficerState officer)
    {
        if (IsCaptive(officer)) return true;
        return state.PersonnelReturns?.IsOfficerReturning(officer.Id) == true;
    }
}
