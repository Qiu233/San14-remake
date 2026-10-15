namespace San14.Data;

public sealed record MapSourceCreatedLightingRetainedRange(int Offset, ReadOnlyMemory<byte> Bytes);
public sealed record MapSourceCreatedLightingRetainedExtent(int Offset, int Length);

/// <summary>The retained byte ranges of the supported main-light construction layout.
/// Callers supply these ranges explicitly; MainLightConstruction writes the remaining
/// constructor fields. The layout is an interop contract, not a runtime coverage proof.</summary>
public static class MapSourceCreatedLightingRetainedSource
{
    internal static readonly IReadOnlyList<MapSourceCreatedLightingRetainedExtent> Layout =
        Array.AsReadOnly<MapSourceCreatedLightingRetainedExtent>(
        [
            new(0x164, 76), new(0x2BC, 4), new(0xAD5, 3), new(0xB6F, 1), new(0xB95, 3),
            new(0xDC9, 3), new(0xDD5, 3), new(0xDE1, 3), new(0xE01, 3),
            new(0xF21, 71), new(0x1090, 32), new(0x10B8, 8)
        ]);

    public static IReadOnlyList<MapSourceCreatedLightingRetainedExtent> ReadGameDefaultLayout(
        LoadedMainLightBindingSource source, uint contextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Layout;
    }

    public static IReadOnlyList<MapSourceCreatedLightingRetainedRange> ExtractGameDefault(
        LoadedMainLightBindingSource source, ReadOnlySpan<byte> explicitComplete10C0,
        uint contextFlags3E4)
    {
        if (explicitComplete10C0.Length != MainLightConstruction.LightDataBytes)
            throw new InvalidDataException("Retained source extraction requires the complete explicit10C0 template.");
        var result = new List<MapSourceCreatedLightingRetainedRange>();
        foreach (var extent in ReadGameDefaultLayout(source, contextFlags3E4))
            result.Add(new(extent.Offset, explicitComplete10C0.Slice(extent.Offset, extent.Length).ToArray()));
        return result.AsReadOnly();
    }

    /// <summary>Known fields in this intermediate template will all be replaced
    /// by the main-light constructor. Every retained range must be supplied
    /// exactly once; no retained byte value is inferred from the declaration.</summary>
    public static byte[] BuildGameDefaultTemplate(LoadedMainLightBindingSource source,
        IReadOnlyList<MapSourceCreatedLightingRetainedRange> explicitRetained,
        uint contextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(explicitRetained);
        var layout = ReadGameDefaultLayout(source, contextFlags3E4);
        if (explicitRetained.Count != layout.Count || explicitRetained.Any(r => r is null) ||
            explicitRetained.Select(r => r.Offset).Distinct().Count() != layout.Count)
            throw new InvalidDataException("Main-light construction requires every retained range exactly once.");
        var input = explicitRetained.ToDictionary(r => r.Offset, r => r.Bytes.ToArray());
        byte[] result = new byte[MainLightConstruction.LightDataBytes];
        foreach (var extent in layout)
        {
            if (!input.TryGetValue(extent.Offset, out var bytes) || bytes.Length != extent.Length)
                throw new InvalidDataException($"Main-light retained extent {extent.Offset:X4}/{extent.Length} is missing or incomplete.");
            bytes.CopyTo(result.AsSpan(extent.Offset, extent.Length));
        }
        return result;
    }
}
