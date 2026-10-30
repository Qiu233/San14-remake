using San14.Core;

namespace San14.Data;

public sealed record ItemExchangeImageSource(int Id, int Kind, int ImageIndex);
public sealed record ItemExchangeFileCandidate(int Index, string FileName, string FullPath, ItemExchangeRecord Item);
public sealed record ItemExchangeFileFailure(string FileName, string Reason);
public sealed record ItemExchangeLoadResult(IReadOnlyList<ItemExchangeFileCandidate> Candidates,
    IReadOnlyList<ItemExchangeFileFailure> Failures, int SignatureCount, bool LimitReached);
public sealed class ItemExchangeExportException : IOException
{
    public IReadOnlyList<string> WrittenPaths {get;}
    public IReadOnlyList<int> WrittenItemIds {get;}
    internal ItemExchangeExportException(Exception cause,IReadOnlyList<string> paths,IReadOnlyList<int> ids)
        :base(cause.Message,cause) {WrittenPaths=paths.ToArray();WrittenItemIds=ids.ToArray();}
}

/// <summary>Only individual ITEMDATA files. The caller explicitly owns the
/// directory and saves an imported candidate through the one title library.</summary>
public static class ItemExchangeFiles
{
    public const int MaximumCandidates = 500;
    public static ItemExchangeLoadResult LoadCandidates(string directory)
    {
        var candidates = new List<ItemExchangeFileCandidate>(); var failures = new List<ItemExchangeFileFailure>();
        int signatures = 0; bool limited = false;
        if (!Directory.Exists(directory)) return new(candidates, failures, signatures, limited);
        foreach (string path in Directory.EnumerateFiles(directory, "*.s14tc", SearchOption.TopDirectoryOnly))
        {
            // The original compares the actual extension after enumeration.
            if (Path.GetExtension(path) != ".s14tc") continue;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (!ItemExchangeCodec.HasSignature(bytes)) continue;
                signatures++;
                if (candidates.Count >= MaximumCandidates) { limited = true; break; }
                var item = ItemExchangeCodec.Read(bytes);
                candidates.Add(new(candidates.Count, Path.GetFileName(path), Path.GetFullPath(path), item));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.DecoderFallbackException)
            { failures.Add(new(Path.GetFileName(path), error.Message)); }
        }
        return new(candidates, failures, signatures, limited);
    }

    public static IReadOnlyList<string> Export(string directory, ItemEditorLibrary library, IReadOnlyList<int> ids,
        IReadOnlyList<ItemExchangeImageSource> sourceImages)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count) throw new InvalidDataException("No unique created items were selected for export.");
        var items = ids.Select(id => library.FindCreated(id) ?? throw new InvalidDataException("Selected created item has retired.")).ToArray();
        var captured = sourceImages.OrderBy(image => image.Id).ToArray();
        if (captured.Select(image => image.Id).Distinct().Count() != captured.Length ||
            captured.Any(image => image.Id is < 0 or > 201 || image.ImageIndex is < 0 or > 456))
            throw new InvalidDataException("Item export artwork sources are malformed.");
        int existing = CountSignatures(directory);
        if (items.Length > MaximumCandidates - existing) throw new InvalidDataException("The item exchange directory has reached its original capacity.");
        var encoded = new List<(CreatedItem Item,byte[] Bytes)>();
        foreach (var item in items)
        {
            if (item.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("The created item name cannot be used as its exchange filename.");
            int image = item.Values.ImageIndex;
            if (image >= 201)
            {
                // 20B930 uses the first current physical item of the same kind;
                // no matching kind retains the initial zero fallback.
                image = captured.FirstOrDefault(source => source.Kind == item.Values.Kind)?.ImageIndex ?? 0;
                if (image >= 201) image -= 201;
            }
            encoded.Add((item,ItemExchangeCodec.Write(item, image)));
        }
        Directory.CreateDirectory(directory);
        var paths = new List<string>(); var writtenIds=new List<int>();
        try
        {
        foreach(var (item,bytes) in encoded)
        {
            int suffix = 0;
            while (true)
            {
                string path = Path.Combine(directory, item.Name + (suffix == 0 ? "" : suffix.ToString(System.Globalization.CultureInfo.InvariantCulture)) + ".s14tc");
                if (File.Exists(path)) { suffix = checked(suffix + 1); continue; }
                try { AtomicSaveFiles.Write(path, bytes, overwrite: false); paths.Add(Path.GetFullPath(path));writtenIds.Add(item.Id); break; }
                catch (IOException) when (File.Exists(path)) { suffix = checked(suffix + 1); }
            }
        }
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException)
        {throw new ItemExchangeExportException(error,paths,writtenIds);}
        return paths;
    }

    public static ItemEditorLibrary Import(ItemEditorLibrary library, IReadOnlyList<ItemExchangeFileCandidate> candidates,
        IReadOnlyList<int> selectedIndices)
    {
        if (selectedIndices.Count == 0 || selectedIndices.Distinct().Count() != selectedIndices.Count)
            throw new InvalidDataException("No unique item file occurrences were selected for import.");
        if (selectedIndices.Count > 50 - library.Created.Length) throw new InvalidDataException("The created item library has insufficient empty slots.");
        var byIndex = candidates.ToDictionary(candidate => candidate.Index);
        var next = library;
        foreach (int index in selectedIndices)
        {
            if (!byIndex.TryGetValue(index, out var candidate)) throw new InvalidDataException("The selected item file occurrence has retired.");
            int id = next.FirstFreeCreatedId ?? throw new InvalidDataException("The created item library is full.");
            var item = candidate.Item;
            next = next.SetCreated(new(id, item.Name, item.Description, item.Values));
        }
        return next; // No archive or campaign mutation before the caller saves.
    }

    private static int CountSignatures(string directory)
    {
        if (!Directory.Exists(directory)) return 0;
        int count = 0;
        Span<byte> header = stackalloc byte[16];
        foreach (string path in Directory.EnumerateFiles(directory, "*.s14tc", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetExtension(path) != ".s14tc") continue;
            using var stream = File.OpenRead(path);
            if (stream.ReadAtLeast(header, 16, throwOnEndOfStream: false) == 16 && ItemExchangeCodec.HasSignature(header)) count++;
            if (count >= MaximumCandidates) break;
        }
        return count;
    }
}
