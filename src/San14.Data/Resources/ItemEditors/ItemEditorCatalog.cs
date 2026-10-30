using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ItemEditorCandidate(int Id, string Name, string Description,
    ItemEditorValues OriginalValues, ItemEditorValues Values, bool Edited);
public sealed record ItemEditorChoice(int Id, string Caption);

/// <summary>The ordinary title editor's own admission and field catalog.
/// This does not reuse encyclopedia admission, discovery or campaign custody.</summary>
public sealed class ItemEditorCatalog
{
    public static readonly ImmutableArray<int> SelectableKinds =
        [1, 21, 25, 22, 24, 23, 20, 18, 15, 14, 11, 2, 3, 5, 4, 6, 7, 10, 26];
    private readonly LoadedMessages messages;
    private readonly Dictionary<int, ItemEditorCandidate> originals;
    private readonly IReadOnlyList<S14ItemDefinition> physicalDefinitions;
    public string Locale { get; }
    public IReadOnlyList<ItemEditorChoice> Kinds { get; }
    public IReadOnlyList<ItemEditorChoice> AbilityKinds { get; }
    public IReadOnlyList<ItemEditorChoice> EffectKinds { get; }
    public string Data(int id) => Data(1, id);
    public string Data(int section, int id)
    {
        if ((uint)section >= (uint)messages.Messages.Sections.Count ||
            (uint)id >= (uint)messages.Messages.Sections[section].Records.Count ||
            messages.Messages.Sections[section].Records[id].Text is not string value)
            throw new InvalidDataException($"item.editor.data.{section}.{id:X}");
        return value;
    }
    public ItemEditorCatalog(GameResourceRepository repository, LoadedScenario definitions, string locale)
    {
        Locale = locale; messages = repository.LoadMessages(locale, MessageKind.Data);
        if (definitions.Snapshot.Locale != locale || definitions.Snapshot.ItemDefinitions is not { } items)
            throw new InvalidDataException("Title item editor definitions are unavailable for this language.");
        originals = []; physicalDefinitions = items.Definitions;
        foreach (var item in items.Definitions)
        {
            if (item.Slot is < 1 or > 151 || item.Slot is >= 125 and <= 127 || item.RawBytes.Length != 22) continue;
            int kind = item.RawBytes[14];
            // Original 4C79D0: exclude kind 0, 8/9 and mask B3000 at kinds <=19.
            if (kind == 0 || kind <= 19 && ((0xB3000 >> kind) & 1) != 0 || kind is 8 or 9) continue;
            var values = ReadValues(item);
            originals.Add(item.Slot, new(item.Slot, item.Name, Data(0, item.Slot + 0x765), values, values, false));
        }
        Kinds = SelectableKinds.Select(kind => new ItemEditorChoice(kind, Data(0, 0x81E + kind))).ToArray();
        AbilityKinds = Enumerable.Range(1, 5).Select(kind => new ItemEditorChoice(kind, Data(0x237 + kind)))
            .Append(new(0, Data(0x53F))).ToArray();
        EffectKinds = [new(1, Data(0x5F1)), new(2, Data(0x5B4)), new(0, Data(0x53F))];
    }
    public IReadOnlyList<ItemEditorCandidate> Historical(ItemEditorLibrary library, bool editedOnly = false) =>
        originals.Values.Where(item => !editedOnly || library.FindHistorical(item.Id) is not null)
            .Select(item => library.FindHistorical(item.Id) is { } edit
                ? item with { Values = edit.Values, Edited = true } : item).ToArray();
    public ItemEditorCandidate Historical(int id, ItemEditorLibrary library)
    {
        if (!originals.TryGetValue(id, out var item)) throw new InvalidDataException("Item is not admitted to the historical editor.");
        return library.FindHistorical(id) is { } edit ? item with { Values = edit.Values, Edited = true } : item;
    }
    public IReadOnlyList<ItemEditorCandidate> Templates(ItemEditorLibrary library) =>
        Historical(ItemEditorLibrary.Empty).Concat(library.Created.Where(item => item.Values.Kind != 0)
            .Select(item => new ItemEditorCandidate(item.Id, item.Name, item.Description, item.Values, item.Values, false))).ToArray();
    public IReadOnlyList<ItemExchangeImageSource> PhysicalImages(ItemEditorLibrary library) =>
        physicalDefinitions.Select(item => library.FindCreated(item.Slot) is { } created
            ? new ItemExchangeImageSource(item.Slot, created.Values.Kind, created.Values.ImageIndex)
            : new ItemExchangeImageSource(item.Slot, ReadValues(item).Kind, ReadValues(item).ImageIndex)).ToArray();
    public IReadOnlyList<ItemEditorCandidate> NormalImages() => physicalDefinitions
        .Where(item => item.Slot is >= 1 and <= 151 && item.Slot is not (>=104 and <=108) &&
            item.Slot is not (>=125 and <=127) && ReadValues(item).Kind != 0)
        .Select(item => new ItemEditorCandidate(item.Slot, item.Name, "", ReadValues(item), ReadValues(item), false)).ToArray();
    public ItemEditorLibrary CommitHistorical(ItemEditorLibrary library, int id, ItemEditorValues values)
    {
        var source = Historical(id, ItemEditorLibrary.Empty); values.Validate();
        return source.OriginalValues == values ? library.RestoreHistorical([id]) : library.SetHistorical(new(id, values));
    }
    public string Ability(ItemEditorValues values) => values.AbilityKind == 0 || values.AbilityValue == 0
        ? Data(0x53F) : Data(0x237 + values.AbilityKind) + "+" + values.AbilityValue;
    public string Effect(ItemEditorValues values) => values.EffectKind == 0 || values.EffectKind == 2 && values.EffectValue == 0
        ? Data(0x53F) : values.EffectKind == 1 ? Data(0x5F1) : Data(0x5B4) + "+" + values.EffectValue;
    public string Kind(ItemEditorValues values) => values.Kind == 0 ? Data(0x53F) : Data(0, 0x81E + values.Kind);

    internal static ItemEditorValues ReadValues(S14ItemDefinition definition)
    {
        int offset = definition.RecordStride switch { 22 => 14, 59 => 52,
            _ => throw new InvalidDataException("Item editor source definition stride is unsupported.") };
        var row = definition.RawBytes;
        if (row.Length != definition.RecordStride) throw new InvalidDataException("Item editor source row is incomplete.");
        return new(Math.Min(row[offset], (byte)26), Math.Min(row[offset + 1], (byte)5),
            Math.Min(row[offset + 2], (byte)10), Math.Min(row[offset + 3], (byte)2),
            Math.Min(row[offset + 4], (byte)10), Math.Min(row[offset + 5], (byte)50), row[^1]);
    }
}
