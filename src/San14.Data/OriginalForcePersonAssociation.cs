namespace San14.Data;

public sealed record OriginalAssociationRemovalReport(OriginalForceReference OldForce,
    int PersonPhysicalSlot, int AssociationSlot, OriginalForceReference TargetForce);

public interface IOriginalForcePersonAssociationState
{
    bool? ReadForceNativeValidity(OriginalForceReference force);
    bool? ReadPersonNativeValidity(int physicalSlot);
    ushort? ReadPersonIdentity(int physicalSlot);
    ushort? ReadForceAssociationPersonId(OriginalForceReference force, int index);
    byte? ReadForceAssociationTargetSlot(OriginalForceReference force, int index);
    OriginalForceReference? ReadForceTableReference(int slot);
    void WriteForceAssociationTargetSlot(OriginalForceReference force, int index, byte value);
    void WriteForceAssociationPersonId(OriginalForceReference force, int index, ushort value);
    OriginalForceReference? ReadCurrentPlayerForce();
    int? ReadForcePhysicalSlot(OriginalForceReference force);
    void EmitAssociationRemovalReport(OriginalAssociationRemovalReport report);
}

/// <summary>277550 with actual transfer arguments R8=0/R9=1, including mandatory player report.</summary>
public static class OriginalForcePersonAssociation
{
    public static bool Clear(OriginalForceReference oldForce, int personPhysicalSlot,
        IOriginalForcePersonAssociationState state)
    {
        ArgumentNullException.ThrowIfNull(oldForce);
        ArgumentNullException.ThrowIfNull(state);
        if (!Known(state.ReadForceNativeValidity(oldForce), "old force validity")) return false;
        if (!Known(state.ReadPersonNativeValidity(personPhysicalSlot), "person validity")) return false;
        ushort id = Known(state.ReadPersonIdentity(personPhysicalSlot), "current person ID");
        if (id == 0) return false;
        int found = -1;
        for (int index = 0; index < 3; index++)
        {
            if (Known(state.ReadForceAssociationPersonId(oldForce, index), $"association person{index}") != id) continue;
            found = index; break;
        }
        if (found == -1) return false;
        byte target = Known(state.ReadForceAssociationTargetSlot(oldForce, found), "association target");
        OriginalForceReference targetForce = state.ReadForceTableReference(target <= 51 ? target : 0)
            ?? throw new InvalidDataException("Current association target force pointer is unknown.");
        if (!Known(state.ReadForceNativeValidity(targetForce), "association target validity")) return false;
        state.WriteForceAssociationTargetSlot(oldForce, found, 0);
        state.WriteForceAssociationPersonId(oldForce, found, 0);
        OriginalForceReference player = state.ReadCurrentPlayerForce()
            ?? throw new InvalidDataException("Current player force pointer is unknown after association removal.");
        int playerPhysical = Known(state.ReadForcePhysicalSlot(player), "current player force physical index");
        if (playerPhysical != 0 && playerPhysical == Known(state.ReadForcePhysicalSlot(oldForce), "old force physical index"))
            state.EmitAssociationRemovalReport(new(oldForce, personPhysicalSlot, found, targetForce));
        return true;
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current force-person association input {field} is unknown.");
}
