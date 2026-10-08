using San14.Core;

namespace San14.Data;

/// <summary>Independent mutable Person/Base ownership. This installer seeds
/// only a fresh session; current captures never use initial fields as fallback.</summary>
public static class OriginalOwnedArmyReturnCurrentFields
{
    public const string Revision = "CE-owned-return-current-v1";
    public const string GateCombatRevision = "CE-owned-gate-combat-current-v1";
    public static GameState InstallFresh(GameState state, OriginalOwnedArmyCurrentDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(definitions);
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Officers.Values.Any(x => x.NativeLocationRuntime is not null) ||
            state.Cities.Values.Any(x => x.NativeReturnRuntime is not null) ||
            state.Gates.Values.Any(x => x.NativeCombatRuntime is not null))
            throw new InvalidDataException("Return fields may only be installed once in an explicit fresh session.");
        var prefix = definitions.Capture(state) ?? throw new InvalidDataException("Fresh return source definitions do not match current content.");
        var people = state.Officers.ToDictionary(x => x.Key, x => x.Value);
        foreach (var person in people.Values.ToArray())
        {
            var ability = person.NativeAbilityRuntime ?? throw new InvalidDataException("Fresh Person source identity is missing.");
            if (prefix.ReadPersonIdentity(ability.PhysicalSlot) != person.Id)
                throw new InvalidDataException("Fresh Person identity differs from the installed ability owner.");
            var extras = Enumerable.Range(0,9).Select(i => prefix.ReadPersonTraitExtra1BE(ability.PhysicalSlot,i)).ToArray();
            people[person.Id] = person with { NativeLocationRuntime = new(Revision,
                prefix.ReadPersonSecondLocation(ability.PhysicalSlot), prefix.ReadPersonTraitMask1BC(ability.PhysicalSlot),
                extras.All(x => x.HasValue) ? extras.Select(x => x!.Value).ToArray() : null)
                { District118 = prefix.ReadPersonDistrictSlot(ability.PhysicalSlot) } };
        }
        var cities = state.Cities.ToDictionary(x => x.Key, x => x.Value);
        foreach (var city in cities.Values.ToArray())
        {
            var target = new OriginalBaseReference(OriginalBaseKind.City, city.Id);
            //Actual shared Base reset's qword+3C zeroes BOTH ready3C and
            //wounded40. The serialized prefix overwrites3C, skips40, and starts
            //with word2E. These are explicit fresh allocation/prefix writes.
            cities[city.Id] = city with { NativeReturnRuntime = new(Revision,
                prefix.ReadBaseMorale44(target),0,definitions.ReadFreshReturnGovernor2E(city.Id)) };
        }
        var gates = state.Gates.ToDictionary(x => x.Key, x => x.Value);
        foreach (var gate in gates.Values.ToArray())
        {
            var target = new OriginalBaseReference(OriginalBaseKind.Gate, gate.Id - 51);
            //The shared ctor's qword3C clears40. Static237920 writes44 but
            //skips40; only this fresh allocation consumes the loaded value.
            gates[gate.Id] = gate with { NativeCombatRuntime = new(GateCombatRevision,
                prefix.ReadBaseMorale44(target), 0) };
        }
        var result = state with { Officers = people, Cities = cities, Gates = gates };
        NativeArmyReturnCurrentStateValidation.Validate(result); return result;
    }

    public static OriginalEventInitializationContext CaptureCurrent(GameState state,
        OriginalOwnedArmyCurrentDefinitions definitions)
    {
        NativeArmyReturnCurrentStateValidation.Validate(state);
        var current = definitions.Capture(state) ?? throw new InvalidDataException("Return definitions/content identity mismatch.");
        current.OverlayOwnedArmyReturnCurrentFields(state);
        return current;
    }

    /// <summary>Publishes the current context after a source packet has really
    /// committed. This does not remove Army headers or admit pending effects.</summary>
    internal static GameState PublishCurrent(GameState state, OriginalEventInitializationContext current)
    {
        var people = state.Officers.ToDictionary(x => x.Key, x => x.Value);
        foreach (var person in people.Values.ToArray())
        {
            var ability = person.NativeAbilityRuntime ?? throw new InvalidDataException("Current Person ability owner is missing.");
            var fields = person.NativeLocationRuntime ?? throw new InvalidDataException("Current Person mutable owner is missing.");
            int physical = ability.PhysicalSlot;
            if (current.ReadPersonIdentity(physical) != person.Id) throw new InvalidDataException("Current Person publication identity mismatch.");
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
            var extras = Enumerable.Range(0,9).Select(i => current.ReadPersonTraitExtra1BE(physical,i)).ToArray();
            people[person.Id] = person with
            {
                LocationId = first == 0 ? null : first, DistrictId = district == 0 ? null : district, ForceId = forceId,
                Role = current.ReadPersonStatus(physical) switch
                { 1=>OfficerRole.Ruler,2=>OfficerRole.DistrictLeader,3=>OfficerRole.Governor,4=>OfficerRole.Officer,5=>OfficerRole.Free,_=>person.Role },
                NativeAbilityRuntime = ability with { Status11E = current.ReadPersonStatus(physical),
                    Flags196 = current.ReadOwnedReturnPersonFlags(physical), Health199 = current.ReadArmyReturnPersonByte199(physical) },
                NativeLocationRuntime = fields with { Second11C = current.ReadPersonSecondLocation(physical),
                    District118 = district,
                    TraitMask1BC = current.ReadPersonTraitMask1BC(physical),
                    TraitExtras1BE = extras.All(x => x.HasValue) ? extras.Select(x => x!.Value).ToArray() : null }
            };
        }
        var cities = state.Cities.ToDictionary(x => x.Key,x => x.Value);
        foreach (var city in cities.Values.ToArray())
        {
            var fields = city.NativeReturnRuntime ?? throw new InvalidDataException("Current Base mutable owner is missing.");
            var target = new OriginalBaseReference(OriginalBaseKind.City,city.Id);
            cities[city.Id] = city with
            {
                Gold = Known(current.ReadBaseResource(target,OriginalBaseResourceKind.Gold)),
                Supplies = Known(current.ReadBaseResource(target,OriginalBaseResourceKind.Supplies)),
                Troops = Known(current.ReadBaseResource(target,OriginalBaseResourceKind.Troops)),
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
            var target = new OriginalBaseReference(OriginalBaseKind.Gate, gate.Id - 51);
            gates[gate.Id] = gate with { Troops = Known(current.ReadBaseResource(target, OriginalBaseResourceKind.Troops)),
                NativeCombatRuntime = fields with { Morale44 = current.ReadBaseMorale44(target),
                    Wounded40 = current.ReadArmyReturnBaseWounded40(target) } };
        }
        var result = state with { Officers = people, Cities = cities, Gates = gates };
        result = OriginalBaseReturnCaptivesInputs.PublishCurrent(result, current);
        NativeArmyReturnCurrentStateValidation.Validate(result); return result;
        static int Known(int? value) => value ?? throw new InvalidDataException("Current Base numeric quantity is unknown.");
    }
}

public sealed partial class OriginalOwnedArmyCurrentDefinitions
{
    internal ushort ReadFreshReturnGovernor2E(int physical)
    {
        var city=_source.Snapshot.Cities.Single(x=>x.Slot==physical);
        return city.OfficerSlot;
    }
}

public sealed partial class OriginalEventInitializationContext
{
    internal ushort? ReadOwnedReturnPersonFlags(int physical) => _persons.GetValueOrDefault(physical)?.Flags;
    internal void OverlayOwnedArmyReturnCurrentFields(GameState state)
    {
        var ownedPeople = new HashSet<int>();
        foreach (var person in state.Officers.Values)
        {
            var ability = person.NativeAbilityRuntime ?? throw new InvalidDataException("Current Person native identity is missing.");
            int physical = ability.PhysicalSlot;
            ownedPeople.Add(physical);
            if (ReadPersonIdentity(physical) != person.Id) throw new InvalidDataException("Current Person physical identity mismatch.");
            var location = person.NativeLocationRuntime;
            if (location?.District118 is byte district) SetPersonDistrictSlot(physical, district);
            var old = _persons[physical];
            _persons[physical] = old with { Status = ability.Status11E, Flags = ability.Flags196,
                Tail = ability.Extra1FD, Location = location?.Second11C };
            SetArmyReturnPersonByte199(physical, ability.Health199);
            SetPersonTraitMask1BC(physical,location?.TraitMask1BC);
            for (int i=0;i<9;i++) SetPersonTraitExtra1BE(physical,i,location?.TraitExtras1BE?[i]);
        }
        //The full manager may contain receivers outside the playable projection.
        //Their mutable fields are unknown until an independent current owner is
        //saved. Static scenario status/second/flags must not masquerade as current.
        foreach (int physical in _persons.Keys.Where(x => x != 0 && !ownedPeople.Contains(x)).ToArray())
            _persons[physical] = _persons[physical] with { Status=null, Flags=null, Location=null };
        foreach (var city in state.Cities.Values)
        {
            var target = new OriginalBaseReference(OriginalBaseKind.City,city.Id);
            SetBaseResource(target,OriginalBaseResourceKind.Gold,city.Gold);
            SetBaseResource(target,OriginalBaseResourceKind.Supplies,city.Supplies);
            SetBaseResource(target,OriginalBaseResourceKind.Troops,city.Troops);
            SetBaseMorale44(target,city.NativeReturnRuntime?.Morale44);
            SetArmyReturnBaseWounded40(target,city.NativeReturnRuntime?.Wounded40);
            SetArmyReturnBaseGovernor2E(target,city.NativeReturnRuntime?.Governor2E);
        }
        foreach (var gate in state.Gates.Values)
        {
            var target = new OriginalBaseReference(OriginalBaseKind.Gate, gate.Id - 51);
            SetBaseResource(target, OriginalBaseResourceKind.Troops, gate.Troops);
            SetBaseMorale44(target, gate.NativeCombatRuntime?.Morale44);
            SetArmyReturnBaseWounded40(target, gate.NativeCombatRuntime?.Wounded40);
        }
        OriginalBaseReturnCaptivesInputs.BindCurrent(state, this);
    }
}
