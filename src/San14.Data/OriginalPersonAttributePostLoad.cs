namespace San14.Data;

public interface IOriginalPersonAttributePostLoadState
{
    int? ReadPersonPostLoadPhysicalSlot(int receiver);
    ushort? ReadPersonIdentity(int receiver);
    byte? ReadPersonPostLoadByte(int receiver, int offset);
    void WritePersonPostLoadByte(int receiver, int offset, byte value);
}

/// <summary>The224800 cap prefix and its post-skill cost/ability block.
/// The intervening formation/trait/tactic stages and later position stages
/// are required separately; these operators alone are not a complete slot7.</summary>
public static class OriginalPersonAttributePostLoad
{
    public static void WriteCaps(int receiver, IOriginalPersonAttributePostLoadState state)
    {
        foreach (int offset in new[] { 0x224, 0x225 })
        {
            byte physical = unchecked((byte)Known(state.ReadPersonPostLoadPhysicalSlot(receiver), "fresh person physical slot"));
            ushort id = Known(state.ReadPersonIdentity(receiver), "fresh person ID10");
            int parity = id & 1;
            if (parity == 0) parity = (id - 1) & 1;
            byte raw = (byte)(parity * 100 + (offset == 0x224 ? 20 : 0));
            byte mixed = (byte)(raw ^ physical);
            byte encoded = (byte)((mixed << 5) | (mixed >> 3));
            state.WritePersonPostLoadByte(receiver, offset, encoded);
        }
    }
    public static void NormalizeCostAndAbilities(int receiver, IOriginalPersonAttributePostLoadState state)
    {
        byte cost = Known(state.ReadPersonPostLoadByte(receiver, 0x1BA), "person policy base cost1BA");
        state.WritePersonPostLoadByte(receiver, 0x1BA, (byte)Math.Min((int)cost, 6));
        for (int offset = 0x124; offset <= 0x128; offset++)
        {
            byte physical = unchecked((byte)Known(state.ReadPersonPostLoadPhysicalSlot(receiver), "fresh person ability physical slot"));
            byte encoded = Known(state.ReadPersonPostLoadByte(receiver, 0x225), "current encoded person cap225");
            byte cap = (byte)(((encoded << 3) | (encoded >> 5)) ^ physical);
            byte raw = Known(state.ReadPersonPostLoadByte(receiver, offset), "current person base ability");
            state.WritePersonPostLoadByte(receiver, offset, raw < 1 ? (byte)1 : Math.Min(raw, cap));
        }
    }
    private static T Known<T>(T? value, string field) where T : struct
        => value ?? throw new InvalidDataException($"Current {field} is unknown.");
}
