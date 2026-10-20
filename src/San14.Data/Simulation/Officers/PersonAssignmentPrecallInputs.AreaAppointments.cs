using San14.Core;

namespace San14.Data;

public sealed partial class PersonAssignmentPrecallInputs
{
    private const string AreaAppointmentCostKey = Prefix + "regional-command-cost";
    // Extracted paired source data: TC1A493BC / SC1A4A3EC, Int32 = 1.
    private const int ExtractedAreaAppointmentCost = 1;
    internal static int? ReadAreaAppointmentCost(GameState state) => Value(state, AreaAppointmentCostKey, true);

    internal static GameState PublishAreaAppointments(GameState state, SourceWorldContext current,
        IEnumerable<int> touchedCities, IEnumerable<int> touchedAreas, IEnumerable<(int Force, int Slot)> touchedAssociations)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("saved_simulation");
        if (simulation.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1) throw Missing("saved_owner");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        var cities = new Dictionary<int, CityState>(state.Cities);
        foreach (int city in touchedCities)
        {
            ushort ca = current.ReadAssignmentCityCA(city) ?? throw Missing("published_recruiter");
            ushort cc = current.ReadAssignmentCityCC(city) ?? throw Missing("published_trainer");
            values[CityFieldKey(city, 0xCA)] = ca;
            values[CityFieldKey(city, 0xCC)] = cc;
            if (cities.TryGetValue(city, out var row) && row.Duties is { } duties)
                cities[city] = row with { Duties = duties with { RecruiterId = ca == 0 ? null : ca, TrainerId = cc == 0 ? null : cc } };
        }
        var areas = new Dictionary<int, AreaState>(state.Areas);
        foreach (int area in touchedAreas)
        {
            ushort person = current.ReadAssignmentArea36(area) ?? throw Missing("published_area_person");
            byte focus = current.ReadAssignmentArea72(area) ?? throw Missing("published_area_focus");
            values[AreaFieldKey(area, 0x36)] = person;
            values[AreaFieldKey(area, 0x72)] = focus;
            if (areas.TryGetValue(area, out var row))
                areas[area] = row with { Administration = new(person == 0 ? null : person, AreaAdministration.DecodeFocus(focus)) };
        }
        foreach (var (force, slot) in touchedAssociations)
        {
            var reference = current.ReadForceTableReference(force) ?? throw Missing("published_force_pointer");
            values[AssociationPersonKey(force, slot)] = current.ReadForceAssociationPersonId(reference, slot) ?? throw Missing("published_association_person");
            values[AssociationTargetKey(force, slot)] = current.ReadForceAssociationTargetSlot(reference, slot) ?? throw Missing("published_association_target");
        }
        return state with { Cities = cities, Areas = areas, NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    /// <summary>One operation's edits to the existing independent ordered owner.
    /// A capacity-one replacement never reads inactive storage or the old count.
    /// This is a save-boundary patch, not a second saved fact.</summary>
    internal sealed class AreaAppointmentDraft(GameState state)
    {
        private readonly Dictionary<int, int> _counts = [];
        private readonly Dictionary<(int Person, int Index), int> _entries = [];
        internal IEnumerable<int> ChangedPeople => _counts.Keys;
        internal AreaAppointmentDraft Fork()
        {
            var copy = new AreaAppointmentDraft(state);
            foreach (var row in _counts) copy._counts.Add(row.Key, row.Value);
            foreach (var row in _entries) copy._entries.Add(row.Key, row.Value);
            return copy;
        }

        internal int Count(int person) => _counts.TryGetValue(person, out int count) ? count :
            Value(state, PersonAreaCount1E8Key(person), true) ?? throw Missing("person_active_count");
        internal int Length(int person) => Value(state, PersonAreaLengthKey(person), true) is int length && length >= 0
            ? length : throw Missing("person_storage_length");
        internal int Entry(int person, int index)
        {
            if (index < 0 || index >= Length(person)) throw Missing("person_indexed_storage");
            return _entries.TryGetValue((person, index), out int value) ? value :
                Value(state, PersonAreaEntryKey(person, index), true) ?? throw Missing("person_storage_value");
        }
        internal void SetFirst(int person, int area)
        {
            if (Length(person) < 1) throw Missing("person_first_storage");
            _entries[(person, 0)] = area;
            _counts[person] = 1;
        }
        internal PersonAreaAssignmentVector Materialize(int person)
        {
            var values = new int[Length(person)];
            for (int i = 0; i < values.Length; i++) values[i] = Entry(person, i);
            return new(values, Count(person));
        }
        internal void Write(int person, PersonAreaAssignmentVector vector)
        {
            if (vector.Values.Length != Length(person)) throw Missing("person_storage_resize");
            _counts[person] = vector.ActiveCount1E8;
            for (int i = 0; i < vector.Values.Length; i++) _entries[(person, i)] = vector.Values[i];
        }
        internal GameState Publish(GameState result)
        {
            if (_counts.Count == 0 && _entries.Count == 0) return result;
            var simulation = result.NativeArmySimulation ?? throw Missing("saved_simulation");
            if (simulation.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1) throw Missing("saved_owner");
            var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
            foreach (var row in _counts) values[PersonAreaCount1E8Key(row.Key)] = row.Value;
            foreach (var row in _entries) values[PersonAreaEntryKey(row.Key.Person, row.Key.Index)] = row.Value;
            return result with { NativeArmySimulation = simulation with
                { Parameters = simulation.Parameters with { Int32Values = values } } };
        }
    }

    private static InvalidDataException Missing(string input) => new("area-administration." + input);
}
