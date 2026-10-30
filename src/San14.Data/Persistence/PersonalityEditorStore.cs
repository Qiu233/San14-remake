using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using San14.Core;

namespace San14.Data;

/// <summary>The title library publishes only after its atomic replacement succeeds.</summary>
public sealed class PersonalityEditorStore
{
    public const int CurrentVersion = 1;
    private const string Format = "san14-remake-personality-editors";
    private static readonly JsonSerializerOptions Json = new()
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true };
    private readonly string path;
    private PersonalityEditorLibrary current;
    private PersonalityEditorStore(string path, PersonalityEditorLibrary current)
    { this.path = Path.GetFullPath(path); this.current = current; }
    public PersonalityEditorLibrary Current => current;
    public static PersonalityEditorStore CreateNew(string path)
    {
        AtomicSaveFiles.Write(path, Encode(PersonalityEditorLibrary.Empty), overwrite: false);
        return new(path, PersonalityEditorLibrary.Empty);
    }
    public static PersonalityEditorStore OpenExisting(string path) => new(path, Decode(File.ReadAllBytes(path)));
    public PersonalityEditorLibrary Save(PersonalityEditorLibrary next)
    { AtomicSaveFiles.Write(path, Encode(next)); current = next; return current; }
    private sealed record Envelope(string Format, int Version, PersonalityEditorLibrary Library);
    private static byte[] Encode(PersonalityEditorLibrary value)
    { value.Validate(); return JsonSerializer.SerializeToUtf8Bytes(new Envelope(Format, CurrentVersion, value), Json); }
    private static PersonalityEditorLibrary Decode(byte[] bytes)
    {
        try
        {
            var root = JsonNode.Parse(bytes) as JsonObject ?? throw new InvalidDataException("Empty personality archive.");
            Require(root, "Format", "Version", "Library");
            if (root["Format"]?.GetValue<string>() != Format || root["Version"]?.GetValue<int>() != CurrentVersion ||
                root["Library"] is not JsonObject library)
                throw new InvalidDataException("Unsupported personality archive format or version.");
            Require(library, "Created");
            if (library["Created"] is not JsonArray rows || rows.Count > 100)
                throw new InvalidDataException("Invalid personality archive capacity.");
            foreach (var item in rows)
            {
                if (item is not JsonObject row) throw new InvalidDataException("Invalid personality record.");
                Require(row, "Id", "Name", "ConditionKind", "ConditionValue", "OptionKind", "OptionValue",
                    "Effect1Code", "Effect1Value", "Effect2Code", "Effect2Value");
            }
            var result = root.Deserialize<Envelope>(Json)?.Library ?? throw new InvalidDataException("Missing personality library.");
            result.Validate(); return result;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        { throw new InvalidDataException("Invalid personality archive field types.", error); }
    }
    private static void Require(JsonObject value, params string[] fields)
    {
        if (value.Count != fields.Length || fields.Any(field => !value.ContainsKey(field)))
            throw new InvalidDataException("Incomplete personality archive record.");
    }
}
