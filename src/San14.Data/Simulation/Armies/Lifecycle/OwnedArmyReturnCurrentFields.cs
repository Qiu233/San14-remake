using San14.Core;

namespace San14.Data;

/// <summary>Independent mutable Person/Base ownership. This installer seeds
/// only a fresh session; current captures never use initial fields as fallback.</summary>
public static class OwnedArmyReturnCurrentFields
{
    public const string Revision = "CE-owned-return-current-v1";
    public const string GateCombatRevision = "CE-owned-gate-combat-current-v1";
    public static GameState InstallFresh(GameState state, ArmyOperationContext definitions)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(definitions);
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Officers.Values.Any(x => x.LocationState is not null || x.TraitActivation is not null) ||
            state.Cities.Values.Any(x => x.NativeReturnRuntime is not null) ||
            state.Gates.Values.Any(x => x.NativeCombatRuntime is not null))
            throw new InvalidDataException("Return fields may only be installed once in an explicit fresh session.");
        var prefix = definitions.Capture(state) ?? throw new InvalidDataException("Fresh return source definitions do not match current content.");
        var people = state.Officers.ToDictionary(x => x.Key, x => x.Value);
        var receivers = OfficerLocationSourceAdapter.Receivers(state) ?? ArmyReceiverSourceDirectory.CreateFresh(501);
        var residues = CampaignSourceKnowledge.From(state.ResourceState);
        foreach (var person in people.Values.ToArray())
        {
            int physical = prefix.ResolveOfficerSource(person.Id)
                ?? throw new InvalidDataException("Fresh officer has no matching source record.");
            var location = OfficerLocationSourceAdapter.Decode(prefix.ReadPersonSecondLocation(physical), prefix.ReadPersonDistrictSlot(physical), receivers);
            residues = residues.With(OfficerLocationSourceAdapter.Residue(person.Id, location,
                prefix.ReadPersonSecondLocation(physical), prefix.ReadPersonDistrictSlot(physical)));
            people[person.Id] = person with
            {
                LocationState = location,
                TraitActivation = OfficerTraitActivationSourceAdapter.Read(prefix, physical),
                CurrentFormationMask = prefix.ReadPersonFormationMask164(physical),
                CurrentTraitIds = ReadTraitIds(prefix, physical)
                    ?? throw new InvalidDataException("Fresh person trait slots are unknown."),
                CurrentTacticSlots = ReadTacticSlots(prefix, physical)
                    ?? throw new InvalidDataException("Fresh person tactic slots are unknown.")
            };
        }
        var cities = state.Cities.ToDictionary(x => x.Key, x => x.Value);
        foreach (var city in cities.Values.ToArray())
        {
            var target = new BaseReference(BaseKind.City, city.Id);
            //Actual shared Base reset's qword+3C zeroes BOTH ready3C and
            //wounded40. The serialized prefix overwrites3C, skips40, and starts
            //with word2E. These are explicit fresh allocation/prefix writes.
            cities[city.Id] = city with { NativeReturnRuntime = new(Revision,
                prefix.ReadBaseMorale44(target),0,definitions.ReadFreshReturnGovernor2E(city.Id)) };
        }
        var gates = state.Gates.ToDictionary(x => x.Key, x => x.Value);
        foreach (var gate in gates.Values.ToArray())
        {
            var target = new BaseReference(BaseKind.Gate, gate.Id - 51);
            //The shared ctor's qword3C clears40. Static237920 writes44 but
            //skips40; only this fresh allocation consumes the loaded value.
            gates[gate.Id] = gate with { NativeCombatRuntime = new(GateCombatRevision,
                prefix.ReadBaseMorale44(target), 0) };
        }
        var result = state with { Officers = people, Cities = cities, Gates = gates, ResourceState = residues };
        NativeArmyReturnCurrentStateValidation.Validate(result); return result;
    }

    public static SourceWorldContext CaptureCurrent(GameState state,
        ArmyOperationContext definitions, ArmyReceiverSourceDirectory? freshReceivers = null)
    {
        var current = definitions.Capture(state) ?? throw new InvalidDataException("Return definitions/content identity mismatch.");
        current.OverlayOwnedArmyReturnCurrentFields(state, freshReceivers);
        OwnedArmyLifecycleTargets.BindCurrent(state, current);
        return current;
    }

    /// <summary>Operation-local current fields actually consumed by the city menu.</summary>
    internal static SourceWorldContext CaptureCityMenuCurrent(GameState state, ArmyOperationContext definitions)
    {
        var current = definitions.Capture(state) ?? throw new InvalidDataException("city-domestic.menu.definitions_identity");
        current.OverlayCityMenuCurrent(state);
        return current;
    }

    /// <summary>Publishes the current context after a source packet has really
    /// committed. This does not remove Army headers or admit pending effects.</summary>
    internal static GameState PublishCurrent(GameState state, SourceWorldContext current)
    {
        var people = state.Officers.ToDictionary(x => x.Key, x => x.Value);
        var residues = CampaignSourceKnowledge.From(state.ResourceState);
        foreach (var person in people.Values.ToArray())
        {
            int physical = current.ResolveOfficerSource(person.Id)
                ?? throw new InvalidDataException("Current officer publication identity mismatch.");
            _ = person.LocationState ?? throw new InvalidDataException("Current officer location owner is missing.");
            var location = current.ReadOfficerLocation(person.Id) ?? throw new InvalidDataException("Current officer location owner is missing.");
            var residue = current.ReadOfficerLocationResidue(person.Id);
            residues = residues.With(new(person.Id, location.Position?.Kind == OfficerPositionKind.Unresolved ? residue?.LocationCode : null,
                location.Membership?.Kind == OfficerDistrictMembershipKind.Unresolved ? residue?.DistrictCode : null));
            ushort? first = current.ReadPersonFirstLocation(physical); byte? district = current.ReadPersonDistrictSlot(physical);
            int? forceId = person.ForceId;
            if (district != person.DistrictId)
            {
                var force = current.ReadPersonForce(physical)
                    ?? throw new InvalidDataException("Current changed-District Person Force pointer is unknown.");
                int nativeForce = current.ReadForcePhysicalSlot(force)
                    ?? throw new InvalidDataException("Current changed-District Person Force physical slot is unknown.");
                forceId = nativeForce == 0 ? null : nativeForce;
            }
            people[person.Id] = person with
            {
                LocationId = first == 0 ? null : first, DistrictId = district == 0 ? null : district, ForceId = forceId,
                Role = OfficerServiceRules.PublishedRole(OfficerServiceStatusSourceAdapter.Decode(current.ReadPersonStatus(physical)), person.Role),
                ServiceStatus = OfficerServiceStatusSourceAdapter.Decode(current.ReadPersonStatus(physical)),
                Behavior = current.ReadPersonBehavior(physical),
                Health = current.ReadOfficerHealth(person.Id),
                LocationState = location,
                TraitActivation = OfficerTraitActivationSourceAdapter.Read(current, physical),
                CurrentTraitIds = ReadTraitIds(current, physical),
                CurrentTacticSlots = ReadTacticSlots(current, physical)
            };
        }
        var cities = state.Cities.ToDictionary(x => x.Key,x => x.Value);
        foreach (var city in cities.Values.ToArray())
        {
            var fields = city.NativeReturnRuntime ?? throw new InvalidDataException("Current Base mutable owner is missing.");
            var target = new BaseReference(BaseKind.City,city.Id);
            cities[city.Id] = city with
            {
                Gold = Known(current.ReadBaseResource(target,BaseResourceKind.Gold)),
                Supplies = Known(current.ReadBaseResource(target,BaseResourceKind.Supplies)),
                Troops = Known(current.ReadBaseResource(target,BaseResourceKind.Troops)),
                Morale = current.ReadBaseMorale44(target),
                DistrictId = current.ReadBaseDistrictSlot(target) is int nativeDistrict ? nativeDistrict == 0 ? null : nativeDistrict : null,
                GovernorId = current.ReadArmyReturnBaseGovernor2E(target) is ushort governor ? governor == 0 ? null : governor : null,
                NativeReturnRuntime = fields with { Morale44 = current.ReadBaseMorale44(target), Wounded40 = current.ReadArmyReturnBaseWounded40(target),
                    Governor2E = current.ReadArmyReturnBaseGovernor2E(target) }
            };
        }
        var gates = state.Gates.ToDictionary(x => x.Key, x => x.Value);
        foreach (var gate in gates.Values.ToArray())
        {
            if (gate.NativeCombatRuntime is not { } fields) continue;
            var target = new BaseReference(BaseKind.Gate, gate.Id - 51);
            gates[gate.Id] = gate with { Troops = Known(current.ReadBaseResource(target, BaseResourceKind.Troops)),
                NativeCombatRuntime = fields with { Morale44 = current.ReadBaseMorale44(target),
                    Wounded40 = current.ReadArmyReturnBaseWounded40(target) } };
        }
        var result = state with { Officers = people, Cities = cities, Gates = gates, ResourceState = residues };
        result = BaseReturnCaptivesInputs.PublishCurrent(result, current);
        return result;
        static int Known(int? value) => value ?? throw new InvalidDataException("Current Base numeric quantity is unknown.");
    }

    private static int[]? ReadTraitIds(SourceWorldContext current, int physical)
    {
        var slots = new int[9];
        for (int index = 0; index < slots.Length; index++)
        {
            if (current.ReadPersonTraitSlot150(physical, index) is not ushort value) return null;
            slots[index] = value;
        }
        return slots;
    }

    private static byte[]? ReadTacticSlots(SourceWorldContext current, int physical)
    {
        var slots = new byte[10];
        for (int index = 0; index < slots.Length; index++)
        {
            if (current.ReadPersonTacticSlot168(physical, index) is not byte value) return null;
            slots[index] = value;
        }
        return slots;
    }
}

public sealed partial class ArmyOperationContext
{
    internal ushort ReadFreshReturnGovernor2E(int physical)
    {
        var city=_source.Snapshot.Cities.Single(x=>x.Slot==physical);
        return city.OfficerSlot;
    }
}

public sealed partial class SourceWorldContext
{
    internal ushort? ReadOwnedReturnPersonFlags(int physical) => OfficerBehaviorSourceAdapter.EncodeFlags(_persons.GetValueOrDefault(physical)?.Behavior);
    internal void OverlayCityMenuCurrent(GameState state)
    {
        // Missing mutable owners cannot retain the scenario's opening values.
        // Receiver0 retains its explicit native-invalid/fallback semantics.
        for (int slot = 1; slot < 52; slot++)
        {
            // Reproject each legitimate key owner after the shared prefix's
            // Values traversal; a malformed row cannot contaminate another slot.
            if (state.Districts.TryGetValue(slot, out var district) && district.Id == slot)
            {
                WriteDistrictAffiliation(slot, (byte)district.ForceId, (byte)district.Number);
                SetDistrictRawByte(slot, 0x12, district.LeaderId is int leader ? (byte)leader : null);
                SetDistrictRawByte(slot, 0x13, district.LeaderId is int high ? (byte)(high >> 8) : null);
            }
            else
            {
                WriteDistrictAffiliation(slot, null, null);
                SetDistrictRawByte(slot, 0x12, null); SetDistrictRawByte(slot, 0x13, null);
            }
            SetOwnedForceRulerSlot10(slot, state.Forces.TryGetValue(slot, out var force) && force.Id == slot
                ? force.RulerId is int ruler ? (ushort)ruler : null : null);
        }
        var ownedPeople = new HashSet<int>();
        var residues = state.ResourceState as CampaignSourceKnowledge;
        foreach (var person in state.Officers.Values)
        {
            int physical = ResolveOfficerSource(person.Id)
                ?? throw new InvalidDataException("city-domestic.menu.person_identity");
            ownedPeople.Add(physical);
            SetPersonDistrictSlot(physical, OfficerLocationSourceAdapter.EncodeMembership(
                person.LocationState?.Membership, residues?.Find(person.Id)));
            _persons[physical] = _persons[physical] with
            { Status = OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus), Behavior = person.Behavior };
        }
        foreach (int physical in _persons.Keys.Where(x => x != 0 && !ownedPeople.Contains(x)).ToArray())
        {
            SetPersonFirstLocation(physical, null); SetPersonDistrictSlot(physical, null);
            _persons[physical] = _persons[physical] with { Status = null, Behavior = null };
        }
    }

    internal void OverlayOwnedArmyReturnCurrentFields(GameState state, ArmyReceiverSourceDirectory? freshReceivers = null)
    {
        foreach (var city in state.Cities.Values) SetBasePostLoadFlag66(new(BaseKind.City, city.Id), city.DamageTier);
        foreach (var gate in state.Gates.Values) SetBasePostLoadFlag66(new(BaseKind.Gate, gate.Id - 51), gate.DamageTier);
        BindArmyMapOccupancy(state);
        var ownedPeople = new HashSet<int>();
        var receivers = OfficerLocationSourceAdapter.Receivers(state) ?? freshReceivers;
        var residues = state.ResourceState as CampaignSourceKnowledge;
        foreach (var person in state.Officers.Values)
        {
            int physical = ResolveOfficerSource(person.Id)
                ?? throw new InvalidDataException("Current officer has no matching source record.");
            ownedPeople.Add(physical);
            SetPersonFormationMask164(physical, person.CurrentFormationMask);
            if (person.CurrentTraitIds is { Length: not 9 })
                throw new InvalidDataException("Current person trait selection must contain nine entries.");
            for (int index = 0; index < 9; index++)
            {
                int? id = person.CurrentTraitIds?[index];
                if (id is < 0 or > ushort.MaxValue)
                    throw new InvalidDataException("Current person trait reference is not representable by this source profile.");
                SetPersonTraitSlot150(physical, index, id is int value ? (ushort)value : null);
            }
            if (person.CurrentTacticSlots is { Length: not 10 })
                throw new InvalidDataException("Current person tactic slots must contain ten entries.");
            for (int index = 0; index < 10; index++)
                SetPersonTacticSlot168(physical, index, person.CurrentTacticSlots?[index]);
            var location = person.LocationState;
            SetPersonDistrictSlot(physical, OfficerLocationSourceAdapter.EncodeMembership(location?.Membership, residues?.Find(person.Id)));
            var old = _persons[physical];
            _persons[physical] = old with { Status = OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus), Behavior = person.Behavior, Location = OfficerLocationSourceAdapter.EncodePosition(location?.Position, receivers, residues?.Find(person.Id)) };
            BindOfficerHealthSource(physical, person.Id, person.Health);
            OfficerTraitActivationSourceAdapter.Write(this, physical, person.TraitActivation);
        }
        //The full manager may contain receivers outside the playable projection.
        //Their mutable fields are unknown until an independent current owner is
        //saved. Static scenario status/second/flags must not masquerade as current.
        foreach (int physical in _persons.Keys.Where(x => x != 0 && !ownedPeople.Contains(x)).ToArray())
        {
            SetPersonFormationMask164(physical, null);
            for (int index = 0; index < 9; index++) SetPersonTraitSlot150(physical, index, null);
            for (int index = 0; index < 10; index++) SetPersonTacticSlot168(physical, index, null);
            _persons[physical] = _persons[physical] with { Status=null, Behavior=OfficerBehaviorSourceAdapter.WithSourceFlags(_persons[physical].Behavior, null), Location=null };
        }
        BindOfficerLocations(state, receivers);
        foreach (var city in state.Cities.Values)
        {
            var target = new BaseReference(BaseKind.City,city.Id);
            SetBaseResource(target,BaseResourceKind.Gold,city.Gold);
            SetBaseResource(target,BaseResourceKind.Supplies,city.Supplies);
            SetBaseResource(target,BaseResourceKind.Troops,city.Troops);
            SetBaseMorale44(target,city.NativeReturnRuntime?.Morale44);
            SetArmyReturnBaseWounded40(target,city.NativeReturnRuntime?.Wounded40);
            SetArmyReturnBaseGovernor2E(target,city.NativeReturnRuntime?.Governor2E);
        }
        foreach (var gate in state.Gates.Values)
        {
            var target = new BaseReference(BaseKind.Gate, gate.Id - 51);
            SetBaseResource(target, BaseResourceKind.Troops, gate.Troops);
            SetBaseMorale44(target, gate.NativeCombatRuntime?.Morale44);
            SetArmyReturnBaseWounded40(target, gate.NativeCombatRuntime?.Wounded40);
        }
        BaseReturnCaptivesInputs.BindCurrent(state, this);
    }
}
