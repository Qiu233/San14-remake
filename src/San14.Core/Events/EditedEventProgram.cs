using System.Collections.Immutable;

namespace San14.Core;

/// <summary>A statement in an edited event's S14 condition or action language.
/// Arguments are the eight typed integer operands consumed by that statement;
/// they are not an original object image or an executable instruction.</summary>
public sealed record EditedEventInstruction(int Code, ImmutableArray<int> Arguments,
    short SettingCondition = 0, short Suppression = 0)
{
    public void Validate()
    {
        if (Code is < 0 or >= 453 || Arguments.IsDefault || Arguments.Length != 8)
            throw new InvalidDataException("Edited event statement has an invalid code or operand count.");
    }

    public static EditedEventInstruction Create(int code, params int[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Length > 8) throw new ArgumentOutOfRangeException(nameof(arguments));
        var operands = new int[8];
        arguments.CopyTo(operands, 0);
        var result = new EditedEventInstruction(code, ImmutableArray.CreateRange(operands));
        result.Validate();
        return result;
    }
}

/// <summary>An immutable semantic program. The Data event-language reader owns
/// structural matching, opcode availability and current-state execution.</summary>
public sealed record EditedEventProgram(ImmutableArray<EditedEventInstruction> Instructions)
{
    public void Validate()
    {
        if (Instructions.IsDefault) throw new InvalidDataException("Edited event program is unresolved.");
        foreach (var instruction in Instructions)
        {
            if (instruction is null) throw new InvalidDataException("Edited event statement is missing.");
            instruction.Validate();
        }
    }
}
