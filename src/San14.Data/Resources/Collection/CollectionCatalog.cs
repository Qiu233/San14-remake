using System.Buffers.Binary;
using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A title Collection publication from the installed fixed resources.
/// It has no campaign, item holder, discovery or profile state. Recreate it when
/// the containing resource set changes; no process-wide catalog cache is used.</summary>
public sealed class CollectionCatalog
{
    private static readonly int[] PoemImages =
        [1, 2, 3, 11, 4, 5, 6, 12, 7, 8, 9, 9, 9, 9, 9, 10, 10, 10, 10, 10];
    private readonly Dictionary<int, CollectionPoemDetail> poems;
    private readonly Dictionary<int, CollectionSceneryDetail> sceneries;

    public string Locale { get; }
    public string PoemsTitle { get; }
    public string SceneryTitle { get; }
    public string DescriptionCaption { get; }
    public string AuthorCaption { get; }
    public string EraCaption { get; }
    public string CityCaption { get; }
    public string AreaCaption { get; }
    public IReadOnlyList<CollectionColumn> PoemColumns { get; }
    public IReadOnlyList<CollectionColumn> SceneryColumns { get; }
    public IReadOnlyList<CollectionPoemRow> Poems { get; }
    public IReadOnlyList<CollectionSceneryRow> Sceneries { get; }
    public IReadOnlyList<SourceProvenance> Sources { get; }

    public static CollectionCatalog Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return repository.LoadCollectionCatalog(locale);
    }

    public CollectionPoemDetail ReadPoem(int itemId) => poems.TryGetValue(itemId, out var value)
        ? value : throw new ArgumentOutOfRangeException(nameof(itemId), "Not a Collection poem.");

    public CollectionSceneryDetail ReadScenery(int sceneryId) => sceneries.TryGetValue(sceneryId, out var value)
        ? value : throw new ArgumentOutOfRangeException(nameof(sceneryId), "Not a valid Collection scenery.");

    internal CollectionCatalog(string locale, S14Document fixedData, SourceProvenance fixedSource,
        LoadedMap mapSource, LoadedMessages messages)
    {
        Locale = locale;
        string Data(int section, int id)
        {
            var sections = messages.Messages.Sections;
            if ((uint)section >= (uint)sections.Count ||
                (uint)id >= (uint)sections[section].Records.Count ||
                sections[section].Records[id].Text is not string text)
                throw new InvalidDataException($"collection.data.{section}.{id:X}");
            return text;
        }
        PoemsTitle = Data(1, 0xB8);
        SceneryTitle = Data(1, 0xB7);
        DescriptionCaption = Data(1, 0x400);
        AuthorCaption = Data(1, 0x857);
        EraCaption = Data(1, 0x858);
        CityCaption = Data(1, 0x201);
        AreaCaption = Data(1, 0x202);
        PoemColumns = Array.AsReadOnly(new[]
        {
            new CollectionColumn(20, Data(1, 0xAE), 160, false),
            new CollectionColumn(21, AuthorCaption, 132, false),
            new CollectionColumn(22, EraCaption, 132, false)
        });
        SceneryColumns = Array.AsReadOnly(new[]
        {
            new CollectionColumn(0, Data(1, 0xAD), 188, false),
            new CollectionColumn(3, Data(1, 0x207), 76, true),
            new CollectionColumn(1, AreaCaption, 132, true)
        });
        Sources = Array.AsReadOnly(new[] { fixedSource, mapSource.Source, messages.Source });

        var itemDefinitions = S14ItemReader.ReadDefinitions(fixedData);
        var sceneryDefinitions = S14SceneryReader.ReadFixed(fixedData);
        var cities = S14ScenarioTables.ReadCityDefinitions(fixedData);
        var areas = S14ScenarioTables.ReadAreaDefinitions(fixedData);
        var map = mapSource.Map;
        // The Collection CCmnMapS initializer uses neutral color60 for every
        // static base, independently of a campaign's current ownership.
        var mapBases = new List<DetailMiniMapMarker>();
        for (int location = 1; location <= 61; location++)
        {
            if (location is >= 47 and <= 51) continue;
            mapBases.Add(DetailMiniMapProjection.Project(location <= 51
                ? DetailMiniMapIconKind.City : DetailMiniMapIconKind.Gate,
                location, DetailMiniMapLocations.ReadHex(location), 60));
        }
        var staticMapBases = mapBases.AsReadOnly();
        poems = [];
        var poemRows = new List<CollectionPoemRow>();
        foreach (var item in itemDefinitions.Definitions)
        {
            // Fixed CItem serializer: name14, category44, effect45, ... .
            // RawKind is effect45 and is deliberately not the category gate.
            if (item.Slot == 0 || item.RawBytes[14] != 26 || item.Slot is < 132 or > 151) continue;
            string author = Data(0, item.Slot + 0x16E2);
            string era = Data(0, item.Slot + 0x16F7);
            int imageId = PoemImages[item.Slot - 132];
            string suffix = imageId.ToString("D3", CultureInfo.InvariantCulture);
            poems.Add(item.Slot, new(item.Slot, item.Name, author, era,
                Data(0, item.Slot + 0x170C),
                new("texture_stl_poems_" + suffix, "stl_event_p_" + suffix + "_trim", 28, imageId)));
            poemRows.Add(new(item.Slot, item.Name, author, era,
                new(UnresolvedOwner: "collection.poem.name_sort")));
        }
        Poems = poemRows.AsReadOnly();

        sceneries = [];
        var sceneryRows = new List<CollectionSceneryRow>();
        foreach (var scenery in sceneryDefinitions.Sceneries)
        {
            // Native scenery validity follows its fixed hex and nonzero area;
            // collection mode omits the ordinary discovered/force gates.
            int hex = scenery.RawHex44 < map.Cells.Length ? scenery.RawHex44 : 0;
            int areaId = map.Cells[hex].AreaSlot;
            if (scenery.Slot == 0 || areaId == 0) continue;
            if ((uint)areaId >= (uint)areas.Count)
                throw new InvalidDataException("collection.scenery.hex_area_reference");
            var area = areas[areaId];
            bool areaValid = area.CitySlot != 0;
            var city = areaValid && area.CitySlot < cities.Count ? cities[area.CitySlot] : null;
            string cityName = city?.Name ?? "";
            // CAreaData's first 36 serialized bytes are name5 + sort13
            // UTF16 units. The general legacy reader splits these as 4+14;
            // keep this Collection projection at the actual source boundary.
            const int areaTableStart = 0x10E8 + 4 + 48400 * 3 + 8;
            var areaBytes = fixedData.DecodedPayload.AsSpan(areaTableStart + areaId * 65, 36);
            string areaName = areaValid ? ReadCodeUnits(areaBytes[..10]) : "";
            var citySort = city is null ? new CollectionSortValue(Number: int.MaxValue)
                : new CollectionSortValue(Text: city.SecondaryText);
            var areaSort = !areaValid ? new CollectionSortValue(Number: int.MaxValue)
                : new CollectionSortValue(Text: ReadCodeUnits(areaBytes[10..]));
            sceneryRows.Add(new(scenery.Slot, scenery.Name, cityName, areaName,
                scenery.RawHex44, new(UnresolvedOwner: "collection.scenery.name_sort"), citySort, areaSort));
            string suffix = scenery.RawField48.ToString("D3", CultureInfo.InvariantCulture);
            sceneries.Add(scenery.Slot, new(scenery.Slot, scenery.Name, cityName, areaName,
                scenery.RawHex44, Data(0, scenery.Slot + 0x16F6),
                new("texture_stl_scenery_" + suffix, "sti_scenery_" + suffix, 27, scenery.RawField48))
            {
                // Source kind42 takes the ordinary unit-marker branch. Its
                // force0 argument selects the same known neutral color60.
                MiniMap = scenery.RawHex44 < map.Cells.Length
                    ? new(staticMapBases, DetailMiniMapProjection.Project(
                        DetailMiniMapIconKind.Area, scenery.Slot, scenery.RawHex44, 60)) : null,
                MiniMapUnresolvedOwner = scenery.RawHex44 < map.Cells.Length
                    ? null : "collection.scenery.minimap_invalid_hex"
            });
        }
        Sceneries = sceneryRows.AsReadOnly();
    }

    private static string ReadCodeUnits(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[bytes.Length / 2];
        int count = 0;
        for (; count < chars.Length; count++)
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(count * 2, 2));
            if (value == 0) break;
            chars[count] = (char)value;
        }
        return new(chars, 0, count);
    }
}

public sealed partial class GameResourceRepository
{
    /// <summary>Uses the same file URI/override resolution as ordinary resource
    /// loading. No scenario is selected or initialized to browse Collection.</summary>
    public CollectionCatalog LoadCollectionCatalog(string locale)
    {
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        var (bytes, source) = ReadSource($"0010_{locale}/fixdataexce.s14");
        var fixedData = S14Reader.Read(bytes, false);
        if (fixedData.FormatMarker != 0x66)
            throw new InvalidDataException("Collection requires the CE fixed catalog.");
        return new(locale, fixedData, source, LoadCeMap(), LoadMessages(locale, MessageKind.Data));
    }
}
