using San14.Core;

namespace San14.Data;

public sealed record OriginalOwnedCityCaptureReceipt(int LocationId, int PreviousForceId, int WinnerForceId,
    int WinnerUnitId, int CommanderId, OriginalBaseCapturePersonPrefixReceipt? Persons,
    OriginalBaseCaptureResourceReceipt? Resources, OriginalCityCaptureTerritoryReceipt? Territory,
    OriginalBaseCaptureTailReceipt? Rewards, IReadOnlyList<string> SourceEffectOrder, bool NativeSkipped = false)
{
    public int ReturnedTroops { get; init; }
    public PeriodReportEntry[] Reports => NativeSkipped ? [] :
        [new("capture", WinnerForceId, LocationId, CommanderId, 0) { LocationId = LocationId, RuleId = OriginalOwnedCityCaptureStage.Revision },
         new("unit_entered", WinnerForceId, LocationId, CommanderId, ReturnedTroops) { LocationId = LocationId, RuleId = OriginalOwnedCityCaptureStage.Revision }];
}
public sealed record OriginalOwnedCityCaptureResult(GameState? State, OriginalOwnedCityCaptureReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>Ordinary non-last City2851B0: one private current operation from
/// the real140 request to26DA10,29F220 and the reward tail. Reached special
/// callbacks stay unresolved;19 owns traversal and26 owns final retirement.</summary>
public sealed class OriginalOwnedCityCaptureStage(OriginalOwnedArmyCampaignRules package)
{
    public const string Revision = "ce-City-2851B0-26DA10-29F220-capture-20261008";
    public OriginalOwnedCityCaptureResult TryApply(GameState input, BaseCaptureRequest request,
        NativeMapSeasonDate? operationThirdDate = null, OriginalTacticSourceSamplerWorld? sampler = null)
    {
        try
        {
            if (request.Target.Kind != SettlementBaseKind.City || request.WinnerUnitId is not int winnerId)
                throw Missing("branch.ordinary_City_settlement");
            var world = package.CaptureOwnedArmyReturnWorld(input); var current = world.Context;
            var target = OriginalArmyDestructionQueue.ResolveBaseReference(input, current, request.Target);
            int winnerSlot = OriginalArmyDestructionQueue.ResolveUnitSlot(input, winnerId) ?? throw Missing("winner_entity_mapping");
            var winner = new OriginalArmyUnitReference(winnerSlot);
            if (!K(current.ReadBaseNativeValidity(target), "City_validity") || !K(current.ReadArmyUnitNativeValidity(winner), "winner_validity"))
                throw Missing("branch.invalid_City_or_winner");
            int location = K(current.ReadBaseObjectLink(target), "City4E");
            int oldForce = K(current.ReadBaseForceSlot(target), "City_B0");
            int commanderId = K(current.ReadArmyUnitCommanderId(winner), "winner12");
            int commander = K(current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)?.PhysicalSlot, "Commander_pointer");
            if (!K(current.ReadPersonNativeValidity(commander), "Commander_validity")) throw Missing("branch.invalid_Commander");
            byte rawDistrict = K(current.ReadPersonDistrictSlot(commander), "Commander118");
            var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0) ?? throw Missing("current_22BB10_District");
            if (!K(current.ReadDistrictNativeValidity(district), "winner_District_validity")) throw Missing("branch.invalid_winner_District");
            int newDistrict = K(district.PhysicalSlot, "winner_District_pointer");
            int newForce = K(current.ReadDistrictForceSlot(district), "winner_District10");
            int unitId = K(input.NativeArmyLifecycle!.Slots[winnerSlot].ActiveUnitId, "winner_projection");
            if (oldForce == newForce)
                return new(input, new(location, oldForce, newForce, unitId, commanderId, null, null, null, null, [], true), null);
            if (oldForce is < 1 or > 45 || newForce is < 1 or > 45) throw Missing("branch.nonordinary_Force");
            int cityCount = 0;
            for (int slot = 1; slot <= 51; slot++)
                if (K(current.ReadBaseForceSlot(current.ReadCityTableReference(slot) ?? throw Missing("old_Force_City_pointer")), "old_Force_City_B0") == oldForce)
                    cityCount++;
            if (cityCount <= 1) throw Missing("branch.last_City_full_capture_Force_unregister");
            var winningForce = current.ReadForceTableReference(newForce) ?? throw Missing("winner_Force_pointer");
            var effects = new List<string>(); var working = input;
            var initial = OriginalBaseCaptureRewardSource.ApplyInitialForceResult(working, current, winningForce, target);
            working = initial.State ?? throw Missing(initial.UnresolvedOwner ?? "257670_Base_result");
            var callback = OriginalBaseCaptureRewardSource.ReadInitialCityCallbackGate(working, current, target);
            if (callback.Reached is not false) throw Missing(callback.UnresolvedOwner ?? "branch.City2A2620_callback");
            effects.Add("2851B0.257670_and_C0_2A2620_admission");
            var persons = new OriginalBaseCapturePersonPrefix(package).TryApply(working, target, winner, operationThirdDate, sampler);
            working = persons.State ?? throw Missing(persons.UnresolvedOwner ?? "person_prefix");
            current = persons.Current!; var prefix = persons.Receipt!;
            effects.Add("2851B0.current_HomeArmy_and_Person_prefix");
            var commanderReward = package.ApplyBaseCaptureCommanderReward(working, current, target, winner);
            working = commanderReward.State ?? throw Missing(commanderReward.UnresolvedOwner ?? "winner_reward");
            effects.Add("2851B0.2B9290_kind3_and_1695C0");
            var rewards = OriginalBaseCaptureRewardSource.BeginTerritoryRewards(working, current, winningForce);
            var counts = rewards.Receipt ?? throw Missing(rewards.UnresolvedOwner ?? "State_Region_temporaries");
            _ = OriginalGateCaptureRoutingSource.RequireEmptyOldForceNearbyArmies(working, current, target);
            effects.Add("2851B0.State_Region_counts_and_mode0_current_ring");
            var scope = OriginalCityCaptureTerritorySource.CaptureRelatedAreas(current, target, newForce);
            var assignment = OriginalCityCaptureTerritorySource.AssignCityDistrict(current, target, newDistrict);
            var queries = package.CreateCaptureGovernorQueries(working);
            Primary(current, assignment.CapturedOldDistrict, queries);
            effects.Add("26DA10.related_Area_chain_26D2E0_and_first_old_2E9140");
            current.SetObjectForceSlot13(location, (byte)newForce);
            current.CurrentSceneryLayout = new(null);
            current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
            _ = OriginalCityCaptureTerritorySource.ApplyFootprintBeforeResources(working, current, target, newForce);
            var resources = OriginalBaseCaptureResourceSource.ApplyCurrent(working, current, target, new(commander), package.CreateArmyDestructionAbilities(current));
            _ = OriginalCityCaptureTerritorySource.ClearCityAdministration(current, target);
            effects.Add("26DA10.Object13_footprint_resources_and_current_administration");
            Primary(current, assignment.CapturedOldDistrict, queries); Primary(current, district, queries);
            OriginalOwnedGateCaptureStage.RequireNoHomeArmies(current, location);
            var refreshed = OriginalPersonAdvisorResetSource.RefreshCurrentForce(working, current, winningForce);
            working = refreshed.State ?? throw Missing(refreshed.UnresolvedOwner ?? "27BE40_new_Force");
            var land = OriginalCityCaptureTerritorySource.ApplyTerritoryAfterResources(working, current, scope, newDistrict, package.CreateArmyDestructionAbilities(current));
            _ = OriginalCityCaptureTerritorySource.RequireNoReachedAreaObjectService(current, scope);
            int cap = K(OriginalArmyDestructionParameterSeed.ReadCurrent(working, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "231480_bound");
            byte zero = unchecked((byte)Math.Min(cap, 0));
            current.SetCaptureBaseThreatFields(target, new(zero, zero, 255, 0));
            _ = OriginalCityCaptureTerritorySource.RequireEmperorCaptureNotReached(working, current, scope);
            _ = OriginalCityCaptureTerritorySource.ClearTargetRouting(current, target);
            _ = OriginalCityCaptureTerritorySource.RequireNoSpecialStateAdvantageTransition(working, current, scope);
            _ = OriginalCityCaptureTerritorySource.ApplyCityFoodWarning(working, current, target,
                () => Food(OriginalCityCaptureFoodWarningSource.TryReadFoodExpense(working, current, target)),
                () => Food(OriginalCityCaptureFoodWarningSource.TryReadPredictedFoodIncome(working, current, target, operationThirdDate)));
            OriginalCityCaptureTerritorySource.ReleaseRelatedAreas(current, scope);
            effects.Add("26DA10.territory_E0_threat_Emperor_routing_State_C9_and_release_Area");
            working = OriginalBaseCapturePublicationSource.Publish(working, current, current.ReadOwnedCurrentListPool() ?? throw Missing("capture_primary_pool"));
            var returnWorld = package.CaptureOwnedArmyReturnWorld(working);
            var returned = OriginalArmyOrdinaryReturnRuntime.PrepareCurrentResourceOutputs(winner, target, 0,
                returnWorld.Context, returnWorld.Pool, package.CreateCaptureGovernorQueries(working), new(false, false),
                new OriginalArmyReturnCaptivesSource(package, working, new(false)));
            working = returnWorld.CommitPrepared(working, returned.Packet ?? throw Missing(returned.UnresolvedOwner ?? "winner_29F220"))
                ?? throw Missing("winner_29F220_publication");
            current = returnWorld.Context;
            var tail = OriginalBaseCaptureRewardSource.ApplyTail(working, current, target, winningForce, district, winner, counts);
            working = tail.State ?? throw Missing(tail.UnresolvedOwner ?? "reward_tail");
            working = OriginalBaseCapturePersonPrefix.Release(working, current, prefix);
            working = OriginalBaseCapturePublicationSource.Publish(working, current, current.ReadOwnedCurrentListPool()!);
            effects.Add("2851B0.winner_29F220_Force164_World1562_and_release_temporaries");
            return new(working, new(location, oldForce, newForce, unitId, commanderId, prefix, resources, land, tail.Receipt, effects)
                { ReturnedTroops = input.Units[unitId].Troops }, null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
    private static int Food(OriginalCityCaptureFoodQuote result) => result.Value ?? throw Missing(result.UnresolvedOwner ?? "current_food_quote");
    private static void Primary(OriginalEventInitializationContext current, OriginalDistrictReference district,
        IOriginalArmyReturnGovernorCurrentQueries queries)
    {
        var result = OriginalPersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, district, queries);
        if (result.UnresolvedOwner is not null) throw Missing(result.UnresolvedOwner);
    }
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("current.City2851B0." + name);
}
