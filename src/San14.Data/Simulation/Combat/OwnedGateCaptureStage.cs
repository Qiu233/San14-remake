using San14.Core;

namespace San14.Data;

public sealed record OwnedGateCaptureReceipt(int LocationId, int PreviousForceId, int WinnerForceId,
    int WinnerUnitId, int CommanderId, BaseCapturePersonPrefixReceipt? Persons,
    BaseCaptureResourceReceipt? Resources, BaseCaptureTailReceipt? Rewards,
    IReadOnlyList<string> SourceEffectOrder, bool NativeSkipped = false)
{
    public int ReturnedTroops { get; init; }
    public PeriodReportEntry[] Reports => NativeSkipped ? [] :
        [new("capture", WinnerForceId, null, CommanderId, 0) { LocationId = LocationId, RuleId = OwnedGateCaptureStage.Revision },
         new("unit_entered", WinnerForceId, null, CommanderId, ReturnedTroops) { LocationId = LocationId, RuleId = OwnedGateCaptureStage.Revision }];
}
public sealed record OwnedGateCaptureResult(GameState? State, OwnedGateCaptureReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>The ordinary Gate branch of2851B0 in one private operation.
/// Uses actual Home people, current District/Hex writers and29F220. The
/// enclosing19 consumer owns the live140 traversal;26 owns header retirement.</summary>
public sealed class OwnedGateCaptureStage(ArmyCampaignRuleAdapter package)
{
    public const string Revision = "ce-Gate-2851B0-26DA10-29F220-capture-20261008";
    public OwnedGateCaptureResult TryApply(GameState input, BaseCaptureRequest request,
        NativeMapSeasonDate? operationThirdDate = null, TacticSourceSamplerWorld? sampler = null)
        => TryApplyCore(input, request, operationThirdDate, sampler, null);

    internal OwnedGateCaptureResult TryApplyCurrent(GameState input, SourceWorldContext current,
        BaseCaptureRequest request, NativeMapSeasonDate? operationThirdDate, TacticSourceSamplerWorld? sampler)
        => TryApplyCore(input, request, operationThirdDate, sampler, current);

    private OwnedGateCaptureResult TryApplyCore(GameState input, BaseCaptureRequest request,
        NativeMapSeasonDate? operationThirdDate, TacticSourceSamplerWorld? sampler, SourceWorldContext? borrowedCurrent)
    {
        try
        {
            if (request.Target.Kind != SettlementBaseKind.Gate || request.WinnerUnitId is not int winnerId)
                throw Missing("branch.ordinary_Gate_settlement");
            var current = borrowedCurrent ?? package.CaptureOwnedArmyReturnWorld(input).Context;
            var target = ArmyDestructionQueue.ResolveBaseReference(input, current, request.Target);
            int winnerSlot = ArmyDestructionQueue.ResolveUnitSlot(input, winnerId) ?? throw Missing("winner_entity_mapping");
            var winner = new ArmyUnitReference(winnerSlot);
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
            var arena = input.ReadArmyLifecycle() ?? throw Missing("winner_arena");
            var winnerReceiver = arena.Receivers.Find(arena.Slots[winnerSlot].ReceiverId)
                ?? throw Missing("winner_receiver");
            int unitId = K(winnerReceiver.ActiveUnitId, "winner_projection");
            if (oldForce == newForce)
                return new(input, new(location, oldForce, newForce, unitId, commanderId, null, null, null, [], true), null);
            if (oldForce is < 1 or > 45 || newForce is < 1 or > 45) throw Missing("branch.nonordinary_Force");
            var winningForce = current.ReadForceTableReference(newForce) ?? throw Missing("winner_Force_pointer");
            var effects = new List<string>(); var working = input;
            var initial = BaseCaptureRewardSource.ApplyInitialForceResult(working, current, winningForce, target);
            working = initial.State ?? throw Missing(initial.UnresolvedOwner ?? "257670_Base_result");
            effects.Add("2851B0.257670_Base_kind6");
            var persons = new BaseCapturePersonPrefix(package).TryApplyCurrent(working, current, target, winner, operationThirdDate, sampler);
            working = persons.State ?? throw Missing(persons.UnresolvedOwner ?? "2851B0_person_prefix");
            current = persons.Current!; var prefix = persons.Receipt!;
            effects.Add("2851B0.current_HomeArmy_and_Person_prefix");
            var commanderReward = package.ApplyBaseCaptureCommanderRewardCurrent(working, current, target, winner);
            working = commanderReward.State ?? throw Missing(commanderReward.UnresolvedOwner ?? "winner_reward");
            effects.Add("2851B0.2B9290_kind3_and_1695C0");
            var territory = BaseCaptureRewardSource.BeginTerritoryRewards(working, current, winningForce);
            var counts = territory.Receipt ?? throw Missing(territory.UnresolvedOwner ?? "State_Region_temporaries");
            effects.Add("2851B0.State_Region_counts_before_transfer");
            _ = GateCaptureRoutingSource.RequireEmptyOldForceNearbyArmies(working, current, target);
            effects.Add("2851B0.mode0_current_ring_old_Force_Army_body_empty");
            var assignment = GateCaptureTerritorySource.AssignGateCity(current, target, newDistrict, commander);
            var districtEffects = new List<CampaignEffectRequest>();
            var queries = package.CreateCaptureDistrictOperationQueries(current, () => working,
                next => working = next, operationThirdDate, districtEffects);
            Primary(current, assignment.CapturedOldDistrict, queries);
            effects.Add("26D2E0.270880_and_first_old_District_2E9140");
            current.SetObjectForceSlot13(location, (byte)newForce);
            current.CurrentSceneryLayout = new(null);
            var previousTransferModeReader = current.TransferModeStateReader;
            current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
            _ = GateCaptureTerritorySource.ApplyFootprintBeforeResources(working, current, target, newForce);
            effects.Add("26DA10.Object13_and_footprint185D00_2413D0");
            var resources = BaseCaptureResourceSource.ApplyCurrent(working, current, target, new(commander), package.CreateArmyDestructionAbilities(current));
            var area = CurrentGateMapQueries.FindArea(target, current);
            int areaId = K(area.PhysicalSlot, "Gate_Area_pointer");
            ushort assigned = K(current.ReadAssignmentArea36(areaId), "Gate_Area36");
            if (assigned != 0)
            {
                current.SetAssignmentArea36(areaId, 0);
                var removed = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, areaId, assigned);
                if (removed.UnresolvedOwner is not null) throw Missing(removed.UnresolvedOwner);
            }
            effects.Add("26DA10.resource_keep_and_Area36_removal");
            Primary(current, assignment.CapturedOldDistrict, queries); Primary(current, district, queries);
            working = package.ApplyBaseCaptureHomeArmiesCurrent(working, current,
                current.ReadOwnedCurrentListPool() ?? throw Missing("HomeArmy_pool"),
                target, operationThirdDate, districtEffects);
            var refreshed = PersonAdvisorResetSource.RefreshWithinBaseCapture(working, current, winningForce);
            working = refreshed.State ?? throw Missing(refreshed.UnresolvedOwner ?? "27BE40_new_Force");
            effects.Add("26DA10.two_2E9140_HomeArmy_and_27BE40");
            _ = GateCaptureTerritorySource.ApplyTerritoryAfterResources(working, current, target, newForce,
                assignment.CapturedOldDistrict.PhysicalSlot ?? 0, newDistrict);
            int cap = K(ArmyDestructionParameterSeed.ReadCurrent(working, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "231480_bound");
            byte zero = unchecked((byte)Math.Min(cap, 0));
            current.SetCaptureBaseThreatFields(target, new(zero, zero, 255, 0));
            _ = GateCaptureRoutingSource.ClearTargetRouting(current, target);
            effects.Add("26DA10.2DF9F0_final_footprint_threat_and_D0_routing");
            //The territory wrapper is local to26DA10. Winner29F220 uses
            //the actual ordinary transfer-service fields retained at entry.
            current.TransferModeStateReader = previousTransferModeReader;
            //Gate capture cannot delete an ordinary Force's final City. Its
            //structural Gate branch has no City-only Emperor/C9/1562 writers.
            var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("capture_primary_pool");
            //Personnel and State/Region temporaries still belong to this
            //same primary pool. Project domain roles without exporting or
            //replacing the operation before their real source release tail.
            working = ArmyCampaignRuleAdapter.PublishCaptureDistrictOperationCurrent(working, current, queries, districtEffects);
            working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            var returnWorld = OwnedArmyReturnWorld.Borrow(working, current, pool);
            var returnResult = package.PrepareOwnedArmyReturnCurrent(working, winner, target, 0,
                returnWorld.Context, returnWorld.Pool, package.CreateCaptureGovernorQueries(working), new(false, false),
                operationDate: operationThirdDate, sampler: sampler);
            var returnPacket = returnResult.Packet ?? throw Missing(returnResult.UnresolvedOwner ?? "winner_29F220");
            working = returnWorld.CommitPrepared(working, returnPacket) ?? throw Missing("winner_29F220_publication");
            current = returnWorld.Context;
            effects.Add("2851B0.winner_29F220_live140_pending70");
            var tail = BaseCaptureRewardSource.ApplyTailWithinCapture(working, current, target, winningForce, district, winner, counts);
            working = tail.State ?? throw Missing(tail.UnresolvedOwner ?? "2851B0_reward_tail");
            working = BaseCapturePersonPrefix.ReleaseWithinCapture(working, current, prefix);
            working = BaseCapturePublicationSource.Publish(working, current, current.ReadOwnedCurrentListPool()!);
            effects.Add("2851B0.Force164_and_release_temporaries");
            return new(working, new(location, oldForce, newForce, unitId, commanderId, prefix, resources, tail.Receipt, effects)
                { ReturnedTroops = input.Units[unitId].Troops }, null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private static void Primary(SourceWorldContext current, DistrictReference district,
        IArmyReturnGovernorCurrentQueries queries)
    {
        var result = PersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, district, queries);
        if (result.UnresolvedOwner is not null) throw Missing(result.UnresolvedOwner);
    }
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("current.Gate2851B0." + name);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal IArmyReturnGovernorCurrentQueries CreateCaptureGovernorQueries(GameState state) => new CaptureGovernorQueries(() => state, _governorInputs);
    private sealed class CaptureGovernorQueries(Func<GameState> latest, OwnedArmyGovernorComparatorInputs inputs) : IArmyReturnGovernorCurrentQueries
    { public ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int personPhysical) => inputs.ReadCurrentComparatorFields(latest(), personPhysical); }

    internal ArmyReturnDistrictOperationQueries CreateCaptureDistrictOperationQueries(SourceWorldContext current,
        Func<GameState> latest, Action<GameState> publish, NativeMapSeasonDate? date,
        List<CampaignEffectRequest> effects)
        => new(new CaptureGovernorQueries(latest, _governorInputs), latest, publish, (person, assignment) =>
        {
            if (assignment.NativeReturn is null) throw new InvalidDataException(assignment.UnresolvedOwner ?? "capture.District_assignment");
            if (assignment.Reports.Count != 0)
                publish(ApplyPersonAssociationRemovalReports(latest(), current, person, assignment,
                    () => (ReadArmyDestructionSampler(latest(), current)
                        ?? throw new InvalidDataException("capture.District_report_World450")).Day450, effects));
        }, current, [], this, date);

    internal static GameState PublishCaptureDistrictOperationCurrent(GameState state, SourceWorldContext current,
        ArmyReturnDistrictOperationQueries operation, List<CampaignEffectRequest> effects)
    {
        state = PersonReturnDistrictReorganizationSource.PublishCurrent(state, current, operation);
        state = PersonReturnDistrictTransferSource.PublishAreaAssignments(state, current, [], operation.ChangedGates);
        return effects.Count == 0 ? state : state with
        {
            CampaignEffectRequests = (state.CampaignEffectRequests
                ?? throw new InvalidDataException("capture.District_effect_owner")).Append(CampaignEffectSource.ForceAssociationRemoval,
                    state.NativeMapSeason?.AdvancedCalendarSteps ?? throw new InvalidDataException("capture.District_effect_calendar"), effects.ToArray())
        };
    }
}
