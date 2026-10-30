using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>
/// Current campaign fields for the person conditions. Explicit physical-to-Core
/// maps establish receiver identity. The native provider owns pointer aliases,
/// registration order and validity; missing Core fields never reuse scenario
/// status/ruler values. This view is recreated for each current event snapshot.
/// </summary>
public sealed class CampaignEventCurrentQueries : IS14PlayerEventConditionQueries
{
    private readonly GameState _state;
    private readonly IS14PersonEventConditionQueries _native;
    private readonly IReadOnlyDictionary<int, int> _persons, _forces;
    private readonly int? _currentPlayerForceId;

    public CampaignEventCurrentQueries(GameState state,
        IS14PersonEventConditionQueries native,
        IReadOnlyDictionary<int, int> personPhysicalToOfficerId,
        IReadOnlyDictionary<int, int> forcePhysicalToForceId, int? currentPlayerForceId = null)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _native = native ?? throw new ArgumentNullException(nameof(native));
        ArgumentNullException.ThrowIfNull(personPhysicalToOfficerId);
        ArgumentNullException.ThrowIfNull(forcePhysicalToForceId);
        _persons = new Dictionary<int, int>(personPhysicalToOfficerId);
        _forces = new Dictionary<int, int>(forcePhysicalToForceId);
        _currentPlayerForceId = currentPlayerForceId;
        if (_persons.Any(pair => pair.Key < 0 || !_state.Officers.ContainsKey(pair.Value)) ||
            _forces.Any(pair => pair.Key < 0 || !_state.Forces.ContainsKey(pair.Value)))
            throw new InvalidDataException("Campaign event physical receiver maps contain an unknown Core entity.");
        if (currentPlayerForceId is int player && !_state.Forces.ContainsKey(player))
            throw new InvalidDataException("Current event player force is not a campaign force.");
    }

    public S14EventConditionPersonPointer? ReadConditionPersonTableSlot(int slot) => _native.ReadConditionPersonTableSlot(slot);
    public S14EventConditionPersonPointer? ReadConditionBasePerson() => _native.ReadConditionBasePerson();
    public bool? ReadConditionPersonValidity(S14EventConditionPersonReference person) => _native.ReadConditionPersonValidity(person);
    public IReadOnlyList<S14EventConditionForceReference>? ReadRegisteredConditionForcesInNativeOrder() =>
        _native.ReadRegisteredConditionForcesInNativeOrder();

    public byte? ReadConditionPersonStatus(S14EventConditionPersonReference person)
    {
        if (!_persons.TryGetValue(person.PhysicalSlot, out int id) ||
            !_state.Officers.TryGetValue(id, out OfficerState? officer)) return null;
        return officer.Presence switch
        {
            OfficerPresence.Dead => 9,
            OfficerPresence.NotAppeared => 7,
            OfficerPresence.Undiscovered => 8,
            OfficerPresence.Present => officer.Role switch
            {
                OfficerRole.Ruler => 1, OfficerRole.DistrictLeader => 2,
                OfficerRole.Governor => 3, OfficerRole.Officer => 4,
                OfficerRole.Free => 5, _ => null
            },
            _ => null
        };
    }

    public ushort? ReadConditionForceRulerPersonId(S14EventConditionForceReference force)
    {
        if (!_forces.TryGetValue(force.PhysicalSlot, out int id) ||
            !_state.Forces.TryGetValue(id, out ForceState? current) ||
            current.RulerId is not int ruler) return null;
        return ruler is >= 0 and <= ushort.MaxValue ? (ushort)ruler
            : throw new InvalidDataException("Current campaign ruler ID cannot fit the original word getter.");
    }

    private IS14PlayerEventConditionQueries? NativePlayer => _native as IS14PlayerEventConditionQueries;
    public S14EventConditionPersonPointer? ReadConditionCurrentPlayerPerson() => _native.ReadConditionCurrentPlayerPerson();
    public S14EventConditionForcePointer? ReadConditionCurrentPlayerForce() => NativePlayer?.ReadConditionCurrentPlayerForce();
    public ushort? ReadConditionPersonFlags(S14EventConditionPersonReference person) => _native.ReadConditionPersonFlags(person);
    public bool? ReadConditionPersonHumble(S14EventConditionPersonReference person) => _native.ReadConditionPersonHumble(person);
    public byte? ReadConditionPersonRawLoyalty(S14EventConditionPersonReference person) => _native.ReadConditionPersonRawLoyalty(person);
    public ushort? ReadConditionPersonHome(S14EventConditionPersonReference person) => _native.ReadConditionPersonHome(person);
    public bool? ReadConditionPersonDislikes(S14EventConditionPersonReference first, S14EventConditionPersonReference second) => _native.ReadConditionPersonDislikes(first, second);
    public bool? ReadConditionPersonFamily(S14EventConditionPersonReference first, S14EventConditionPersonReference second) => _native.ReadConditionPersonFamily(first, second);
    public S14EventConditionForcePointer? ReadConditionForceTableSlot(int slot) => NativePlayer?.ReadConditionForceTableSlot(slot);
    public bool? ReadConditionForceValidity(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceValidity(force);
    public int? ReadConditionForceIdentity(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceIdentity(force);
    public int? ReadConditionForceExperience(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceExperience(force);
    public int? ReadConditionAreaIdentity(AreaReference area) => NativePlayer?.ReadConditionAreaIdentity(area);
    public bool? ReadConditionApplicationPresent() => NativePlayer?.ReadConditionApplicationPresent();
    public int? ReadConditionCurrentUserIndex() => NativePlayer?.ReadConditionCurrentUserIndex();
    public int? ReadConditionSelectedUserPersonId(int index) => NativePlayer?.ReadConditionSelectedUserPersonId(index);
    public ushort? ReadConditionEmperorPersonId() => NativePlayer?.ReadConditionEmperorPersonId();
    public int? ReadConditionEmperorForcePhysicalSlot() => NativePlayer?.ReadConditionEmperorForcePhysicalSlot();
    public S14EventConditionCityPointer? ReadConditionCityTableSlot(int slot) => NativePlayer?.ReadConditionCityTableSlot(slot);
    public S14EventConditionCityPointer? ReadConditionCurrentPlayerCity() => NativePlayer?.ReadConditionCurrentPlayerCity();
    public byte? ReadConditionCityLevel(int physical) => NativePlayer?.ReadConditionCityLevel(physical);
    public int? ReadConditionCityForceSlot(int physicalSlot) => NativePlayer?.ReadConditionCityForceSlot(physicalSlot);
    public byte? ReadConditionPersonRawForceSlot(S14EventConditionPersonReference person) => NativePlayer?.ReadConditionPersonRawForceSlot(person);
    public byte? ReadConditionSelectedForce() => NativePlayer?.ReadConditionSelectedForce();
    public BaseReference? ReadConditionBaseSelector(int selector) => NativePlayer?.ReadConditionBaseSelector(selector);
    public bool? ReadConditionBaseValidity(BaseReference reference) => NativePlayer?.ReadConditionBaseValidity(reference);
    public int? ReadConditionBaseTroops(BaseReference reference) => NativePlayer?.ReadConditionBaseTroops(reference);
    public int? ReadConditionBaseGold(BaseReference reference) => NativePlayer?.ReadConditionBaseGold(reference);
    public int? ReadConditionBaseSupplies(BaseReference reference) => NativePlayer?.ReadConditionBaseSupplies(reference);
    public IReadOnlyList<S14EventConditionForcePointer>? ReadRegisteredConditionForcePointers() => NativePlayer?.ReadRegisteredConditionForcePointers();
    public bool? ReadConditionForceCoalitionMembership(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceCoalitionMembership(force);
    public int? ReadConditionBaseForceSlot(BaseReference receiver) => NativePlayer?.ReadConditionBaseForceSlot(receiver);
    public bool? ReadConditionBaseSpecialGovernor(BaseReference receiver) => NativePlayer?.ReadConditionBaseSpecialGovernor(receiver);
    public byte? ReadConditionItemPresence(RegistryObjectReference item) => NativePlayer?.ReadConditionItemPresence(item);
    public bool? ReadConditionForcePolicyEnabled(S14EventConditionForceReference force, int effect) => NativePlayer?.ReadConditionForcePolicyEnabled(force, effect);
    public ForceArmyCityPresence? ReadConditionForceArmyCityPresence(S14EventConditionForceReference force, S14EventConditionCityPointer city) => NativePlayer?.ReadConditionForceArmyCityPresence(force, city);
    public SceneryReference? ReadConditionSceneryTableSlot(int slot) => NativePlayer?.ReadConditionSceneryTableSlot(slot);
    public bool? ReadConditionSceneryValidity(SceneryReference scenery) => NativePlayer?.ReadConditionSceneryValidity(scenery);
    public byte? ReadConditionSceneryFlags(SceneryReference scenery) => NativePlayer?.ReadConditionSceneryFlags(scenery);
    public byte? ReadConditionSceneryLevel(SceneryReference scenery) => NativePlayer?.ReadConditionSceneryLevel(scenery);
    public byte? ReadConditionSceneryForceSlot(SceneryReference scenery) => NativePlayer?.ReadConditionSceneryForceSlot(scenery);
    public byte? ReadConditionBaseMorale(BaseReference reference) => NativePlayer?.ReadConditionBaseMorale(reference);
    public IReadOnlyList<S14EventConditionCityPointer>? ReadRegisteredConditionCities() => NativePlayer?.ReadRegisteredConditionCities();
    public S14EventConditionStatePointer? ReadConditionCityState(int physical) => NativePlayer?.ReadConditionCityState(physical);
    public S14EventConditionForcePointer? ReadConditionBaseForce(BaseReference reference) => NativePlayer?.ReadConditionBaseForce(reference);
    public BaseReference? ReadConditionPersonLocationBase(S14EventConditionPersonReference person) => NativePlayer?.ReadConditionPersonLocationBase(person);
    public ushort? ReadConditionBaseObjectId(BaseReference reference) => NativePlayer?.ReadConditionBaseObjectId(reference);
    public ushort? ReadConditionPersonPartnerId(S14EventConditionPersonReference person) => _native.ReadConditionPersonPartnerId(person);
    public IReadOnlyList<int?>? ReadRegisteredConditionPersonPhysicalSlots() => _native.ReadRegisteredConditionPersonPhysicalSlots();
    public int? ReadConditionForceCityCount(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceCityCount(force);
    public int? ReadConditionForcePersonCount(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForcePersonCount(force);
    public int? ReadConditionForceHealthyHomePersonCount(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceHealthyHomePersonCount(force);
    public S14EventConditionCityPointer? ReadConditionBaseMappedCity(BaseReference home) => NativePlayer?.ReadConditionBaseMappedCity(home);
    public int? ReadConditionForceCompleteStateCount(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceCompleteStateCount(force);
    public short? ReadConditionPersonImperialCourtTendency(S14EventConditionPersonReference person) => NativePlayer?.ReadConditionPersonImperialCourtTendency(person);
    public float? ReadConditionForceTerritoryPercent(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceTerritoryPercent(force);
    public byte? ReadConditionSpecialPersonEncounterFlags() => NativePlayer?.ReadConditionSpecialPersonEncounterFlags();
    public uint? ReadConditionEventAchievementFlags() => NativePlayer?.ReadConditionEventAchievementFlags();
    public IReadOnlyList<CombatEngagementPair>? ReadConditionCombatEngagements() => NativePlayer?.ReadConditionCombatEngagements();
    public int? ReadConditionForceIllnessCount(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceIllnessCount(force);
    public int? ReadConditionForceLifespanCount(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceLifespanCount(force);
    public int? ReadConditionForceReadyTroops(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceReadyTroops(force);
    public IReadOnlyList<S14EventConditionForceReference?>? ReadConditionForceProcessingOrder() => NativePlayer?.ReadConditionForceProcessingOrder();
    public byte? ReadConditionForceAllianceByte(S14EventConditionForceReference force, int otherPhysicalSlot) => NativePlayer?.ReadConditionForceAllianceByte(force, otherPhysicalSlot);
    public int? ReadConditionForceFriendshipLevel(S14EventConditionForceReference first, S14EventConditionForceReference second) => NativePlayer?.ReadConditionForceFriendshipLevel(first, second);
    public bool? ReadConditionForceAdjacent(S14EventConditionForceReference first, S14EventConditionForceReference second) => NativePlayer?.ReadConditionForceAdjacent(first, second);
    public ushort? ReadConditionForceAdvisorId(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceAdvisorId(force);
    public byte? ReadConditionForceKingdomNameIndex(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceKingdomNameIndex(force);
    public byte? ReadConditionForceRank(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceRank(force);
    public RegistryObjectReference? ReadConditionItemTableSlot(int slot) => NativePlayer?.ReadConditionItemTableSlot(slot);
    public bool? ReadConditionItemValidity(RegistryObjectReference item) => NativePlayer?.ReadConditionItemValidity(item);
    public int? ReadConditionItemIllustrationIndex(RegistryObjectReference item)
    {
        if (item.Kind != RegistryKind.Item || item.PhysicalSlot is not int physical ||
            _state.Items?.GetValueOrDefault(physical) is not { } owner) return null;
        //The fresh Item/editor producer has already normalized source51 and
        //flags4F into this independently saved current artwork selection.
        return _state.ItemDefinitions.GetValueOrDefault(owner.DefinitionId)?.Appearance?.ImageIndex;
    }
    public ushort? ReadConditionPersonRawCharacterId(S14EventConditionPersonReference person) =>
        _native.ReadConditionPersonCharacterId(person);
    public ushort? ReadConditionItemOwnerId(RegistryObjectReference item) => NativePlayer?.ReadConditionItemOwnerId(item);
    public ushort? ReadConditionPersonBloodlineId(S14EventConditionPersonReference person) => NativePlayer?.ReadConditionPersonBloodlineId(person);
    public ushort? ReadConditionPersonRelationListIdentity(S14EventConditionPersonReference person, int index, bool like) =>
        NativePlayer?.ReadConditionPersonRelationListIdentity(person, index, like);
    public int? ReadConditionForceRegionCityCount(S14EventConditionForceReference force, int region) => NativePlayer?.ReadConditionForceRegionCityCount(force, region);
    public AreaReference? ReadConditionBaseArea(BaseReference reference) => NativePlayer?.ReadConditionBaseArea(reference);
    public bool? ReadConditionAreaValidity(AreaReference reference) => NativePlayer?.ReadConditionAreaValidity(reference);
    public AreaReference? ReadConditionAreaTableSlot(int slot) => NativePlayer?.ReadConditionAreaTableSlot(slot);
    public int? ReadConditionAreaForceSlot(AreaReference reference) => NativePlayer?.ReadConditionAreaForceSlot(reference);
    public byte? ReadConditionAreaSecurity(AreaReference reference) => NativePlayer?.ReadConditionAreaSecurity(reference);
    public int? ReadConditionAreaDevelopmentLevel(AreaReference reference, CeAreaDevelopmentField field) => NativePlayer?.ReadConditionAreaDevelopmentLevel(reference, field);
    public BaseReference? ReadConditionPersonHomeBase(S14EventConditionPersonReference person) => NativePlayer?.ReadConditionPersonHomeBase(person);
    public S14EventConditionForcePointer? ReadConditionPrisonerCustodianForce(S14EventConditionPersonReference person) => NativePlayer?.ReadConditionPrisonerCustodianForce(person);
    public byte? ReadConditionPersonBaseAbility(S14EventConditionPersonReference person, int abilityIndex) => _native.ReadConditionPersonBaseAbility(person, abilityIndex);
    public bool? ReadConditionPersonIsAreaOfficer(S14EventConditionPersonReference person) => _native.ReadConditionPersonIsAreaOfficer(person);
    public S14EventConditionStatePointer? ReadConditionStateTableSlot(int slot) => NativePlayer?.ReadConditionStateTableSlot(slot);
    public bool? ReadConditionStateValidity(S14EventConditionStatePointer state) => NativePlayer?.ReadConditionStateValidity(state);
    public int? ReadConditionForceStateCityCount(S14EventConditionForceReference force, int statePhysicalSlot) => NativePlayer?.ReadConditionForceStateCityCount(force, statePhysicalSlot);
    public int? ReadConditionForceStateAreaCount(S14EventConditionForceReference force, int statePhysicalSlot) => NativePlayer?.ReadConditionForceStateAreaCount(force, statePhysicalSlot);
    public byte? ReadConditionForceDependence(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceDependence(force);
    public S14EventConditionStatePointer? ReadConditionAreaOriginalState(AreaReference area) => NativePlayer?.ReadConditionAreaOriginalState(area);
    public bool? ReadConditionForceStateAdvantage(S14EventConditionForceReference force, S14EventConditionStatePointer state) => NativePlayer?.ReadConditionForceStateAdvantage(force, state);

    public S14EventConditionForcePointer? ReadConditionPersonForce(S14EventConditionPersonReference person)
    {
        if (!_persons.TryGetValue(person.PhysicalSlot, out int id) ||
            !_state.Officers.TryGetValue(id, out OfficerState? officer) || officer.ForceId is not int owner) return null;
        int[] physical = _forces.Where(pair => pair.Value == owner).Select(pair => pair.Key).ToArray();
        return physical.Length == 1 ? new(new(physical[0])) : null;
    }
    public int? ReadConditionCurrentPlayerForcePhysicalSlot()
    {
        if (_currentPlayerForceId is not int player) return null;
        int[] physical = _forces.Where(pair => pair.Value == player).Select(pair => pair.Key).ToArray();
        return physical.Length == 1 ? physical[0] : null;
    }
    public ushort? ReadConditionPersonCharacterId(S14EventConditionPersonReference person)
    {
        if (!_persons.TryGetValue(person.PhysicalSlot, out int id)) return null;
        return id is >= 0 and <= ushort.MaxValue ? (ushort)id
            : throw new InvalidDataException("Current campaign character ID cannot fit the original word getter.");
    }

    // These getters expose current independent world facts for subsequent
    // source leaves. They do not assert an unimplemented native comparison.
    public int? ReadCityForceId(int cityId) => _state.Cities.GetValueOrDefault(cityId)?.ForceId;
    public int? ReadOfficerForceId(int officerId) => _state.Officers.GetValueOrDefault(officerId)?.ForceId;
    public int? ReadOfficerLocationId(int officerId) => _state.Officers.GetValueOrDefault(officerId)?.LocationId;
    public int? ReadAreaOfficerId(int areaId) => _state.Areas.GetValueOrDefault(areaId)?.Administration?.OverseerId;
    public int? ReadAreaForceId(int areaId) => _state.Areas.GetValueOrDefault(areaId)?.ForceId;
    public int? ReadAreaSecurity(int areaId) => _state.Areas.GetValueOrDefault(areaId)?.Security;
    public AreaDevelopmentValues? ReadAreaDevelopment(int areaId) => _state.Areas.GetValueOrDefault(areaId)?.Development;
}
