namespace San14.Core;

public sealed partial record CityState
{
    /// <summary>The last actually reached Turn's stock expenses. Older saves
    /// leave these unknown until the real Turn reset runs for this City.</summary>
    public int? PeriodGoldExpenditure { get; init; }
    public int? PeriodFoodExpenditure { get; init; }
}

public sealed partial record GateState
{
    // The Turn visits Gate receivers too, although food debits are posted to
    // their currently mapped City. Keep the actual reset independent.
    public int? PeriodGoldExpenditure { get; init; }
    public int? PeriodFoodExpenditure { get; init; }
}
