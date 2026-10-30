using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

//Only the already selected ordered payloads cross a modal wait. No borrowed
//list node, descriptor, pointer, current object or recomputed ownership is saved.
internal static class CaptureEmperorTailLists
{
    internal static ImmutableArray<int> ReadObjects(SourceWorldContext current, HexListHandle list, RegistryKind kind)
    {
        var values = ImmutableArray.CreateBuilder<int>();
        var node = current.ReadHexListHead(list) ?? throw Missing("object_head");
        while (node.Identity.HasValue)
        {
            int physical;
            if (kind == RegistryKind.Person)
                physical = current.ReadPersonRelationListPayload(node)?.PhysicalSlot ?? throw Missing("person_payload");
            else
            {
                var value = current.ReadRegistryNodePayload(new(node.Identity));
                if (value?.Kind != kind || value.PhysicalSlot is not int slot) throw Missing("object_payload");
                physical = slot;
            }
            values.Add(physical);
            node = current.ReadHexListNodeNext(node) ?? throw Missing("object_next");
        }
        return values.ToImmutable();
    }

    internal static ImmutableArray<int> ReadStatic(SourceWorldContext current, HexListHandle list, string kind)
    {
        var values = ImmutableArray.CreateBuilder<int>();
        var node = current.ReadHexListHead(list) ?? throw Missing("static_head");
        while (node.Identity is int identity)
        {
            if (current.ReadCurrentListPayload(identity) is not ReturnStaticTablePayload value || value.TableKind != kind)
                throw Missing("static_payload");
            values.Add(value.PhysicalSlot ?? throw Missing("static_payload_identity"));
            node = current.ReadHexListNodeNext(node) ?? throw Missing("static_next");
        }
        return values.ToImmutable();
    }

    internal static HexListHandle RestoreObjects(SourceWorldContext current, ImmutableArray<int> slots, RegistryKind kind)
    {
        var list = current.CreateTemporaryHexList() ?? throw Missing("object_allocator");
        try
        {
            foreach (int slot in slots)
            {
                if (kind == RegistryKind.Person)
                {
                    if (!current.AppendPersonReturnTemporary(list, slot)) throw Missing("person_allocation");
                }
                else
                {
                    var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("object_node");
                    if (!node.Identity.HasValue) throw Missing("object_allocation");
                    current.WriteRegistryNodePayload(node, new(kind, slot));
                }
            }
            return list;
        }
        catch { Release(current, list); throw; }
    }

    internal static HexListHandle RestoreStatic(SourceWorldContext current, ImmutableArray<int> slots, string kind)
    {
        var list = current.CreateTemporaryHexList() ?? throw Missing("static_allocator");
        try
        {
            foreach (int slot in slots)
                if (!current.AppendCaptureStaticTemporary(list, kind, slot)) throw Missing("static_allocation");
            return list;
        }
        catch { Release(current, list); throw; }
    }

    internal static BaseCapturePersonPrefixReceipt RestorePeopleReceipt(SourceWorldContext current,
        int location, int oldForce, int newForce, ImmutableArray<int> captured, ImmutableArray<int> noncaptured,
        ImmutableArray<int> special, ImmutableArray<int> changed)
    {
        HexListHandle? c = null, n = null, s = null, f = null;
        try
        {
            c = RestoreObjects(current, captured, RegistryKind.Person);
            n = RestoreObjects(current, noncaptured, RegistryKind.Person);
            s = RestoreObjects(current, special, RegistryKind.Person);
            f = RestoreObjects(current, changed, RegistryKind.Person);
            return new(location, oldForce, newForce, [], [], [], Ids(captured), Ids(special), Ids(noncaptured),
                [], [], c, n, s, f) { HasCompleteDiagnosticHistory = false };
        }
        catch
        {
            if (f is not null) Release(current, f);
            if (s is not null) Release(current, s);
            if (n is not null) Release(current, n);
            if (c is not null) Release(current, c);
            throw;
        }
        int[] Ids(ImmutableArray<int> slots) => slots.Select(slot =>
            (int)(current.ReadPersonIdentity(slot) ?? throw Missing("person_identity"))).ToArray();
    }

    private static void Release(SourceWorldContext current, HexListHandle list)
    { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    private static InvalidDataException Missing(string field) => new("capture.emperor.saved-list." + field);
}
