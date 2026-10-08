/// <summary>The explicit backend's small current target stack. This records
/// row selection/return only, never geometry submissions or texture content.</summary>
internal sealed class OriginalWorldGeometryTargetScopeState
{
    private readonly Stack<OriginalWorldGeometryTargetRow> parents = new();
    private OriginalWorldGeometryTargetRow? prior, current;
    private int next;
    private readonly int[] operations;
    public OriginalWorldGeometryTargetScopeState(bool includesRs4 = false)
        => operations = includesRs4 ? [43,47,48,52,53,59] : [48,52,53,59];
    public OriginalWorldGeometryTargetRow Current => current ?? throw new InvalidOperationException("No current geometry target row.");
    public OriginalWorldGeometryTargetRow Parent => parents.TryPeek(out var row) ? row :
        throw new InvalidOperationException("Geometry target stack has no parent to restore.");
    public bool ReturnedToPrior => next == operations.Length && parents.Count == 0 && ReferenceEquals(prior, current);
    public void Begin(OriginalWorldGeometryTargetRow actualPrior)
    {
        if (current is not null) throw new InvalidOperationException("Previous geometry target scope remains active.");
        actualPrior.Domain.Validate();
        if (actualPrior.TargetStackIndex < 0) throw new InvalidDataException("Actual target-stack index is invalid.");
        prior = current = actualPrior; next = 0;
    }
    public void RequireNext(int node)
    {
        if (current is null || next >= operations.Length || node != operations[next])
            throw new InvalidDataException("Geometry target operations must follow their loaded source order; intervening draws belong to their owners.");
    }
    public OriginalWorldGeometryTargetRow Push(int node, OriginalRenderTargetFrame actualFrame,
        OriginalWorldGeometryDrawDomain actualDomain)
    {
        RequireNext(node);
        if (node is not (43 or 48 or 53) || actualFrame.Source.Node.Index != node)
            throw new InvalidDataException("Only the actual selected geometry bind pushes its own target row.");
        var row = new OriginalWorldGeometryTargetRow(actualFrame, checked(Current.TargetStackIndex + 1), actualDomain);
        parents.Push(Current); current = row; next++; return row;
    }
    public void SkipBind(int node)
    {
        RequireNext(node);
        if (node is not (43 or 48 or 53)) throw new InvalidDataException("Only bind nodes have a source no-bind gate.");
        next++;
    }
    public OriginalWorldGeometryTargetRow Pop(int node, bool shouldPop)
    {
        RequireNext(node);
        if (node is not (47 or 52 or 59)) throw new InvalidDataException("Only actual geometry pop nodes return a target row.");
        if (shouldPop) current = parents.Pop();
        next++; return Current;
    }
    public void End()
    {
        if (!ReturnedToPrior) throw new InvalidOperationException("Geometry target rows did not return to the actual prior row.");
        Retire();
    }
    public void Retire() { parents.Clear(); prior = current = null; next = 0; }
}
