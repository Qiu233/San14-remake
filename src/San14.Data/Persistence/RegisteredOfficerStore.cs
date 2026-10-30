using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using San14.Core;

namespace San14.Data;

/// <summary>The one durable registered-officer library. Campaigns receive a
/// detached new-game projection and never read this file while running.</summary>
public sealed class RegisteredOfficerStore
{
    public const int CurrentVersion = 1;
    private const string Format = "san14-remake-registered-officers";
    private static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };
    private readonly string path;
    private RegisteredOfficerLibrary current;
    private RegisteredOfficerStore(string path, RegisteredOfficerLibrary current)
    { this.path = Path.GetFullPath(path); this.current = current; }
    public RegisteredOfficerLibrary Current => current;

    public static RegisteredOfficerStore CreateNew(string path)
    {
        var library = RegisteredOfficerLibrary.Empty;
        AtomicSaveFiles.Write(path, Encode(library), overwrite: false);
        return new(path, library);
    }
    public static RegisteredOfficerStore OpenExisting(string path) => new(path, Decode(File.ReadAllBytes(path)));
    public RegisteredOfficerLibrary Save(RegisteredOfficerLibrary next)
    {
        byte[] bytes = Encode(next);
        AtomicSaveFiles.Write(path, bytes);
        current = next;
        return current;
    }
    private sealed record Envelope(string Format, int Version, RegisteredOfficerLibrary Library);
    private static byte[] Encode(RegisteredOfficerLibrary library)
    {
        library.Validate();
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope(Format, CurrentVersion, library), Json);
    }
    private static RegisteredOfficerLibrary Decode(byte[] bytes)
    {
        JsonObject document = JsonNode.Parse(bytes) as JsonObject ?? throw new InvalidDataException("Registered officer archive is invalid.");
        if (document.Count != 3 || document["Format"]?.GetValue<string>() != Format ||
            document["Version"]?.GetValue<int>() != CurrentVersion || document["Library"] is not JsonObject library)
            throw new InvalidDataException("Registered officer archive format or version is unsupported.");
        //The original version1 writer included this computed getter. Accept
        //only that known redundant field; all actual records remain required.
        bool hasLegacyFirstFreeId = library.TryGetPropertyValue("FirstFreeId", out var legacyFirstFreeId);
        if (hasLegacyFirstFreeId)
        {
            if (legacyFirstFreeId is not null &&
                (legacyFirstFreeId is not JsonValue value || !value.TryGetValue<int>(out _)))
                throw new InvalidDataException("Registered officer archive has an invalid derived FirstFreeId.");
            library.Remove("FirstFreeId");
        }
        if (library.Count != 1 || library["Officers"] is not JsonArray officers || officers.Count > 150)
            throw new InvalidDataException("Registered officer archive format or version is unsupported.");
        string[] fields = ["Id", "FamilyName", "GivenName", "CourtesyName", "FamilyNameSortKey", "GivenNameSortKey", "CourtesyNameSortKey",
            "PortraitId", "Sex", "BirthYear", "DeathYear", "FamilyId", "FatherId", "MotherId", "SpouseId", "SwornBrotherId",
            "CompatibilityPersonId", "Compatibility", "LikePersonIds", "DislikePersonIds", "Leadership", "Strength", "Intelligence",
            "Politics", "Charm", "TraitIds", "FormationIds", "TacticIds", "DoctrineId", "PolicyId", "PolicyBaseCost", "VoiceType", "Description"];
        foreach (var node in officers)
            if (node is not JsonObject officer || officer.Count != fields.Length || fields.Any(field => !officer.ContainsKey(field)))
                throw new InvalidDataException("Registered officer archive contains an incomplete record.");
        RegisteredOfficerLibrary result;
        try { result = document.Deserialize<Envelope>(Json)?.Library ?? throw new InvalidDataException("Registered officer archive is empty."); }
        catch (JsonException error) { throw new InvalidDataException("Registered officer archive has invalid field types.", error); }
        result.Validate();
        if (hasLegacyFirstFreeId && legacyFirstFreeId?.GetValue<int>() != result.FirstFreeId)
            throw new InvalidDataException("Registered officer archive derived FirstFreeId does not match its records.");
        return result;
    }
}
