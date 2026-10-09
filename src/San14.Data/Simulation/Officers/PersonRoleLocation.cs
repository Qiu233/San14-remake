namespace San14.Data;

public sealed record NativeForce(int PhysicalSlot, ushort RawRuler10);
// Missing reference = unknown; a reference with no physical slot = observed null pointer.
public sealed record ForceReference(int? PhysicalSlot);

public interface IPersonRoleLocationState
{
    byte? ReadPersonStatus(int physicalSlot);
    ForceReference? ReadPersonForce(int physicalSlot);
    bool? ReadForceNativeValidity(ForceReference force);
    int? ReadForcePhysicalSlot(ForceReference force);
    void WritePersonStatus(int physicalSlot, byte status);
    ushort? ReadPersonFirstLocation(int physicalSlot);
    void WritePersonSecondLocation(int physicalSlot, ushort location);
}

/// <summary>
/// The 224DB4..224E7E lost-force branch of CE person slot7. Inputs are current
/// native identities; Core activity and static affiliation are not substitutes.
/// Subsequent district synchronization and position branches remain separate.
/// </summary>
public static class PersonRoleLocation
{
    public static bool ApplyLostForce(int personPhysicalSlot, IPersonRoleLocationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (personPhysicalSlot is < 0 or >= 1851)
            throw new InvalidDataException("Person physical slot is outside supported CE source profile.");
        byte status = Known(state.ReadPersonStatus(personPhysicalSlot), "person status11E");
        if (status is < 1 or > 4) return false;
        ForceReference force = state.ReadPersonForce(personPhysicalSlot)
            ?? throw new InvalidDataException("Current person force pointer is unknown.");
        if (Known(state.ReadForceNativeValidity(force), "force validity")) return false;
        // These are two separate getter calls on the saved force receiver.
        int first = Known(state.ReadForcePhysicalSlot(force), "force physical index first read");
        CheckForce(first);
        if (first == 51) return false;
        int second = Known(state.ReadForcePhysicalSlot(force), "force physical index second read");
        CheckForce(second);
        if (second is >= 46 and <= 50) return false;
        state.WritePersonStatus(personPhysicalSlot, 5);
        ushort place = Known(state.ReadPersonFirstLocation(personPhysicalSlot), "current person first location11A");
        state.WritePersonSecondLocation(personPhysicalSlot, (ushort)Math.Min((int)place, 1061));
        return true;
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current person post-load input {field} is unknown.");
    private static void CheckForce(int physical)
    {
        if (physical is < 0 or >= 52)
            throw new InvalidDataException("Force physical slot is outside supported native profile.");
    }
}
