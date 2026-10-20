namespace San14.Data;

public enum CityCommandMenuBranch { Primary, Secondary, Absent }
public enum CityCommandMenuCommand
{
    Deploy, CancelDeployment, Administration, AreaAdministration, CityDomestic,
    Merchant, Move, Transfer, Summon, Transport, Investment, CityInformation, AreaInformation
}

/// <summary>Source admission is independent of an editor's implementation or formal acceptance.</summary>
public sealed record CityCommandMenuEntry(CityCommandMenuCommand Command,
    CityCommandMenuCommand? Parent, int CaptionId, AreaAdministrationQuery<bool> SourceEnabled);

public sealed record CityCommandMenuProjection(CityCommandMenuBranch Branch,
    IReadOnlyList<CityCommandMenuEntry> Entries);
