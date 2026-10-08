namespace San14.Data;

/// <summary>The actual current App catalog entry fields used by37E9C0.
/// Data10 is the instance Data returned by its source creator, not a model
/// definition or an ordinary-camera visible primitive. Category20 and
/// Enabled18 belong to that entry; neither is inferred from a display family.</summary>
public sealed record OriginalSceneCatalogListEntry<TData>(TData? Data10, uint Enabled18, uint Category20)
    where TData : class;

public sealed record OriginalSceneCatalogListAppend<TData>(uint DestinationListObjectId,
    int Category20, int CatalogBaseOffset, int CatalogSlot, TData Data)
    where TData : class;

/// <summary>The original four-category App catalog publication. Source import
/// verifies the native producer once; normal calls only consume the actual
/// current entries and preserve source index order, aliases and duplicates.
/// This does not create catalog entries or choose a static/Army category.</summary>
public sealed class LoadedOriginalSceneCatalogListPublicationSource
{
    internal LoadedOriginalSceneCatalogListPublicationSource(string locale, string evidence)
        => (Locale, SourceEvidence) = (locale, evidence);

    public const int PrimaryCatalogBaseOffset = 0x458;
    public const int PrimaryCatalogCount = 0x820;
    public const int SecondaryCatalogBaseOffset = 0x4558;
    public const int SecondaryCatalogCount = 0x200;
    private static readonly uint[] CategoryDestinations =
        [0xE65D70AE, 0xF0792D7D, 0x3E3E42CD, 0x012AAC2C];

    public string Locale { get; }
    public string SourceEvidence { get; }
    public IReadOnlyList<uint> DestinationListObjectIds { get; } = Array.AsReadOnly(CategoryDestinations);

    /// <summary>37F050 runs categories0..3;37E9C0 visits the complete458 table
    /// then4558 table for each category. It tests entry20 equality, Data10
    /// nonnull and Enabled18 nonzero. It does not inspect Data20 or deduplicate.
    /// These are append calls, not a ready shadow-caster population.</summary>
    public IReadOnlyList<OriginalSceneCatalogListAppend<TData>> PrepareCurrentCategoryAppends<TData>(
        IReadOnlyList<OriginalSceneCatalogListEntry<TData>?> actualPrimaryCatalog,
        IReadOnlyList<OriginalSceneCatalogListEntry<TData>?> actualSecondaryCatalog,
        IReadOnlyList<bool> actualCurrentDestinationCpuPresence)
        where TData : class
    {
        ArgumentNullException.ThrowIfNull(actualPrimaryCatalog);
        ArgumentNullException.ThrowIfNull(actualSecondaryCatalog);
        ArgumentNullException.ThrowIfNull(actualCurrentDestinationCpuPresence);
        if (actualPrimaryCatalog.Count != PrimaryCatalogCount || actualSecondaryCatalog.Count != SecondaryCatalogCount ||
            actualCurrentDestinationCpuPresence.Count != CategoryDestinations.Length)
            throw new InvalidDataException("Catalog publication requires both complete original current App pointer tables.");
        var calls = new List<OriginalSceneCatalogListAppend<TData>>();
        for (int category = 0; category < CategoryDestinations.Length; category++)
        {
            if (!actualCurrentDestinationCpuPresence[category]) continue;
            Append(actualPrimaryCatalog, PrimaryCatalogBaseOffset, category);
            Append(actualSecondaryCatalog, SecondaryCatalogBaseOffset, category);
        }
        return calls.AsReadOnly();

        void Append(IReadOnlyList<OriginalSceneCatalogListEntry<TData>?> table, int offset, int category)
        {
            for (int slot = 0; slot < table.Count; slot++)
            {
                var entry = table[slot];
                if (entry is null || entry.Category20 != (uint)category || entry.Data10 is null || entry.Enabled18 == 0)
                    continue;
                calls.Add(new(CategoryDestinations[category], category, offset, slot, entry.Data10));
            }
        }
    }
}

public static class OriginalSceneCatalogListPublicationSource
{
    public static LoadedOriginalSceneCatalogListPublicationSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadSceneCatalogListPublicationSource(repository.SourceDirectory, locale);
    }
}
