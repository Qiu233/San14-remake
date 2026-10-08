namespace San14.Data;

/// <summary>Ordinary creator SC2D3077/TC2D31F7 calls the actual second-location
/// setter SC2421A0/TC241F20 with kind27 and the selected physical Army ID.
/// Publication belongs to that same deployment transaction, before registration.
/// No Core Unit ID or first/home location participates in this encoding.</summary>
public static class OriginalPersonArmyLocationSource
{
    public const string ProfileId = "san14:ce-person-army-second-location-20261003";
    public static ushort EncodeArmySecondLocation(int physicalArmySlot)
        => unchecked((uint)(physicalArmySlot - 1)) <= 499 ? (ushort)(physicalArmySlot + 61) : (ushort)0;
}
