using System.Collections.Immutable;

namespace San14.Core;

public sealed record NormalTurnDistrictResourceHistory(ImmutableArray<int> Gold, ImmutableArray<int> Food,
    ImmutableArray<int> Troops, ImmutableArray<int> MilitaryIntelligence);

public sealed partial record GameState
{
    public ImmutableDictionary<int, NormalTurnDistrictResourceHistory>? PlayerDistrictResourceHistory { get; init; }
    public bool? ChallengeServiceTurnStopPending { get; init; }
}
