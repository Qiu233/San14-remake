namespace San14.Data;

public sealed record OriginalMapSourceCreatedLightingRetainedRange(int Offset, ReadOnlyMemory<byte> Bytes);
public sealed record OriginalMapSourceCreatedLightingRetainedExtent(int Offset, int Length);

/// <summary>The exact Game source constructor/loader writes4078 of4288 bytes.
/// Callers may supply only the210 retained bytes; proved source/default fields
/// are written by OriginalMainLightConstruction. Missing native holes never
/// become zeros, and coverage-only packets are never exposed as light owners.</summary>
public static class OriginalMapSourceCreatedLightingRetainedSource
{
    public static IReadOnlyList<OriginalMapSourceCreatedLightingRetainedExtent> ReadGameDefaultLayout(
        LoadedOriginalMainLightBindingSource source, uint contextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(source);
        var nulls = new uint[] { 0x5A53AD58, 0x80F9DB7B, 0x7F97260C, 0x54F8C0DE, 0x7D85DA95 }
            .Select(key => new OriginalMainLightConstructionResourceBinding(key, 0,
                $"coverage-only-explicit-null:{key:X8}", false, 0)).ToArray();
        var coverage = OriginalMainLightConstruction.Apply(source,
            new byte[OriginalMainLightConstruction.LightDataBytes],
            new(contextFlags3E4, "coverage-only;not-a-light-generation", nulls));
        bool[] written = new bool[OriginalMainLightConstruction.LightDataBytes];
        foreach (var range in coverage.WrittenRanges)
        {
            if (range.Offset < 0 || range.Length < 1 || range.Offset > written.Length - range.Length)
                throw new InvalidDataException("Game source construction write coverage is outside its exact10C0 record.");
            Array.Fill(written, true, range.Offset, range.Length);
        }
        if (written.Count(value => value) != 4078)
            throw new InvalidDataException("Game source construction differs from the proved4078 written/210 retained byte coverage.");
        var extents = new List<OriginalMapSourceCreatedLightingRetainedExtent>();
        for (int offset = 0; offset < written.Length;)
        {
            if (written[offset]) { offset++; continue; }
            int start = offset;
            while (offset < written.Length && !written[offset]) offset++;
            extents.Add(new(start, offset - start));
        }
        return extents.AsReadOnly();
    }

    public static IReadOnlyList<OriginalMapSourceCreatedLightingRetainedRange> ExtractGameDefault(
        LoadedOriginalMainLightBindingSource source, ReadOnlySpan<byte> explicitComplete10C0,
        uint contextFlags3E4)
    {
        if (explicitComplete10C0.Length != OriginalMainLightConstruction.LightDataBytes)
            throw new InvalidDataException("Retained source extraction requires the complete explicit10C0 template.");
        var result = new List<OriginalMapSourceCreatedLightingRetainedRange>();
        foreach (var extent in ReadGameDefaultLayout(source, contextFlags3E4))
            result.Add(new(extent.Offset, explicitComplete10C0.Slice(extent.Offset, extent.Length).ToArray()));
        return result.AsReadOnly();
    }

    /// <summary>Known fields in this intermediate template will all be replaced
    /// by the source constructor/loader. Every native retained byte is supplied
    /// exactly once; the final source packet contains no missing-byte defaults.</summary>
    public static byte[] BuildGameDefaultTemplate(LoadedOriginalMainLightBindingSource source,
        IReadOnlyList<OriginalMapSourceCreatedLightingRetainedRange> explicitRetained,
        uint contextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(explicitRetained);
        var layout = ReadGameDefaultLayout(source, contextFlags3E4);
        if (explicitRetained.Count != layout.Count || explicitRetained.Any(r => r is null) ||
            explicitRetained.Select(r => r.Offset).Distinct().Count() != layout.Count)
            throw new InvalidDataException("Game source construction requires all210 retained bytes in their exact nonoverlapping native extents.");
        var input = explicitRetained.ToDictionary(r => r.Offset, r => r.Bytes.ToArray());
        byte[] result = new byte[OriginalMainLightConstruction.LightDataBytes];
        foreach (var extent in layout)
        {
            if (!input.TryGetValue(extent.Offset, out var bytes) || bytes.Length != extent.Length)
                throw new InvalidDataException($"Game source retained extent{extent.Offset:X4}/{extent.Length} is missing or incomplete.");
            bytes.CopyTo(result.AsSpan(extent.Offset, extent.Length));
        }
        return result;
    }
}
