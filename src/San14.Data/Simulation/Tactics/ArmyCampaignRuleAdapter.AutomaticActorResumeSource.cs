using San14.Core;

namespace San14.Data;

internal sealed record AutomaticActorResumeCurrent(ArmyUnitReference Army, ArmyReceiverId Receiver,
    UnitState? LatestImage, bool NativeValid, bool IsActiveProjection);

public sealed partial class ArmyCampaignRuleAdapter
{
    //178990 owns its original physical pointer across an internal Full call.
    //An image leaving Units is not a32FB90 false result: pending70 retains it
    //until the distinct282580 reset/unregister phase.
    internal static AutomaticActorResumeCurrent ReadAutomaticActorResumeCurrent(GameState state,
        SourceWorldContext current, int physical)
    {
        if (physical is < 1 or > 500) throw ActorResumeMissing("physical_slot");
        var arena = state.ReadArmyLifecycle() ?? throw ActorResumeMissing("lifecycle");
        var source = arena.Slots[physical];
        var receiver = arena.Receivers.Find(source.ReceiverId) ?? throw ActorResumeMissing("receiver");
        //Import only unknown headers; current operation writes remain authoritative.
        current.BindUnspecifiedOwnedArmyQueryHeader(new(source, receiver));
        var army = current.ReadArmyUnitTableReference(physical);
        if (army is null)
        {
            if (source.PhysicalSlot != physical) throw ActorResumeMissing("source_pointer");
            current.SetArmyUnitTablePointer(physical, physical);
            army = new(physical);
        }
        bool valid = current.ReadArmyUnitNativeValidity(army) ?? throw ActorResumeMissing("native_validity");
        UnitState? image = receiver.ActiveUnitId is int id
            ? state.Units.GetValueOrDefault(id) ?? throw ActorResumeMissing("active_image") : receiver.PendingImage;
        if (image is not null && image.NativeArmyOrigin?.NativeSlotIndex != physical)
            throw ActorResumeMissing("image_physical_identity");
        if (valid && image is null) throw ActorResumeMissing("valid_receiver_image");
        return new(army, receiver.ReceiverId, image, valid, receiver.ActiveUnitId is not null);
    }

    internal static UnitState RequireLatestAutomaticActorImageCurrent(GameState state,
        SourceWorldContext current, int unitId)
    {
        var active = state.Units.GetValueOrDefault(unitId);
        int physical;
        if (active?.NativeArmyOrigin?.NativeSlotIndex is int slot) physical = slot;
        else
        {
            var arena = state.ReadArmyLifecycle() ?? throw ActorResumeMissing("lifecycle");
            var receiver = arena.Receivers.FindUnit(unitId) ?? throw ActorResumeMissing("captured_Army_receiver");
            physical = receiver.PendingImage?.NativeArmyOrigin?.NativeSlotIndex
                ?? throw ActorResumeMissing("captured_Army_physical");
        }
        var reached = ReadAutomaticActorResumeCurrent(state, current, physical);
        return reached.LatestImage is { } latest && latest.Id == unitId ? latest
            : throw ActorResumeMissing("latest_image");
    }

    //Only a real writer calls this. Pending remains pending, even when a later
    //instruction or waiting tail updates its owned image.
    internal static GameState PublishAutomaticActorResumeImageCurrent(GameState state,
        SourceWorldContext current, UnitState before, UnitState after)
    {
        int physical = before.NativeArmyOrigin?.NativeSlotIndex ?? throw ActorResumeMissing("publish_physical");
        var reached = ReadAutomaticActorResumeCurrent(state, current, physical);
        if (reached.LatestImage != before || after.Id != before.Id ||
            after.NativeArmyOrigin?.NativeSlotIndex != physical)
            throw ActorResumeMissing("publish_current_image");
        if (reached.IsActiveProjection)
            return state with { Units = new(state.Units) { [after.Id] = after } };
        var arena = state.ReadArmyLifecycle()!;
        var receiver = arena.Receivers.Find(reached.Receiver)!;
        return state.WithArmyLifecycle(arena with
            { Receivers = arena.Receivers.With(receiver with { PendingImage = after }) });
    }

    private static InvalidDataException ActorResumeMissing(string owner) => new("automatic_actor_resume." + owner);
}

public sealed partial class TacticRemakeCurrentContext
{
    private readonly HashSet<int> _automaticActorResumeSlots = [];

    //Opt in only the current execution's actual source receivers. General
    //tactic availability and unrelated callers keep their active-world gate.
    internal void BindAutomaticActorResumeCurrent(GameState state, SourceWorldContext current, int physical)
    {
        if (!ReferenceEquals(current, _definitions) || !Bound(state))
            throw new InvalidDataException("automatic_actor_resume.bound_current");
        _ = ArmyCampaignRuleAdapter.ReadAutomaticActorResumeCurrent(state, current, physical);
        _automaticActorResumeSlots.Add(physical);
    }
}
