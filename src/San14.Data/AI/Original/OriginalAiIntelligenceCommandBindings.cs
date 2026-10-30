namespace San14.Data.AI.Original;

/// <summary>Load-time bindings for the district's persistent intelligence slots.</summary>
public static class OriginalAiIntelligenceCommandBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        var a = node.Arguments;
        switch (node.TypeId)
        {
            case 0xB5A524E2:
                if (a.Length != 1) { unresolved = "SpyHomeOfficerRemove integer-slot parameter shape"; return false; }
                execute = operation => operation.RemoveIntelligenceAssignment(a[0]); return true;
            case 0xFB5E2F3C:
                if (a.Length != 3) { unresolved = "SpyHomeOfficerSet Force/Person/integer-slot parameter shape"; return false; }
                execute = operation => operation.SetIntelligenceAssignment(a[0], a[1], a[2]); return true;
            default: return false;
        }
    }
}
