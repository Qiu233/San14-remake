namespace San14.Data.AI.Original;

/// <summary>Source control leaves which neither manufacture commands nor
/// turn unavailable business operations into successful nodes.</summary>
public static class OriginalAiControlLeafBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0x0AB7AEA8 or 0x13BE7C11)) return false;
        int width = node.TypeId == 0x0AB7AEA8 ? 0 : 1;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || node.Arguments.Length != width)
        {
            unresolved = $"control leaf {node.TypeId:X8} requires {width} source words";
            return false;
        }
        if (node.TypeId == 0x0AB7AEA8)
            execute = operation => operation.RequestLoopBreak();
        else
        {
            // The serialized word is a terminal status selector, not a delay
            // or an instruction to fabricate a command result.
            var status = node.Arguments[0] == 1 ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
            execute = _ => status;
        }
        return true;
    }
}
