using San14.Core;

namespace San14.Data;

public enum PersonnelAdministrationSelectionKind { Reward, Banish, CreditGive, CreditRankup }

/// <summary>A real personnel draft's Person chooser. Eligible membership,
/// independent health and per-cell palette owners are kept separate.</summary>
public sealed record PersonnelAdministrationOfficerSelection(OfficerInformationPageSet Pages,
    IReadOnlyList<int> InitialSelection, int MaximumSelected, int TitleMessageId,
    string GuideText, bool RequireSelection)
{
    public PersonnelAdministrationSelectionKind Kind { get; init; }
    public IReadOnlyList<int> EligibleOfficerIds { get; init; } = [];
    public IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> AlteredHealth { get; init; }
        = new Dictionary<int, AreaAdministrationQuery<bool>>();
    public IReadOnlyDictionary<int, string> DisabledReasons { get; init; }
        = new Dictionary<int, string>();
}

//The draft already captures these two actual owners for its quotes/cards.
//Keeping their references lets all child pages borrow that same publication.
internal sealed record PersonnelAdministrationSelectionSource(GameState State, SourceWorldContext Current);
