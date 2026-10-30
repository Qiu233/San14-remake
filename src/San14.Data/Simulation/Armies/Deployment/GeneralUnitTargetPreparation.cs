using System.Globalization;
using San14.Core;

namespace San14.Data;

/// <summary>A real new deployment packet and its own source Foothold.
/// It does not identify an already allocated Army.</summary>
public sealed record GeneralUnitTargetArmy(int SourceLocationId, DeploymentDraftRow Row, int InitialGoalHexId);

public sealed record GeneralUnitTargetHeader(int ArmyCount, int TotalTroops, int DistinctSourceCount,
    string TargetName, int TargetTroops, int CommandBooks, int CommandBookCost, int CommandBooksAfter)
{
    //The original dispatch-header literal, extracted offline. Width3 is
    //minimum padding; negative and wider values retain their actual digits.
    public string CommandBooksText => CommandBooksAfter.ToString(CultureInfo.InvariantCulture).PadLeft(3) +
        "（^03-" + CommandBookCost.ToString(CultureInfo.InvariantCulture) + "^00）";
}

/// <summary>One ordinary mode1 entry, prepared from the actual target and
/// primary District. The UI owns subsequent local edits of these rows.</summary>
public sealed record GeneralUnitTargetPreparation(int ForceId, int PrimaryDistrictId,
    DeploymentTarget Target, int TargetHexId, IReadOnlyList<GeneralUnitTargetArmy> Armies,
    GeneralUnitTargetHeader? Header, string? UnresolvedOwner)
{
    public bool Available => UnresolvedOwner is null && Header is not null;
}
