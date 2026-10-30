using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using San14.Core;

namespace San14.Data;

/// <summary>Title-only item edits. Saving publishes Current only after the
/// atomic replacement succeeds; cancelling a draft never reaches this owner.</summary>
public sealed class ItemEditorStore
{
    public const int CurrentVersion = 1;
    private const string Format = "san14-remake-item-editors";
    private static readonly JsonSerializerOptions Json = new()
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true };
    private readonly string path;
    private ItemEditorLibrary current;
    private ItemEditorStore(string path, ItemEditorLibrary library)
    { this.path = Path.GetFullPath(path); current = library; }
    public ItemEditorLibrary Current => current;
    public static ItemEditorStore CreateNew(string path)
    {
        AtomicSaveFiles.Write(path, Encode(ItemEditorLibrary.Empty), overwrite: false);
        return new(path, ItemEditorLibrary.Empty);
    }
    public static ItemEditorStore OpenExisting(string path) => new(path, Decode(File.ReadAllBytes(path)));
    public ItemEditorLibrary Save(ItemEditorLibrary next)
    {
        AtomicSaveFiles.Write(path, Encode(next)); current = next; return current;
    }
    private sealed record Envelope(string Format, int Version, ItemEditorLibrary Library);
    private static byte[] Encode(ItemEditorLibrary library)
    { library.Validate(); return JsonSerializer.SerializeToUtf8Bytes(new Envelope(Format, CurrentVersion, library), Json); }
    private static ItemEditorLibrary Decode(byte[] bytes)
    {
        try
        {
            var root = JsonNode.Parse(bytes) as JsonObject ?? throw new InvalidDataException("Item archive is empty.");
            Require(root, "Format", "Version", "Library");
            if (root["Format"]?.GetValue<string>() != Format || root["Version"]?.GetValue<int>() != CurrentVersion ||
                root["Library"] is not JsonObject library)
                throw new InvalidDataException("Unsupported item archive format or version.");
            Require(library, "Historical", "Created");
            if (library["Historical"] is not JsonArray historical || historical.Count > 151 ||
                library["Created"] is not JsonArray created || created.Count > 50)
                throw new InvalidDataException("Item archive slot counts are invalid.");
            foreach (var item in historical)
            {
                if (item is not JsonObject row) throw new InvalidDataException("Invalid historical item record.");
                Require(row, "Id", "Values"); RequireValues(row);
            }
            foreach (var item in created)
            {
                if (item is not JsonObject row) throw new InvalidDataException("Invalid created item record.");
                Require(row, "Id", "Name", "Description", "Values"); RequireValues(row);
            }
            var result = root.Deserialize<Envelope>(Json)?.Library ?? throw new InvalidDataException("Missing item archive library.");
            result.Validate(); return result;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        { throw new InvalidDataException("Item archive has invalid field types.", error); }
    }
    private static void RequireValues(JsonObject row)
    {
        if (row["Values"] is not JsonObject values) throw new InvalidDataException("Missing item values.");
        Require(values, "Kind", "AbilityKind", "AbilityValue", "EffectKind", "EffectValue", "Worth", "ImageIndex");
    }
    private static void Require(JsonObject row, params string[] fields)
    {
        if (row.Count != fields.Length || fields.Any(field => !row.ContainsKey(field)))
            throw new InvalidDataException("Item archive contains an incomplete record.");
    }
}
