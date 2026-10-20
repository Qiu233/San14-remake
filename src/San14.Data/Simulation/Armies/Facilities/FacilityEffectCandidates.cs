using San14.Core;

namespace San14.Data;

internal static class FacilityEffectCandidates
{
    internal static void Fill(GameState state, SourceWorldContext current, HexListHandle output)
    {
        var directory = current.CurrentMapObjectDirectory ?? throw Missing("object_directory");
        var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("pool");
        var pending = pool.PendingObjectRetirements ?? throw Missing("retirement_F0");
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Object) ?? throw Missing("registry_E0"))
        {
            if (entry is not { Kind: RegistryKind.Object, PhysicalSlot: int physical }) throw Missing("registry_Object_pointer");
            var obj = new ObjectReference(physical);
            if (!K(current.ReadObjectNativeValidity(obj), "Object_validity")) continue;
            var receiver = directory.ReceiverAtPhysical(physical);
            bool retired = false;
            foreach (var id in pending)
            {
                if (id is null) throw Missing("retirement_pointer");
                if (id == receiver) { retired = true; break; }
            }
            if (retired) continue;
            var record = current.ReadPhysicalObject(physical) ?? throw Missing("Object_kind");
            var definition = current.ReadObjectDefinitionReference(record.RawField12 <= 30 ? record.RawField12 : 0)
                ?? throw Missing("definition_pointer");
            if (!K(current.ReadObjectDefinitionValidity(definition), "definition_validity") ||
                K(current.ReadObjectDefinitionRaw2C(definition), "definition_category") != 2) continue;
            if (!K(FacilityActivationCurrent.ReadEffectsEnabled(current, obj), "current_activation16")) continue;
            current.AppendFacilityObjectTemporary(output, obj);
        }
    }
    internal static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("army.progress.facility-effects." + field);
}

public sealed partial class SourceWorldContext
{
    internal void AppendFacilityObjectTemporary(HexListHandle list, ObjectReference obj)
    {
        ConditionalRegistryEnter();
        try
        {
            var node = AllocateRegistryNode(new(list.Identity)) ?? throw FacilityEffectCandidates.Missing("Object_node");
            if (node.Identity.HasValue) WriteRegistryNodePayload(node, new(RegistryKind.Object, obj.PhysicalSlot));
        }
        finally { ConditionalRegistryExit(); }
    }
    internal void RemoveFacilityHexTemporary(HexListHandle list, HexListNodeReference node)
    {
        ConditionalRegistryEnter();
        try { _orderedLists.Remove(list.Identity!.Value, node.Identity!.Value); }
        finally { ConditionalRegistryExit(); }
    }
}
