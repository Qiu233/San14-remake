namespace San14.Data;

public sealed record OriginalPersonalityReference(int? PhysicalSlot);

public interface IOriginalPersonalityNormalizationState
{
    OriginalPersonalityReference? ReadPersonalityTableReference(int slot);
    int? ReadPersonalityPhysicalSlot(OriginalPersonalityReference personality);
    byte? ReadPersonalityByte(OriginalPersonalityReference personality, int offset);
    void WritePersonalityByte(OriginalPersonalityReference personality, int offset, byte value);
    void WritePersonalityWord(OriginalPersonalityReference personality, int offset, ushort value);
}

/// <summary>Complete game-field effects of217C60 and the356-entry204630 loop.
/// CPersonalityData is original officer trait data, separate from future ruler AI.
/// Current fields and the original helper/inline write order remain explicit.</summary>
public static class OriginalPersonalityNormalization
{
    // Exact two-stage switch tables in the supported CE code profile.
    private static readonly HashSet<int> Query4E0False = [0,15,16,17,18,33,34,43,44,45,50,63,64,65,66,68,74,77,100,113,114,119,120,122,123,125,126];
    private static readonly HashSet<int> Query420False = [0,4,5,6,7,8,14,19,28,29,30,49,51,55,57,82,108];
    public static bool Query55C4E0(int code) => !Query4E0False.Contains(code);
    public static bool Query55C3E0(int code) => Query55C4E0(code) && code is not (0 or 32 or 94);
    public static bool Query55C420(int code) => Query55C4E0(code) && !Query420False.Contains(code);

    public static void ApplyManagerTable(IOriginalPersonalityNormalizationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        for (int slot = 0; slot < 356; slot++)
        {
            OriginalPersonalityReference personality = state.ReadPersonalityTableReference(slot)
                ?? throw new InvalidDataException($"Current personality table pointer{slot} is unknown.");
            _ = Apply(personality,state,inline:true);
        }
    }

    public static bool ApplyHelper(OriginalPersonalityReference personality, IOriginalPersonalityNormalizationState state)
        => Apply(personality,state,inline:false);

    private static bool Apply(OriginalPersonalityReference personality, IOriginalPersonalityNormalizationState state, bool inline)
    {
        ArgumentNullException.ThrowIfNull(personality);ArgumentNullException.ThrowIfNull(state);
        int Physical() => state.ReadPersonalityPhysicalSlot(personality)
            ?? throw new InvalidDataException("Current personality physical identity is unknown.");
        byte Byte(int offset) => state.ReadPersonalityByte(personality,offset)
            ?? throw new InvalidDataException($"Current personality byte{offset:X} is unknown.");
        void Flag(byte mask, bool enabled)
        {
            byte old = Byte(0xBF);
            state.WritePersonalityByte(personality,0xBF,enabled ? (byte)(old|mask) : (byte)(old&~mask));
        }

        if (Physical() < 256 || Physical() > 355) return false;
        //204630 has two outer identity checks followed by two inline checks.
        if (inline && (Physical() < 256 || Physical() > 355)) return false;
        byte firstCode = Byte(0xB6);
        if (firstCode == 0) return false;
        byte kind = Byte(0xA4);
        state.WritePersonalityByte(personality,0xBE,5);
        bool special = kind == 30;
        ushort word = kind switch
        {
            30 => 12,
            39 or 40 => ushort.MaxValue,
            0 => Query55C4E0(firstCode) || Query55C4E0(Byte(0xBA)) ? ushort.MaxValue : (ushort)0,
            _ => 0
        };
        if (special)
        {
            Flag(4,true);
            state.WritePersonalityWord(personality,0xB4,word);
        }
        else if (inline)
        {
            //Inline204630 stores B4 before the current BF read/clear.
            state.WritePersonalityWord(personality,0xB4,word);
            Flag(4,false);
        }
        else
        {
            //Helper217C60 clears BF before storing the saved B4 result.
            Flag(4,false);
            state.WritePersonalityWord(personality,0xB4,word);
        }
        Flag(2,Query55C3E0(Byte(0xB6)) || Query55C3E0(Byte(0xBA)));
        Flag(8,Query55C420(Byte(0xB6)) || Query55C420(Byte(0xBA)));
        Flag(16,true);
        return true;
    }
}
