using System.Buffers.Binary;
using System.Collections.ObjectModel;

namespace San14.Data.Formats;

/// <summary>An immutable snapshot of a condition operation, including all original bytes.</summary>
public sealed record S14ConditionInstruction(int Index, int Offset, int Code,
    short RawField04, short RawField06, IReadOnlyList<int> RawArguments,
    IReadOnlyList<byte> RawBytes);

/// <summary>
/// A validated OR/AND source program. Nonzero dispatch controls are retained.
/// Matching is strict across the full stream; malformed native recovery and
/// other statement languages are outside this adapter's supported profile.
/// </summary>
public sealed class S14EventConditionProgram
{
    public int StreamIndex { get; }
    public int Offset { get; }
    public IReadOnlyList<S14ConditionInstruction> Operations { get; }
    internal int[] GroupEnds { get; }

    private S14EventConditionProgram(int streamIndex, int offset,
        S14ConditionInstruction[] operations, int[] groupEnds)
    {
        StreamIndex = streamIndex;
        Offset = offset;
        Operations = Array.AsReadOnly(operations);
        GroupEnds = groupEnds;
    }

    public static S14EventConditionProgram Read(S14EventStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.Index < 0 || stream.Offset < 0 || stream.Length < 0 ||
            stream.Operations is null || stream.Length % S14EventReader.OperationBytes != 0 ||
            stream.Operations.Count != stream.Length / S14EventReader.OperationBytes ||
            (long)stream.Offset + stream.Length > int.MaxValue)
            throw new InvalidDataException("Condition program has an invalid source window.");

        var operations = new S14ConditionInstruction[stream.Operations.Count];
        int[] ends = Enumerable.Repeat(-1, operations.Length).ToArray();
        var stack = new Stack<int>();
        for (int index = 0; index < operations.Length; index++)
        {
            S14EventOperation? source = stream.Operations[index];
            if (source is null || source.Index != index ||
                source.Offset != stream.Offset + index * S14EventReader.OperationBytes ||
                source.RawBytes is not { Length: S14EventReader.OperationBytes })
                throw new InvalidDataException($"Condition program operation {index} has an invalid source record.");
            byte[] bytes = source.RawBytes.ToArray();
            if (BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)) != source.Code ||
                BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)) != source.RawField04 ||
                BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) != source.RawField06)
                throw new InvalidDataException($"Condition program operation {index} disagrees with its source bytes.");
            if (source.Code is < 0 or >= 453 || source.Code is 1 or 2 or 3 or 4 or 216 or 257 or 372 or 379)
                throw new InvalidDataException($"Condition program operation {index} has unsupported code {source.Code}.");

            int[] arguments = new int[8];
            for (int argument = 0; argument < arguments.Length; argument++)
                arguments[argument] = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12 + argument * 4));
            operations[index] = new(index, source.Offset, source.Code,
                source.RawField04, source.RawField06, Array.AsReadOnly(arguments), Array.AsReadOnly(bytes));
            if (source.Code is 5 or 7)
                stack.Push(index);
            else if (source.Code is 6 or 8)
            {
                if (stack.Count == 0 || operations[stack.Peek()].Code + 1 != source.Code)
                    throw new InvalidDataException($"Condition program operation {index} has an unmatched group end.");
                ends[stack.Pop()] = index;
            }
        }
        if (stack.Count != 0)
            throw new InvalidDataException($"Condition program operation {stack.Peek()} has an unmatched group begin.");
        return new(stream.Index, stream.Offset, operations, ends);
    }
}

public readonly record struct S14ConditionBinding(int FirstPermissionControl,
    int SecondPermissionControl, bool HasHandler);

/// <summary>Current inputs, sampled for each operation rather than cached for the whole event.</summary>
public readonly record struct S14ConditionDispatchInputs(bool FirstGateEnabled,
    int SettingPredicateValue, bool ExecutionGateEnabled);

public sealed record S14ConditionContext(S14EventConditionProgram Program,
    S14ConditionInstruction Operation, int LocalSpanBegin, int LocalSpanEnd,
    int GroupDepth, int OrDepth);

/// <summary>
/// Explicit leaf semantics and metadata. Unknown bindings must return false.
/// Accepted leaves preserve step one; other control languages need a separate
/// implementation. Providers may keep proven selection/blocking side effects
/// in their own per-evaluation state and expose the current blocked flag here.
/// </summary>
public interface IS14EventConditionRuntime
{
    bool TryGetBinding(int code, out S14ConditionBinding binding);
    S14ConditionDispatchInputs ReadInputs(S14ConditionContext context);
    int InvokeLeaf(S14ConditionContext context);
    bool ConditionBlocked { get; }
}

public sealed record S14ConditionCallback(int OperationIndex, int Result, int Category)
{
    public bool Passed => Result != 0;
    public byte StoredCategory => unchecked((byte)Category);
}

public enum S14ConditionStatus { Satisfied = 0, Unsatisfied = 1, Blocked = 2 }

public sealed record S14EventConditionResult(bool RootFold,
    IReadOnlyList<S14ConditionCallback> Callbacks, int SuccessCount, int EvaluatedCount,
    S14ConditionStatus Status)
{
    // 3D93A0 resets its reported success count when a handler marks the
    // execution blocked. Keep the callback count available independently.
    public int EffectiveSuccessCount => Status == S14ConditionStatus.Blocked ? 0 : SuccessCount;
}

/// <summary>
/// Evaluates the verified CE OR/AND dispatch profile and result callback order.
/// It executes no original machine code. A leaf provider supplies exact state
/// and semantics; a successful structural parse alone is not an executable
/// profile. Event ID filtering and already-consumed status are outside this VM.
/// </summary>
public static class S14EventConditionEvaluator
{
    public static IReadOnlyDictionary<int, S14ConditionBinding> BuiltInBindings { get; } =
        new ReadOnlyDictionary<int, S14ConditionBinding>(new Dictionary<int, S14ConditionBinding>
        {
            [0] = new(0, 0, true),
            [5] = new(0, 1, true), [6] = new(0, 1, true),
            [7] = new(0, 1, true), [8] = new(0, 1, true)
        });

    private sealed class Frame(int begin, int end, bool all, int depth, int orDepth)
    {
        public int Begin { get; } = begin;
        public int End { get; } = end;
        public bool All { get; } = all;
        public int Depth { get; } = depth;
        public int OrDepth { get; } = orDepth;
        public int Cursor { get; set; } = begin;
        public bool Fold { get; set; } = all;
        public int PendingGroup { get; set; } = -1;
    }

    public static S14EventConditionResult Evaluate(S14EventConditionProgram program,
        IS14EventConditionRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(program);
        return Evaluate(program, runtime, 0, program.Operations.Count);
    }

    /// <summary>
    /// Dispatches a contained local span in root OR mode. An invoked group can
    /// extend beyond that local span up to its matched end in the full program,
    /// as the native scanner uses the full context bound.
    /// </summary>
    public static S14EventConditionResult Evaluate(S14EventConditionProgram program,
        IS14EventConditionRuntime runtime, int begin, int end, int mode = 0)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(runtime);
        if (begin < 0 || end < begin || end > program.Operations.Count)
            throw new InvalidDataException("Condition evaluation span is outside the validated program.");
        var bindings = new Dictionary<int, S14ConditionBinding>();
        foreach (S14ConditionInstruction operation in program.Operations)
            if (!bindings.ContainsKey(operation.Code))
            {
                if (!runtime.TryGetBinding(operation.Code, out S14ConditionBinding binding))
                    throw new InvalidDataException($"Condition stream {program.StreamIndex}, operation {operation.Index}, " +
                        $"offset {operation.Offset}: no executable binding for code {operation.Code}.");
                bindings.Add(operation.Code, binding);
            }

        var callbacks = new List<S14ConditionCallback>();
        int successes = 0;
        int total = 0;
        var frames = new Stack<Frame>();
        frames.Push(new(begin, end, mode != 0, 0, 0));
        bool rootFold = false;
        while (frames.Count != 0)
        {
            Frame frame = frames.Peek();
            if (frame.Cursor >= frame.End)
            {
                frames.Pop();
                if (frames.Count == 0)
                {
                    rootFold = frame.Fold;
                    break;
                }
                Frame parent = frames.Peek();
                int group = parent.PendingGroup;
                parent.PendingGroup = -1;
                Finish(parent, group, frame.Fold ? 1 : 0, false,
                    program.GroupEnds[group] - group + 1);
                continue;
            }

            S14ConditionInstruction operation = program.Operations[frame.Cursor];
            S14ConditionBinding metadata = bindings[operation.Code];
            var context = new S14ConditionContext(program, operation, frame.Begin,
                frame.End, frame.Depth, frame.OrDepth);
            S14ConditionDispatchInputs inputs = runtime.ReadInputs(context);
            bool firstPermission = !inputs.FirstGateEnabled || metadata.FirstPermissionControl == 0;
            int suppression = operation.RawField04 == 1 && inputs.SettingPredicateValue == 0 ||
                operation.RawField04 == 2 && inputs.SettingPredicateValue != 0 ? 1 : operation.RawField06;
            int result = suppression != 0 ? 1 : 0;
            bool permission = suppression == 0 && firstPermission;
            if (permission && inputs.ExecutionGateEnabled)
                permission = metadata.SecondPermissionControl != 0;
            // Native override occurs after permission/result calculation.
            if (operation.Code == 369) suppression = 1;

            if (permission && metadata.HasHandler)
            {
                if (operation.Code is 5 or 7)
                {
                    int groupEnd = program.GroupEnds[frame.Cursor];
                    frame.PendingGroup = frame.Cursor;
                    frames.Push(new(frame.Cursor + 1, groupEnd, operation.Code == 7,
                        frame.Depth + 1, frame.OrDepth + (operation.Code == 5 ? 1 : 0)));
                    continue;
                }
                result = operation.Code switch
                {
                    0 => 0,
                    6 or 8 => 1,
                    _ => runtime.InvokeLeaf(context)
                };
            }
            else if (permission) result = 0; // Explicitly known null handler.
            Finish(frame, frame.Cursor, result, suppression != 0, 1);
        }
        S14ConditionStatus status = runtime.ConditionBlocked ? S14ConditionStatus.Blocked
            : successes == total ? S14ConditionStatus.Satisfied : S14ConditionStatus.Unsatisfied;
        return new(rootFold, callbacks.AsReadOnly(), successes, total, status);

        void Finish(Frame frame, int operationIndex, int result, bool suppressed, int step)
        {
            frame.Fold = frame.All ? frame.Fold && result != 0 : frame.Fold || result != 0;
            if (!suppressed)
            {
                callbacks.Add(new(operationIndex, result, frame.Depth));
                if (frame.Depth == 0)
                {
                    total++;
                    if (result != 0) successes++;
                }
            }
            frame.Cursor += step;
        }
    }
}
