using San14.Core;

namespace San14.Data;

public sealed record OriginalArmyDestructionForce164Resolution(OriginalArmyDestructionForce164Packet? Packet,
    string? UnresolvedOwner);
public sealed class OriginalArmyDestructionForce164Packet
{
    public OriginalForceReference Force { get; }
    public int? Wounded18 { get; }
    public int? RequestedGain { get; }
    public int? Before164 { get; }
    public int? After164 { get; }
    public int? BeforeLevel { get; }
    public int? AfterLevel { get; }
    public int? PlayerDisplayGain { get; }
    public bool PlayerReachedLevelTen { get; }
    public bool LevelIncreased => BeforeLevel.HasValue && AfterLevel > BeforeLevel;
    public bool Writes164 => After164.HasValue;
    internal OriginalEventInitializationContext Owner { get; }
    internal OriginalCurrentListPool? PoolOwner { get; }
    internal OriginalCurrentListPool? PreparedPool { get; }
    internal OriginalArmyDestructionForce164Packet(OriginalEventInitializationContext owner, OriginalForceReference force,
        int? wounded = null, int? requested = null, int? before = null, int? after = null,
        int? oldLevel = null, int? newLevel = null, int? display = null, bool playerMaximum = false,
        OriginalCurrentListPool? pool = null, OriginalCurrentListPool? prepared = null)
    { Owner = owner; Force = force; Wounded18 = wounded; RequestedGain = requested; Before164 = before; After164 = after;
        BeforeLevel = oldLevel; AfterLevel = newLevel; PlayerDisplayGain = display; PlayerReachedLevelTen = playerMaximum;
        PoolOwner = pool; PreparedPool = prepared; }
}

/// <summary>259360 TC25C2xx→258A60→2236B0/22C030 only. Does not duplicate
/// prefix Force162, change Person traits or own the remaining winner callbacks.
/// State-effect6 borrows the real primary pool and is prepared atomically.</summary>
public static class OriginalArmyDestructionForce164
{
    public static OriginalArmyDestructionForce164Resolution Prepare(GameState state,
        OriginalEventInitializationContext current, OriginalArmyUnitReference victim,
        OriginalArmyUnitReference? winner = null, OriginalBaseReference? winnerBase = null)
    {
        try
        {
            //25C18x has set plVar18=null. Late Army Force wins initially;
            //a valid late Base then overrides it, even when Army is also valid.
            var force = new OriginalForceReference(null);
            bool winnerValid = ArmyValid(current, winner);
            if (winnerValid) force = current.ReadArmyUnitForceReference(winner!);
            if (winnerBase?.PhysicalSlot.HasValue == true && Get(current.ReadBaseNativeValidity(winnerBase), "late winner Base validity"))
            {
                int raw = Get(current.ReadBaseForceSlot(winnerBase), "late winner Base ForceB0");
                force = current.ReadForceTableReference(raw is >= 0 and <= 51 ? raw : 0) ?? throw Missing("late Base Force pointer");
            }
            if (!Get(current.ReadForceNativeValidity(force), "late winner Force validity")) return new(new(current, force), null);
            int physical = Get(current.ReadForcePhysicalSlot(force), "late winner Force physical");
            int wounded = Get(current.ReadArmyReturnResourceFields(victim)?.Wounded18, "victim Army word18");
            int scale = P(0x1A49060);
            if (scale == 0) throw Missing("Force164 division parameter1A49060 is zero");
            int traitTerm = 0;
            if (winnerValid)
            {
                ushort id = Get(current.ReadArmyUnitCommanderId(winner!), "late winner Army commander12");
                var person = id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
                if (person is null) throw Missing("late winner commander ID lookup");
                if (person.PhysicalSlot is int commander && Get(current.ReadPersonNativeValidity(commander), "late winner commander validity"))
                {
                    int effect = Get(OriginalTacticRemakeCurrentTraits.CommanderEffectSum(current, commander, 0x4C), "late winner commander effect4C");
                    traitTerm = unchecked(effect * P(0x1A490B4) * scale) / 100;
                }
            }
            var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("Force164 owned primary pool");
            var draft = current.CreateArmyReturnTransactionDraft();
            //The general return draft copies node storage, but City90's
            //actual registry binding is also an input of2AAEE0. Keep its real
            //descriptor identity over the detached chain; do not rebuild it
            //by enumerating Core cities.
            draft.SetRegistryDescriptor(OriginalRegistryKind.City, current.ReadRegistryDescriptor(OriginalRegistryKind.City));
            var preparedPool = pool.DetachForArmyReturn(draft);
            var advantage = OriginalPersonReturnPathSource.ReadForceAdvantage(state, draft, force, 6);
            if (advantage.Value is not int effect6) throw Missing(advantage.UnresolvedOwner ?? "Force164 source2ADBC0 effect6");
            int gain = unchecked((int)((long)wounded / (long)scale) + unchecked(effect6 * P(0x1A49BB8) * scale) / 100 + traitTerm);
            if (!state.NativeArmySimulation!.Forces.TryGetValue(physical, out var nativeForce) || nativeForce.Flags12 is not byte flags)
                throw Missing("late winner Force current byte12");
            if ((flags & 8) != 0) gain = unchecked(gain + unchecked(P(0x1A49794) * scale) / 100);
            //258A60 accepts nonnegative delta; the preceding real effect6
            //temporary lifecycle still executes when a negative result skips.
            if (gain < 0) return new(new(current, force, wounded, gain, pool: pool, prepared: preparedPool), null);
            int before = Get(current.ReadForce164(force), "late winner saved Force164");
            int oldLevel = Get(OriginalOwnedForce164Inputs.ReadLevel(state, before), "Force164 before level table0");
            int after = Math.Clamp(unchecked(before + gain), 0, 9999999);
            int newLevel = Get(OriginalOwnedForce164Inputs.ReadLevel(state, after), "Force164 after level table0");
            var player = current.CurrentPlayerForce ?? OriginalOwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("258A60 current player Force2338E0");
            int playerPhysical = Get(current.ReadForcePhysicalSlot(player), "258A60 player Force physical");
            bool isPlayer = playerPhysical != 0 && playerPhysical == physical;
            int? display = null;
            if (isPlayer && gain > 0 && oldLevel != 10)
            {
                int displayed = newLevel == 10 ? unchecked(Get(OriginalOwnedForce164Inputs.ReadMaximumThreshold(state), "Force164 maximum table threshold") - before) : gain;
                if (displayed > 0) display = displayed;
            }
            return new(new(current, force, wounded, gain, before, after, oldLevel, newLevel, display,
                isPlayer && oldLevel < newLevel && newLevel == 10, pool, preparedPool), null);

            int P(int tcRva) => Get(OriginalArmyDestructionParameterSeed.ReadCurrentTc(state, tcRva), $"Force164 parameter{tcRva:X}");
        }
        catch (MissingOwner missing) { return new(null, missing.Message); }
        catch (InvalidDataException missing) { return new(null, "current.Force164: " + missing.Message); }
    }

    public static void ApplyContext(OriginalArmyDestructionForce164Packet packet)
    {
        if (packet.PreparedPool is { } draft) packet.PoolOwner!.CommitArmyReturnPool(draft);
        if (packet.After164 is int after) packet.Owner.SetForce164(packet.Force.PhysicalSlot!.Value, after);
    }
    public static GameState PublishCurrent(GameState state, OriginalArmyDestructionForce164Packet packet)
    {
        if (packet.After164 is int after)
        {
            var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("Force164 publication has no saved parameter owner.");
            var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values)
            { [OriginalOwnedForce164Inputs.ForceValueKey(packet.Force.PhysicalSlot!.Value)] = after };
            state = state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = integers } } };
        }
        if (packet.PoolOwner is { } pool)
            state = state with { NativeArmyLifecycle = (state.NativeArmyLifecycle ?? throw new InvalidDataException("Force164 publication has no saved primary pool owner."))
                with { Pool = pool.ExportOwnedState() } };
        return state;
    }
    private static bool ArmyValid(OriginalEventInitializationContext current, OriginalArmyUnitReference? army)
        => army?.PhysicalSlot.HasValue == true && Get(current.ReadArmyUnitNativeValidity(army), "late winner Army validity");
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string name) => new("current.Force164: " + name);
    private sealed class MissingOwner(string name) : Exception(name);
}
