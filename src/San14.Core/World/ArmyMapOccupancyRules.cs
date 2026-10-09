namespace San14.Core;

public readonly record struct ArmyMapOccupancyWrite(int HexId, ArmyMapCell Cell);
public sealed record ArmyMapOccupancyResult(ArmyMapOccupancyState State, ArmyMapOccupancyWrite[] Writes);

public static class ArmyMapOccupancyRules
{
    public static ArmyMapOccupancyState CreateBinding(ArmyMapOccupancyState state, int bindingId,
        ArmyOccupancyUnitReference? target = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bindingId);
        var next = state.Copy();
        next.Bindings.Add(bindingId, target ?? ArmyOccupancyUnitReference.Vacant);
        return next;
    }

    /// <summary>Assignment retargets every existing marker for this binding.
    /// It does not add, clear or rebuild map cells.</summary>
    public static ArmyMapOccupancyState? AssignBinding(ArmyMapOccupancyState state, int bindingId, int unitId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bindingId);
        ArgumentOutOfRangeException.ThrowIfNegative(unitId);
        if (!state.Bindings.ContainsKey(bindingId)) return null;
        var next = state.Copy();
        next.Bindings[bindingId] = ArmyOccupancyUnitReference.ForUnit(unitId);
        return next;
    }

    /// <summary>Removing the active projection retains its logical identity
    /// while the caller still owns the pending army image.</summary>
    public static ArmyMapOccupancyState? RetainPending(ArmyMapOccupancyState state, int bindingId, int unitId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bindingId);
        ArgumentOutOfRangeException.ThrowIfNegative(unitId);
        return state.GetBinding(bindingId) is { Kind: ArmyOccupancyUnitKind.Unit } target && target.UnitId == unitId
            ? state.Copy() : null;
    }

    /// <summary>Actual receiver cleanup leaves its map markers intact. The
    /// vacant binding remains available for a later assignment.</summary>
    public static ArmyMapOccupancyState? ReleaseBinding(ArmyMapOccupancyState state, int bindingId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bindingId);
        if (!state.Bindings.ContainsKey(bindingId)) return null;
        var next = state.Copy();
        next.Bindings[bindingId] = ArmyOccupancyUnitReference.Vacant;
        return next;
    }

    public static ArmyMapOccupancyState? SetCell(ArmyMapOccupancyState state, int hexId, ArmyMapCell cell) =>
        ApplyOrderedWrites(state, [new(hexId, cell)])?.State;

    /// <summary>Return and destruction clear the reached current cell even if
    /// another binding has since overwritten it.</summary>
    public static ArmyMapOccupancyState ClearCurrentCell(ArmyMapOccupancyState state, int hexId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hexId);
        var next = state.Copy();
        WriteCell(next, new(hexId, ArmyMapCell.Empty));
        return next;
    }

    /// <summary>Apply caller order without deduplication or an empty-cell gate.
    /// A missing binding rejects the candidate without modifying the input.</summary>
    public static ArmyMapOccupancyResult? ApplyOrderedWrites(ArmyMapOccupancyState state,
        ReadOnlySpan<ArmyMapOccupancyWrite> writes)
    {
        var next = state.Copy();
        foreach (var write in writes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(write.HexId);
            ArgumentNullException.ThrowIfNull(write.Cell);
            if (write.Cell.Kind == ArmyMapCellKind.Binding &&
                (write.Cell.BindingId is not int bindingId || !next.Bindings.ContainsKey(bindingId))) return null;
            WriteCell(next, write);
        }
        return new(next, writes.ToArray());
    }

    /// <summary>The caller supplies the already admitted ordered traversal.
    /// Each former cell is cleared only when it names this binding; each
    /// destination is overwritten. All edges publish as one detached candidate.</summary>
    public static ArmyMapOccupancyResult? TryApplyTraversal(ArmyMapOccupancyState state,
        int bindingId, int fromHex, ReadOnlySpan<int> destinations)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bindingId);
        ArgumentOutOfRangeException.ThrowIfNegative(fromHex);
        if (!state.Bindings.ContainsKey(bindingId)) return null;
        var next = state.Copy();
        var writes = new List<ArmyMapOccupancyWrite>();
        int from = fromHex;
        foreach (int to in destinations)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(to);
            ArmyMapCell previous = next.ReadCell(from);
            if (previous.Kind == ArmyMapCellKind.Unknown) return null;
            if (previous is { Kind: ArmyMapCellKind.Binding } && previous.BindingId == bindingId)
                Write(new(from, ArmyMapCell.Empty));
            Write(new(to, ArmyMapCell.ForBinding(bindingId)));
            from = to;
        }
        return new(next, writes.ToArray());

        void Write(ArmyMapOccupancyWrite write)
        {
            WriteCell(next, write);
            writes.Add(write);
        }
    }

    private static void WriteCell(ArmyMapOccupancyState state, ArmyMapOccupancyWrite write)
    {
        bool isDefault = write.Cell == (state.DefaultCellKnowledge == ArmyMapDefaultCellKnowledge.Empty
            ? ArmyMapCell.Empty : ArmyMapCell.Unknown);
        if (isDefault) state.Cells.Remove(write.HexId);
        else state.Cells[write.HexId] = write.Cell;
    }
}
