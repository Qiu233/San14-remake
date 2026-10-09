namespace San14.Data;

public interface IPersonBaseSyncState : IBaseLookupState
{
    ushort? ReadPersonFirstLocation(int physicalSlot);
    byte? ReadPersonOldForceSlot(int physicalSlot);
    byte? ReadPersonStatus(int physicalSlot);
    bool? ReadBaseNativeValidity(BaseReference reference);
    int? ReadBaseForceSlot(BaseReference reference);
    int? ReadBaseDistrictSlot(BaseReference reference);
    byte? ReadPersonDistrictSlot(int physicalSlot);
    void TransferPersonDistrict(int physicalSlot, int requestedDistrict);
}

public sealed record PersonBaseSyncResult(BaseReference Base, byte CachedOldForceSlot,
    IReadOnlyList<int> RequestedDistricts);

/// <summary>224E7E..224F4B. A transfer provider must perform the real cleanup and refresh effects.</summary>
public static class PersonBaseSync
{
    public static PersonBaseSyncResult Apply(int personPhysicalSlot, IPersonBaseSyncState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (personPhysicalSlot is < 0 or >= 1851)
            throw new InvalidDataException("Person physical slot is outside supported CE profile.");
        ushort first = Known(state.ReadPersonFirstLocation(personPhysicalSlot), "first location11A");
        BaseReference selected = BaseLookup.Find(first, state);
        byte oldForce = Known(state.ReadPersonOldForceSlot(personPhysicalSlot), "cached old force byte");
        byte status = Known(state.ReadPersonStatus(personPhysicalSlot), "current status11E");
        var requests = new List<int>();
        if (status is >= 1 and <= 4 && Known(state.ReadBaseNativeValidity(selected), "base validity") && oldForce is >= 1 and <= 51)
        {
            if (Known(state.ReadBaseForceSlot(selected), "current base force") != oldForce)
            {
                int request = Known(state.ReadBaseDistrictSlot(selected), "base district first request");
                state.TransferPersonDistrict(personPhysicalSlot, request);
                requests.Add(request);
            }
            byte currentDistrict = Known(state.ReadPersonDistrictSlot(personPhysicalSlot), "current district118 after first transfer");
            int compared = Known(state.ReadBaseDistrictSlot(selected), "base district comparison");
            if (compared != currentDistrict)
            {
                int request = Known(state.ReadBaseDistrictSlot(selected), "base district second request");
                state.TransferPersonDistrict(personPhysicalSlot, request);
                requests.Add(request);
            }
        }
        return new(selected, oldForce, requests.AsReadOnly());
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current person base-sync input {field} is unknown.");
}
