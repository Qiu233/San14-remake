namespace San14.Data.AI.Original;

/// <summary>Load-time binding of the Person receiver's information family.</summary>
public static class OriginalAiPersonInformationBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId != 0x0BB9F92D) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || node.Arguments.Length != 3)
        { unresolved = "PersonInfo requires a leaf and Person/selector/output words"; return false; }
        var a = node.Arguments;
        if (a[1] > 38)
        { unresolved = "PersonInfo selector outside the source information family"; return false; }
        execute = operation => operation.PersonInformation(a[0], (int)a[1], a[2]);
        return true;
    }
}
