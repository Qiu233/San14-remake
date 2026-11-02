using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : ICampaignExpeditionExecution
{
    internal const string CampaignAllRetreatBookCostKey = "simulation.campaign-expedition.all-retreat-command-cost";

    CommandResult ICampaignExpeditionExecution.Retreat(GameState state, ForceState force,
        RetreatCampaignsOrder order, CommandContext context)
        => ApplyCampaignAllRetreatCurrent(state, force, order.CampaignIds, context);

    internal int ReadCampaignAllRetreatBookCostCurrent(GameState state)
    {
        var bank = state.NativeArmySimulation?.Parameters;
        if (bank is null || bank.Language != "TC" || bank.RuleVersion != SimulationRuleIdentity.ForLocale("TC") ||
            bank.Float32Values.ContainsKey(CampaignAllRetreatBookCostKey)) throw CampaignRetreatMissing("command_cost");
        //The TC rule is immutable offline data. Saved integer overrides win,
        //including when an older campaign predates its named parameter key.
        return bank.Int32Values.TryGetValue(CampaignAllRetreatBookCostKey, out int value) ? value : 1;
    }

    internal CommandResult ApplyCampaignAllRetreatCurrent(GameState state, ForceState force,
        IReadOnlyList<int> campaignIds, CommandContext context)
    {
        try
        {
            var owner = state.CampaignExpeditions ?? throw CampaignRetreatMissing("controller_owner");
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var pool = world.Pool;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw CampaignRetreatMissing("current_player");
            if (player.PhysicalSlot != force.Id || !CK(current.ReadForceNativeValidity(player), "player_validity"))
                return CommandResult.Reject("Only the current player can withdraw expeditions.", "campaign.expedition.control");
            foreach (int id in campaignIds)
                if (!owner.Expeditions.TryGetValue(id, out var expedition) || expedition.ProvinceId == 0)
                    return CommandResult.Reject("An expedition is no longer active.", "campaign.expedition.selection");
            var operation = FacilityOperation.Playable(state);
            return CommandResult.Accept(ContinueCampaignAllRetreatCurrent(state, current, pool,
                campaignIds, operation, null));
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable(error.Message, "Expedition withdrawal is unresolved."); }
    }

    // Both the initial command and a saved player captive reply enter this
    // same loop. Only the interrupted Campaign uses its saved manager suffix.
    private GameState ContinueCampaignAllRetreatCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, IReadOnlyList<int> campaignIds, FacilityOperation operation,
        ImmutableArray<int>? firstManagerSuffix)
    {
        var working = state;
        for (int campaignIndex = 0; campaignIndex < campaignIds.Count; campaignIndex++)
        {
            int id = campaignIds[campaignIndex];
            var manager = campaignIndex == 0 && firstManagerSuffix is { } saved ? saved :
                (current.ReadRegistryReferences(RegistryKind.Army)
                    ?? throw CampaignRetreatMissing("manager60"))
                    .Select(entry => CK(entry?.PhysicalSlot, "manager60_Army")).ToImmutableArray();
            for (int armyIndex = 0; armyIndex < manager.Length; armyIndex++)
            {
                int physical = manager[armyIndex];
                var image = CampaignRetreatArmyImage(working, physical);
                var source = working.ReadArmyLifecycle()!.Slots[physical];
                if (CK(image.NativeMapUiByte13C ?? source.MapUiByte13C, "Army13C") != id) continue;
                var army = new ArmyUnitReference(physical);
                ushort at = CK(current.ReadArmyUnitRawHex2A(army), "Army2A");
                var hex = current.ReadHexTableReference(at < 48400 ? at : 0)
                    ?? throw CampaignRetreatMissing("Army_Hex");
                ushort link = CK(current.ReadHexObjectLink(hex), "Hex1A");
                var location = current.ReadCityTableReference(link <= 51 ? link : 0)
                    ?? throw CampaignRetreatMissing("City_pointer");
                if (!CK(current.ReadBaseNativeValidity(location), "City_validity"))
                    location = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0)
                        ?? throw CampaignRetreatMissing("Gate_pointer");
                if (!CK(current.ReadBaseNativeValidity(location), "Base_validity"))
                    working = ApplyCampaignFieldWithdrawalCurrent(working, current, image, operation);
                else
                {
                    working = ReturnCampaignArmyAtBaseCurrent(working, current, pool, army, location, operation,
                        out int retainedPerson);
                    if (working.ArmyReturnCaptiveContinuation is { } pending)
                    {
                        // The return prefix is committed, but this Army's
                        // retained Person flag and Campaign tail are not.
                        return working with { ArmyReturnCaptiveContinuation = pending with
                        {
                            Caller = ArmyReturnCaptiveCaller.CampaignWithdrawal,
                            CampaignTail = new(id, campaignIds.Skip(campaignIndex + 1).ToImmutableArray(),
                                operation.ReadDate(), operation.Revision)
                            {
                                RemainingArmyPhysicalSlots = manager.Skip(armyIndex + 1).ToImmutableArray(),
                                RetainedCommanderPhysicalSlot = retainedPerson,
                                OperationCampaignDay = operation.ReadCampaignDay(working)
                            }
                        } };
                    }
                }
            }
            var reached = working.CampaignExpeditions ?? throw CampaignRetreatMissing("reached_controller");
            var expedition = reached.Expeditions.GetValueOrDefault(id) ?? throw CampaignRetreatMissing("reached_Campaign");
            working = working with { CampaignExpeditions = reached with
                { Expeditions = new(reached.Expeditions) { [id] = expedition with { IsWithdrawing = true } } } };
        }
        //28E6D0 occurs once after all selected Campaigns, before District14.
        working = ConsumeArmyLifecyclePendingCurrent(working, current, pool)
            ?? throw CampaignRetreatMissing("pending70_cleanup");
        var primary = ForcePrimaryDistrictSource.Resolve(current,
            OwnedCurrentPlayerForce.ReadCurrentReference(working, current) ?? throw CampaignRetreatMissing("fee_player"));
        int districtId = CK(primary.PhysicalSlot, "fee_primary_District");
        var district = working.Districts.GetValueOrDefault(districtId) ?? throw CampaignRetreatMissing("fee_District_owner");
        int before = district.OrdersRemaining ?? throw CampaignRetreatMissing("fee_District14");
        int after = Math.Clamp(unchecked(before - ReadCampaignAllRetreatBookCostCurrent(working)), 0, 200);
        current.SetDistrictRawByte(districtId, 0x14, (byte)after);
        var districtFields = working.NativeForceUnregisterCurrent ?? throw CampaignRetreatMissing("fee_District_saved_fields");
        var fields = districtFields.DistrictWindows10.GetValueOrDefault(districtId)?.ToArray()
            ?? throw CampaignRetreatMissing("fee_District_saved_window");
        fields[4] = (byte)after;
        working = working with
        {
            Districts = new(working.Districts) { [districtId] = district with { OrdersRemaining = after } },
            NativeForceUnregisterCurrent = districtFields with
            { DistrictWindows10 = new(districtFields.DistrictWindows10) { [districtId] = fields } }
        };
        working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
        working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
        working = OwnedArmyLifecycleTargets.PublishCurrent(working, current);
        return working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
            { Indexes = pool.ExportOwnedState() });
    }

    /// <summary>Continue only the caller suffix belonging to the already
    /// committed return prefix. Selection replies never begin a new period.</summary>
    internal GameState ResumeDirectArmyReturnCaptiveSuffix(GameState input)
    {
        var pending = input.ArmyReturnCaptiveContinuation
            ?? throw CampaignRetreatMissing("captive_return_owner");
        if (pending.Caller is not (ArmyReturnCaptiveCaller.OrdinaryEntry or ArmyReturnCaptiveCaller.CampaignWithdrawal))
            throw CampaignRetreatMissing("captive_return_caller");
        var world = CaptureOwnedArmyReturnWorld(input);
        var working = ResumePlayerArmyReturnCurrent(input, world.Context, world.Pool);
        if (pending.Caller == ArmyReturnCaptiveCaller.OrdinaryEntry)
        {
            if (pending.DeploymentCancellationTail is { } cancelled)
            {
                ClearCampaignReturningCommanderCurrent(world.Context, cancelled.RetainedCommanderPhysicalSlot);
                var cancelledCity = world.Context.ReadCityTableReference(cancelled.CityId)
                    ?? throw CampaignRetreatMissing("cancelled_return_City");
                return FinishCancelledDeploymentReturnCurrent(working, world.Context, world.Pool,
                    new(pending.ArmyPhysicalSlot), cancelledCity,
                    new FacilityOperation(() => cancelled.OperationDate, cancelled.OperationRevision,
                        _ => cancelled.OperationCampaignDay));
            }
            working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, world.Context);
            working = ConsumeArmyLifecyclePendingCurrent(working, world.Context, world.Pool)
                ?? throw CampaignRetreatMissing("captive_return_pending70_cleanup");
            if (pending.ImmediateEntryReportRuleId is { } reportRule)
            {
                var original = input.Units[pending.ArmyUnitId];
                var settled = PlayableOfficerArrivalAftermath.Settle(working, [original.OfficerId]);
                var reports = settled.Reports.Append(new PeriodReportEntry("unit_entered", original.ForceId,
                    pending.Destination.Id, original.OfficerId, original.Troops, original.HexId, original.HexId)
                    { LocationId = pending.Destination.Id, RuleId = reportRule }).ToArray();
                working = settled.State with
                {
                    PeriodReports = settled.State.PeriodReports.Concat(reports).ToArray(),
                    PendingCommandReports = settled.State.PendingCommandReports.Concat(reports).ToArray()
                };
            }
            return working;
        }
        var tail = pending.CampaignTail ?? throw CampaignRetreatMissing("captive_Campaign_tail");
        ClearCampaignReturningCommanderCurrent(world.Context, tail.RetainedCommanderPhysicalSlot);
        var operation = new FacilityOperation(() => tail.OperationDate, tail.OperationRevision,
            _ => tail.OperationCampaignDay);
        return ContinueCampaignAllRetreatCurrent(working, world.Context, world.Pool,
            new[] { tail.CurrentExpeditionId }.Concat(tail.RemainingExpeditionIds).ToArray(),
            operation, tail.RemainingArmyPhysicalSlots);
    }

    private static void ClearCampaignReturningCommanderCurrent(SourceWorldContext current, int person)
    {
        ushort flags = CK(current.ReadArmyReturnPersonFlags196(person), "return_Person15E");
        current.WriteArmyReturnPersonFlags196(person, (ushort)(flags & 0xFFFE));
    }

    private GameState ReturnCampaignArmyAtBaseCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, ArmyUnitReference army, BaseReference target, FacilityOperation operation,
        out int person)
    {
        ushort rawCommander = CK(current.ReadArmyUnitCommanderId(army), "return_Commander12");
        person = CK((rawCommander <= 6000 ? current.ReadPersonLookupReference(rawCommander)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "return_Commander_pointer");
        var working = state;
        if (CK(current.ReadArmyUnitNativeValidity(army), "return_Army_validity") &&
            CK(current.ReadPersonNativeValidity(person), "return_Commander_validity") &&
            CK(current.ReadPersonOldForceSlot(person), "return_Army_Force") ==
            CK(current.ReadBaseForceSlot(target), "return_Base_Force"))
        {
            //Borrow without rebinding any owner from an earlier saved view.
            var world = OwnedArmyReturnWorld.Borrow(working, current, pool);
            var image = CampaignRetreatArmyImage(working, CK(army.PhysicalSlot, "return_Army_identity"));
            var prepared = PrepareOwnedArmyReturnCurrent(working, army, target, 0,
                current, pool, CreateCaptureGovernorQueries(working), new(false, false),
                image.NativeTacticRuntime?.Status10 == 2 ? image.NativeTacticRuntime.Flags26 : null,
                operationDate: operation.ReadDate());
            if (prepared.Suspension is { } suspension)
                return world.CommitSuspension(working, suspension);
            var packet = prepared.Packet ?? throw CampaignRetreatMissing(prepared.UnresolvedOwner ?? "29F220");
            working = world.CommitPrepared(working, packet) ?? throw CampaignRetreatMissing("29F220_publication");
        }
        //The caller clears this retained Person pointer even if29F220's own
        //validity/affiliation gate skipped its body. It does not ungroup yet.
        ClearCampaignReturningCommanderCurrent(current, person);
        return working;
    }

    private GameState ApplyCampaignFieldWithdrawalCurrent(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw CampaignRetreatMissing("withdraw_Army_receiver");
        var actor = new ArmyUnitReference(physical);
        //2EA480 still writes48 and runs its no-path suffix for an invalid
        //header, including receivers already detached from active Units.
        if (!CK(current.ReadArmyUnitNativeValidity(actor), "withdraw_Army_validity"))
            return ApplyInvalidCampaignArmyWithdrawalCurrent(state, current, image, operation);
        byte control = CK(ArmySavedCommandKnowledge.ReadControl(state, image), "withdraw_control140");
        if (control == 0)
            return image.NativeTacticRuntime?.Status10 == 2
                ? FinishTransportReturnCommand(state, current, image, operation)
                : ApplyAutomaticArmyWithdrawal(state, current, image, operation, reportReason: false);
        //2EA480 preserves37/38/3A and the saved command under control140.
        //It still clears the five live points, override (unless25), and48.
        var runtime = image.NativeTacticRuntime ?? throw CampaignRetreatMissing("withdraw_runtime");
        var changed = image with
        {
            MilitaryPlan = image.MilitaryPlan is { } plan ? plan with
                { WaypointHexIds = [], NextWaypointIndex = 0, IncidentalEnemyId = null } : null,
            March = (image.March ?? throw CampaignRetreatMissing("withdraw_March")) with
                { Target = ArmyMarchDestination.None, FollowingWaypoints = new([]), NextStep = ArmyMarchDestination.None },
            OverrideTarget = runtime.Abnormal25 == 0 ? ArmyOverrideTargetState.Cleared : image.OverrideTarget
        };
        var working = state with { Units = new(state.Units) { [image.Id] = changed } };
        if (runtime.Status10 == 2)
        {
            byte choice = CK(TransportRouteSourceAdapter.ReadChoice(state, image), "withdraw_transport_Choice41");
            working = working with { ResourceState = TransportRouteSourceAdapter.Write(working.ResourceState,
                image.Id, Enumerable.Repeat((ushort)48400, 5).ToArray(), choice) };
            changed = changed with { TransportRoute = TransportRouteSourceAdapter.Decode(Enumerable.Repeat((ushort)48400, 5).ToArray()) };
            working = working with { Units = new(working.Units) { [image.Id] = changed } };
        }
        if (runtime.Abnormal25 == 0)
        {
            current.ClearArmyCommandOverride(physical);
            var arena = working.ReadArmyLifecycle()!;
            var receiver = arena.Receivers.FindUnit(image.Id) ?? throw CampaignRetreatMissing("withdraw_receiver");
            working = working.WithArmyLifecycle(arena with
                { Receivers = arena.Receivers.With(receiver with { OverrideTarget = ArmyOverrideTargetState.Cleared }) });
            working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, changed);
        }
        working = FRoute(working, image.Id, true, current, false);
        changed = working.Units[image.Id];
        if (changed.March?.NextStep != ArmyMarchDestination.None) return working;
        if (runtime.Status10 != 1) return StopTransportRoute(working, current, changed, operation, maintenanceCaller: false);
        _ = CK(current.ReadArmyUnitRawHex2A(actor), "withdraw_no_path_Army2A");
        if (changed.OverrideTarget?.Kind == ArmyOverrideTargetKind.None && runtime.Abnormal23 == 0 && runtime.Abnormal24 == 0 && runtime.Abnormal25 == 0)
            working = AppendArmyCommandSpeech(working, current, changed, operation, CombatSpeechEvent.ArmyHoldingPosition);
        return IsPlayerRulerLedDistrict(working, current, changed)
            ? AppendAutomaticWithdrawalReport(working, current, changed, operation, 0x3231, null) : working;
    }

    private static UnitState CampaignRetreatArmyImage(GameState state, int physical)
    {
        var arena = state.ReadArmyLifecycle() ?? throw CampaignRetreatMissing("Army_lifecycle");
        var slot = arena.Slots[physical];
        var receiver = arena.Receivers.Find(slot.ReceiverId) ?? throw CampaignRetreatMissing("Army_receiver");
        return receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) ?? throw CampaignRetreatMissing("active_Army_image")
            : receiver.PendingImage ?? throw CampaignRetreatMissing("pending_Army_image");
    }
    private static T CK<T>(T? value, string field) where T : struct => value ?? throw CampaignRetreatMissing(field);
    private static InvalidDataException CampaignRetreatMissing(string field) => new("campaign.all-retreat." + field);
}
