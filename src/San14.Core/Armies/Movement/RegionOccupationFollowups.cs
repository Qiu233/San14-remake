namespace San14.Core;

/// <summary>A captured center to be evaluated by the separate encirclement
/// operation. Entries retain production order and duplicates.</summary>
public sealed record RegionEncirclementSeed(int ForceId, int AreaId);

/// <summary>An actual commander announcement request. Admission, coalescing
/// and display belong to the presentation queue, not the territory writer.</summary>
public sealed record RegionOccupationAnnouncement(int UnitId, int OfficerId, int AreaId, int HexId);

public sealed record EncirclementHexAppearance(int HexId, int? PreviousForceId);

/// <summary>An unplayed encirclement presentation, captured before territory
/// changes. Numerical completion does not acknowledge this request.</summary>
public sealed record RegionEncirclementPresentation
{
    public int ForceId { get; }
    public int? CityId { get; }
    public IReadOnlyList<int> AreaIds { get; }
    public IReadOnlyList<EncirclementHexAppearance> Hexes { get; }
    public RegionEncirclementPresentation(int forceId, int? cityId,
        IReadOnlyList<int> areaIds, IReadOnlyList<EncirclementHexAppearance> hexes)
    {
        ForceId = forceId; CityId = cityId;
        AreaIds = Array.AsReadOnly(areaIds.ToArray());
        Hexes = Array.AsReadOnly(hexes.ToArray());
    }
}

/// <summary>Saved outstanding work produced by center occupation. A missing
/// owner in an older save does not imply an empty queue.</summary>
public sealed record RegionOccupationFollowups
{
    public IReadOnlyList<RegionEncirclementSeed> EncirclementSeeds { get; }
    public IReadOnlyList<RegionOccupationAnnouncement> Announcements { get; }
    public IReadOnlyList<RegionEncirclementPresentation> EncirclementPresentations { get; }
    public RegionOccupationFollowups(IReadOnlyList<RegionEncirclementSeed> encirclementSeeds,
        IReadOnlyList<RegionOccupationAnnouncement> announcements,
        IReadOnlyList<RegionEncirclementPresentation>? encirclementPresentations = null)
    {
        ArgumentNullException.ThrowIfNull(encirclementSeeds);
        ArgumentNullException.ThrowIfNull(announcements);
        EncirclementSeeds = Array.AsReadOnly(encirclementSeeds.ToArray());
        Announcements = Array.AsReadOnly(announcements.ToArray());
        //Older remake builds could not complete a positive component, so they
        //could not have produced any of these new presentation requests.
        EncirclementPresentations = Array.AsReadOnly((encirclementPresentations ?? []).ToArray());
    }
}

public sealed partial record GameState
{
    public RegionOccupationFollowups? RegionOccupationFollowups { get; init; }
}
