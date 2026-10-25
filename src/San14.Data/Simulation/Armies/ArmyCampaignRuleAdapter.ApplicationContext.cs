using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private ApplicationGameplayState? applicationGameplay;

    public void ConfigureApplicationGameplay(ApplicationGameplayState application)
        => applicationGameplay = application ?? throw new ArgumentNullException(nameof(application));

    public ApplicationGameplayContext? ReadApplicationGameplay() => applicationGameplay?.Context;

    //3343E0/441760 asks whether the attached game is leaving for the title,
    //independent of Story skip and the World's saved mode selection.
    internal bool? ReadApplicationReturnToTitlePending() => applicationGameplay?.ReturnToTitlePending;

    //9021C0 first tests the independent1D3840 service, then the saved signed
    //BC selection. A missing service short-circuits before that saved field.
    public bool? ReadApplicationInformationRestriction(GameState state)
        => ReadApplicationGameplay()?.UserSelectionServicePresent switch
        {
            false => false,
            true => state.CampaignEventWorld?.CurrentUserIndex is sbyte selection ? selection != -1 : null,
            _ => null
        };

    //570850 is a separate tutorial service; it is not the user-selection
    //service and not the special-mode controller returned by570840.
    public bool? ReadApplicationTutorialActive()
    {
        var context = ReadApplicationGameplay();
        return context?.TutorialServicePresent switch
        { false => false, true => context.TutorialActive, _ => null };
    }

    internal ApplicationSpecialPersonAvailability? ReadApplicationSpecialPersons()
        => ReadApplicationGameplay()?.SpecialPersons;
}
