namespace San14.Data.AI.Original;

/// <summary>Signed integer min/max bindings. Calculation variants write a
/// separate destination; in-place variants preserve their source key.</summary>
public static class OriginalAiNumericBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        bool maximum, variable, calculation;
        switch (node.TypeId)
        {
            case 0x0E1685EF: (maximum, variable, calculation) = (true, false, false); break;
            case 0xD8747FC2: (maximum, variable, calculation) = (true, true, false); break;
            case 0xDFB6775D: (maximum, variable, calculation) = (false, false, false); break;
            case 0xAA147130: (maximum, variable, calculation) = (false, true, false); break;
            case 0xDF5951D9: (maximum, variable, calculation) = (true, false, true); break;
            case 0xC4A43398: (maximum, variable, calculation) = (true, true, true); break;
            case 0xB0F94347: (maximum, variable, calculation) = (false, false, true); break;
            case 0x96442506: (maximum, variable, calculation) = (false, true, true); break;
            default: return false;
        }
        int width = calculation ? 3 : 2;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || node.Arguments.Length != width)
        {
            unresolved = $"integer min/max native {node.TypeId:X8} requires {width} source words";
            return false;
        }
        var args = node.Arguments;
        uint destination = calculation ? args[2] : args[0];
        execute = variable
            ? operation => operation.SelectIntegerVariable(args[0], args[1], destination, maximum)
            : operation => operation.SelectIntegerValue(args[0], unchecked((int)args[1]), destination, maximum);
        return true;
    }
}
