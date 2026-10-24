using San14.Core;
using San14.Data;
using San14.Data.Formats;

namespace San14.Client.Gameplay;

/// <summary>Construction names imported with the campaign content. Controls
/// retain independent choices and never reload the source catalogue.</summary>
internal sealed class CampaignConstructionCatalog
{
    private CampaignConstructionCatalog(IReadOnlyList<NamedCommandChoice> choices, IReadOnlyDictionary<string, string> names,
        string sourceUri, string sourceSha256)
    {
        Choices = choices;
        _names = names;
        SourceUri = sourceUri;
        SourceSha256 = sourceSha256;
    }

    public IReadOnlyList<NamedCommandChoice> Choices { get; }
    private readonly IReadOnlyDictionary<string, string> _names;
    public string? NameFor(string kindId) => _names.GetValueOrDefault(kindId);
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
        var names = new Dictionary<string, string>();
        foreach (var definition in loaded.Catalog.Definitions.Where(row => !string.IsNullOrWhiteSpace(row.Name)))
        {
            names.Add($"san14-object-definition:{definition.Slot}", definition.Name);
            names.Add($"san14:object/{definition.Slot}", definition.Name);
        }
        return new(Array.AsReadOnly(choices), names, loaded.Source.ResourceUri, loaded.Source.Sha256);
    }
}
