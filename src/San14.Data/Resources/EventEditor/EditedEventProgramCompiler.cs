using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Compiles the edited event language into the same resource-neutral
/// statements used by the normal event evaluator. Compilation executes nothing.</summary>
public static class EditedEventProgramCompiler
{
    public static S14EventConditionProgram Compile(EditedEventProgram program, int slot)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (slot is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(slot));
        program.Validate();
        var operations = new S14EventOperation[program.Instructions.Length];
        for (int index = 0; index < operations.Length; index++)
        {
            var statement = program.Instructions[index];
            byte[] bytes = new byte[S14EventReader.OperationBytes];
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), statement.SettingCondition);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), statement.Suppression);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), statement.Code);
            for (int operand = 0; operand < 8; operand++)
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12 + operand * 4), statement.Arguments[operand]);
            operations[index] = new(index, checked(index * S14EventReader.OperationBytes), statement.Code,
                statement.SettingCondition, statement.Suppression, bytes);
        }
        return S14EventConditionProgram.Read(new(slot, 0,
            checked(operations.Length * S14EventReader.OperationBytes), Array.AsReadOnly(operations)));
    }

    /// <summary>Uses the normal evaluator and real current providers; unknown
    /// handlers are reported by that evaluator rather than treated as true.</summary>
    public static S14EventConditionResult Evaluate(S14EventConditionProgram compiled,
        IS14EventConditionRuntime currentRuntime)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(currentRuntime);
        return S14EventConditionEvaluator.Evaluate(compiled, currentRuntime);
    }
}
