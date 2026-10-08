namespace San14.Data;

// Missing reference is unknown; a reference with no physical slot is an observed null pointer.
public sealed record OriginalTransferPersonReference(int? PhysicalSlot);

/// <summary>The four consumed fields from one original system-time snapshot, not world game time.</summary>
public sealed record OriginalTransferServiceClockSnapshot(int? YearsSince1900,
    int? ZeroBasedMonth, int? Day, int? Hour);

public sealed record OriginalTransferServiceDate(ushort? Year, byte? Month, byte? Day,
    byte? Hour, byte? RawByte5);

public enum OriginalTransferServicePlatformPolicy { Unknown, Absent, Callback }

public interface IOriginalServiceFlagQueries
{
    bool? ReadTransferServiceEligibility();
}

public interface IOriginalTransferServiceQueries : IOriginalServiceFlagQueries
{
    OriginalForceReference? ReadCurrentPlayerForce();
    bool? ReadForceNativeValidity(OriginalForceReference force);
    int? ReadForcePhysicalSlot(OriginalForceReference force);
    OriginalTransferPersonReference? ReadTransferPersonTableReference(int tableSlot);
    bool? ReadPersonNativeValidity(int physicalSlot);
    byte? ReadPersonStatus(int physicalSlot);
    byte? ReadPersonCurrentRawForceSlot(int physicalSlot);
}

public interface IOriginalTransferServiceState
{
    void WriteDirtyFlag(byte value);
    bool? ReadPrimaryFlag(int index);
    // Merge this bit into CURRENT state; do not restore other bits from a previously captured word.
    void WritePrimaryFlag(int index, bool value);
    bool? ReadPlatformPresent();
    void NotifyPlatform(int index);
    OriginalTransferServiceClockSnapshot? CaptureClockSnapshot();
    void WriteDateYear(int index, ushort value);
    void WriteDateMonth(int index, byte value);
    void WriteDateDay(int index, byte value);
    void WriteDateHour(int index, byte value);
}

/// <summary>
/// Partial local fields of the independent service. Construction, explicit reset,
/// clock and platform bindings are separate from scenario materialization.
/// This is a Data compatibility profile, not a Core save or native persistence reader.
/// </summary>
public sealed class OriginalTransferServiceState : IOriginalTransferServiceState
{
    private readonly bool?[] _primary = new bool?[64], _secondary = new bool?[64];
    private readonly OriginalTransferServiceDate[] _dates = Enumerable.Range(0, 57)
        .Select(_ => new OriginalTransferServiceDate(null, null, null, null, null)).ToArray();

    public byte? DirtyFlag { get; private set; }
    public OriginalTransferServicePlatformPolicy PlatformPolicy { get; set; }
    public Action<int>? PlatformNotifier { get; set; }
    public Func<OriginalTransferServiceClockSnapshot?>? ClockSnapshotProvider { get; set; }

    /// <summary>Only the 31A780 date construction effects; does not reset either bitmap or byte274.</summary>
    public void InitializeConstructedDates()
    {
        for (int index = 0; index < _dates.Length; index++) _dates[index] = new(0, 1, 1, 0, 0);
    }

    /// <summary>Consumed 3EB960 reset fields. Date byte5 and external bindings are preserved.</summary>
    public void ResetLocalFields()
    {
        Array.Fill(_primary, false);
        Array.Fill(_secondary, false);
        for (int index = 0; index < _dates.Length; index++)
            _dates[index] = new(0, 1, 1, 0, _dates[index].RawByte5);
        WriteDirtyFlag(1);
    }

    public bool? ReadPrimaryFlag(int index) { Check(index, 64, "primary bitmap bit"); return _primary[index]; }
    public bool? ReadSecondaryFlag(int index) { Check(index, 64, "secondary bitmap bit"); return _secondary[index]; }
    public void SetPrimaryFlag(int index, bool? value) { Check(index, 64, "primary bitmap bit"); _primary[index] = value; }
    public void SetSecondaryFlag(int index, bool? value) { Check(index, 64, "secondary bitmap bit"); _secondary[index] = value; }
    public void WritePrimaryFlag(int index, bool value) => SetPrimaryFlag(index, value);
    public void WriteDirtyFlag(byte value) => DirtyFlag = value;
    public OriginalTransferServiceDate ReadDate(int index) { Check(index, 57, "service date"); return _dates[index]; }
    public void SetDate(int index, OriginalTransferServiceDate value)
    { Check(index, 57, "service date"); ArgumentNullException.ThrowIfNull(value); _dates[index] = value; }
    public void WriteDateYear(int index, ushort value) => SetDate(index, ReadDate(index) with { Year = value });
    public void WriteDateMonth(int index, byte value) => SetDate(index, ReadDate(index) with { Month = value });
    public void WriteDateDay(int index, byte value) => SetDate(index, ReadDate(index) with { Day = value });
    public void WriteDateHour(int index, byte value) => SetDate(index, ReadDate(index) with { Hour = value });
    public bool? ReadPlatformPresent() => PlatformPolicy switch
    {
        OriginalTransferServicePlatformPolicy.Unknown => null,
        OriginalTransferServicePlatformPolicy.Absent => false,
        OriginalTransferServicePlatformPolicy.Callback => true,
        _ => throw new InvalidDataException("Unsupported transfer service platform policy.")
    };
    public void NotifyPlatform(int index)
    {
        if (PlatformNotifier is null) throw new InvalidDataException("Current transfer service platform callback is unknown.");
        PlatformNotifier(index);
    }
    public OriginalTransferServiceClockSnapshot? CaptureClockSnapshot() => ClockSnapshotProvider?.Invoke();
    private static void Check(int value, int count, string field)
    {
        if (value < 0 || value >= count) throw new InvalidDataException($"Transfer service {field} is outside supported native profile.");
    }
}

/// <summary>
/// Verified 3E73F0(1,1,0) and its local 3EFEB0(index,1,0) effects.
/// Refresh returns whether the full count reaches200, not whether a flag was newly written.
/// Unknown needed inputs fail where consumed, preserving completed writes.
/// </summary>
public static class OriginalTransferService
{
    public static bool Refresh(IOriginalTransferServiceQueries queries, IOriginalTransferServiceState state)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(state);
        if (!Known(queries.ReadTransferServiceEligibility(), "refresh eligibility")) return false;
        OriginalForceReference player = queries.ReadCurrentPlayerForce()
            ?? throw new InvalidDataException("Current transfer service player force pointer is unknown.");
        if (!Known(queries.ReadForceNativeValidity(player), "current player validity")) return false;
        int count = 0;
        for (int tableSlot = 0; tableSlot < 1851; tableSlot++)
        {
            OriginalTransferPersonReference person = queries.ReadTransferPersonTableReference(tableSlot)
                ?? throw new InvalidDataException($"Current transfer service person table pointer {tableSlot} is unknown.");
            if (person.PhysicalSlot is not int physical) continue;
            if (physical is < 0 or >= 1851) throw new InvalidDataException("Transfer service person pointer is outside supported CE profile.");
            if (!Known(queries.ReadPersonNativeValidity(physical), $"person {physical} validity")) continue;
            byte status = Known(queries.ReadPersonStatus(physical), $"person {physical} status");
            if (status is < 1 or > 4) continue;
            int playerPhysical = Known(queries.ReadForcePhysicalSlot(player), "saved player physical identity");
            if (playerPhysical is < 0 or >= 52) throw new InvalidDataException("Transfer service force identity is outside supported CE profile.");
            byte force = Known(queries.ReadPersonCurrentRawForceSlot(physical), $"person {physical} current raw force");
            if (force == playerPhysical) count++;
        }
        if (count < 200) return false;
        SetFlag(17, queries, state);
        for (int bit = 1; bit <= 37; bit++)
            if (!Known(state.ReadPrimaryFlag(bit), $"current completion bit {bit}")) return true;
        SetFlag(0, queries, state);
        return true;
    }

    /// <summary>3EFEB0(index,1,0). No platform action is assumed absent and no clock is invented.</summary>
    public static void SetFlag(int index, IOriginalServiceFlagQueries queries, IOriginalTransferServiceState state)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(state);
        if (index is < 0 or > 56) return;
        if (!Known(queries.ReadTransferServiceEligibility(), "flag-set eligibility")) return;
        state.WriteDirtyFlag(1);
        bool oldBit = Known(state.ReadPrimaryFlag(index), $"old service bit {index}");
        state.WritePrimaryFlag(index, false);
        state.WritePrimaryFlag(index, true);
        if (Known(state.ReadPlatformPresent(), "platform presence")) state.NotifyPlatform(index);
        if (oldBit) return;
        OriginalTransferServiceClockSnapshot clock = state.CaptureClockSnapshot()
            ?? throw new InvalidDataException("Current transfer service system-time snapshot is unknown.");
        state.WriteDateYear(index, unchecked((ushort)(Known(clock.YearsSince1900, "snapshot tm_year") + 1900)));
        state.WriteDateMonth(index, unchecked((byte)(Known(clock.ZeroBasedMonth, "snapshot tm_mon") + 1)));
        state.WriteDateDay(index, unchecked((byte)Known(clock.Day, "snapshot tm_mday")));
        state.WriteDateHour(index, unchecked((byte)Known(clock.Hour, "snapshot tm_hour")));
    }

    private static T Known<T>(T? value, string name) where T : struct => value
        ?? throw new InvalidDataException($"Current transfer service input {name} is unknown.");
}
