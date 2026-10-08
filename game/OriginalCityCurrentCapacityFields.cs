using San14.Data;

/// <summary>Current campaign queries for the city detail panel. Unknown saved
/// fields stay unknown; this does not consult an opening observation table.</summary>
public sealed record OriginalCityCurrentCapacityFields(int? Durability, int? Troops,
    int? Morale, IReadOnlyList<string> Unavailable)
{
    public static OriginalCityCurrentCapacityFields Capture(int cityId, IOriginalBasePostLoadState? current)
    {
        if (current is null) return new(null, null, null, ["current-campaign-fields-unavailable"]);
        var reference = new OriginalBaseReference(OriginalBaseKind.City, cityId);
        int? durability = null, troops = null, morale = null;
        var unavailable = new List<string>();
        try
        {
            OriginalBaseCapacities capacity = OriginalBasePostLoad.ReadCapacities(reference, current);
            (durability, troops) = (capacity.Field46, capacity.Troops);
        }
        catch (InvalidDataException error) { unavailable.Add("capacity: " + error.Message); }
        try
        {
            int forceSlot = current.ReadBaseForceSlot(reference)
                ?? throw new InvalidDataException("Current city force is unknown.");
            OriginalForceReference force = current.ReadForceTableReference(forceSlot <= 51 ? forceSlot : 0)
                ?? throw new InvalidDataException("Current city force pointer is unknown.");
            morale = OriginalBasePostLoad.ReadMoraleUpper(force, current);
        }
        catch (InvalidDataException error) { unavailable.Add("morale: " + error.Message); }
        return new(durability, troops, morale, unavailable.AsReadOnly());
    }
}
