namespace San14.Data.AI.Original;

public static class OriginalAiDepartureBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId != 0x5DE944EE) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || node.Arguments.Length != 14)
        { unresolved = "Departure requires its actual fourteen-field native leaf"; return false; }
        var a = node.Arguments;
        //044860 deliberately ignores argument10. Output is argument11;
        //the construction definition/continuation are arguments12/13.
        execute = operation => operation.Departure(a[0], a[1], a[2], a[3], unchecked((int)a[4]), a[5],
            unchecked((int)a[6]), unchecked((int)a[7]), unchecked((int)a[8]), unchecked((int)a[9]), a[11], a[12], a[13]);
        return true;
    }
}
