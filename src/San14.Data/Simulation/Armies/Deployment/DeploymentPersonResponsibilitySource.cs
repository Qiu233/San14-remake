using San14.Core;

namespace San14.Data;

internal readonly record struct DeploymentPersonResponsibilityKey(int Weight, int? NamedArea);

internal static class DeploymentPersonResponsibilitySource
{
    internal static DeploymentPersonResponsibilityKey Read(GameState state, SourceWorldContext current,
        int physical, Func<bool> special)
    {
        BaseReference city = City(current, physical);
        int id = K(current.ReadPersonIdentity(physical), "person_identity");
        var key = CityKey(current, city, id);
        int weight = key.Weight; int? name = key.NamedArea;
        var force = current.ReadPersonForce(physical) ?? throw Missing("person_force");
        if (K(current.ReadForceNativeValidity(force), "force_validity") && id != 0)
            for (int index = 0; index < 3; index++)
                if (K(current.ReadForceAssociationPersonId(force, index), "force_appointment") == id)
                { weight = 8; break; }
        if (K(force.PhysicalSlot, "force_pointer") is >= 46 and <= 50 || special()) weight = 10000;
        return new(weight == 0 ? 9995 : weight, name);
    }

    // The pair consumer deliberately does not build two complete per-person keys:
    // both Cities precede both City scans, which precede both Force phases.
    internal static bool BeforePrimary(GameState state, SourceWorldContext current, int left, int right,
        Func<int, bool> special, bool reversed = false)
    {
        if (left == right) return false;
        bool leftValid = K(current.ReadPersonNativeValidity(left), "person_validity");
        bool rightValid = K(current.ReadPersonNativeValidity(right), "person_validity");
        if (!leftValid) return false;
        if (!rightValid) return true;
        if (ForceCategory(current, left) || special(left)) return false;
        if (ForceCategory(current, right) || special(right)) return true;
        // 3437C0 calls the complete natural wrapper with exchanged pointers.
        // Its repeated validity and special gates are part of the source trace.
        return reversed ? BeforePrimary(state, current, right, left, special) :
            BeforeModeZero(state, current, left, right, special);
    }

    internal static bool BeforeModeZero(GameState state, SourceWorldContext current, int left, int right,
        Func<int, bool> special)
    {
        if (left == right) return false;
        BaseReference leftCity = City(current, left), rightCity = City(current, right);
        int leftId = K(current.ReadPersonIdentity(left), "person_identity");
        int rightId = K(current.ReadPersonIdentity(right), "person_identity");
        var a = CityKey(current, leftCity, leftId);
        var b = CityKey(current, rightCity, rightId);
        if (ForceAppointment(current, left)) a = a with { Weight = 8 };
        if (ForceAppointment(current, right)) b = b with { Weight = 8 };
        if (ForceCategory(current, left) || special(left)) a = a with { Weight = 1 };
        if (ForceCategory(current, right) || special(right)) b = b with { Weight = 1 };
        int one = a.Weight == 0 ? 5 : a.Weight, two = b.Weight == 0 ? 5 : b.Weight;
        return one == two ? DeploymentResponsibilityNameOrder.Before(Name(state, b.NamedArea), Name(state, a.NamedArea)) : one < two;
    }

    internal static bool BeforeSecondary(GameState state, SourceWorldContext current, int left, int right,
        int index, Func<int, bool> special, Func<int, int> additionalReader, Func<int, int> countReader,
        Func<int, int, int> entryReader, bool reversed = false)
    {
        if (index is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(index));
        if (left == right) return false;
        bool leftValid = K(current.ReadPersonNativeValidity(left), "person_validity");
        bool rightValid = K(current.ReadPersonNativeValidity(right), "person_validity");
        if (!leftValid) return false;
        if (!rightValid) return true;
        if (ForceCategory(current, left) || special(left)) return false;
        if (ForceCategory(current, right) || special(right)) return true;
        if (unchecked(additionalReader(left) + 1) < index + 1) return false;
        if (unchecked(additionalReader(right) + 1) < index + 1) return true;
        if (countReader(left) <= index) return false;
        int leftAreaIndex = entryReader(left, index);
        if (leftAreaIndex == 0) return false;
        if (reversed)
        {
            if (countReader(right) <= index || entryReader(right, index) == 0) return true;
            // 343940 delegates only this final populated pair, retaining every
            // earlier reverse-specific gate and its reads.
            return BeforeSecondary(state, current, right, left, index, special,
                additionalReader, countReader, entryReader);
        }
        var leftArea = Area(current, leftAreaIndex);
        if (countReader(right) <= index) return true;
        int rightAreaIndex = entryReader(right, index);
        if (rightAreaIndex == 0) return true;
        var rightArea = Area(current, rightAreaIndex);
        return DeploymentResponsibilityNameOrder.Before(Name(state, K(rightArea.PhysicalSlot, "area_pointer")),
            Name(state, K(leftArea.PhysicalSlot, "area_pointer")));
    }

    private static BaseReference City(SourceWorldContext current, int physical)
    {
        ushort first = K(current.ReadPersonFirstLocation(physical), "first_location");
        return first switch
        {
            >= 1 and <= 51 => current.ReadCityTableReference(first) ?? throw Missing("city_pointer"),
            >= 52 and <= 61 => CurrentGateMapQueries.FindCity(new(BaseKind.Gate, first - 51), current),
            _ => current.ReadCityTableReference(0) ?? throw Missing("city_zero")
        };
    }

    private static DeploymentPersonResponsibilityKey CityKey(SourceWorldContext current, BaseReference city, int id)
    {
        int weight = 0; int? name = null;
        if (K(current.ReadBaseNativeValidity(city), "city_validity"))
        {
            int cityPhysical = K(city.PhysicalSlot, "city_pointer");
            foreach (var area in current.ReadRegisteredAreaReferences() ?? throw Missing("area_registry"))
            {
                int at = K(area?.PhysicalSlot, "area_pointer");
                if (cityPhysical is < 1 or > 51 || K(current.ReadAreaCitySlot(new(at)), "area_city") != cityPhysical) continue;
                if (K(current.ReadAssignmentArea36(at), "area_appointment") == id) { weight = 1000; name = at; }
            }
            if (K(current.ReadAssignmentCityCA(cityPhysical), "recruitment_appointment") == id) weight = 100;
            if (K(current.ReadAssignmentCityCC(cityPhysical), "training_appointment") == id) weight = 10;
        }
        return new(weight, name);
    }

    private static bool ForceAppointment(SourceWorldContext current, int physical)
    {
        var force = current.ReadPersonForce(physical) ?? throw Missing("person_force");
        if (!K(current.ReadForceNativeValidity(force), "force_validity")) return false;
        int id = K(current.ReadPersonIdentity(physical), "person_identity");
        if (id == 0) return false;
        for (int index = 0; index < 3; index++)
            if (K(current.ReadForceAssociationPersonId(force, index), "force_appointment") == id) return true;
        return false;
    }

    private static bool ForceCategory(SourceWorldContext current, int physical) =>
        K((current.ReadPersonForce(physical) ?? throw Missing("person_force")).PhysicalSlot, "force_pointer") is >= 46 and <= 50;

    private static AreaReference Area(SourceWorldContext current, int index)
    {
        if (index < 0) throw Missing("negative_area_index");
        return current.ReadAreaTableReference(index <= 500 ? index : 0) ?? throw Missing("area_pointer");
    }

    private static string Name(GameState state, int? physical) => physical is int at
        ? state.Areas.GetValueOrDefault(at)?.Name ?? throw Missing("area_name") : "";
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("deployment.person-order.responsibility." + name);
}
