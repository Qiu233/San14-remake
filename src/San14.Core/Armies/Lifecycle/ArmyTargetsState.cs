using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace San14.Core;

public enum ArmyTargetKind { None, City, ArmyReceiver, Unresolved }
public enum ArmyTargetDestinationKind { City, ArmyReceiver, Unresolved }

/// <summary>A destination names a campaign entity, not a source-table row.
/// Unresolved means a selection is known to exist but has no decoded identity.</summary>
public sealed record ArmyTargetDestination(ArmyTargetDestinationKind Kind, int? CityId, ArmyReceiverId? ReceiverId);

/// <summary>Target category and destination knowledge are independent. A null
/// channel is unknown; an unresolved channel is known but not yet understood.</summary>
public sealed record ArmyTargetState(ArmyTargetKind? Kind, ArmyTargetDestination? Destination);
public sealed record ArmyTargetAssignment(ArmyReceiverId ReceiverId, ArmyTargetState Target);

/// <summary>Immutable target choices of reusable receivers. Empty receivers
/// retain their choices until an explicit reset or creator write.</summary>
public sealed class ArmyTargetsState
{
    private readonly IReadOnlyDictionary<ArmyReceiverId, ArmyTargetState> _targets;
    public IReadOnlyList<ArmyTargetAssignment> Assignments { get; }
    [JsonConstructor]
    public ArmyTargetsState(IReadOnlyList<ArmyTargetAssignment> assignments)
    {
        var rows = assignments.ToArray();
        if (rows.Any(row => row is null || row.ReceiverId.Value <= 0 || row.Target is null))
            throw new InvalidDataException("Army target assignments require receiver identities and target knowledge.");
        _targets = new ReadOnlyDictionary<ArmyReceiverId, ArmyTargetState>(rows.ToDictionary(row => row.ReceiverId, row => row.Target));
        Assignments = Array.AsReadOnly(rows);
    }
    public ArmyTargetState? Find(ArmyReceiverId receiverId) => _targets.GetValueOrDefault(receiverId);
    public ArmyTargetsState With(ArmyReceiverId receiverId, ArmyTargetState target) => new(
        _targets.ContainsKey(receiverId)
            ? Assignments.Select(row => row.ReceiverId == receiverId ? new(receiverId, target) : row).ToArray()
            : Assignments.Append(new(receiverId, target)).ToArray());
}

public sealed partial record NativeArmyLifecycleState
{
    public ArmyTargetsState? Targets { get; init; }
}
