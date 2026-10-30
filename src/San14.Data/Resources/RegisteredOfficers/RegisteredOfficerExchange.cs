using System.Collections.Immutable;
using System.Text;
using San14.Core;

namespace San14.Data;

public sealed record RegisteredOfficerImportCandidate(string FileName, RegisteredOfficer Officer);
public sealed record RegisteredOfficerImportFailure(string FileName, string Detail);
public sealed record RegisteredOfficerImportPublication(IReadOnlyList<RegisteredOfficerImportCandidate> Candidates,
    IReadOnlyList<RegisteredOfficerImportFailure> Failures, bool LimitReached);
public sealed class RegisteredOfficerExportException : IOException
{
    public IReadOnlyList<string> WrittenFiles { get; }
    public IReadOnlyList<int> WrittenOfficerIds { get; }
    internal RegisteredOfficerExportException(Exception cause, IReadOnlyList<string> paths, IReadOnlyList<int> ids)
        : base(cause.Message, cause) { WrittenFiles = paths; WrittenOfficerIds = ids; }
}

/// <summary>Portable individual files share one explicitly supplied directory.
/// Library adoption remains a separate candidate transaction for the caller's Store.</summary>
public static class RegisteredOfficerExchange
{
    public const int MaximumDirectoryRecords = 5000;
    private static readonly byte[] Signature = Encoding.ASCII.GetBytes("SAN14EDITPERSON\0");

    public static int CountSignatureFiles(string directory)
    {
        int count = 0;
        foreach (string path in Paths(directory)) if (HasSignature(path)) count++;
        return count;
    }

    public static RegisteredOfficerImportPublication LoadCandidates(string directory, string locale,
        CollectionOfficerCatalog source, OfficerEditorPortraitCatalog? portraits = null)
    {
        var candidates = new List<RegisteredOfficerImportCandidate>();
        var failures = new List<RegisteredOfficerImportFailure>(); bool limit = false;
        portraits ??= source.CreateEditorPortraitCatalog();
        foreach (string path in Paths(directory))
        {
            try
            {
                if (!HasSignature(path)) continue; // Source explicitly rejects a foreign signature.
                if (candidates.Count == MaximumDirectoryRecords) { limit = true; break; }
                var raw = RegisteredOfficerExchangeCodec.Read(File.ReadAllBytes(path), locale);
                candidates.Add(new(Path.GetFileName(path), Normalize(raw, source, portraits)));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            { failures.Add(new(Path.GetFileName(path), error.Message)); }
        }
        return new(candidates.AsReadOnly(), failures.AsReadOnly(), limit);
    }

    public static IReadOnlyList<string> Export(string directory, RegisteredOfficerLibrary library,
        IReadOnlyList<int> selectedIds, string locale, OfficerEditorPortraitCatalog? portraits = null)
    {
        if (selectedIds.Count == 0 || selectedIds.Distinct().Count() != selectedIds.Count)
            throw new InvalidDataException("No distinct registered officers were selected for export.");
        if (selectedIds.Count > Math.Max(0, MaximumDirectoryRecords - CountSignatureFiles(directory)))
            throw new InvalidDataException("The portable officer directory has reached its record limit.");
        var people = selectedIds.Select(id => library.Find(id) ?? throw new InvalidDataException("The export selection is stale.")).ToArray();
        if (portraits is not null)
            foreach (var person in people) portraits.ValidateChoice(person.PortraitId);
        // Encode and validate all requested names before publishing the first file.
        var prepared = people.Select(person => (Officer: person, Bytes: RegisteredOfficerExchangeCodec.Write(person, locale))).ToArray();
        foreach (var (person, _) in prepared)
            if (person.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("The registered officer's name cannot be used as a portable filename.");
        Directory.CreateDirectory(directory);
        var written = new List<string>(); var ids = new List<int>();
        try
        {
            foreach (var (person, bytes) in prepared)
            {
                int suffix = 0; string path;
                do { path = Path.Combine(directory, person.Name + (suffix == 0 ? "" : suffix.ToString(System.Globalization.CultureInfo.InvariantCulture)) + ".s14tc"); suffix++; }
                while (File.Exists(path) || Directory.Exists(path));
                AtomicSaveFiles.Write(path, bytes, overwrite: false);
                written.Add(path); ids.Add(person.Id);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new RegisteredOfficerExportException(error, written.AsReadOnly(), ids.AsReadOnly()); }
        return written.AsReadOnly();
    }

    public static RegisteredOfficerLibrary Import(RegisteredOfficerLibrary library,
        IReadOnlyList<RegisteredOfficerImportCandidate> candidates, IReadOnlyList<int> selectedIndices)
    {
        if (selectedIndices.Count == 0 || selectedIndices.Distinct().Count() != selectedIndices.Count ||
            selectedIndices.Any(index => (uint)index >= (uint)candidates.Count) || selectedIndices.Count > 150 - library.Officers.Length)
            throw new InvalidDataException("The portable officer selection exceeds the current free slots.");
        var next = library.Officers.ToList(); var used = next.Select(person => person.Id).ToHashSet();
        foreach (int index in selectedIndices)
        {
            int id = Enumerable.Range(3001, 150).First(candidate => !used.Contains(candidate)); used.Add(id);
            var person = candidates[index].Officer with { Id = id, Compatibility = 0 };
            person.Validate(requireName: false); next.Add(person);
        }
        var result = new RegisteredOfficerLibrary([.. next.OrderBy(person => person.Id)]);
        result.Validate(); return result;
    }

    private static RegisteredOfficer Normalize(RegisteredOfficer raw, CollectionOfficerCatalog source, OfficerEditorPortraitCatalog portraits)
    {
        byte sex = (byte)Math.Min((int)raw.Sex, 1);
        var portrait = source.ResolveRegisteredImportPortrait(raw.PortraitId, sex, portraits);
        if (!portrait.Available) throw new InvalidDataException(portrait.UnresolvedInput);
        int birth = Math.Clamp((int)raw.BirthYear, 135, 250);
        var result = raw with
        {
            Id = 0, FamilyName = Name(raw.FamilyName), GivenName = Name(raw.GivenName), CourtesyName = Name(raw.CourtesyName),
            FamilyNameSortKey = "", GivenNameSortKey = "", CourtesyNameSortKey = "",
            PortraitId = portrait.Value, Sex = sex, BirthYear = (ushort)birth,
            DeathYear = (ushort)Math.Clamp((int)raw.DeathYear, birth + 29, birth + 98),
            FamilyId = Historical(raw.FamilyId), FatherId = Historical(raw.FatherId), MotherId = Historical(raw.MotherId),
            SpouseId = 0, SwornBrotherId = 0, CompatibilityPersonId = Historical(raw.CompatibilityPersonId),
            // Preview's source-Person BE is not displayed by this basic selector.
            // Adoption independently resets this byte to zero, as the source does.
            Compatibility = 0, LikePersonIds = Compact(raw.LikePersonIds, 8, 1400),
            DislikePersonIds = Compact(raw.DislikePersonIds, 8, 1400), TraitIds = Compact(raw.TraitIds, 9, 355),
            TacticIds = Compact(raw.TacticIds, 10, 200),
            Leadership = Ability(raw.Leadership), Strength = Ability(raw.Strength), Intelligence = Ability(raw.Intelligence),
            Politics = Ability(raw.Politics), Charm = Ability(raw.Charm),
            DoctrineId = raw.DoctrineId is >= 1 and <= 6 ? raw.DoctrineId : (byte)0,
            PolicyId = raw.PolicyId is >= 1 and <= 120 ? raw.PolicyId : (byte)0,
            PolicyBaseCost = (byte)Math.Min((int)raw.PolicyBaseCost, 10),
            VoiceType = raw.VoiceType <= 16 ? raw.VoiceType : sex == 0 ? (byte)9 : (byte)13,
            Description = raw.Description.Length <= 500 ? raw.Description : ""
        };
        return result;
        static string Name(string value) => value.Length <= 2 ? value : "";
        static byte Ability(byte value) => (byte)Math.Clamp((int)value, 1, 100);
    }
    private static ushort Historical(int id) => id is >= 1 and <= 1400 ? (ushort)id : (ushort)0;
    private static ImmutableArray<int> Compact(ImmutableArray<int> values, int count, int maximum)
    {
        var retained = values.Where(value => value is >= 1 && value <= maximum).Take(count).ToArray();
        return [.. retained, .. Enumerable.Repeat(0, count - retained.Length)];
    }
    private static IEnumerable<string> Paths(string directory) => Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory, "*.s14tc", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(Path.GetExtension(path), ".s14tc", StringComparison.Ordinal))
        : [];
    private static bool HasSignature(string path)
    {
        using var stream = File.OpenRead(path); Span<byte> header = stackalloc byte[16];
        if (stream.Length < 16) return false;
        stream.ReadExactly(header); return header.SequenceEqual(Signature);
    }
}
