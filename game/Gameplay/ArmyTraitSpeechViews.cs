using System.Collections.Immutable;
using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>Actual source view knowledge, captured on the engine thread.
/// A worker can read this immutable directory without touching scene nodes.</summary>
internal sealed class ArmyTraitSpeechViews
{
    private sealed record View(ArmyReceiverId Receiver, ArmyReceiverPhase Phase,
        int? ActiveUnitId, int? PendingUnitId, bool? Available);
    private readonly ImmutableDictionary<int, View> views;
    internal bool? ControllerAvailable { get; }
    private ArmyTraitSpeechViews(ImmutableDictionary<int, View> views, bool controllerAvailable)
        => (this.views, ControllerAvailable) = (views, controllerAvailable);

    internal static ArmyTraitSpeechViews WithoutController { get; } =
        new(ImmutableDictionary<int, View>.Empty, false);

    internal static ArmyTraitSpeechViews? Capture(GameState source, Func<int, bool?> available)
    {
        if (source.ArmyLifecycle is not { } lifecycle) return null;
        var views = ImmutableDictionary.CreateBuilder<int, View>();
        foreach (var receiver in lifecycle.Receivers.Entries)
        {
            int? id = receiver.ActiveUnitId ?? receiver.PendingImage?.Id;
            if (id is int unitId) views.Add(unitId, new(receiver.ReceiverId, receiver.Phase,
                receiver.ActiveUnitId, receiver.PendingImage?.Id, available(unitId)));
        }
        // This capture is called by the actual constructed Army label
        // controller; an absent individual card does not remove that owner.
        return new(views.ToImmutable(), true);
    }

    internal bool? Read(GameState candidate, int unitId)
    {
        if (ControllerAvailable == false) return false;
        if (!views.TryGetValue(unitId, out var captured) || candidate.ArmyLifecycle?.Receivers.Find(captured.Receiver) is not { } actual)
            return null;
        //A new assignment or retirement after capture is not evidence about
        //the new view. Preserve unknown until a real publication captures it.
        return actual.Phase == captured.Phase && actual.ActiveUnitId == captured.ActiveUnitId &&
            actual.PendingImage?.Id == captured.PendingUnitId ? captured.Available : null;
    }
}
