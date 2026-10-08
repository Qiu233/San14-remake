using San14.Core;

namespace San14.Data;

public sealed record OriginalArmyChangedListsReceipt(string SourceBranch, bool CopiedPending,
    bool WorkCleared, int? StoppedArmyPhysicalSlot = null, int? AllocatedNode = null);
public sealed record OriginalArmyChangedListsResult(GameState? State, OriginalArmyChangedListsReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>0BD880/167900's two actual descriptors in the already owned
/// primary pool. Finite17AFB0/17A620 branches and the explicit source type2
/// route writer share one private operation; unsupported routes and an
/// externally running worker remain explicit unknowns.</summary>
public static class OriginalArmyChangedListsSource
{
    public const string Revision = "san14:ce-army-changed-lists-167900-20261008";

    public static GameState InstallFresh(GameState state, OriginalEventInitializationContext current)
    {
        var arena = state.NativeArmyLifecycle ?? throw new InvalidDataException("current.changed_lists_primary_arena");
        if (arena.ChangedLists is not null || state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0 })
            throw new InvalidDataException("Fresh changed-list constructor requires its unused owned campaign.");
        var pool = current.ReadOwnedCurrentListPool() ?? throw new InvalidDataException("current.changed_lists_primary_pool");
        //167900 writes each container+8=0 then01ABD0. Descriptor allocation
        //does not reset a retained row; its real current pool owns that row.
        var work = pool.AllocateDescriptor(); var pending = pool.AllocateDescriptor();
        var owner = new NativeArmyChangedListsState(Revision, new(work.Identity), new(pending.Identity), 0);
        return state with { NativeArmyLifecycle = arena with { Pool = pool.ExportOwnedState(), ChangedLists = owner } };
    }

    /// <summary>Exact018CC0(...,1) tail append plus Army pointer store.
    /// It does not deduplicate or ask native validity before registering.</summary>
    public static OriginalArmyChangedListsResult Append(GameState state, int armyPhysical)
    {
        if (armyPhysical is < 0 or > 500) return Unknown("current.changed_lists_Army_pointer");
        if (state.NativeArmyLifecycle is not { ChangedLists: { } owner } arena)
            return Unknown("current.changed_lists_saved_owner");
        try
        {
            var current = new OriginalEventInitializationContext();
            var pool = OriginalCurrentListPool.ImportOwnedState(current, arena.Pool);
            var at = current.AllocateRegistryNode(new(owner.Pending18.Identity))
                ?? throw new Missing("current.changed_lists_append_node");
            if (at.Identity is null)
                return Success(state, new("097E10.no_allocated_node", false, false));
            current.WriteRegistryNodePayload(at, new(OriginalRegistryKind.Army, armyPhysical));
            return Success(state with { NativeArmyLifecycle = arena with { Pool = pool.ExportOwnedState() } },
                new("097E10.append_tail", false, false, AllocatedNode: at.Identity));
        }
        catch (Missing error) { return Unknown(error.Message); }
        catch (InvalidDataException error) { return Unknown(error.Message); }
    }

    public static OriginalArmyChangedListsResult TryAdvanceFinite(GameState state)
        => TryAdvanceFinite(state, new OriginalEventInitializationContext());

    /// <summary>The optional view supplies only actually reached233560
    /// queries. The pool mutations always use a private import of this State's
    /// existing primary owner, preserving atomic failure and current headers.</summary>
    public static OriginalArmyChangedListsResult TryAdvanceFinite(GameState state,
        OriginalEventInitializationContext currentQueries,
        Func<GameState, OriginalEventInitializationContext, UnitState, OriginalTransportArmyRouteResult>? rewriteTransportRoute = null)
    {
        if (state.NativeArmyLifecycle is not { ChangedLists: { } owner } arena)
            return Unknown("current.changed_lists_saved_owner");
        if (owner.Busy90 is null) return Unknown("current.changed_lists_Busy90");
        if (owner.Busy90 != 0) return Unknown("current.changed_lists_running_worker_completion");
        try
        {
            var pending = ReadPointers(arena.Pool, owner.Pending18);
            if (pending.Count == 0) return Success(state, new("17AFB0.no_pending", false, false));
            //The route caller supplies one private writable capture of this
            //state. Its allocator owns both queue and actual301DD0 scratch.
            var current = rewriteTransportRoute is null ? new OriginalEventInitializationContext() : currentQueries;
            var pool = rewriteTransportRoute is null ? OriginalCurrentListPool.ImportOwnedState(current, arena.Pool) :
                current.ReadOwnedCurrentListPool() ?? throw new Missing("current.changed_lists_route_primary_pool");
            var working = state;
            var work = new OriginalHexListHandle(owner.Work8.Identity);
            current.ClearTemporaryHexList(work); //179FF0 replaces the old work chain.
            var copied = new List<NativeSharedListPayload>();
            foreach (var payload in pending)
            {
                var node = current.AllocateRegistryNode(new(owner.Work8.Identity))
                    ?? throw new Missing("current.changed_lists_copy_node");
                if (node.Identity is null) continue; //018CC0 failure skips only this pointer store.
                if (payload.Kind == "Null") current.SetHexListNodePayload(new(node.Identity), null);
                else current.WriteRegistryNodePayload(node, new(OriginalRegistryKind.Army, payload.Identity));
                copied.Add(payload);
            }
            current.ClearTemporaryHexList(new(owner.Pending18.Identity));
            //17AFB0 starts its worker and writes90=1. This finite synchronous
            //operation completes that same reached worker and joins to90=0.
            foreach (var payload in copied)
            {
                int? physical = payload.Kind == "Null" ? null : payload.Identity;
                if (physical is null or 0) continue;
                if (arena.Slots.Length != 501 || physical is < 1 or > 500)
                    throw new Missing("current.changed_lists_Army_header");
                var slot = working.NativeArmyLifecycle!.Slots[physical.Value];
                if (slot.Commander12 is not ushort commander)
                    throw new Missing("current.changed_lists_Army12");
                if (commander == 0) continue;
                if (slot.Status10 is not byte status) throw new Missing("current.changed_lists_Army10");
                if (status == 0) continue;
                bool? excluded = OriginalOwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, currentQueries, physical.Value);
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
            current.ClearTemporaryHexList(work); //17A620 reaches null; only this path clears+8.
            return Publish(new("17A620.reached_null_clear_work", true, true));

            OriginalArmyChangedListsResult PublishStopped(int physical, string sourceBranch)
                => Publish(new(sourceBranch, true, false, physical));
            OriginalArmyChangedListsResult Publish(OriginalArmyChangedListsReceipt receipt)
                => Success(working with { NativeArmyLifecycle = working.NativeArmyLifecycle! with { Pool = pool.ExportOwnedState(),
                    ChangedLists = owner with { Busy90 = 0 } } }, receipt);
        }
        catch (Missing error) { return Unknown(error.Message); }
        catch (InvalidDataException error) { return Unknown(error.Message); }
    }

    private static List<NativeSharedListPayload> ReadPointers(NativeSharedListPoolState pool, NativeSharedListHandle descriptor)
    {
        if (descriptor.Identity is not int key) return [];
        if (!pool.Descriptors.TryGetValue(key, out var row)) throw new Missing("current.changed_lists_descriptor");
        var result = new List<NativeSharedListPayload>();
        for (int? at = row.Head; at is int node;)
        {
            if (!pool.Nodes.TryGetValue(node, out var value) || value.Payload is not { } payload)
                throw new Missing("current.changed_lists_node_payload");
            if (payload.Kind is not ("Registry:Army" or "Null"))
                throw new Missing("current.changed_lists_Army_pointer_kind");
            result.Add(payload); at = value.Next;
        }
        return result;
    }

    private static OriginalArmyChangedListsResult Success(GameState state, OriginalArmyChangedListsReceipt receipt)
        => new(state, receipt, null);
    private static OriginalArmyChangedListsResult Unknown(string owner) => new(null, null, owner);
    private sealed class Missing(string owner) : Exception(owner);
}
