namespace San14.Core;

public sealed record BaseTurnIncomeTotals(int Gold, int Food, int RecruitedTroops);
public sealed record AreaTurnIncomeTotals(ushort Gold, ushort Food, ushort RecruitedTroops);

public sealed partial record CityState
{
    public BaseTurnIncomeTotals? TurnIncomeTotals { get; init; }
}

public sealed partial record AreaState
{
    public AreaTurnIncomeTotals? TurnIncomeTotals { get; init; }
}
