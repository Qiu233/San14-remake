using System.Collections;
using San14.Core;

namespace San14.Data;

/// <summary>Operation-local join of source fields and campaign ownership.
/// It is never saved: publication writes the two owners separately.</summary>
public sealed record ArmyLifecycleReceiverView(ArmyReceiverSourceSlot Source, ArmyReceiverState Receiver)
{
    public int PhysicalSlot => Source.PhysicalSlot;
    public ArmyReceiverId ReceiverId => Receiver.ReceiverId;
    public int? ActiveUnitId { get => Receiver.ActiveUnitId; init => Receiver = Receiver with { ActiveUnitId = value }; }
    public UnitState? PendingImage { get => Receiver.PendingImage; init => Receiver = Receiver with { PendingImage = value }; }
    public ushort? Commander12 { get => Source.Commander12; init => Source = Source with { Commander12 = value }; }
    public byte? Status10 { get => Source.Status10; init => Source = Source with { Status10 = value }; }
    public ushort? Priority14 { get => Source.Priority14; init => Source = Source with { Priority14 = value }; }
    public ArmyStoreKnowledge StoredResources { get => Receiver.StoredResources; init => Receiver = Receiver with { StoredResources = value }; }
    public IReadOnlyList<ushort>? AuxiliaryAC { get => Source.AuxiliaryAC; init => Source = Source with { AuxiliaryAC = value }; }
    public IReadOnlyList<ushort>? AuxiliaryF4 { get => Source.AuxiliaryF4; init => Source = Source with { AuxiliaryF4 = value }; }
    public ArmyOverrideTargetState? OverrideTarget { get => Receiver.OverrideTarget; init => Receiver = Receiver with { OverrideTarget = value }; }
    public byte? Direction36 { get => Source.Direction36; init => Source = Source with { Direction36 = value }; }
    public byte? MapUiByte13C { get => Source.MapUiByte13C; init => Source = Source with { MapUiByte13C = value }; }
    public ArmyMarchDestination? FalseReportDestination { get => Receiver.FalseReportDestination; init => Receiver = Receiver with { FalseReportDestination = value }; }
    public FacilityConstructionIntent? FacilityConstruction { get => Receiver.FacilityConstruction; init => Receiver = Receiver with { FacilityConstruction = value }; }
    public ArmySavedCommandState? SavedCommand { get => Receiver.SavedCommand; init => Receiver = Receiver with { SavedCommand = value }; }
    public byte? CommandControl140
    {
        get => Receiver.SavedCommand?.Control ?? Source.CommandControl140;
        init
        {
            //The only normal scalar writer is an explicit reset. Positive
            //values require the complete receiver-owned command first.
            if (value == 0) Receiver = Receiver with { SavedCommand = ArmySavedCommandState.Cleared };
            else if (Receiver.SavedCommand is { } saved && value != saved.Control)
                throw new InvalidDataException("Army control projection cannot replace its complete saved command.");
            Source = Source with { CommandControl140 = value };
        }
    }
    public byte? RouteHold56 { get => Source.RouteHold56; init => Source = Source with { RouteHold56 = value }; }
    public ushort? RouteGoal1F8 { get => Source.RouteGoal1F8; init => Source = Source with { RouteGoal1F8 = value }; }
    public ushort? AiActionFlags { get => Source.AiActionFlags; init => Source = Source with { AiActionFlags = value }; }
    public uint? AiEvaluation { get => Source.AiEvaluation; init => Source = Source with { AiEvaluation = value }; }
    public bool? GroupAssignmentReady { get => Receiver.GroupAssignmentReady; init => Receiver = Receiver with { GroupAssignmentReady = value }; }
    public int? OccupancyBindingId { get => Receiver.OccupancyBindingId; init => Receiver = Receiver with { OccupancyBindingId = value }; }
}

public sealed class ArmyLifecycleReceiverViews(ArmyLifecycleSourceView lifecycle) : IReadOnlyList<ArmyLifecycleReceiverView>
{
    public int Count => lifecycle.Slots.Count;
    public int Length => Count;
    public ArmyLifecycleReceiverView this[int index]
    {
        get
        {
            var source = lifecycle.Slots[index];
            return new(source, lifecycle.Receivers.Find(source.ReceiverId)
                ?? throw new InvalidDataException("Army source receiver has no campaign roster entry."));
        }
    }
    public IEnumerator<ArmyLifecycleReceiverView> GetEnumerator()
    {
        for (int index = 0; index < Count; index++) yield return this[index];
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Source-operation publication boundary. Receiver phase is decoded
/// here; Core operations never interpret a source header or physical ordinal.</summary>
public static class ArmyLifecycleReceiverAdapter
{
    public static ArmyLifecycleReceiverViews ReceiverSlots(this ArmyLifecycleSourceView lifecycle) => new(lifecycle);

    public static ArmyLifecycleSourceView WithReceiverSlots(this ArmyLifecycleSourceView lifecycle,
        IReadOnlyList<ArmyLifecycleReceiverView> slots)
    {
        var changes = slots.ToDictionary(slot => slot.ReceiverId, slot => Transition(
            lifecycle.Receivers.Find(slot.ReceiverId)!, slot));
        return lifecycle with
        {
            Source = lifecycle.Source with { Slots = slots.Select(slot => slot.Source).ToArray() },
            Receivers = lifecycle.Receivers.WithEntries(lifecycle.Receivers.Entries.Select(entry => changes[entry.ReceiverId]).ToArray())
        };
    }

    private static ArmyReceiverState Transition(ArmyReceiverState before, ArmyLifecycleReceiverView after)
    {
        if (after.ActiveUnitId is int active)
            return after.Receiver with { Phase = (before.ActiveUnitId == active ? before : before.Assign(active)).Phase };
        if (after.PendingImage is { } pending)
            return after.Receiver with { Phase = (before.PendingImage?.Id == pending.Id ? before : before.Retire(pending)).Phase };
        ArmyReceiverPhase importedPhase = DecodeEmptyPhase(after.Commander12, after.Status10);
        var phase = before.Phase == ArmyReceiverPhase.Pending && importedPhase == ArmyReceiverPhase.Ready
            ? before.Release().Phase : importedPhase;
        return after.Receiver with { Phase = phase, ActiveUnitId = null, PendingImage = null };
    }

    public static ArmyReceiverPhase DecodeEmptyPhase(ushort? commander, byte? status) =>
        commander is null || commander != 0 && status is null ? ArmyReceiverPhase.Unknown :
        commander == 0 || status == 0 ? ArmyReceiverPhase.Ready : ArmyReceiverPhase.Reserved;
}
