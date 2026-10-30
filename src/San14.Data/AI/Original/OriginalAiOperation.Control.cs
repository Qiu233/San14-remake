using System.Collections.Immutable;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    private OriginalAiBehaviorRuntime? behaviorRuntime;

    /// <summary>Bind this operation to its actual actor's retained node state.
    /// The runtime contains no borrowed world and must not be shared by actors.</summary>
    public void UseBehaviorRuntime(OriginalAiBehaviorRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (behaviorRuntime is not null && !ReferenceEquals(behaviorRuntime, runtime))
            throw new InvalidOperationException("Original AI operation already has its actor runtime.");
        behaviorRuntime = runtime;
    }

    private OriginalAiBehaviorRuntime BehaviorRuntime => behaviorRuntime ??= new();

    internal void ExitBehaviorSubtree(uint bundle, ImmutableHashSet<int> subtree) => BehaviorRuntime.Exit(bundle, subtree);

    internal OriginalAiNodeStatus ControlRoot(uint bundle, int node,
        Func<OriginalAiOperation, OriginalAiNodeStatus> child, ImmutableHashSet<int> childSubtree)
    {
        var status = child(this);
        if (status == OriginalAiNodeStatus.Running) BehaviorRuntime.KeepChild(bundle, node, 0);
        else
        {
            BehaviorRuntime.Exit(bundle, childSubtree);
            BehaviorRuntime.ClearNode(bundle, node);
        }
        return status;
    }

    internal OriginalAiNodeStatus ControlGuard(uint bundle, int node,
        Func<OriginalAiOperation, bool> predicate,
        Func<OriginalAiOperation, OriginalAiNodeStatus> child, ImmutableHashSet<int> childSubtree)
    {
        // A running child resumes without evaluating the guard again. This
        // differs from merely re-running the preceding expression on a new World.
        if (!BehaviorRuntime.TryChild(bundle, node, out _) && !predicate(this))
            return OriginalAiNodeStatus.Failure;
        var status = child(this);
        if (status == OriginalAiNodeStatus.Running) BehaviorRuntime.KeepChild(bundle, node, 0);
        else
        {
            BehaviorRuntime.Exit(bundle, childSubtree);
            BehaviorRuntime.ClearNode(bundle, node);
        }
        return status;
    }

    internal OriginalAiNodeStatus ControlSequence(uint bundle, int node,
        OriginalAiSequentialPolicy policy, bool continueInSameUpdate,
        ImmutableArray<Func<OriginalAiOperation, OriginalAiNodeStatus>> children,
        ImmutableArray<bool> eligible, ImmutableArray<ImmutableHashSet<int>> childSubtrees)
    {
        int child;
        if (BehaviorRuntime.TryChild(bundle, node, out child))
        {
            if ((uint)child >= (uint)children.Length)
                throw new InvalidDataException("original_ai.runtime.sequence_child");
        }
        else
        {
            child = NextEligible(0);
            // The source update precondition fails before policy dispatch.
            if (child == children.Length) return OriginalAiNodeStatus.Failure;
        }
        while (true)
        {
            var status = children[child](this);
            if (status == OriginalAiNodeStatus.Running)
            {
                BehaviorRuntime.KeepChild(bundle, node, child);
                return status;
            }
            BehaviorRuntime.Exit(bundle, childSubtrees[child]);
            bool stop = policy switch
            {
                OriginalAiSequentialPolicy.FirstSuccess => status == OriginalAiNodeStatus.Success,
                OriginalAiSequentialPolicy.RequireAllSuccess => status == OriginalAiNodeStatus.Failure,
                OriginalAiSequentialPolicy.VisitAll => false,
                _ => throw new InvalidDataException("original_ai.runtime.sequence_policy")
            };
            if (stop)
            {
                BehaviorRuntime.ClearNode(bundle, node);
                return status;
            }
            child = NextEligible(child + 1);
            if (child == children.Length)
            {
                BehaviorRuntime.ClearNode(bundle, node);
                return policy == OriginalAiSequentialPolicy.FirstSuccess
                    ? OriginalAiNodeStatus.Failure : OriginalAiNodeStatus.Success;
            }
            BehaviorRuntime.KeepChild(bundle, node, child);
            if (!continueInSameUpdate) return OriginalAiNodeStatus.Running;
        }

        int NextEligible(int start)
        {
            while (start < children.Length && !eligible[start]) start++;
            return start;
        }
    }
}
