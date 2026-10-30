using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static BaseWinnerEmperorTail CaptureBaseWinnerEmperorTail(SourceWorldContext current,
        BaseCaptureRequest request, NativeMapSeasonDate? date, TacticSourceSamplerWorld? sampler,
        CityCaptureAreaScope scope, BaseCaptureTerritoryReceipt counts,
        BaseCapturePersonPrefixReceipt people, int destinationDistrict)
        => new(request,
            scope.CapturedOldDistrict.PhysicalSlot ?? throw BaseWinnerMissing("Emperor_old_District"),
            scope.OldForcePhysical, scope.NewForcePhysical, destinationDistrict, date,
            sampler?.Day450, sampler?.Salt45C,
            CaptureEmperorTailLists.ReadObjects(current, scope.Areas, RegistryKind.Area),
            CaptureEmperorTailLists.ReadStatic(current, counts.States, "State"),
            CaptureEmperorTailLists.ReadStatic(current, counts.Regions, "Region"),
            counts.StateCount, counts.RegionCount,
            CaptureEmperorTailLists.ReadObjects(current, people.CapturedTemporary, RegistryKind.Person),
            CaptureEmperorTailLists.ReadObjects(current, people.NoncapturedTemporary, RegistryKind.Person),
            CaptureEmperorTailLists.ReadObjects(current, people.SpecialTemporary, RegistryKind.Person),
            CaptureEmperorTailLists.ReadObjects(current, people.ChangedForceTemporary, RegistryKind.Person));

    /// <summary>Continue2851B0's genuine Base-source winner after its already
    /// completed transfer/threat prefix. The saved State/Region counts and
    /// ordered payloads survive the real decision and presentation waits.</summary>
    internal GameState ResumeBaseWinnerEmperorCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, ArmyMapGroupingOwner? groups = null)
    {
        var tail = state.BaseWinnerEmperorTail ?? throw BaseWinnerMissing("Emperor_resume_tail");
        if (tail.Request.WinnerUnitId is not null || tail.Request.WinnerDistrictId is null)
            throw BaseWinnerMissing("Emperor_resume_Base_winner");
        var working = ResumeCaptureEmperorServiceCurrent(state, current, pool);
        if (working.AutomaticEmperorCapture is not
            { Phase: AutomaticEmperorCapturePhase.Completed, NeedsPresentationReceipt: false, PendingDecision: null })
            return working;

        var target = ArmyDestructionQueue.ResolveBaseReference(working, current, tail.Request.Target);
        var force = new ForceReference(tail.NewForcePhysicalSlot);
        var district = new DistrictReference(tail.DestinationDistrictPhysicalSlot);
        var noArmy = new ArmyUnitReference(null);
        CityCaptureAreaScope? areas = null;
        HexListHandle? states = null, regions = null;
        BaseCaptureTerritoryReceipt? counts = null;
        BaseCapturePersonPrefixReceipt? people = null;
        var previousTransfer = current.TransferModeStateReader;
        try
        {
            //Recreate only transient nodes from saved physical payloads. Never
            //rescan ownership, rerun Person prefix or recompute before-counts.
            states = CaptureEmperorTailLists.RestoreStatic(current, tail.StatePhysicalSlots, "State");
            regions = CaptureEmperorTailLists.RestoreStatic(current, tail.RegionPhysicalSlots, "Region");
            counts = new(states, regions, tail.BeforeStateCount, tail.BeforeRegionCount);
            states = regions = null;
            areas = new(target, tail.OldForcePhysicalSlot, tail.NewForcePhysicalSlot,
                new(tail.CapturedOldDistrictPhysicalSlot),
                CaptureEmperorTailLists.RestoreObjects(current, tail.RelatedAreaPhysicalSlots, RegistryKind.Area));
            int location = current.ReadBaseObjectLink(target) ?? throw BaseWinnerMissing("Emperor_resume_Base4E");
            people = CaptureEmperorTailLists.RestorePeopleReceipt(current, location,
                tail.OldForcePhysicalSlot, tail.NewForcePhysicalSlot,
                tail.CapturedPersonPhysicalSlots, tail.NoncapturedPersonPhysicalSlots,
                tail.SpecialPersonPhysicalSlots, tail.ChangedForcePersonPhysicalSlots);
            current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
            CityCaptureTerritorySource.ClearTargetRouting(current, target);
            var advantageEffects = new List<CampaignEffectRequest>();
            working = ApplyCityCaptureStateAdvantageCurrent(working, current, areas, tail.OperationDate, advantageEffects);
            CityCaptureTerritorySource.ApplyCityFoodWarning(working, current, target,
                () => Quote(ReadCityCaptureFoodQuoteCurrent(working, current, target,
                    groups ?? CreateCaptureMapGroupingOwner())),
                () => Quote(CityCaptureFoodWarningSource.TryReadPredictedFoodIncome(working, current, target,
                    tail.OperationDate, CreateArmyDestructionAbilities(current), FoodIncomeSceneries)));
            CityCaptureTerritorySource.ReleaseRelatedAreas(current, areas);
            areas = null;
            current.TransferModeStateReader = previousTransfer;
            working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);

            if (advantageEffects.Count != 0)
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw BaseWinnerMissing("Emperor_resume_advantage_effect_queue"))
                    .Append(CampaignEffectSource.ArmyBattleDefeat,
                        working.NativeMapSeason?.AdvancedCalendarSteps
                            ?? throw BaseWinnerMissing("Emperor_resume_advantage_effect_calendar"), advantageEffects) };

            var ownedCounts = counts;
            counts = null; //ApplyTail owns both releases, including its failure branch.
            var reward = BaseCaptureRewardSource.ApplyTailWithinCapture(working, current,
                target, force, district, noArmy, ownedCounts);
            working = reward.State ?? throw BaseWinnerMissing(reward.UnresolvedOwner ?? "Emperor_resume_reward_tail");
            var ownedPeople = people;
            people = null;
            working = BaseCapturePersonPrefix.ReleaseWithinCapture(working, current, ownedPeople);
            working = working with { AutomaticEmperorCapture = null, BaseWinnerEmperorTail = null };
            //The original valid-Army-only history/entry report remains absent.
            return BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
        }
        finally
        {
            current.TransferModeStateReader = previousTransfer;
            if (areas is not null) CityCaptureTerritorySource.ReleaseRelatedAreas(current, areas);
            if (counts is not null) BaseCaptureRewardSource.ReleaseTerritoryRewards(current, counts);
            if (regions is not null) Release(regions);
            if (states is not null) Release(states);
            if (people is not null) working = BaseCapturePersonPrefix.ReleaseWithinCapture(working, current, people);
        }

        int Quote(CityCaptureFoodQuote quote)
        {
            int value = quote.Value ?? throw BaseWinnerMissing(quote.UnresolvedOwner ?? "Emperor_resume_food_quote");
            working = quote.State ?? working;
            return value;
        }
        void Release(HexListHandle list)
        { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    }
}
