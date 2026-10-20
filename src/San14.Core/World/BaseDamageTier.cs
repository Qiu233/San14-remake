namespace San14.Core;

public sealed partial record CityState
{
    // Last actual wall-state writer's damage tier. It is not a live ratio query.
    public byte? DamageTier { get; init; }
}
public sealed partial record GateState
{
    public byte? DamageTier { get; init; }
}
