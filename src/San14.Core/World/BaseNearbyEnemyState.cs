namespace San14.Core;

public sealed partial record GameState
{
    /// <summary>The default lookup receivers are not playable cities or gates,
    /// but participate in the source base refresh registry.</summary>
    public bool? UnassignedCityHasNearbyEnemyArmy { get; init; }
    public bool? UnassignedGateHasNearbyEnemyArmy { get; init; }
}

public sealed partial record CityState
{
    /// <summary>Last completed nearby-Army refresh. Null is an older or not
    /// yet evaluated state, not proof that the city is safe.</summary>
    public bool? HasNearbyEnemyArmy { get; init; }
}

public sealed partial record GateState
{
    public bool? HasNearbyEnemyArmy { get; init; }
}
