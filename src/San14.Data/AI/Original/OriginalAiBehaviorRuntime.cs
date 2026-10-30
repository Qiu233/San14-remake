using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>One actor's node cursors. It never owns a borrowed operation,
/// GameState, resource tree or compiled delegate. A scheduler takes a snapshot
/// only when that actor's real update has completed.</summary>
public sealed class OriginalAiBehaviorRuntime
{
    private readonly Dictionary<(uint Bundle, int Node), int> running = [];

    public OriginalAiBehaviorRuntime() { }

    public OriginalAiBehaviorRuntime(OriginalAiActorExecution saved)
    {
        if (saved.RunningChildren.IsDefault) throw new InvalidDataException("original_ai.runtime.saved_children");
        foreach (var node in saved.RunningChildren)
        {
            if (node.NodeIndex < 0 || node.ChildIndex < 0 ||
                !running.TryAdd((node.BundleKey, node.NodeIndex), node.ChildIndex))
                throw new InvalidDataException("original_ai.runtime.saved_cursor");
        }
    }

    internal bool TryChild(uint bundle, int node, out int child) => running.TryGetValue((bundle, node), out child);
    internal void KeepChild(uint bundle, int node, int child) => running[(bundle, node)] = child;
    internal void ClearNode(uint bundle, int node) => running.Remove((bundle, node));

    /// <summary>Actual source exit/reset of this compiled subtree. Only the
    /// actor's entered controls are visited; no world or resource scan occurs.</summary>
    internal void Exit(uint bundle, ImmutableHashSet<int> subtree)
    {
        foreach (var key in running.Keys.Where(key => key.Bundle == bundle && subtree.Contains(key.Node)).ToArray())
            running.Remove(key);
    }

    public void Reset() => running.Clear();

    public OriginalAiActorExecution Capture(OriginalAiActorIdentity actor) => new(actor,
        running.OrderBy(pair => pair.Key.Bundle).ThenBy(pair => pair.Key.Node)
            .Select(pair => new OriginalAiRunningChild(pair.Key.Bundle, pair.Key.Node, pair.Value)).ToImmutableArray());
}
