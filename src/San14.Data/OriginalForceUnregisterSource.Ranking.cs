using San14.Core;

namespace San14.Data;

public sealed partial class OriginalForceUnregisterSource
{
    internal static (GameState State, int[] Forces) RankCurrent(GameState state,
        OriginalEventInitializationContext current, OriginalCurrentListPool primary)
    {
        var module = state.NativeForceUnregisterCurrent!;
        var commands = state.NativePersonReturnCommands ?? throw Missing("ranking_shared_Int32_pool");
        var ranking = new Dictionary<int, NativeForceRankingCurrent>(module.ForceRankings);
        var first = Temporary(current); var last = Temporary(current);
        var scalar = new OriginalPersonCommandValuePool(commands.MembersPool);
        var values = scalar.AllocateDescriptor();
        if (values.Identity is not int descriptor) throw Missing("branch.ranking_Int32_descriptor_allocation");
        var active = new List<int>();
        for (int force = 1; force <= 45; force++)
        {
            if (!Known(current.ReadForceNativeValidity(new(force)), "ranking_Force_validity")) continue;
            active.Add(force); ranking[force] = ranking[force] with { Byte1C4 = 0 };
            Append(current, first, OriginalRegistryKind.Force, force);
            ushort value = Need(ranking[force].Word1BC, "ranking_Force1BC");
            if (!scalar.Values(values).Any(x => x?.Identity == value) && scalar.Append(values, new("Int32Value", value)) is null)
                throw Missing("branch.ranking_Int32_node_allocation");
        }
        //24B350 sorts the existing UInt16-valued Int32 nodes descending.
        //Preserve node identities and payloads; clearing later returns this
        //sorted chain to the very same saved global member-value free list.
        var image = scalar.Export(); var row = image.Descriptors[descriptor];
        var ordered = new List<int>();
        for (int? at = row.Head; at is int node; at = image.Nodes[node].Next) ordered.Add(node);
        ordered = ordered.OrderByDescending(node => image.Nodes[node].Payload!.Identity!.Value).ToList();
        var nodes = new Dictionary<int, NativeSharedListNode>(image.Nodes);
        for (int i = 0; i < ordered.Count; i++) nodes[ordered[i]] = nodes[ordered[i]] with
            { Previous = i == 0 ? null : ordered[i - 1], Next = i + 1 == ordered.Count ? null : ordered[i + 1] };
        var rows = new Dictionary<int, NativeSharedListDescriptor>(image.Descriptors)
            { [descriptor] = new(ordered.Count == 0 ? null : ordered[0], ordered.Count == 0 ? null : ordered[^1], row.Count) };
        scalar = new(image with { Nodes = nodes, Descriptors = rows });
        var distinct = scalar.Values(values).Select(x => x!.Identity!.Value).ToArray();
        foreach (int force in active)
        {
            int value = Need(ranking[force].Word1BC, "ranking_Force1BC_second_read");
            int position = Array.IndexOf(distinct, value) + 1;
            if (position == 1) ranking[force] = ranking[force] with { Byte1C4 = value == 0 ? (byte)3 : (byte)1 };
            else if (position == 2) ranking[force] = ranking[force] with { Byte1C4 = 2 };
            else if (distinct.Length >= 4 && position == distinct.Length) Append(current, last, OriginalRegistryKind.Force, force);
            else ranking[force] = ranking[force] with { Byte1C4 = 3 };
        }
        int lowestCount = Need(current.ReadTemporaryHexListCount(last), "ranking_lowest_pointer_count");
        var atLast = current.ReadRegistryHead(new(last.Identity)) ?? throw Missing("ranking_lowest_head");
        while (atLast.Identity is not null)
        {
            int force = current.ReadRegistryNodePayload(atLast)?.PhysicalSlot ?? throw Missing("ranking_lowest_payload");
            ranking[force] = ranking[force] with { Byte1C4 = lowestCount == 1 ? (byte)4 : (byte)3 };
            atLast = current.ReadRegistryNodeNext(atLast) ?? throw Missing("ranking_lowest_next");
        }
        scalar.Clear(values); scalar.ReleaseDescriptor(values);
        current.ClearTemporaryHexList(last); current.ReleaseTemporaryHexList(last);
        current.ClearTemporaryHexList(first); current.ReleaseTemporaryHexList(first);
        return (state with { NativeForceUnregisterCurrent = module with { ForceRankings = ranking },
            NativePersonReturnCommands = commands with { MembersPool = scalar.Export() },
            NativeArmyLifecycle = state.NativeArmyLifecycle! with { Pool = primary.ExportOwnedState() } }, active.ToArray());
    }
}
