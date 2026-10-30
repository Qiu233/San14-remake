using San14.Core;

namespace San14.Data;

public sealed partial class OwnedCityCaptureStage
{
    private OwnedCityCaptureResult ResumeEmperorTail(GameState input, BaseCaptureRequest request,
        NativeMapSeasonDate? operationDate, TacticSourceSamplerWorld? sampler,
        SourceWorldContext? borrowedCurrent, ArmyMapGroupingOwner? groups)
    {
        var saved = input.CityCaptureEmperorTail ?? throw Missing("Emperor_tail");
        var operation = saved.Operation;
        if (operation.Request != request) throw Missing("Emperor_request");
        var current = borrowedCurrent ?? package.CaptureOwnedArmyReturnWorld(input).Context;
        var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("Emperor_pool");
        var working = package.ResumeCaptureEmperorServiceCurrent(input, current, pool);
        if (working.AutomaticEmperorCapture is not { Phase: AutomaticEmperorCapturePhase.Completed, NeedsPresentationReceipt: false })
            return new(working, null, null);
        var target = new BaseReference(BaseKind.City, request.Target.Id);
        var winner = new ArmyUnitReference(operation.WinnerArmyPhysicalSlot);
        var force = new ForceReference(operation.WinnerForceId);
        var district = new DistrictReference(operation.WinnerDistrictId);
        int slot = operation.WinnerArmyPhysicalSlot;
        var arena = working.ReadArmyLifecycle() ?? throw Missing("Emperor_winner_arena");
        if (arena.Slots[slot].ReceiverId != operation.WinnerReceiverId ||
            !K(current.ReadArmyUnitNativeValidity(winner), "Emperor_winner_validity")) throw Missing("Emperor_winner_receiver");
        if (operation is { SampleDay: int day, SampleSalt: int salt }) sampler = new(day, salt);
        var areas = CaptureEmperorTailLists.RestoreObjects(current, saved.RelatedAreaPhysicalSlots, RegistryKind.Area);
        var scope = new CityCaptureAreaScope(target, operation.PreviousForceId, operation.WinnerForceId,
            new(saved.CapturedOldDistrictPhysicalSlot), areas);
        BaseCaptureTerritoryReceipt? counts = null;
        BaseCapturePersonPrefixReceipt? people = null;
        var previousTransfer = current.TransferModeStateReader;
        try
        {
            var states = CaptureEmperorTailLists.RestoreStatic(current, saved.StatePhysicalSlots, "State");
            try
            {
                var regions = CaptureEmperorTailLists.RestoreStatic(current, saved.RegionPhysicalSlots, "Region");
                counts = new(states, regions, saved.BeforeStateCount, saved.BeforeRegionCount);
            }
            catch { current.ClearTemporaryHexList(states); current.ReleaseTemporaryHexList(states); throw; }
            people = CaptureEmperorTailLists.RestorePeopleReceipt(current, request.Target.Id,
                operation.PreviousForceId, operation.WinnerForceId, saved.CapturedPersonPhysicalSlots,
                saved.NoncapturedPersonPhysicalSlots, saved.SpecialPersonPhysicalSlots, saved.ChangedForcePersonPhysicalSlots);
            current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
            CityCaptureTerritorySource.ClearTargetRouting(current, target);
            var aftermathEffects = new List<CampaignEffectRequest>();
            working = package.ApplyCityCaptureStateAdvantageCurrent(working, current, scope, operationDate, aftermathEffects);
            _ = CityCaptureTerritorySource.ApplyCityFoodWarning(working, current, target,
                () =>
                {
                    var quote = package.ReadCityCaptureFoodQuoteCurrent(working, current, target, groups);
                    int expense = Food(quote); working = quote.State ?? working; return expense;
                },
                () => Food(CityCaptureFoodWarningSource.TryReadPredictedFoodIncome(working, current, target,
                    operationDate, package.CreateArmyDestructionAbilities(current), package.FoodIncomeSceneries)));
            if (aftermathEffects.Count != 0)
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw Missing("Emperor_aftermath_effects")).Append(CampaignEffectSource.ArmyBattleDefeat,
                        working.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("Emperor_aftermath_revision"), aftermathEffects) };
            CityCaptureTerritorySource.ReleaseRelatedAreas(current, scope); scope = null!;
            current.TransferModeStateReader = previousTransfer;
            working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            var returnWorld = OwnedArmyReturnWorld.Borrow(working, current, pool);
            var returned = package.PrepareOwnedArmyReturnCurrent(working, winner, target, 0,
                returnWorld.Context, returnWorld.Pool, package.CreateCaptureGovernorQueries(working), new(false, false),
                operationDate: operationDate, sampler: sampler);
            working = returnWorld.CommitPrepared(working, returned.Packet ?? throw Missing(returned.UnresolvedOwner ?? "Emperor_winner_return"))
                ?? throw Missing("Emperor_return_publication");
            current = returnWorld.Context;
            var rewardInput = counts; counts = null;
            var reward = BaseCaptureRewardSource.ApplyTailWithinCapture(working, current, target, force, district, winner, rewardInput);
            working = reward.State ?? throw Missing(reward.UnresolvedOwner ?? "Emperor_reward_tail");
            working = BaseCapturePersonPrefix.ReleaseWithinCapture(working, current, people); people = null;
            working = working with { CityCaptureEmperorTail = null, AutomaticEmperorCapture = null };
            working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            return new(working, new(request.Target.Id, operation.PreviousForceId, operation.WinnerForceId,
                request.WinnerUnitId ?? throw Missing("Emperor_winner_unit"), operation.CommanderOfficerId,
                null, null, null, reward.Receipt, ["2F6890.actual_stages_and_original_capture_tail"])
                { ReturnedTroops = operation.ReturnedTroops }, null);
        }
        finally
        {
            current.TransferModeStateReader = previousTransfer;
            if (scope is not null) CityCaptureTerritorySource.ReleaseRelatedAreas(current, scope);
            if (counts is not null) BaseCaptureRewardSource.ReleaseTerritoryRewards(current, counts);
            if (people is not null) working = BaseCapturePersonPrefix.ReleaseWithinCapture(working, current, people);
        }
    }
}
