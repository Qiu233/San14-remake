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
    public S14EventConditionForcePointer? ReadConditionForceTableSlot(int slot) => NativePlayer?.ReadConditionForceTableSlot(slot);
    public bool? ReadConditionForceValidity(S14EventConditionForceReference force) => NativePlayer?.ReadConditionForceValidity(force);
    public bool? ReadConditionApplicationPresent() => NativePlayer?.ReadConditionApplicationPresent();
    public int? ReadConditionCurrentUserIndex() => NativePlayer?.ReadConditionCurrentUserIndex();
    public int? ReadConditionSelectedUserPersonId(int index) => NativePlayer?.ReadConditionSelectedUserPersonId(index);

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
