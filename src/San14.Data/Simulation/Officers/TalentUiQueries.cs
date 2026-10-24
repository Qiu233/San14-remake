using San14.Core;

namespace San14.Data;

public sealed record TalentSearchDestination(int CityId, string Name, IReadOnlyList<int> AreaHexes);

/// <summary>One ordinary talent page owns its captured current and its selected
/// executor/target pairs. Refreshing the view never reselects or resamples.</summary>
public sealed class TalentUiDraft
{
    private readonly TalentUiQuerySource source;
    private int[] selected = [];
    public bool Recruitment => source.Recruitment;
    public IReadOnlyList<TalentSearchDestination> Destinations => source.Destinations;
    public IReadOnlyList<int> TargetOfficerIds => source.TargetOfficerIds;
    public IReadOnlyList<int> ExecutorOfficerIds => source.ExecutorOfficerIds;
    public IReadOnlyList<int> SelectedExecutorOfficerIds => Array.AsReadOnly(selected);
    public IReadOnlyList<TalentAssignment> Assignments { get; private set; } = Array.Empty<TalentAssignment>();
    public int? CurrentDestinationCityId { get; private set; }
    public int? TargetOfficerId { get; private set; }
    public int AvailableBooks => source.AvailableBooks;
    public int RequiredBooks => checked(selected.Length * source.BookCost);
    public int MaximumExecutors => source.BookCost > 0 ? Math.Min(ExecutorOfficerIds.Count, AvailableBooks / source.BookCost) : ExecutorOfficerIds.Count;
    public int? TravelDays { get; private set; }
    public string? AdviceText { get; private set; }
    public int? AdvisorOfficerId { get; private set; }
    public int RecruitMode { get; private set; }
    public bool CanChooseRecruitMode { get; private set; }
    public string? RecruitModeUnresolvedInput { get; private set; }
    private bool recruitModeInitialized;
    public CampaignAppRandomState? AppRandomState => source.State.CampaignAppRandom32;
    public string? UnresolvedInput { get; private set; }
    public bool CanSubmit => UnresolvedInput is null && Assignments.Count != 0 && RequiredBooks <= AvailableBooks;

    internal TalentUiDraft(TalentUiQuerySource source) => this.source = source;

    public void SelectDestination(int cityId)
    {
        if (Recruitment || !Destinations.Any(city => city.CityId == cityId))
            throw new ArgumentOutOfRangeException(nameof(cityId));
        CurrentDestinationCityId = cityId;
        RecommendAndRebuild();
    }

    public void SelectRecruitTarget(int officerId)
    {
        if (!Recruitment || !TargetOfficerIds.Contains(officerId)) throw new ArgumentOutOfRangeException(nameof(officerId));
        TargetOfficerId = officerId;
        RecommendAndRebuild();
    }

    public void SelectExecutors(IReadOnlyList<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(officerIds);
        if (officerIds.Count > MaximumExecutors || officerIds.Distinct().Count() != officerIds.Count ||
            officerIds.Any(id => !ExecutorOfficerIds.Contains(id)))
            throw new ArgumentException("Talent executors must be the page's current distinct ordered candidates.", nameof(officerIds));
        selected = officerIds.ToArray();
        Rebuild();
    }

    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard(int officerId) => source.Card(officerId);

    private void RecommendAndRebuild()
    {
        //744250/74F8C0 replace the existing selection with one ranked
        //executor on an accepted target, including a later target change.
        selected = []; Assignments = Array.Empty<TalentAssignment>(); TravelDays = null; AdviceText = null; AdvisorOfficerId = null; UnresolvedInput = null;
        try
        {
            var order = source.Recommend(CurrentDestinationCityId, TargetOfficerId);
            if (order.Count != 0) selected = [order[0]];
            Rebuild();
        }
        catch (InvalidDataException missing) { UnresolvedInput = missing.Message; RefreshRecruitModeGate(); }
    }

    public void SetRecruitMode(int mode)
    {
        if (!Recruitment || mode is not (0 or 10 or 30) || !CanChooseRecruitMode)
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (RecruitMode == mode) return;
        RecruitMode = mode;
        // The radio changes only the packet's absence policy. It does not
        // rerun advice, choose executors, rebuild paths or draw App random.
        Assignments = Array.AsReadOnly(Assignments.Select(value => value with { RecruitMode = mode }).ToArray());
    }

    private void Rebuild()
    {
        RefreshRecruitModeGate();
        Assignments = Array.Empty<TalentAssignment>(); TravelDays = null; AdviceText = null; AdvisorOfficerId = null; UnresolvedInput = null;
        if (selected.Length == 0 || Recruitment && TargetOfficerId is null || !Recruitment && CurrentDestinationCityId is null) return;
        try
        {
            var assignments = new List<TalentAssignment>();
            foreach (int actor in selected)
            {
                var result = source.Pair(actor, CurrentDestinationCityId, TargetOfficerId);
                assignments.Add(result.Assignment with { RecruitMode = Recruitment ? RecruitMode : 0 });
                // Each executor has a real travel result. A combined multi-
                // executor display rule is not inferred from those values.
                if (selected.Length == 1)
                { TravelDays = result.TravelDays; AdviceText = result.AdviceText; AdvisorOfficerId = result.AdvisorOfficerId; }
            }
            Assignments = assignments.AsReadOnly();
        }
        catch (InvalidDataException missing) { UnresolvedInput = missing.Message; }
    }

    private void RefreshRecruitModeGate()
    {
        CanChooseRecruitMode = false; RecruitModeUnresolvedInput = null;
        if (!Recruitment || selected.Length == 0 || TargetOfficerId is not int target) return;
        try
        {
            CanChooseRecruitMode = source.RecruitModeAvailable(selected[0], target);
            // 7A42B0's +1EC flag resets the first newly enabled radio group
            // to Immediate Return; later selection edits retain its choice.
            if (CanChooseRecruitMode && !recruitModeInitialized)
            { recruitModeInitialized = true; RecruitMode = 0; }
        }
        catch (InvalidDataException missing) { RecruitModeUnresolvedInput = missing.Message; }
    }
}

internal sealed record TalentUiPair(TalentAssignment Assignment, int TravelDays, string? AdviceText, int? AdvisorOfficerId);

internal sealed class TalentUiQuerySource
{
    internal GameState State { get; private set; }
    internal bool Recruitment { get; }
    internal int AvailableBooks { get; }
    internal int BookCost { get; }
    internal IReadOnlyList<TalentSearchDestination> Destinations { get; }
    internal IReadOnlyList<int> TargetOfficerIds { get; }
    internal IReadOnlyList<int> ExecutorOfficerIds { get; }
    private readonly SourceWorldContext current;
    private readonly Func<GameState, int, int, TalentAdvisorSampleResult> advise;
    private readonly Func<GameState, TacticSourceSamplerWorld> sampler;
    private readonly Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>> card;
    private readonly Func<GameState, int, int, int> ability;
    internal TalentOfficerRecommendationSource Recommendations { get; }
    private readonly Dictionary<int, int> physicals = [], homes = [];
    private readonly Dictionary<int, List<AreaReference>> areas = [];

    internal TalentUiQuerySource(GameState state, SourceWorldContext current, int forceId, int districtId,
        bool recruitment, Func<GameState, int, int, TalentAdvisorSampleResult> advise,
        Func<GameState, TacticSourceSamplerWorld> sampler,
        Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>> card,
        Func<GameState, int, int, int> ability)
    {
        State = state; this.current = current; Recruitment = recruitment;
        this.advise = advise; this.sampler = sampler; this.card = card; this.ability = ability;
        Recommendations = new(this, current);
        if (state.Phase != TurnPhase.Strategy || !state.Districts.TryGetValue(districtId, out var district) ||
            district.ForceId != forceId) throw Missing("District_owner");
        if (state.PersonnelCommands?.Talents is null) throw Missing("saved_task_owner");
        AvailableBooks = checked((int)Math.Max(0L, Need(district.OrdersRemaining, "District_books") - CommandBookAvailability.Reserved(state, districtId)));
        BookCost = PersonReturnCommands.ReadTalentBookCost(state, recruitment ? TalentOrderKind.Recruit : TalentOrderKind.Search);
        var people = new List<int>(); var targets = new List<int>();
        foreach (int? raw in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person_registry"))
        {
            int physical = Need(raw, "Person_pointer");
            if (!Need(current.ReadPersonNativeValidity(physical), "Person_validity")) continue;
            int id = Need(current.ReadPersonIdentity(physical), "Person_identity");
            int own = Need((current.ReadPersonForce(physical) ?? throw Missing("Person_Force")).PhysicalSlot, "Person_Force_physical");
            physicals[id] = physical;
            if (recruitment && id != 0 && own != forceId && Need(current.ReadPersonStatus(physical), "target_status") is >= 1 and <= 6)
                targets.Add(id);
            int home = Need(current.ReadPersonFirstLocation(physical), "Person_Home");
            if (own != forceId || !state.Cities.TryGetValue(home, out var city) || city.DistrictId != districtId ||
                Need(OfficerHealthSourceAdapter.ReadByte(current, physical), "Person_health161") >= 2 ||
                (Need(current.ReadArmyReturnPersonFlags196(physical), "Person_flags15E") & 1) != 0 ||
                BattleRemakeCurrentRegionFactor.IsNativeBandPerson(id) && home is >= 1 and <= 61 && own is not (>= 46 and <= 50) ||
                PersonCurrentBusySource.Read(current, physical, Missing)) continue;
            if (!state.Officers.TryGetValue(id, out var actor) || actor.Deployed ||
                state.PreparedTalentOrders?.ContainsKey(id) == true || state.PreparedOfficerMoves?.ContainsKey(id) == true ||
                state.PreparedDeployments.Values.Any(plan => plan.OfficerId == id) || PlayableDiplomacy.IsOfficerOnMission(state, id)) continue;
            homes[id] = home; people.Add(id);
        }
        ExecutorOfficerIds = people.AsReadOnly(); TargetOfficerIds = targets.AsReadOnly();
        if (recruitment) { Destinations = Array.Empty<TalentSearchDestination>(); return; }
        var registered = current.ReadRegisteredAreaReferences() ?? throw Missing("Area_registry");
        foreach (var entry in registered)
        {
            var area = entry ?? throw Missing("Area_pointer");
            if (Need(current.ReadAreaCurrentForceSlot(area), "Area_Force") != forceId) continue;
            byte rawCity = Need(current.ReadAreaCitySlot(area), "Area35");
            var city = current.ReadCityTableReference(rawCity < 52 ? rawCity : 0) ?? throw Missing("Area_City_pointer");
            int cityId = Need(current.ReadBaseObjectLink(city), "Area_City_identity");
            if (!state.Cities.ContainsKey(cityId)) throw Missing("Area_City_owner");
            if (!areas.TryGetValue(cityId, out var list)) areas.Add(cityId, list = []);
            list.Add(area);
        }
        // 6DF400 sorts Area physicals before deduplicating their City owners.
        var order = areas.OrderBy(pair => pair.Value.Min(area => Need(area.PhysicalSlot, "Area_physical")));
        Destinations = Array.AsReadOnly(order.Select(pair => new TalentSearchDestination(pair.Key, state.Cities[pair.Key].Name,
            Array.AsReadOnly(pair.Value.Select(area => (int)Need(current.ReadAreaHexIndex(area), "Area3A")).ToArray()))).ToArray());
    }

    internal AreaAdministrationQuery<AreaAdministrationOfficerCard> Card(int id) => card(id);
    internal int Physical(int id) => physicals[id];
    internal int Home(int id) => homes[id];
    internal IReadOnlyList<AreaReference> SearchAreas(int city) => areas.TryGetValue(city, out var found) ? found : Array.Empty<AreaReference>();
    internal void InstallAppRandom(uint bits) => State = State with { CampaignAppRandom32 = new(bits) };

    internal IReadOnlyList<int> Recommend(int? destination, int? target)
    {
        IReadOnlyList<TalentOfficerRecommendation> rows;
        if (Recruitment)
        {
            int person = physicals[target ?? throw Missing("recommend_target")];
            rows = Recommendations.RecruitTarget(person, ExecutorOfficerIds, actor => Advice(actor, person));
            //6B0880: recommended actors prioritize days; others prioritize
            //Charm. Both partitions retain identity as the final tie break.
            return Array.AsReadOnly(rows.OrderByDescending(row => row.Recommended)
                .ThenBy(row => row.Recommended ? row.TravelDays : -Ability(row.OfficerId, 4))
                .ThenBy(row => row.Recommended ? -Ability(row.OfficerId, 4) : row.TravelDays)
                .ThenBy(row => row.OfficerId).Select(row => row.OfficerId).ToArray());
        }
        rows = Recommendations.SearchCity(destination ?? throw Missing("recommend_City"), ExecutorOfficerIds,
            (actor, area) => Advice(actor, Need(area.PhysicalSlot, "recommend_Area")));
        return Array.AsReadOnly(rows.OrderByDescending(row => row.Recommended).ThenBy(row => row.TravelDays)
            .ThenByDescending(row => Ability(row.OfficerId, 3)).ThenBy(row => row.OfficerId)
            .Select(row => row.OfficerId).ToArray());

        bool Advice(int actor, int actualTarget)
        {
            var result = advise(State, physicals[actor], actualTarget); State = result.State;
            return result.PositiveAdvice;
        }
        int Ability(int actor, int dimension) => ability(State, actor, dimension);
    }

    internal bool RecruitModeAvailable(int firstExecutorId, int targetId)
    {
        // 6DC1E0 returns the first selected Person (or Person0), not a
        // target Base or the target's current geographic position.
        int actor = physicals[firstExecutorId], target = physicals[targetId];
        return Need(current.ReadPersonNativeValidity(actor), "mode_first_executor_validity") &&
            !PersonCurrentBusySource.Read(current, actor, Missing) &&
            Need(current.ReadPersonNativeValidity(target), "mode_target_validity");
    }

    internal TalentUiPair Pair(int actorId, int? destinationCity, int? targetId)
    {
        int actor = physicals[actorId]; int home = homes[actorId];
        var force = current.ReadPersonForce(actor) ?? throw Missing("executor_Force");
        var sourceBase = BaseLookup.Find(home, current);
        var date = State.NativeMapSeason?.ThirdDate ?? throw Missing("current_date");
        if (Recruitment)
        {
            int target = physicals[targetId ?? throw Missing("target_Person")];
            var advice = advise(State, actor, target); State = advice.State;
            var travel = TalentTravelSource.Resolve(State, current, TalentOrderKind.Recruit, actor, sourceBase, null, target, force, date);
            return new(new(home, actorId, TargetOfficerId: targetId), travel.TravelDays, advice.AdviceText, advice.AdvisorOfficerId);
        }
        var candidates = areas[destinationCity ?? throw Missing("target_City")];
        AreaReference? chosen = null; string? text = null; int? advisorId = null;
        foreach (var area in candidates)
        {
            var advice = advise(State, actor, Need(area.PhysicalSlot, "target_Area_physical"));
            State = advice.State; text = advice.AdviceText; advisorId = advice.AdvisorOfficerId;
            if (advice.PositiveAdvice) { chosen = area; break; }
        }
        if (chosen is null)
        {
            int identity = Need(current.ReadPersonIdentity(actor), "executor_ID10");
            int index = TacticEffectSourceSamplerMath.Sample(candidates.Count, 1, identity, 0, 0, 0, sampler(State));
            chosen = candidates[index];
        }
        int hex = Need(current.ReadAreaHexIndex(chosen), "target_Area3A");
        var reached = TalentTravelSource.Resolve(State, current, TalentOrderKind.Search, actor, sourceBase, chosen, null, force, date);
        return new(new(home, actorId, TargetHexId: hex), reached.TravelDays, text, advisorId);
    }

    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("talent.ui." + field);
}
