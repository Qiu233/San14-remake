using San14.Core;

namespace San14.Data;

public sealed record PersonalityImportCandidate(string FileName, CreatedPersonality Definition);
public sealed record PersonalityImportFailure(string FileName, string Detail);
public sealed record PersonalityImportPublication(IReadOnlyList<PersonalityImportCandidate> Candidates,
    IReadOnlyList<PersonalityImportFailure> Failures, bool LimitReached);
public sealed class PersonalityExportException : IOException
{
    public IReadOnlyList<string> WrittenFiles { get; }
    internal PersonalityExportException(Exception cause, IReadOnlyList<string> written)
        : base(cause.Message, cause) => WrittenFiles = written;
}

public static class PersonalityEditorExchange
{
    public const int MaximumDirectoryRecords = 500;
    public static int CountSignatureFiles(string directory) => Paths(directory).Count(HasSignature);
    public static PersonalityImportPublication LoadCandidates(string directory)
    {
        var rows = new List<PersonalityImportCandidate>();
        var failures = new List<PersonalityImportFailure>(); bool limit = false;
        foreach (string path in Paths(directory))
        {
            try
            {
                if (!HasSignature(path)) continue;
                if (rows.Count == MaximumDirectoryRecords) { limit = true; break; }
                rows.Add(new(Path.GetFileName(path), PersonalityEditorExchangeCodec.Read(File.ReadAllBytes(path))));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            { failures.Add(new(Path.GetFileName(path), error.Message)); }
        }
        return new(rows.AsReadOnly(), failures.AsReadOnly(), limit);
    }
    public static IReadOnlyList<string> Export(string directory, PersonalityEditorLibrary library,
        IReadOnlyList<int> selectedIds)
    {
        // The menu checks the directory cap once on entry. Its ordinary
        // exporter then writes every selected definition without a per-batch
        // remaining-capacity restriction.
        if (selectedIds.Count == 0 || selectedIds.Distinct().Count() != selectedIds.Count ||
            CountSignatureFiles(directory) >= MaximumDirectoryRecords)
            throw new InvalidDataException("The personality export selection exceeds the directory capacity.");
        var prepared = selectedIds.Select(id => library.Find(id) ??
                throw new InvalidDataException("The personality export selection is stale."))
            .Select(value => (value.Name, Bytes: PersonalityEditorExchangeCodec.Write(value))).ToArray();
        foreach (var value in prepared)
            if (value.Name.Length == 0 || value.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("A personality name cannot be used as a portable filename.");
        Directory.CreateDirectory(directory);
        var written = new List<string>();
        try
        {
            foreach (var value in prepared)
            {
                int suffix = 0; string path;
                do { path = Path.Combine(directory, value.Name + (suffix == 0 ? "" : suffix.ToString(System.Globalization.CultureInfo.InvariantCulture)) + ".s14tc"); suffix++; }
                while (File.Exists(path) || Directory.Exists(path));
                AtomicSaveFiles.Write(path, value.Bytes, overwrite: false); written.Add(path);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new PersonalityExportException(error, written.AsReadOnly()); }
        return written.AsReadOnly();
    }
    public static PersonalityEditorLibrary Import(PersonalityEditorLibrary library,
        IReadOnlyList<PersonalityImportCandidate> candidates, IReadOnlyList<int> selectedIndices)
    {
        if (selectedIndices.Count == 0 || selectedIndices.Distinct().Count() != selectedIndices.Count ||
            selectedIndices.Any(index => (uint)index >= (uint)candidates.Count) || selectedIndices.Count > 100 - library.Created.Length)
            throw new InvalidDataException("The personality import selection exceeds the free slots.");
        var next = library;
        foreach (int index in selectedIndices)
            next = next.Set(candidates[index].Definition with { Id = next.FirstFreeId ??
                throw new InvalidDataException("The personality library is full.") });
        return next;
    }
    private static IEnumerable<string> Paths(string directory) => Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory).Where(path => Path.GetExtension(path).Equals(".s14tc", StringComparison.OrdinalIgnoreCase))
        : [];
    private static bool HasSignature(string path)
    {
        using var stream = File.OpenRead(path); Span<byte> header = stackalloc byte[28];
        return stream.Read(header) == header.Length && PersonalityEditorExchangeCodec.HasSignature(header);
    }
}
