using San14.Core;

namespace San14.Data;

public sealed record OwnedCityCaptureReceipt(int LocationId, int PreviousForceId, int WinnerForceId,
    int WinnerUnitId, int CommanderId, BaseCapturePersonPrefixReceipt? Persons,
    BaseCaptureResourceReceipt? Resources, CityCaptureTerritoryReceipt? Territory,
    BaseCaptureTailReceipt? Rewards, IReadOnlyList<string> SourceEffectOrder, bool NativeSkipped = false)
{
    public int ReturnedTroops { get; init; }
    public PeriodReportEntry[] Reports => NativeSkipped ? [] :
        [new("capture", WinnerForceId, LocationId, CommanderId, 0) { LocationId = LocationId, RuleId = OwnedCityCaptureStage.Revision },
         new("unit_entered", WinnerForceId, LocationId, CommanderId, ReturnedTroops) { LocationId = LocationId, RuleId = OwnedCityCaptureStage.Revision }];
}
public sealed record OwnedCityCaptureResult(GameState? State, OwnedCityCaptureReceipt? Receipt,
    string? UnresolvedOwner)
{
    public bool NativeTerminal { get; init; }
}

/// <summary>Ordinary non-last City2851B0: one private current operation from
/// the real140 request to26DA10,29F220 and the reward tail. Reached special
/// callbacks stay unresolved;19 owns traversal and26 owns final retirement.</summary>
public sealed partial class OwnedCityCaptureStage(ArmyCampaignRuleAdapter package)
{
    public const string Revision = "ce-City-2851B0-26DA10-29F220-capture-20261008";
    public OwnedCityCaptureResult TryApply(GameState input, BaseCaptureRequest request,
        NativeMapSeasonDate? operationThirdDate = null, TacticSourceSamplerWorld? sampler = null)
        => TryApplyCore(input, request, operationThirdDate, sampler, null, null);

    internal OwnedCityCaptureResult TryApplyCurrent(GameState input, SourceWorldContext current,
        BaseCaptureRequest request, NativeMapSeasonDate? operationThirdDate, TacticSourceSamplerWorld? sampler,
        ArmyMapGroupingOwner? groups = null)
        => TryApplyCore(input, request, operationThirdDate, sampler, current, groups);

    private OwnedCityCaptureResult TryApplyCore(GameState input, BaseCaptureRequest request,
        NativeMapSeasonDate? operationThirdDate, TacticSourceSamplerWorld? sampler, SourceWorldContext? borrowedCurrent,
        ArmyMapGroupingOwner? groups)
    {
        try
        {
            if (input.CityCaptureEmperorTail is not null)
                return ResumeEmperorTail(input, request, operationThirdDate, sampler, borrowedCurrent, groups);
            if (request.Target.Kind != SettlementBaseKind.City || request.WinnerUnitId is not int winnerId)
                throw Missing("branch.ordinary_City_settlement");
            var resumed = input.BaseCaptureContinuation;
            if (resumed is not null && resumed.Operation.Request != request)
                throw Missing("resume_capture_request");
            var saved = resumed?.Operation;
            if (saved is { SampleDay: int sampleDay, SampleSalt: int sampleSalt })
                sampler = new(sampleDay, sampleSalt);
            var current = borrowedCurrent ?? package.CaptureOwnedArmyReturnWorld(input).Context;
            groups ??= package.CreateCaptureMapGroupingOwner();
            var target = ArmyDestructionQueue.ResolveBaseReference(input, current, request.Target);
            int winnerSlot = saved?.WinnerArmyPhysicalSlot ?? ArmyDestructionQueue.ResolveUnitSlot(input, winnerId)
                ?? throw Missing("winner_entity_mapping");
            var winner = new ArmyUnitReference(winnerSlot);
            if (saved is null && !K(current.ReadBaseNativeValidity(target), "City_validity") ||
                !K(current.ReadArmyUnitNativeValidity(winner), "winner_validity"))
                throw Missing("branch.invalid_City_or_winner");
            int location = K(current.ReadBaseObjectLink(target), "City4E");
            int oldForce = saved?.PreviousForceId ?? K(current.ReadBaseForceSlot(target), "City_B0");
            int commanderId = saved?.CommanderOfficerId ?? K(current.ReadArmyUnitCommanderId(winner), "winner12");
            int commander = K(current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)?.PhysicalSlot, "Commander_pointer");
            if (!K(current.ReadPersonNativeValidity(commander), "Commander_validity")) throw Missing("branch.invalid_Commander");
            int districtId = saved?.WinnerDistrictId ?? K(current.ReadPersonDistrictSlot(commander), "Commander118");
            var district = current.ReadDistrictTableReference(districtId <= 51 ? districtId : 0) ?? throw Missing("current_22BB10_District");
            if (!K(current.ReadDistrictNativeValidity(district), "winner_District_validity")) throw Missing("branch.invalid_winner_District");
            int newDistrict = K(district.PhysicalSlot, "winner_District_pointer");
            int newForce = saved?.WinnerForceId ?? K(current.ReadDistrictForceSlot(district), "winner_District10");
            var arena = input.ReadArmyLifecycle() ?? throw Missing("winner_arena");
            var winnerReceiver = arena.Receivers.Find(arena.Slots[winnerSlot].ReceiverId)
                ?? throw Missing("winner_receiver");
            int unitId = K(winnerReceiver.ActiveUnitId ?? winnerReceiver.PendingImage?.Id, "winner_projection");
            var winnerImage = winnerReceiver.ActiveUnitId is int activeId
                ? input.Units.GetValueOrDefault(activeId) : winnerReceiver.PendingImage;
            if (saved is not null && (saved.WinnerReceiverId != arena.Slots[winnerSlot].ReceiverId || unitId != winnerId))
                throw Missing("resume_winner_receiver");
            if (saved is null && oldForce == newForce)
                return new(input, new(location, oldForce, newForce, unitId, commanderId, null, null, null, null, [], true), null);
            if (oldForce is < 1 or > 45 || newForce is < 1 or > 45) throw Missing("branch.nonordinary_Force");
            var winningForce = current.ReadForceTableReference(newForce) ?? throw Missing("winner_Force_pointer");
            var effects = new List<string>(); var working = input;
            if (saved is null)
            {
                var initial = BaseCaptureRewardSource.ApplyInitialForceResult(working, current, winningForce, target);
                working = initial.State ?? throw Missing(initial.UnresolvedOwner ?? "257670_Base_result");
                var callbackEffects = new List<CampaignEffectRequest>();
                working = package.ApplyCityCaptureAttackRequestsCurrent(working, current, target, winningForce, callbackEffects);
                if (callbackEffects.Count != 0)
                    working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                        ?? throw Missing("City2A2620_effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                            working.NativeClock?.UpdateOrdinal ?? throw Missing("City2A2620_clock"), callbackEffects) };
                effects.Add("2851B0.257670_and_C0_2A2620_admission");
            }
            var captureContext = saved ?? new BaseCaptureOperationContext(request, oldForce, newForce,
                newDistrict, commanderId, winnerSlot, arena.Slots[winnerSlot].ReceiverId,
                (winnerImage ?? throw Missing("winner_image")).Troops, sampler?.Day450, sampler?.Salt45C);
            var persons = new BaseCapturePersonPrefix(package).TryApplyCurrent(working, current, target, winner, operationThirdDate, sampler, captureContext);
            working = persons.State ?? throw Missing(persons.UnresolvedOwner ?? "person_prefix");
            current = persons.Current!;
            if (working.NativePlayableTerminal is not null || working.CampaignTurnStop is not null)
                return new(working, null, null) { NativeTerminal = true };
            if (working.BaseCaptureContinuation is not null) return new(working, null, null);
            var prefix = persons.Receipt ?? throw Missing("completed_person_prefix");
            effects.Add("2851B0.current_HomeArmy_and_Person_prefix");
            BaseCapturePersonPrefixReceipt? ownedPrefix = prefix;
            BaseCaptureTerritoryReceipt? ownedCounts = null;
            CityCaptureAreaScope? ownedScope = null;
            var previousTransferModeReader = current.TransferModeStateReader;
            var districtEffects = new List<CampaignEffectRequest>();
            ArmyReturnDistrictOperationQueries? queries = null;
            try
            {
                var commanderReward = package.ApplyBaseCaptureCommanderRewardCurrent(working, current, target, winner);
                working = commanderReward.State ?? throw Missing(commanderReward.UnresolvedOwner ?? "winner_reward");
                effects.Add("2851B0.2B9290_kind3_and_1695C0");
                var rewards = BaseCaptureRewardSource.BeginTerritoryRewards(working, current, winningForce);
                var counts = ownedCounts = rewards.Receipt ?? throw Missing(rewards.UnresolvedOwner ?? "State_Region_temporaries");
                working = package.ApplyBaseCaptureNearbyArmiesCurrent(working, current,
                    current.ReadOwnedCurrentListPool() ?? throw Missing("nearby_Army_pool"),
                    target, operationThirdDate, districtEffects, sampler);
                effects.Add("2851B0.State_Region_counts_and_mode0_current_ring");
                var scope = ownedScope = CityCaptureTerritorySource.CaptureRelatedAreas(current, target, newForce);
                var assignment = CityCaptureTerritorySource.AssignCityDistrict(current, target, newDistrict);
                queries = package.CreateCaptureDistrictOperationQueries(current, () => working,
                    next => working = next, operationThirdDate, districtEffects, newForce, groups);
                Primary(current, assignment.CapturedOldDistrict, queries);
                if (IsTerminal()) return CompleteTerminal();
                effects.Add("26DA10.related_Area_chain_26D2E0_and_first_old_2E9140");
                current.SetObjectForceSlot13(location, (byte)newForce);
                current.CurrentSceneryLayout = new(null);
                current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
                _ = CityCaptureTerritorySource.ApplyFootprintBeforeResources(working, current, target, newForce);
                var resources = BaseCaptureResourceSource.ApplyCurrent(working, current, target, new(commander), package.CreateArmyDestructionAbilities(current));
                _ = CityCaptureTerritorySource.ClearCityAdministration(current, target);
                effects.Add("26DA10.Object13_footprint_resources_and_current_administration");
                Primary(current, assignment.CapturedOldDistrict, queries);
                if (IsTerminal()) return CompleteTerminal();
                Primary(current, district, queries);
                if (IsTerminal()) return CompleteTerminal();
                working = package.ApplyBaseCaptureHomeArmiesCurrent(working, current,
                    current.ReadOwnedCurrentListPool() ?? throw Missing("HomeArmy_pool"),
                    target, operationThirdDate, districtEffects);
                var refreshed = PersonAdvisorResetSource.RefreshWithinBaseCapture(working, current, winningForce);
                working = refreshed.State ?? throw Missing(refreshed.UnresolvedOwner ?? "27BE40_new_Force");
                var land = CityCaptureTerritorySource.ApplyTerritoryAfterResources(working, current, scope, newDistrict, package.CreateArmyDestructionAbilities(current), queries);
                working = package.ApplyCityCaptureAreaObjectsCurrent(working, current, scope, districtEffects);
                int cap = K(ArmyDestructionParameterSeed.ReadCurrent(working, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "231480_bound");
                byte zero = unchecked((byte)Math.Min(cap, 0));
                current.SetCaptureBaseThreatFields(target, new(zero, zero, 255, 0));
                var emperor = package.PrepareCaptureEmperorCurrent(working, current, scope);
                working = emperor.State;
                if (emperor.Progress is { } emperorProgress)
                {
                    var suspended = new CityCaptureEmperorTail(resumed?.SettlementIndex ?? -1, captureContext,
                        K(scope.CapturedOldDistrict.PhysicalSlot, "Emperor_old_District"),
                        CaptureEmperorTailLists.ReadObjects(current, scope.Areas, RegistryKind.Area),
                        CaptureEmperorTailLists.ReadStatic(current, counts.States, "State"),
                        CaptureEmperorTailLists.ReadStatic(current, counts.Regions, "Region"), counts.StateCount, counts.RegionCount,
                        CaptureEmperorTailLists.ReadObjects(current, prefix.CapturedTemporary, RegistryKind.Person),
                        CaptureEmperorTailLists.ReadObjects(current, prefix.NoncapturedTemporary, RegistryKind.Person),
                        CaptureEmperorTailLists.ReadObjects(current, prefix.SpecialTemporary, RegistryKind.Person),
                        CaptureEmperorTailLists.ReadObjects(current, prefix.ChangedForceTemporary, RegistryKind.Person));
                    ReleaseOwned();
                    working = ArmyCampaignRuleAdapter.PublishCaptureDistrictOperationCurrent(working, current, queries, districtEffects);
                    districtEffects.Clear();
                    working = working with { AutomaticEmperorCapture = emperorProgress, CityCaptureEmperorTail = suspended };
                    return new(BaseCapturePublicationSource.Publish(working, current,
                        current.ReadOwnedCurrentListPool() ?? throw Missing("Emperor_pause_pool"), projectOwnedRoles: true), null, null);
                }
                _ = CityCaptureTerritorySource.ClearTargetRouting(current, target);
                working = package.ApplyCityCaptureStateAdvantageCurrent(working, current, scope, operationThirdDate, districtEffects);
                _ = CityCaptureTerritorySource.ApplyCityFoodWarning(working, current, target,
                    () =>
                    {
                        var quote = package.ReadCityCaptureFoodQuoteCurrent(working, current, target, groups);
                        int expense = Food(quote);
                        working = quote.State ?? working;
                        return expense;
                    },
                    () => Food(CityCaptureFoodWarningSource.TryReadPredictedFoodIncome(working, current, target, operationThirdDate,
                        package.CreateArmyDestructionAbilities(current), package.FoodIncomeSceneries)));
                CityCaptureTerritorySource.ReleaseRelatedAreas(current, scope);
                ownedScope = null;
                //The territory wrapper is local to26DA10. The actual29F220
                //consumer requires its ordinary saved transfer-service fields.
                current.TransferModeStateReader = previousTransferModeReader;
                effects.Add("26DA10.territory_E0_threat_Emperor_routing_State_C9_and_release_Area");
                working = ArmyCampaignRuleAdapter.PublishCaptureDistrictOperationCurrent(working, current, queries, districtEffects);
                working = BaseCapturePublicationSource.Publish(working, current,
                    current.ReadOwnedCurrentListPool() ?? throw Missing("capture_primary_pool"), projectOwnedRoles: true);
                var returnWorld = OwnedArmyReturnWorld.Borrow(working, current,
                    current.ReadOwnedCurrentListPool() ?? throw Missing("return_primary_pool"));
                var returned = package.PrepareOwnedArmyReturnCurrent(working, winner, target, 0,
                    returnWorld.Context, returnWorld.Pool, package.CreateCaptureGovernorQueries(working), new(false, false),
                    operationDate: operationThirdDate, sampler: sampler);
                working = returnWorld.CommitPrepared(working, returned.Packet ?? throw Missing(returned.UnresolvedOwner ?? "winner_29F220"))
                    ?? throw Missing("winner_29F220_publication");
                current = returnWorld.Context;
                ownedCounts = null; //The shared tail releases both descriptors even on failure.
                var tail = BaseCaptureRewardSource.ApplyTailWithinCapture(working, current, target, winningForce, district, winner, counts);
                working = tail.State ?? throw Missing(tail.UnresolvedOwner ?? "reward_tail");
                ReleaseOwned();
                working = BaseCapturePublicationSource.Publish(working, current, current.ReadOwnedCurrentListPool()!,
                    projectOwnedRoles: borrowedCurrent is not null);
                effects.Add("2851B0.winner_29F220_Force164_World1562_and_release_temporaries");
                return new(working, new(location, oldForce, newForce, unitId, commanderId, prefix, resources, land, tail.Receipt, effects)
                    { ReturnedTroops = captureContext.ReturnedTroops }, null);
            }
            finally { ReleaseOwned(); }

            bool IsTerminal() => working.NativePlayableTerminal is not null || working.CampaignTurnStop is not null;
            void ReleaseOwned()
            {
                current.TransferModeStateReader = previousTransferModeReader;
                if (ownedScope is not null)
                {
                    CityCaptureTerritorySource.ReleaseRelatedAreas(current, ownedScope);
                    ownedScope = null;
                }
                if (ownedCounts is not null)
                {
                    BaseCaptureRewardSource.ReleaseTerritoryRewards(current, ownedCounts);
                    ownedCounts = null;
                }
                if (ownedPrefix is not null)
                {
                    working = BaseCapturePersonPrefix.ReleaseWithinCapture(working, current, ownedPrefix);
                    ownedPrefix = null;
                }
            }
            OwnedCityCaptureResult CompleteTerminal()
            {
                ReleaseOwned();
                if (queries is not null)
                    working = ArmyCampaignRuleAdapter.PublishCaptureDistrictOperationCurrent(working, current, queries, districtEffects);
                working = BaseCapturePublicationSource.Publish(working, current,
                    current.ReadOwnedCurrentListPool() ?? throw Missing("terminal_primary_pool"),
                    projectOwnedRoles: borrowedCurrent is not null);
                return new(working, null, null) { NativeTerminal = true };
            }
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
    private static int Food(CityCaptureFoodQuote result) => result.Value ?? throw Missing(result.UnresolvedOwner ?? "current_food_quote");
    private static void Primary(SourceWorldContext current, DistrictReference district,
        IArmyReturnGovernorCurrentQueries queries)
    {
        var result = PersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, district, queries);
        if (result.UnresolvedOwner is not null) throw Missing(result.UnresolvedOwner);
    }
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("current.City2851B0." + name);
}
