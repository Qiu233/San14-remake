using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2851B0 with a saved winning District and a genuinely null
    /// Army. The District supplies the recipient Force;22CD80(null) remains
    /// Force0 at the distinct friendly-Army early return. No Commander reward
    /// or29F220 winner entry is reached.</summary>
    internal GameState ApplyBaseWinnerCaptureCurrent(GameState state, SourceWorldContext current,
        BaseCaptureRequest request, NativeMapSeasonDate? date, TacticSourceSamplerWorld? sampler,
        List<CampaignEffectRequest> effects, ArmyMapGroupingOwner? groups = null)
    {
        if (request.WinnerUnitId is not null || request.WinnerDistrictId is not int districtId)
            throw BaseWinnerMissing("winning_District_owner");
        var target = ArmyDestructionQueue.ResolveBaseReference(state, current, request.Target);
        if (!K(current.ReadBaseNativeValidity(target), "target_validity")) return state;
        var district = current.ReadDistrictTableReference(districtId)
            ?? throw BaseWinnerMissing("winning_District_pointer");
        int newForce = K(current.ReadDistrictForceSlot(district), "winning_District10");
        int oldForce = K(current.ReadBaseForceSlot(target), "target_ForceB0");
        //285233 compares the target to District10;285252 independently
        //compares it to22CD80(Army0), whose real result is Force0.
        var noArmy = new ArmyUnitReference(null);
        if (oldForce == newForce || oldForce == current.ReadArmyUnitRawForceSlot(noArmy)) return state;
        if (!K(current.ReadDistrictNativeValidity(district), "winning_District_validity") ||
            oldForce is < 1 or > 45 || newForce is < 1 or > 45)
            throw BaseWinnerMissing("nonordinary_District_or_Force");
        var force = current.ReadForceTableReference(newForce) ?? throw BaseWinnerMissing("winning_Force_pointer");
        int person0 = K(current.ReadPersonLookupReference(0)?.PhysicalSlot, "Person0_pointer");
        int location = K(current.ReadBaseObjectLink(target), "target4E");
        var pool = current.ReadOwnedCurrentListPool() ?? throw BaseWinnerMissing("current_pool");
        //The Person prefix publishes its own source-ordered report batches.
        //Commit preceding intents first so those reports cannot overtake them.
        if (effects.Count != 0)
        {
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests
                ?? throw BaseWinnerMissing("effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                    state.NativeClock?.UpdateOrdinal ?? throw BaseWinnerMissing("effect_clock"), effects.ToArray()) };
            effects.Clear();
        }
        var initial = BaseCaptureRewardSource.ApplyInitialForceResult(state, current, force, target);
        var working = initial.State ?? throw BaseWinnerMissing(initial.UnresolvedOwner ?? "initial_Force_result");
        if (target.Kind == BaseKind.City)
        {
            var callback = BaseCaptureRewardSource.ReadInitialCityCallbackGate(working, current, target);
            if (callback.Reached is not false) throw BaseWinnerMissing(callback.UnresolvedOwner ?? "City2A2620_callback");
        }
        var people = new BaseCapturePersonPrefix(this).TryApplyCurrent(working, current, target,
            noArmy, date, sampler, winnerDistrict: district);
        working = people.State ?? throw BaseWinnerMissing(people.UnresolvedOwner ?? "person_prefix");
        var prefix = people.Receipt ?? throw BaseWinnerMissing("person_prefix_receipt");
        var territory = BaseCaptureRewardSource.BeginTerritoryRewards(working, current, force);
        var counts = territory.Receipt ?? throw BaseWinnerMissing(territory.UnresolvedOwner ?? "territory_counts");
        //The callback's late current Force may differ after a defeated ruler
        //loses their final City. Keep the same mutable current and group owner.
        working = ApplyBaseCaptureNearbyArmiesCurrent(working, current, pool, target, date, effects, sampler);
        var queries = CreateCaptureDistrictOperationQueries(current, () => working,
            value => working = value, date, effects, newForce, groups);
        var previousTransfer = current.TransferModeStateReader;
        current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
        try
        {
            if (target.Kind == BaseKind.City)
            {
                var scope = CityCaptureTerritorySource.CaptureRelatedAreas(current, target, newForce);
                var assignment = CityCaptureTerritorySource.AssignCityDistrict(current, target, districtId);
                Primary(assignment.CapturedOldDistrict);
                current.SetObjectForceSlot13(location, (byte)newForce);
                current.CurrentSceneryLayout = new(null);
                _ = CityCaptureTerritorySource.ApplyFootprintBeforeResources(working, current, target, newForce);
                _ = BaseCaptureResourceSource.ApplyCurrent(working, current, target, new(person0), CreateArmyDestructionAbilities(current));
                _ = CityCaptureTerritorySource.ClearCityAdministration(current, target);
                Primary(assignment.CapturedOldDistrict); Primary(district);
                HomeArmyAndForce();
                _ = CityCaptureTerritorySource.ApplyTerritoryAfterResources(working, current, scope, districtId,
                    CreateArmyDestructionAbilities(current));
                _ = CityCaptureTerritorySource.RequireNoReachedAreaObjectService(current, scope);
                ClearThreat();
                _ = CityCaptureTerritorySource.RequireEmperorCaptureNotReached(working, current, scope);
                _ = CityCaptureTerritorySource.ClearTargetRouting(current, target);
                _ = CityCaptureTerritorySource.RequireNoSpecialStateAdvantageTransition(working, current, scope);
                _ = CityCaptureTerritorySource.ApplyCityFoodWarning(working, current, target,
                    () =>
                    {
                        var quote = ReadCityCaptureFoodQuoteCurrent(working, current, target,
                            groups ?? CreateCaptureMapGroupingOwner());
                        working = quote.State ?? working;
                        return quote.Value ?? throw BaseWinnerMissing(quote.UnresolvedOwner ?? "food_expense");
                    },
                    () =>
                    {
                        var quote = CityCaptureFoodWarningSource.TryReadPredictedFoodIncome(working, current, target,
                            date, CreateArmyDestructionAbilities(current), FoodIncomeSceneries);
                        return quote.Value ?? throw BaseWinnerMissing(quote.UnresolvedOwner ?? "food_income");
                    });
                CityCaptureTerritorySource.ReleaseRelatedAreas(current, scope);
            }
            else
            {
                var assignment = GateCaptureTerritorySource.AssignGateCity(current, target, districtId, person0);
                Primary(assignment.CapturedOldDistrict);
                current.SetObjectForceSlot13(location, (byte)newForce);
                current.CurrentSceneryLayout = new(null);
                _ = GateCaptureTerritorySource.ApplyFootprintBeforeResources(working, current, target, newForce);
                _ = BaseCaptureResourceSource.ApplyCurrent(working, current, target, new(person0), CreateArmyDestructionAbilities(current));
                var area = CurrentGateMapQueries.FindArea(target, current);
                int areaId = K(area.PhysicalSlot, "Gate_Area_pointer");
                ushort assigned = K(current.ReadAssignmentArea36(areaId), "Gate_Area36");
                if (assigned != 0)
                {
                    current.SetAssignmentArea36(areaId, 0);
                    var removal = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, areaId, assigned);
                    if (removal.UnresolvedOwner is not null) throw BaseWinnerMissing(removal.UnresolvedOwner);
                }
                Primary(assignment.CapturedOldDistrict); Primary(district);
                HomeArmyAndForce();
                _ = GateCaptureTerritorySource.ApplyTerritoryAfterResources(working, current, target, newForce,
                    assignment.CapturedOldDistrict.PhysicalSlot ?? 0, districtId);
                ClearThreat();
                _ = GateCaptureRoutingSource.ClearTargetRouting(current, target);
            }
        }
        finally { current.TransferModeStateReader = previousTransfer; }
        working = PublishCaptureDistrictOperationCurrent(working, current, queries, effects);
        effects.Clear(); //The shared publisher has appended these intents once.
        working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
        var tail = BaseCaptureRewardSource.ApplyTailWithinCapture(working, current, target, force, district, noArmy, counts);
        working = tail.State ?? throw BaseWinnerMissing(tail.UnresolvedOwner ?? "Force_reward_tail");
        working = BaseCapturePersonPrefix.ReleaseWithinCapture(working, current, prefix);
        working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
        //2851B0's capture-history block is inside the valid-Army branch.
        //Army0 produces neither that history nor an entered-unit report.
        return working;

        void Primary(DistrictReference value)
        {
            var result = PersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, value, queries);
            if (result.UnresolvedOwner is not null) throw BaseWinnerMissing(result.UnresolvedOwner);
        }
        void HomeArmyAndForce()
        {
            working = ApplyBaseCaptureHomeArmiesCurrent(working, current, pool, target, date, effects);
            var refreshed = PersonAdvisorResetSource.RefreshWithinBaseCapture(working, current, force);
            working = refreshed.State ?? throw BaseWinnerMissing(refreshed.UnresolvedOwner ?? "Force_refresh27BE40");
        }
        void ClearThreat()
        {
            int cap = K(ArmyDestructionParameterSeed.ReadCurrent(working, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "counter_bound");
            byte zero = unchecked((byte)Math.Min(cap, 0));
            current.SetCaptureBaseThreatFields(target, new(zero, zero, 255, 0));
        }
        static T K<T>(T? value, string name) where T : struct => value ?? throw BaseWinnerMissing(name);
    }

    private static InvalidDataException BaseWinnerMissing(string owner) => new("current.Base_winner2851B0." + owner);
}
