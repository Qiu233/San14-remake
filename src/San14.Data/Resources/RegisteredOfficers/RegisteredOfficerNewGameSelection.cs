using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>The user's new-game choices, independent of the editor library.
/// An adopted officer without an explicit placement retains the original
/// parent-location/initialization path; it is not assigned a guessed city.</summary>
public sealed record RegisteredOfficerNewGameSelection(ImmutableArray<int> AdoptedOfficerIds,
    ImmutableArray<RegisteredOfficerNewGamePlacement> Placements,
    ImmutableArray<RegisteredOfficerNewGameForce> NewForces)
{
    public static RegisteredOfficerNewGameSelection None => new([], [], []);

    public void Validate(RegisteredOfficerLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (AdoptedOfficerIds.IsDefault || Placements.IsDefault || NewForces.IsDefault ||
            AdoptedOfficerIds.Distinct().Count() != AdoptedOfficerIds.Length ||
            AdoptedOfficerIds.Any(id => library.Find(id) is null) ||
            Placements.Any(entry => entry is null) || NewForces.Any(force => force is null) ||
            Placements.Select(entry => entry.OfficerId).Distinct().Count() != Placements.Length ||
            Placements.Any(entry => entry is null || !AdoptedOfficerIds.Contains(entry.OfficerId) || entry.CityId is < 1 or > 51) ||
            NewForces.Length > 4 || NewForces.Select(force => force.CityId).Distinct().Count() != NewForces.Length)
            throw new InvalidDataException("Registered-officer new-game selection is invalid.");
        var members = new HashSet<int>();
        var cities = new HashSet<int>();
        foreach (var force in NewForces)
        {
            if (force is null || force.CityId is < 1 or > 51 || force.OfficerIds.IsDefaultOrEmpty ||
                force.AdditionalCityIds.IsDefault || force.AdditionalCityIds.Any(id => id is < 1 or > 51) ||
                force.AdditionalCityIds.Append(force.CityId).Any(id => !cities.Add(id)) ||
                force.ColourId is < 50 or > 59 || force.RankId is < 1 or > 9 || force.KingdomId is < 0 or > 100 ||
                force.RankId != 1 && force.KingdomId != 0 ||
                !force.OfficerIds.Contains(force.RulerOfficerId) ||
                force.OfficerIds.Distinct().Count() != force.OfficerIds.Length ||
                force.OfficerIds.Any(id => !AdoptedOfficerIds.Contains(id) || !members.Add(id)) ||
                force.OfficerIds.Any(id => !Placements.Any(entry => entry.OfficerId == id && entry.Serving &&
                    (entry.CityId == force.CityId || force.AdditionalCityIds.Contains(entry.CityId)))))
                throw new InvalidDataException("Registered-officer new-force selection is invalid.");
        }
    }
}

/// <summary>A real selected City, not an implicit choice for all adopted people.</summary>
public sealed record RegisteredOfficerNewGamePlacement(int OfficerId, int CityId, bool Serving = false);

/// <summary>A new force's selected ruler, city and ordered members. The
/// scenario initializer owns allocation of the actual vacant Force/District.</summary>
public sealed record RegisteredOfficerNewGameForce(int CityId, int RulerOfficerId, ImmutableArray<int> OfficerIds)
{
    public ImmutableArray<int> AdditionalCityIds { get; init; } = [];
    public int ColourId { get; init; } = 50;
    public int RankId { get; init; } = 9;
    public int KingdomId { get; init; }
}

public sealed record RegisteredOfficerNewGameChoice(int Id, string Text);
