namespace San14.Data;

public sealed record OriginalCustomPersonalityReference(int? PhysicalSlot);
public sealed record OriginalCustomPersonalityRecord(int PhysicalSlot, int? TypeCode, ushort? TargetSlot,
    string? Name, uint? RawField38, uint? RawField3C, uint? RawField40,
    byte? RawField48, short? RawField4A, byte? RawField4C, short? RawField4E);

public interface IOriginalPersonalityManagerState : IOriginalPersonalityNormalizationState
{
    bool? ReadPersonalityManagerPresent();
    OriginalCustomPersonalityReference? ReadCustomPersonalityReference(int slot);
    int? ReadCustomPersonalityType(OriginalCustomPersonalityReference source);
    ushort? ReadCustomPersonalityTargetSlot(OriginalCustomPersonalityReference source);
    string? ReadCustomPersonalityName(OriginalCustomPersonalityReference source);
    uint? ReadCustomPersonalityUInt32(OriginalCustomPersonalityReference source, int offset);
    byte? ReadCustomPersonalityByte(OriginalCustomPersonalityReference source, int offset);
    short? ReadCustomPersonalityInt16(OriginalCustomPersonalityReference source, int offset);
    void WritePersonalityName(OriginalPersonalityReference target, string sourceCString);
    void WritePersonalityUInt32(OriginalPersonalityReference target, int offset, uint value);
}

/// <summary>2045B0 current custom trait scan,20A130 copy and204630 normalization.
/// Native heap/string addresses are owned managed values in this Data profile.
/// Unknown required inputs fail at their consumption point without rollback.</summary>
public static class OriginalPersonalityManager
{
    public static void Apply(IOriginalPersonalityManagerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Known(state.ReadPersonalityManagerPresent(),"manager singleton"))
            throw new InvalidDataException("Current personality manager is null at2045B0.");
        for (int slot = 0; slot < 100; slot++)
        {
            OriginalCustomPersonalityReference source = state.ReadCustomPersonalityReference(slot)
                ?? throw new InvalidDataException($"Current custom personality pointer{slot} is unknown.");
            if (!source.PhysicalSlot.HasValue) continue;
            if (Known(state.ReadCustomPersonalityType(source),"outer source type") == 0) continue;
            ushort requested = Known(state.ReadCustomPersonalityTargetSlot(source),"source target wordA");
            _ = Copy(source,requested,state);
        }
        OriginalPersonalityNormalization.ApplyManagerTable(state);
    }

    public static bool Copy(OriginalCustomPersonalityReference source, int requestedSlot, IOriginalPersonalityManagerState state)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(state);
        if (!source.PhysicalSlot.HasValue) return false;
        if (Known(state.ReadCustomPersonalityType(source),"copy source type") == 0) return false;
        //The original signed<=355 check has no negative clamp. Its normal
        //2045B0 caller supplies u16; an unsafe negative lookup remains a failure.
        int key = requestedSlot <= 355 ? requestedSlot : 0;
        OriginalPersonalityReference target = state.ReadPersonalityTableReference(key)
            ?? throw new InvalidDataException("Current personality copy target pointer is unknown.");
        if (!target.PhysicalSlot.HasValue) return false;
        string name = state.ReadCustomPersonalityName(source)
            ?? throw new InvalidDataException("Current custom personality CString is unknown.");
        state.WritePersonalityName(target,name);
        state.WritePersonalityByte(target,0xA4,(byte)Math.Min(Known(state.ReadCustomPersonalityUInt32(source,0x38),"source38"),100u));
        state.WritePersonalityUInt32(target,0xA8,Known(state.ReadCustomPersonalityUInt32(source,0x3C),"source3C"));
        state.WritePersonalityByte(target,0xAC,(byte)Math.Min(Known(state.ReadCustomPersonalityUInt32(source,0x40),"source40"),100u));
        state.WritePersonalityByte(target,0xB6,(byte)Math.Min((int)Known(state.ReadCustomPersonalityByte(source,0x48),"source48"),126));
        state.WritePersonalityWord(target,0xB8,unchecked((ushort)Math.Clamp((int)Known(state.ReadCustomPersonalityInt16(source,0x4A),"source4A"),-100,2000)));
        state.WritePersonalityByte(target,0xBA,(byte)Math.Min((int)Known(state.ReadCustomPersonalityByte(source,0x4C),"source4C"),126));
        state.WritePersonalityWord(target,0xBC,unchecked((ushort)Math.Clamp((int)Known(state.ReadCustomPersonalityInt16(source,0x4E),"source4E"),-100,2000)));
        _ = OriginalPersonalityNormalization.ApplyHelper(target,state);
        return true;
    }

    private static T Known<T>(T? value,string field) where T:struct
        => value ?? throw new InvalidDataException($"Current personality {field} is unknown.");
}
