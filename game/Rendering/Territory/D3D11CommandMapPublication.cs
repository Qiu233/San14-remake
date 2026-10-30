using San14.Data;

public enum D3D11CommandMapDisplayKind : uint
{
    None = 0,
    AreaAdministration = 1,
    FixedCandidateGray = 2,
    CampaignProvinceFocus = 3,
    CampaignDistrictFocus = 4,
    SummaryCityFocus = 5,
    SummaryAreaFocus = 6,
    DiplomacyForceFocus = 7,
    DiplomacyFootholdFocus = 8
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
    internal bool HasMembership => Kind is D3D11CommandMapDisplayKind.AreaAdministration or D3D11CommandMapDisplayKind.FixedCandidateGray or
        D3D11CommandMapDisplayKind.DiplomacyForceFocus or D3D11CommandMapDisplayKind.DiplomacyFootholdFocus;
    internal bool HasFocusGroups => Kind is D3D11CommandMapDisplayKind.AreaAdministration or
        D3D11CommandMapDisplayKind.CampaignProvinceFocus or D3D11CommandMapDisplayKind.CampaignDistrictFocus or
        D3D11CommandMapDisplayKind.SummaryCityFocus or D3D11CommandMapDisplayKind.SummaryAreaFocus or
        D3D11CommandMapDisplayKind.DiplomacyForceFocus or D3D11CommandMapDisplayKind.DiplomacyFootholdFocus;

    // 17F300/181050 use each Hex12's CURRENT Area35 City. Build on world
    // adoption, once; pointer/camera requests only index this retained array.
    internal static (D3D11CommandMapData City,D3D11CommandMapData Area) CaptureSummaryFocus(SourceWorldContext current)
    {
        var cities=new ushort?[501];
        var groups=new ushort[CellCount];
        var rawAreas=new ushort[CellCount];
        for(int hex=0;hex<groups.Length;hex++)
        {
            var reference=current.ReadHexTableReference(hex)??throw new InvalidDataException("summary-focus.Hex_pointer");
            ushort raw=current.ReadHexAreaSlot(reference)??throw new InvalidDataException("summary-focus.Hex12");
            rawAreas[hex]=raw;
            int area=raw<501?raw:0;
            if(cities[area] is not ushort city)
            {
                var pointer=current.ReadAreaTableReference(area)??throw new InvalidDataException("summary-focus.Area_pointer");
                city=current.ReadAreaCitySlot(pointer)??throw new InvalidDataException("summary-focus.Area35:"+area);
                cities[area]=city;
            }
            groups[hex]=city;
        }
        return (new([],groups,D3D11CommandMapDisplayKind.SummaryCityFocus),new([],rawAreas,D3D11CommandMapDisplayKind.SummaryAreaFocus));
    }

    // V2's byte/word channels have explicit kind-specific meanings. This is
    // an interop encoding, not candidate membership: Force uses raw Hex14 and
    // Force49; Foothold uses City/Gate4E and the priority hover target4E.
    internal static D3D11CommandMapData CaptureDiplomacyFocus(DiplomacyTargetMapQuery query)
    {
        if(query.Presentation==MapTargetPresentation.IntelligenceInspectCity)
        {
            var cities=new ushort[CellCount];
            for(int hex=0;hex<CellCount;hex++)cities[hex]=query.ReadTerritoryCell(hex).CityLink;
            // Original selector5 is exactly the existing181050 City footprint:
            // raw Area35, no Gate priority or membership/candidate masking.
            return new([],cities,D3D11CommandMapDisplayKind.SummaryCityFocus);
        }
        var bytes=new byte[CellCount];var words=new ushort[CellCount];
        bool force=query.Kind==DiplomacyTargetSelectionKind.Force;
        for(int hex=0;hex<CellCount;hex++)
        {
            var cell=query.ReadTerritoryCell(hex);
            if(force){words[hex]=cell.ForceSlot;bytes[hex]=cell.ForceColor;}
            else
            {
                if(cell.CityLink>255||cell.GateLink>255||cell.HoverLink>255)
                    throw new InvalidDataException("diplomacy.overlay.foothold_link_range");
                words[hex]=(ushort)(cell.CityLink|(cell.GateLink<<8));bytes[hex]=(byte)cell.HoverLink;
            }
        }
        return new(bytes,words,force?D3D11CommandMapDisplayKind.DiplomacyForceFocus:
            D3D11CommandMapDisplayKind.DiplomacyFootholdFocus);
    }
    internal ushort DiplomacyFocusAt(int hex)=>Kind is D3D11CommandMapDisplayKind.DiplomacyForceFocus or D3D11CommandMapDisplayKind.SummaryCityFocus?RawAreas[hex]:Membership[hex];

    // Strategic map types10/5/7 have distinct input and marker policies,
    // but their original territory selectors4/5/1 use the existing native
    // Area/City/Force channels. Membership never masks these focus groups.
    internal static D3D11CommandMapData CaptureStrategicFocus(StrategicTargetMapQuery query)
    {
        var words=new ushort[CellCount];
        bool force=query.Kind==San14.Core.StrategicCommandTargetKind.Force;
        byte[] colours=force?new byte[CellCount]:[];
        for(int hex=0;hex<CellCount;hex++)
        {
            var cell=query.ReadTerritoryCell(hex);words[hex]=cell.Group;
            if(force)colours[hex]=cell.ForceColour;
        }
        return new(colours,words,force?D3D11CommandMapDisplayKind.DiplomacyForceFocus:
            query.Kind==San14.Core.StrategicCommandTargetKind.City?D3D11CommandMapDisplayKind.SummaryCityFocus:
                D3D11CommandMapDisplayKind.SummaryAreaFocus);
    }
    internal ushort StrategicFocusAt(int hex)=>RawAreas[hex];

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
                    D3D11CommandMapDisplayKind.CampaignProvinceFocus or D3D11CommandMapDisplayKind.CampaignDistrictFocus or
                    D3D11CommandMapDisplayKind.SummaryCityFocus or D3D11CommandMapDisplayKind.SummaryAreaFocus or
                    D3D11CommandMapDisplayKind.DiplomacyForceFocus or D3D11CommandMapDisplayKind.DiplomacyFootholdFocus)
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
