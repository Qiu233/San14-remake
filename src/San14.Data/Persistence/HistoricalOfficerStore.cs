using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using San14.Core;

namespace San14.Data;

/// <summary>The durable historical-officer override profile. New games adopt
/// its source projection; running and restored campaigns keep their own state.</summary>
public sealed class HistoricalOfficerStore
{
    public const int CurrentVersion = 1;
    private const string Format = "san14-remake-historical-officers";
    private static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };
    private readonly string path;
    private HistoricalOfficerProfile current;

    private HistoricalOfficerStore(string path, HistoricalOfficerProfile current)
    { this.path = Path.GetFullPath(path); this.current = current; }

    public HistoricalOfficerProfile Current => current;

    public static HistoricalOfficerStore CreateNew(string path)
    {
        var profile = HistoricalOfficerProfile.Empty;
        AtomicSaveFiles.Write(path, Encode(profile), overwrite: false);
        return new(path, profile);
    }

    public static HistoricalOfficerStore OpenExisting(string path) => new(path, Decode(File.ReadAllBytes(path)));

    public HistoricalOfficerProfile Save(HistoricalOfficerProfile next)
    {
        byte[] bytes = Encode(next);
        AtomicSaveFiles.Write(path, bytes);
        current = next;
        return current;
    }

    private sealed record Envelope(string Format, int Version, HistoricalOfficerProfile Profile);

    private static byte[] Encode(HistoricalOfficerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope(Format, CurrentVersion, profile), Json);
    }

    private static HistoricalOfficerProfile Decode(byte[] bytes)
    {
        JsonObject document = JsonNode.Parse(bytes) as JsonObject
            ?? throw new InvalidDataException("Historical officer archive is invalid.");
        if (document.Count != 3 || document["Format"]?.GetValue<string>() != Format ||
            document["Version"]?.GetValue<int>() != CurrentVersion || document["Profile"] is not JsonObject profile ||
            profile.Count != 1 || profile["Overrides"] is not JsonArray officers || officers.Count > 1088)
            throw new InvalidDataException("Historical officer archive format or version is unsupported.");
        string[] fields = ["Id", "PortraitId", "DeathYear", "LikePersonIds", "DislikePersonIds",
            "Leadership", "Strength", "Intelligence", "Politics", "Charm", "TraitIds", "FormationIds",
            "TacticIds", "DoctrineId", "PolicyId", "PolicyBaseCost"];
        foreach (var node in officers)
            if (node is not JsonObject officer || officer.Count != fields.Length || fields.Any(field => !officer.ContainsKey(field)))
                throw new InvalidDataException("Historical officer archive contains an incomplete record.");
        HistoricalOfficerProfile result;
        try { result = document.Deserialize<Envelope>(Json)?.Profile ?? throw new InvalidDataException("Historical officer archive is empty."); }
        catch (JsonException error) { throw new InvalidDataException("Historical officer archive has invalid field types.", error); }
        result.Validate();
        return result;
    }
}
