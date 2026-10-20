using San14.Data.Formats;
using San14.Core;

namespace San14.Data;

public sealed partial class SourceWorldContext : IS14PlayerEventConditionQueries
{
    public S14EventConditionCityPointer? ReadConditionCurrentPlayerCity()
    {
        if (ReadConditionCurrentPlayerPerson()?.Value is not { } ruler ||
            ReadConditionPersonHomeBase(ruler) is not { } home ||
            CurrentGateMapQueries.FindCity(home, this) is not { } city) return null;
        return new(city.PhysicalSlot);
    }
    public Func<int, byte?>? ConditionCityLevelReader { get; set; }
    public byte? ReadConditionCityLevel(int physical) => ConditionCityLevelReader?.Invoke(physical);
    public int? ReadConditionForceReadyTroops(S14EventConditionForceReference force)
    {
        if (ReadConditionForceValidity(force) is not bool valid) return null;
        if (!valid) return 0;
        int count = 0;
        if (ReadRegisteredBases() is not { } bases) return null;
        foreach (var entry in bases)
        {
            if (force.PhysicalSlot is not (>= 1 and <= 51)) continue;
            if (entry is null || ReadBaseForceSlot(entry) is not int owner) return null;
            if (owner != force.PhysicalSlot) continue;
            if (ReadBaseResource(entry, BaseResourceKind.Troops) is not int ready) return null;
            count = unchecked(count + ready);
        }
        if (ReadRegistryReferences(RegistryKind.Army) is not { } armies) return null;
        foreach (var entry in armies)
        {
            if (force.PhysicalSlot is not (>= 1 and <= 51)) continue;
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) return null;
            var army = new ArmyUnitReference(physical);
            if (ReadArmyUnitRawForceSlot(army) != force.PhysicalSlot) continue;
            if (ReadOwnedArmyExclusion233560(entry) is not bool excluded) return null;
            if (excluded) continue;
            if (ReadArmyReturnResourceFields(army)?.Ready16 is not ushort ready) return null;
            count = unchecked(count + ready);
        }
        return count;
    }
    public IReadOnlyList<int>? ConditionForceProcessingOrderIds { get; set; }
    public IReadOnlyList<S14EventConditionForceReference?>? ReadConditionForceProcessingOrder()
    {
        if (ConditionForceProcessingOrderIds is not { } ids) return null;
        var result = new List<S14EventConditionForceReference?>(ids.Count);
        foreach (int id in ids)
        {
            if (id < 0) throw new InvalidDataException("Saved force processing order has a negative table index.");
            var force = ReadConditionForceTableSlot(id < 52 ? id : 0);
            if (force is null) return null;
            result.Add(force.Value);
        }
        return result;
    }
    public int? ReadConditionForcePersonCount(S14EventConditionForceReference force) => CountRegisteredForcePersons(force, false);
    public int? ReadConditionForceHealthyHomePersonCount(S14EventConditionForceReference force) =>
        CountRegisteredForcePersons(force, false, healthyHomeOnly: true);
    public S14EventConditionCityPointer? ReadConditionBaseMappedCity(BaseReference home) =>
        new(CurrentGateMapQueries.FindCity(home, this).PhysicalSlot);
    public Func<int, int?>? ConditionCompleteStateCountReader { get; set; }
    public int? ReadConditionForceCompleteStateCount(S14EventConditionForceReference force) => ConditionCompleteStateCountReader?.Invoke(force.PhysicalSlot);
    public int? ReadConditionForceIllnessCount(S14EventConditionForceReference force) => CountRegisteredForcePersons(force, true);
    public int? ReadConditionForceLifespanCount(S14EventConditionForceReference force) => CountRegisteredForcePersons(force, false, true);
    private int? CountRegisteredForcePersons(S14EventConditionForceReference force, bool illnessOnly, bool lifespanOnly = false, bool healthyHomeOnly = false)
    {
        if (force.PhysicalSlot is not (>= 1 and <= 51)) return 0;
        if (ReadRegisteredPersonPhysicalSlots() is not { } persons) return null;
        int count = 0;
        foreach (int? entry in persons)
        {
            if (entry is not int physical) return null;
            if (healthyHomeOnly)
            {
                // The source force-person iterator checks membership before
                // status; unrelated people must not require status knowledge.
                if (ReadPersonOldForceSlot(physical) is not byte owner) return null;
                if (owner != force.PhysicalSlot) continue;
                if (ReadPersonStatus(physical) is not byte status) return null;
                if (status is not (>= 1 and <= 4)) continue;
            }
            else
            {
                if (ReadPersonStatus(physical) is not byte status) return null;
                if (status is not (>= 1 and <= 4)) continue;
                if (ReadPersonOldForceSlot(physical) is not byte owner) return null;
                if (owner != force.PhysicalSlot) continue;
            }
            if (healthyHomeOnly)
            {
                if (ReadConditionPersonValidity(new(physical)) is not bool valid) return null;
                if (!valid) continue;
                if (ReadPersonSecondLocation(physical) is not ushort raw) return null;
                if (PersonPositionRepair.DecodeSecondLocation(raw).Kind is not (5 or 6)) continue;
                if (OfficerHealthSourceAdapter.ReadByte(this, physical) is not byte health) return null;
                if (health != 0) continue;
            }
            if (illnessOnly)
            {
                if (OfficerHealthSourceAdapter.ReadByte(this, physical) is not byte health) return null;
                if (health == 0) continue;
            }
            if (lifespanOnly)
            {
                if (ReadConditionPersonAtTerminalYear(physical) is not bool terminal) return null;
                if (!terminal) continue;
            }
            count = unchecked(count + 1);
        }
        return count;
    }
    private readonly Dictionary<int, int?> _conditionSelectedUserPersons = [];
    public bool? ConditionApplicationPresent { get; set; }
    public int? ConditionCurrentUserIndex { get; set; }
    public byte? ConditionSpecialPersonEncounterFlags { get; set; }
    public byte? ReadConditionSpecialPersonEncounterFlags() => ConditionSpecialPersonEncounterFlags;
    public uint? ConditionEventAchievementFlags { get; set; }
    public uint? ReadConditionEventAchievementFlags() => ConditionEventAchievementFlags;
    public IReadOnlyList<CombatEngagementPair>? ConditionCombatEngagements { get; set; }
    public IReadOnlyList<CombatEngagementPair>? ReadConditionCombatEngagements() => ConditionCombatEngagements;
    public Func<int, ushort?>? ConditionPartnerReader { get; set; }
    public Func<int, ushort?>? ConditionBloodlineReader { get; set; }
    public Func<int, byte?>? ConditionCityRegionReader { get; set; }
    public Func<int, byte?>? ConditionCityStateReader { get; set; }
    public Func<int, byte?>? ConditionForceDependenceReader { get; set; }
    public byte? ReadConditionForceDependence(S14EventConditionForceReference force) => ConditionForceDependenceReader?.Invoke(force.PhysicalSlot);
    public Func<int, byte?>? ConditionStateAdvantageReader { get; set; }
    public Func<int, byte?>? ConditionAdvantageRequirementReader { get; set; }
    public bool? ReadConditionForceStateAdvantage(S14EventConditionForceReference force, S14EventConditionStatePointer state)
    {
        if (ReadConditionForceValidity(force) is not bool forceValid) return null;
        if (!forceValid) return false;
        if (ReadConditionStateValidity(state) is not bool stateValid) return null;
        if (!stateValid) return false;
        if (state.PhysicalSlot is not int statePhysical || ConditionStateAdvantageReader?.Invoke(statePhysical) is not byte raw) return null;
        var advantage = ReadStaticTableReference(0x85990, raw < 18 ? raw : 0);
        if (advantage is null) return null;
        if (advantage.PhysicalSlot is not int advantagePhysical) return false;
        if (ReadStaticTableReference(0x85990, 0)?.PhysicalSlot is not int advantageAnchor) return null;
        if (advantagePhysical == advantageAnchor) return false;
        if (ConditionAdvantageRequirementReader?.Invoke(advantagePhysical) is not byte required) return null;
        if (ReadForcePhysicalSlot(new(force.PhysicalSlot)) is not int forcePhysical ||
            ReadConditionStateTableSlot(0)?.PhysicalSlot is not int stateAnchor ||
            ReadRegistryReferences(RegistryKind.City) is not { } cities) return null;
        int stateIdentity = statePhysical - stateAnchor, count = 0;
        foreach (var entry in cities)
        {
            if (entry is not { Kind: RegistryKind.City }) return null;
            if (ReadRegistryObjectNativeValidity(entry) is not bool valid) return null;
            if (!valid) continue;
            if (entry.PhysicalSlot is not int cityPhysical) return null;
            if (ConditionCityStateReader?.Invoke(cityPhysical) is not byte cityState) return null;
            if (cityState != stateIdentity) continue;
            if (ReadBaseForceSlot(new(BaseKind.City, cityPhysical)) is not int owner) return null;
            if (owner == forcePhysical) count = unchecked(count + 1);
        }
        return required <= count;
    }
    public S14EventConditionStatePointer? ReadConditionStateTableSlot(int slot) =>
        ReadStaticTableReference(0xDC80, slot) is { } state ? new(state.PhysicalSlot) : null;
    public bool? ReadConditionStateValidity(S14EventConditionStatePointer state)
    {
        if (state.PhysicalSlot is null) return false;
        var anchor = ReadConditionStateTableSlot(0);
        return anchor?.PhysicalSlot is int zero ? state.PhysicalSlot != zero : null;
    }
    public int? ReadConditionForceStateCityCount(S14EventConditionForceReference force, int statePhysicalSlot)
    {
        if (force.PhysicalSlot is not (>= 1 and <= 51)) return 0;
        int count = 0;
        for (int slot = 0; slot < 52; slot++)
        {
            var city = ReadCityTableReference(slot);
            if (city is null || ReadBaseForceSlot(city) is not int owner) return null;
            if (owner != force.PhysicalSlot) continue;
            if (city.PhysicalSlot is not int physical || ConditionCityStateReader?.Invoke(physical) is not byte raw) return null;
            var state = ReadConditionStateTableSlot(raw < 23 ? raw : 0);
            if (state is null) return null;
            if (state.PhysicalSlot == statePhysicalSlot) count++;
        }
        return count;
    }
    public Func<int, byte?>? ConditionAreaSecurityReader { get; set; }
    public int? ReadConditionForceStateAreaCount(S14EventConditionForceReference force, int statePhysicalSlot)
    {
        if (ReadForcePhysicalSlot(new(force.PhysicalSlot)) is not int owner) return null;
        if (owner is not (>= 1 and <= 51)) return 0;
        if (ReadRegisteredAreaReferences() is not { } areas) return null;
        int count = 0;
        foreach (var area in areas)
        {
            if (area is null || ReadAreaCurrentForceSlot(area) is not byte areaOwner) return null;
            if (areaOwner != owner) continue;
            // The source uses the fixed original city, not current administration.
            // It adds no Area/City/inner State validity gate and retains duplicates.
            var state = ReadConditionAreaOriginalState(area);
            if (state is null) return null;
            if (state.PhysicalSlot == statePhysicalSlot) count = unchecked(count + 1);
        }
        return count;
    }
    public S14EventConditionStatePointer? ReadConditionAreaOriginalState(AreaReference area)
    {
        if (ReadAreaValidityCode(area) is not byte citySlot) return null;
        var city = ReadCityTableReference(citySlot < 52 ? citySlot : 0);
        if (city?.PhysicalSlot is not int physical || ConditionCityStateReader?.Invoke(physical) is not byte stateSlot) return null;
        return ReadConditionStateTableSlot(stateSlot < 23 ? stateSlot : 0);
    }
    public AreaReference? ReadConditionBaseArea(BaseReference reference) => CurrentGateMapQueries.FindArea(reference, this);
    public bool? ReadConditionAreaValidity(AreaReference reference) => ReadAreaNativeValidity(reference);
    public AreaReference? ReadConditionAreaTableSlot(int slot) => ReadAreaTableReference(slot);
    public int? ReadConditionAreaForceSlot(AreaReference reference) => ReadAreaCurrentForceSlot(reference);
    public byte? ReadConditionAreaSecurity(AreaReference reference) => reference.PhysicalSlot is int physical ? ConditionAreaSecurityReader?.Invoke(physical) : null;
    public Func<int, CeAreaDevelopmentField, int?>? ConditionAreaDevelopmentLevelReader { get; set; }
    public int? ReadConditionAreaDevelopmentLevel(AreaReference reference, CeAreaDevelopmentField field) =>
        reference.PhysicalSlot is int physical ? ConditionAreaDevelopmentLevelReader?.Invoke(physical, field) : null;
    public Func<int, ushort?>? ConditionAppearanceYearReader { get; set; }
    public Func<int, short?>? ConditionImperialCourtTendencyReader { get; set; }
    public short? ReadConditionPersonImperialCourtTendency(S14EventConditionPersonReference person) => ConditionImperialCourtTendencyReader?.Invoke(person.PhysicalSlot);
    public Func<int, byte?>? ConditionLoyaltyReader { get; set; }
    public Func<int, int, byte?>? ConditionBaseAbilityReader { get; set; }
    public Func<int, int, bool?>? ConditionDislikeReader { get; set; }
    public Func<int, int, bool?>? ConditionFamilyReader { get; set; }
    public Func<int, int, byte?>? ConditionAllianceReader { get; set; }
    public Func<int, int, int?>? ConditionFriendshipLevelReader { get; set; }
    public Func<int, int, bool?>? ConditionForceAdjacentReader { get; set; }
    public ushort? ReadConditionForceAdvisorId(S14EventConditionForceReference force) => ReadAssignmentForceAdvisor52(force.PhysicalSlot);
    public ushort? ReadConditionPersonBloodlineId(S14EventConditionPersonReference person) => ConditionBloodlineReader?.Invoke(person.PhysicalSlot);
    public int? ReadConditionForceRegionCityCount(S14EventConditionForceReference force, int region)
    {
        if (ReadForcePhysicalSlot(new(force.PhysicalSlot)) is not int owner || ReadRegistryReferences(RegistryKind.City) is not { } cities) return null;
        int count = 0;
        foreach (var entry in cities)
        {
            if (entry is not { Kind: RegistryKind.City }) throw new InvalidDataException("Region city-count registry contains an unknown receiver.");
            var city = new BaseReference(BaseKind.City, entry.PhysicalSlot);
            if (ReadBaseNativeValidity(city) is not bool valid) return null;
            if (!valid) continue;
            if (city.PhysicalSlot is not int physical || ConditionCityRegionReader?.Invoke(physical) is not byte actualRegion) return null;
            if (actualRegion != region) continue;
            if (ReadBaseForceSlot(city) is not int actualOwner) return null;
            if (actualOwner == owner) count = unchecked(count + 1);
        }
        return count;
    }
    public byte? ReadConditionForceKingdomNameIndex(S14EventConditionForceReference force) => ReadForceKingdomNameIndex(force.PhysicalSlot);
    public byte? ReadConditionForceRank(S14EventConditionForceReference force) => ReadMoraleWaveForceRank47(force.PhysicalSlot);
    public RegistryObjectReference? ReadConditionItemTableSlot(int slot) => ReadRegistryTableReference(RegistryKind.Item, slot);
    public bool? ReadConditionItemValidity(RegistryObjectReference item) => ReadRegistryObjectNativeValidity(item);
    public ushort? ReadConditionItemOwnerId(RegistryObjectReference item) => item.PhysicalSlot is int physical ? ReadCaptureItemOwner4A(physical) : null;
    public BaseReference? ReadConditionPersonHomeBase(S14EventConditionPersonReference person) =>
        ReadPersonFirstLocation(person.PhysicalSlot) is ushort raw ? BaseLookup.Find(raw, this) : null;
    public int? ReadConditionForceFriendshipLevel(S14EventConditionForceReference first, S14EventConditionForceReference second) =>
        ConditionFriendshipLevelReader?.Invoke(first.PhysicalSlot, second.PhysicalSlot);
    public bool? ReadConditionForceAdjacent(S14EventConditionForceReference first, S14EventConditionForceReference second) =>
        ConditionForceAdjacentReader?.Invoke(first.PhysicalSlot, second.PhysicalSlot);
    public byte? ReadConditionForceAllianceByte(S14EventConditionForceReference force, int otherPhysicalSlot) =>
        ConditionAllianceReader?.Invoke(force.PhysicalSlot, otherPhysicalSlot);

    public S14EventConditionPersonPointer? ReadConditionPersonTableSlot(int slot)
    {
        TransferPersonReference? value = ReadPersonLookupReference(slot);
        return value is null ? null : new(value.PhysicalSlot is int physical ? new(physical) : null);
    }

    public S14EventConditionPersonPointer? ReadConditionBasePerson() => _officerSources.Placeholder is not { } reference
        ? null : new(reference.Record is int physical ? new(physical) : null);

    public byte? ReadConditionPersonStatus(S14EventConditionPersonReference person) => ReadPersonStatus(person.PhysicalSlot);
    public bool? ReadConditionPersonValidity(S14EventConditionPersonReference person) => ReadPersonNativeValidity(person.PhysicalSlot);

    public IReadOnlyList<S14EventConditionForceReference>? ReadRegisteredConditionForcesInNativeOrder()
    {
        IReadOnlyList<RegistryObjectReference?>? values = ReadRegistryReferences(RegistryKind.Force);
        return values is null ? null : new ConditionForceList(values);
    }
    public IReadOnlyList<S14EventConditionForcePointer>? ReadRegisteredConditionForcePointers() =>
        ProjectRegistry<S14EventConditionForcePointer>(RegistryKind.Force, force => force is not null
            ? new(force.PhysicalSlot is int physical ? new(physical) : null)
            : throw new InvalidDataException("Registered condition force pointer is unknown."));
    public Func<int, bool?>? ConditionForceCoalitionReader { get; set; }
    public bool? ReadConditionForceCoalitionMembership(S14EventConditionForceReference force) =>
        ConditionForceCoalitionReader?.Invoke(force.PhysicalSlot);

    public ushort? ReadConditionForceRulerPersonId(S14EventConditionForceReference force)
    {
        Check(force.PhysicalSlot, 52, "condition force receiver");
        return _forceRulers.GetValueOrDefault(force.PhysicalSlot);
    }

    public S14EventConditionForcePointer? ReadConditionForceTableSlot(int slot)
    {
        ForceReference? value = ReadForceTableReference(slot);
        return value is null ? null : new(value.PhysicalSlot is int physical ? new(physical) : null);
    }
    public S14EventConditionForcePointer? ReadConditionPersonForce(S14EventConditionPersonReference person)
    {
        ForceReference? value = ReadPersonForce(person.PhysicalSlot);
        return value is null ? null : new(value.PhysicalSlot is int physical ? new(physical) : null);
    }
    public bool? ReadConditionForceValidity(S14EventConditionForceReference force) =>
        ReadForceNativeValidity(new(force.PhysicalSlot));
    public int? ReadConditionCurrentPlayerForcePhysicalSlot() => CurrentPlayerForce?.PhysicalSlot;
    public S14EventConditionForcePointer? ReadConditionCurrentPlayerForce() => CurrentPlayerForce is not { } player
        ? null : new(player.PhysicalSlot is int physical ? new(physical) : null);
    public S14EventConditionPersonPointer? ReadConditionCurrentPlayerPerson()
    {
        if (CurrentPlayerForce?.PhysicalSlot is not int physical || ReadConditionForceRulerPersonId(new(physical)) is not ushort id) return null;
        return id <= 6000 ? ReadConditionPersonTableSlot(id) : ReadConditionBasePerson();
    }
    public ushort? ReadConditionPersonFlags(S14EventConditionPersonReference person) => ReadArmyReturnPersonFlags196(person.PhysicalSlot);
    public byte? ReadConditionPersonRawLoyalty(S14EventConditionPersonReference person) => ConditionLoyaltyReader?.Invoke(person.PhysicalSlot);
    public byte? ReadConditionPersonBaseAbility(S14EventConditionPersonReference person, int abilityIndex) => ConditionBaseAbilityReader?.Invoke(person.PhysicalSlot, abilityIndex);
    public bool? ReadConditionPersonIsAreaOfficer(S14EventConditionPersonReference person)
    {
        var areas = ReadRegistryReferences(RegistryKind.Area);
        if (areas is null) return null;
        foreach (var area in areas)
        {
            if (area is not { Kind: RegistryKind.Area, PhysicalSlot: int physical } || ReadAssignmentArea36(physical) is not ushort id) return null;
            var officer = id <= 6000 ? ReadConditionPersonTableSlot(id) : ReadConditionBasePerson();
            if (officer is null) return null;
            // Direct lookup: no Person0 fallback and no extra validity gate.
            if (officer.Value == person) return true;
        }
        return false;
    }
    public ushort? ReadConditionPersonHome(S14EventConditionPersonReference person) => ReadPersonFirstLocation(person.PhysicalSlot);
    public bool? ReadConditionPersonDislikes(S14EventConditionPersonReference first, S14EventConditionPersonReference second) => ConditionDislikeReader?.Invoke(first.PhysicalSlot, second.PhysicalSlot);
    public bool? ReadConditionPersonFamily(S14EventConditionPersonReference first, S14EventConditionPersonReference second) => ConditionFamilyReader?.Invoke(first.PhysicalSlot, second.PhysicalSlot);
    public bool? ReadConditionPersonHumble(S14EventConditionPersonReference person)
    {
        int physical = person.PhysicalSlot;
        if (ReadArmyReturnPersonFlags196(physical) is not ushort flags) return null;
        if ((flags & 0x80) != 0) return false;
        if (ReadPersonStatus(physical) is not byte status) return null;
        bool result = status is 5 or 8;
        var associated = ReadPersonLinkedEntity(physical);
        if (associated is null) return null;
        if (associated.Identity is not null)
        {
            bool away = false;
            if (status is >= 1 and <= 4)
            {
                if (ReadPersonFirstLocation(physical) is not ushort first || ReadPersonSecondLocation(physical) is not ushort second) return null;
                away = PersonPositionRepair.DecodeFirstLocation(first) != PersonPositionRepair.DecodeSecondLocation(second);
            }
            if (away) result = true;
            else
            {
                associated = ReadPersonLinkedEntity(physical);
                if (associated is null || ReadLinkedEntityNativeValidity(associated) is not bool valid) return null;
                if (valid)
                {
                    if (associated.Identity is not int receiver) return null;
                    var commands = ReadOwnedPersonCommandManager();
                    if (commands?.Disappearances?.ContainsKey(receiver) == true) result = true; // Actual kind16 in232370.
                    else if (commands?.Returns.ContainsKey(receiver) != true) return null;
                }
            }
        }
        if (ReadConditionDate() is not { } date || ConditionAppearanceYearReader?.Invoke(physical) is not ushort year) return null;
        return date.Year >= year && result;
    }
    public bool? ReadConditionApplicationPresent() => ConditionApplicationPresent;
    public int? ReadConditionCurrentUserIndex() => ConditionCurrentUserIndex;
    public ushort? ReadConditionPersonCharacterId(S14EventConditionPersonReference person) =>
        ReadPersonIdentity(person.PhysicalSlot);
    public ushort? ReadConditionEmperorPersonId() => ReadEmperorId();
    public S14EventConditionCityPointer? ReadConditionCityTableSlot(int slot) => ReadCityTableReference(slot) is { } city ? new(city.PhysicalSlot) : null;
    public int? ReadConditionCityForceSlot(int physicalSlot) => ReadBaseForceSlot(new(BaseKind.City, physicalSlot));
    public byte? ReadConditionPersonRawForceSlot(S14EventConditionPersonReference person) => ReadPersonOldForceSlot(person.PhysicalSlot);
    public byte? ReadConditionSelectedForce() => EventConditions.SelectedForce;
    public ushort? ReadConditionPersonPartnerId(S14EventConditionPersonReference person) => ConditionPartnerReader?.Invoke(person.PhysicalSlot);
    public IReadOnlyList<int?>? ReadRegisteredConditionPersonPhysicalSlots() => ReadRegisteredPersonPhysicalSlots();
    public int? ReadConditionForceCityCount(S14EventConditionForceReference force) => PersonArmyCaptureSource.ReadOwnedCityCount(this, new(force.PhysicalSlot));
    public BaseReference? ReadConditionBaseSelector(int selector)
    {
        if (selector < 0) throw new InvalidDataException("Dynamic base selector requires an event execution context.");
        var value = BaseLookup.Find(selector, this);
        return value.PhysicalSlot is null ? BaseLookup.Find(0, this) : value;
    }
    public bool? ReadConditionBaseValidity(BaseReference reference) => ReadBaseNativeValidity(reference);
    public int? ReadConditionBaseTroops(BaseReference reference) => ReadBaseResource(reference, BaseResourceKind.Troops);
    public int? ReadConditionBaseGold(BaseReference reference) => ReadBaseResource(reference, BaseResourceKind.Gold);
    public byte? ReadConditionBaseMorale(BaseReference reference) => ReadBaseMorale44(reference);
    public IReadOnlyList<S14EventConditionCityPointer>? ReadRegisteredConditionCities() =>
        ProjectRegistry<S14EventConditionCityPointer>(RegistryKind.City, city => city is not null
            ? new(city.PhysicalSlot) : throw new InvalidDataException("Registered condition city pointer is unknown."));
    public S14EventConditionStatePointer? ReadConditionCityState(int physical) =>
        ConditionCityStateReader?.Invoke(physical) is byte raw ? ReadConditionStateTableSlot(raw < 23 ? raw : 0) : null;
    public ushort? ReadConditionBaseObjectId(BaseReference reference) => ReadBaseObjectLink(reference);
    public S14EventConditionForcePointer? ReadConditionBaseForce(BaseReference reference)
    {
        if (ReadBaseForceSlot(reference) is not int raw) return null;
        var force = ReadForceTableReference(raw < 52 ? raw : 0);
        return force is null ? null : new(force.PhysicalSlot is int physical ? new(physical) : null);
    }
    public S14EventConditionForcePointer? ReadConditionPrisonerCustodianForce(S14EventConditionPersonReference person)
    {
        if (ReadPersonSecondLocation(person.PhysicalSlot) is not ushort raw) return null;
        var position = PersonPositionRepair.DecodeSecondLocation(raw);
        if (position.Kind is 2 or 5 or 6)
        {
            // The location kind comes from current whereabouts, but custody at
            // a base deliberately follows the person's home, not those whereabouts.
            var home = ReadConditionPersonHomeBase(person);
            if (home is null || ReadConditionBaseValidity(home) is not bool valid) return null;
            return valid ? ReadConditionBaseForce(home) : new(null);
        }
        if (position.Kind == 27)
        {
            var army = ReadArmyUnitTableReference(position.Index < 501 ? position.Index : 0);
            if (army is null || ReadArmyUnitNativeValidity(army) is not bool valid) return null;
            if (!valid) return new(null);
            var force = ReadArmyUnitForceReference(army);
            return new(force.PhysicalSlot is int physical ? new(physical) : null);
        }
        return new(null);
    }
    public BaseReference? ReadConditionPersonLocationBase(S14EventConditionPersonReference person)
    {
        if (ReadPersonSecondLocation(person.PhysicalSlot) is not ushort raw) return null;
        var position = PersonPositionRepair.DecodeSecondLocation(raw);
        return position.Kind switch
        {
            5 => ReadCityTableReference(position.Index < 52 ? position.Index : 0),
            6 => ReadGateTableReference(position.Index < 11 ? position.Index : 0),
            _ => new(BaseKind.City, null)
        };
    }
    public int? ReadConditionEmperorForcePhysicalSlot()
    {
        if (ReadEmperorId() is not ushort id) return null;
        var lookup = id <= 6000 ? ReadConditionPersonTableSlot(id) : ReadConditionBasePerson();
        if (lookup is null) return null;
        int citySlot = 0;
        if (lookup.Value is { } person)
        {
            if (ReadConditionPersonValidity(person) is not bool valid) return null;
            if (valid)
            {
                if (ReadPersonSecondLocation(person.PhysicalSlot) is not ushort raw) return null;
                var location = PersonPositionRepair.DecodeSecondLocation(raw);
                if (location.Kind == 5) citySlot = location.Index;
            }
        }
        var city = ReadCityTableReference(citySlot);
        if (city is null) return null;
        int rawForce = 0;
        if (city.PhysicalSlot is not null)
        {
            if (ReadBaseNativeValidity(city) is not bool cityValid) return null;
            if (cityValid)
            {
                if (ReadBaseForceSlot(city) is not int owner) return null;
                rawForce = owner;
            }
        }
        var force = ReadForceTableReference(rawForce < 52 ? rawForce : 0);
        if (force is null) return null;
        if (force.PhysicalSlot is null) return 0;
        return ReadForceNativeValidity(force) is not bool forceValid ? null : forceValid ? force.PhysicalSlot : 0;
    }
    public int? ReadConditionSelectedUserPersonId(int index)
    {
        if (index < 0) throw new InvalidDataException("Selected-user ID query requires a nonnegative current index.");
        return _conditionSelectedUserPersons.GetValueOrDefault(index);
    }
    public void SetConditionSelectedUserPersonId(int index, int? value)
    {
        if (index < 0) throw new InvalidDataException("Selected-user ID write requires a nonnegative current index.");
        _conditionSelectedUserPersons[index] = value;
    }

    // Keep the native traversal lazy: a matched ruler must not consume later nodes.
    private sealed class ConditionForceList(IReadOnlyList<RegistryObjectReference?> values)
        : IReadOnlyList<S14EventConditionForceReference>
    {
        private static S14EventConditionForceReference Project(RegistryObjectReference? value) =>
            value is { Kind: RegistryKind.Force, PhysicalSlot: int physical } ? new(physical)
                : throw new InvalidDataException("Current condition force list contains an unknown or null receiver.");
        public int Count => values.Count;
        public S14EventConditionForceReference this[int index] => Project(values[index]);
        public IEnumerator<S14EventConditionForceReference> GetEnumerator()
        { foreach (RegistryObjectReference? value in values) yield return Project(value); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
