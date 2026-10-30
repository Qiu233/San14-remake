using System.Collections.Immutable;

namespace San14.Core;

internal static class AutomaticEmperorCaptureValidation
{
    internal static void Validate(GameState state)
    {
        ValidateOuterOwners(state);
        int tails = (state.CityCaptureEmperorTail is null ? 0 : 1) +
            (state.BaseWinnerEmperorTail is null ? 0 : 1) + (state.ForceTransferEmperorTail is null ? 0 : 1);
        if (state.AutomaticEmperorCapture is not { } progress)
        {
            if (tails != 0) throw Invalid();
            return;
        }
        if (tails != 1 || progress.Context is not { } context || !Enum.IsDefined(progress.Phase) ||
            context.CityPhysicalSlot is < 1 or > 51 || context.CityLocationId is < 1 or > 51 ||
            context.NewForcePhysicalSlot is < 1 or > 51 || context.ExcludedForcePhysicalSlot is < 0 or > 51 ||
            context.EmperorPhysicalSlot is < 0 or > 6000 || context.EmperorId is < 0 or > 6000 ||
            context.RulerPhysicalSlot is < 0 or > 6000 || context.RulerId is < 0 or > 6000 ||
            context.KingdomName is < 0 or > 255 ||
            !Slots(progress.LoyaltyPeople, 6000) || !Slots(progress.UnfavorableForces, 51) ||
            !Slots(progress.FavorableForces, 51) ||
            progress.NeedsPresentationReceipt != (progress.AwaitingEffectBatchSequence is > 0) ||
            progress.AwaitingEffectBatchSequence is long sequence &&
                (state.CampaignEffectRequests is not { } queue || sequence > queue.LastBatchSequence))
            throw Invalid();
        if (progress.Phase == AutomaticEmperorCapturePhase.AwaitingChoice)
        {
            if (progress.SelectedMode is not null || progress.NeedsPresentationReceipt || !context.NewForceIsPlayer ||
                progress.PendingDecision is not { } decision || progress.ChoiceText is null ||
                decision.TextAtSource != progress.ChoiceText || decision.CityId != context.CityLocationId ||
                decision.CapturingForceId != context.NewForcePhysicalSlot || decision.EmperorId != context.EmperorId)
                throw Invalid();
        }
        else if (progress.SelectedMode is not (1 or 2) || progress.PendingDecision is not null) throw Invalid();
        if (state.CityCaptureEmperorTail is { } city)
        {
            if (city.Operation is not { } operation || operation.Request is not { } request ||
                request.Target is not { Kind: SettlementBaseKind.City } target || target.Id != context.CityLocationId ||
                operation.WinnerForceId != context.NewForcePhysicalSlot || operation.PreviousForceId is < 0 or > 51 ||
                operation.WinnerDistrictId is < 0 or > 51 || operation.CommanderOfficerId is < 0 or > 6000 ||
                operation.WinnerArmyPhysicalSlot is < 1 or > 500 || operation.WinnerReceiverId.Value <= 0 ||
                (operation.SampleDay is null) != (operation.SampleSalt is null) || city.SettlementIndex < -1 ||
                !Scope(city.CapturedOldDistrictPhysicalSlot, city.RelatedAreaPhysicalSlots, city.StatePhysicalSlots,
                    city.RegionPhysicalSlots, city.BeforeStateCount, city.BeforeRegionCount,
                    city.CapturedPersonPhysicalSlots, city.NoncapturedPersonPhysicalSlots,
                    city.SpecialPersonPhysicalSlots, city.ChangedForcePersonPhysicalSlots)) throw Invalid();
        }
        if (state.BaseWinnerEmperorTail is { } winner)
        {
            if (winner.Request is not { WinnerUnitId: null, WinnerDistrictId: not null } request ||
                request.Target is not { Kind: SettlementBaseKind.City } target || target.Id != context.CityLocationId ||
                winner.OldForcePhysicalSlot is < 0 or > 51 || winner.NewForcePhysicalSlot != context.NewForcePhysicalSlot ||
                winner.DestinationDistrictPhysicalSlot is < 0 or > 51 || winner.SettlementIndex < -1 ||
                (winner.SampleDay is null) != (winner.SampleSalt is null) ||
                !Scope(winner.CapturedOldDistrictPhysicalSlot, winner.RelatedAreaPhysicalSlots, winner.StatePhysicalSlots,
                    winner.RegionPhysicalSlots, winner.BeforeStateCount, winner.BeforeRegionCount,
                    winner.CapturedPersonPhysicalSlots, winner.NoncapturedPersonPhysicalSlots,
                    winner.SpecialPersonPhysicalSlots, winner.ChangedForcePersonPhysicalSlots)) throw Invalid();
        }
        if (state.ForceTransferEmperorTail is { } transfer &&
            (transfer.CityPhysicalSlot != context.CityPhysicalSlot || transfer.NewForcePhysicalSlot != context.NewForcePhysicalSlot ||
             transfer.OldForcePhysicalSlot is < 0 or > 51 || transfer.DestinationDistrictPhysicalSlot is < 0 or > 51 ||
             transfer.CapturedOldDistrictPhysicalSlot is < 0 or > 51 || !Slots(transfer.RelatedAreaPhysicalSlots, 500))) throw Invalid();
    }

    private static void ValidateOuterOwners(GameState state)
    {
        if (state.StoryForceTransferContinuation is { } story &&
            (story.Code is not (149 or 212) || story.TargetCityPhysicalSlot is < 0 or > 51 ||
             story.PreviousForcePhysicalSlot is < 0 or > 51 || story.ExecutorPhysicalSlot is < 0 or > 6000 ||
             story.DestinationDistrictPhysicalSlot is < 0 or > 51 || !Slots(story.CityPhysicalSlots, 51) ||
             story.CityIndex < 0)) throw Invalid();
        if (state.ForceAbsorptionContinuation is { } absorption &&
            (absorption.OriginalForcePhysicalSlot is < 0 or > 51 || absorption.RecipientForcePhysicalSlot is < 0 or > 51 ||
             absorption.PrimaryDistrictPhysicalSlot is < 0 or > 51 || absorption.RecipientRulerId is < 0 or > 6000 ||
             !Slots(absorption.DistrictPhysicalSlots, 51) || !Bases(absorption.ProcessedBases) ||
             !Bases(absorption.RemainingBases) || absorption.BaseIndex < 0)) throw Invalid();
        if (state.DemandSurrenderAbsorptionTail is { } demand &&
            (!Task(demand.Task) || demand.ChronicleSlot is < 0)) throw Invalid();
        if (state.DemandSurrenderCompletionTail is { } completion &&
            (!Task(completion.Task) || completion.Report is not { } report || report.TargetName is null ||
             report.Actor is < 0 or > 6000 || report.OwnRuler is < 0 or > 6000 || report.TargetRuler is < 0 or > 6000 ||
             report.TargetSpeaker is < 0 or > 6000 || report.ActorDistrict is < 0 or > 51 ||
             report.TargetDistrict is < 0 or > 51 || report.TargetHex < 0)) throw Invalid();
        if (state.PlayerCoalitionAbsorptionTail is { } player &&
            (player.PlayerForcePhysicalSlot is < 0 or > 51 || player.HeadForcePhysicalSlot is < 0 or > 51 ||
             !Slots(player.RemainingMemberForcePhysicalSlots, 51))) throw Invalid();
        if (state.NpcForceAbsorptionTail is { } npc &&
            (npc.PlayerForcePhysicalSlot is < 0 or > 51 || npc.SurvivorForcePhysicalSlot is < 0 or > 51 || npc.CameraHex < 0)) throw Invalid();
        if (state.StoryForceAbsorptionTail is { } absorbed &&
            (absorbed.OriginalForcePhysicalSlot is < 0 or > 51 || absorbed.RecipientForcePhysicalSlot is < 0 or > 51)) throw Invalid();
        if (state.StrategicPoisonEmperorContinuation is { } poison &&
            (poison.TaskId < 0 || poison.TargetCityPhysicalSlot is < 1 or > 51 || poison.ExecutorPhysicalSlot is < 0 or > 6000 ||
             poison.OwnForcePhysicalSlot is < 0 or > 51 || poison.PreviousForcePhysicalSlot is < 0 or > 51 || poison.ResultCode != 1)) throw Invalid();
        if (state.EditedEventForceAbsorptionTail is { } edited &&
            (edited.OriginalForcePhysicalSlot is < 0 or > 51 || edited.RecipientForcePhysicalSlot is < 0 or > 51)) throw Invalid();
        if (state.EditedEventResultCursor is { } cursor)
        {
            if (state.EditedEventPlayback is not { Phase: EditedEventPlaybackPhase.Results } || cursor.Frames.IsDefault ||
                cursor.SuccessCount < 0 || cursor.EvaluatedCount < cursor.SuccessCount ||
                cursor.Frames.Any(frame => frame is null || frame.Begin < 0 || frame.End < frame.Begin ||
                    frame.Cursor < frame.Begin || frame.Depth < 0 || frame.OrDepth < 0 || frame.PendingGroup < -1 ||
                    frame.IfPhase is < 0 or > 2 || frame.ThenMarker < -1 || frame.ElseMarker < -1)) throw Invalid();
        }
        else if (state.EditedEventForceAbsorptionTail is not null) throw Invalid();
    }

    private static bool Task(PersonnelDiplomacyTask task) => task is not null && task.MemberIds is not null &&
        task.Id >= 0 && task.DispatchSlot >= 0 && task.MemberIds.All(id => id is >= 0 and <= 6000) &&
        task.ForceId is >= 0 and <= 51 && task.TargetForceId is >= 0 and <= 51;
    private static bool Bases(ImmutableArray<SettlementBase> values) => !values.IsDefault &&
        values.All(value => value is not null && Enum.IsDefined(value.Kind) && value.Id is >= 1 and <= 61 &&
            (value.Kind == SettlementBaseKind.City ? value.Id <= 51 : value.Id >= 52));

    private static bool Scope(int district, ImmutableArray<int> areas, ImmutableArray<int> states,
        ImmutableArray<int> regions, int beforeStates, int beforeRegions, ImmutableArray<int> captured,
        ImmutableArray<int> noncaptured, ImmutableArray<int> special, ImmutableArray<int> changed) =>
        district is >= 0 and <= 51 && beforeStates >= 0 && beforeRegions >= 0 &&
        Slots(areas, 500) && Slots(states, 255) && Slots(regions, 255) && Slots(captured, 6000) &&
        Slots(noncaptured, 6000) && Slots(special, 6000) && Slots(changed, 6000);
    private static bool Slots(ImmutableArray<int> values, int maximum) =>
        !values.IsDefault && values.All(slot => slot >= 0 && slot <= maximum);
    private static InvalidDataException Invalid() => new("The emperor capture continuation is incomplete or has no matching capture tail.");
}
