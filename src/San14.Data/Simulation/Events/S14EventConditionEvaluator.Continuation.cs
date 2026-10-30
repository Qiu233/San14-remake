using System.Collections.Immutable;

namespace San14.Data.Formats;

/// <summary>The evaluator's own control state, independent of world or Core
/// persistence. Frames are stored from the root to the currently active span.</summary>
internal sealed record S14EventExecutionFrame(int Begin, int End, bool All,
    int Depth, int OrDepth, int Cursor, bool Fold, int PendingGroup, int IfPhase,
    int ThenMarker, int ElseMarker, bool SelectedThen);

internal sealed record S14EventExecutionCheckpoint(ImmutableArray<S14EventExecutionFrame> Frames,
    int SuccessCount, int EvaluatedCount, bool RootFold);

/// <summary>Callbacks belong only to this slice. The checkpoint carries the
/// cumulative counts; an empty frame array means execution has completed.</summary>
internal sealed record S14EventExecutionSlice(S14EventConditionResult Result,
    S14EventExecutionCheckpoint Checkpoint);

public static partial class S14EventConditionEvaluator
{
    internal static S14EventExecutionSlice EvaluateUntilYield(S14EventConditionProgram program,
        IS14EventConditionRuntime runtime, S14EventExecutionCheckpoint? checkpoint, Func<bool> shouldYield,
        Func<S14ConditionContext, bool>? suspendBeforeLeaf = null, Func<bool>? isLeafIncomplete = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        return EvaluateCore(program, runtime, 0, program.Operations.Count, 0, checkpoint, shouldYield, suspendBeforeLeaf, isLeafIncomplete);
    }

    private static S14EventExecutionFrame SnapshotFrame(Frame frame)
        => new(frame.Begin, frame.End, frame.All, frame.Depth, frame.OrDepth,
            frame.Cursor, frame.Fold, frame.PendingGroup, frame.IfPhase,
            frame.ThenMarker, frame.ElseMarker, frame.SelectedThen);

    private static Stack<Frame> RestoreFrames(S14EventConditionProgram program,
        S14EventExecutionCheckpoint? checkpoint, int begin, int end, int mode)
    {
        var result = new Stack<Frame>();
        if (checkpoint is null)
        {
            result.Push(new(begin, end, mode != 0, 0, 0));
            return result;
        }
        int count = program.Operations.Count;
        if (checkpoint.Frames.IsDefault || checkpoint.SuccessCount < 0 ||
            checkpoint.EvaluatedCount < checkpoint.SuccessCount || checkpoint.EvaluatedCount > count)
            throw InvalidCheckpoint("counts or frame storage");
        if (checkpoint.Frames.Length == 0) return result;
        if (checkpoint.RootFold) throw InvalidCheckpoint("unfinished root result");
        for (int index = 0; index < checkpoint.Frames.Length; index++)
        {
            var saved = checkpoint.Frames[index] ?? throw InvalidCheckpoint("null frame");
            if (saved.Begin < 0 || saved.End < saved.Begin || saved.End > count ||
                saved.Cursor < saved.Begin || saved.Cursor > count || saved.Depth != index ||
                saved.OrDepth < 0 || saved.OrDepth > saved.Depth ||
                saved.PendingGroup < -1 || saved.PendingGroup >= count || saved.IfPhase is < 0 or > 2)
                throw InvalidCheckpoint("frame indices");
            if (index == 0)
            {
                if (saved.Begin != begin || saved.End != end || saved.All != (mode != 0) || saved.OrDepth != 0)
                    throw InvalidCheckpoint("root span");
            }
            else ValidateChild(program, checkpoint.Frames[index - 1], saved);
            if (index == checkpoint.Frames.Length - 1 && (saved.PendingGroup != -1 || saved.IfPhase != 0))
                throw InvalidCheckpoint("active child is absent");
            result.Push(new(saved.Begin, saved.End, saved.All, saved.Depth, saved.OrDepth)
            {
                Cursor = saved.Cursor, Fold = saved.Fold, PendingGroup = saved.PendingGroup,
                IfPhase = saved.IfPhase, ThenMarker = saved.ThenMarker,
                ElseMarker = saved.ElseMarker, SelectedThen = saved.SelectedThen
            });
        }
        return result;
    }

    private static void ValidateChild(S14EventConditionProgram program,
        S14EventExecutionFrame parent, S14EventExecutionFrame child)
    {
        int group = parent.PendingGroup;
        if (group < 0 || parent.Cursor != group || group < parent.Begin || group >= parent.End)
            throw InvalidCheckpoint("pending group");
        int closing = program.GroupEnds[group];
        if (closing < group || closing >= program.Operations.Count)
            throw InvalidCheckpoint("pending group end");
        int code = program.Operations[group].Code;
        int childBegin, childEnd, childOrDepth = parent.OrDepth;
        bool childAll;
        if (code == 1)
        {
            if (parent.IfPhase is not (1 or 2) || parent.ThenMarker <= group ||
                parent.ThenMarker >= closing || program.Operations[parent.ThenMarker].Code != 2 ||
                parent.ElseMarker != -1 && (parent.ElseMarker <= parent.ThenMarker ||
                    parent.ElseMarker >= closing || program.Operations[parent.ElseMarker].Code != 3))
                throw InvalidCheckpoint("If branch markers");
            childAll = true;
            if (parent.IfPhase == 1) { childBegin = group + 1; childEnd = parent.ThenMarker; }
            else
            {
                if (!parent.SelectedThen && parent.ElseMarker < 0)
                    throw InvalidCheckpoint("absent Else branch");
                childBegin = (parent.SelectedThen ? parent.ThenMarker : parent.ElseMarker) + 1;
                childEnd = parent.SelectedThen && parent.ElseMarker >= 0 ? parent.ElseMarker : closing;
            }
        }
        else
        {
            if (parent.IfPhase != 0 || code is not (5 or 7 or 372) &&
                !(code == 216 && program.PersonListConditionBlocks))
                throw InvalidCheckpoint("group phase");
            bool isOr = code is 5 or 372;
            childAll = !isOr;
            childOrDepth += isOr ? 1 : 0;
            childBegin = group + 1; childEnd = closing;
        }
        if (child.Begin != childBegin || child.End != childEnd || child.All != childAll || child.OrDepth != childOrDepth)
            throw InvalidCheckpoint("child span");
    }

    private static InvalidDataException InvalidCheckpoint(string field)
        => new("Event execution checkpoint has invalid " + field + ".");
}
