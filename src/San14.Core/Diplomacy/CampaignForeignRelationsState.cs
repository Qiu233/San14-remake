using System.Collections.Immutable;

namespace San14.Core;

/// <summary>One of the five independent foreign countries. Relation values
/// retain their three source states and are not ordinary Force diplomacy.</summary>
public sealed record CampaignForeignCountryRelations(int Id, string Name, ImmutableDictionary<int, byte> Relations);
public sealed record CampaignForeignRelationsState(ImmutableDictionary<int, CampaignForeignCountryRelations> Countries);

public sealed partial record GameState
{
    public CampaignForeignRelationsState? ForeignCountryRelations { get; init; }
}
