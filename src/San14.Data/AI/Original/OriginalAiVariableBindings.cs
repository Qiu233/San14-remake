namespace San14.Data.AI.Original;

/// <summary>One-time native bindings for the installed integer arithmetic and
/// integer/object blackboard assignment family. No operation consumes a raw graph.</summary>
public static class OriginalAiVariableBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        int width = node.TypeId switch
        {
            0x261904C6 => 1,
            0x1C972788 => 3,
            0xC7F0763D or 0xA7E41C85 or 0x8B144FCD or 0x0C75905A or
                0xC288DF3B or 0x6D4FB04E or 0xD826AEC4 or 0x82ED7FD7 or
                0xD426860E or 0x8239D75B => 2,
            _ => 0
        };
        if (width == 0) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || node.Arguments.Length != width)
        {
            unresolved = $"variable native {node.TypeId:X8} requires a leaf with {width} source words";
            return false;
        }
        var a = node.Arguments;
        execute = node.TypeId switch
        {
            0xC7F0763D => operation => operation.CreateVariable(a[0], unchecked((int)a[1])),
            0x261904C6 => operation => operation.ClearVariable(a[0]),
            0xA7E41C85 => operation => operation.CreateSpecifiedVariable(a[0], a[1]),
            0x1C972788 => operation => operation.CopySpecifiedVariable(a[0], a[1], a[2]),
            0x8B144FCD => operation => operation.VariableArithmeticValue(a[0], unchecked((int)a[1]), OriginalAiVariableArithmetic.Add),
            0x0C75905A => operation => operation.VariableArithmeticKey(a[0], a[1], OriginalAiVariableArithmetic.Add),
            0xC288DF3B => operation => operation.VariableArithmeticValue(a[0], unchecked((int)a[1]), OriginalAiVariableArithmetic.Subtract),
            0x6D4FB04E => operation => operation.VariableArithmeticKey(a[0], a[1], OriginalAiVariableArithmetic.Subtract),
            0xD826AEC4 => operation => operation.VariableArithmeticValue(a[0], unchecked((int)a[1]), OriginalAiVariableArithmetic.Multiply),
            0x82ED7FD7 => operation => operation.VariableArithmeticKey(a[0], a[1], OriginalAiVariableArithmetic.Multiply),
            0xD426860E => operation => operation.VariableArithmeticValue(a[0], unchecked((int)a[1]), OriginalAiVariableArithmetic.Divide),
            0x8239D75B => operation => operation.VariableArithmeticKey(a[0], a[1], OriginalAiVariableArithmetic.Divide),
            _ => throw new InvalidDataException("original_ai.variable_native_binding")
        };
        return true;
    }
}
