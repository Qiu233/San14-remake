namespace San14.Data;

public interface IOriginalPersonDistrictTransferState
{
    byte? ReadPersonDistrictSlot(int physicalSlot);
    OriginalForceReference? ReadPersonForce(int physicalSlot);
    OriginalForceReference? ReadRequestedDistrictForce(int requestedDistrict);
    void RemoveForcePersonAssociation(OriginalForceReference oldForce, int personPhysicalSlot);
    int? ReadForcePhysicalSlot(OriginalForceReference force);
    void WritePersonResetWord1FF(int physicalSlot, ushort value);
    void WritePersonResetByte1FE(int physicalSlot, byte value);
    void WritePersonDistrictSlot(int physicalSlot, byte value);
    byte? ReadDistrictForceWithoutValidity(int districtSlot);
    OriginalForceReference? ReadCurrentPlayerForce();
    void RefreshTransferService(int first, int second, int third);
}

/// <summary>Complete 2400D0 field/dispatch order; callbacks must supply real cleanup and service effects.</summary>
public static class OriginalPersonDistrictTransfer
{
    public static void Apply(int personPhysicalSlot, int requestedDistrict, IOriginalPersonDistrictTransferState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (personPhysicalSlot is < 0 or >= 1851)
            throw new InvalidDataException("Transfer person physical slot is outside supported CE profile.");
        byte current = Known(state.ReadPersonDistrictSlot(personPhysicalSlot), "current district118");
        if (current != requestedDistrict)
        {
            OriginalForceReference oldForce = state.ReadPersonForce(personPhysicalSlot)
                ?? throw new InvalidDataException("Current old force pointer is unknown.");
            if (requestedDistrict < 0)
                throw new InvalidDataException("Negative requested district would index before the native pointer table.");
            OriginalForceReference targetForce = state.ReadRequestedDistrictForce(requestedDistrict)
                ?? throw new InvalidDataException("Current requested district force pointer is unknown.");
            state.RemoveForcePersonAssociation(oldForce, personPhysicalSlot);
            int oldPhysical = Known(state.ReadForcePhysicalSlot(oldForce), "saved old force physical index");
            int targetPhysical = Known(state.ReadForcePhysicalSlot(targetForce), "saved target force physical index");
            if (oldPhysical != targetPhysical)
            {
                state.WritePersonResetWord1FF(personPhysicalSlot, 0);
                state.WritePersonResetByte1FE(personPhysicalSlot, 0);
            }
        }
        byte written = (byte)Math.Clamp(requestedDistrict, 0, 51);
        state.WritePersonDistrictSlot(personPhysicalSlot, written);
        if (written == 0) return;
        byte force = Known(state.ReadDistrictForceWithoutValidity(written), "post-write district raw force byte");
        if (force is < 1 or > 51) return;
        OriginalForceReference player = state.ReadCurrentPlayerForce()
            ?? throw new InvalidDataException("Current transfer service player force is unknown.");
        if (force == Known(state.ReadForcePhysicalSlot(player), "current service player force physical index"))
            state.RefreshTransferService(1, 1, 0);
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current district transfer input {field} is unknown.");
}
