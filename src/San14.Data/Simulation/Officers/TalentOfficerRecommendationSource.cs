using San14.Core;

namespace San14.Data;

internal sealed record TalentOfficerRecommendation(int OfficerId, int TravelDays, bool Recommended);

/// <summary>The shared716BA0/744250 travel projection. Advice is supplied by
/// the actual caller: automatic search uses its fast trial, manual selection
/// uses the real message-producing advisor. Neither path issues commands.</summary>
internal sealed class TalentOfficerRecommendationSource(TalentUiQuerySource page, SourceWorldContext current)
{
    private readonly Dictionary<(int Home, int City), int> distances = [];
    private readonly Dictionary<(int Home, int City), int> ordinarySearchDays = [];
    private readonly Dictionary<(int Home, int Target), int> ordinaryRecruitDays = [];

    internal IReadOnlyList<TalentOfficerRecommendation> SearchCity(int city, IReadOnlyList<int> actors,
        Func<int, AreaReference, bool> advise)
    {
        var rows = new List<TalentOfficerRecommendation>();
        foreach (int actor in actors)
        {
            bool recommended = false;
            foreach (var area in page.SearchAreas(city))
                if (advise(actor, area)) { recommended = true; break; }
            rows.Add(new(actor, SearchDays(actor, city), recommended));
        }
        return rows.AsReadOnly();
    }

    internal IReadOnlyList<TalentOfficerRecommendation> RecruitTarget(int targetPhysical, IReadOnlyList<int> actors,
        Func<int, bool> advise)
    {
        var rows = new List<TalentOfficerRecommendation>();
        foreach (int actor in actors)
        {
            bool recommended = advise(actor);
            int physical = page.Physical(actor), home = page.Home(actor);
            int days;
            if (HasJourneyTrait(physical) || !ordinaryRecruitDays.TryGetValue((home, targetPhysical), out days))
            {
                var force = current.ReadPersonForce(physical) ?? throw Missing("recruit_Force");
                days = TalentTravelSource.Resolve(page.State, current, TalentOrderKind.Recruit, physical,
                    BaseLookup.Find(home, current), null, targetPhysical, force,
                    page.State.NativeMapSeason?.ThirdDate ?? throw Missing("date")).TravelDays;
                if (!HasJourneyTrait(physical)) ordinaryRecruitDays[(home, targetPhysical)] = days;
            }
            rows.Add(new(actor, days, recommended));
        }
        return rows.AsReadOnly();
    }

    internal int SearchDays(int actor, int city)
    {
        int physical = page.Physical(actor), home = page.Home(actor);
        if (!HasJourneyTrait(physical) && ordinarySearchDays.TryGetValue((home, city), out int cached)) return cached;
        var force = current.ReadPersonForce(physical) ?? throw Missing("search_Force");
        int days = PersonReturnPathSource.CalculateTalentTravelDays(page.State, current, Distance(home, city), physical, force);
        if (!HasJourneyTrait(physical)) ordinarySearchDays[(home, city)] = days;
        return days;
    }

    internal int RoundTripDays(int actor, int city, int outwardDays)
    {
        int physical = page.Physical(actor);
        if (!HasJourneyTrait(physical)) return unchecked(outwardDays * 2);
        var force = current.ReadPersonForce(physical) ?? throw Missing("return_Force");
        return unchecked(outwardDays + PersonReturnPathSource.CalculateTalentTravelDays(page.State, current,
            Distance(page.Home(actor), city), 0, force));
    }

    private bool HasJourneyTrait(int physical)
        => K(TacticRemakeCurrentTraits.CommanderEffectSum(current, physical, 0x12), "trait18") > 0;

    private int Distance(int home, int city)
    {
        if (distances.TryGetValue((home, city), out int cached)) return cached;
        var first = CurrentGateMapQueries.FindHex(BaseLookup.Find(home, current), current);
        var last = CurrentGateMapQueries.FindHex(BaseLookup.Find(city, current), current);
        int count = first.PhysicalSlot is int a && last.PhysicalSlot is int b ? PersonReturnPathSource.Search(current, a, b).Length : 0;
        int distance = unchecked(count - 1); distances.Add((home, city), distance); return distance;
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("talent.recommendation." + field);
}
