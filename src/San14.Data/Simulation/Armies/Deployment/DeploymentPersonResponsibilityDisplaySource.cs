using San14.Core;

namespace San14.Data;

internal static class DeploymentPersonResponsibilityDisplaySource
{
    internal static DeploymentPersonResponsibility Read(GameState state, SourceWorldContext current, int physical)
        => Read(state, current, physical,
            person => K(PersonAssignmentPrecallInputs.ReadCurrentAreaAssignmentActiveCount(state, person), "area_assignment_count"),
            (person, _) => K(PersonAssignmentPrecallInputs.ReadCurrentAreaAssignmentFirstValue(state, person), "area_assignment_first"));

    internal static DeploymentPersonResponsibility Read(GameState state, SourceWorldContext current, int physical,
        Func<int, int> countReader, Func<int, int, int> entryReader)
    {
        var force = current.ReadPersonForce(physical) ?? throw Missing("person_force");
        int forcePhysical = K(force.PhysicalSlot, "force_pointer");
        if (forcePhysical is >= 46 and <= 50) return new(DeploymentPersonResponsibilityKind.NotApplicable);
        ushort id = K(current.ReadPersonIdentity(physical), "person_identity");
        bool special = unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
            unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 || unchecked((uint)(id - 4191)) <= 9;
        if (special && K(current.ReadPersonFirstLocation(physical), "special_first_location") is >= 1 and <= 61)
            return new(DeploymentPersonResponsibilityKind.NotApplicable);

        ushort first = K(current.ReadPersonFirstLocation(physical), "first_location");
        BaseReference city = first switch
        {
            >= 1 and <= 51 => current.ReadCityTableReference(first) ?? throw Missing("city_pointer"),
            >= 52 and <= 61 => CurrentGateMapQueries.FindCity(new(BaseKind.Gate, first - 51), current),
            _ => current.ReadCityTableReference(0) ?? throw Missing("city_zero")
        };
        int count = countReader(physical);
        int areaIndex = count < 1 ? 0 : entryReader(physical, 0);
        if (areaIndex < 0) throw Missing("negative_area_index");
        var area = current.ReadAreaTableReference(areaIndex <= 500 ? areaIndex : 0) ?? throw Missing("area_pointer");
        if (K(current.ReadAreaNativeValidity(area), "area_validity"))
        {
            int at = K(area.PhysicalSlot, "area_pointer");
            if (state.Areas.GetValueOrDefault(at)?.Name is null) throw Missing("area_name");
            return new(DeploymentPersonResponsibilityKind.AreaName, at);
        }
        // Display reads City0 appointments too; there is no extra City validity gate.
        int cityPhysical = K(city.PhysicalSlot, "city_pointer");
        if (K(current.ReadAssignmentCityCA(cityPhysical), "recruitment_appointment") == id)
            return new(DeploymentPersonResponsibilityKind.Recruit);
        if (K(current.ReadAssignmentCityCC(cityPhysical), "training_appointment") == id)
            return new(DeploymentPersonResponsibilityKind.Train);
        if (K(current.ReadForceNativeValidity(force), "force_validity") && id != 0)
            for (int index = 0; index < 3; index++)
                if (K(current.ReadForceAssociationPersonId(force, index), "force_appointment") == id)
                    return new(DeploymentPersonResponsibilityKind.Spy);
        return new(DeploymentPersonResponsibilityKind.Empty);
    }

    internal static DeploymentPersonResponsibility ReadSecondary(GameState state, SourceWorldContext current,
        int physical, int index, Func<int, int> additionalReader, Func<int, int> countReader,
        Func<int, int, int> entryReader)
    {
        if (index is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(index));
        int capacity = unchecked(additionalReader(physical) + 1);
        if (index == 1 ? capacity == 1 : capacity != 3)
            return new(DeploymentPersonResponsibilityKind.NotApplicable);
        var force = current.ReadPersonForce(physical) ?? throw Missing("person_force");
        if (K(force.PhysicalSlot, "force_pointer") is >= 46 and <= 50)
            return new(DeploymentPersonResponsibilityKind.NotApplicable);
        ushort id = K(current.ReadPersonIdentity(physical), "person_identity");
        bool special = unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
            unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 || unchecked((uint)(id - 4191)) <= 9;
        if (special && K(current.ReadPersonFirstLocation(physical), "special_first_location") is >= 1 and <= 61)
            return new(DeploymentPersonResponsibilityKind.NotApplicable);
        ushort first = K(current.ReadPersonFirstLocation(physical), "first_location");
        if (first is >= 52 and <= 61)
            _ = CurrentGateMapQueries.FindCity(new(BaseKind.Gate, first - 51), current);
        else if (first is < 1 or > 51) return new(DeploymentPersonResponsibilityKind.Empty);
        int areaIndex = countReader(physical) <= index ? 0 : entryReader(physical, index);
        if (areaIndex < 0) throw Missing("negative_area_index");
        var area = current.ReadAreaTableReference(areaIndex <= 500 ? areaIndex : 0) ?? throw Missing("area_pointer");
        if (!K(current.ReadAreaNativeValidity(area), "area_validity"))
            return new(DeploymentPersonResponsibilityKind.Empty);
        int at = K(area.PhysicalSlot, "area_pointer");
        if (state.Areas.GetValueOrDefault(at)?.Name is null) throw Missing("area_name");
        return new(DeploymentPersonResponsibilityKind.AreaName, at);
    }
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("deployment.person-basic.responsibility." + name);
}
