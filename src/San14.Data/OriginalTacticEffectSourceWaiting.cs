using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Fresh current parameter values consumed by Army waiting assignment.
/// None is filled from an executable's image-initial globals.</summary>
public sealed record OriginalTacticArmyWaitingSourceInput(int SumOfNative42,
    int ChainCount, int FormationAdjustment, int ForceFlagReduction,
    int ComputedReduction, int CurrentPrimaryCap, int CurrentSecondCap,
    int CurrentThirdCap);

public sealed record OriginalTacticArmyWaitingSourceResult(int AverageNative42,
    byte Primary20, byte Second21, byte Third22);

/// <summary>Results of current native permission queries. OtherNativeGateAllowed
/// includes the outer17D450 target/action/player/commander checks; this record
/// does not replace them with a fabricated default.</summary>
public sealed record OriginalTacticOrdinaryArmyGateSourceInput(bool NativeArmyValid,
    byte ArmyStatus10, bool HasCurrentNativeTactics, bool SelectedTacticAvailable,
    byte CurrentWaiting20, byte CurrentAbnormal23, byte CurrentAbnormal25,
    bool Abnormal25TargetMatches, bool OtherNativeGateAllowed,
    bool ArmyFlags26Mask02, bool SpecialWaitingModeActive, int? CurrentOrdinaryWaitingRate);

/// <summary>The native trigger occurs at the beginning of the ten-day advance;
/// Core's SettleBeforeMovement reports and stores its result at NextPeriod.
/// Both boundaries are explicit so the ready-at calculation cannot hide an offset.</summary>
public sealed record OriginalTacticWaitingPeriodBoundaries(int TriggerPeriod,
    int SharedSettlementPeriod);

/// <summary>Pure waiting arithmetic and the inspected ordinary calendar cadence.
/// SC224110,173B78..173C92,44D884..44DB1D,33DF00..33DF6C; corresponding
/// current TC code is independently paired in the dispatch evidence note.</summary>
public static class OriginalTacticEffectSourceWaiting
{
    public const int OrdinaryUpdatesPerDay = 4;
    public const int OrdinaryUpdatesPerTenDayPeriod = 40;

    public static OriginalTacticArmyWaitingSourceResult AfterActivation(
        OriginalTacticArmyWaitingSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.SumOfNative42 < 0 || input.ChainCount < 0 ||
            input.ChainCount == int.MaxValue)
            throw new InvalidDataException("Current ordinary Army waiting aggregate is unsupported.");
        CurrentByteCap(input.CurrentPrimaryCap);
        CurrentByteCap(input.CurrentSecondCap);
        CurrentByteCap(input.CurrentThirdCap);
        // MOVD/CVTDQ2PS, DIVSS and CVTTSS2SI preserve the native float32 order.
        float average = (float)input.SumOfNative42 / (float)(input.ChainCount + 1);
        int nativeAverage = Truncate(average);
        int requested = unchecked(input.FormationAdjustment - input.ForceFlagReduction);
        requested = unchecked(requested - input.ComputedReduction);
        requested = unchecked(requested + nativeAverage);
        byte primary = (byte)Math.Clamp(requested, 0, input.CurrentPrimaryCap);
        return new(nativeAverage, primary, (byte)input.CurrentSecondCap,
            (byte)input.CurrentThirdCap);
    }

    /// <summary>Exact reached224110 byte arithmetic with both current cap reads
    /// explicit. Index0 rereads its cap; indices1/2 use the entry-captured values.
    /// This helper preserves int32 wrapping and final low-byte conversion.</summary>
    public static byte AddCounter(byte current, int signedAddend,
        int firstCapRead, int secondCapRead)
    {
        int added = unchecked(current + signedAddend);
        int first = added < 0 ? 0 : Math.Min(added, firstCapRead);
        int second = first < 0 ? 0 : Math.Min(first, secondCapRead);
        return unchecked((byte)second);
    }

    /// <summary>Scheduler44D680 observes even current hours because33DF00 adds2
    /// after each complete32-stage cycle. The ordinary waiting phase occurs at
    /// hours4,10,16,22; the writer advances them to6,12,18,next-day0.</summary>
    public static bool IsOrdinaryWaitingUpdateHour(int currentHour)
    {
        if (currentHour is < 0 or > 23 || (currentHour & 1) != 0)
            throw new InvalidDataException("Ordinary native calendar hour is unsupported.");
        int halfHourSlot = currentHour / 2;
        return halfHourSlot > 0 && (halfHourSlot + 1) % 3 == 0;
    }

    /// <summary>Ceiling to the next command boundary, for ordinary rate1 with
    /// Army26 mask02 and no special waiting branch. Native readiness may occur
    /// inside the intervening period; this function does not move that event.</summary>
    public static int CommandBoundaryPeriods(byte currentWaiting) =>
        (currentWaiting + OrdinaryUpdatesPerTenDayPeriod - 1) /
        OrdinaryUpdatesPerTenDayPeriod;

    /// <summary>Core uses readyAt=SharedSettlementPeriod+CooldownPeriods.
    /// Calculate the absolute native command boundary first, then subtract the
    /// supplied shared settlement boundary. Zero represents readiness by that boundary.</summary>
    public static int CoreCooldownPeriods(byte waitingAfterActivation,
        OriginalTacticWaitingPeriodBoundaries boundaries)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        if (boundaries.TriggerPeriod < 0 ||
            boundaries.SharedSettlementPeriod != checked(boundaries.TriggerPeriod + 1))
            throw new InvalidDataException("Original waiting conversion requires the explicit next-period settlement boundary.");
        int readyAtCommandBoundary = checked(boundaries.TriggerPeriod +
            CommandBoundaryPeriods(waitingAfterActivation));
        return Math.Max(0, readyAtCommandBoundary - boundaries.SharedSettlementPeriod);
    }

    public static bool NativeArmyGateAllows(OriginalTacticOrdinaryArmyGateSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.NativeArmyValid && input.ArmyStatus10 == 1 &&
            input.HasCurrentNativeTactics && input.SelectedTacticAvailable &&
            input.CurrentWaiting20 == 0 && input.CurrentAbnormal23 == 0 &&
            (input.CurrentAbnormal25 == 0 || input.Abnormal25TargetMatches) &&
            input.OtherNativeGateAllowed;
    }

    private static void CurrentByteCap(int value)
    {
        if (value is < 0 or > byte.MaxValue)
            throw new InvalidDataException("Current ordinary Army waiting cap is unsupported.");
    }

    private static int Truncate(float value) => !float.IsFinite(value) ||
        value >= 2147483648f || value < -2147483648f ? int.MinValue : (int)value;
}

/// <summary>Pinned ordinary Army activation route for complete opcode15/19/20/23
/// programs.16FBC0→17D450→17D5D0→172C10 assigns waiting and executes these
/// operators without a consumable deduction. This scoped fact does not describe
/// other triggers, tactics or command-book operations.</summary>
public sealed class OriginalTacticOrdinaryArmyUsageSourceProfile
{
    public const string Revision = "ce-current-ordinary-army-15-19-20-23-20261002";
    public string Language { get; }
    public string ExecutableSha256 { get; }

    public OriginalTacticOrdinaryArmyUsageSourceProfile(string language,
        string executableSha256)
    {
        string expected = language switch
        {
            "SC" => "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
            "TC" => "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
            _ => throw new InvalidDataException("Original ordinary Army usage language is unsupported.")
        };
        if (!expected.Equals(executableSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Original ordinary Army usage executable revision differs.");
        Language = language;
        ExecutableSha256 = expected;
    }

    public OriginalTacticEffectUsageSource Resolve(S14TacticEffectProgram program,
        OriginalTacticArmyWaitingSourceInput waiting,
        OriginalTacticOrdinaryArmyGateSourceInput gate,
        OriginalTacticWaitingPeriodBoundaries boundaries)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(gate);
        if (!gate.ArmyFlags26Mask02 || gate.SpecialWaitingModeActive ||
            gate.CurrentOrdinaryWaitingRate != 1)
            throw new InvalidDataException("Current Army waiting progression is outside the ordinary rate1 source profile.");
        ValidateSupported(program);
        OriginalTacticArmyWaitingSourceResult assigned =
            OriginalTacticEffectSourceWaiting.AfterActivation(waiting);
        int cooldown = OriginalTacticEffectSourceWaiting.CoreCooldownPeriods(
            assigned.Primary20, boundaries);
        bool allowed = OriginalTacticEffectSourceWaiting.NativeArmyGateAllows(gate);
        return new(cooldown, true, new(allowed,
            allowed ? null : "tactic.native_army_gate"),
            $"{Revision}:{Language}:{ExecutableSha256}");
    }

    /// <summary>Exact subday activation: retain all three source waiting bytes
    /// and let the shared native updater advance them at their proven boundaries.</summary>
    public OriginalTacticEffectUsageSource ResolveNative(S14TacticEffectProgram program,
        OriginalTacticArmyWaitingSourceInput waiting, OriginalTacticOrdinaryArmyGateSourceInput gate)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(gate);
        ValidateSupported(program);
        //17D5D0's activation gate does not require logistics flag02. That flag
        //controls later waiting progress, and is not a permission default. Raw
        //native assignment therefore does not borrow the period adapter's rate1
        //and logistics assumptions or predict a future readiness boundary.
        OriginalTacticArmyWaitingSourceResult assigned = OriginalTacticEffectSourceWaiting.AfterActivation(waiting);
        bool allowed = OriginalTacticEffectSourceWaiting.NativeArmyGateAllows(gate);
        string scoped = $"{Revision}:{Language}:{ExecutableSha256}";
        return new(0, true, new(allowed, allowed ? null : "tactic.native_army_gate"), scoped)
        {
            NativeActivationWaiting = new(assigned.Primary20, assigned.Second21, assigned.Third22,
                scoped)
        };
    }

    private static void ValidateSupported(S14TacticEffectProgram program)
    {
        if (program.TacticSlot is < 1 or > 200 || program.Instructions.Count != 2 ||
            program.Instructions[0].Index != 0 || program.Instructions[1].Index != 1 ||
            program.Instructions.Any(instruction => instruction.Opcode is not (0 or 15 or 19 or 20 or 23)) ||
            !program.Instructions.Any(instruction => instruction.Opcode is 15 or 19 or 20 or 23))
            throw new InvalidDataException("Ordinary Army no-consumable source profile requires a complete supported Army program.");
    }
}
