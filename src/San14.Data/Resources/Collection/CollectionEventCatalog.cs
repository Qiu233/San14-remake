using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Normal title event resources and application occurrence history.
/// Browsing neither initializes a campaign nor evaluates conditions against a fabricated world.</summary>
public sealed partial class CollectionEventCatalog
{
    private readonly LoadedEvents events;
    private readonly LoadedMessages messages;
    private readonly Dictionary<int, S14EventRecord> records;
    private readonly IReadOnlyDictionary<int, string> scenarios;
    private readonly Dictionary<int, CollectionEventDetail> details = [];
    public string Locale { get; }
    public string Title => Data(1, 0x7DD);
    public string LayoutName => "09_01_01_event_list_pk_cn";
    public string RowLayoutName => "09_01_01_event_list_offset_00_cn";
    public string TriggerRowLayoutName => "09_01_01_event_list_offset_02";
    public IReadOnlyList<CollectionEventTab> Tabs { get; }
    public IReadOnlyList<CollectionEventRow> Rows { get; }
    public IReadOnlyList<SourceProvenance> Sources { get; }
    public bool CanSortByCompletion => Rows.All(row => row.Unlocked.HasValue);

    public static CollectionEventCatalog Load(GameResourceRepository repository, string locale,
        TransferServiceState? progress = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return repository.LoadCollectionEventCatalog(locale, progress);
    }

    internal CollectionEventCatalog(string locale, LoadedEvents events, LoadedMessages messages,
        IReadOnlyDictionary<int, string> scenarios, TransferServiceState? progress)
    {
        Locale = locale; this.events = events; this.messages = messages; this.scenarios = scenarios;
        records = new();
        foreach (var record in events.Document.Events)
            if (!records.TryAdd(record.Id, record)) throw new InvalidDataException("collection.event.duplicate_resource_identity");
        Sources = Array.AsReadOnly(new[] { events.Source, messages.Source });
        int[] captions = [0x49D,0x49A,0xAE,0x49C,0x7E4];
        Tabs = Array.AsReadOnly(captions.Select((caption, index) => new CollectionEventTab(index, Data(1, caption))).ToArray());
        // The number mapping includes ALL resource rows, including the zero
        // record, before the Collection's own candidate filter is applied.
        var numbers = new Dictionary<int, int>(); int next = 0;
        foreach (var record in events.Document.Events.OrderBy(record => record.RawField34)) numbers[record.RawField34] = next++;
        var rows = new List<CollectionEventRow>();
        foreach (var record in events.Document.Events)
        {
            int kind = Word(record, 4);
            // 3E2CC0's actual title arguments short-circuit year/mode checks;
            // its CE content predicate is constant false, so no DLC gate is read.
            if (record.Id == 0 || (kind == 0 && record.Id != 950 && record.Id <= 951)) continue;
            int variants = CollectionEventDefinitions.Directives.Any(d => d.Event == record.Id && d.Kind == 2) ? 2 : 1;
            for (int variant = 0; variant < variants; variant++)
            {
                bool? unlocked = record.Id is >= 0 and < 1024 ? progress?.ReadEventFlag(record.Id) : false;
                string title = Data(3, checked(record.Id * 4 + 1));
                if (variants == 2)
                {
                    var directive = CollectionEventDefinitions.Directives.First(d => d.Event == record.Id && d.Kind == 2);
                    title += Data(1, variant == 0 ? directive.First : directive.Second);
                }
                else if (record.Id >= 950 && unlocked == false) title = Data(1, 0x7DB);
                rows.Add(new(rows.Count, record.Id, variant, record.Id < 900 ? numbers[record.RawField34] : null, title, unlocked)
                {
                    Kind = kind, TriggerKind = Word(record, 8), RepeatMode = Word(record, 0x30),
                    Markers = ReadMarkers(record), Thumbnail = ReadThumbnail(record, unlocked),
                    UnresolvedOwner = unlocked is null ? "collection.event.application_occurrence_history" : null
                });
            }
        }
        Rows = rows.AsReadOnly();
    }

    public IReadOnlyList<CollectionEventRow> ReadRows(int tabId, int sortColumn = 0, bool ascending = true)
    {
        if ((uint)tabId >= 5) throw new ArgumentOutOfRangeException(nameof(tabId));
        if ((uint)sortColumn >= 2) throw new ArgumentOutOfRangeException(nameof(sortColumn));
        if (sortColumn == 1 && !CanSortByCompletion)
            throw new InvalidOperationException("collection.event.application_occurrence_history");
        var result = Rows.Where(row => Included(tabId, row.EventId >= 950 ? 5 : row.Kind)).ToList();
        // Strict insertion preserves source order for the original comparator's equal keys.
        for (int i = 1; i < result.Count; i++)
        {
            var value = result[i]; int prior = i - 1;
            while (prior >= 0 && Compare(value, result[prior], sortColumn, ascending) < 0)
            { result[prior + 1] = result[prior]; prior--; }
            result[prior + 1] = value;
        }
        return result.AsReadOnly();
    }

    private int Compare(CollectionEventRow a, CollectionEventRow b, int column, bool ascending)
    {
        int value = column == 1 ? a.Unlocked!.Value.CompareTo(b.Unlocked!.Value) : 0;
        if (value == 0)
        {
            if (a.EventId >= 950 && b.EventId >= 950)
            {
                int x = Array.IndexOf(CollectionEventDefinitions.EndingOrder, a.EventId);
                int y = Array.IndexOf(CollectionEventDefinitions.EndingOrder, b.EventId);
                value = Math.Max(x, 0).CompareTo(Math.Max(y, 0));
            }
            else value = records[a.EventId].RawField34.CompareTo(records[b.EventId].RawField34);
        }
        return ascending ? value : -value;
    }

    private static bool Included(int tab, int category) => tab switch
    { 0 => category is 1 or 3 or 4 or 5, 1 => category == 1, 2 => category == 4, 3 => category == 3, 4 => category == 5, _ => false };

    private CollectionEventRow Row(int id) => (uint)id < (uint)Rows.Count ? Rows[id] : throw new ArgumentOutOfRangeException(nameof(id));
    private static int Word(S14EventRecord record, int offset) => BinaryPrimitives.ReadInt32LittleEndian(record.RawBytes.AsSpan(offset, 4));
    private string Data(int section, int index)
    {
        var rows = messages.Messages.Sections[section].Records;
        // The original Data getter returns its empty string outside the table.
        return (uint)index < (uint)rows.Count ? rows[index].Text ?? "" : "";
    }

    private IReadOnlyList<CollectionEventMarker> ReadMarkers(S14EventRecord record)
    {
        var result = new List<CollectionEventMarker>();
        if (record.Id is >= 339 and <= 445 or >= 447 and <= 557) Add(41, 0x337);
        else if (record.Id is >= 956 and <= 961) Add(41, 0x2FA);
        switch (Word(record, 4)) { case 1: Add(19,0x2D6); break; case 3: Add(21,0x2D8); break; case 4: Add(22,0x332); break; }
        switch (Word(record, 8)) { case 2: Add(37,0x2DD); break; case 1: Add(38,0x2DE); break; }
        if (record.Id < 950) Add(Word(record,0x30) == 0 ? 39 : 40, Word(record,0x30) == 0 ? 0x2DF : 0x2E0);
        return result.AsReadOnly();
        void Add(int node, int sprite)
        {
            string name = sprite switch
            { 0x2D6 => "000_cmn_mrk_ev_a", 0x2D8 => "000_cmn_mrk_ev_c", 0x332 => "000_cmn_mrk_ev_l",
              0x2DD => "000_cmn_mrk_ev_h", 0x2DE => "000_cmn_mrk_ev_i", 0x2DF => "000_cmn_mrk_ev_j",
              0x2E0 => "000_cmn_mrk_ev_k", 0x337 => "020_sho_mrk_ce", 0x2FA => "020_sho_mrk_pk", _ => throw new InvalidDataException("collection.event.marker") };
            string language = Locale == "SC" ? "ch" : "tw";
            result.Add(new(node, sprite) { Image = new($"texture_{(sprite is 0x332 or 0x337 ? "ce_" : "")}lang_{language}", $"{language}_{name}", sprite is 0x332 or 0x337 ? 5 : 3, sprite) });
        }
    }

    private static CollectionImageRoute? ReadThumbnail(S14EventRecord record, bool? unlocked)
    {
        if (unlocked != true) return null;
        int type = Word(record, 0x18), index = Word(record, 0x1C);
        if (index == 0) index = 1;
        if (type is not (>= 1 and <= 4))
        {
            if (record.Id < 900) return null;
            type = 3; index = 21;
        }
        (int kind, string suffix) = type switch { 1 => (15,"bg_"), 2 => (14,"d_"), 3 => (13,""), 4 => (16,"komono_"), _ => throw new InvalidDataException("collection.event.thumbnail") };
        return new($"texture_stl_event_{suffix}{index:D3}", $"stl_event_{suffix}{index:D3}", kind, index);
    }
}

public sealed partial class GameResourceRepository
{
    public CollectionEventCatalog LoadCollectionEventCatalog(string locale, TransferServiceState? progress = null)
    {
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        var scenarios = EffectiveScenarios.Where(s => s.Locale == locale).ToDictionary(s => s.Number, s => s.SourcePath);
        return new(locale, LoadEvents(CampaignEventCatalog.SourcePath), LoadMessages(locale, MessageKind.Data), scenarios, progress);
    }
}
