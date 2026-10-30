namespace San14.Data;

/// <summary>Selected draft commander and formation inputs for the unit tab.
/// This is display data, not an Army or an activation-effect publication.</summary>
public sealed record DeploymentUnitPanePublication(int OfficerId, int LandFormationId,
    IReadOnlyList<AreaAdministrationOfficerTrait> Traits)
{
    public Func<int, AreaAdministrationQuery<string?>>? TraitTooltip { get; init; }
}

/// <summary>Prepared once when the selected commander changes. The caller
/// retains the result through troop/formation edits and ordinary redraws.</summary>
public sealed record DeploymentUnitSpeechPublication(int OfficerId, int? SelectedTraitId,
    int SelectedIndex, int CandidateCount, int MessageId, string Text);
