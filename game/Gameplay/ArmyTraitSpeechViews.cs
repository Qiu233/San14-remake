using System.Collections.Immutable;
using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>Actual source view knowledge captured on the engine thread.
/// The worker reads immutable facts; only a stopped-worker presentation borrow
/// can publish a newly initialized view without touching worker scene nodes.</summary>
internal sealed class ArmyTraitSpeechViews
{
    internal sealed record View(int UnitId, ArmyReceiverId Receiver, ArmyReceiverPhase Phase,
        int? ActiveUnitId, int? PendingUnitId, bool? Available);
    private ImmutableDictionary<int, View> views;
    private readonly ImmutableDictionary<int, View> capturedViews;
    internal bool? ControllerAvailable { get; }
    private ArmyTraitSpeechViews(ImmutableDictionary<int, View> views, bool controllerAvailable)
        => (this.views, capturedViews, ControllerAvailable) = (views, views, controllerAvailable);

    //A failed/cancelled calculation never adopts its initialized view owners.
    //Restore the original immutable root for the still-visible source rules.
    internal void DiscardInitializedViews() => Volatile.Write(ref views, capturedViews);

    internal static ArmyTraitSpeechViews WithoutController { get; } =
        new(ImmutableDictionary<int, View>.Empty, false);

    internal static ArmyTraitSpeechViews? Capture(GameState source, Func<int, bool?> available)
    {
        if (source.ArmyLifecycle is not { } lifecycle) return null;
        var views = ImmutableDictionary.CreateBuilder<int, View>();
        foreach (var receiver in lifecycle.Receivers.Entries)
        {
            int? id = receiver.ActiveUnitId ?? receiver.PendingImage?.Id;
            if (id is int unitId) views.Add(unitId, CaptureView(receiver, unitId, available(unitId)));
        }
        // This capture is called by the actual constructed Army label
        // controller; an absent individual card does not remove that owner.
        return new(views.ToImmutable(), true);
    }

    internal static View? CaptureView(GameState source, int unitId, bool? available)
        => source.ArmyLifecycle?.Receivers.FindUnit(unitId) is { } receiver
            ? CaptureView(receiver, unitId, available) : null;

    private static View CaptureView(ArmyReceiverState receiver, int unitId, bool? available)
        => new(unitId, receiver.ReceiverId, receiver.Phase,
            receiver.ActiveUnitId, receiver.PendingImage?.Id, available);

    //The UI is the sole writer, while Checkpoint owns the stopped worker.
    //SetItem replaces one immutable branch, never copies or retains a World.
    internal bool PublishInitializedView(GameState candidate, View view)
    {
        if (ControllerAvailable != true || !Matches(candidate, view)) return false;
        Volatile.Write(ref views, Volatile.Read(ref views).SetItem(view.UnitId, view));
        return true;
    }

    internal bool? Read(GameState candidate, int unitId)
    {
        if (ControllerAvailable == false) return false;
        var capturedViews = Volatile.Read(ref views);
        if (!capturedViews.TryGetValue(unitId, out var captured) || !Matches(candidate, captured)) return null;
        return captured.Available;
    }

    private static bool Matches(GameState candidate, View captured)
        => candidate.ArmyLifecycle?.Receivers.Find(captured.Receiver) is { } actual &&
            actual.Phase == captured.Phase && actual.ActiveUnitId == captured.ActiveUnitId &&
            actual.PendingImage?.Id == captured.PendingUnitId;
}
