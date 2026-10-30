using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Source 6225D0 followed by 5329A0/205240 at the fresh-game boundary.
/// The installed resource stays unchanged. The running game receives its own
/// immutable definitions, custody and random continuation.</summary>
public static class ItemEditorScenario
{
    public static LoadedScenario Apply(GameResourceRepository repository, LoadedScenario source,
        ItemEditorLibrary library, ItemEditorNewGameSelection selection, uint initializationSeed)
    {
        library.Validate(); selection.Validate(library);
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale is not ("TC" or "SC"))
            throw new InvalidDataException("Title item projection requires the verified CE new-game source.");
        string path = string.IsNullOrWhiteSpace(source.ScenarioSource.PhysicalPath)
            ? Path.Combine(repository.SourceDirectory, source.ScenarioSource.SourcePath) : source.ScenarioSource.PhysicalPath;
        var original = source.InitialScenarioDocument ?? S14Reader.Read(File.ReadAllBytes(path), true);
        var document = original with { DecodedPayload = original.DecodedPayload.ToArray() };
        var items = S14ItemReader.ReadScenarioItems(document, source.Snapshot.Locale);
        var messages = repository.LoadMessages(source.Snapshot.Locale, MessageKind.Data).Messages.Sections;
        string Description(int id) => messages[0].Records[id + 0x765].Text ??
            throw new InvalidDataException("Historical item description resource is absent.");
        var appearances = ImmutableDictionary.CreateBuilder<int, ItemAppearance>();
        uint crt = selection.CrtAfterManagement ?? source.InitialRegisteredOfficerRandom?.State ?? initializationSeed;
        var managedCustody = selection.Custodies.ToDictionary(item => item.ItemId);
        var selectedCreated = selection.CreatedIds.ToHashSet();
        var selectedHistorical = selection.HistoricalIds.ToHashSet();
        // The real 233C00 predicate uses original Area34, independently of
        // subsequently reassigned Area35. Snapshot.CitySlot comes from fixed data.
        var validAreas = source.Snapshot.Areas.Where(area => area.Slot is > 0 and <= 500 && area.CitySlot is >= 1 and <= 51)
            .Select(area => area.Slot).ToHashSet();
        foreach (var item in items.Items.Where(item => item.Slot >= 152)
                     .Concat(items.Items.Where(item => item.Slot is > 0 and < 152)))
        {
            int id = item.Slot;
            var definition = item.EmbeddedDefinition ?? throw new InvalidDataException("CE embedded item definition is missing.");
            var values = ItemEditorCatalog.ReadValues(definition);
            byte oldFlags = item.RetainedFlags ?? throw new InvalidDataException("Fresh item flags are unknown.");
            int image = values.ImageIndex + ((oldFlags & 0x10) != 0 ? 201 : 0);
            string description = id < 152 ? Description(id) : definition.SecondaryText ?? "";
            var row = document.DecodedPayload.AsSpan(item.RecordOffset, item.RecordStride);
            // 20A790 installs every valid custom editor definition before the
            // adoption page. Its dynamic description is a separate string owner.
            if (library.FindCreated(id) is { } created)
            {
                values = created.Values; image = values.ImageIndex; description = created.Description;
                Text(row.Slice(6, 14), created.Name);
                WriteValues(row, values);
                if (managedCustody.TryGetValue(id, out var management))
                {
                    //Actual51B810 decisions have already consumed their CRT
                    //and changed custody; do not reconstruct them at Confirm.
                    BinaryPrimitives.WriteUInt16LittleEndian(row, checked((ushort)(management.OfficerId ?? 0)));
                    BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(2, 2), management.Lifecycle.RetainedLocationCode!.Value);
                    row[4] = management.Lifecycle.Status!.Code!.Value;
                    //20A790's current image bit is independent of custody.
                    row[5] = (byte)((management.Lifecycle.RetainedFlags!.Value & 0xEF) | (row[5] & 0x10));
                }
                if (selectedCreated.Contains(id))
                {
                    row[58] = (byte)values.Kind;
                    row[5] = (byte)((row[5] & 0xFE) | 5);
                    if (BinaryPrimitives.ReadUInt16LittleEndian(row) == 0)
                    {
                        if (validAreas.Count == 0)
                            throw new InvalidDataException("Fresh custom item placement has no valid original Area.");
                        row[4] = 2;
                        int area;
                        do
                        {
                            crt = unchecked(crt * 0x343FDu + 0x269EC3u);
                            area = (int)((crt >> 16) & 0x7FFF) % 501;
                        } while (!validAreas.Contains(area));
                        BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(2, 2), (ushort)area);
                    }
                }
                else
                {
                    // 324580(person0, Area0, status3) precedes kind/flag writes.
                    BinaryPrimitives.WriteUInt16LittleEndian(row, 0);
                    BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(2, 2), 0);
                    row[4] = 3; row[58] = 0; row[5] &= 0xFA;
                    values = values with { Kind = 0 };
                }
            }
            // Source 205240 applies historical adoptions after custom placement.
            if (selectedHistorical.Contains(id) && library.FindHistorical(id) is { } historical)
            { values = historical.Values; image = values.ImageIndex; WriteValues(row, values); }
            var appearance = new ItemAppearance(values.Kind, values.AbilityKind, values.AbilityValue,
                values.EffectKind, values.EffectValue, image, description);
            appearance.Validate(); appearances.Add(id, appearance);
        }
        string sha = Convert.ToHexString(SHA256.HashData(document.DecodedPayload));
        return source with
        {
            InitialScenarioDocument = document, InitialRegisteredOfficerRandom = new(crt),
            InitialItemEditorState = new(selection, appearances.ToImmutable()),
            Metadata = source.Metadata with { DecodedSha256 = sha },
            Snapshot = source.Snapshot with { DecodedSha256 = sha,
                ScenarioItems = S14ItemReader.ReadScenarioItems(document, source.Snapshot.Locale),
                WorldParameters = S14ScenarioWorldReader.Read(document, source.Snapshot.Locale),
                ObjectSourceRecords = S14ScenarioObjectReader.Read(document) }
        };
    }
    private static void WriteValues(Span<byte> row, ItemEditorValues values)
    {
        values.Validate(); row[58] = (byte)values.Kind; row[59] = (byte)values.AbilityKind;
        row[60] = (byte)values.AbilityValue; row[61] = (byte)values.EffectKind;
        row[62] = (byte)values.EffectValue; row[63] = (byte)values.Worth;
        row[64] = (byte)(values.ImageIndex < 201 ? values.ImageIndex : values.ImageIndex - 201);
        row[5] = (byte)((row[5] & 0xEF) | (values.ImageIndex < 201 ? 0 : 0x10));
    }
    private static void Text(Span<byte> target, string text)
    {
        target.Clear(); int bytes = Encoding.Unicode.GetByteCount(text);
        if (bytes >= target.Length) throw new InvalidDataException("Created item name exceeds the original terminated window.");
        Encoding.Unicode.GetBytes(text, target);
    }
}
