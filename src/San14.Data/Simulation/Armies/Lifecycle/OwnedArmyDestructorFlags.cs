using San14.Core;

namespace San14.Data;

public static class OwnedArmyDestructorFlags
{
    public static GameState InstallFresh(GameState state)
    {
        if (state.ReadArmyLifecycle() is not { Slots.Count: 501, ArmyFlags150: null } arena ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0)
            throw new InvalidDataException("Army flags150 require the unused fresh constructor allocation.");
        //Actual2269E0, called for all501 receivers from326990. Normal creator
        //reuses this receiver; it does not replace this byte from a default.
        return state.WithArmyLifecycle(arena with { ArmyFlags150 = Enumerable.Repeat<byte?>(0, 501).ToArray() });
    }

    public static void BindCurrent(ArmyLifecycleSourceView arena, SourceWorldContext current)
    {
        if (arena.ArmyFlags150 is not { } flags) return;
        for (int i = 0; i < flags.Count; i++) current.SetArmyUnitFlags150(i, flags[i]);
    }

    public static bool SetSourceBit(SourceWorldContext current, int physical, int bit, bool enabled)
    {
        //2409E0 supports exactly bits0..4, retaining every other current bit.
        if (bit is < 0 or > 4) return true;
        if (current.ReadArmyUnitFlags150(new(physical)) is not byte before) return false;
        byte after = (byte)(before & ~(1 << bit));
        if (enabled) after |= (byte)(1 << bit);
        current.SetArmyUnitFlags150(physical, after);
        return true;
    }

    public static ArmyLifecycleSourceView PublishCurrent(ArmyLifecycleSourceView arena, SourceWorldContext current) =>
        arena.ArmyFlags150 is null ? arena : arena with { ArmyFlags150 = Enumerable.Range(0, 501)
            .Select(i => current.ReadArmyUnitFlags150(new(i))).ToArray() };
}
