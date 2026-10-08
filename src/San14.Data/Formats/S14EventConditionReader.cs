using System.Buffers.Binary;

namespace San14.Data.Formats;

public enum ConditionGroupMode
{
    All,
    Any
}

public abstract record ConditionNode;

/// <summary>An uninterpreted source operation, including code zero.</summary>
public sealed record ConditionLeaf(S14EventOperation Operation) : ConditionNode;

/// <summary>
/// A structurally matched native AND or OR block. Source markers and all child
/// operations remain available; this record does not evaluate the block.
/// </summary>
public sealed record ConditionGroup(ConditionGroupMode Mode,
    S14EventOperation BeginOperation, S14EventOperation EndOperation,
    IReadOnlyList<ConditionNode> Children) : ConditionNode
{
    public int InteriorOperationCount => EndOperation.Index - BeginOperation.Index - 1;
    public int StepOperationCount => EndOperation.Index - BeginOperation.Index + 1;
}

/// <summary>
/// Ordered top-level structure. It is deliberately not an All or Any group:
/// native dispatch, result callbacks and runtime suppression are not evaluated.
/// </summary>
public sealed record ConditionSequence(IReadOnlyList<ConditionNode> Children);

public sealed record S14EventConditionTree(S14EventStream Stream,
    ConditionSequence Root, IReadOnlyList<S14EventOperation> Operations)
{
    public bool RuntimeDispatchInterpreted => false;
}

/// <summary>
/// Reads a source-only condition structure verified in frozen CE native code.
/// Supports zero RawField04/06 controls and strictly matched 5/6 (OR), 7/8
/// (AND) markers. Other leaves retain their complete original operation refs.
/// Unknown runtime dispatch state is not assumed to permit these operations;
/// callers must establish a runtime profile before implementing evaluation.
/// </summary>
public static class S14EventConditionReader
{
    private sealed class Frame(S14EventOperation? begin)
    {
        public S14EventOperation? Begin { get; } = begin;
        public List<ConditionNode> Children { get; } = [];
    }

    public static S14EventConditionTree Read(S14EventStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.Index < 0 || stream.Offset < 0 || stream.Length < 0 ||
            stream.Operations is null || stream.Length % S14EventReader.OperationBytes != 0 ||
            stream.Length / S14EventReader.OperationBytes != stream.Operations.Count ||
            (long)stream.Offset + stream.Length > int.MaxValue)
            throw new InvalidDataException($"Condition stream {stream.Index} has an invalid operation window.");

        // Snapshot the sequence while retaining the original operation objects.
        // A stack avoids recursion on attacker-controlled nesting depth.
        S14EventOperation[] operations = stream.Operations.ToArray();
        var stack = new Stack<Frame>();
        var root = new Frame(null);
        stack.Push(root);
        for (int index = 0; index < operations.Length; index++)
        {
            S14EventOperation? operation = operations[index];
            if (operation is null || operation.RawBytes is null ||
                operation.RawBytes.Length != S14EventReader.OperationBytes ||
                operation.Index != index ||
                operation.Offset != stream.Offset + index * S14EventReader.OperationBytes)
                throw new InvalidDataException($"Condition stream {stream.Index} operation {index} has an invalid source record/window.");

            ReadOnlySpan<byte> raw = operation.RawBytes;
            if (BinaryPrimitives.ReadInt32LittleEndian(raw[8..]) != operation.Code ||
                BinaryPrimitives.ReadInt16LittleEndian(raw[4..]) != operation.RawField04 ||
                BinaryPrimitives.ReadInt16LittleEndian(raw[6..]) != operation.RawField06)
                throw Failure(stream, operation, "record fields disagree with the original bytes");
            if (operation.RawField04 != 0 || operation.RawField06 != 0)
                throw Failure(stream, operation,
                    $"unsupported dispatch controls {operation.RawField04}/{operation.RawField06}");
            if (operation.Code is < 0 or >= 453)
                throw Failure(stream, operation, "unsupported native dispatch code range");
            if (operation.Code is 1 or 2 or 3 or 4 or 216 or 257 or 372 or 379)
                throw Failure(stream, operation, "unsupported statement/control-flow structure");

            switch (operation.Code)
            {
                case 5:
                case 7:
                    stack.Push(new Frame(operation));
                    break;
                case 6:
                case 8:
                    if (stack.Count == 1)
                        throw Failure(stream, operation, "unmatched group end");
                    Frame group = stack.Peek();
                    if (group.Begin!.Code + 1 != operation.Code)
                        throw Failure(stream, operation,
                            $"group end does not match begin operation {group.Begin.Index} code {group.Begin.Code}");
                    stack.Pop();
                    stack.Peek().Children.Add(new ConditionGroup(
                        group.Begin.Code == 5 ? ConditionGroupMode.Any : ConditionGroupMode.All,
                        group.Begin, operation, group.Children.AsReadOnly()));
                    break;
                default:
                    stack.Peek().Children.Add(new ConditionLeaf(operation));
                    break;
            }
        }

        if (stack.Count != 1)
            throw Failure(stream, stack.Peek().Begin!, "group begin has no matching end");
        return new S14EventConditionTree(stream,
            new ConditionSequence(root.Children.AsReadOnly()), Array.AsReadOnly(operations));
    }

    private static InvalidDataException Failure(S14EventStream stream,
        S14EventOperation operation, string detail) => new(
        $"Condition stream {stream.Index}, operation {operation.Index}, offset {operation.Offset}, " +
        $"code {operation.Code}: {detail}.");
}
