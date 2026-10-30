using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace San14.Data.Formats;

/// <summary>An immutable snapshot of a condition operation, including all original bytes.</summary>
public sealed record S14ConditionInstruction(int Index, int Offset, int Code,
    short RawField04, short RawField06, IReadOnlyList<int> RawArguments,
    IReadOnlyList<byte> RawBytes);

/// <summary>
/// A validated If/Then/Else and OR/AND source program. Nonzero dispatch controls are retained.
/// Matching is strict across the full stream; malformed native recovery and
/// other statement languages are outside this adapter's supported profile.
/// </summary>
public sealed class S14EventConditionProgram
{
    public int StreamIndex { get; }
    public int Offset { get; }
    public IReadOnlyList<S14ConditionInstruction> Operations { get; }
    internal int[] GroupEnds { get; }
    internal bool PersonListConditionBlocks { get; }

    private S14EventConditionProgram(int streamIndex, int offset,
        S14ConditionInstruction[] operations, int[] groupEnds, bool personListConditionBlocks)
    {
        StreamIndex = streamIndex;
        Offset = offset;
        Operations = Array.AsReadOnly(operations);
        GroupEnds = groupEnds;
        PersonListConditionBlocks = personListConditionBlocks;
    }

    public static S14EventConditionProgram Read(S14EventStream stream) => Read(stream, true);

    /// <summary>Presentation execution appends the Person list and continues
    /// at the next instruction; only condition execution consumes its AND block.</summary>
    public static S14EventConditionProgram ReadExecution(S14EventStream stream) => Read(stream, false);

    private static S14EventConditionProgram Read(S14EventStream stream, bool personListConditionBlocks)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.Index < 0 || stream.Offset < 0 || stream.Length < 0 ||
            stream.Operations is null || stream.Length % S14EventReader.OperationBytes != 0 ||
            stream.Operations.Count != stream.Length / S14EventReader.OperationBytes ||
            (long)stream.Offset + stream.Length > int.MaxValue)
            throw new InvalidDataException("Condition program has an invalid source window.");

        var operations = new S14ConditionInstruction[stream.Operations.Count];
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
            int[] arguments = new int[8];
            for (int argument = 0; argument < arguments.Length; argument++)
                arguments[argument] = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12 + argument * 4));
            operations[index] = new(index, source.Offset, source.Code,
                source.RawField04, source.RawField06, Array.AsReadOnly(arguments), Array.AsReadOnly(bytes));
        }
        int[] ends = ReadGroupEnds(operations.Select(operation => operation.Code).ToArray(), out _, personListConditionBlocks);
        return new(stream.Index, stream.Offset, operations, ends, personListConditionBlocks);
    }

    // The catalog uses the same structural profile to locate a rejected source
    // operation. List blocks use the native independent end scan, not a mixed
    // opener stack: only ordinary OR/AND groups contribute to that scan's depth.
    internal static int[] ReadGroupEnds(IReadOnlyList<int> codes, out int failureIndex,
        bool personListConditionBlocks = true)
    {
        int[] ends = Enumerable.Repeat(-1, codes.Count).ToArray();
        var stack = new Stack<int>();
        for (int index = 0; index < codes.Count; index++)
        {
            failureIndex = index;
            int code = codes[index];
            if (code is < 0 or >= 453)
                throw new InvalidDataException($"Condition program operation {index} has unsupported code {code}.");
            if (code is 1 or 5 or 7)
                stack.Push(index);
            else if (code is 2 or 3)
            {
                if (stack.Count == 0 || codes[stack.Peek()] != 1)
                    throw new InvalidDataException($"Condition program operation {index} has an unmatched If branch.");
                int start = stack.Peek();
                bool seenThen = false, seenElse = false;
                for (int part = start + 1; part < index; part++)
                {
                    if (codes[part] is 1 or 5 or 7) { part = ends[part]; continue; }
                    seenThen |= codes[part] == 2; seenElse |= codes[part] == 3;
                }
                if (code == 2 && seenThen || code == 3 && (!seenThen || seenElse))
                    throw new InvalidDataException($"Condition program operation {index} has a repeated or unordered If branch.");
            }
            else if (code is 4 or 6 or 8)
            {
                if (stack.Count == 0 || (code == 4 ? codes[stack.Peek()] != 1 : codes[stack.Peek()] + 1 != code))
                    throw new InvalidDataException($"Condition program operation {index} has an unmatched group end.");
                int start = stack.Pop();
                if (code == 4)
                {
                    bool then = false;
                    for (int part = start + 1; part < index; part++)
                    {
                        if (codes[part] is 1 or 5 or 7) { part = ends[part]; continue; }
                        then |= codes[part] == 2;
                    }
                    if (!then) throw new InvalidDataException($"Condition program operation {start} has no Then marker.");
                }
                ends[start] = index;
            }
        }
        failureIndex = stack.Count == 0 ? -1 : stack.Peek();
        if (stack.Count != 0)
            throw new InvalidDataException($"Condition program operation {stack.Peek()} has an unmatched group begin.");
        var listGroups = new List<int>();
        var cityEnds = new HashSet<int>();
        for (int index = 0; index < codes.Count; index++)
        {
            int opener = codes[index];
            if (opener != 372 && (opener != 216 || !personListConditionBlocks)) continue;
            failureIndex = index;
            int closing = opener == 216 ? 257 : 379;
            int depth = 0;
            for (int next = index + 1; next < codes.Count; next++)
            {
                int code = codes[next];
                if (code == opener)
                {
                    failureIndex = next;
                    throw new InvalidDataException($"Condition program operation {next} has an unsupported nested list block.");
                }
                if (code == closing && depth == 0) { ends[index] = next; break; }
                if (code is 1 or 5 or 7) depth++;
                else if (code is 4 or 6 or 8) depth--;
                if (depth < 0) break;
            }
            if (ends[index] < 0)
                throw new InvalidDataException($"Condition program operation {index} has an unmatched list block.");
            listGroups.Add(index);
            if (opener == 372) cityEnds.Add(ends[index]);
        }
        foreach (int first in listGroups)
            foreach (int second in listGroups)
                if (first < second && second < ends[first] && ends[first] < ends[second])
                {
                    failureIndex = second;
                    throw new InvalidDataException($"Condition program operation {second} has an unsupported crossing list block.");
                }
        for (int index = 0; index < codes.Count; index++)
            if (codes[index] == 379 && !cityEnds.Contains(index))
            {
                failureIndex = index;
                throw new InvalidDataException($"Condition program operation {index} has an unmatched city-list end.");
            }
        failureIndex = -1;
        return ends;
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
    // Playback context1D8/1DC overrides the already evaluated If predicate.
    // It is independent of settings-based dispatch suppression.
    bool? ForcedIfResult => null;
    bool TryGetBinding(int code, out S14ConditionBinding binding);
    S14ConditionDispatchInputs ReadInputs(S14ConditionContext context);
    int InvokeLeaf(S14ConditionContext context);
    // Only condition-mode execution owns this AND sub-block. Presentation
    // runtimes must not inherit the condition branch for execution+40 != null.
    void PreparePersonListGroup(S14ConditionContext context) =>
        throw new InvalidDataException("Person-list condition group has no current runtime owner.");
    void PrepareCityListGroup(S14ConditionContext context) =>
        throw new InvalidDataException("City-list condition group has no current runtime owner.");
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
public static partial class S14EventConditionEvaluator
{
    public static IReadOnlyDictionary<int, S14ConditionBinding> BuiltInBindings { get; } =
        new ReadOnlyDictionary<int, S14ConditionBinding>(new Dictionary<int, S14ConditionBinding>
        {
            [0] = new(0, 0, true),
            [1] = new(0, 1, true), [2] = new(0, 1, true),
            [3] = new(0, 1, true), [4] = new(0, 1, true),
            [5] = new(0, 1, true), [6] = new(0, 1, true),
            [7] = new(0, 1, true), [8] = new(0, 1, true),
            [257] = new(0, 1, true), [379] = new(0, 1, true)
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
        public int IfPhase { get; set; }
        public int ThenMarker { get; set; }
        public int ElseMarker { get; set; }
        public bool SelectedThen { get; set; }
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
        => EvaluateCore(program, runtime, begin, end, mode, null, static () => false).Result;

    private static S14EventExecutionSlice EvaluateCore(S14EventConditionProgram program,
        IS14EventConditionRuntime runtime, int begin, int end, int mode,
        S14EventExecutionCheckpoint? checkpoint, Func<bool> shouldYield,
        Func<S14ConditionContext, bool>? suspendBeforeLeaf = null, Func<bool>? isLeafIncomplete = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(shouldYield);
        if (begin < 0 || end < begin || end > program.Operations.Count)
            throw new InvalidDataException("Condition evaluation span is outside the validated program.");
        var callbacks = new List<S14ConditionCallback>();
        int successes = checkpoint?.SuccessCount ?? 0;
        int total = checkpoint?.EvaluatedCount ?? 0;
        var frames = RestoreFrames(program, checkpoint, begin, end, mode);
        bool rootFold = checkpoint?.RootFold ?? false;
        if (frames.Count == 0) return CompleteSlice();
        var bindings = new Dictionary<int, S14ConditionBinding>();
        foreach (S14ConditionInstruction operation in program.Operations)
            if (!bindings.ContainsKey(operation.Code))
            {
                if (!runtime.TryGetBinding(operation.Code, out S14ConditionBinding binding))
                    throw new InvalidDataException($"Condition stream {program.StreamIndex}, operation {operation.Index}, " +
                        $"offset {operation.Offset}: no executable binding for code {operation.Code}.");
                bindings.Add(operation.Code, binding);
            }

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
                if (parent.IfPhase == 1)
                {
                    bool selected = runtime.ForcedIfResult ?? frame.Fold;
                    parent.SelectedThen = selected;
                    parent.IfPhase = 2;
                    int branchBegin = selected ? parent.ThenMarker + 1 : parent.ElseMarker + 1;
                    int groupEnd = program.GroupEnds[group];
                    int branchEnd = selected ? (parent.ElseMarker < 0 ? groupEnd : parent.ElseMarker) : groupEnd;
                    if (!selected && parent.ElseMarker < 0)
                    {
                        parent.IfPhase = 0; parent.PendingGroup = -1;
                        Finish(parent, group, 1, false, groupEnd - group + 1);
                    }
                    else frames.Push(new(branchBegin, branchEnd, true, parent.Depth + 1, parent.OrDepth));
                    continue;
                }
                if (parent.IfPhase == 2)
                {
                    if (parent.SelectedThen)
                        callbacks.Add(new(parent.ThenMarker, frame.Fold ? 1 : 0, parent.Depth + 1));
                    parent.IfPhase = 0; parent.PendingGroup = -1;
                    Finish(parent, group, frame.Fold ? 1 : 0, false, program.GroupEnds[group] - group + 1);
                    continue;
                }
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
                if (operation.Code == 1)
                {
                    int groupEnd = program.GroupEnds[frame.Cursor];
                    int then = -1, alternate = -1;
                    for (int part = frame.Cursor + 1; part < groupEnd; part++)
                    {
                        int code = program.Operations[part].Code;
                        if (code is 1 or 5 or 7) { part = program.GroupEnds[part]; continue; }
                        if (code == 2) then = part;
                        if (code == 3) alternate = part;
                    }
                    if (then < 0) throw new InvalidDataException("If block has no Then marker.");
                    frame.PendingGroup = frame.Cursor; frame.IfPhase = 1;
                    frame.ThenMarker = then; frame.ElseMarker = alternate;
                    //3C2A80 evaluates the condition span in All mode even
                    //when playback will subsequently override its result.
                    frames.Push(new(frame.Cursor + 1, then, true, frame.Depth + 1, frame.OrDepth));
                    continue;
                }
                if (operation.Code is 5 or 7 or 372 ||
                    operation.Code == 216 && program.PersonListConditionBlocks)
                {
                    if (operation.Code == 216) runtime.PreparePersonListGroup(context);
                    if (operation.Code == 372) runtime.PrepareCityListGroup(context);
                    int groupEnd = program.GroupEnds[frame.Cursor];
                    frame.PendingGroup = frame.Cursor;
                    bool isOr = operation.Code is 5 or 372;
                    frames.Push(new(frame.Cursor + 1, groupEnd, !isOr,
                        frame.Depth + 1, frame.OrDepth + (isOr ? 1 : 0)));
                    continue;
                }
                if (operation.Code is not (0 or 2 or 3 or 4 or 6 or 8 or 257 or 379) &&
                    suspendBeforeLeaf?.Invoke(context) == true)
                    return CompleteSlice();
                result = operation.Code switch
                {
                    0 => 0,
                    2 or 3 or 4 or 6 or 8 or 257 or 379 => 1,
                    _ => runtime.InvokeLeaf(context)
                };
            }
            else if (permission) result = 0; // Explicitly known null handler.
            // A real modal business continuation retains this exact uncompleted
            // leaf. It produces neither a callback nor a folded success result.
            if (isLeafIncomplete?.Invoke() == true) return CompleteSlice();
            Finish(frame, frame.Cursor, result, suppression != 0, 1);
            //A leaf is committed through Finish before the host observes
            //its commands. Resuming starts at the next cursor, including
            //when an enclosing group still needs its completion callback.
            if (shouldYield()) return CompleteSlice();
        }
        return CompleteSlice();

        S14EventExecutionSlice CompleteSlice()
        {
            S14ConditionStatus status = runtime.ConditionBlocked ? S14ConditionStatus.Blocked
                : successes == total ? S14ConditionStatus.Satisfied : S14ConditionStatus.Unsatisfied;
            return new(new(rootFold, callbacks.AsReadOnly(), successes, total, status),
                new(frames.Reverse().Select(SnapshotFrame).ToImmutableArray(), successes, total, rootFold));
        }

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
