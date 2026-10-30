namespace San14.Data;

/// <summary>The office page's retained single-person selector. Its cells read
/// each candidate's actual draft appointment, independently of the target slot.</summary>
public sealed record OfficerOfficePeopleSelection(OfficerInformationPageSet Pages,
    IReadOnlyDictionary<int, OfficerOfficePeopleAdmission> Candidates, bool CanDismiss)
{
    /// <summary>The actual common selector guide, with its Person noun bound.</summary>
    public string? GuideText { get; init; }
}

public sealed record OfficerOfficePeopleAdmission(bool Enabled, string? DisabledReason)
{
    /// <summary>Only disabled focus consumes this prepared source hint. An
    /// unavailable argument stays distinct from an absent enabled-row hint.</summary>
    public AreaAdministrationQuery<string>? FocusHint { get; init; }
}
