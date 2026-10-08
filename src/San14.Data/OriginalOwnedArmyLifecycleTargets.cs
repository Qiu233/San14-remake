using San14.Core;

namespace San14.Data;

public static class OriginalOwnedArmyLifecycleTargets
{
    public static GameState InstallFresh(GameState state)
    {
        if (state.NativeArmyLifecycle is not { Slots.Length: 501, Targets38: null } arena ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0)
            throw new InvalidDataException("Army targets require the unused fresh constructor allocation.");
        //2269E0 writes byte38=FF and word3A=0 for every501 receiver.
        return state with { NativeArmyLifecycle = arena with { Targets38 = Enumerable.Range(0, 501)
            .Select(_ => new NativeArmyLifecycleTargetState(0xFF, 0)).ToArray() } };
    }

    internal static NativeArmyLifecycleState PublishCreation(NativeArmyLifecycleState arena, int physical, byte? packetKind38,
        ushort? packetIdentity3A = null)
    {
        if (arena.Targets38 is null && packetKind38 is null) return arena;
        //A real new creator store can make just this old-save receiver known.
        //Other old receivers do not acquire constructor defaults.
        var targets = arena.Targets38?.ToArray() ?? Enumerable.Range(0, 501)
            .Select(_ => new NativeArmyLifecycleTargetState(null, null)).ToArray();
        //2D2CA0 writes low bytes of packetE/F. The current short packet owns
        //E=9. Its caller's F=param2 is not yet represented by this producer.
        targets[physical] = new(packetKind38, packetIdentity3A);
        return arena with { Targets38 = targets };
    }

    public static void BindCurrent(NativeArmyLifecycleState arena, OriginalEventInitializationContext current)
    {
        if (arena.Targets38 is not { } targets) return;
        for (int i = 0; i < targets.Length; i++) current.SetArmyLifecycleTargetFields(i, targets[i]);
    }

    public static NativeArmyLifecycleState PublishCurrent(NativeArmyLifecycleState arena, OriginalEventInitializationContext current) =>
        arena.Targets38 is null ? arena : arena with { Targets38 = Enumerable.Range(0, 501)
            .Select(i => current.ReadArmyLifecycleTargetFields(i) ?? new NativeArmyLifecycleTargetState(null, null)).ToArray() };
}

public sealed partial class OriginalEventInitializationContext
{
    private readonly Dictionary<int, NativeArmyLifecycleTargetState?> _armyLifecycleTargets38 = [];
    public void SetArmyLifecycleTargetFields(int physical, NativeArmyLifecycleTargetState? target)
    { Check(physical, 501, "Army target38 receiver"); _armyLifecycleTargets38[physical] = target; }
    public NativeArmyLifecycleTargetState? ReadArmyLifecycleTargetFields(int physical)
    { Check(physical, 501, "Army target38 receiver"); return _armyLifecycleTargets38.GetValueOrDefault(physical); }
}
