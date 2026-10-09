using San14.Core;

namespace San14.Data;

public sealed record ArmyChangedListsReceipt(string SourceBranch, bool CopiedPending,
    bool WorkCleared, int? StoppedArmyPhysicalSlot = null, int? AllocatedNode = null);
public sealed record ArmyChangedListsResult(GameState? State, ArmyChangedListsReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>0BD880/167900's two actual descriptors in the already owned
/// primary pool. Finite17AFB0/17A620 branches and the explicit source type2
/// route writer share one private operation; unsupported routes and an
/// externally running worker remain explicit unknowns.</summary>
public static class ArmyChangedListsSource
{
    public const string Revision = "san14:ce-army-changed-lists-167900-20261008";

    public static GameState InstallFresh(GameState state, SourceWorldContext current)
    {
        var arena = state.NativeArmyLifecycle ?? throw new InvalidDataException("current.changed_lists_primary_arena");
        if (arena.Indexes.ChangedProcessing is not null || state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0 })
            throw new InvalidDataException("Fresh changed-list constructor requires its unused owned campaign.");
        var pool = current.ReadOwnedCurrentListPool() ?? throw new InvalidDataException("current.changed_lists_primary_pool");
        pool.CreateChangedIndexes();
        return state with { NativeArmyLifecycle = arena with { Indexes = pool.ExportOwnedState() } };
    }
    /// <summary>Exact018CC0(...,1) tail append plus Army pointer store.
    /// It does not deduplicate or ask native validity before registering.</summary>
    public static ArmyChangedListsResult Append(GameState state, int armyPhysical)
    {
        if (armyPhysical is < 0 or > 500) return Unknown("current.changed_lists_Army_pointer");
        if (state.NativeArmyLifecycle is not { } arena || arena.Indexes.ChangedProcessing is null || arena.Indexes.ChangedPendingUnitIds is null)
            return Unknown("current.changed_lists_saved_owner");
        try
        {
            var current = new SourceWorldContext();
            var pool = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
            var at = current.AllocateRegistryNode(new(pool.ChangedPendingHandle!.Identity))
                ?? throw new Missing("current.changed_lists_append_node");
            if (at.Identity is null)
                return Success(state, new("097E10.no_allocated_node", false, false));
            current.WriteRegistryNodePayload(at, new(RegistryKind.Army, armyPhysical));
            return Success(state with { NativeArmyLifecycle = arena with { Indexes = pool.ExportOwnedState() } },
                new("097E10.append_tail", false, false, AllocatedNode: at.Identity));
        }
        catch (Missing error) { return Unknown(error.Message); }
        catch (InvalidDataException error) { return Unknown(error.Message); }
    }

    public static ArmyChangedListsResult TryAdvanceFinite(GameState state)
        => TryAdvanceFinite(state, new SourceWorldContext());

    /// <summary>The optional view supplies only actually reached233560
    /// queries. The pool mutations always use a private import of this State's
    /// existing primary owner, preserving atomic failure and current headers.</summary>
    public static ArmyChangedListsResult TryAdvanceFinite(GameState state,
        SourceWorldContext currentQueries,
        Func<GameState, SourceWorldContext, UnitState, TransportArmyRouteResult>? rewriteTransportRoute = null)
    {
        if (state.NativeArmyLifecycle is not { } arena || arena.Indexes.ChangedProcessing is null)
            return Unknown("current.changed_lists_saved_owner");
        if (arena.Indexes.ChangedProcessing is null) return Unknown("current.changed_lists_Busy90");
        if (arena.Indexes.ChangedProcessing == true) return Unknown("current.changed_lists_running_worker_completion");
        try
        {
            var pending = arena.Indexes.ChangedPendingUnitIds ?? throw new Missing("current.changed_pending_units");
            if (pending.Count == 0) return Success(state, new("17AFB0.no_pending", false, false));
            var current = rewriteTransportRoute is null ? new SourceWorldContext() : currentQueries;
            var pool = rewriteTransportRoute is null ? WorldIndexContext.ImportOwnedState(current, arena.Indexes, state) :
                current.ReadOwnedCurrentListPool() ?? throw new Missing("current.changed_routes_index_owner");
            var batch = pool.BeginChangedWork();
            var copied = batch.ChangedWorkingUnitIds!;
            var working = state;
            foreach (var payload in copied)
            {
                int? physical = pool.ArmyPhysical(payload);
                if (physical is null or 0) continue;
                if (arena.Slots.Length != 501 || physical is < 1 or > 500)
                    throw new Missing("current.changed_lists_Army_header");
                var slot = working.NativeArmyLifecycle!.Slots[physical.Value];
                if (slot.Commander12 is not ushort commander)
                    throw new Missing("current.changed_lists_Army12");
                if (commander == 0) continue;
                if (slot.Status10 is not byte status) throw new Missing("current.changed_lists_Army10");
                if (status == 0) continue;
                bool? excluded = OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, currentQueries, physical.Value);
                if (excluded is null) throw new Missing("current.changed_lists_Army233560");
                if (excluded.Value) continue;
                if (status == 2)
                {
                    if (arena.ArmyFlags150 is not { Length: 501 } flags || flags[physical.Value] is not byte raw150)
                        throw new Missing("current.changed_lists_Army150_bit3");
                    if ((raw150 & 8) != 0) return PublishStopped(physical.Value, "17A620.type2_bit3_return");
                    var image = slot.ActiveUnitId is int id ? working.Units.GetValueOrDefault(id) : slot.PendingImage;
                    if (image?.NativeTacticRuntime?.Flags26 is not byte raw26)
                        throw new Missing("current.changed_lists_Army26_bit02");
                    if ((raw26 & 2) == 0) return PublishStopped(physical.Value, "17A620.type2_disconnected_return");
                    if (rewriteTransportRoute is not null && slot.ActiveUnitId is int live && image is not null)
                    {
                        var rewritten = rewriteTransportRoute(working, current, image);
                        if (rewritten.Receipt is not { } route) throw new Missing(rewritten.UnresolvedOwner!);
                        if (route.ArmyPhysical != physical.Value) throw new Missing("current.changed_lists_route_Army_pointer");
                        var transport = image.NativeTransport;
                        if (route.NextHex48Write.HasValue && transport is null)
                            throw new Missing("current.changed_lists_transport48_owner");
                        var changed = image with { NativeRouteGoal1F8 = route.Goal1F8Write ?? image.NativeRouteGoal1F8,
                            NativeTransport = transport is null ? null : transport with {
                                CurrentNextHex48 = route.NextHex48Write ?? transport.CurrentNextHex48 } };
                        working = working with { Units = new(working.Units) { [live] = changed } };
                        continue;
                    }
                }
                throw new Missing($"current.changed_lists_Army{physical.Value}_301DD0_route_writer");
            }
            pool.CompleteChangedWork();
            return Publish(new("17A620.reached_null_clear_work", true, true));

            ArmyChangedListsResult PublishStopped(int physical, string sourceBranch)
            { pool.ChangedProcessing = false; return Publish(new(sourceBranch, true, false, physical)); }
            ArmyChangedListsResult Publish(ArmyChangedListsReceipt receipt)
                => Success(working with { NativeArmyLifecycle = working.NativeArmyLifecycle! with { Indexes = pool.ExportOwnedState() } }, receipt);
        }
        catch (Missing error) { return Unknown(error.Message); }
        catch (InvalidDataException error) { return Unknown(error.Message); }
    }

    private static ArmyChangedListsResult Success(GameState state, ArmyChangedListsReceipt receipt)
        => new(state, receipt, null);
    private static ArmyChangedListsResult Unknown(string owner) => new(null, null, owner);
    private sealed class Missing(string owner) : Exception(owner);
}
