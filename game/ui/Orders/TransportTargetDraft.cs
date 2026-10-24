using San14.Core;

/// <summary>The accepted quantity page owns cargo and preferences separately
/// from its later target. Cargo supplies never stand for the convoy's food
/// consumption. Unknown preferences are resolved before creating this value.</summary>
internal sealed record TransportSetupSelection(int OfficerId, int Troops, int CargoGold,
    int CargoSupplies, int NavalFormationId, bool? ReturnOfficerToSource, int? SourceBehaviorChoice);

/// <summary>One unsubmitted mode2 row. Saved local target points and the fresh
/// selected route are separate, just as they are for the existing Army page.
/// This editor never creates an Army or edits a campaign world.</summary>
internal sealed class TransportTargetDraft(TransportSetupSelection selection, int currentHex)
{
    private readonly List<int> activePoints = [];
    private int[] acceptedPoints = [];
    internal TransportSetupSelection Selection { get; } = selection;
    internal int CurrentHex { get; } = currentHex;
    internal bool Selected { get; private set; } = true;
    internal DeploymentAction? Action { get; private set; }
    internal int? TargetHex { get; private set; }
    internal IReadOnlyList<int> Waypoints => Selected ? activePoints : acceptedPoints;
    internal int ActiveWaypointIndex => Selected ? activePoints.Count : 0;
    internal int? EstimatedDays { get; private set; }
    internal bool? ReturnOfficerToSource { get; private set; } = selection.ReturnOfficerToSource;
    internal int? SourceBehaviorChoice { get; private set; } = selection.SourceBehaviorChoice;
    internal bool CanDecide => Action is not null && ReturnOfficerToSource.HasValue && SourceBehaviorChoice.HasValue;

    internal void ChangeReturnOfficer(bool value) => ReturnOfficerToSource = value;
    internal void ChangeSourceBehavior(int value)
    {
        if (value is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(value));
        SourceBehaviorChoice = value;
    }

    internal void Select(bool value)
    {
        if (Selected == value) return;
        Selected = value;
        // A reselected row starts a new active group at its current Hex. It
        // does not import the previously assigned target's intermediate points.
        activePoints.Clear();
    }
    internal bool Append(int hex)
    {
        if (!Selected || activePoints.Count >= 5) return false;
        activePoints.Add(hex); return true;
    }
    internal bool Undo()
    {
        if (!Selected || activePoints.Count == 0) return false;
        activePoints.RemoveAt(activePoints.Count - 1); return true;
    }
    internal void Assign(DeploymentAction action, int targetHex, int? days)
    {
        if (!Selected) throw new InvalidOperationException("Transport assignment requires its selected row.");
        Action = action; TargetHex = targetHex; EstimatedDays = days;
        acceptedPoints = activePoints.ToArray(); activePoints.Clear(); Selected = false;
    }
    internal DispatchTransportArmyOrder CreateOrder(int force, int source)
    {
        if (Action is not { } action) throw new InvalidOperationException("Transport has no assigned target.");
        if (ReturnOfficerToSource is not bool returnOfficer || SourceBehaviorChoice is not int behavior)
            throw new InvalidOperationException("Transport preferences have not been selected.");
        return new(force, source, Selection.OfficerId, Selection.Troops, Selection.CargoSupplies,
            Selection.CargoGold, Selection.NavalFormationId, action, acceptedPoints.ToArray(),
            returnOfficer, behavior);
    }
}
