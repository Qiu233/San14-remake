using San14.Core;

namespace San14.Data;

public sealed record TalentSearchDestination(int CityId, string Name, IReadOnlyList<int> AreaHexes);

/// <summary>One ordinary talent page owns its captured current and its selected
/// executor/target pairs. Refreshing the view never reselects or resamples.</summary>
public sealed class TalentUiDraft
{
    private readonly TalentUiQuerySource source;
    internal TalentUiQuerySource Source => source;
    private int[] selected = [];
    public bool Recruitment => source.Recruitment;
    public bool CanPrepareRecruitExecutorSelection => Recruitment && TargetOfficerId is int id && source.IsRecruitTargetValid(id);
    public bool CanPrepareExecutorSelection => !Recruitment && CurrentDestinationCityId is int city && source.IsSearchDestinationValid(city);
    public IReadOnlyList<TalentSearchDestination> Destinations => source.Destinations;
    public IReadOnlyList<int> TargetOfficerIds => source.TargetOfficerIds;
    public IReadOnlyList<int> ExecutorOfficerIds => source.ExecutorOfficerIds;
    public IReadOnlyList<int> SelectedExecutorOfficerIds => Array.AsReadOnly(selected);
    public IReadOnlyList<TalentAssignment> Assignments { get; private set; } = Array.Empty<TalentAssignment>();
    public int? CurrentDestinationCityId { get; private set; }
    public int? TargetOfficerId { get; private set; }
    public int AvailableBooks => source.AvailableBooks;
    public int RequiredBooks => checked(selected.Length * source.BookCost);
    public int MaximumExecutors => Recruitment ? source.RecruitSelectionMaximum : source.BookCost > 0 ? Math.Min(ExecutorOfficerIds.Count, AvailableBooks / source.BookCost) : ExecutorOfficerIds.Count;
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

    public bool TryAdoptPresentationWorld(GameState previous, GameState replacement, CampaignAppRandomState appRandom)
        => source.TryAdoptPresentationWorld(previous, replacement, appRandom);

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
        Assignments = Array.Empty<TalentAssignment>(); TravelDays = null; AdviceText = null; AdvisorOfficerId = null;
        try
        {
            //74F8C0 refreshes the new target while the old first executor
            //still owns190.77A130 performs that actual advice event.
            if (selected.Length != 0) source.RefreshRecruitParentAdvice(selected[0], officerId);
            RecommendAndRebuild();
        }
        catch (InvalidDataException missing) { ClearRecruitPreparedPublication(missing); }
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

    /// <summary>The ordinary Search open event prepares candidate advice and
    /// travel once. It preserves the current checked executors and pairs;
    /// subsequent page reads and sorts consume these retained results.</summary>
    public bool PrepareExecutorSelection()
    {
        if (!CanPrepareExecutorSelection) throw new InvalidDataException("talent.search.selection.destination_required");
        //74D500 does not open a selector when6DC650 returns no candidates.
        if (ExecutorOfficerIds.Count == 0) return false;
        source.PrepareSearchSelection(CurrentDestinationCityId!.Value);
        return true;
    }

    /// <summary>746A20 prepares full advice and actual-position travel once.
    /// The reader and its sorting/checking consumers do not repeat this event.</summary>
    public bool PrepareRecruitExecutorSelection()
    {
        if (!CanPrepareRecruitExecutorSelection) throw new InvalidDataException("talent.recruit.selection.target_required");
        if (ExecutorOfficerIds.Count == 0) return false;
        try { source.PrepareRecruitSelection(TargetOfficerId!.Value); }
        catch (InvalidDataException missing) { ClearRecruitPreparedPublication(missing); throw; }
        return true;
    }

    public void ApplyRecruitExecutorSelection(IReadOnlyList<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(officerIds);
        if (!CanPrepareRecruitExecutorSelection || officerIds.Count > MaximumExecutors ||
            officerIds.Distinct().Count() != officerIds.Count || officerIds.Any(id => !ExecutorOfficerIds.Contains(id)))
            throw new ArgumentException("Recruit executors must be this prepared chooser's candidates.", nameof(officerIds));
        Assignments = Array.Empty<TalentAssignment>();
        try
        {
            selected = source.OrderRecruitExecutors(officerIds).ToArray();
            RebuildRecruitPreparedSelection();
        }
        catch (InvalidDataException missing) { ClearRecruitPreparedPublication(missing); }
    }

    private void RebuildRecruitPreparedSelection()
    {
        Assignments = Array.Empty<TalentAssignment>(); TravelDays = null; AdviceText = null; AdvisorOfficerId = null; UnresolvedInput = null;
        try
        {
            RefreshRecruitModeGate();
            if (selected.Length != 0)
            {
                int first = selected[0];
                TravelDays = source.RecruitSelectionRecommendations?.FirstOrDefault(row => row.OfficerId == first)?.TravelDays
                    ?? throw TalentUiQuerySource.Missing("recruit_selection_first_preview");
                //793710->7A15F0->77A130 formats the real first executor
                //once, not every member and not a cached-text substitute.
                var advice = source.RefreshRecruitParentAdvice(first, TargetOfficerId!.Value);
                AdviceText = advice?.AdviceText; AdvisorOfficerId = advice?.AdvisorOfficerId;
            }
            Assignments = Array.AsReadOnly(selected.Select(id => new TalentAssignment(source.Home(id), id,
                TargetOfficerId: TargetOfficerId) { RecruitMode = RecruitMode }).ToArray());
        }
        catch (InvalidDataException missing) { ClearRecruitPreparedPublication(missing); }
    }

    private void ClearRecruitPreparedPublication(InvalidDataException missing)
    {
        // Keep the consumed presentation App stream and the user's current
        //draft fields, but never expose an old or partially prepared order.
        Assignments = Array.Empty<TalentAssignment>(); TravelDays = null; AdviceText = null; AdvisorOfficerId = null;
        UnresolvedInput = missing.Message;
        RefreshRecruitModeGate();
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
            if (Recruitment) RebuildRecruitPreparedSelection(); else Rebuild();
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
    private GameState presentationWorld;
    internal bool Recruitment { get; }
    internal int ForceId { get; }
    internal SourceWorldContext Current => current;
    internal IReadOnlyList<TalentOfficerRecommendation>? SelectionRecommendations { get; private set; }
    internal IReadOnlyList<int>? SelectionRecommendationOrder { get; private set; }
    internal int AvailableBooks { get; }
    internal int BookCost { get; }
    internal int RecruitSelectionMaximum { get; }
    internal IReadOnlyList<TalentOfficerRecommendation>? RecruitSelectionRecommendations { get; private set; }
    internal IReadOnlyList<TalentSearchDestination> Destinations { get; }
    internal IReadOnlyList<int> TargetOfficerIds { get; }
    internal IReadOnlyList<int> ExecutorOfficerIds { get; }
    private readonly SourceWorldContext current;
    private readonly Func<GameState, int, int, TalentAdvisorSampleResult> advise;
    private readonly Func<GameState, TacticSourceSamplerWorld> sampler;
    private readonly Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>> card;
    private readonly Func<GameState, int, int, int> ability;
    private readonly Func<GameState, int, AreaReference, bool>? searchSelectionAdvice;
    internal TalentOfficerRecommendationSource Recommendations { get; }
    private readonly Dictionary<int, int> physicals = [], homes = [];
    private readonly Dictionary<int, List<AreaReference>> areas = [];

    internal TalentUiQuerySource(GameState state, SourceWorldContext current, int forceId, int districtId,
        bool recruitment, Func<GameState, int, int, TalentAdvisorSampleResult> advise,
        Func<GameState, TacticSourceSamplerWorld> sampler,
        Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>> card,
        Func<GameState, int, int, int> ability, GameState? presentationWorld = null,
        Func<GameState, int, AreaReference, bool>? searchSelectionAdvice = null,
        IReadOnlyList<int>? recruitTargets = null,
        IReadOnlyList<TalentRecruitExecutorCandidate>? recruitExecutors = null)
    {
        State = state; this.presentationWorld = presentationWorld ?? state; this.current = current; Recruitment = recruitment; ForceId = forceId;
        this.advise = advise; this.sampler = sampler; this.card = card; this.ability = ability;
        this.searchSelectionAdvice = searchSelectionAdvice;
        Recommendations = new(this, current);
        if (state.Phase != TurnPhase.Strategy || !state.Districts.TryGetValue(districtId, out var district) ||
            district.ForceId != forceId) throw Missing("District_owner");
        if (state.PersonnelCommands?.Talents is null) throw Missing("saved_task_owner");
        AvailableBooks = checked((int)Math.Max(0L, Need(district.OrdersRemaining, "District_books") - CommandBookAvailability.Reserved(state, districtId)));
        BookCost = PersonReturnCommands.ReadTalentBookCost(state, recruitment ? TalentOrderKind.Recruit : TalentOrderKind.Search);
        if (recruitment)
        {
            // The current semantic14 owner is OrdersRemaining. The saved
            //DistrictWindows10 projection is independently maintained and
            //is not an always-current mirror of this actual counter.
            int books = Need(district.OrdersRemaining, "recruit_selection_District_books14");
            if ((uint)books > byte.MaxValue) throw Missing("recruit_selection_District_books14_range");
            RecruitSelectionMaximum = books;
        }
        var people = new List<int>(); var targets = new List<int>();
        if (recruitment)
        {
            var candidates = recruitExecutors ?? throw Missing("recruit_executor_source");
            var targetIds = recruitTargets ?? throw Missing("recruit_target_source");
            //Keep the target producer's actual registry receivers, including
            //its source-known invalid-target branch. Do not canonicalize IDs.
            foreach (int? raw in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person_registry"))
            {
                int person = Need(raw, "Person_pointer");
                int id = Need(current.ReadPersonIdentity(person), "Person_identity");
                if (targetIds.Contains(id)) physicals[id] = person;
            }
            foreach (var candidate in candidates)
            {
                physicals[candidate.OfficerId] = candidate.PhysicalSlot;
                homes[candidate.OfficerId] = candidate.HomeLocationId;
                people.Add(candidate.OfficerId);
            }
        }
        else
        {
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
        }
        ExecutorOfficerIds = people.AsReadOnly(); TargetOfficerIds = recruitTargets ?? targets.AsReadOnly();
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

    internal bool TryAdoptPresentationWorld(GameState previous, GameState replacement, CampaignAppRandomState appRandom)
    {
        if (ReferenceEquals(previous, replacement) || !ReferenceEquals(presentationWorld, previous)) return false;
        // Preserve advice, assignment and selection caches. Resume future edit
        // draws from the live host cursor, including any speech-start draws.
        State = replacement with { CampaignAppRandom32 = appRandom };
        presentationWorld = replacement;
        return true;
    }

    internal AreaAdministrationQuery<AreaAdministrationOfficerCard> Card(int id) => card(id);
    internal int Physical(int id) => physicals[id];
    internal int Home(int id) => homes[id];
    internal IReadOnlyList<AreaReference> SearchAreas(int city) => areas.TryGetValue(city, out var found) ? found : Array.Empty<AreaReference>();
    internal void InstallAppRandom(uint bits) => State = State with { CampaignAppRandom32 = new(bits) };

    internal bool IsRecruitTargetValid(int id) => physicals.TryGetValue(id, out int physical) &&
        Need(current.ReadPersonNativeValidity(physical), "recruit_selection_target_validity");

    internal TalentAdvisorSampleResult? RefreshRecruitParentAdvice(int actor, int target)
    {
        if (!Recruitment || !IsRecruitTargetValid(actor) || !IsRecruitTargetValid(target)) return null;
        var result = advise(State, physicals[actor], physicals[target]);
        State = result.State;
        return result;
    }

    internal void PrepareRecruitSelection(int target)
    {
        if (!Recruitment || !IsRecruitTargetValid(target)) throw Missing("recruit_selection_target");
        RecruitSelectionRecommendations = null;
        var rows = Recommendations.RecruitTarget(physicals[target], ExecutorOfficerIds, actor =>
        {
            var adviceRow = advise(State, physicals[actor], physicals[target]);
            State = adviceRow.State;
            return adviceRow.PositiveAdvice;
        });
        RecruitSelectionRecommendations = rows;
    }

    internal IReadOnlyList<int> OrderRecruitExecutors(IReadOnlyList<int> ids)
    {
        var rows = (RecruitSelectionRecommendations ?? throw Missing("recruit_selection_prepared_rows"))
            .ToDictionary(row => row.OfficerId);
        return Array.AsReadOnly(ids.OrderByDescending(id => rows[id].Recommended)
            .ThenBy(id => rows[id].Recommended ? rows[id].TravelDays : -ability(State, id, 4))
            .ThenBy(id => rows[id].Recommended ? -ability(State, id, 4) : rows[id].TravelDays)
            .ThenBy(id => id).ToArray());
    }

    internal bool IsSearchDestinationValid(int city) =>
        Need(current.ReadBaseNativeValidity(BaseLookup.Find(city, current)), "selection_destination_validity");

    internal void PrepareSearchSelection(int city)
    {
        SelectionRecommendations = null; SelectionRecommendationOrder = null;
        if (Recruitment || !IsSearchDestinationValid(city)) throw Missing("selection_destination");
        var fastAdvice = searchSelectionAdvice ?? throw Missing("selection_fast_advice");
        var rows = Recommendations.SearchCity(city, ExecutorOfficerIds,
            (actor, area) => fastAdvice(State, physicals[actor], area));
        SelectionRecommendations = rows;
        SelectionRecommendationOrder = Array.AsReadOnly(rows.OrderByDescending(row => row.Recommended)
            .ThenBy(row => row.TravelDays).ThenByDescending(row => ability(State, row.OfficerId, 3))
            .ThenBy(row => row.OfficerId).Select(row => row.OfficerId).ToArray());
    }

    internal IReadOnlyList<int> Recommend(int? destination, int? target)
    {
        // Retain this actual edit event's results for the inert Person reader.
        // Opening, painting or sorting that reader must not run advice again.
        SelectionRecommendations = null; SelectionRecommendationOrder = null;
        IReadOnlyList<TalentOfficerRecommendation> rows;
        if (Recruitment)
        {
            Recommendations.ClearRecruitTargetCache();
            PrepareRecruitSelection(target ?? throw Missing("recommend_target"));
            return OrderRecruitExecutors(ExecutorOfficerIds);
        }
        rows = Recommendations.SearchCity(destination ?? throw Missing("recommend_City"), ExecutorOfficerIds,
            (actor, area) => Advice(actor, Need(area.PhysicalSlot, "recommend_Area")));
        SelectionRecommendations = rows;
        SelectionRecommendationOrder = Array.AsReadOnly(rows.OrderByDescending(row => row.Recommended).ThenBy(row => row.TravelDays)
            .ThenByDescending(row => Ability(row.OfficerId, 3)).ThenBy(row => row.OfficerId)
            .Select(row => row.OfficerId).ToArray());
        return SelectionRecommendationOrder;

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
