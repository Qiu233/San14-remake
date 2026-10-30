namespace San14.Data;

// Missing reference is unknown; a reference with no physical slot is an observed null pointer.
public sealed record TransferPersonReference(int? PhysicalSlot);

/// <summary>The four consumed fields from one original system-time snapshot, not world game time.</summary>
public sealed record TransferServiceClockSnapshot(int? YearsSince1900,
    int? ZeroBasedMonth, int? Day, int? Hour);

public sealed record TransferServiceDate(ushort? Year, byte? Month, byte? Day,
    byte? Hour, byte? RawByte5);

public enum TransferServicePlatformPolicy { Unknown, Absent, Callback }

public interface IServiceFlagQueries
{
    bool? ReadTransferServiceEligibility();
}

public interface ITransferServiceQueries : IServiceFlagQueries
{
    ForceReference? ReadCurrentPlayerForce();
    bool? ReadForceNativeValidity(ForceReference force);
    int? ReadForcePhysicalSlot(ForceReference force);
    TransferPersonReference? ReadTransferPersonTableReference(int tableSlot);
    bool? ReadPersonNativeValidity(int physicalSlot);
    byte? ReadPersonStatus(int physicalSlot);
    byte? ReadPersonCurrentRawForceSlot(int physicalSlot);
}

public interface ITransferServiceState
{
    void WriteDirtyFlag(byte value);
    bool? ReadPrimaryFlag(int index);
    // Merge this bit into CURRENT state; do not restore other bits from a previously captured word.
    void WritePrimaryFlag(int index, bool value);
    bool? ReadPlatformPresent();
    void NotifyPlatform(int index);
    TransferServiceClockSnapshot? CaptureClockSnapshot();
    TransferServiceClockSnapshot? CaptureClockSnapshotForFlag(int index) => CaptureClockSnapshot();
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
public sealed class TransferServiceState : ITransferServiceState
{
    private readonly bool?[] _primary = new bool?[64], _secondary = new bool?[64];
    private readonly bool?[] _events = new bool?[1024];
    //Independent application discovery bits, not achievements or campaign history.
    private readonly bool?[] _mainCommandSeen = new bool?[13];
    private readonly bool?[] _completedScenarios = new bool?[200];
    private readonly uint?[] _endingCompletion = new uint?[3];
    private readonly bool?[] _bonusOfficers = new bool?[61];
    private readonly bool?[] _tutorialCompleted = new bool?[20];
    private readonly TransferServiceDate[] _dates = Enumerable.Range(0, 57)
        .Select(_ => new TransferServiceDate(null, null, null, null, null)).ToArray();

    public byte? DirtyFlag { get; private set; }
    public TransferServicePlatformPolicy PlatformPolicy { get; set; }
    public Action<int>? PlatformNotifier { get; set; }
    public Func<TransferServiceClockSnapshot?>? ClockSnapshotProvider { get; set; }
    public Func<int, TransferServiceClockSnapshot?>? AchievementClockSnapshotProvider { get; set; }

    /// <summary>Operation-local copy. External bindings are retained, but no
    /// bitmap, date or dirty write can affect the supplied service owner.</summary>
    public TransferServiceState Copy()
    {
        var copy = new TransferServiceState { PlatformPolicy = PlatformPolicy,
            PlatformNotifier = PlatformNotifier, ClockSnapshotProvider = ClockSnapshotProvider,
            AchievementClockSnapshotProvider = AchievementClockSnapshotProvider };
        Array.Copy(_primary, copy._primary, _primary.Length);
        Array.Copy(_secondary, copy._secondary, _secondary.Length);
        Array.Copy(_events, copy._events, _events.Length);
        Array.Copy(_mainCommandSeen, copy._mainCommandSeen, _mainCommandSeen.Length);
        Array.Copy(_completedScenarios, copy._completedScenarios, _completedScenarios.Length);
        Array.Copy(_endingCompletion, copy._endingCompletion, _endingCompletion.Length);
        Array.Copy(_bonusOfficers, copy._bonusOfficers, _bonusOfficers.Length);
        Array.Copy(_tutorialCompleted, copy._tutorialCompleted, _tutorialCompleted.Length);
        Array.Copy(_dates, copy._dates, _dates.Length);
        copy.DirtyFlag = DirtyFlag;
        return copy;
    }

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
        Array.Fill(_events, false);
        Array.Fill(_mainCommandSeen, false);
        //Current3EC350 reset clears the200 scenario bits and three uint tracks.
        Array.Fill(_completedScenarios, false);
        Array.Fill(_endingCompletion, 0u);
        Array.Fill(_bonusOfficers, false);
        //3EC350 clears the profile's tutorial completion word at byte24.
        Array.Fill(_tutorialCompleted, false);
        for (int index = 0; index < _dates.Length; index++)
            _dates[index] = new(0, 1, 1, 0, _dates[index].RawByte5);
        WriteDirtyFlag(1);
    }

    public bool? ReadPrimaryFlag(int index) { Check(index, 64, "primary bitmap bit"); return _primary[index]; }
    public bool? ReadMainCommandSeen(int index) { Check(index, 13, "main command discovery"); return _mainCommandSeen[index]; }
    public void SetMainCommandSeen(int index, bool? value) { Check(index, 13, "main command discovery"); _mainCommandSeen[index] = value; }
    public bool? ReadSecondaryFlag(int index) { Check(index, 64, "secondary bitmap bit"); return _secondary[index]; }
    public bool? ReadEventFlag(int index) { Check(index, 1024, "event history bit"); return _events[index]; }
    public bool? ReadScenarioCompleted(int index) { Check(index, 200, "scenario completion bit"); return _completedScenarios[index]; }
    public void SetScenarioCompleted(int index, bool? value) { Check(index, 200, "scenario completion bit"); _completedScenarios[index] = value; }
    public uint? ReadEndingTrack(int index) { Check(index, 3, "Ending completion track"); return _endingCompletion[index]; }
    public void SetEndingTrack(int index, uint? value)
    {
        Check(index, 3, "Ending completion track");
        if (value is > 2) throw new InvalidDataException("Ending completion track is outside its source range.");
        _endingCompletion[index] = value;
    }
    public bool? ReadBonusOfficerUnlocked(int index) { Check(index, 61, "bonus officer unlock"); return _bonusOfficers[index]; }
    public void SetBonusOfficerUnlocked(int index, bool? value) { Check(index, 61, "bonus officer unlock"); _bonusOfficers[index] = value; }
    public bool? ReadTutorialCompleted(int index) { Check(index, 20, "tutorial completion bit"); return _tutorialCompleted[index]; }
    public void SetTutorialCompleted(int index, bool? value) { Check(index, 20, "tutorial completion bit"); _tutorialCompleted[index] = value; }
    public void SetEventFlag(int index, bool? value) { Check(index, 1024, "event history bit"); _events[index] = value; }
    public void SetPrimaryFlag(int index, bool? value) { Check(index, 64, "primary bitmap bit"); _primary[index] = value; }
    public void SetSecondaryFlag(int index, bool? value) { Check(index, 64, "secondary bitmap bit"); _secondary[index] = value; }
    public void WritePrimaryFlag(int index, bool value) => SetPrimaryFlag(index, value);
    public void WriteDirtyFlag(byte value) => DirtyFlag = value;
    public TransferServiceDate ReadDate(int index) { Check(index, 57, "service date"); return _dates[index]; }
    public void SetDate(int index, TransferServiceDate value)
    { Check(index, 57, "service date"); ArgumentNullException.ThrowIfNull(value); _dates[index] = value; }
    public void WriteDateYear(int index, ushort value) => SetDate(index, ReadDate(index) with { Year = value });
    public void WriteDateMonth(int index, byte value) => SetDate(index, ReadDate(index) with { Month = value });
    public void WriteDateDay(int index, byte value) => SetDate(index, ReadDate(index) with { Day = value });
    public void WriteDateHour(int index, byte value) => SetDate(index, ReadDate(index) with { Hour = value });
    public bool? ReadPlatformPresent() => PlatformPolicy switch
    {
        TransferServicePlatformPolicy.Unknown => null,
        TransferServicePlatformPolicy.Absent => false,
        TransferServicePlatformPolicy.Callback => true,
        _ => throw new InvalidDataException("Unsupported transfer service platform policy.")
    };
    public void NotifyPlatform(int index)
    {
        if (PlatformNotifier is null) throw new InvalidDataException("Current transfer service platform callback is unknown.");
        PlatformNotifier(index);
    }
    public TransferServiceClockSnapshot? CaptureClockSnapshot() => ClockSnapshotProvider?.Invoke();
    public TransferServiceClockSnapshot? CaptureClockSnapshotForFlag(int index) => AchievementClockSnapshotProvider is { } provider
        ? provider(index) : CaptureClockSnapshot();
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
public static class TransferService
{
    public static bool Refresh(ITransferServiceQueries queries, ITransferServiceState state)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(state);
        if (!Known(queries.ReadTransferServiceEligibility(), "refresh eligibility")) return false;
        ForceReference player = queries.ReadCurrentPlayerForce()
            ?? throw new InvalidDataException("Current transfer service player force pointer is unknown.");
        if (!Known(queries.ReadForceNativeValidity(player), "current player validity")) return false;
        int count = 0;
        for (int tableSlot = 0; tableSlot < 1851; tableSlot++)
        {
            TransferPersonReference person = queries.ReadTransferPersonTableReference(tableSlot)
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
    public static void SetFlag(int index, IServiceFlagQueries queries, ITransferServiceState state)
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
        TransferServiceClockSnapshot clock = state.CaptureClockSnapshotForFlag(index)
            ?? throw new InvalidDataException("Current transfer service system-time snapshot is unknown.");
        state.WriteDateYear(index, unchecked((ushort)(Known(clock.YearsSince1900, "snapshot tm_year") + 1900)));
        state.WriteDateMonth(index, unchecked((byte)(Known(clock.ZeroBasedMonth, "snapshot tm_mon") + 1)));
        state.WriteDateDay(index, unchecked((byte)Known(clock.Day, "snapshot tm_mday")));
        state.WriteDateHour(index, unchecked((byte)Known(clock.Hour, "snapshot tm_hour")));
    }

    private static T Known<T>(T? value, string name) where T : struct => value
        ?? throw new InvalidDataException($"Current transfer service input {name} is unknown.");
}
