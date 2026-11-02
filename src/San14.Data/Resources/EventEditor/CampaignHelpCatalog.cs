using System.Buffers.Binary;
using System.Collections.Immutable;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignHelpCategory(int Id, string Title, IReadOnlyList<EventEditorHelpTopic> Topics);

public sealed partial class EventEditorHelpCatalog
{
    public int Mode { get; private init; } = 1;
    public IReadOnlyList<EventEditorHelpTopic> HintTopics { get; private init; } = [];
    public IReadOnlyList<CampaignHelpCategory> Categories { get; private init; } = [];

    internal static EventEditorHelpCatalog Campaign(string locale, EventEditorHelpCatalog definitions,
        LoadedMessages data, LoadedMessages game, Func<ushort, CollectionImageRoute> imageSource)
    {
        var formatter = new NarrationResourceMessages(game.Messages);
        var hintTopics = definitions.Hints.Select(row => Topic(row.Id,
            row.MessageKey * 4 + 0x9FCA, _ => row.ImageId,
            HintMarker18.Contains(row.Id), HintMarker17.Contains(row.Id))).ToArray();
        var dictionaries = definitions.Dictionaries.Where(row => row.Category is >= 1 and <= 7)
            .OrderBy(row => row.DisplayOrder).Select(row => Topic(row.Id, row.Id * 4 + 0xA05F,
                page => row.PageImages[page], DictionaryMarker18.Contains(row.Id),
                DictionaryMarker17.Contains(row.Id), row.DisplayOrder)).ToArray();
        int[] labels = [0x2B9, 0x3CD, 0x211, 0x5C0, 0x2A0, 0x563, 0x5ED, 0x7B];
        var groups = Enumerable.Range(0, 8).Select(category => new CampaignHelpCategory(category,
            Data(labels[category]), category == 0 ? hintTopics : dictionaries.Where(topic =>
                definitions.Dictionaries.Single(row => row.Id == topic.Id).Category == category).ToArray())).ToArray();
        return new(locale, definitions.Title, Data(labels[0]), "", definitions.Hints.ToArray(),
            definitions.Dictionaries.ToArray(), dictionaries, definitions.Sources.ToArray())
            { Mode = 0, HintTopics = Array.AsReadOnly(hintTopics), Categories = Array.AsReadOnly(groups) };

        EventEditorHelpTopic Topic(int id, int titleKey, Func<int, ushort> imageAt,
            bool marker18, bool marker17, ushort order = 0)
        {
            string title = formatter.ResolveCampaignHelpContent(titleKey);
            string[] bodies = Enumerable.Range(1, 3).Select(page =>
                formatter.ResolveCampaignHelpContent(titleKey + page)).ToArray();
            int count = bodies.Count(value => value.Length != 0);
            var pages = ImmutableArray.CreateBuilder<EventEditorHelpPage>(count);
            ushort retainedImage = 0;
            for (int page = 0; page < count; page++)
            {
                ushort selected = imageAt(page);
                if (selected != 0) retainedImage = selected;
                pages.Add(new(titleKey + page + 1, bodies[page], retainedImage,
                    retainedImage == 0 ? null : imageSource(retainedImage)));
            }
            return new(id, title, order, pages.MoveToImmutable())
                { ShowMarker18 = marker18, ShowMarker17 = !marker18 && marker17 };
        }
        string Data(int id) => data.Messages.Sections[1].Records[id].Text ??
            throw new InvalidDataException($"campaign.help.data.{id:X}");
    }

    private static readonly HashSet<int> HintMarker18 = [15,16,21,22,23,24,28,29,30];
    private static readonly HashSet<int> HintMarker17 = [6,10,14,20,31];
    private static readonly HashSet<int> DictionaryMarker18 =
        [9,20,24,25,32,34,35,37,38,39,40,41,42,43,44,46,47,48,89,95,99,102,108,119,124,128,
         129,130,131,132,133,134,135,136,137,138,139,140,141,142,143,144,145];
    private static readonly HashSet<int> DictionaryMarker17 =
        [5,23,31,32,34,35,38,43,59,65,66,71,75,77,78,79,84,91,93,95,
         98,99,100,101,102,103,104,105,106,107,108,109,110,118,119,121,124,125,126];
}

public sealed partial class GameResourceRepository
{
    public EventEditorHelpCatalog LoadCampaignHelpCatalog(string locale)
    {
        // Reuse the existing fixed-table admission and mode-one parser. Only
        // normal topics need the existing message evaluator's colour dialect.
        var definitions = LoadEventEditorHelpCatalog(locale);
        return EventEditorHelpCatalog.Campaign(locale, definitions,
            LoadMessages(locale, MessageKind.Data), LoadMessages(locale, MessageKind.Game),
            CreateHelpImageResolver(locale));
    }

    private Func<ushort, CollectionImageRoute> CreateHelpImageResolver(string locale)
    {
        var declarations = LoadObjects("KIDSSystemResource", 0x208BE13A).Document.Objects;
        var settings = declarations.Single(value => value.ObjectId == 0xE0D0BB40);
        byte[] keys = settings.Properties.Single(value => value.NameHash == 0x39187761).RawValue;
        if (keys.Length % 4 != 0) throw new InvalidDataException("Help texture declaration names are malformed.");
        var names = new HashSet<uint>();
        for (int at = 0; at < keys.Length; at += 4)
            names.Add(BinaryPrimitives.ReadUInt32LittleEndian(keys.AsSpan(at, 4)));
        string language = locale == "SC" ? "ch" : "tw";
        return id =>
        {
            string number = id.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            foreach (string suffix in new[] { "_cn", "_pk", "" })
            {
                string pack = $"texture_stl_help_{language}{suffix}_{number}";
                if (names.Contains(ResourceNameHash.Compute(pack)))
                    return new(pack, $"{language}_win_stl_ss_{number}{suffix}", 0x12, id);
            }
            throw new InvalidDataException($"event_editor.help.image.{locale}.{id}");
        };
    }
}
