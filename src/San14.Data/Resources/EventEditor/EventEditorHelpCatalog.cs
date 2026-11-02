using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record EventEditorHelpHint(int Id, byte Availability, int MessageKey,
    int RetainedBodyKey, ushort ImageId);
public sealed record EventEditorHelpDictionary(int Id, byte Category, byte Availability,
    int RetainedTitleKey, ImmutableArray<int> RetainedBodyKeys,
    ImmutableArray<ushort> PageImages, ushort DisplayOrder);
public sealed record EventEditorHelpPage(int MessageId, string Text, ushort ImageId,
    CollectionImageRoute? Image);
public sealed record EventEditorHelpTopic(int Id, string Title, ushort DisplayOrder,
    ImmutableArray<EventEditorHelpPage> Pages)
{
    public bool ShowMarker17 { get; init; }
    public bool ShowMarker18 { get; init; }
}

/// <summary>The ordinary mode-one event-editor help directory. Contents are
/// read from the installed fixed definitions and game messages; no event or
/// story program, world or editor library is created or executed.</summary>
public sealed partial class EventEditorHelpCatalog
{
    public string Locale { get; }
    public string Title { get; }
    public string CategoryTitle { get; }
    public string Introduction { get; }
    public IReadOnlyList<EventEditorHelpHint> Hints { get; }
    public IReadOnlyList<EventEditorHelpDictionary> Dictionaries { get; }
    public IReadOnlyList<EventEditorHelpTopic> Topics { get; }
    public IReadOnlyList<SourceProvenance> Sources { get; }
    private EventEditorHelpCatalog(string locale, string title, string category, string introduction,
        EventEditorHelpHint[] hints, EventEditorHelpDictionary[] dictionaries,
        EventEditorHelpTopic[] topics, SourceProvenance[] sources)
    {
        Locale = locale; Title = title; CategoryTitle = category; Introduction = introduction;
        Hints = Array.AsReadOnly(hints); Dictionaries = Array.AsReadOnly(dictionaries);
        Topics = Array.AsReadOnly(topics); Sources = Array.AsReadOnly(sources);
    }
    public static EventEditorHelpCatalog Load(GameResourceRepository repository, string locale) =>
        repository.LoadEventEditorHelpCatalog(locale);
    public EventEditorHelpTopic Topic(int id) => Topics.FirstOrDefault(topic => topic.Id == id) ??
        throw new InvalidDataException("The selected help topic is not admitted to event-editor mode.");

    internal static EventEditorHelpCatalog Read(string locale, S14Document fixedData, LoadedMessages data,
        LoadedMessages game, SourceProvenance fixedSource, Func<ushort, CollectionImageRoute> imageSource)
    {
        const int hintStart = 337731, hintCount = 49, hintStride = 11;
        const int dictionaryStart = 338278, dictionaryCount = 501, dictionaryStride = 26;
        ReadOnlySpan<byte> source = fixedData.DecodedPayload;
        int end = dictionaryStart + dictionaryCount * dictionaryStride;
        if (fixedData.FormatMarker != 0x66 || source.Length < end + 8 ||
            I32(source, hintStart - 8) != 28 || I32(source, hintStart - 4) != hintCount ||
            I32(source, dictionaryStart - 8) != 29 || I32(source, dictionaryStart - 4) != dictionaryCount ||
            I32(source, end) != 30 || I32(source, end + 4) != 51)
            throw new InvalidDataException("Event help requires the CE Hint/Dictionary fixed-table profile.");
        var hints = new List<EventEditorHelpHint>();
        for (int id = 1; id < hintCount; id++)
        {
            var row = source.Slice(hintStart + id * hintStride, hintStride);
            if (row[0] is not (1 or 3)) continue;
            hints.Add(new(id, row[0], I32(row, 1), I32(row, 5), U16(row, 9)));
        }
        var dictionaries = new List<EventEditorHelpDictionary>();
        for (int id = 1; id < dictionaryCount; id++)
        {
            var row = source.Slice(dictionaryStart + id * dictionaryStride, dictionaryStride);
            if (id == 96 || row[1] is not (1 or 3)) continue;
            if (row[0] > 8) throw new InvalidDataException("Help dictionary category exceeds its source nine groups.");
            dictionaries.Add(new(id, row[0], row[1], I32(row, 2),
                [I32(row, 6), I32(row, 10), I32(row, 14)],
                [U16(row, 18), U16(row, 20), U16(row, 22)], U16(row, 24)));
        }
        var topics = dictionaries.Where(row => row.Category == 8).OrderBy(row => row.DisplayOrder)
            .Select(row =>
            {
                var bodies = Enumerable.Range(0, 3).Select(page => Text(game.Messages, row.Id * 4 + 0xA060 + page)).ToArray();
                int count = bodies.Count(value => value.Length != 0);
                var pages = ImmutableArray.CreateBuilder<EventEditorHelpPage>(count);
                ushort image = 0;
                // The original counts nonempty bodies, then indexes source
                // pages directly. Do not compact a hole into a different page.
                for (int page = 0; page < count; page++)
                {
                    if (row.PageImages[page] != 0) image = row.PageImages[page];
                    pages.Add(new(row.Id * 4 + 0xA060 + page, bodies[page], image,
                        image == 0 ? null : imageSource(image)));
                }
                return new EventEditorHelpTopic(row.Id, Text(game.Messages, row.Id * 4 + 0xA05F),
                    row.DisplayOrder, pages.MoveToImmutable());
            }).ToArray();
        return new(locale, Data(0x579), Data(0x2E7), Text(game.Messages, 0x9FCD),
            hints.ToArray(), dictionaries.ToArray(), topics, [fixedSource, data.Source, game.Source]);
        string Data(int id) => data.Messages.Sections[1].Records[id].Text ??
            throw new InvalidDataException($"event_editor.help.data.{id:X}");
    }
    private static int I32(ReadOnlySpan<byte> source, int at) => BinaryPrimitives.ReadInt32LittleEndian(source.Slice(at, 4));
    private static ushort U16(ReadOnlySpan<byte> source, int at) => BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(at, 2));
    private static string Text(MessageDocument messages, int id)
    {
        if ((uint)(id / 10000) >= messages.Sections.Count ||
            (uint)(id % 10000) >= messages.Sections[id / 10000].Records.Count)
            throw new InvalidDataException($"event_editor.help.message.{id:X}");
        var record = messages.Sections[id / 10000].Records[id % 10000];
        var output = new StringBuilder(); var bytes = record.RawBytes; int at = 0;
        while (at < bytes.Length)
        {
            if (Has(7, 7, 1))
            {
                var literal = record.Literals.Single(value => value.Offset == at + 3);
                output.Append(literal.Text); at += 3 + literal.ByteLength;
                if (!Has(7, 7, 2)) throw Unknown(); at += 3;
            }
            else if (Has(5, 5, 5))
            { at += 3; if (at != bytes.Length) throw Unknown(); break; }
            else throw Unknown();
        }
        return output.ToString();
        bool Has(byte a, byte b, byte c) => at + 3 <= bytes.Length && bytes[at] == a && bytes[at + 1] == b && bytes[at + 2] == c;
        InvalidDataException Unknown() => new($"event_editor.help.message.{id:X}.at.{at}");
    }
}

public sealed partial class GameResourceRepository
{
    public EventEditorHelpCatalog LoadEventEditorHelpCatalog(string locale)
    {
        if (locale is not ("SC" or "TC")) throw new ArgumentOutOfRangeException(nameof(locale));
        var (bytes, source) = ReadSource($"0010_{locale}/fixdataexce.s14");
        var imageSource = CreateHelpImageResolver(locale);
        return EventEditorHelpCatalog.Read(locale, S14Reader.Read(bytes, false),
            LoadMessages(locale, MessageKind.Data), LoadMessages(locale, MessageKind.Game), source, imageSource);
    }
}
