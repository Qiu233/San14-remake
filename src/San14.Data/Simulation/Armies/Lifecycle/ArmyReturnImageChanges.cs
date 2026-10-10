using San14.Core;

namespace San14.Data;

/// <summary>Actual writes made by one army lifecycle operation. Hydrated source
/// fields are inputs, not writes. The captured receiver and logical image identify
/// the aggregate even after that image enters pending retirement.</summary>
internal sealed class ArmyReturnImageChanges
{
    internal sealed record ImageOwner(ArmyReceiverId ReceiverId, int? UnitId);
    internal sealed record Change(ImageOwner Owner)
    {
        internal ArmyTraitRoster? Traits { get; init; }
        internal ArmyReturnTraitFields? TraitSource { get; init; }
        internal bool WritesWounded { get; init; }
        internal int? Wounded { get; init; }
        internal bool WritesMorale { get; init; }
        internal int? Morale { get; init; }
    }

    private readonly Dictionary<int, ImageOwner> _owners;
    private readonly Dictionary<int, Change> _writes;
    internal IReadOnlyDictionary<int, Change> Writes => _writes;

    internal ArmyReturnImageChanges(ArmyLifecycleSourceView lifecycle)
    {
        _owners = lifecycle.Slots.ToDictionary(slot => slot.PhysicalSlot, slot =>
        {
            var receiver = lifecycle.Receivers.Find(slot.ReceiverId)!;
            return new ImageOwner(slot.ReceiverId, receiver.ActiveUnitId ?? receiver.PendingImage?.Id);
        });
        _writes = [];
    }
    private ArmyReturnImageChanges(Dictionary<int, ImageOwner> owners, Dictionary<int, Change> writes)
    { _owners = owners; _writes = writes; }
    internal ArmyReturnImageChanges Copy() => new(_owners, new(_writes));
    internal ImageOwner Owner(int physical) => _owners[physical];
    private Change Current(int physical) => _writes.GetValueOrDefault(physical) ?? new(_owners[physical]);

    internal void WriteTraits(int physical, ArmyReturnTraitFields fields)
    {
        var before = Current(physical);
        _writes[physical] = before with { Traits = ArmyTraitSourceAdapter.Decode(fields.Traits64), TraitSource = fields };
    }
    internal void WriteWounded(int physical, ushort? value)
        => _writes[physical] = Current(physical) with { WritesWounded = true, Wounded = value };
    internal void WriteMorale(int physical, byte? value)
        => _writes[physical] = Current(physical) with { WritesMorale = true, Morale = value };
    internal void AcknowledgeNumerics(int physical, bool wounded, bool morale)
    {
        if (!_writes.TryGetValue(physical, out var before)) return;
        var after = before with { WritesWounded = before.WritesWounded && !wounded, WritesMorale = before.WritesMorale && !morale };
        if (after.TraitSource is null && !after.WritesWounded && !after.WritesMorale) _writes.Remove(physical);
        else _writes[physical] = after;
    }
    internal void Clear() => _writes.Clear();
}

public sealed partial class SourceWorldContext
{
    private ArmyReturnImageChanges? _armyReturnImageChanges;
    internal ArmyReturnImageChanges ArmyImageChanges => _armyReturnImageChanges
        ?? throw new InvalidDataException("Army image publication has no lifecycle operation owner.");
    internal void BindArmyReturnImages(ArmyLifecycleSourceView lifecycle)
        => _armyReturnImageChanges = new(lifecycle);
    internal void RequireArmyReturnImage(int physical, ArmyReceiverState receiver)
    {
        if (_armyReturnImageChanges is not { } changes) return;
        var owner = changes.Owner(physical);
        if (receiver.ReceiverId != owner.ReceiverId || (receiver.ActiveUnitId ?? receiver.PendingImage?.Id) != owner.UnitId)
            throw new InvalidDataException("Army numeric write no longer identifies its captured image.");
    }
    internal void AcknowledgeArmyReturnNumerics(int physical, bool wounded = false, bool morale = false)
        => _armyReturnImageChanges?.AcknowledgeNumerics(physical, wounded, morale);
}
