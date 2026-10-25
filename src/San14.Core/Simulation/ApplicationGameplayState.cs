namespace San14.Core;

public enum ApplicationGameplayMode { Campaign, Tutorial, Challenge }

/// <summary>The independent, ordered original-person catalog used by DLC skill
/// availability. These are not live campaign Person receivers.</summary>
public sealed record ApplicationSpecialPersonDefinition(int PersonId,
    IReadOnlyList<ushort> PersonalityIds, IReadOnlyList<byte> TacticIds);

/// <summary>Application content capabilities supplied by their real loader.
/// Missing people or an absent catalog retain unknown availability.</summary>
public sealed record ApplicationSpecialPersonAvailability(
    IReadOnlyDictionary<int, bool?> FirstAvailability,
    IReadOnlyList<ApplicationSpecialPersonDefinition>? OriginalPersons);

/// <summary>Application controllers and their reached state, separate from a
/// campaign's saved user index and scenario. A game mode alone does not infer
/// the presence or flags of any other application service.</summary>
public sealed record ApplicationGameplayContext(ApplicationGameplayMode Mode,
    bool? UserSelectionServicePresent, bool? TutorialServicePresent, bool? TutorialActive,
    bool? SpecialModeServicePresent, bool? TutorialBlocksAreaReports,
    bool? TutorialRestrictsFacilities, bool? SpecialModeSuppressesSceneryReport,
    bool? SpecialModeSuppressesAssociationReport)
{
    public ApplicationSpecialPersonAvailability? SpecialPersons { get; init; }
}

/// <summary>The attached application's lifetime. Loading a world never edits
/// its saved mode-related fields; new-game/load hosts establish their own facts.</summary>
public sealed class ApplicationGameplayState
{
    public ApplicationGameplayContext? Context { get; private set; }
    public bool? ReturnToTitlePending { get; private set; }
    public PersonnelGroupMoveDestinations PersonnelGroupMoveDestinations { get; }

    public ApplicationGameplayState(ApplicationGameplayContext context,
        PersonnelGroupMoveDestinations? retainedPersonnelGroupMoveDestinations = null)
    {
        PersonnelGroupMoveDestinations = retainedPersonnelGroupMoveDestinations ?? new();
        Enter(context);
    }

    //This host route constructs none of the optional user-selection, tutorial
    //or special-mode controllers. Their absence is a lifecycle fact, not a
    //guess from a scenario, saved App.BC or a missing old-save field.
    public static ApplicationGameplayState CreateNormalCampaign(
        PersonnelGroupMoveDestinations? retainedPersonnelGroupMoveDestinations = null) => new(new(
        ApplicationGameplayMode.Campaign, false, false, false, false, false, false, false, false),
        retainedPersonnelGroupMoveDestinations);

    public void Enter(ApplicationGameplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(context.Mode)) throw new ArgumentOutOfRangeException(nameof(context));
        Context = context;
        ReturnToTitlePending = false;
    }

    public void RequestReturnToTitle()
    {
        if (Context is null) throw new InvalidOperationException("No application gameplay context is attached.");
        ReturnToTitlePending = true;
    }

    public void BeginLoad() => ReturnToTitlePending = null;

    public void Detach()
    {
        Context = null;
        ReturnToTitlePending = null;
    }
}
