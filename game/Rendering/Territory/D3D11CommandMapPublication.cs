using San14.Data;

public enum D3D11CommandMapDisplayKind : uint
{
    None = 0,
    AreaAdministration = 1,
    FixedCandidateGray = 2,
    CampaignProvinceFocus = 3,
    CampaignDistrictFocus = 4
}

/// <summary>The parent draft's fixed display inputs, captured once. Camera
/// requests never query a draft, serialize a world or rebuild these arrays.</summary>
public sealed class D3D11CommandMapData
{
    public D3D11CommandMapDisplayKind Kind { get; }
    internal byte[] Membership { get; }
    internal ushort[] RawAreas { get; }
    public ReadOnlyMemory<byte> PredicateResults => Membership;
    public ReadOnlyMemory<ushort> AreaSlots => RawAreas;
    public const int CellCount = 48400;
    internal bool HasMembership => Kind is D3D11CommandMapDisplayKind.AreaAdministration or D3D11CommandMapDisplayKind.FixedCandidateGray;
    internal bool HasFocusGroups => Kind is D3D11CommandMapDisplayKind.AreaAdministration or
        D3D11CommandMapDisplayKind.CampaignProvinceFocus or D3D11CommandMapDisplayKind.CampaignDistrictFocus;

    private D3D11CommandMapData(byte[] membership, ushort[] areas, D3D11CommandMapDisplayKind kind)
        => (Membership, RawAreas, Kind) = (membership, areas, kind);

    // The caller supplies actual physical candidate Hex identities once when
    // entering the destination state. There is no Area-focus owner here.
    public static D3D11CommandMapData CaptureCandidateHexes(IEnumerable<int> physicalHexIds)
    {
        ArgumentNullException.ThrowIfNull(physicalHexIds);
        var membership = new byte[CellCount];
        foreach (int hex in physicalHexIds)
        {
            if ((uint)hex >= CellCount) throw new ArgumentOutOfRangeException(nameof(physicalHexIds));
            membership[hex] = 1;
        }
        return new(membership, [], D3D11CommandMapDisplayKind.FixedCandidateGray);
    }

    internal static D3D11CommandMapData CaptureCampaignFocus(D3D11CommandMapDisplayKind kind,Func<int,int?> groupAtHex)
    {
        if(kind is not (D3D11CommandMapDisplayKind.CampaignProvinceFocus or D3D11CommandMapDisplayKind.CampaignDistrictFocus))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var groups=new ushort[CellCount];
        for(int hex=0;hex<CellCount;hex++)
        {
            int group=groupAtHex(hex)??0;
            if((uint)group>ushort.MaxValue)throw new InvalidDataException("Campaign focus identity is outside its field range.");
            groups[hex]=(ushort)group;
        }
        return new([],groups,kind);
    }

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
        return new(membership, areas, D3D11CommandMapDisplayKind.AreaAdministration);
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
    public D3D11CommandMapDisplayKind DisplayKind => Data?.Kind ?? D3D11CommandMapDisplayKind.None;
    internal bool UsesCandidateProtocol { get; }

    // A candidate close must retain its protocol identity even without Data.
    public static D3D11CommandMapPublication ForFixedCandidates(D3D11CommandMapData? data,
        ulong revision, ulong dataRevision, ulong providerGeneration)
        => new(data, revision, dataRevision, providerGeneration, D3D11CommandMapData.CellCount, true);
    internal static D3D11CommandMapPublication ForCampaignFocus(D3D11CommandMapData? data,
        ulong revision,ulong dataRevision,ulong providerGeneration,uint hoverHex=D3D11CommandMapData.CellCount)
        => new(data,revision,dataRevision,providerGeneration,hoverHex,true);
    internal D3D11CommandMapPublication(D3D11CommandMapData? data, ulong revision,
        ulong dataRevision, ulong providerGeneration, uint hoverHex = D3D11CommandMapData.CellCount)
        : this(data, revision, dataRevision, providerGeneration, hoverHex, false) { }

    private D3D11CommandMapPublication(D3D11CommandMapData? data, ulong revision,
        ulong dataRevision, ulong providerGeneration, uint hoverHex, bool candidateProtocol)
    {
        if (data is not null && (candidateProtocol
                ? data.Kind is not (D3D11CommandMapDisplayKind.FixedCandidateGray or
                    D3D11CommandMapDisplayKind.CampaignProvinceFocus or D3D11CommandMapDisplayKind.CampaignDistrictFocus)
                : data.Kind != D3D11CommandMapDisplayKind.AreaAdministration))
            throw new ArgumentException("Command map data belongs to a different display owner.", nameof(data));
        if (revision == 0 || data is not null && dataRevision == 0 || hoverHex > D3D11CommandMapData.CellCount)
            throw new ArgumentOutOfRangeException(nameof(revision));
        if (providerGeneration == 0 || (providerGeneration % 2 != 0) != (data is not null))
            throw new ArgumentOutOfRangeException(nameof(providerGeneration));
        UsesCandidateProtocol = candidateProtocol;
        Data = data; Revision = revision; DataRevision = dataRevision;
        ProviderGeneration = providerGeneration; HoverHex = hoverHex;
    }
}
