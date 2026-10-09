namespace San14.Core;

/// <summary>In-memory composition of a single source's next preparation order.
/// This is not a submitted reservation and is not part of the saved world.
/// Eligibility, authority and resource accounting remain in GameEngine.</summary>
public sealed class DeploymentCompositionDraft
{
    private readonly List<DeploymentUnitRequest> _units = [];
    public int? ForceId { get; private set; }
    public int? SourceLocationId { get; private set; }
    public int Count => _units.Count;
    public long Troops => _units.Sum(unit => (long)unit.Troops);
    public bool ContainsOfficer(int officerId) => _units.Any(unit => unit.OfficerId == officerId);

    public void BindSource(int? forceId, int? sourceLocationId)
    {
        if (ForceId == forceId && SourceLocationId == sourceLocationId) return;
        Clear(); ForceId = forceId; SourceLocationId = sourceLocationId;
    }

    /// <summary>Editing the same commander replaces that row in place.</summary>
    public bool Put(DeploymentUnitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ForceId is null || SourceLocationId is null) return false;
        int index = _units.FindIndex(unit => unit.OfficerId == request.OfficerId);
        if (index < 0 && Count >= DeploymentCommandRules.MaximumUnitsPerBatch) return false;
        DeploymentUnitRequest copied = Copy(request);
        if (index >= 0) _units[index] = copied;
        else _units.Add(copied);
        return true;
    }

    public bool Remove(int officerId) => _units.RemoveAll(unit => unit.OfficerId == officerId) > 0;
    public void Clear() => _units.Clear();
    public DeploymentUnitRequest[] Snapshot() => _units.Select(Copy).ToArray();
    public PrepareDeploymentOrder? CreateOrder() => ForceId is int force && SourceLocationId is int source && Count > 0
        ? new(force, source, Snapshot()) : null;

    public bool Submit(Func<PrepareDeploymentOrder, bool> issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (CreateOrder() is not { } order || !issue(order)) return false;
        Clear(); return true;
    }

    private static DeploymentUnitRequest Copy(DeploymentUnitRequest unit) => unit with
    {
        TacticIds = Array.AsReadOnly(unit.TacticIds.ToArray()),
        Actions = Array.AsReadOnly(unit.Actions.ToArray()),
        WaypointHexIds = Array.AsReadOnly(unit.WaypointHexIds.ToArray())
    };
}
