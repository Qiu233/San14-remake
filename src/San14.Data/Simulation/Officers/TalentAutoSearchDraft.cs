using San14.Core;

namespace San14.Data;

public sealed record TalentAutoSearchCandidate(int OfficerId, int CityId, int TravelDays, bool Recommended);

/// <summary>The automatic-search page owns a captured query world and the
/// App stream consumed by its actual edit events. Reading the page is inert.</summary>
public sealed class TalentAutoSearchDraft
{
    private readonly TalentAutoSearchQuerySource source;
    internal TalentAutoSearchQuerySource Source => source;
    private int[] selected = [];
    public int? ExecutorPolicy { get; private set; }
    public int? TravelRange { get; private set; }
    public int? AllocationPolicy { get; private set; }
    public IReadOnlyList<TalentAutoSearchCandidate> Candidates { get; private set; } = Array.Empty<TalentAutoSearchCandidate>();
    public IReadOnlyList<int> SelectedExecutorOfficerIds => Array.AsReadOnly(selected);
    public IReadOnlyList<int> PreviewSelectedExecutorOfficerIds { get; private set; } = Array.Empty<int>();
    public IReadOnlyList<TalentAssignment> Assignments { get; private set; } = Array.Empty<TalentAssignment>();
    public int AvailableBooks => source.Page.AvailableBooks;
    public int MaximumExecutors => source.Page.BookCost > 0 ? Math.Min(Candidates.Count, AvailableBooks / source.Page.BookCost) : Candidates.Count;
    public int PreviewExecutorCount => MaximumExecutors;
    public int PlannedBooks => checked(PreviewExecutorCount * source.Page.BookCost);
    public int RequiredBooks => checked(selected.Length * source.Page.BookCost);
    public CampaignAppRandomState? AppRandomState => source.Page.State.CampaignAppRandom32;
    public string? UnresolvedInput { get; private set; }
    public bool CanPrepareSelection => ExecutorPolicy.HasValue && TravelRange.HasValue && AllocationPolicy.HasValue && UnresolvedInput is null && Candidates.Count != 0;
    public bool CanSubmit => CanPrepareSelection && selected.Length != 0 && Assignments.Count == selected.Length && RequiredBooks <= AvailableBooks;

    internal TalentAutoSearchDraft(TalentAutoSearchQuerySource source, TalentAutoSearchPreferences? preferences)
    {
        this.source = source;
        if (preferences is null) { UnresolvedInput = "talent.auto.saved_preferences"; return; }
        if (preferences.ExecutorPolicy is < 0 or > 3 || preferences.TravelRange is < 0 or > 2 || preferences.AllocationPolicy is < 0 or > 1)
            throw new InvalidDataException("talent.auto.saved_preferences_values");
        ExecutorPolicy = preferences.ExecutorPolicy; TravelRange = preferences.TravelRange; AllocationPolicy = preferences.AllocationPolicy;
        Recompute(); //726960's final6C7500, not a view refresh.
    }

    public bool TryAdoptPresentationWorld(GameState previous, GameState replacement, CampaignAppRandomState appRandom)
        => source.Page.TryAdoptPresentationWorld(previous, replacement, appRandom);

    public void SetExecutorPolicy(int value)
    { if ((uint)value > 3) throw new ArgumentOutOfRangeException(nameof(value)); if (ExecutorPolicy == value) return; ExecutorPolicy = value; Recompute(); }
    public void SetTravelRange(int value)
    { if ((uint)value > 2) throw new ArgumentOutOfRangeException(nameof(value)); if (TravelRange == value) return; TravelRange = value; Recompute(); }
    public void SetAllocationPolicy(int value)
    { if ((uint)value > 1) throw new ArgumentOutOfRangeException(nameof(value)); if (AllocationPolicy == value) return; AllocationPolicy = value; Recompute(); }

    public void PrepareSelection() => Recompute(); //7340B0 recomputes before opening the chooser.

    public void SelectExecutors(IReadOnlyList<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(officerIds);
        if (!CanPrepareSelection || officerIds.Count > MaximumExecutors || officerIds.Distinct().Count() != officerIds.Count)
            throw new ArgumentException("Automatic-search selection is outside its current candidates.", nameof(officerIds));
        var rows = Candidates.ToDictionary(row => row.OfficerId);
        if (officerIds.Any(id => !rows.ContainsKey(id))) throw new ArgumentException("Unknown automatic-search executor.", nameof(officerIds));
        //7340B0's6A1960 sorts the accepted list before making6DEC40 packets.
        selected = officerIds.OrderByDescending(id => rows[id].Recommended).ThenBy(id => rows[id].TravelDays)
            .ThenByDescending(source.Politics).ThenBy(id => id).ToArray();
        Assignments = Array.Empty<TalentAssignment>();
        try
        {
            var assignments = new List<TalentAssignment>();
            foreach (int actor in selected) assignments.Add(source.Page.Pair(actor, rows[actor].CityId, null).Assignment);
            Assignments = assignments.AsReadOnly();
        }
        catch (InvalidDataException missing) { UnresolvedInput = missing.Message; }
    }

    public PrepareTalentBatchOrder BuildOrder()
    {
        if (!CanSubmit) throw new InvalidOperationException(UnresolvedInput ?? "Automatic-search selection is incomplete.");
        return new(source.ForceId, TalentOrderKind.Search, Assignments)
        { AutoSearchPreferences = new(ExecutorPolicy!.Value, TravelRange!.Value, AllocationPolicy!.Value) };
    }

    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard(int officerId) => source.Page.Card(officerId);

    private void Recompute()
    {
        selected = []; Assignments = Array.Empty<TalentAssignment>(); Candidates = Array.Empty<TalentAutoSearchCandidate>();
        PreviewSelectedExecutorOfficerIds = Array.Empty<int>(); UnresolvedInput = null;
        if (ExecutorPolicy is not int executors || TravelRange is not int range || AllocationPolicy is not int allocation)
        { UnresolvedInput = "talent.auto.saved_preferences"; return; }
        try
        {
            var rows = source.Prepare(executors, range, allocation);
            var initial = rows.Take(source.Page.BookCost > 0 ? AvailableBooks / source.Page.BookCost : rows.Count)
                .Select(row => row.OfficerId).ToHashSet();
            PreviewSelectedExecutorOfficerIds = Array.AsReadOnly(rows.Where(row => initial.Contains(row.OfficerId)).Select(row => row.OfficerId).ToArray());
            //6B1470 keeps the budget selection first, then optional advice,
            //effective politics and actual Person identity.
            Candidates = Array.AsReadOnly(rows.OrderByDescending(row => initial.Contains(row.OfficerId))
                .ThenByDescending(row => allocation == 0 && row.Recommended).ThenByDescending(row => source.Politics(row.OfficerId))
                .ThenBy(row => row.OfficerId).ToArray());
        }
        catch (InvalidDataException missing) { UnresolvedInput = missing.Message; }
    }
}

internal sealed class TalentAutoSearchQuerySource
{
    internal TalentUiQuerySource Page { get; }
    internal int ForceId { get; }
    private readonly int districtId;
    private readonly SourceWorldContext current;
    private readonly Func<GameState, int, int> politics;
    private readonly Func<GameState, int, AreaReference, bool> advice;
    private readonly int[] cities;
    private readonly Dictionary<int, int> politicsCache = [];

    internal TalentAutoSearchQuerySource(TalentUiQuerySource page, SourceWorldContext current, int forceId, int districtId,
        Func<GameState, int, int> politics, Func<GameState, int, AreaReference, bool> advice)
    {
        Page = page; this.current = current; ForceId = forceId; this.districtId = districtId;
        this.politics = politics; this.advice = advice;
        var order = new List<int>();
        foreach (var raw in current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("City_registry"))
        {
            var entry = raw ?? throw Missing("City_pointer");
            var city = new BaseReference(BaseKind.City, K(entry.PhysicalSlot, "City_physical"));
            if (!K(current.ReadBaseNativeValidity(city), "City_validity") || K(current.ReadBaseForceSlot(city), "City_Force") != forceId) continue;
            int id = K(current.ReadBaseObjectLink(city), "City_identity");
            order.Add(id);
        }
        cities = order.ToArray();
    }

    internal int Politics(int id)
    {
        if (!politicsCache.TryGetValue(id, out int value)) politicsCache.Add(id, value = politics(Page.State, id));
        return value;
    }

    internal List<TalentAutoSearchCandidate> Prepare(int policy, int range, int allocation)
    {
        var excluded = Excluded(policy);
        int[] actors = Page.ExecutorOfficerIds.Where(id => !excluded.Contains(id)).ToArray();
        var primary = new List<TalentAutoSearchCandidate>[cities.Length];
        var secondary = new List<TalentAutoSearchCandidate>[cities.Length];
        for (int i = 0; i < cities.Length; i++)
        {
            int city = cities[i]; primary[i] = []; secondary[i] = [];
            foreach (var recommendation in Page.Recommendations.SearchCity(city, actors,
                (actor, area) => advice(Page.State, Page.Physical(actor), area)))
            {
                int actor = recommendation.OfficerId, days = recommendation.TravelDays;
                bool recommended = recommendation.Recommended;
                int roundTrip = Page.Recommendations.RoundTripDays(actor, city, days);
                if (range == 0 && roundTrip > 11 || range == 1 && roundTrip > 21) continue;
                var row = new TalentAutoSearchCandidate(actor, city, days, recommended);
                (allocation == 0 && !recommended ? secondary[i] : primary[i]).Add(row);
            }
            primary[i] = Ordered(primary[i]); secondary[i] = Ordered(secondary[i]);
        }
        var result = new List<TalentAutoSearchCandidate>(); var used = new HashSet<int>();
        SelectCities(primary); SelectCities(secondary);
        return result;

        List<TalentAutoSearchCandidate> Ordered(List<TalentAutoSearchCandidate> rows)
            => rows.OrderByDescending(row => Politics(row.OfficerId)).ThenBy(row => row.OfficerId).Reverse().ToList();
        void SelectCities(List<TalentAutoSearchCandidate>[] groups)
        {
            int threshold = 100;
            while (true)
            {
                var eligible = new List<int>(); bool any = false;
                for (int i = 0; i < groups.Length; i++)
                    if (groups[i].Count != 0)
                    { any = true; if ((groups[i][^1].TravelDays - 1) / 5 < threshold) eligible.Add(i); }
                if (!any) return;
                if (eligible.Count == 0) { threshold++; continue; }
                //3FCB20 is a real raw draw, including when only one City remains.
                uint next = SourceRandom32Mix.Advance(Page.State.CampaignAppRandom32?.State ?? throw Missing("App32"));
                Page.InstallAppRandom(next);
                var group = groups[eligible[(int)((next & 0x7fffffff) % (uint)eligible.Count)]];
                while (group.Count != 0)
                {
                    var row = group[^1];
                    if (threshold < (row.TravelDays - 1) / 5 + 1) break;
                    group.RemoveAt(group.Count - 1);
                    if (used.Add(row.OfficerId)) { result.Add(row); break; }
                }
            }
        }
    }

    private HashSet<int> Excluded(int policy)
    {
        var excluded = new HashSet<int>();
        if (policy == 0) return excluded;
        foreach (int id in Page.ExecutorOfficerIds)
            if (K(OfficerHealthSourceAdapter.ReadByte(current, Page.Physical(id)), "actor_health") != 0) excluded.Add(id);
        if (policy == 1)
            for (int i = 0; i < 3; i++) Add(K(current.ReadForceAssociationPersonId(new(ForceId), i), "Force196"));
        foreach (var pair in Page.State.Cities)
        {
            if (pair.Value.ForceId != ForceId || pair.Value.DistrictId != districtId) continue;
            if (policy is 1 or 3)
            { Add(K(current.ReadAssignmentCityCA(pair.Key), "CityCA")); Add(K(current.ReadAssignmentCityCC(pair.Key), "CityCC")); }
            if (policy is 1 or 2)
                foreach (var area in Page.SearchAreas(pair.Key)) Add(K(current.ReadAssignmentArea36(K(area.PhysicalSlot, "Area_physical")), "Area36"));
        }
        return excluded;
        void Add(int raw)
        {
            var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0)) ?? throw Missing("appointed_Person");
            int p = K(person.PhysicalSlot, "appointed_Person_pointer");
            if (K(current.ReadPersonNativeValidity(p), "appointed_Person_validity")) excluded.Add(K(current.ReadPersonIdentity(p), "appointed_Person_identity"));
        }
    }

    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("talent.auto." + field);
}
