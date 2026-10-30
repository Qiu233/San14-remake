using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using San14.Core;

namespace San14.Data;

/// <summary>The independently saved title event library. Runtime eligibility
/// and occurrence history belong to the current campaign, never this archive.</summary>
public sealed class EditedEventStore
{
    public const int CurrentVersion = 1;
    public const string LibraryResourceUri = "san14://user/edited-events";
    private const string Format = "san14-remake-edited-events";
    private static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        IgnoreReadOnlyProperties = true,
        WriteIndented = true
    };
    private readonly string path;
    private EditedEventLibrary current;
    private readonly Dictionary<int, EditedEventCompiledDefinition> compiled = [];
    private EditedEventStore(string path, EditedEventLibrary current, byte[] storedBytes)
    { this.path = Path.GetFullPath(path); this.current = current; Revision = Hash(storedBytes); }
    public EditedEventLibrary Current => current;
    public string ResourceUri => LibraryResourceUri;
    public string Revision { get; private set; }

    public EditedEventCompiledDefinition Compile(int slot)
    {
        if (!compiled.TryGetValue(slot, out var value))
            compiled[slot] = value = new(current.FindEvent(slot) ?? throw Invalid("event slot is absent"),
                ResourceUri, Revision);
        return value;
    }

    public static EditedEventStore CreateNew(string path)
    {
        var library = EditedEventLibrary.Empty;
        byte[] bytes = Encode(library);
        AtomicSaveFiles.Write(path, bytes, overwrite: false);
        return new(path, library, bytes);
    }
    public static EditedEventStore OpenExisting(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return new(path, Decode(bytes), bytes);
    }
    public EditedEventLibrary Save(EditedEventLibrary next)
    {
        byte[] bytes = Encode(next);
        AtomicSaveFiles.Write(path, bytes);
        current = next;
        Revision = Hash(bytes);
        compiled.Clear();
        return current;
    }

    private sealed record Envelope(string Format, int Version, EditedEventLibrary Library);
    private static byte[] Encode(EditedEventLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        library.Validate();
        ValidateStorage(library);
        //OccurrenceId is derived from Slot and deliberately excluded by the
        //read-only-property policy. Save only the semantic authoring values.
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope(Format, CurrentVersion, library), Json);
    }
    private static EditedEventLibrary Decode(byte[] bytes)
    {
        try
        {
            var document = Object(JsonNode.Parse(bytes), "Format", "Version", "Library");
            if (document["Format"]?.GetValue<string>() != Format ||
                document["Version"]?.GetValue<int>() != CurrentVersion)
                throw Invalid("format or version is unsupported");
            var library = Object(document["Library"], "Events", "Messages");
            var events = library["Events"] as JsonArray ?? throw Invalid("event inventory is missing");
            var messages = library["Messages"] as JsonArray ?? throw Invalid("message inventory is missing");
            if (events.Count > EditedEventLibrary.EventCapacity || messages.Count > EditedEventLibrary.MessageCapacity)
                throw Invalid("slot inventory exceeds its source capacity");
            foreach (var node in events)
            {
                var item = Object(node, "Slot", "Name", "Description", "Category", "Enabled", "Automatic", "Repeat",
                    "Conditions", "Contents", "Results");
                Program(item["Conditions"]);
                Program(item["Results"]);
                var contents = item["Contents"] as JsonArray ?? throw Invalid("content inventory is missing");
                foreach (var content in contents) Program(Object(content, "Program")["Program"]);
            }
            foreach (var node in messages) Object(node, "Slot", "Text");
            var result = document.Deserialize<Envelope>(Json)?.Library ?? throw Invalid("library is empty");
            result.Validate();
            ValidateStorage(result);
            return result;
        }
        catch (JsonException error) { throw new InvalidDataException("Edited event archive has invalid JSON or field types.", error); }
        catch (InvalidOperationException error) { throw new InvalidDataException("Edited event archive has invalid field types.", error); }

        static JsonObject Object(JsonNode? node, params string[] fields)
        {
            if (node is not JsonObject item || item.Count != fields.Length || fields.Any(field => !item.ContainsKey(field)))
                throw Invalid("record is incomplete or has unknown fields");
            return item;
        }
        static void Program(JsonNode? node)
        {
            var program = Object(node, "Instructions");
            var statements = program["Instructions"] as JsonArray ?? throw Invalid("statement inventory is missing");
            foreach (var statement in statements)
            {
                var item = Object(statement, "Code", "Arguments", "SettingCondition", "Suppression");
                if (item["Arguments"] is not JsonArray { Count: 8 })
                    throw Invalid("statement requires eight operands");
            }
        }
    }
    private static void ValidateStorage(EditedEventLibrary library)
    {
        if (EditedEventArticleStorage.UsedStorageBytes(library) > EditedEventArticleStorage.CapacityBytes)
            throw Invalid("article storage exceeds its source capacity");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static InvalidDataException Invalid(string reason) => new("Edited event archive " + reason + ".");
}
