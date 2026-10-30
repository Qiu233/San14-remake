using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record EditedEventConditionRow(int StartIndex, int EndIndex, int Depth, bool IsGroupBoundary);
public sealed record EditedEventConditionEditResult(EditedEventProgram Program, int? SelectedIndex);
public sealed record EditedEventConditionActions(bool CanInsert, bool CanEdit, bool CanMoveUp,
    bool CanMoveDown, bool CanDelete, bool CanCopy);

/// <summary>Detached normal-editor edits. Group markers travel with their
/// complete matching block; operands and dispatch controls remain unchanged.</summary>
public static class EditedEventConditionEditing
{
    public static IReadOnlyList<EditedEventConditionRow> ReadRows(EditedEventProgram program) => Array.AsReadOnly(Parse(program));

    public static EditedEventConditionActions ReadActions(EditedEventProgram program, int? index, int capacity)
    {
        var rows = Parse(program); CheckCapacity(capacity);
        if (index is not int selected || (uint)selected >= (uint)rows.Length)
            return new(rows.Length < capacity, false, false, false, false, false);
        var block = rows[selected]; int length = block.EndIndex - block.StartIndex + 1;
        return new(rows.Length < capacity, !block.IsGroupBoundary,
            Previous(rows, block) >= 0, Next(rows, block) >= 0, true, rows.Length + length <= capacity);
    }

    public static EditedEventConditionEditResult Insert(EditedEventProgram program, int index,
        EditedEventInstruction instruction, int capacity)
    {
        ArgumentNullException.ThrowIfNull(instruction); instruction.Validate();
        var rows = Parse(program); CheckCapacity(capacity);
        if ((uint)index > (uint)rows.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (instruction.Code is 6 or 8) throw new InvalidDataException("event_editor.condition_group.end_insertion");
        int added = instruction.Code is 5 or 7 ? 2 : 1;
        if (rows.Length + added > capacity) throw new InvalidDataException("event_editor.conditions.capacity");
        // Inserting on an end marker is an ordinary insertion immediately
        // before that marker, so it is inside the selected group.
        var next = program.Instructions.Insert(index, instruction);
        if (added == 2) next = next.Insert(index + 1, EditedEventInstruction.Create(instruction.Code + 1));
        return new(new(next), index);
    }

    public static EditedEventConditionEditResult Delete(EditedEventProgram program, int index)
    {
        var rows = Parse(program); var block = Row(rows, index);
        var next = program.Instructions.RemoveRange(block.StartIndex, block.EndIndex - block.StartIndex + 1);
        return new(new(next), next.IsEmpty ? null : Math.Min(block.StartIndex, next.Length - 1));
    }

    public static EditedEventConditionEditResult Copy(EditedEventProgram program, int index, int capacity)
    {
        var rows = Parse(program); CheckCapacity(capacity); var block = Row(rows, index);
        int length = block.EndIndex - block.StartIndex + 1;
        if (rows.Length + length > capacity) throw new InvalidDataException("event_editor.conditions.capacity");
        var copied = program.Instructions.Skip(block.StartIndex).Take(length);
        return new(new(program.Instructions.InsertRange(block.EndIndex + 1, copied)), block.EndIndex + 1);
    }

    public static EditedEventConditionEditResult Move(EditedEventProgram program, int index, int direction, int capacity)
    {
        var rows = Parse(program); CheckCapacity(capacity); var block = Row(rows, index);
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        int adjacent = direction < 0 ? Previous(rows, block) : Next(rows, block);
        if (adjacent < 0) throw new InvalidOperationException("event_editor.condition_group.sibling_boundary");
        var other = rows[adjacent];
        int first = Math.Min(block.StartIndex, other.StartIndex), last = Math.Max(block.EndIndex, other.EndIndex);
        var ordered = direction < 0
            ? program.Instructions.Skip(block.StartIndex).Take(block.EndIndex - block.StartIndex + 1)
                .Concat(program.Instructions.Skip(other.StartIndex).Take(other.EndIndex - other.StartIndex + 1))
            : program.Instructions.Skip(other.StartIndex).Take(other.EndIndex - other.StartIndex + 1)
                .Concat(program.Instructions.Skip(block.StartIndex).Take(block.EndIndex - block.StartIndex + 1));
        var next = program.Instructions.RemoveRange(first, last - first + 1).InsertRange(first, ordered);
        int selected = direction < 0 ? other.StartIndex : block.StartIndex + other.EndIndex - other.StartIndex + 1;
        return new(new(next), selected);
    }

    private static int Previous(EditedEventConditionRow[] rows, EditedEventConditionRow block)
    {
        int previous = block.StartIndex - 1;
        return previous >= 0 && rows[previous].Depth == block.Depth ? rows[previous].StartIndex : -1;
    }
    private static int Next(EditedEventConditionRow[] rows, EditedEventConditionRow block)
    {
        int next = block.EndIndex + 1;
        return next < rows.Length && rows[next].Depth == block.Depth ? next : -1;
    }
    private static EditedEventConditionRow Row(EditedEventConditionRow[] rows, int index) =>
        (uint)index < (uint)rows.Length ? rows[index] : throw new ArgumentOutOfRangeException(nameof(index));
    private static void CheckCapacity(int capacity)
    { if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity)); }
    private static EditedEventConditionRow[] Parse(EditedEventProgram program)
    {
        ArgumentNullException.ThrowIfNull(program); program.Validate();
        var rows = new EditedEventConditionRow[program.Instructions.Length];
        var stack = new Stack<int>();
        for (int i = 0; i < rows.Length; i++)
        {
            int code = program.Instructions[i].Code;
            if (code is 5 or 7)
            {
                rows[i] = new(i, -1, stack.Count, true); stack.Push(i);
            }
            else if (code is 6 or 8)
            {
                if (stack.Count == 0 || program.Instructions[stack.Peek()].Code + 1 != code)
                    throw new InvalidDataException("event_editor.condition_group.unmatched_end");
                int start = stack.Pop(); rows[start] = rows[start] with { EndIndex = i };
                rows[i] = new(start, i, stack.Count, true);
            }
            else rows[i] = new(i, i, stack.Count, false);
        }
        if (stack.Count != 0) throw new InvalidDataException("event_editor.condition_group.unmatched_begin");
        return rows;
    }
}
