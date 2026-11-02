using System.Collections.ObjectModel;
using System.Globalization;
using San14.Data;
using San14.Data.Formats;

namespace San14.Client.Gameplay;

/// <summary>The ordinary page's native filtering/order and source row content.
/// Available sources and application history/capabilities are supplied by their
/// actual owners; this class does not infer them from installed filenames.</summary>
internal sealed class ScenarioSelectionCatalog
{
    private readonly IReadOnlyDictionary<int, S14Entry> documents;
    internal IReadOnlyList<ScenarioSelectionEntry> Rows { get; }

    private ScenarioSelectionCatalog(ScenarioSelectionEntry[] rows, Dictionary<int, S14Entry> documents)
    {
        Rows = Array.AsReadOnly(rows);
        this.documents = new ReadOnlyDictionary<int, S14Entry>(documents);
    }

    internal S14Entry Scenario(int id) => documents[id];

    internal static ScenarioSelectionCatalog Create(GameResourceRepository repository, string locale,
        IReadOnlyDictionary<int, S14Entry> availableSources, IReadOnlyList<uint?> specialUnlocks,
        Func<int, bool?> completed, Func<bool?> specialScenarioEnabled, Func<S14Entry, int> resolveLegacyEdition)
    {
        ArgumentNullException.ThrowIfNull(availableSources);
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(specialScenarioEnabled);
        ArgumentNullException.ThrowIfNull(resolveLegacyEdition);
        if (specialUnlocks.Count != 3)
            throw new ArgumentException("The application must supply all three independent scenario unlock values.", nameof(specialUnlocks));
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        var selected = new List<S14Entry>();
        // The native ordinary call scans [0,199), requiring the actual source
        // ID and filter0/1. Known zero special unlocks omit rows. Unknown older
        // profile history retains only that row for preview without admitting it.
        for (int id = 0; id < 199; id++)
        {
            if (!availableSources.TryGetValue(id, out var document)) continue;
            var metadata = document.SelectionMetadata
                ?? throw new InvalidDataException("Scenario selection metadata is missing: " + document.SourcePath);
            if (metadata.NativeId != id || metadata.FilterKind is not (0 or 1)) continue;
            int unlock = id switch { 8 => 0, 25 => 1, 63 => 2, _ => -1 };
            if (unlock >= 0 && specialUnlocks[unlock] == 0) continue;
            if (document.Title is null || document.StartYear is null || document.StartMonth is null)
                throw new InvalidDataException("Scenario selection text/date is missing: " + document.SourcePath);
            selected.Add(document);
        }
        selected.Sort(Compare);
        var captions = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records;
        string yearSuffix = captions[0x2AB].Text ?? throw new InvalidDataException("Scenario year suffix is missing.");
        string monthSuffix = captions[0x2AC].Text ?? throw new InvalidDataException("Scenario month suffix is missing.");
        var sprites = new LocalizedUiSpriteSource(repository);
        var badges = new Dictionary<(int Edition, bool Download), LoadedSprite?>();
        var rows = new List<ScenarioSelectionEntry>();
        var documents = new Dictionary<int, S14Entry>();
        foreach (var document in selected)
        {
            var metadata = document.SelectionMetadata!;
            int id = metadata.NativeId;
            int edition = metadata.EditionCode ?? resolveLegacyEdition(document);
            bool download = HasDownloadBadge(id);
            if (!badges.TryGetValue((edition, download), out var badge))
            {
                badge = LoadEditionBadge(sprites, locale, edition, id);
                badges.Add((edition, download), badge);
            }
            string year = string.Format(CultureInfo.InvariantCulture, "{0,3}{1}", document.StartYear, yearSuffix);
            string month = string.Format(CultureInfo.InvariantCulture, "{0,2}{1}", document.StartMonth, monthSuffix);
            var content = new ScenarioSelectionButtonContent(year, month, document.Title!, badge,
                metadata.ScenarioKindCode != 0, completed(id));
            int unlock = id switch { 8 => 0, 25 => 1, 63 => 2, _ => -1 };
            bool? enabled = unlock >= 0 && specialUnlocks[unlock] is null ? null :
                id == 19 ? specialScenarioEnabled() : true;
            rows.Add(new(id, content, enabled));
            documents.Add(id, document);
        }
        return new(rows.ToArray(), documents);
    }

    // Both the directory row and subsequent startup headers use this same
    // serialized-edition / native-ID artwork route.
    internal static LoadedSprite? LoadEditionBadge(LocalizedUiSpriteSource sprites, string locale, int edition, int nativeId)
    {
        string language = locale switch { "TC" => "tw", "SC" => "ch", _ => throw new ArgumentOutOfRangeException(nameof(locale)) };
        bool download = HasDownloadBadge(nativeId);
        string? name = edition switch
        {
            2 => "020_sho_mrk_ce" + (download ? "_dlc" : ""),
            1 => "020_sho_mrk_pk" + (download ? "_dlc" : ""),
            _ => download ? "020_sho_mrk_dlc" : null
        };
        return name is null ? null : sprites.LoadTexturePackSprite(
            "texture_" + (edition == 2 ? "ce_" : "") + "lang_" + language, language + "_" + name);
    }

    // This is the fixed ID classification used by both sorting and badges.
    // It is separate from alternate-stream resolution, entitlement or presence.
    private static bool HasDownloadBadge(int id) =>
        id is >= 150 and < 200 || id is >= 9 and <= 50 && id is not (>= 21 and <= 25 or 30 or 31 or 34 or 35);

    private static int Compare(S14Entry left, S14Entry right)
    {
        var a = left.SelectionMetadata!; var b = right.SelectionMetadata!;
        if ((a.FilterKind == 2) != (b.FilterKind == 2)) return a.FilterKind == 2 ? -1 : 1;
        if ((a.SortSubtypeCode == 2) != (b.SortSubtypeCode == 2)) return a.SortSubtypeCode == 2 ? 1 : -1;
        if ((a.ScenarioKindCode == 0) != (b.ScenarioKindCode == 0)) return a.ScenarioKindCode == 0 ? 1 : -1;
        int order = left.StartYear!.Value.CompareTo(right.StartYear!.Value);
        if (order != 0) return order;
        order = left.StartMonth!.Value.CompareTo(right.StartMonth!.Value);
        if (order != 0) return order;
        order = HasDownloadBadge(a.NativeId).CompareTo(HasDownloadBadge(b.NativeId));
        if (order != 0) return order;
        if (a.SortSubtypeCode == 2 && b.SortSubtypeCode == 2)
        {
            if (a.NativeId == 63 && b.NativeId == 35) return -1;
            if (a.NativeId == 35 && b.NativeId == 63) return 1;
        }
        return a.NativeId.CompareTo(b.NativeId);
    }
}
