using San14.Core;

namespace San14.Data;

public sealed record ArmyCommandStageCheck(int PhysicalSlot, int? UnitId, bool? NativeValid,
    byte? Action, byte? CommandControl);
public sealed record ArmyCommandStageResult(GameState? State,
    IReadOnlyList<ArmyCommandStageCheck> Checks, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Stage3 collects and sorts registered action17 actors, then
    /// applies the complete supported false-report success branch atomically.</summary>
    public ArmyCommandStageResult TryAdvanceSpecialArmyActions(GameState state,
        NativeArmyProgressStageMoment moment) => TryAdvanceArmyCommandGate(state, moment, false);

    /// <summary>Stage4 reads control140 before any native-validity gate.
    /// Zero control completes independently of the unused backup141/142 values.
    /// The owned saved-stay/invalid-target branch restores with its real route
    /// and report tail. Other positive branches remain unresolved.</summary>
    public ArmyCommandStageResult TryAdvanceArmyCommandRestoration(GameState state,
        NativeArmyProgressStageMoment moment) => TryAdvanceArmyCommandGate(state, moment, true);

    private ArmyCommandStageResult TryAdvanceArmyCommandGate(GameState state,
        NativeArmyProgressStageMoment moment, bool restoration)
    {
        byte stage = (byte)(restoration ? NativeArmyProgressStage.RestoreArmyCommands : NativeArmyProgressStage.SpecialArmyActions);
        if (moment.SchedulerStageIndex != stage || moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Army command stage requires its reached ordinary two-hour stage entry.");
        if (!restoration) return AdvanceFalseReportStage(state, moment);
        try
        {
            var lifecycle = state.ReadArmyLifecycle() ?? throw CommandStageMissing("saved_lifecycle");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var registry = current.ReadRegistryReferences(RegistryKind.Army) ?? throw CommandStageMissing("manager60");
            var slots = lifecycle.ReceiverSlots();
            var checks = new List<ArmyCommandStageCheck>();
            var working = state;
            var player = restoration ? OwnedCurrentPlayerForce.ReadCurrentReference(state, current) : null;
            foreach (var entry in registry)
            {
                if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical } || physical is < 0 or > 500)
                    throw CommandStageMissing("manager_Army_pointer");
                var slot = working.ReadArmyLifecycle()!.ReceiverSlots()[physical];
                UnitState? image = slot.ActiveUnitId is int id
                    ? working.Units.GetValueOrDefault(id) ?? throw CommandStageMissing("active_Army_owner")
                    : slot.PendingImage;
                if (restoration)
                {
                    //2E1AD0 has no native-validity filter before reading140.
                    //A complete receiver command is authoritative. Without
                    //it, an old image retains only its own scalar knowledge.
                    byte control = slot.SavedCommand?.Control ?? (image?.NativeCommandControl140 is byte known ? known : image is null
                        ? slot.CommandControl140 ?? throw CommandStageMissing("retired_Army_control140")
                        : throw CommandStageMissing("current_Army_control140"));
                    if (control != 0)
                        working = RestoreSavedStayCommand(working, current, slot, image, player, moment);
                    checks.Add(new(physical, image?.Id, null, null, control));
                }
                else
                {
                    bool valid = current.ReadArmyUnitNativeValidity(new(physical)) ?? throw CommandStageMissing("native_Army_validity");
                    if (!valid) { checks.Add(new(physical, image?.Id, false, null, null)); continue; }
                    if (image is null) throw CommandStageMissing("valid_Army_action_owner");
                    byte action = ArmyConfigurationSourceAdapter.ReadAction(state, image) ?? throw CommandStageMissing("current_Army_action37");
                    if (action == 17) throw CommandStageMissing("branch_action17_score_and_application");
                    checks.Add(new(physical, image.Id, true, action, null));
                }
            }
            return new(working, checks.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private GameState RestoreSavedStayCommand(GameState state, SourceWorldContext current,
        ArmyLifecycleReceiverView slot, UnitState? image, ForceReference? player,
        NativeArmyProgressStageMoment moment)
    {
        if (slot.SavedCommand is not { Kind: ArmySavedCommandKind.Stored,
                Command: { Action.Id: 13, Target: { Kind: ArmyTargetKind.Hex,
                    Destination: { Kind: ArmyTargetDestinationKind.Hex, HexId: not null } } } } ||
            image is null || slot.ActiveUnitId != image.Id)
            throw CommandStageMissing("branch_nonzero140_complete_saved_stay");
        var army = new ArmyUnitReference(slot.PhysicalSlot);
        if (current.ReadArmyUnitNativeValidity(army) != true || image.NativeTacticRuntime?.Status10 != 1 ||
            ArmyConfigurationSourceAdapter.ReadAction(state, image) != 1)
            throw CommandStageMissing("branch_restoration_actor_or_action");
        var target = OwnedArmyLifecycleTargets.Read(state, slot.ReceiverId);
        if (target is not { Kind38: 9, Identity3A: ushort identity })
            throw CommandStageMissing("current_restoration_Hex_target");
        var hex = current.ReadHexTableReference(identity < 48400 ? identity : 0)
            ?? throw CommandStageMissing("current_restoration_target_Hex_pointer");
        ushort link = current.ReadHexObjectLink(hex) ?? throw CommandStageMissing("current_restoration_target_Object_link");
        var obj = current.ReadObjectTableReference(link < 3001 ? link : 0)
            ?? throw CommandStageMissing("current_restoration_Object_pointer");
        if (current.ReadObjectNativeValidity(obj) is not bool validObject)
            throw CommandStageMissing("current_restoration_Object_validity");
        if (validObject) return PreserveSavedCommandAtValidObject(state, image);
        if (image.NativeTacticRuntime.Abnormal25 != 0 || image.March is not { Target.Kind: ArmyMarchDestinationKind.None } march ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, image) is not ushort at)
            throw CommandStageMissing("branch_restoration_abnormal_or_waypoints");

        var restored = image with { NativeCommandControl140 = 0,
            FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(13),
            OverrideTarget = ArmyOverrideTargetState.Cleared,
            March = march with { NextStep = ArmyMarchDestination.None } };
        var staged = OwnedArmyLifecycleTargets.Write(state, slot.ReceiverId, new(9, at));
        var lifecycle = staged.ReadArmyLifecycle()!;
        var sources = lifecycle.Slots.ToArray();
        sources[slot.PhysicalSlot] = sources[slot.PhysicalSlot] with { CommandControl140 = 0 };
        staged = (staged with { Units = new(staged.Units) { [image.Id] = restored },
            ResourceState = ArmyConfigurationSourceAdapter.WriteAction(staged.ResourceState, image.Id, 13) })
            .WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(slot.Receiver with
                { SavedCommand = ArmySavedCommandState.Cleared }), Source = lifecycle.Source with { Slots = sources } });
        staged = ArmyOverrideTargetSourceAdapter.ReconcileOwner(staged, restored);
        var routeCurrent = CaptureWritableTransportRouteCurrent(staged);
        var route = OrdinaryArmyRouteSource.Rewrite(staged, routeCurrent, restored, refresh: true);
        if (route.Receipt is not { } receipt) throw CommandStageMissing(route.UnresolvedOwner ?? "restoration_route");
        restored = restored with { NativeRouteGoal1F8 = receipt.CachedGoalWrite ?? restored.NativeRouteGoal1F8,
            March = receipt.NextHexWrite is ushort next ? restored.March with
                { NextStep = ArmyMarchSourceAdapter.DecodeDestination(next) } : restored.March };
        staged = (staged with { Units = new(staged.Units) { [image.Id] = restored } }).WithArmyLifecycle(
            staged.ReadArmyLifecycle()! with { Indexes = routeCurrent.ReadOwnedCurrentListPool()!.ExportOwnedState() });

        int force = current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army))
            ?? throw CommandStageMissing("restoration_report_Army_force");
        int playerForce = player is not null ? current.ReadForcePhysicalSlot(player)
            ?? throw CommandStageMissing("restoration_report_player_identity")
            : throw CommandStageMissing("restoration_report_player");
        if (force == playerForce)
        {
            int commander = slot.Commander12 ?? throw CommandStageMissing("restoration_report_commander");
            if (!state.Officers.TryGetValue(commander, out var person) || !state.Forces.ContainsKey(force))
                throw CommandStageMissing("restoration_report_domain_identity");
            int personPhysical = current.ResolveOfficerSource(person.Id) ?? throw CommandStageMissing("restoration_report_person");
            byte district = current.ReadPersonDistrictSlot(personPhysical) ?? throw CommandStageMissing("restoration_report_district");
            if (district != 0 && !state.Districts.ContainsKey(district)) throw CommandStageMissing("restoration_report_district_identity");
            var report = new PeriodReportEntry("army_command_restored", force, OfficerId: commander)
            { UnitId = image.Id, DistrictId = district == 0 ? null : district, CampaignDay = StatusCampaignDay(state, moment),
                RuleId = "army.progress.command-restored.C96F" };
            staged = staged with { PendingCommandReports = [.. staged.PendingCommandReports, report] };
        }
        return staged;
    }

    private static InvalidDataException CommandStageMissing(string owner) => new("army.command_stage." + owner);
}
