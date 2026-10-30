namespace San14.Data.AI.Original;

internal enum OriginalAiVariableArithmetic { Add, Subtract, Multiply, Divide }

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus CreateVariable(uint destination, int value)
        => SetInteger(destination, value);

    internal OriginalAiNodeStatus ClearVariable(uint destination)
    {
        // 085030 deliberately ignores the writer result and returns Success1.
        // Clear changes the blackboard tag to integer2 with value0; it does not erase a key.
        SetInteger(destination, 0);
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus CreateSpecifiedVariable(uint sourceType, uint destination)
        => sourceType == 14 ? SetInteger(destination, 0) : SetVariableNullObject(destination);

    internal OriginalAiNodeStatus CopySpecifiedVariable(uint sourceType, uint destination, uint source)
    {
        if (sourceType == 14)
            return TryInteger(source, out int integer) ? SetInteger(destination, integer) : OriginalAiNodeStatus.Failure;

        // 091520 uses the untyped-object reader for every non14 discriminator.
        // A live integer must not expose an older, shadowed list/Hex entry.
        if (integers.ContainsKey(source)) return OriginalAiNodeStatus.Failure;
        if (TryEntity(source, out var entity)) return SetEntity(destination, entity);
        if (candidateNullEntities.Contains(source)) return SetVariableNullObject(destination);
        // Both helpers capture the source before changing the destination. Lists alias
        // their actual mutable object; assigning a key to itself keeps its identity.
        if (TryCopyDefinitionVariable(source, destination) || TryCopyRegionVariable(source, destination) || TryCopyHireOfferVariable(source, destination) || TryCopyTroopsVariable(source, destination) || TryCopyTradeCountryVariable(source, destination) ||
            TryCopyHexVariable(source, destination) || TryCopyPersonSkillVariable(source, destination) || TryCopyListVariable(source, destination))
            return OriginalAiNodeStatus.Success;
        return OriginalAiNodeStatus.Failure;
    }

    internal OriginalAiNodeStatus VariableArithmeticValue(uint destination, int right,
        OriginalAiVariableArithmetic operation)
    {
        if (!TryInteger(destination, out int left)) return OriginalAiNodeStatus.Failure;
        return WriteVariableArithmetic(destination, left, right, operation);
    }

    internal OriginalAiNodeStatus VariableArithmeticKey(uint destination, uint rightKey,
        OriginalAiVariableArithmetic operation)
    {
        // Source reads the destination first, and does not query the second key after failure.
        if (!TryInteger(destination, out int left) || !TryInteger(rightKey, out int right))
            return OriginalAiNodeStatus.Failure;
        return WriteVariableArithmetic(destination, left, right, operation);
    }

    private OriginalAiNodeStatus WriteVariableArithmetic(uint destination, int left, int right,
        OriginalAiVariableArithmetic operation)
    {
        // These native variable nodes differ from the param_s Divide expression:
        // a zero divisor is Failure2, with no write and no default quotient.
        if (operation == OriginalAiVariableArithmetic.Divide && right == 0)
            return OriginalAiNodeStatus.Failure;
        int value = operation switch
        {
            OriginalAiVariableArithmetic.Add => unchecked(left + right),
            OriginalAiVariableArithmetic.Subtract => unchecked(left - right),
            OriginalAiVariableArithmetic.Multiply => unchecked(left * right),
            // 0850F0/085180 sign-extend both i32 operands before division, then retain low32.
            OriginalAiVariableArithmetic.Divide => unchecked((int)((long)left / right)),
            _ => throw new InvalidDataException("original_ai.variable_arithmetic_binding")
        };
        return SetInteger(destination, value);
    }

    private OriginalAiNodeStatus SetVariableNullObject(uint destination)
    {
        ClearDefinitionVariable(destination);
        ClearRegionVariable(destination);
        ClearHexVariable(destination);
        ClearTradeCountryVariable(destination);
        ClearTroopsVariable(destination);
        ClearHireOfferVariable(destination);
        ClearPersonSkillVariable(destination);
        integers.Remove(destination); entities.Remove(destination); candidateLists.Remove(destination);
        candidateNullEntities.Add(destination);
        return OriginalAiNodeStatus.Success;
    }
}
