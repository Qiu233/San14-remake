using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace San14.Core;

public enum ArmyReceiverPhase { Unknown, Ready, Reserved, Active, Pending }

/// <summary>A reusable campaign receiver. Pending images remain owned here
/// after the active unit leaves the world, until retirement cleanup completes.</summary>
public sealed record ArmyReceiverState(ArmyReceiverId ReceiverId, bool CanAssign,
    ArmyReceiverPhase Phase, int? ActiveUnitId, UnitState? PendingImage)
{
    public ArmyStoreKnowledge StoredResources { get; init; } = ArmyStoreKnowledge.Empty;
    public ArmyOverrideTargetState? OverrideTarget { get; init; }
    public bool? GroupAssignmentReady { get; init; }
    public int? OccupancyBindingId { get; init; }

    public ArmyReceiverState Assign(int unitId) => CanAssign && Phase == ArmyReceiverPhase.Ready && unitId >= 0
        ? this with { Phase = ArmyReceiverPhase.Active, ActiveUnitId = unitId, PendingImage = null }
        : throw new InvalidDataException("Army receiver is not ready for assignment.");

    public ArmyReceiverState Retire(UnitState image) => Phase == ArmyReceiverPhase.Active &&
        ActiveUnitId == image.Id && PendingImage is null
        ? this with { Phase = ArmyReceiverPhase.Pending, ActiveUnitId = null, PendingImage = image }
        : throw new InvalidDataException("Army retirement does not identify an active receiver.");

    public ArmyReceiverState Release() => Phase == ArmyReceiverPhase.Pending
        ? this with { Phase = ArmyReceiverPhase.Ready, ActiveUnitId = null, PendingImage = null }
        : throw new InvalidDataException("Army receiver cleanup has no pending retirement.");
}

/// <summary>Ordered campaign receiver ownership, independent of resource-table
/// ordinals and header encodings. Updates retain receiver identity and order.</summary>
public sealed class ArmyReceiverRoster
{
    private readonly IReadOnlyDictionary<ArmyReceiverId, ArmyReceiverState> _byId;
    public IReadOnlyList<ArmyReceiverState> Entries { get; }

    [JsonConstructor]
    public ArmyReceiverRoster(IReadOnlyList<ArmyReceiverState> entries) : this(entries.Select(entry =>
        entry with { PendingImage = GameStateCopy.Copy(entry.PendingImage) }).ToArray(), ownsImages: true) { }

    private ArmyReceiverRoster(ArmyReceiverState[] owned, bool ownsImages)
    {
        _byId = new ReadOnlyDictionary<ArmyReceiverId, ArmyReceiverState>(owned.ToDictionary(entry => entry.ReceiverId));
        Entries = Array.AsReadOnly(owned);
    }

    public ArmyReceiverState? Find(ArmyReceiverId receiverId) => _byId.GetValueOrDefault(receiverId);
    public ArmyReceiverState? FindUnit(int unitId) => Entries.SingleOrDefault(entry =>
        entry.ActiveUnitId == unitId || entry.PendingImage?.Id == unitId);

    /// <summary>Unknown readiness blocks allocation at its position. Reserved,
    /// active and pending receivers remain unavailable until explicit cleanup.</summary>
    public ArmyReceiverId? FirstAssignable()
    {
        foreach (var entry in Entries.Where(entry => entry.CanAssign))
        {
            if (entry.Phase == ArmyReceiverPhase.Unknown) return null;
            if (entry.Phase == ArmyReceiverPhase.Ready) return entry.ReceiverId;
        }
        return null;
    }

    public ArmyReceiverRoster With(ArmyReceiverState entry)
    {
        if (!_byId.ContainsKey(entry.ReceiverId)) throw new InvalidDataException("Army receiver is outside this campaign roster.");
        return WithEntries([entry]);
    }

    /// <summary>Publishes receiver changes within the simulation owner. Existing
    /// pending aggregates are shared; each newly supplied image is detached once.
    /// GameStateCopy remains the boundary for a fully independent world snapshot.</summary>
    public ArmyReceiverRoster WithEntries(IReadOnlyList<ArmyReceiverState> entries)
    {
        var changes = entries.ToDictionary(entry => entry.ReceiverId);
        if (changes.Keys.Any(id => !_byId.ContainsKey(id)))
            throw new InvalidDataException("Army receiver change is outside this campaign roster.");
        return new(Entries.Select(before =>
        {
            var after = changes.GetValueOrDefault(before.ReceiverId) ?? before;
            return ReferenceEquals(before.PendingImage, after.PendingImage) ? after :
                after with { PendingImage = GameStateCopy.Copy(after.PendingImage) };
        }).ToArray(), ownsImages: true);
    }

    public ArmyReceiverRoster Retire(int unitId, UnitState image)
    {
        var entry = FindUnit(unitId);
        if (entry is null || image.Id != unitId)
            throw new InvalidDataException("Army retirement does not identify an active receiver.");
        return With(entry.Retire(image));
    }

    public ArmyReceiverRoster Copy() => new(Entries);
}
