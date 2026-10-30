using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>Load-time bindings for the real district/force ancestor inputs.
/// Resource modes remain at this boundary; execution uses the current actor and world.</summary>
public static class OriginalAiAncestorInputBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (!node.IsNative || !node.ChildIndices.IsEmpty) return false;
        var a = node.Arguments;
        switch (node.TypeId)
        {
            case 0xF523AD06 when a.Length == 1:
                execute = operation => operation.AncestorMyselfDistrict(a[0]); break;
            case 0x9BD46CAA when a.Length == 1:
                execute = operation => operation.AncestorMyselfForce(a[0]); break;
            case 0xD3B17B34 when a.Length == 2 && a[0] <= 3:
                execute = operation => operation.AncestorGameInformation((int)a[0], a[1]); break;
            case 0x70368754 when a.Length == 3 && a[1] is 0 or 1 or 2 or 4 or 5:
                execute = operation => operation.AncestorDistrictInformation(a[0], (int)a[1], a[2]); break;
            // Field5 belongs to the independent city duties group, which owns that binding.
            case 0x7B9D3E14 when a.Length == 2 && a[0] is 0 or 1 or 2 or 4:
                execute = operation => operation.AncestorMyselfDistrictInformation((int)a[0], a[1]); break;
            case 0x9CDB6038 when a.Length == 2 && a[0] is 0 or 2:
                execute = operation => operation.AncestorMyselfForceInformation((int)a[0], a[1]); break;
            case 0xEBBF8C69 when a.Length == 3 && a[1] == 37:
                execute = operation => operation.AncestorForceAssociationPeople(a[0], a[2]); break;
            // ForceInfo19/21 are owned by OriginalAiStrategyMemoryBindings.
            case 0xA5BF7BE1 when a.Length == 6:
                execute = operation => operation.AncestorRandom(a[0], unchecked((int)a[1]), a[2], a[3], a[4], a[5]); break;
            default: return false;
        }
        return true;
    }
}
