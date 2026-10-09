namespace San14.Data;

/// <summary>Actual receiver fields for the selected tag1 record of mode3.
/// Flag140 is the source command override byte, not Flags26 or Abnormal25.</summary>
public sealed record TransportArmyRetargetCurrent(int ArmyPhysicalSlot, bool? ArmyValid,
    byte? Status10, byte? Action37, byte? Flag140, byte? Abnormal25, byte? Flags26, byte? Choice41);

/// <summary>Selections after7DA2B0 has finalized the five user waypoint words.
/// Null options retain the current7D8390 packet values. These waypoints are
/// not the automatically generated route or Core movement preview.</summary>
public sealed record TransportArmyRetargetCitySelection(int CityPhysicalSlot, bool? CityValid,
    IReadOnlyList<int> Waypoints2C, int? Choice41 = null, bool? ReturnToSource = null);

/// <summary>Scalar output of7F6870(tag1), followed by7F66D0(tag1).
/// The changed-list insertion lies after NextHex48 and before the option
/// writes. This receipt requests that actual insertion; it does not create
/// its saved owner or claim that301DD0 has executed.</summary>
public sealed record TransportArmyRetargetReceipt(int ArmyPhysicalSlot, byte Action37,
    byte TargetKind38, ushort TargetIdentity3A, IReadOnlyList<ushort> Waypoints2C,
    bool ClearOverride3C3E, ushort NextHex48, int ChangedArmyList18PhysicalSlot,
    byte Choice41, byte Flags26);

public sealed record TransportArmyRetargetResult(TransportArmyRetargetReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>Finite player existing-transport City setter. It prepares actual
/// source scalar writes without changing a GameState, allocating an Army,
/// charging resources/books, rebuilding a path, or publishing a partial
/// command. The caller owns the real mode3 selection and changed-list owner.</summary>
public static class TransportArmyRetargetSource
{
    public static TransportArmyRetargetResult PrepareCity(TransportArmyRetargetCurrent current,
        TransportArmyRetargetCitySelection selected)
    {
        if (current.ArmyPhysicalSlot is < 1 or > 500 || current.ArmyValid is null)
            return Unknown("current.transport_retarget_Army_receiver");
        if (!current.ArmyValid.Value) return Unknown("branch.transport_retarget_invalid_Army");
        if (current.Status10 is null) return Unknown("current.transport_retarget_Status10");
        if (current.Status10 != 2) return Unknown("branch.transport_retarget_tag1_type2");
        if (current.Flag140 is null) return Unknown("current.transport_retarget_byte140");
        //The native locked branch leaves action/target/options untouched, but
        //clears all waypoints and still resets48/enqueues. It is not a usable
        //target change and must not be reported as an accepted retarget.
        if (current.Flag140 != 0) return Unknown("branch.transport_retarget_locked140");
        if (current.Action37 is null) return Unknown("current.transport_retarget_Action37");
        if (current.Action37 == 17) return Unknown("branch.transport_retarget_old17_word13E");
        if (selected.CityPhysicalSlot is < 1 or > 51 || selected.CityValid is null)
            return Unknown("current.transport_retarget_City_receiver");
        if (!selected.CityValid.Value) return Unknown("branch.transport_retarget_invalid_City");
        if (selected.Waypoints2C is not { Count: 5 })
            return Unknown("current.transport_retarget_five_selected_waypoints");
        if (current.Abnormal25 is null) return Unknown("current.transport_retarget_Abnormal25");
        if (current.Flags26 is not byte flags) return Unknown("current.transport_retarget_Flags26");
        int? choice = selected.Choice41 ?? current.Choice41;
        if (choice is null) return Unknown("current.transport_retarget_Choice41");

        var waypoints = new ushort[5];
        for (int i = 0; i < waypoints.Length; i++)
        {
            int value = selected.Waypoints2C[i];
            waypoints[i] = value == 0xBD10 ? (ushort)0xBD10 : (ushort)Math.Clamp(value, 0, 48399);
        }
        bool returnToSource = selected.ReturnToSource ?? (flags & 0x20) != 0;
        flags = (byte)(flags & 0xDF);
        if (returnToSource) flags |= 0x20;
        return new(new(current.ArmyPhysicalSlot, 2, 5, (ushort)selected.CityPhysicalSlot,
            Array.AsReadOnly(waypoints), current.Abnormal25 == 0, 0xBD10, current.ArmyPhysicalSlot,
            (byte)Math.Clamp(choice.Value, 0, 2), flags), null);
    }

    private static TransportArmyRetargetResult Unknown(string owner) => new(null, owner);
}
