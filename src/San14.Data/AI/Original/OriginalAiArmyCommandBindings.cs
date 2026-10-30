namespace San14.Data.AI.Original;

/// <summary>One-time bindings for the ordinary Army actor instruction family.</summary>
public static class OriginalAiArmyCommandBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        int width = node.TypeId switch
        {
            0x7AE49AAC or 0xDDC7F4D7 or 0x8AEA0861 or 0xA7551ED2 => 0,
            0x503972A4 or 0x9F649845 => 1,
            0x4657905F or 0xC22C23BA or 0x9D091CFB => 2,
            0xD67CBD65 => 3,
            _ => -1
        };
        if (width < 0) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || node.Arguments.Length != width)
        { unresolved = "Army instruction native parameter shape"; return false; }
        var a = node.Arguments;
        if (node.TypeId == 0xD67CBD65 && a[1] > 29)
        { unresolved = "Army information selector " + a[1]; return false; }
        execute = node.TypeId switch
        {
            0x8AEA0861 => operation => operation.RecoverArmyLogistics(),
            0xA7551ED2 => operation => operation.ExcludeArmyFromPlayerFoothold(),
            0x7AE49AAC => operation => operation.ClearArmyAiAction(),
            0xDDC7F4D7 => operation => operation.AvoidArmyPoison(),
            0x503972A4 => operation => operation.ReadMyselfArmy(a[0]),
            0x4657905F => operation => operation.SetArmyInstructionAction(a[0], unchecked((int)a[1])),
            0xC22C23BA => operation => operation.SetArmyInstructionTarget(a[0], a[1]),
            0x9D091CFB => operation => operation.SetArmyInstructionRelay(a[0], a[1]),
            0x9F649845 => operation => operation.RetreatArmyInstruction(a[0]),
            0xD67CBD65 => operation => operation.ReadArmyInformation(a[0], unchecked((int)a[1]), a[2]),
            _ => throw new InvalidDataException("original_ai.Army_instruction_binding")
        };
        return true;
    }
}
