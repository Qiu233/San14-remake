using System.Collections.ObjectModel;
using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The fresh title application's item encyclopedia. It reads fixed
/// definitions and messages, not campaign items, holders or unlock profiles.
/// Own it with the resource set and reload after a resource override changes.</summary>
public sealed class CollectionItemCatalog
{
    private readonly IReadOnlyList<CollectionItemRow> rows;
    private readonly Dictionary<int, CollectionItemDetail> details = [];

    public string Locale { get; }
    public string Title { get; }
    public IReadOnlyList<CollectionItemTab> Tabs { get; }
    public IReadOnlyList<SourceProvenance> Sources { get; }
    public IReadOnlyList<CollectionItemUnresolvedCandidate> UnresolvedCandidates { get; }

    public static CollectionItemCatalog Load(GameResourceRepository repository, string locale,
        IReadOnlyDictionary<int, CollectionItemContentAvailability>? availability = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return repository.LoadCollectionItemCatalog(locale, availability);
    }

    public IReadOnlyList<CollectionItemRow> ReadRows(int tabId) => tabId == 0 ? rows :
        throw new ArgumentOutOfRangeException(nameof(tabId), "The item encyclopedia has one feature tab.");

    public CollectionItemDetail ReadItem(int itemId) => details.TryGetValue(itemId, out var detail)
        ? detail : throw new ArgumentOutOfRangeException(nameof(itemId), "Not an admitted Collection item.");

    internal CollectionItemCatalog(string locale, S14Document fixedData,
        SourceProvenance fixedSource, LoadedMessages messages,
        IReadOnlyDictionary<int, CollectionItemContentAvailability>? availability)
    {
        Locale = locale;
        string Data(int section, int id)
        {
            var sections = messages.Messages.Sections;
            if ((uint)section >= (uint)sections.Count ||
                (uint)id >= (uint)sections[section].Records.Count ||
                sections[section].Records[id].Text is not string text)
                throw new InvalidDataException($"collection.item.data.{section}.{id:X}");
            return text;
        }
        Title = Data(1, 0x6D);
        // The next 0x18-byte descriptor has a null first pointer. The adjacent
        // custom-item configuration is not a second Collection tab.
        (int Id, int Caption, int Width)[] profile =
            [(0, 0x246, 188), (1, 0x401, 104), (8, 0x452, 132),
             (3, 0x411, 132), (4, 0x423, 76), (7, 0x426, 76)];
        // The exact comparators have not been recovered. Preserve source table
        // order rather than substituting localized text or display-number sorts.
        var columns = Array.AsReadOnly(profile.Select(column =>
            new CollectionColumn(column.Id, Data(1, column.Caption), column.Width, false)).ToArray());
        Tabs = Array.AsReadOnly(new[] { new CollectionItemTab(0, Data(1, 0x2DC), columns) });
        Sources = Array.AsReadOnly(new[] { fixedSource, messages.Source });

        var publishedRows = new List<CollectionItemRow>();
        var unresolved = new List<CollectionItemUnresolvedCandidate>();
        foreach (var item in S14ItemReader.ReadDefinitions(fixedData).Definitions)
        {
            int id = item.Slot;
            byte type = item.RawBytes[14];
            // Collection producer admission deliberately does not consult a
            // holder, Force, discovery status or a running campaign.
            if (id == 0 || ((type & 0xF6) == 0 && type != 1) || id is >= 152 and <= 201) continue;
            if (id is >= 125 and <= 127)
            {
                if (availability is null || !availability.TryGetValue(id, out var content))
                {
                    unresolved.Add(new(id, "collection.item.content_ownership_and_availability"));
                    continue;
                }
                if (!content.Owned || !content.Available) continue;
            }

            string typeName = type is >= 1 and <= 26 ? Data(0, 0x81E + type) : "";
            int value = Math.Min((int)item.RawBytes[19], 50);
            byte abilityId = item.RawBytes[15], effectId = item.RawBytes[17];
            string ability = abilityId switch
            {
                0 => "",
                >= 1 and <= 5 => Data(0, 0x183 + abilityId - 1) + "+" +
                    Math.Min((int)item.RawBytes[16], 10).ToString(CultureInfo.InvariantCulture),
                _ => throw new InvalidDataException($"collection.item.{id}.ability_kind")
            };
            string effect = effectId is 1 or 2 ? Data(0, 0x839 + effectId) : "";
            string description = Data(0, id + 0x765);

            // CItem's fresh constructor clears the DWORD containing flags4F;
            // the fixed-data archive (version >=14) does not overwrite it.
            // This is the fresh title baseline, not a claim about scenario/save
            // flags or an application custom-item manager's modified artwork.
            const bool transferable = false;
            string transferText = Data(1, 0x339);
            int imageIndex = item.RawBytes[21];
            CollectionImageRoute? image = imageIndex == 0 ? null : CollectionImageRoute.ForItem(imageIndex);
            var fields = new Dictionary<int, string>
            {
                // Node11 is an authored-empty ruby control: TextId=-1, empty
                // text pool, and the Collection binder never writes a value.
                [10] = item.Name, [11] = "", [17] = typeName,
                [22] = value.ToString(CultureInfo.InvariantCulture), [27] = transferText,
                [32] = ability, [37] = effect, [42] = description,
                [16] = Data(1, 0x401), [21] = Data(1, 0x423), [26] = Data(1, 0x426),
                [31] = Data(1, 0x452), [36] = Data(1, 0x411), [41] = Data(1, 0x400)
            };
            details.Add(id, new(id, item.Name, typeName, value, transferable, ability, effect,
                description, image)
            {
                DetailFields = new ReadOnlyDictionary<int, string>(fields),
                CanAppreciatePoem = type == 26 && id is >= 132 and <= 151
            });

            var texts = new Dictionary<int, string>
            { [0] = item.Name, [1] = typeName, [8] = ability, [3] = effect,
                [4] = fields[22], [7] = transferText };
            var cells = texts.ToDictionary(pair => pair.Key, pair => new CollectionItemCell(pair.Key,
                pair.Value, new(UnresolvedOwner: $"collection.item.sort.column_{pair.Key}")));
            publishedRows.Add(new(id, new ReadOnlyDictionary<int, CollectionItemCell>(cells)));
        }
        rows = publishedRows.AsReadOnly();
        UnresolvedCandidates = unresolved.AsReadOnly();
    }
}

public sealed partial class GameResourceRepository
{
    public CollectionItemCatalog LoadCollectionItemCatalog(string locale,
        IReadOnlyDictionary<int, CollectionItemContentAvailability>? availability = null)
    {
        if (locale is not ("TC" or "SC"))
            throw new ArgumentException("Collection locale must be TC or SC.", nameof(locale));
        var (bytes, source) = ReadSource($"0010_{locale}/fixdataexce.s14");
        var fixedData = S14Reader.Read(bytes, false);
        if (fixedData.FormatMarker != 0x66)
            throw new InvalidDataException("The CE item encyclopedia requires CE fixed definitions.");
        return new(locale, fixedData, source, LoadMessages(locale, MessageKind.Data), availability);
    }
}
