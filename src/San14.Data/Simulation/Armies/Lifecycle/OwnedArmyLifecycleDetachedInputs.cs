using San14.Core;

namespace San14.Data;

/// <summary>Current raw fields that the gameplay projection cannot encode
/// with nullable IDs. Fresh initialization saves native values once. Current
/// explicit gameplay IDs take precedence; a load never rereads initial rows.</summary>
public static class OwnedArmyLifecycleDetachedInputs
{
    public const string VersionKey = "ce-army-detached-current:version";
    internal static string Key(string table, int slot, string field) => $"ce-army-detached-current:{table}:{slot}:{field}";
    public static GameState InstallFresh(GameState state, ArmyOperationContext definitions)
    {
        if (state.NativeArmySimulation is not { } simulation || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Detached current receivers require their unused fresh source owner.");
        var values = definitions.ReadFreshDetachedReceiverInputs();
        var saved = new Dictionary<string,int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var pair in values) saved.Add(pair.Key,pair.Value);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = saved } } };
    }
    public static void BindCurrent(GameState state, SourceWorldContext current)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Int32Values.GetValueOrDefault(VersionKey) != 1) return;
        var values = parameters.Int32Values;
        foreach (var city in state.Cities.Values)
            if (city.DistrictId is null && Byte("City",city.Id,"30") is byte district) current.SetCityDistrictSlot(city.Id,district);
        foreach (var force in state.Forces.Values)
            if (force.RulerId is null && Word("Force",force.Id,"10") is ushort ruler) current.SetForceRecord(new(force.Id,ruler));
        foreach (var gate in state.Gates.Values)
            current.SetArmyReturnBaseGovernor2E(new(BaseKind.Gate,gate.Id-51),
                gate.GovernorId is int governor ? (ushort)governor : Word("Gate",gate.Id-51,"2E"));
        var owned = state.Officers.Values.Select(x => current.ResolveOfficerSource(x.Id)
            ?? throw new InvalidDataException("Detached Person source identity is unknown.")).ToHashSet();
        foreach (var person in state.Officers.Values)
        {
            int physical = current.ResolveOfficerSource(person.Id)
                ?? throw new InvalidDataException("Detached Person source identity is unknown.");
            if (person.LocationId is null) current.SetPersonFirstLocation(physical, ReadPersonFirstLocation(state, person, physical));
            if (person.DistrictId is null && person.LocationState?.Membership is null)
                current.SetPersonDistrictSlot(physical,Byte("Person",physical,"E0"));
        }
        for (int physical=1;physical<1851;physical++)
        {
            if (owned.Contains(physical) || Word("Person",physical,"ID") is not ushort id ||
                Word("Person",physical,"15E") is not ushort flags || Byte("Person",physical,"1C5") is not byte tail ||
                Byte("Person",physical,"E6") is not byte status || Word("Person",physical,"E4") is not ushort second) continue;
            current.SetPersonRecord(new(physical,id,flags,tail,status,second));
        }
        byte? Byte(string table,int physical,string field) => values.TryGetValue(Key(table,physical,field),out int raw) && raw is >=0 and <=255 ? (byte)raw : null;
        ushort? Word(string table,int physical,string field) => values.TryGetValue(Key(table,physical,field),out int raw) && raw is >=0 and <=65535 ? (ushort)raw : null;
    }

    /// <summary>The existing detached owner preserves known native no-home0,
    /// independently of the nullable gameplay Base reference. Queries never
    /// reread initial rows or derive vacancy from death/presence.</summary>
    internal static ushort? ReadPersonFirstLocation(GameState state, OfficerState person, int physical)
    {
        if (person.LocationId is int first) return checked((ushort)first);
        var values = state.NativeArmySimulation?.Parameters.Int32Values;
        return values?.GetValueOrDefault(VersionKey) == 1 &&
            values.TryGetValue(Key("Person", physical, "E2"), out int raw) && raw is >= 0 and <= ushort.MaxValue
            ? (ushort)raw : null;
    }

    /// <summary>Publishes one reached first-location write to that same saved
    /// owner; no unrelated receiver is rebound or refreshed.</summary>
    internal static GameState PublishCurrentPersonFirstLocation(GameState state, SourceWorldContext current, int physical)
    {
        if (state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1)
            throw new InvalidDataException("Person first-location current owner is unknown.");
        ushort first = current.ReadPersonFirstLocation(physical)
            ?? throw new InvalidDataException("Published Person first-location knowledge is unknown.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
        { [Key("Person", physical, "E2")] = first };
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    internal static GameState PublishCurrent(GameState state, SourceWorldContext current)
    {
        var simulation = state.NativeArmySimulation!;
        var values = new Dictionary<string,int>(simulation.Parameters.Int32Values);
        var cities = new Dictionary<int,CityState>(state.Cities);
        foreach (var city in cities.Values.ToArray())
        {
            int? raw = current.ReadBaseDistrictSlot(new(BaseKind.City,city.Id));
            if (raw is int district) values[Key("City",city.Id,"30")] = district;
            cities[city.Id] = city with { DistrictId = raw == 0 ? null : raw };
        }
        var people = new Dictionary<int,OfficerState>(state.Officers);
        foreach (var person in people.Values.ToArray())
        {
            int physical = current.ResolveOfficerSource(person.Id)
                ?? throw new InvalidDataException("Detached Person source identity is unknown.");
            ushort? first = current.ReadPersonFirstLocation(physical); byte? district = current.ReadPersonDistrictSlot(physical);
            if (first is ushort rawFirst) values[Key("Person",physical,"E2")] = rawFirst;
            if (district is byte rawDistrict) values[Key("Person",physical,"E0")] = rawDistrict;
            //Known native placeholder0 is retained in the raw owner. The
            //gameplay reference uses null because Base0/District0 are not
            //playable entities in this program's projection.
            people[person.Id] = person with { LocationId = first == 0 ? null : first, DistrictId = district == 0 ? null : district };
        }
        var gates = new Dictionary<int,GateState>(state.Gates);
        foreach (var gate in gates.Values.ToArray())
            if (current.ReadArmyReturnBaseGovernor2E(new(BaseKind.Gate,gate.Id-51)) is ushort governor)
            { values[Key("Gate",gate.Id-51,"2E")] = governor; gates[gate.Id] = gate with { GovernorId = governor == 0 ? null : governor }; }
        return state with { Cities = cities, Officers = people, Gates = gates,
            NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}

public sealed partial class ArmyOperationContext
{
    internal Dictionary<string,int> ReadFreshDetachedReceiverInputs()
    {
        var values = new Dictionary<string,int>();
        foreach (var city in _source.Snapshot.Cities) values.Add(OwnedArmyLifecycleDetachedInputs.Key("City",city.Slot,"30"),city.DistrictSlot);
        foreach (var force in _source.Snapshot.Forces)
            if (force.RulerSlot==0) values.Add(OwnedArmyLifecycleDetachedInputs.Key("Force",force.Slot,"10"),force.RulerSlot);
        foreach (var gate in _source.Snapshot.Gates) values.Add(OwnedArmyLifecycleDetachedInputs.Key("Gate",gate.Slot,"2E"),gate.OfficerSlot);
        foreach (var person in _source.Snapshot.Characters)
        {
            values.Add(OwnedArmyLifecycleDetachedInputs.Key("Person",person.Slot,"E2"),person.CitySlot);
            values.Add(OwnedArmyLifecycleDetachedInputs.Key("Person",person.Slot,"E0"),person.DistrictSlot);
            if (person.CreditReference is not { RawFlagsWord: ushort flags } credit)
                throw new InvalidDataException("Fresh detached Person receiver flags are unknown.");
            values.Add(OwnedArmyLifecycleDetachedInputs.Key("Person",person.Slot,"ID"),person.CharacterId);
            values.Add(OwnedArmyLifecycleDetachedInputs.Key("Person",person.Slot,"15E"),flags);
            values.Add(OwnedArmyLifecycleDetachedInputs.Key("Person",person.Slot,"1C5"),credit.RawTailCode);
            values.Add(OwnedArmyLifecycleDetachedInputs.Key("Person",person.Slot,"E6"),person.StatusCode);
            values.Add(OwnedArmyLifecycleDetachedInputs.Key("Person",person.Slot,"E4"),person.SecondaryCitySlot);
            //Only placeholders have no domain OfficerState owner.
            if (person.CharacterId == 0 && person.AppearanceYear is ushort year)
                values.Add(OwnedArmyLifecycleDetachedInputs.Key("Person",person.Slot,"appearance-year"), year);
        }
        return values;
    }
}
