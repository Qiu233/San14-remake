using San14.Core;

namespace San14.Data.Formats;

public sealed record S14EventConditionForcePointer(S14EventConditionForceReference? Value);
public sealed record S14EventConditionCityPointer(int? PhysicalSlot);
public sealed record S14EventConditionStatePointer(int? PhysicalSlot);
public sealed record ForceArmyCityPresence(bool HasEligibleArmy, bool InOriginalCity);

/// <summary>
/// Explicit native inputs for SCompareUserForce. Person-force and current-player
/// getters include the native district/force-zero fallbacks described below;
/// an unknown outer lookup is distinct from a known null pointer.
/// </summary>
public interface IS14PlayerEventConditionQueries : IS14PersonEventConditionQueries
{
    S14EventConditionForcePointer? ReadConditionForceTableSlot(int slot);

    // Complete 22CE20: person+118 -> district table (0 when >51), native
    // district validity -> district+10 -> force table (0 when invalid or >51).
    S14EventConditionForcePointer? ReadConditionPersonForce(S14EventConditionPersonReference person);
    bool? ReadConditionForceValidity(S14EventConditionForceReference force);
    int? ReadConditionForceIdentity(S14EventConditionForceReference force) => null;
    int? ReadConditionForceExperience(S14EventConditionForceReference force) => null;
    int? ReadConditionAreaIdentity(AreaReference area) => null;

    // Complete 32F200 -> force.slot12: world+3A force slot (>51 selects force0).
    int? ReadConditionCurrentPlayerForcePhysicalSlot();
    bool? ReadConditionApplicationPresent();

    // Complete 1D2A80: signed world+BC; a known null world pointer returns -1.
    int? ReadConditionCurrentUserIndex();
    byte? ReadConditionSpecialPersonEncounterFlags() => null;
    uint? ReadConditionEventAchievementFlags() => null;
    IReadOnlyList<CombatEngagementPair>? ReadConditionCombatEngagements() => null;

    // Signed i32 world+64+index*4. No guessed player-selection contents.
    int? ReadConditionSelectedUserPersonId(int index);
    ushort? ReadConditionEmperorPersonId() => null;
    int? ReadConditionEmperorForcePhysicalSlot() => null;
    S14EventConditionCityPointer? ReadConditionCityTableSlot(int slot) => null;
    S14EventConditionCityPointer? ReadConditionCurrentPlayerCity() => null;
    byte? ReadConditionCityLevel(int physical) => null;
    int? ReadConditionCityForceSlot(int physicalSlot) => null;
    byte? ReadConditionPersonRawForceSlot(S14EventConditionPersonReference person) => null;
    byte? ReadConditionSelectedForce() => null;
    BaseReference? ReadConditionBaseSelector(int selector) => null;
    bool? ReadConditionBaseValidity(BaseReference reference) => null;
    S14EventConditionForcePointer? ReadConditionBaseForce(BaseReference reference) => null;
    BaseReference? ReadConditionPersonLocationBase(S14EventConditionPersonReference person) => null;
    ushort? ReadConditionBaseObjectId(BaseReference reference) => null;
    int? ReadConditionForceCityCount(S14EventConditionForceReference force) => null;
    int? ReadConditionForcePersonCount(S14EventConditionForceReference force) => null;
    int? ReadConditionForceHealthyHomePersonCount(S14EventConditionForceReference force) => null;
    S14EventConditionCityPointer? ReadConditionBaseMappedCity(BaseReference home) => null;
    int? ReadConditionForceCompleteStateCount(S14EventConditionForceReference force) => null;
    short? ReadConditionPersonImperialCourtTendency(S14EventConditionPersonReference person) => null;
    float? ReadConditionForceTerritoryPercent(S14EventConditionForceReference force) => null;
    int? ReadConditionForceIllnessCount(S14EventConditionForceReference force) => null;
    int? ReadConditionForceLifespanCount(S14EventConditionForceReference force) => null;
    int? ReadConditionForceReadyTroops(S14EventConditionForceReference force) => null;
    IReadOnlyList<S14EventConditionForceReference?>? ReadConditionForceProcessingOrder() => null;
    S14EventConditionForcePointer? ReadConditionCurrentPlayerForce() => null;
    byte? ReadConditionForceAllianceByte(S14EventConditionForceReference force, int otherPhysicalSlot) => null;
    int? ReadConditionForceFriendshipLevel(S14EventConditionForceReference first, S14EventConditionForceReference second) => null;
    bool? ReadConditionForceAdjacent(S14EventConditionForceReference first, S14EventConditionForceReference second) => null;
    ushort? ReadConditionForceAdvisorId(S14EventConditionForceReference force) => null;
    BaseReference? ReadConditionPersonHomeBase(S14EventConditionPersonReference person) => null;
    byte? ReadConditionForceKingdomNameIndex(S14EventConditionForceReference force) => null;
    byte? ReadConditionForceRank(S14EventConditionForceReference force) => null;
    RegistryObjectReference? ReadConditionItemTableSlot(int slot) => null;
    bool? ReadConditionItemValidity(RegistryObjectReference item) => null;
    int? ReadConditionItemIllustrationIndex(RegistryObjectReference item) => null;
    //95 writes the receiver's raw ID10 even when it fails native validity.
    ushort? ReadConditionPersonRawCharacterId(S14EventConditionPersonReference person) =>
        ReadConditionPersonCharacterId(person);
    ushort? ReadConditionItemOwnerId(RegistryObjectReference item) => null;
    ushort? ReadConditionPersonBloodlineId(S14EventConditionPersonReference person) => null;
    ushort? ReadConditionPersonRelationListIdentity(S14EventConditionPersonReference person, int index, bool like) => null;
    int? ReadConditionForceRegionCityCount(S14EventConditionForceReference force, int region) => null;
    AreaReference? ReadConditionBaseArea(BaseReference reference) => null;
    bool? ReadConditionAreaValidity(AreaReference reference) => null;
    AreaReference? ReadConditionAreaTableSlot(int slot) => null;
    int? ReadConditionAreaForceSlot(AreaReference reference) => null;
    byte? ReadConditionAreaSecurity(AreaReference reference) => null;
    int? ReadConditionAreaDevelopmentLevel(AreaReference reference, CeAreaDevelopmentField field) => null;
    S14EventConditionForcePointer? ReadConditionPrisonerCustodianForce(S14EventConditionPersonReference person) => null;
    S14EventConditionStatePointer? ReadConditionStateTableSlot(int slot) => null;
    bool? ReadConditionStateValidity(S14EventConditionStatePointer state) => null;
    int? ReadConditionForceStateCityCount(S14EventConditionForceReference force, int statePhysicalSlot) => null;
    int? ReadConditionForceStateAreaCount(S14EventConditionForceReference force, int statePhysicalSlot) => null;
    byte? ReadConditionForceDependence(S14EventConditionForceReference force) => null;
    S14EventConditionStatePointer? ReadConditionAreaOriginalState(AreaReference area) => null;
    bool? ReadConditionForceStateAdvantage(S14EventConditionForceReference force, S14EventConditionStatePointer state) => null;
    IReadOnlyList<S14EventConditionCityPointer>? ReadRegisteredConditionCities() => null;
    S14EventConditionStatePointer? ReadConditionCityState(int physical) => null;
    int? ReadConditionBaseTroops(BaseReference reference) => null;
    byte? ReadConditionBaseMorale(BaseReference reference) => null;
    int? ReadConditionBaseGold(BaseReference reference) => null;
    int? ReadConditionBaseSupplies(BaseReference reference) => null;
    IReadOnlyList<S14EventConditionForcePointer>? ReadRegisteredConditionForcePointers() => null;
    bool? ReadConditionForceCoalitionMembership(S14EventConditionForceReference force) => null;
    int? ReadConditionBaseForceSlot(BaseReference receiver) => null;
    bool? ReadConditionBaseSpecialGovernor(BaseReference receiver) => null;
    byte? ReadConditionItemPresence(RegistryObjectReference item) => null;
    bool? ReadConditionForcePolicyEnabled(S14EventConditionForceReference force, int effect) => null;
    ForceArmyCityPresence? ReadConditionForceArmyCityPresence(S14EventConditionForceReference force, S14EventConditionCityPointer city) => null;
    SceneryReference? ReadConditionSceneryTableSlot(int slot) => null;
    bool? ReadConditionSceneryValidity(SceneryReference scenery) => null;
    byte? ReadConditionSceneryFlags(SceneryReference scenery) => null;
    byte? ReadConditionSceneryLevel(SceneryReference scenery) => null;
    byte? ReadConditionSceneryForceSlot(SceneryReference scenery) => null;
}

/// <summary>
/// Current CE SCompareUserForce slot0 3B5A10..3B5B5A, composed with the complete
/// person conditions59/298 and optional basic conditions. Supports the literal
/// nonnegative person-selector branch; dynamic selectors remain explicit errors.
/// Current queries are sampled only when the original branch consumes them.
/// </summary>
public sealed partial class S14PlayerEventConditionRuntime : IS14EventConditionRuntime
{
    private readonly IS14PlayerEventConditionQueries _queries;
    private readonly Func<S14ConditionContext, S14ConditionDispatchInputs> _inputs;
    private readonly S14PersonEventConditionRuntime _person;
    private S14EventConditionProgram? _program;
    private bool _blocked;
    private readonly List<S14EventConditionPersonReference> _selectedPersons = [];
    private readonly List<S14EventConditionCityPointer> _selectedCities = [];
    // A fresh native condition execution clears these target references.
    // They belong to this event evaluation, not the saved world's selected force.
    private readonly S14EventConditionForceReference?[] _targetForces = new S14EventConditionForceReference?[13];
    public bool ConditionBlocked => _blocked || _person.ConditionBlocked;

    public S14PlayerEventConditionRuntime(IS14PlayerEventConditionQueries queries,
        Func<S14ConditionContext, S14ConditionDispatchInputs> inputs,
        IS14BasicEventConditionQueries? basicQueries = null)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        _person = new(queries, inputs, basicQueries, RawPersonPointer);
    }

    public static bool IsSupported(int code) => IsMessageParameterConditionCode(code) || IsStoryPresenceConditionCode(code) || IsStoryAggregateConditionCode(code) || code is 46 or 444 or 452 or 436 or 437 or 431 or 417 or 422 or 423 or 451 or 367 or 372 or 373 or 374 or 12 or 18 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27 or 28 or 29 or 30 or 32 or 33 or 34 or 35 or 36 or 37 or 38 or 39 or 40 or 41 or 42 or 47 or 48 or 49 or 50 or 52 or 55 or 56 or 57 or 58 or 65 or 68 or 73 or 74 or 78 or 216 or 225 or 229 or 231 or 243 or 246 or 255 or 264 or 265 or 280 or 282 or 300 or 317 or 335 or 336 or 347 or 355 or 361 or 362 or 363 or 369 or 371 or 378 || S14PersonEventConditionRuntime.IsSupported(code);

    public bool TryGetBinding(int code, out S14ConditionBinding binding)
    {
        if (IsMessageParameterConditionCode(code) || IsStoryPresenceConditionCode(code)) { binding = new(0, 1, true); return true; }
        if (IsStoryAggregateConditionCode(code)) { binding = new(0, 1, true); return true; }
        if (code is 46 or 444 or 452 or 436 or 437 or 431 or 417 or 422 or 423 or 451 or 367 or 373 or 374) { binding = new(0, 1, true); return true; }
        if (code == 372) { binding = new(0, 0, true); return true; }
        if (code is 280 or 282 or 300 or 317 or 335 or 336 or 347 or 355 or 361 or 362 or 363 or 371 or 378) { binding = new(0, 1, true); return true; }
        if (code is 246 or 255 or 264 or 265 or 369) { binding = new(0, 0, true); return true; }
        if (code is 12 or 18 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27 or 28 or 29 or 30 or 32 or 33 or 34 or 35 or 36 or 37 or 38 or 39 or 40 or 41 or 42 or 47 or 48 or 49 or 50 or 52 or 55 or 56 or 57 or 58 or 65 or 68 or 73 or 74 or 78 or 216 or 225 or 229 or 231 or 243) { binding = new(0, 1, true); return true; }
        return _person.TryGetBinding(code, out binding);
    }

    public S14ConditionDispatchInputs ReadInputs(S14ConditionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_program is not null && !ReferenceEquals(_program, context.Program))
            throw new InvalidDataException("Player condition state cannot be reused across programs.");
        _program = context.Program;
        return _inputs(context);
    }

    public int InvokeLeaf(S14ConditionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsMessageParameterConditionCode(context.Operation.Code)) return InvokeMessageParameterCondition(context);
        if (IsStoryPresenceConditionCode(context.Operation.Code)) return EvaluateStoryPresenceCondition(context) ? 1 : 0;
        if (IsStoryAggregateConditionCode(context.Operation.Code))
            return EvaluateStoryAggregateCondition(context).Result ? 1 : 0;
        if (context.Operation.Code is not (46 or 444 or 452 or 436 or 437 or 431 or 417 or 422 or 423 or 451 or 367 or 373 or 374 or 12 or 18 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27 or 28 or 29 or 30 or 32 or 33 or 34 or 35 or 36 or 37 or 38 or 39 or 40 or 41 or 42 or 47 or 48 or 49 or 50 or 52 or 55 or 56 or 57 or 58 or 65 or 68 or 73 or 74 or 78 or 225 or 229 or 231 or 243 or 246 or 255 or 264 or 265 or 280 or 282 or 300 or 317 or 335 or 336 or 347 or 355 or 361 or 362 or 363 or 369 or 371 or 378)) return _person.InvokeLeaf(context);
        try
        {
            IReadOnlyList<int> arguments = context.Operation.RawArguments;
            if (arguments.Count != 8) throw new InvalidDataException("Player condition operands are incomplete.");
            if (context.Operation.Code == 46) return SelectObstacleTarget(arguments[0], arguments[1]) ? 1 : 0;
            if (context.Operation.Code == 444)
            {
                var city = ResolveCityPointer(arguments[0]);
                var receiver = new BaseReference(BaseKind.City, city.PhysicalSlot);
                S14EventConditionForceReference? force = null;
                if (BaseValid(receiver) && Known(_queries.ReadConditionCityForceSlot(Known(city.PhysicalSlot, "target city identity")), "target city force") != 0 &&
                    !Known(_queries.ReadConditionBaseSpecialGovernor(receiver), "target city special governor"))
                    force = (_queries.ReadConditionBaseForce(receiver) ?? throw new InvalidDataException("Target city force pointer is unknown.")).Value;
                if (force is null || !Known(_queries.ReadConditionForceValidity(force), "target city force validity")) return 0;
                StoreTargetForce(arguments[1], force);
                return 1;
            }
            if (context.Operation.Code == 452)
            {
                var first = ResolveRulerForce(arguments[0]);
                var second = ResolveRulerForce(arguments[2]);
                int found = second is not null && Known(_queries.ReadConditionForceValidity(second), "any dependence second force validity") &&
                    first is not null && Known(_queries.ReadConditionForceValidity(first), "any dependence first force validity") &&
                    Known(_queries.ReadConditionForceDependence(first), "current any dependence") == second.PhysicalSlot ? 1 : 0;
                if (context.OrDepth == 0 && arguments[1] == 0 && found == 1) _blocked = true;
                return found == arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 436)
            {
                if (arguments[0] < 0) throw new InvalidDataException("Negative raw item index is unsupported.");
                var item = _queries.ReadConditionItemTableSlot(arguments[0] < 202 ? arguments[0] : 0)
                    ?? throw new InvalidDataException("Current event item pointer is unknown.");
                if (item.PhysicalSlot is null) return 0;
                return (Known(_queries.ReadConditionItemPresence(item), "current event item presence") == 1 ? 1 : 0) == arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 437)
            {
                var receivers = arguments.Skip(1).Take(5).Select(ResolveBase).ToArray();
                bool HasEmptyBase()
                {
                    foreach (var receiver in receivers)
                        if (BaseValid(receiver) && Known(_queries.ReadConditionBaseForceSlot(receiver), "empty base force") == 0)
                            return true;
                    return false;
                }
                int found = HasEmptyBase() ? 1 : 0;
                if (context.OrDepth == 0 && arguments[0] == 1 && !HasEmptyBase()) _blocked = true;
                return found == arguments[0] ? 1 : 0;
            }
            if (context.Operation.Code == 431)
            {
                if (arguments[1] is < 1 or > 79) return 0;
                var force = ResolveRulerForce(arguments[0]);
                if (force is null || !Known(_queries.ReadConditionForceValidity(force), "policy panel force validity")) return 0;
                return (Known(_queries.ReadConditionForcePolicyEnabled(force, arguments[1]), "current enabled policy panel") ? 1 : 0) == arguments[2] ? 1 : 0;
            }
            if (context.Operation.Code == 417)
            {
                var force = ResolveRulerForce(arguments[0]);
                int expected = arguments[1];
                var city = ResolveCityPointer(arguments[2]);
                if (force is null || !Known(_queries.ReadConditionForceValidity(force), "unit-location force validity")) return 0;
                var presence = _queries.ReadConditionForceArmyCityPresence(force, city)
                    ?? throw new InvalidDataException("Current registered force army city presence is unknown.");
                return presence.HasEligibleArmy && (presence.InOriginalCity ? 1 : 0) == expected ? 1 : 0;
            }
            if (context.Operation.Code is 422 or 423 or 451)
            {
                var scenery = ResolveScenery(arguments[0]);
                // 422 resolves its force before the second scenery validity read.
                var force = context.Operation.Code == 422 ? ResolveRulerForce(arguments[2]) : null;
                if (!Known(_queries.ReadConditionSceneryValidity(scenery), "current scenery validity")) return 0;
                if (context.Operation.Code == 422)
                {
                    if (force is null || !Known(_queries.ReadConditionForceValidity(force), "scenery target force validity")) return 0;
                    return Compare(Known(_queries.ReadConditionSceneryForceSlot(scenery), "current scenery hex force"), arguments[1], force.PhysicalSlot) ? 1 : 0;
                }
                if ((Known(_queries.ReadConditionSceneryFlags(scenery), "current scenery enabled flags") & 1) == 0) return 0;
                byte level = Known(_queries.ReadConditionSceneryLevel(scenery), "current scenery development level");
                if (context.Operation.Code == 451) return Compare(level, arguments[1], arguments[2]) ? 1 : 0;
                if (arguments[1] > level) return 0;
                if ((Known(_queries.ReadConditionSceneryFlags(scenery), "current scenery completion flags") & 4) == 0) return 1;
                _blocked = true;
                return 0;
            }
            if (context.Operation.Code == 367)
            {
                var force = ResolveRulerForce(arguments[0]);
                if (force is null || !Known(_queries.ReadConditionForceValidity(force), "healthy-home force validity")) return 0;
                return Known(_queries.ReadConditionForceHealthyHomePersonCount(force), "healthy-home person count") >= arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 373) return CompareCityListOwnership(arguments[0]) ? 1 : 0;
            if (context.Operation.Code == 374) return CompareCityListForce(arguments[0], arguments[1], arguments[2]) ? 1 : 0;
            if (context.Operation.Code is 371 or 369)
            {
                if (arguments[0] < 0) throw new InvalidDataException("Negative advantage-state index is unsupported.");
                var state = _queries.ReadConditionStateTableSlot(arguments[0] < 23 ? arguments[0] : 0)
                    ?? throw new InvalidDataException("Advantage-state target pointer is unknown.");
                if (context.Operation.Code == 371)
                {
                    if (!Known(_queries.ReadConditionStateValidity(state), "advantage-existence state validity")) return 0;
                    int found = 0;
                    var forces = _queries.ReadRegisteredConditionForcePointers()
                        ?? throw new InvalidDataException("Advantage-existence force registry is unknown.");
                    foreach (var candidate in forces)
                        if (candidate.Value is { } force && Known(_queries.ReadConditionForceStateAdvantage(force, state), "current force state advantage"))
                        { found = 1; break; }
                    return found == arguments[1] ? 1 : 0;
                }
                if (arguments[1] is < 1 or > 4) return 0;
                // The fallback is sampled before walking the registry, even if a
                // later candidate succeeds. No outer State validity gate exists.
                var selected = (_queries.ReadConditionForceTableSlot(0)
                    ?? throw new InvalidDataException("Advantage target force0 fallback is unknown.")).Value;
                var registered = _queries.ReadRegisteredConditionForcePointers()
                    ?? throw new InvalidDataException("Advantage target force registry is unknown.");
                foreach (var candidate in registered)
                    if (candidate.Value is { } force && Known(_queries.ReadConditionForceStateAdvantage(force, state), "current force state advantage"))
                    { selected = force; break; }
                StoreTargetForce(arguments[1], selected);
                return 1;
            }
            if (context.Operation.Code == 378)
            {
                var first = ResolveRulerForce(arguments[0]);
                var second = ResolveRulerForce(arguments[2]);
                int found = first is not null && Known(_queries.ReadConditionForceValidity(first), "coalition first force validity") &&
                    second is not null && Known(_queries.ReadConditionForceValidity(second), "coalition second force validity") &&
                    Known(_queries.ReadConditionForceCoalitionMembership(first), "first current coalition flag") &&
                    Known(_queries.ReadConditionForceCoalitionMembership(second), "second current coalition flag") ? 1 : 0;
                return found == arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 280)
            {
                var city = ResolveCityPointer(arguments[0]);
                var receiver = new BaseReference(BaseKind.City, city.PhysicalSlot);
                if (!BaseValid(receiver)) return 0;
                int gold = Known(_queries.ReadConditionBaseGold(receiver), "current city gold");
                return Compare(gold, arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code is 282 or 361)
            {
                var receiver = ResolveBase(arguments[0]);
                if (!BaseValid(receiver)) return 0;
                int value = context.Operation.Code == 282
                    ? Known(_queries.ReadConditionBaseTroops(receiver), "current base troops")
                    : Known(_queries.ReadConditionBaseMorale(receiver), "current base morale");
                return Compare(value, arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code is 362 or 363)
            {
                if (arguments[0] < 0) throw new InvalidDataException("Negative city-state index is unsupported.");
                var state = _queries.ReadConditionStateTableSlot(arguments[0] < 23 ? arguments[0] : 0)
                    ?? throw new InvalidDataException("City-state target pointer is unknown.");
                bool every = context.Operation.Code == 362;
                // Morale traverses the full table; troops traverses the registered list.
                // Keep table lookup lazy so a decisive result does not read later slots.
                IEnumerable<S14EventConditionCityPointer> cities = every
                    ? Enumerable.Range(0, 52).Select(slot => _queries.ReadConditionCityTableSlot(slot)
                        ?? throw new InvalidDataException("City-state table pointer is unknown."))
                    : _queries.ReadRegisteredConditionCities()
                        ?? throw new InvalidDataException("City-state registered cities are unknown.");
                foreach (var city in cities)
                {
                    var receiver = new BaseReference(BaseKind.City, city.PhysicalSlot);
                    if (!BaseValid(receiver)) continue;
                    var actual = _queries.ReadConditionCityState(city.PhysicalSlot!.Value)
                        ?? throw new InvalidDataException("Current city-state pointer is unknown.");
                    if (actual != state) continue;
                    if (arguments[3] == 1)
                    {
                        var currentPlayer = _queries.ReadConditionCurrentPlayerForce()
                            ?? throw new InvalidDataException("City-state current player force is unknown.");
                        if (currentPlayer.Value is not { } force || !Known(_queries.ReadConditionForceValidity(force), "city-state player force validity")) continue;
                        var owner = _queries.ReadConditionBaseForce(receiver)
                            ?? throw new InvalidDataException("City-state current city force is unknown.");
                        if (owner != currentPlayer) continue;
                    }
                    int value = every
                        ? Known(_queries.ReadConditionBaseMorale(receiver), "current state city morale")
                        : Known(_queries.ReadConditionBaseTroops(receiver), "current state city troops");
                    bool matches = Compare(value, arguments[1], arguments[2]);
                    if (matches != every) return matches ? 1 : 0;
                }
                return every ? 1 : 0;
            }
            if (context.Operation.Code == 335)
            {
                if (arguments[0] < 0) throw new InvalidDataException("Negative advantage-state index is unsupported.");
                var state = _queries.ReadConditionStateTableSlot(arguments[0] < 23 ? arguments[0] : 0)
                    ?? throw new InvalidDataException("Advantage-state pointer is unknown.");
                var force = ResolveRulerForce(arguments[2]);
                int reached = force is not null && Known(_queries.ReadConditionForceStateAdvantage(force, state), "current state advantage") ? 1 : 0;
                return reached == arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 355)
            {
                var person = RawPersonPointer(arguments[0]) ?? RawPersonPointer(0);
                if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "person-state person validity")) return 0;
                var home = _queries.ReadConditionPersonHomeBase(person)
                    ?? throw new InvalidDataException("Person-state home pointer is unknown.");
                int found = 0;
                if (BaseValid(home))
                {
                    var area = _queries.ReadConditionBaseArea(home) ?? throw new InvalidDataException("Person-state home area is unknown.");
                    var actual = _queries.ReadConditionAreaOriginalState(area) ?? throw new InvalidDataException("Person-state original city state is unknown.");
                    var candidates = arguments.Skip(2).Take(4).Select(raw =>
                    {
                        if (raw < 0) throw new InvalidDataException("Negative person-state candidate index is unsupported.");
                        return _queries.ReadConditionStateTableSlot(raw < 23 ? raw : 0)
                            ?? throw new InvalidDataException("Person-state candidate pointer is unknown.");
                    }).ToArray();
                    foreach (var candidate in candidates)
                    {
                        if (!Known(_queries.ReadConditionStateValidity(candidate), "person-state candidate validity")) continue;
                        int current = actual.PhysicalSlot ?? throw new InvalidDataException("Person-state current receiver is null.");
                        if (current == candidate.PhysicalSlot) { found = 1; break; }
                    }
                }
                return found == arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 347)
            {
                int[] cities = arguments.Skip(1).Take(5).Select(ResolveCity).ToArray();
                bool HasUnownedCity()
                {
                    foreach (int city in cities)
                        if (Known(_queries.ReadConditionBaseValidity(new(BaseKind.City, city)), "empty-city validity") &&
                            Known(_queries.ReadConditionCityForceSlot(city), "empty-city current district force") == 0) return true;
                    return false;
                }
                int found = HasUnownedCity() ? 1 : 0;
                if (context.OrDepth == 0 && arguments[0] == 1 && !HasUnownedCity()) _blocked = true;
                return found == arguments[0] ? 1 : 0;
            }
            if (context.Operation.Code == 336)
            {
                var first = ResolveRulerForce(arguments[0]);
                var second = ResolveRulerForce(arguments[2]);
                int found = 0;
                if (second is not null && Known(_queries.ReadConditionForceValidity(second), "dependence second force validity") &&
                    first is not null && Known(_queries.ReadConditionForceValidity(first), "dependence first force validity"))
                {
                    int playerForce = Known(_queries.ReadConditionCurrentPlayerForcePhysicalSlot(), "dependence current player force");
                    if (playerForce != 0 && playerForce == second.PhysicalSlot)
                        found = Known(_queries.ReadConditionForceDependence(first), "current dependence force") == second.PhysicalSlot ? 1 : 0;
                }
                if (context.OrDepth == 0 && arguments[1] == 0 && found == 1) _blocked = true;
                return found == arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 317)
            {
                var force = ResolveRulerForce(arguments[0]);
                if (arguments[3] < 0) throw new InvalidDataException("Negative state table index is unsupported.");
                var state = _queries.ReadConditionStateTableSlot(arguments[3] < 23 ? arguments[3] : 0)
                    ?? throw new InvalidDataException("Area-count state pointer is unknown.");
                if (force is null || !Known(_queries.ReadConditionForceValidity(force), "area-count force validity") ||
                    !Known(_queries.ReadConditionStateValidity(state), "area-count state validity")) return 0;
                if (state.PhysicalSlot is not int physical) throw new InvalidDataException("Valid area-count state receiver is null.");
                int count = Known(_queries.ReadConditionForceStateAreaCount(force, physical), "current force state area count");
                return Compare(count, arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code == 300)
            {
                var first = ResolveRulerForce(arguments[0]);
                var second = ResolveRulerForce(arguments[2]);
                if (first is null || !Known(_queries.ReadConditionForceValidity(first), "battle first force validity") ||
                    second is null || !Known(_queries.ReadConditionForceValidity(second), "battle second force validity")) return 0;
                var pairs = _queries.ReadConditionCombatEngagements()
                    ?? throw new InvalidDataException("Current generated mixed combat engagements are unknown.");
                int found = 0;
                foreach (var pair in pairs)
                {
                    if (pair.Actor.Kind == CombatParticipantKind.FieldObject || pair.Target.Kind == CombatParticipantKind.FieldObject) continue;
                    var actor = Pointer(pair.Actor.ForceTableIndex);
                    var target = Pointer(pair.Target.ForceTableIndex);
                    if (actor == first && target == second || actor == second && target == first) { found = 1; break; }
                }
                return found == arguments[1] ? 1 : 0;

                S14EventConditionForceReference? Pointer(int index)
                {
                    if (index < 0) throw new InvalidDataException("Negative generated combat Force index is unsupported.");
                    return (_queries.ReadConditionForceTableSlot(index < 52 ? index : 0)
                        ?? throw new InvalidDataException("Current combat Force table pointer is unknown.")).Value;
                }
            }
            if (context.Operation.Code == 12)
            {
                int index = arguments[0];
                if (index <= 0) return 0;
                if (index > 32) return arguments[1] == 0 ? 1 : 0;
                uint flags = Known(_queries.ReadConditionEventAchievementFlags(), "current campaign event achievement flags");
                int bit = (int)((flags >> (index - 1)) & 1u);
                return bit == arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 65)
            {
                int mask = arguments[0] switch { 5006 => 1, 5008 => 2, _ => 0 };
                return mask != 0 && (Known(_queries.ReadConditionSpecialPersonEncounterFlags(), "current special person encounter flags") & mask) != 0 ? 1 : 0;
            }
            if (context.Operation.Code == 246)
            {
                int city = ResolveCity(arguments[0]);
                var cityReference = new BaseReference(BaseKind.City, city);
                if (!Known(_queries.ReadConditionBaseValidity(cityReference), "domestic level city validity")) return 0;
                var area = _queries.ReadConditionBaseArea(cityReference) ?? throw new InvalidDataException("Domestic level city area is unknown.");
                if (!Known(_queries.ReadConditionAreaValidity(area), "domestic level area validity")) return 0;
                int commerce = Known(_queries.ReadConditionAreaDevelopmentLevel(area, CeAreaDevelopmentField.Commerce), "current commerce level");
                int agriculture = Known(_queries.ReadConditionAreaDevelopmentLevel(area, CeAreaDevelopmentField.Agriculture), "current agriculture level");
                int barracks = Known(_queries.ReadConditionAreaDevelopmentLevel(area, CeAreaDevelopmentField.Barracks), "current barracks level");
                return commerce + agriculture + barracks >= arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 21)
            {
                var force = ResolveRulerForce(arguments[0]);
                int count = force is null || !Known(_queries.ReadConditionForceValidity(force), "complete-state force validity")
                    ? 0 : Known(_queries.ReadConditionForceCompleteStateCount(force), "current complete-state count");
                return Compare(count, arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code == 255)
            {
                var force = ResolveRulerForce(arguments[0]);
                if (force is null || !Known(_queries.ReadConditionForceValidity(force), "territory-percent force validity")) return 0;
                float percent = Known(_queries.ReadConditionForceTerritoryPercent(force), "current force territory percent");
                float threshold = (float)arguments[2] * 0.1f;
                return (arguments[1] switch
                {
                    1 => percent != threshold, 2 => percent == threshold, 3 => percent > threshold,
                    4 => percent >= threshold, 5 => percent < threshold, 6 => percent <= threshold, _ => false
                }) ? 1 : 0;
            }
            if (context.Operation.Code == 73)
            {
                var person = RawPersonPointer(arguments[0]);
                if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "imperial-court person validity")) return 0;
                int value = Known(_queries.ReadConditionPersonImperialCourtTendency(person), "current imperial-court tendency");
                return Compare(value, arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code == 264)
            {
                var force = ResolveRulerForce(arguments[2]);
                int index = arguments[0];
                if (index < 0) throw new InvalidDataException("Negative area table index is unsupported.");
                var area = _queries.ReadConditionAreaTableSlot(index < 501 ? index : 0)
                    ?? throw new InvalidDataException("Current condition area table pointer is unknown.");
                if (!Known(_queries.ReadConditionAreaValidity(area), "area-force area validity") || force is null) return 0;
                int owner = Known(_queries.ReadConditionAreaForceSlot(area), "current representative hex force");
                return Compare(owner, arguments[1], force.PhysicalSlot) ? 1 : 0;
            }
            if (context.Operation.Code == 265)
            {
                var first = ResolveRulerForce(arguments[0]);
                var second = ResolveRulerForce(arguments[2]);
                if (first is null || !Known(_queries.ReadConditionForceValidity(first), "city-count first force validity") ||
                    second is null || !Known(_queries.ReadConditionForceValidity(second), "city-count second force validity")) return 0;
                int firstCount = Known(_queries.ReadConditionForceCityCount(first), "first current force city count");
                int secondCount = Known(_queries.ReadConditionForceCityCount(second), "second current force city count");
                return Compare(firstCount, arguments[1], secondCount) ? 1 : 0;
            }
            if (context.Operation.Code == 28)
            {
                var force = ResolveRulerForce(arguments[0]) ?? throw new InvalidDataException("Illness-count force pointer is null.");
                return Compare(Known(_queries.ReadConditionForceIllnessCount(force), "current force illness count"), arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code == 29)
            {
                var force = ResolveRulerForce(arguments[0]) ?? throw new InvalidDataException("Lifespan-count force pointer is null.");
                return Compare(Known(_queries.ReadConditionForceLifespanCount(force), "current force lifespan count"), arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code == 23)
            {
                var force = ResolveRulerForce(arguments[0]) ?? throw new InvalidDataException("Troop-count force pointer is null.");
                return Compare(Known(_queries.ReadConditionForceReadyTroops(force), "current force ready troops"), arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code == 30)
            {
                var force = ResolveRulerForce(arguments[0]) ?? throw new InvalidDataException("Kingdom condition force pointer is null.");
                int raw = Known(_queries.ReadConditionForceKingdomNameIndex(force), "current kingdom name index");
                return (raw == arguments[1] ? 1 : 0) == arguments[2] ? 1 : 0;
            }
            if (context.Operation.Code == 39)
            {
                var force = ResolveRulerForce(arguments[0]);
                if (force is null || !Known(_queries.ReadConditionForceValidity(force), "person-count force validity")) return 0;
                return Compare(Known(_queries.ReadConditionForcePersonCount(force), "current registered force person count"), arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code == 18) return EmperorForce(context, arguments[0], arguments[1]) ? 1 : 0;
            if (context.Operation.Code == 25) return ForceAlliance(arguments[0], arguments[1], arguments[2]) ? 1 : 0;
            if (context.Operation.Code == 34) return SelectForceByBloodline(arguments[0], arguments[1]) ? 1 : 0;
            if (context.Operation.Code == 47) return SelectNorthernCampaignTarget(arguments[0], arguments[1]) ? 1 : 0;
            if (context.Operation.Code == 243) return SelectPersonForce(arguments[0], arguments[1]) ? 1 : 0;
            if (context.Operation.Code == 38)
            {
                var force = ResolveRulerForce(arguments[0]);
                if (arguments[3] < 0) throw new InvalidDataException("Negative state table index is unsupported.");
                var state = _queries.ReadConditionStateTableSlot(arguments[3] < 23 ? arguments[3] : 0)
                    ?? throw new InvalidDataException("Current state table pointer is unknown.");
                if (force is null || !Known(_queries.ReadConditionForceValidity(force), "state city-count force validity") ||
                    !Known(_queries.ReadConditionStateValidity(state), "current state validity")) return 0;
                if (state.PhysicalSlot is not int physical) throw new InvalidDataException("Valid state receiver is null.");
                int count = Known(_queries.ReadConditionForceStateCityCount(force, physical), "current force state city count");
                return Compare(count, arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code == 58)
            {
                var first = RawPersonPointer(arguments[0]) ?? RawPersonPointer(0)
                    ?? throw new InvalidDataException("First person-force receiver is null.");
                var second = RawPersonPointer(arguments[2]) ?? RawPersonPointer(0)
                    ?? throw new InvalidDataException("Second person-force receiver is null.");
                int secondForce = Known(_queries.ReadConditionPersonRawForceSlot(second), "second person district force byte");
                int firstForce = Known(_queries.ReadConditionPersonRawForceSlot(first), "first person district force byte");
                bool result = Compare(firstForce, arguments[1], secondForce);
                if (Known(_queries.ReadConditionPersonStatus(first), "first person death status") == 9 ||
                    Known(_queries.ReadConditionPersonStatus(second), "second person death status") == 9) _blocked = true;
                return result ? 1 : 0;
            }
            if (context.Operation.Code == 37)
            {
                var first = ResolveRulerForce(arguments[0]);
                var second = ResolveRulerForce(arguments[2]);
                if (first is null || !Known(_queries.ReadConditionForceValidity(first), "city-count first force validity") ||
                    second is null || !Known(_queries.ReadConditionForceValidity(second), "city-count second force validity")) return 0;
                int firstCount = Known(_queries.ReadConditionForceCityCount(first), "first force city count");
                int secondCount = Known(_queries.ReadConditionForceCityCount(second), "second force city count");
                return Compare(firstCount, arguments[1], unchecked(secondCount * arguments[3])) ? 1 : 0;
            }
            if (context.Operation.Code == 33) return PrisonerCustodian(arguments[0], arguments[1]) ? 1 : 0;
            if (context.Operation.Code == 78)
            {
                var first = RawPersonPointer(arguments[0]) ?? RawPersonPointer(0);
                var second = RawPersonPointer(arguments[2]) ?? RawPersonPointer(0);
                if (first is null || !Known(_queries.ReadConditionPersonValidity(first), "partner first person validity") ||
                    second is null || !Known(_queries.ReadConditionPersonValidity(second), "partner second person validity")) return 0;
                return Compare(Known(_queries.ReadConditionPersonPartnerId(first), "current partner ID"), arguments[1],
                    Known(_queries.ReadConditionPersonCharacterId(second), "partner target character ID")) ? 1 : 0;
            }
            if (context.Operation.Code == 42) return LordHomeSecurity(arguments[0], arguments[1], arguments[2]) ? 1 : 0;
            if (context.Operation.Code == 22)
            {
                var force = ResolveRulerForce(arguments[0]) ?? throw new InvalidDataException("Region city-count force pointer is null.");
                int count = Known(_queries.ReadConditionForceRegionCityCount(force, arguments[3]), "current force region city count");
                return Compare(count, arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code == 24)
            {
                var force = ResolveRulerForce(arguments[0]) ?? throw new InvalidDataException("Rank force pointer is null.");
                int rank = Known(_queries.ReadConditionForceRank(force), "current force rank");
                bool result = Known(_queries.ReadConditionForceValidity(force), "rank force validity") && Compare(rank, arguments[1], arguments[2]);
                bool blocked = arguments[1] switch
                {
                    1 => arguments[2] == 1 && rank == 1,
                    2 or 4 => rank < arguments[2],
                    3 => rank <= arguments[2],
                    _ => false
                };
                if (context.OrDepth == 0 && rank != 0 && blocked) _blocked = true;
                return result ? 1 : 0;
            }
            if (context.Operation.Code == 68)
            {
                if (arguments[0] < 0) throw new InvalidDataException("Negative item table selectors are unsupported.");
                var item = _queries.ReadConditionItemTableSlot(arguments[0] < 202 ? arguments[0] : 0)
                    ?? throw new InvalidDataException("Item table pointer is unknown.");
                var person = RawPersonPointer(arguments[1]) ?? RawPersonPointer(0);
                if (!Known(_queries.ReadConditionItemValidity(item), "item validity") || person is null ||
                    !Known(_queries.ReadConditionPersonValidity(person), "item target person validity")) return 0;
                ushort ownerId = Known(_queries.ReadConditionItemOwnerId(item), "current item owner ID");
                var owner = ownerId <= 6000 ? _queries.ReadConditionPersonTableSlot(ownerId) : _queries.ReadConditionBasePerson();
                if (owner is null) throw new InvalidDataException("Current item owner pointer is unknown.");
                return owner.Value == person ? 1 : 0;
            }
            if (context.Operation.Code == 32)
            {
                if (arguments[0] is < 1 or > 100) return 0;
                var forces = _queries.ReadRegisteredConditionForcesInNativeOrder()
                    ?? throw new InvalidDataException("Kingdom-name force registry is unknown.");
                bool found = forces.Any(force => Known(_queries.ReadConditionForceKingdomNameIndex(force), "current kingdom name index") == arguments[0]);
                return (found ? 1 : 0) == arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 41)
            {
                if (arguments[0] is < 1 or > 12) return 0;
                var forces = _queries.ReadRegisteredConditionForcesInNativeOrder()
                    ?? throw new InvalidDataException("Rank-count force registry is unknown.");
                int count = 0;
                foreach (var force in forces)
                    if (Known(_queries.ReadConditionForceRank(force), "current registered force rank") == arguments[0])
                        count = unchecked(count + 1);
                return Compare(count, arguments[1], arguments[2]) ? 1 : 0;
            }
            if (context.Operation.Code == 35)
            {
                var force = ResolveRulerForce(arguments[0]);
                var person = RawPersonPointer(arguments[1]) ?? RawPersonPointer(0);
                if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "strategist person validity") ||
                    force is null || !Known(_queries.ReadConditionForceValidity(force), "strategist force validity")) return 0;
                ushort id = Known(_queries.ReadConditionForceAdvisorId(force), "current force strategist ID");
                var advisor = id <= 6000 ? _queries.ReadConditionPersonTableSlot(id) : _queries.ReadConditionBasePerson();
                if (advisor is null) throw new InvalidDataException("Current strategist pointer is unknown.");
                return (advisor.Value == person ? 1 : 0) == arguments[2] ? 1 : 0;
            }
            if (context.Operation.Code == 26)
            {
                var first = ResolveRulerForce(arguments[0]);
                var second = ResolveRulerForce(arguments[1]);
                if (first is null || !Known(_queries.ReadConditionForceValidity(first), "friendship first force validity") ||
                    second is null || !Known(_queries.ReadConditionForceValidity(second), "friendship second force validity")) return 0;
                int threshold = arguments[3] < 1 ? 255 : arguments[3] - 1;
                return Compare(Known(_queries.ReadConditionForceFriendshipLevel(first, second), "current friendship level"), arguments[2], threshold) ? 1 : 0;
            }
            if (context.Operation.Code == 27)
            {
                var first = ResolveRulerForce(arguments[0]) ?? throw new InvalidDataException("Adjacent first force pointer is null.");
                var second = ResolveRulerForce(arguments[2]) ?? throw new InvalidDataException("Adjacent second force pointer is null.");
                int adjacent = Known(_queries.ReadConditionForceAdjacent(first, second), "current force adjacency") ? 1 : 0;
                return adjacent == arguments[1] ? 1 : 0;
            }
            if (context.Operation.Code == 40)
            {
                var forces = _queries.ReadRegisteredConditionForcesInNativeOrder()
                    ?? throw new InvalidDataException("Registered force list is unknown.");
                // Source excludes identities46..51 before its tribal test;
                // that test is always false for all remaining identities.
                int count = forces.Count(force => force.PhysicalSlot is < 46 or > 51);
                return Compare(count, arguments[0], arguments[1]) ? 1 : 0;
            }
            if (context.Operation.Code == 50) return CityForce(arguments[0], arguments[1], arguments[2]) ? 1 : 0;
            if (context.Operation.Code == 52) return CitySecurity(arguments[0], arguments[1], arguments[2]) ? 1 : 0;
            if (context.Operation.Code == 55)
            {
                int city = ResolveCity(arguments[0]);
                if (!Known(_queries.ReadConditionBaseValidity(new(BaseKind.City, city)), "city level validity")) return 0;
                int level = Known(_queries.ReadConditionCityLevel(city), "current city level");
                bool result = Compare(level, arguments[1], arguments[2]);
                if (arguments[1] == 2 && level > arguments[2]) _blocked = true;
                return result ? 1 : 0;
            }
            if (context.Operation.Code == 56) return PersonForce(context, arguments[0], arguments[1], arguments[2]) ? 1 : 0;
            if (context.Operation.Code == 57) return PersonHomeCity(arguments[0], arguments[1], arguments[2]) ? 1 : 0;
            if (context.Operation.Code == 48) return SelectedForce(arguments[0], arguments[1]) ? 1 : 0;
            if (context.Operation.Code == 49) return BaseForce(arguments[0], arguments[1], arguments[2]) ? 1 : 0;
            if (context.Operation.Code == 74) return PersonWhereabouts(arguments[0], arguments[1], arguments[2]) ? 1 : 0;
            if (context.Operation.Code == 225) return FilterSelectedForce(arguments[0]) ? 1 : 0;
            if (context.Operation.Code == 229)
            {
                // This pre-filter block test is independent of OR depth.
                if (!_selectedPersons.Any(person => (Known(_queries.ReadConditionPersonFlags(person), "selected person flags") & 0x80) == 0 &&
                    Known(_queries.ReadConditionPersonStatus(person), "selected person status") != 9)) _blocked = true;
                bool wanted = arguments[0] != 0;
                _selectedPersons.RemoveAll(person => Known(_queries.ReadConditionPersonHumble(person), "selected person humble state") != wanted);
                return _selectedPersons.Count != 0 ? 1 : 0;
            }
            if (context.Operation.Code == 231)
            {
                bool wanted = arguments[0] != 0;
                _selectedPersons.RemoveAll(person =>
                    (Known(_queries.ReadConditionPersonStatus(person), "selected person status") is not (6 or 7 or 9)) != wanted);
                return _selectedPersons.Count != 0 ? 1 : 0;
            }
            if (context.Operation.Code == 20)
            {
                var target = ResolveRulerForce(arguments[0]) ?? throw new InvalidDataException("City-count force pointer is null.");
                return Compare(Known(_queries.ReadConditionForceCityCount(target), "force city count"), arguments[1], arguments[2]) ? 1 : 0;
            }
            int player = CompareUserForce(arguments[1]);
            // This callback compares its actual flag to1 with the source jump
            // table's own direction, which differs from the basic comparer.
            return arguments[0] switch
            {
                1 => player != 1 ? 1 : 0,
                2 => player == 1 ? 1 : 0,
                3 => player < 1 ? 1 : 0,
                4 => player <= 1 ? 1 : 0,
                5 => player > 1 ? 1 : 0,
                6 => player >= 1 ? 1 : 0,
                _ => 0
            };
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Player condition stream {context.Program.StreamIndex}, " +
                $"operation {context.Operation.Index}, code {context.Operation.Code}, offset {context.Operation.Offset}: {error.Message}", error);
        }
    }

    public void PreparePersonListGroup(S14ConditionContext context)
    {
        if (context.Operation.Code != 216 || context.Operation.RawArguments.Count != 8)
            throw new InvalidDataException("Invalid person-list condition group.");
        foreach (int raw in context.Operation.RawArguments.Take(5))
        {
            if (raw < 0) throw new InvalidDataException($"Dynamic person selector {raw} is unsupported.");
            var person = RawPersonPointer(raw) ?? RawPersonPointer(0);
            if (person is not null && Known(_queries.ReadConditionPersonValidity(person), "selected person validity"))
                _selectedPersons.Add(person);
        }
        if (!_selectedPersons.Any(person => Known(_queries.ReadConditionPersonStatus(person), "selected person status") != 9))
            _blocked = true;
    }

    public void PrepareCityListGroup(S14ConditionContext context)
    {
        if (context.Operation.Code != 372 || context.Operation.RawArguments.Count != 8)
            throw new InvalidDataException("Invalid city-list condition group.");
        foreach (int selector in context.Operation.RawArguments)
        {
            var city = ResolveCityPointer(selector);
            if (BaseValid(new(BaseKind.City, city.PhysicalSlot)) && !_selectedCities.Contains(city))
                _selectedCities.Add(city);
        }
        // The source rereads validity after appending. A previous group's city
        // remains in this event's list even when its current validity changes.
        if (!_selectedCities.Any(city => BaseValid(new(BaseKind.City, city.PhysicalSlot))))
            _blocked = true;
    }

    private bool CompareCityListOwnership(int mode)
    {
        if (mode is not (0 or 1)) return false;
        foreach (var city in _selectedCities)
        {
            int owner = Known(_queries.ReadConditionCityForceSlot(city.PhysicalSlot!.Value), "city-list owner");
            if (mode == 0 ? owner != 0 : owner == 0) return true;
        }
        return false;
    }

    private bool CompareCityListForce(int mode, int excludeRulerCity, int selector)
    {
        var target = ResolveRulerForce(selector);
        var rulerCity = new S14EventConditionCityPointer(null);
        if (target is not null && Known(_queries.ReadConditionForceValidity(target), "city-list target validity"))
        {
            ushort rulerId = Known(_queries.ReadConditionForceRulerPersonId(target), "city-list ruler");
            var ruler = RawPersonPointer(rulerId) ?? throw new InvalidDataException("City-list ruler pointer is null.");
            var home = _queries.ReadConditionPersonHomeBase(ruler) ?? throw new InvalidDataException("City-list ruler home is unknown.");
            if (BaseValid(home) && Known(_queries.ReadConditionBaseObjectId(home), "city-list home object ID") < 52)
                rulerCity = _queries.ReadConditionBaseMappedCity(home) ?? throw new InvalidDataException("City-list mapped ruler city is unknown.");
        }
        foreach (var city in _selectedCities)
        {
            int owner = Known(_queries.ReadConditionCityForceSlot(city.PhysicalSlot!.Value), "city-list owner");
            if (owner == 0 || (excludeRulerCity == 1 && city == rulerCity) || mode is not (0 or 1)) continue;
            var actual = _queries.ReadConditionBaseForce(new(BaseKind.City, city.PhysicalSlot))
                ?? throw new InvalidDataException("City-list force pointer is unknown.");
            if (mode == 0 ? actual.Value != target : actual.Value == target) return true;
        }
        return false;
    }

    private bool LordHomeSecurity(int selector, int mode, int threshold)
    {
        var force = ResolveRulerForce(selector);
        if (force is null || !Known(_queries.ReadConditionForceValidity(force), "security force validity")) return false;
        ushort rulerId = Known(_queries.ReadConditionForceRulerPersonId(force), "security force ruler ID");
        var ruler = (rulerId <= 6000 ? _queries.ReadConditionPersonTableSlot(rulerId) : _queries.ReadConditionBasePerson())?.Value
            ?? throw new InvalidDataException("Security ruler pointer is unknown or null.");
        var home = _queries.ReadConditionPersonHomeBase(ruler) ?? throw new InvalidDataException("Security ruler home is unknown.");
        if (!BaseValid(home)) return false;
        var area = _queries.ReadConditionBaseArea(home) ?? throw new InvalidDataException("Security home area pointer is unknown.");
        if (!Known(_queries.ReadConditionAreaValidity(area), "security area validity")) return false;
        return Compare(Known(_queries.ReadConditionAreaSecurity(area), "current home area security"), mode, threshold);
    }

    private bool SelectPersonForce(int rawPerson, int target)
    {
        var person = RawPersonPointer(rawPerson) ?? RawPersonPointer(0);
        if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "target force person validity")) return false;
        var force = (_queries.ReadConditionPersonForce(person) ?? throw new InvalidDataException("Target person's current force pointer is unknown.")).Value;
        if (force is null || !Known(_queries.ReadConditionForceValidity(force), "target person's force validity")) return false;
        StoreTargetForce(target, force);
        return true;
    }

    private bool SelectObstacleTarget(int selector, int target)
    {
        var source = ResolveRulerForce(selector);
        if (source is null || !Known(_queries.ReadConditionForceValidity(source), "obstacle source force validity") || target is < 1 or > 4) return false;
        var registered = _queries.ReadRegisteredConditionForcesInNativeOrder()
            ?? throw new InvalidDataException("Obstacle registered forces are unknown.");
        var candidates = new List<S14EventConditionForceReference>();
        foreach (var force in registered)
            if (force != source && unchecked((uint)Known(_queries.ReadConditionForceFriendshipLevel(force, source), "obstacle candidate friendship")) < 3 &&
                (Known(_queries.ReadConditionForceAllianceByte(force, source.PhysicalSlot), "obstacle candidate alliance") & 0x7F) == 0 &&
                Known(_queries.ReadConditionForceAdjacent(force, source), "obstacle candidate adjacency") &&
                Known(_queries.ReadConditionForceCityCount(force), "obstacle candidate city count") > 4)
                candidates.Add(force);
        S14EventConditionForceReference? selected = candidates.Count == 1 ? candidates[0] : null;
        if (candidates.Count > 1)
        {
            var order = _queries.ReadConditionForceProcessingOrder() ?? throw new InvalidDataException("Current obstacle force processing order is unknown.");
            selected = order.FirstOrDefault(force => force is not null && candidates.Contains(force));
        }
        if (selected is null || !Known(_queries.ReadConditionForceValidity(selected), "obstacle selected force validity")) return false;
        StoreTargetForce(target, selected);
        return true;
    }

    private bool SelectNorthernCampaignTarget(int selector, int target)
    {
        var source = ResolveRulerForce(selector);
        if (source is null || !Known(_queries.ReadConditionForceValidity(source), "campaign source force validity") || target is < 1 or > 4) return false;
        int sourceCities = Known(_queries.ReadConditionForceCityCount(source), "campaign source city count");
        var registered = _queries.ReadRegisteredConditionForcesInNativeOrder()
            ?? throw new InvalidDataException("Campaign registered forces are unknown.");
        var candidates = new List<S14EventConditionForceReference>();
        foreach (var force in registered)
        {
            if (Known(_queries.ReadConditionForceRank(force), "campaign candidate rank") != 1 ||
                Known(_queries.ReadConditionForceCityCount(force), "campaign candidate city count") <= sourceCities) continue;
            bool ownsCities = true;
            foreach (int slot in new[] { 18, 6, 16 })
            {
                var city = _queries.ReadConditionCityTableSlot(slot) ?? throw new InvalidDataException("Campaign required city pointer is unknown.");
                var cityBase = new BaseReference(BaseKind.City, city.PhysicalSlot);
                if (!Known(_queries.ReadConditionBaseValidity(cityBase), "campaign required city validity")) continue;
                var owner = _queries.ReadConditionBaseForce(cityBase) ?? throw new InvalidDataException("Campaign required city force is unknown.");
                if (owner.Value != force) { ownsCities = false; break; }
            }
            if (!ownsCities || Known(_queries.ReadConditionForceFriendshipLevel(force, source), "campaign candidate friendship") >= 3 ||
                (Known(_queries.ReadConditionForceAllianceByte(force, source.PhysicalSlot), "campaign candidate alliance") & 0x7F) != 0 ||
                !Known(_queries.ReadConditionForceAdjacent(force, source), "campaign candidate adjacency")) continue;
            candidates.Add(force);
        }
        S14EventConditionForceReference? selected = candidates.Count == 1 ? candidates[0] : null;
        if (candidates.Count > 1)
        {
            var order = _queries.ReadConditionForceProcessingOrder() ?? throw new InvalidDataException("Current saved force processing order is unknown.");
            selected = order.FirstOrDefault(force => force is not null && candidates.Contains(force));
        }
        if (selected is null || !Known(_queries.ReadConditionForceValidity(selected), "campaign selected force validity")) return false;
        StoreTargetForce(target, selected);
        return true;
    }

    private void StoreTargetForce(int target, S14EventConditionForceReference? force)
    {
        uint slot = unchecked((uint)(5 - target));
        if (slot < _targetForces.Length) _targetForces[slot] = force;
    }

    // Execution actions read the same invocation-local selected-force slots
    // that the condition handlers have actually written, including reset nulls.
    internal S14EventConditionForceReference? ReadMessageTargetForce(int selector) => selector is >= -5 and <= -2
        ? _targetForces[-selector - 1] : throw new ArgumentOutOfRangeException(nameof(selector));

    private bool SelectForceByBloodline(int rawPerson, int target)
    {
        var person = RawPersonPointer(rawPerson) ?? RawPersonPointer(0);
        S14EventConditionForceReference? selected = null;
        if (person is not null && Known(_queries.ReadConditionPersonValidity(person), "bloodline target person validity"))
        {
            ushort identity = Known(_queries.ReadConditionPersonCharacterId(person), "bloodline target person ID");
            var forces = _queries.ReadRegisteredConditionForcesInNativeOrder()
                ?? throw new InvalidDataException("Bloodline registered forces are unknown.");
            foreach (var force in forces)
            {
                ushort rulerId = Known(_queries.ReadConditionForceRulerPersonId(force), "bloodline force ruler ID");
                var rulerLookup = rulerId <= 6000 ? _queries.ReadConditionPersonTableSlot(rulerId) : _queries.ReadConditionBasePerson();
                if (rulerLookup is null) throw new InvalidDataException("Bloodline ruler pointer is unknown.");
                var ruler = rulerLookup.Value;
                if (ruler is null || !Known(_queries.ReadConditionPersonValidity(ruler), "bloodline ruler validity")) continue;
                if (Known(_queries.ReadConditionPersonBloodlineId(ruler), "current ruler bloodline ID") != identity) continue;
                if (selected is not null) { selected = null; break; }
                selected = force;
            }
        }
        if (selected is null || !Known(_queries.ReadConditionForceValidity(selected), "bloodline selected force validity")) return false;
        StoreTargetForce(target, selected);
        return true;
    }

    private bool ForceAlliance(int firstSelector, int expected, int secondSelector)
    {
        var first = ResolveRulerForce(firstSelector);
        var second = ResolveRulerForce(secondSelector);
        int index = second is not null && Known(_queries.ReadConditionForceValidity(second), "alliance second force validity")
            ? second.PhysicalSlot : 0;
        int allied = 0;
        if (first is not null && Known(_queries.ReadConditionForceValidity(first), "alliance first force validity"))
            allied = (Known(_queries.ReadConditionForceAllianceByte(first, index), "current alliance byte") & 0x7F) != 0 ? 1 : 0;
        return allied == expected;
    }

    private bool FilterSelectedForce(int selector)
    {
        var target = ResolveRulerForce(selector);
        if (target is not null && Known(_queries.ReadConditionForceValidity(target), "selected target force validity"))
            _selectedPersons.RemoveAll(person =>
            {
                var actual = _queries.ReadConditionPersonForce(person)
                    ?? throw new InvalidDataException("Selected person force pointer is unknown.");
                return actual.Value?.PhysicalSlot != target.PhysicalSlot;
            });
        return _selectedPersons.Count != 0;
    }

    private bool EmperorForce(S14ConditionContext context, int mode, int selector)
    {
        var target = ResolveRulerForce(selector) ?? throw new InvalidDataException("Emperor target force pointer is null.");
        bool result = Compare(Known(_queries.ReadConditionEmperorForcePhysicalSlot(), "emperor city force identity"), mode, target.PhysicalSlot);
        bool invalidEmperor = mode == 2 && Known(_queries.ReadConditionEmperorPersonId(), "emperor ID") is not (>= 1 and <= 6000);
        if (context.OrDepth == 0 && invalidEmperor) _blocked = true;
        return result;
    }

    private bool CityForce(int rawCity, int mode, int selector)
    {
        if (rawCity < 0) throw new InvalidDataException($"Dynamic city selector {rawCity} is unsupported.");
        var city = _queries.ReadConditionCityTableSlot(rawCity < 52 ? rawCity : 0)
            ?? throw new InvalidDataException("City selector pointer is unknown.");
        if (city.PhysicalSlot is null) city = _queries.ReadConditionCityTableSlot(0)
            ?? throw new InvalidDataException("City zero pointer is unknown.");
        var force = ResolveRulerForce(selector);
        if (force is null || !Known(_queries.ReadConditionForceValidity(force), "target force validity")) return false;
        if (city.PhysicalSlot is not int physical) throw new InvalidDataException("City force receiver is null.");
        return Compare(Known(_queries.ReadConditionCityForceSlot(physical), "city force slot"), mode, force.PhysicalSlot);
    }

    private bool CitySecurity(int rawCity, int mode, int threshold)
    {
        int physical = ResolveCity(rawCity);
        var area = _queries.ReadConditionBaseArea(new(BaseKind.City, physical))
            ?? throw new InvalidDataException("Current security city area is unknown.");
        // This leaf reads the area's byte even when City/Area native validity is false.
        return Compare(Known(_queries.ReadConditionAreaSecurity(area), "current city area security"), mode, threshold);
    }

    private SceneryReference ResolveScenery(int index)
    {
        if (index < 0) throw new InvalidDataException("Negative raw scenery index is unsupported.");
        var scenery = _queries.ReadConditionSceneryTableSlot(index < 101 ? index : 0)
            ?? throw new InvalidDataException("Current scenery pointer is unknown.");
        if (!Known(_queries.ReadConditionSceneryValidity(scenery), "selected scenery validity"))
            scenery = _queries.ReadConditionSceneryTableSlot(0)
                ?? throw new InvalidDataException("Current scenery zero fallback is unknown.");
        return scenery;
    }

    private int ResolveCity(int rawCity) => ResolveCityPointer(rawCity).PhysicalSlot
        ?? throw new InvalidDataException("Current city receiver is null.");

    private S14EventConditionCityPointer ResolveCityPointer(int rawCity)
    {
        if (_storyCityResolver is { } resolve) return resolve(rawCity);
        if (rawCity < -1) throw new InvalidDataException($"Dynamic city selector {rawCity} is unsupported.");
        var city = (rawCity == -1 ? _queries.ReadConditionCurrentPlayerCity() : _queries.ReadConditionCityTableSlot(rawCity < 52 ? rawCity : 0))
            ?? throw new InvalidDataException("Current city selector is unknown.");
        if (city.PhysicalSlot is null) city = _queries.ReadConditionCityTableSlot(0)
            ?? throw new InvalidDataException("Current city zero pointer is unknown.");
        return city;
    }

    private bool PrisonerCustodian(int rawPerson, int selector)
    {
        var person = RawPersonPointer(rawPerson) ?? RawPersonPointer(0);
        var target = ResolveRulerForce(selector);
        if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "prisoner validity") ||
            target is null || !Known(_queries.ReadConditionForceValidity(target), "custodian target validity") ||
            Known(_queries.ReadConditionPersonStatus(person), "prisoner status") != 6) return false;
        var custodian = _queries.ReadConditionPrisonerCustodianForce(person)
            ?? throw new InvalidDataException("Current prisoner custodian force is unknown.");
        return custodian.Value == target;
    }

    private bool PersonForce(S14ConditionContext context, int rawPerson, int mode, int selector)
    {
        var person = RawPersonPointer(rawPerson) ?? RawPersonPointer(0);
        var force = ResolveRulerForce(selector);
        if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "person validity") ||
            force is null || !Known(_queries.ReadConditionForceValidity(force), "force validity")) return false;
        bool result = Compare(Known(_queries.ReadConditionPersonRawForceSlot(person), "person district force byte"), mode, force.PhysicalSlot);
        if (context.OrDepth == 0 && Known(_queries.ReadConditionPersonStatus(person), "person status") == 9) _blocked = true;
        return result;
    }

    private static bool Compare(int left, int mode, int right) => mode switch
    { 1 => left != right, 2 => left == right, 3 => left > right, 4 => left >= right, 5 => left < right, 6 => left <= right, _ => false };

    private bool SelectedForce(int mode, int selector)
    {
        var target = ResolveRulerForce(selector) ?? throw new InvalidDataException("Selected-force target pointer is null.");
        return Compare(Known(_queries.ReadConditionSelectedForce(), "selected force byte"), mode, target.PhysicalSlot);
    }

    private BaseReference ResolveBase(int selector)
    {
        if (selector < 0) throw new InvalidDataException($"Dynamic base selector {selector} is unsupported.");
        return _queries.ReadConditionBaseSelector(selector) ?? throw new InvalidDataException("Base selector is unknown.");
    }

    private bool BaseValid(BaseReference reference) => reference.PhysicalSlot is not null &&
        Known(_queries.ReadConditionBaseValidity(reference), "base validity");

    private bool BaseForce(int rawBase, int mode, int selector)
    {
        var receiver = ResolveBase(rawBase);
        var target = ResolveRulerForce(selector);
        if (selector != 0)
        {
            bool valid = target is not null && Known(_queries.ReadConditionForceValidity(target), "target force validity");
            if (!valid && mode == 2) return false;
        }
        if (!BaseValid(receiver) || target is null) return false;
        var owner = _queries.ReadConditionBaseForce(receiver) ?? throw new InvalidDataException("Base force pointer is unknown.");
        return owner.Value is { } actual && Compare(actual.PhysicalSlot, mode, target.PhysicalSlot);
    }

    private bool PersonWhereabouts(int rawPerson, int mode, int rawBase)
    {
        var person = RawPersonPointer(rawPerson) ?? RawPersonPointer(0);
        var target = ResolveBase(rawBase);
        if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "whereabouts person validity") || !BaseValid(target)) return false;
        var actual = _queries.ReadConditionPersonLocationBase(person) ?? throw new InvalidDataException("Person whereabouts base is unknown.");
        if (!BaseValid(actual)) return false;
        return Compare(Known(_queries.ReadConditionBaseObjectId(actual), "person whereabouts base object ID"), mode,
            Known(_queries.ReadConditionBaseObjectId(target), "target base object ID"));
    }

    private bool PersonHomeCity(int rawPerson, int mode, int rawCity)
    {
        var person = RawPersonPointer(rawPerson) ?? RawPersonPointer(0);
        if (rawCity < 0) throw new InvalidDataException($"Dynamic home city selector {rawCity} is unsupported.");
        var city = _queries.ReadConditionCityTableSlot(rawCity < 52 ? rawCity : 0)
            ?? throw new InvalidDataException("Home-city target pointer is unknown.");
        if (city.PhysicalSlot is null) city = _queries.ReadConditionCityTableSlot(0)
            ?? throw new InvalidDataException("Home-city zero fallback pointer is unknown.");
        var target = new BaseReference(BaseKind.City, city.PhysicalSlot);
        if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "home-city person validity") || !BaseValid(target)) return false;
        var home = _queries.ReadConditionPersonHomeBase(person) ?? throw new InvalidDataException("Current person home base is unknown.");
        bool result = false;
        if (BaseValid(home) && Known(_queries.ReadConditionBaseObjectId(home), "home base object ID") < 52)
        {
            if (home.Kind != BaseKind.City) throw new InvalidDataException("Home base is not a city receiver.");
            result = Compare(Known(home.PhysicalSlot, "home city identity"), mode, Known(city.PhysicalSlot, "target city identity"));
        }
        if (Known(_queries.ReadConditionPersonStatus(person), "home-city person status") == 9) _blocked = true;
        return result;
    }

    private int CompareUserForce(int rawPerson)
    {
        S14EventConditionForceReference? force = ResolveRulerForce(rawPerson);
        if (force is not null && Known(_queries.ReadConditionForceValidity(force), "native force validity"))
        {
            int currentPlayerSlot = Known(_queries.ReadConditionCurrentPlayerForcePhysicalSlot(),
                "world player force physical slot");
            if (currentPlayerSlot != 0 && currentPlayerSlot == force.PhysicalSlot) return 1;
        }

        bool? application = _queries.ReadConditionApplicationPresent();
        if (application == false) return 0;
        if (Known(_queries.ReadConditionCurrentUserIndex(), "signed world+BC user index") == -1) return 0;
        // Both original getters are pure. A known -1 makes the conjunction
        // false even when this application adapter has no playback service.
        _ = Known(application, "application pointer");

        // Negative selectors here are raw manager-table indices, not the
        // ordinary event person selectors. Only the preceding proved branches
        // can consume them without this unrepresented native-layout fallback.
        if (rawPerson < 0) throw new InvalidDataException($"Raw negative application user-force selector {rawPerson} is unsupported.");
        // This branch rereads the raw selector's pointer without identity or
        // force-selector null fallback, and does not test person validity.
        S14EventConditionPersonReference? person = RawPersonPointer(rawPerson);
        if (person is null) return 0;
        ushort characterId = Known(_queries.ReadConditionPersonCharacterId(person), "person+10 character ID");
        int index = Known(_queries.ReadConditionCurrentUserIndex(), "signed world+BC user index reread");
        if (index < 0) throw new InvalidDataException($"Negative selected-user index {index} is outside the supported query window.");
        int selected = Known(_queries.ReadConditionSelectedUserPersonId(index), "signed world+64 selected character ID");
        return characterId == selected ? 1 : 0;
    }

    private S14EventConditionForceReference? ResolveRulerForce(int rawPerson)
    {
        if (rawPerson is >= -5 and <= -2)
            return _targetForces[-rawPerson - 1] ?? (_queries.ReadConditionForceTableSlot(0)
                ?? throw new InvalidDataException("Event target force0 fallback is unknown.")).Value;
        if (rawPerson == -1)
        {
            var player = _queries.ReadConditionCurrentPlayerForce()
                ?? throw new InvalidDataException("Current player force pointer is unknown.");
            return player.Value ?? (_queries.ReadConditionForceTableSlot(0)
                ?? throw new InvalidDataException("Current force0 fallback is unknown.")).Value;
        }
        if (rawPerson < 0) throw new InvalidDataException($"Dynamic person selector {rawPerson} is unsupported.");
        S14EventConditionPersonReference? person = RawPersonPointer(rawPerson);
        if (person is not null && Known(_queries.ReadConditionPersonValidity(person), "native person validity") &&
            Known(_queries.ReadConditionPersonStatus(person), "person+11E status") == 1)
        {
            S14EventConditionForcePointer lookup = _queries.ReadConditionPersonForce(person)
                ?? throw new InvalidDataException("Current native person-force getter is unknown.");
            if (lookup.Value is not null) return CheckForce(lookup.Value);
        }
        S14EventConditionForcePointer fallback = _queries.ReadConditionForceTableSlot(0)
            ?? throw new InvalidDataException("Force table slot0 fallback pointer is unknown.");
        return fallback.Value is null ? null : CheckForce(fallback.Value);
    }

    private S14EventConditionPersonReference? RawPersonPointer(int raw)
    {
        if (raw is >= -14 and <= -10)
            return (_storyActorResolver ?? throw new InvalidDataException("Story actor context is unavailable."))(raw);
        if (raw is >= -8 and <= -5)
        {
            // These selectors read the same event-local target forces written
            // by 34/243. They do not run the lazy actor-context producer.
            var force = _targetForces[-raw - 4];
            if (force is null || !Known(_queries.ReadConditionForceValidity(force), "target-person force validity"))
                return RawPersonPointer(0);
            ushort ruler = Known(_queries.ReadConditionForceRulerPersonId(force), "target force current ruler ID");
            return RawPersonPointer(ruler) ?? RawPersonPointer(0);
        }
        if (raw == -4) return (_queries.ReadConditionCurrentPlayerPerson()
            ?? throw new InvalidDataException("Current player ruler pointer is unknown.")).Value;
        if (raw < 0) throw new InvalidDataException($"Dynamic person selector {raw} is unsupported.");
        S14EventConditionPersonPointer lookup = raw <= 6000
            ? _queries.ReadConditionPersonTableSlot(raw)
                ?? throw new InvalidDataException($"Person table slot {raw} is unknown.")
            : _queries.ReadConditionBasePerson()
                ?? throw new InvalidDataException("Base-person pointer is unknown.");
        if (lookup.Value is { PhysicalSlot: < 0 })
            throw new InvalidDataException("Person pointer has a negative physical identity.");
        return lookup.Value;
    }

    private static S14EventConditionForceReference CheckForce(S14EventConditionForceReference force) =>
        force.PhysicalSlot >= 0 ? force : throw new InvalidDataException("Force pointer has a negative physical identity.");

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current {field} is unknown.");
}
