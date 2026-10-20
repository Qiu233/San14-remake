using San14.Data;

/// <summary>The parent draft's fixed display inputs, captured once. Camera
/// requests never query a draft, serialize a world or rebuild these arrays.</summary>
public sealed class D3D11CommandMapData
{
    internal byte[] Membership { get; }
    internal ushort[] RawAreas { get; }
    public ReadOnlyMemory<byte> PredicateResults => Membership;
    public ReadOnlyMemory<ushort> AreaSlots => RawAreas;
    public const int CellCount = 48400;

    private D3D11CommandMapData(byte[] membership, ushort[] areas)
        => (Membership, RawAreas) = (membership, areas);

    public static D3D11CommandMapData Capture(AreaAdministrationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var membership = new byte[CellCount]; var areas = new ushort[CellCount];
        for (int hex = 0; hex < CellCount; hex++)
        {
            var tint = draft.QueryMapTint(hex);
            if (!tint.Available || tint.Value is not bool known)
                throw new InvalidDataException(tint.UnresolvedInput ?? "area-draft.map_tint");
            var raw = draft.QueryMapRawArea(hex);
            if (!raw.Available || raw.Value is not ushort area)
                throw new InvalidDataException(raw.UnresolvedInput ?? "area-draft.map_raw_area");
            membership[hex] = known ? (byte)1 : (byte)0; areas[hex] = area;
        }
        return new(membership, areas);
    }
}

/// <summary>A request keeps this immutable snapshot. Null publication means
/// no update; a publication whose Data is null explicitly removes the owner.</summary>
public sealed record D3D11CommandMapPublication
{
    public D3D11CommandMapData? Data { get; }
    public ulong Revision { get; }
    public ulong DataRevision { get; }
    public ulong ProviderGeneration { get; }
    public uint HoverHex { get; }
    public bool Present => Data is not null;
    internal D3D11CommandMapPublication(D3D11CommandMapData? data, ulong revision,
        ulong dataRevision, ulong providerGeneration, uint hoverHex = D3D11CommandMapData.CellCount)
    {
        if (revision == 0 || data is not null && dataRevision == 0 || hoverHex > D3D11CommandMapData.CellCount)
            throw new ArgumentOutOfRangeException(nameof(revision));
        if (providerGeneration == 0 || (providerGeneration % 2 != 0) != (data is not null))
            throw new ArgumentOutOfRangeException(nameof(providerGeneration));
        Data = data; Revision = revision; DataRevision = dataRevision;
        ProviderGeneration = providerGeneration; HoverHex = hoverHex;
    }
}
