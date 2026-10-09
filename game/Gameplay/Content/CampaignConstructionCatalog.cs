using San14.Core;
using San14.Data;
using San14.Data.Formats;

namespace San14.Client.Gameplay;

/// <summary>Construction names imported with the campaign content. Controls
/// retain independent choices and never reload the source catalogue.</summary>
internal sealed class CampaignConstructionCatalog
{
    private CampaignConstructionCatalog(IReadOnlyList<NamedCommandChoice> choices,
        string sourceUri, string sourceSha256)
    {
        Choices = choices;
        SourceUri = sourceUri;
        SourceSha256 = sourceSha256;
    }

    public IReadOnlyList<NamedCommandChoice> Choices { get; }
    public string SourceUri { get; }
    public string SourceSha256 { get; }

    public static CampaignConstructionCatalog Load(GameResourceRepository repository, LoadedScenario scenario)
    {
        LoadedObjectDefinitions loaded = repository.LoadObjectDefinitions(scenario.Snapshot.FixedDataPath);
        if (loaded.Source.ResourceUri != scenario.FixedDataSource.ResourceUri ||
            !loaded.Source.Sha256.Equals(scenario.FixedDataSource.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Construction catalog differs from the current scenario source.");
        var choices = PlayableConstructionRules.SupportedKinds.Select(kind =>
        {
            S14ObjectDefinition definition = loaded.Catalog.Definitions.Single(row =>
                row.Slot == kind.ObjectSlot && !string.IsNullOrWhiteSpace(row.Name));
            return new NamedCommandChoice(kind.ObjectSlot, definition.Name);
        }).ToArray();
        return new(Array.AsReadOnly(choices), loaded.Source.ResourceUri, loaded.Source.Sha256);
    }
}
