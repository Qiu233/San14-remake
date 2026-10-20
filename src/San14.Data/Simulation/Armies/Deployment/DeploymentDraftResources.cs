namespace San14.Data;

public sealed record DeploymentTroopResources(int CurrentTroops, int DraftTroops, int RemainingTroops);
public sealed record DeploymentGoldResources(int CurrentGold, int FormationCost, int RemainingGold);
/// <summary>A known invalid District leaves the original header unchanged.
/// Its absent numbers are not an unresolved input or a zero-book quote.</summary>
public sealed record DeploymentCommandBookResources(int? DistrictId, bool IsPublished,
    int? CurrentBooks, int SpentBooks, int? RemainingBooks);
public sealed record DeploymentDraftResources(DeploymentTroopResources? Troops,
    DeploymentGoldResources? Gold, DeploymentCommandBookResources? CommandBooks,
    string? TroopsUnresolvedInput, string? GoldUnresolvedInput, string? CommandBooksUnresolvedInput)
{
    // Make's decision reads the current byte even when the separate header
    // writer declines to publish an invalid District's numbers.
    public int? DecisionCommandBooks {get;init;}
}
