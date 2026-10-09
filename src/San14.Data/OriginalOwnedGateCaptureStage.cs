using San14.Core;

namespace San14.Data;

public sealed record OriginalOwnedGateCaptureReceipt(int LocationId, int PreviousForceId, int WinnerForceId,
    int WinnerUnitId, int CommanderId, OriginalBaseCapturePersonPrefixReceipt? Persons,
    OriginalBaseCaptureResourceReceipt? Resources, OriginalBaseCaptureTailReceipt? Rewards,
    IReadOnlyList<string> SourceEffectOrder, bool NativeSkipped = false)
{
    public int ReturnedTroops { get; init; }
    public PeriodReportEntry[] Reports => NativeSkipped ? [] :
        [new("capture", WinnerForceId, null, CommanderId, 0) { LocationId = LocationId, RuleId = OriginalOwnedGateCaptureStage.Revision },
         new("unit_entered", WinnerForceId, null, CommanderId, ReturnedTroops) { LocationId = LocationId, RuleId = OriginalOwnedGateCaptureStage.Revision }];
}
public sealed record OriginalOwnedGateCaptureResult(GameState? State, OriginalOwnedGateCaptureReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>The ordinary Gate branch of2851B0 in one private operation.
/// Uses actual Home people, current District/Hex writers and29F220. The
/// enclosing19 consumer owns the live140 traversal;26 owns header retirement.</summary>
public sealed class OriginalOwnedGateCaptureStage(OriginalOwnedArmyCampaignRules package)
{
    public const string Revision = "ce-Gate-2851B0-26DA10-29F220-capture-20261008";
    public OriginalOwnedGateCaptureResult TryApply(GameState input, NativeArmyDestructionQueuePayload request,
        NativeMapSeasonDate? operationThirdDate = null, OriginalTacticSourceSamplerWorld? sampler = null)
    {
        try
        {
            if (request.VictimBase8 is not { Kind: NativeArmyDestructionReceiverKind.Gate, PhysicalSlot: >= 1 and <= 10 } victim ||
                request.AttackerArmy10 is not { Kind: NativeArmyDestructionReceiverKind.Army, PhysicalSlot: int winnerSlot } || request.Raw20 != 0)
                throw Missing("branch.ordinary_Gate_mode0_payload");
            var target = new OriginalBaseReference(OriginalBaseKind.Gate, victim.PhysicalSlot);
            var winner = new OriginalArmyUnitReference(winnerSlot);
            var world = package.CaptureOwnedArmyReturnWorld(input); var current = world.Context;
            if (!K(current.ReadBaseNativeValidity(target), "Gate_validity") || !K(current.ReadArmyUnitNativeValidity(winner), "winner_validity"))
                throw Missing("branch.invalid_Gate_or_winner");
            int location = K(current.ReadBaseObjectLink(target), "Gate4E");
            int oldForce = K(current.ReadBaseForceSlot(target), "Gate_B0");
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
                return new(input, new(location, oldForce, newForce, unitId, commanderId, null, null, null, [], true), null);
            if (oldForce is < 1 or > 45 || newForce is < 1 or > 45) throw Missing("branch.nonordinary_Force");
            var winningForce = current.ReadForceTableReference(newForce) ?? throw Missing("winner_Force_pointer");
            var effects = new List<string>(); var working = input;
            var initial = OriginalBaseCaptureRewardSource.ApplyInitialForceResult(working, current, winningForce, target);
            working = initial.State ?? throw Missing(initial.UnresolvedOwner ?? "257670_Base_result");
            effects.Add("2851B0.257670_Base_kind6");
            var persons = new OriginalBaseCapturePersonPrefix(package).TryApply(working, target, winner, operationThirdDate, sampler);
            working = persons.State ?? throw Missing(persons.UnresolvedOwner ?? "2851B0_person_prefix");
            current = persons.Current!; var prefix = persons.Receipt!;
            effects.Add("2851B0.current_HomeArmy_and_Person_prefix");
            var commanderReward = package.ApplyBaseCaptureCommanderReward(working, current, target, winner);
            working = commanderReward.State ?? throw Missing(commanderReward.UnresolvedOwner ?? "winner_reward");
            effects.Add("2851B0.2B9290_kind3_and_1695C0");
            var territory = OriginalBaseCaptureRewardSource.BeginTerritoryRewards(working, current, winningForce);
            var counts = territory.Receipt ?? throw Missing(territory.UnresolvedOwner ?? "State_Region_temporaries");
            effects.Add("2851B0.State_Region_counts_before_transfer");
            _ = OriginalGateCaptureRoutingSource.RequireEmptyOldForceNearbyArmies(working, current, target);
            effects.Add("2851B0.mode0_current_ring_old_Force_Army_body_empty");
            var assignment = OriginalGateCaptureTerritorySource.AssignGateCity(current, target, newDistrict, commander);
            var queries = package.CreateCaptureGovernorQueries(working);
            Primary(current, assignment.CapturedOldDistrict, queries);
            effects.Add("26D2E0.270880_and_first_old_District_2E9140");
            current.SetObjectForceSlot13(location, (byte)newForce);
            current.CurrentSceneryLayout = new(null);
            current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
            _ = OriginalGateCaptureTerritorySource.ApplyFootprintBeforeResources(working, current, target, newForce);
            effects.Add("26DA10.Object13_and_footprint185D00_2413D0");
            var resources = OriginalBaseCaptureResourceSource.ApplyCurrent(working, current, target, new(commander), package.CreateArmyDestructionAbilities(current));
            var area = OriginalCurrentGateMapQueries.FindArea(target, current);
            int areaId = K(area.PhysicalSlot, "Gate_Area_pointer");
            ushort assigned = K(current.ReadAssignmentArea36(areaId), "Gate_Area36");
            if (assigned != 0)
            {
                current.SetAssignmentArea36(areaId, 0);
                var removed = OriginalPersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, areaId, assigned);
                if (removed.UnresolvedOwner is not null) throw Missing(removed.UnresolvedOwner);
            }
            effects.Add("26DA10.resource_keep_and_Area36_removal");
            Primary(current, assignment.CapturedOldDistrict, queries); Primary(current, district, queries);
            RequireNoHomeArmies(current, location);
            var refreshed = OriginalPersonAdvisorResetSource.RefreshCurrentForce(working, current, winningForce);
            working = refreshed.State ?? throw Missing(refreshed.UnresolvedOwner ?? "27BE40_new_Force");
            effects.Add("26DA10.two_2E9140_empty_HomeArmy_and_27BE40");
            _ = OriginalGateCaptureTerritorySource.ApplyTerritoryAfterResources(working, current, target, newForce,
                assignment.CapturedOldDistrict.PhysicalSlot ?? 0, newDistrict);
            int cap = K(OriginalArmyDestructionParameterSeed.ReadCurrent(working, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "231480_bound");
            byte zero = unchecked((byte)Math.Min(cap, 0));
            current.SetCaptureBaseThreatFields(target, new(zero, zero, 255, 0));
            _ = OriginalGateCaptureRoutingSource.ClearTargetRouting(current, target);
            effects.Add("26DA10.2DF9F0_final_footprint_threat_and_D0_routing");
            //Gate capture cannot delete an ordinary Force's final City. Its
            //structural Gate branch has no City-only Emperor/C9/1562 writers.
            var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("capture_primary_pool");
            working = OriginalBaseCapturePublicationSource.Publish(working, current, pool);
            var returnWorld = package.CaptureOwnedArmyReturnWorld(working);
            var returnResult = OriginalArmyOrdinaryReturnRuntime.PrepareCurrentResourceOutputs(winner, target, 0,
                returnWorld.Context, returnWorld.Pool, package.CreateCaptureGovernorQueries(working), new(false, false),
                new OriginalArmyReturnCaptivesSource(package, working, new(false)));
            var returnPacket = returnResult.Packet ?? throw Missing(returnResult.UnresolvedOwner ?? "winner_29F220");
            working = returnWorld.CommitPrepared(working, returnPacket) ?? throw Missing("winner_29F220_publication");
            current = returnWorld.Context;
            effects.Add("2851B0.winner_29F220_live140_pending70");
            var tail = OriginalBaseCaptureRewardSource.ApplyTail(working, current, target, winningForce, district, winner, counts);
            working = tail.State ?? throw Missing(tail.UnresolvedOwner ?? "2851B0_reward_tail");
            working = OriginalBaseCapturePersonPrefix.Release(working, current, prefix);
            working = OriginalBaseCapturePublicationSource.Publish(working, current, current.ReadOwnedCurrentListPool()!);
            effects.Add("2851B0.Force164_and_release_temporaries");
            return new(working, new(location, oldForce, newForce, unitId, commanderId, prefix, resources, tail.Receipt, effects)
                { ReturnedTroops = input.Units[unitId].Troops }, null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private static void Primary(OriginalEventInitializationContext current, OriginalDistrictReference district,
        IOriginalArmyReturnGovernorCurrentQueries queries)
    {
        var result = OriginalPersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, district, queries);
        if (result.UnresolvedOwner is not null) throw Missing(result.UnresolvedOwner);
    }
    internal static void RequireNoHomeArmies(OriginalEventInitializationContext current, int location)
    {
        foreach (var row in OriginalArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
        {
            if (K(current.ReadOwnedArmyExclusion233560(row), "HomeArmy233560")) continue;
            var army = new OriginalArmyUnitReference(K(row.PhysicalSlot, "HomeArmy_pointer"));
            int id = K(current.ReadArmyUnitCommanderId(army), "HomeArmy12");
            int person = K(current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot, "HomeArmy_Commander");
            if (K(current.ReadPersonNativeValidity(person), "HomeArmy_Commander_validity") &&
                K(current.ReadPersonFirstLocation(person), "HomeArmy_E2") == location)
                throw Missing("branch.26CDF0_current_HomeArmy");
        }
    }
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("current.Gate2851B0." + name);
}

public sealed partial class OriginalOwnedArmyCampaignRules
{
    internal IOriginalArmyReturnGovernorCurrentQueries CreateCaptureGovernorQueries(GameState state) => new CaptureGovernorQueries(state, _governorInputs);
    private sealed class CaptureGovernorQueries(GameState state, OriginalOwnedArmyGovernorComparatorInputs inputs) : IOriginalArmyReturnGovernorCurrentQueries
    { public OriginalArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int personPhysical) => inputs.ReadCurrentComparatorFields(state, personPhysical); }
}
