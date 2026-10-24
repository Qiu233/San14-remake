using San14.Core;

namespace San14.Data;

/// <summary>A single diplomacy preparation page. Its captured current is shared
/// by its selectors and quotes; rendering the page does not draw or submit.</summary>
public sealed partial class DiplomacyUiDraft
{
    private readonly DiplomacyUiSource source;
    public DiplomaticMissionKind Kind => source.Kind;
    public int ForceId => source.ForceId;
    public int DistrictId => source.DistrictId;
    public IReadOnlyList<int> ExecutorOfficerIds => source.Executors;
    public IReadOnlyList<int> TargetForceIds => source.Targets;
    public IReadOnlyList<int> TargetCityIds { get; private set; } = Array.Empty<int>();
    public int? SourceCityId { get; private set; }
    public int? SelectedExecutorOfficerId { get; private set; }
    public int? SelectedTargetForceId { get; private set; }
    public int? SelectedTargetCityId { get; private set; }
    public int AllianceMonths { get; private set; } = 6;
    public int GiftGold { get; private set; }
    public int GiftSupplies { get; private set; }
    public int MaxGold => source.MaxGold;
    public int MaxSupplies => source.MaxSupplies;
    public int MaxAllianceMonths { get; private set; } = 48;
    public int? ExistingAllianceMonths { get; private set; }
    public int RequiredBooks => source.RequiredBooks;
    public int AvailableBooks => source.AvailableBooks;
    public int? RelationLevel { get; private set; }
    public int? TravelDays { get; private set; }
    public string? TravelUnresolvedInput { get; private set; }
    public string? AdviceText { get; private set; }
    public string? AdviceUnresolvedInput { get; private set; }
    public int? AdvisorOfficerId { get; private set; }
    public CampaignAppRandomState? AppRandomState => source.State.CampaignAppRandom32;
    public bool CanSubmit { get; private set; }
    public string? UnresolvedInput { get; private set; }

    internal DiplomacyUiDraft(DiplomacyUiSource source)
    {
        this.source = source;
        ResetGiftCore();
        RefreshGiftAdmission();
    }

    public void SelectExecutor(int officerId)
    {
        if (!ExecutorOfficerIds.Contains(officerId)) throw new ArgumentOutOfRangeException(nameof(officerId));
        if (SelectedExecutorOfficerId == officerId) { RefreshAdvice(); return; }
        SelectedExecutorOfficerId = officerId; SourceCityId = source.Home(officerId);
        RefreshGiftAdmission(); RefreshTravel(); RefreshSubmission(); RefreshAdvice();
    }

    public void SelectForce(int forceId)
    {
        if (!TargetForceIds.Contains(forceId)) throw new ArgumentOutOfRangeException(nameof(forceId));
        if (SelectedTargetForceId == forceId) { RefreshAdvice(); return; }
        bool resetArea = GiftAreaId is > 0;
        SelectedTargetForceId = forceId;
        var relation = source.Relation(forceId);
        RelationLevel = source.DisplayRelationLevel(forceId); ExistingAllianceMonths = relation.AllianceMonths;
        MaxAllianceMonths = Math.Max(0, 48 - source.ReverseTreatyMonths(forceId));
        if (AllianceMonths > MaxAllianceMonths) AllianceMonths = 6;
        if (Kind == DiplomaticMissionKind.RequestAttack)
        {
            TargetCityIds = source.Cities(forceId);
            SelectedTargetCityId = null; SelectedExecutorOfficerId = null; SourceCityId = null;
        }
        if (resetArea) ResetGiftCore();
        RefreshGiftAdmission(); RefreshTravel(); RefreshSubmission(); RefreshAdvice();
    }

    public void SelectTargetCity(int cityId)
    {
        if (Kind != DiplomaticMissionKind.RequestAttack || !TargetCityIds.Contains(cityId))
            throw new ArgumentOutOfRangeException(nameof(cityId));
        if (SelectedTargetCityId == cityId) { RefreshAdvice(); return; }
        SelectedTargetCityId = cityId; RefreshGiftAdmission(); RefreshSubmission(); RefreshAdvice();
    }

    public void SetAllianceMonths(int months)
    {
        if (!CanChooseMonths(months))
            throw new ArgumentOutOfRangeException(nameof(months));
        if (AllianceMonths == months) { RefreshAdvice(); return; }
        AllianceMonths = months; InvalidateAmountSelections(); RefreshSubmission(); RefreshAdvice();
    }

    public bool CanChooseMonths(int months) => Kind == DiplomaticMissionKind.Alliance &&
        months is 6 or 12 or 24 && months <= MaxAllianceMonths;

    public void SetGiftGold(int gold)
    {
        if (Kind == DiplomaticMissionKind.CancelAlliance || gold < 0 || gold > MaxGold)
            throw new ArgumentOutOfRangeException(nameof(gold));
        if (CanChooseGold != true) throw new InvalidOperationException(GiftUnresolvedInput ?? "Gold selection is unavailable.");
        if (gold == 0 && GiftGold != 0) { RefreshAdvice(); return; }
        GiftGold = gold; GiftSupplies = 0; GiftItemId = GiftAreaId = null;
        RefreshSubmission(); RefreshAdvice();
    }

    public void SetGiftSupplies(int supplies)
    {
        if (Kind == DiplomaticMissionKind.CancelAlliance || supplies < 0 || supplies > MaxSupplies)
            throw new ArgumentOutOfRangeException(nameof(supplies));
        if (CanChooseSupplies != true) throw new InvalidOperationException(GiftUnresolvedInput ?? "Supplies selection is unavailable.");
        if (supplies == 0 && GiftSupplies != 0) { RefreshAdvice(); return; }
        GiftSupplies = supplies; GiftGold = 0; GiftItemId = GiftAreaId = null;
        RefreshSubmission(); RefreshAdvice();
    }

    public PrepareDiplomaticOrder BuildOrder()
    {
        if (!CanSubmit) throw new InvalidOperationException(UnresolvedInput ?? "The diplomacy page is incomplete.");
        return CreateOrder();
    }

    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard(int officerId) => source.Card(officerId);

    //735360 also refreshes after a child selector is cancelled. The host
    //calls this on that return; Apply and BuildOrder only read the result.
    public void RefreshAdvice()
    {
        AdviceText = null; AdviceUnresolvedInput = null; AdvisorOfficerId = null;
        if (SelectedExecutorOfficerId is not int actor || SelectedTargetForceId is not int target ||
            Kind == DiplomaticMissionKind.RequestAttack && SelectedTargetCityId is null ||
            Kind != DiplomaticMissionKind.CancelAlliance && GiftGold == 0 && GiftSupplies == 0 && GiftItemId is not > 0 && GiftAreaId is not > 0)
            return;
        try
        {
            var result = source.Advice(actor, target, AllianceMonths, SelectedTargetCityId, new(GiftGold, GiftSupplies, GiftItemId, GiftAreaId));
            AdviceText = result.Text; AdvisorOfficerId = result.AdvisorOfficerId;
        }
        catch (InvalidDataException missing) { AdviceUnresolvedInput = missing.Message; }
    }

    private PrepareDiplomaticOrder CreateOrder() => new(ForceId, SourceCityId!.Value,
        SelectedExecutorOfficerId!.Value, SelectedTargetForceId!.Value, Kind, AllianceMonths, SelectedTargetCityId)
        { GiftGold = GiftGold, GiftSupplies = GiftSupplies, GiftItemId = GiftItemId, GiftAreaId = GiftAreaId };

    private void RefreshSubmission()
    {
        CanSubmit = false; UnresolvedInput = null;
        if (SourceCityId is null || SelectedExecutorOfficerId is null || SelectedTargetForceId is null ||
            Kind == DiplomaticMissionKind.RequestAttack && SelectedTargetCityId is null) return;
        try { source.RequireSubmission(CreateOrder()); CanSubmit = true; }
        catch (ArgumentException rejected) { UnresolvedInput = rejected.Message; }
        catch (InvalidDataException missing) { UnresolvedInput = missing.Message; }
    }

    private void RefreshTravel()
    {
        TravelDays = null; TravelUnresolvedInput = null;
        if (SelectedExecutorOfficerId is not int actor || SelectedTargetForceId is not int target) return;
        try { TravelDays = source.Travel(actor, target); }
        catch (InvalidDataException missing) { TravelUnresolvedInput = missing.Message; }
    }
}

internal sealed partial class DiplomacyUiSource
{
    internal GameState State { get; private set; }
    internal int ForceId { get; }
    internal int DistrictId { get; }
    internal DiplomaticMissionKind Kind { get; }
    internal IReadOnlyList<int> Executors { get; }
    internal IReadOnlyList<int> Targets { get; }
    internal int MaxGold { get; }
    internal int MaxSupplies { get; }
    internal int AvailableBooks { get; }
    internal int RequiredBooks { get; }
    private readonly SourceWorldContext current;
    private readonly ArmyCampaignRuleAdapter adapter;
    private readonly DiplomacyCommandSource qualification;
    private readonly Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>> card;
    private readonly Dictionary<int, int> people = [], homes = [];
    private readonly Dictionary<int, IReadOnlyList<int>> targetCities = [];
    private readonly Dictionary<BaseReference, IReadOnlyList<byte>> links = [];

    internal DiplomacyUiSource(ArmyCampaignRuleAdapter adapter, GameState state, SourceWorldContext current,
        LoadedScenario scenario, int forceId, int districtId, DiplomaticMissionKind kind,
        Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>> card)
    {
        this.adapter = adapter; State = state; this.current = current; ForceId = forceId;
        foreach (var area in scenario.Snapshot.Areas) areaNames[area.Slot] = area.Name;
        DistrictId = districtId; Kind = kind; this.card = card;
        if (state.Phase != TurnPhase.Strategy || !state.Districts.TryGetValue(districtId, out var district) ||
            district.ForceId != forceId || Need(current.ReadDistrictNumber(districtId), "District11") != 1)
            throw Missing("primary_District");
        qualification = new(state, current);
        RequiredBooks = DiplomacyCommandSource.ReadBooks(state, kind);
        AvailableBooks = checked((int)Math.Max(0L, Need(district.OrdersRemaining, "District14") - CommandBookAvailability.Reserved(state, districtId)));
        var treasury = qualification.Capital(forceId);
        MaxGold = ArmyCampaignRuleAdapter.ReadDiplomacyBaseResourceCurrent(current, treasury, BaseResourceKind.Gold);
        MaxSupplies = ArmyCampaignRuleAdapter.ReadDiplomacyBaseResourceCurrent(current, treasury, BaseResourceKind.Supplies);
        foreach (var city in scenario.Snapshot.Cities) links.Add(new(BaseKind.City, city.Slot), city.LinkedLocationSlots);
        foreach (var gate in scenario.Snapshot.Gates) links.Add(new(BaseKind.Gate, gate.Slot), gate.LinkedLocationSlots);
        var actors = new List<int>();
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person_registry"))
        {
            int p = Need(entry, "Person_pointer");
            if (!Need(current.ReadPersonNativeValidity(p), "Person_validity")) continue;
            int id = Need(current.ReadPersonIdentity(p), "Person10");
            var own = current.ReadPersonForce(p) ?? throw Missing("Person_Force");
            if (Need(own.PhysicalSlot, "Person_Force_physical") != forceId) continue;
            int home = Need(current.ReadPersonFirstLocation(p), "Person_E2");
            if (!state.Cities.TryGetValue(home, out var city) || city.DistrictId != districtId ||
                Need(current.ReadPersonStatus(p), "Person_E6") is < 1 or > 4 ||
                Need(OfficerHealthSourceAdapter.ReadByte(current, p), "Person_health161") >= 2 ||
                (Need(current.ReadArmyReturnPersonFlags196(p), "Person_flags15E") & 1) != 0 ||
                PersonCurrentBusySource.Read(current, p, Missing) ||
                BattleRemakeCurrentRegionFactor.IsNativeBandPerson(id) && home is >= 1 and <= 61 && forceId is not (>= 46 and <= 50)) continue;
            if (!state.Officers.TryGetValue(id, out var actor) || actor.Deployed ||
                PlayableOfficerAvailability.IsSourceBusy(state, actor) || PlayableDiplomacy.IsOfficerOnMission(state, id) ||
                state.PreparedTalentOrders?.ContainsKey(id) == true || state.PreparedOfficerMoves?.ContainsKey(id) == true ||
                state.PreparedDeployments.Values.Any(plan => plan.OfficerId == id)) continue;
            people[id] = p; homes[id] = home; actors.Add(id);
        }
        Executors = actors.AsReadOnly();
        var recipients = new List<int>();
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("Force_registry"))
        {
            if (entry is not { Kind: RegistryKind.Force }) throw Missing("Force_registry_payload");
            int other = Need(entry.PhysicalSlot, "Force_pointer");
            if (other == forceId || !state.Forces.TryGetValue(other, out var force) || !force.IsActive) continue;
            try
            {
                qualification.RequireInteraction(forceId, other);
                var relation = Relation(other);
                if (kind == DiplomaticMissionKind.Goodwill && (relation.Relation >= 200 || other is >= 46 and <= 50) ||
                    kind == DiplomaticMissionKind.Alliance && (other is >= 46 and <= 50 || relation.Level < 3 || relation.AllianceMonths > 42) ||
                    kind == DiplomaticMissionKind.CancelAlliance && qualification.TreatyMonths(other, forceId) == 0 ||
                    kind == DiplomaticMissionKind.RequestAttack && (relation.AllianceMonths == 0 || relation.Level < 4 ||
                        Need(force.CombatObjective?.RemainingMonths, "recipient_Force161") != 0)) continue;
                if (HasPending(other)) continue;
                if (kind == DiplomaticMissionKind.RequestAttack && Cities(other).Count == 0) continue;
                recipients.Add(other);
            }
            catch (ArgumentException) { }
        }
        Targets = recipients.AsReadOnly();
    }

    internal int Home(int officer) => homes[officer];
    internal int ReverseTreatyMonths(int force) => qualification.TreatyMonths(force, ForceId);
    internal DiplomacyRelationCurrent Relation(int force) => adapter.ReadDiplomacyRelationCurrent(State, current, ForceId, force);
    internal int DisplayRelationLevel(int force) => adapter.ReadDiplomacyRelationCurrent(State, current, force, ForceId).Level;
    internal AreaAdministrationQuery<AreaAdministrationOfficerCard> Card(int officer) => card(officer);
    internal DiplomacyAdvisorResult Advice(int actor, int target, int months, int? targetCity, DiplomacyOffer offer)
    {
        var result = adapter.ReadDiplomacyAdvisorCurrent(State, current, ForceId, actor, target, Kind, months, targetCity, offer);
        State = result.State;
        return result;
    }
    internal void RequireSubmission(PrepareDiplomaticOrder order)
        => _ = adapter.ReadDiplomacySubmissionCurrent(State, current, order, State.Forces[ForceId], new("diplomacy-ui"));

    private bool HasPending(int recipient)
    {
        foreach (var task in State.PersonnelCommands?.Diplomacies?.Values ?? throw Missing("saved_Diplomacies"))
            if (!task.IsRetiring && task.ForceId == ForceId && task.TargetForceId == recipient &&
                task.NativeKind is >= 4 and <= 9 && task.MemberIds.Length != 0 &&
                adapter.DiplomacyContinuationAllowedCurrent(State, current, task).Allowed) return true;
        return false;
    }

    internal IReadOnlyList<int> Cities(int recipient)
    {
        if (targetCities.TryGetValue(recipient, out var stored)) return stored;
        var result = new List<int>(); var seen = new HashSet<BaseReference>();
        //6DE8A0 walks the recipient's Cities, then22F430 through at most
        //two intervening Gates. It is not an arbitrary enemy-City directory.
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("City_registry"))
        {
            if (entry is not { Kind: RegistryKind.City }) throw Missing("City_registry_payload");
            var origin = new BaseReference(BaseKind.City, entry.PhysicalSlot);
            if (Need(current.ReadBaseForceSlot(origin), "recipient_City_Force") != recipient) continue;
            foreach (var candidate in NeighborCities(origin))
            {
                if (!seen.Add(candidate)) continue;
                int victim = Need(current.ReadBaseForceSlot(candidate), "target_City_Force");
                int location = Need(current.ReadBaseObjectLink(candidate), "target_City4E");
                if (victim is < 1 or > 51 || victim == recipient || victim == ForceId || victim is >= 46 and <= 50 ||
                    location is >= 52 and <= 61 || qualification.TreatyMonths(recipient, victim) != 0 ||
                    qualification.TreatyMonths(ForceId, victim) != 0) continue;
                if (State.Cities.ContainsKey(location)) result.Add(location);
            }
        }
        return targetCities[recipient] = result.AsReadOnly();
    }

    private IEnumerable<BaseReference> NeighborCities(BaseReference origin)
    {
        int original = Need(current.ReadBaseObjectLink(origin), "origin4E");
        foreach (byte first in Links(origin))
        {
            if (first == 0 || first == original) continue;
            var a = BaseLookup.Find(first, current);
            if (!Need(current.ReadBaseNativeValidity(a), "neighbor_validity")) continue;
            if (Need(current.ReadBaseObjectLink(a), "neighbor4E") < 52) { yield return a; continue; }
            foreach (byte second in Links(a))
            {
                if (second == 0 || second == original) continue;
                var b = BaseLookup.Find(second, current);
                if (!Need(current.ReadBaseNativeValidity(b), "second_neighbor_validity")) continue;
                if (Need(current.ReadBaseObjectLink(b), "second_neighbor4E") < 52) { yield return b; continue; }
                int firstGate = Need(current.ReadBaseObjectLink(a), "first_Gate4E");
                foreach (byte third in Links(b))
                {
                    if (third == 0 || third == original || third == firstGate) continue;
                    var c = BaseLookup.Find(third, current);
                    if (Need(current.ReadBaseNativeValidity(c), "third_neighbor_validity") &&
                        Need(current.ReadBaseObjectLink(c), "third_neighbor4E") < 52) yield return c;
                }
            }
        }
    }

    private IEnumerable<byte> Links(BaseReference location)
        => links.TryGetValue(location, out var row) ? row.Take(10) : throw Missing("Base58_static_links");

    internal int Travel(int officer, int recipient)
    {
        var date = State.NativeMapSeason?.ThirdDate ?? throw Missing("travel_date");
        var start = PersonReturnCommands.ResolvePersonPosition(State, current, people[officer], date);
        var target = qualification.Capital(recipient);
        int raw = current.ReadBaseCurrentHexIndex(target);
        var end = current.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0) ?? throw Missing("travel_target_Hex");
        var own = current.ReadPersonForce(people[officer]) ?? throw Missing("travel_Person_Force");
        var output = current.CreateTemporaryHexList() ?? throw Missing("travel_list");
        try
        {
            var result = PersonReturnPathSource.Fill(State, current, start, end, own, output);
            return Need(result.Days, result.UnresolvedOwner ?? "travel231670");
        }
        finally { current.ClearTemporaryHexList(output); current.ReleaseTemporaryHexList(output); }
    }

    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("diplomacy.ui." + field);
}
