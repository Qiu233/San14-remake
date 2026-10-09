using San14.Core;

namespace San14.Data;

public sealed record PersonFreedomReceipt(int PersonId, int PersonPhysical, int PriorForce,
    int OldHome, int NewHome, int NewArea, short MeritAfter, byte LoyaltyAfter,
    PersonCommandMemberDetachmentReceipt CommandDetachment,
    IReadOnlyList<string> SourceEffectOrder)
{
    public ArmyCause4Receipt? ArmyCleanup { get; init; }
    public ArmyReturnGovernorPacket? GovernorRepair { get; init; }
    public PersonAdvisorResetReceipt? AdvisorReset { get; init; }
    public PersonCaptureWorldCandidatesReceipt? PlayerCandidates { get; init; }
}
public sealed record PersonFreedomResult(GameState? State, PersonFreedomReceipt? Receipt, string? UnresolvedOwner);

/// <summary>265250's ordinary ruler and status3/4 resident leaves. Uses the old capture
/// Base ownership for its actual prefix, detaches an existing Return member
/// or directly destroys a deployed commander Army with mode0/cause4, then
/// publishes the exact free Person fields and a reached current Governor tail.
/// Captives and District-leader reselection remain independent source paths.</summary>
public sealed class PersonFreedomSource(ArmyCampaignRuleAdapter package, LoadedScenario source)
{
    public const string Revision = "ce-265250-ordinary-returning-Person-freedom";
    private readonly PersonFreedomHomeSource homeSource = new(source);

    public PersonFreedomResult TryApplyReturning(GameState postCapture, int personId,
        CityState beforeCaptureCity) => Apply(postCapture, personId, beforeCaptureCity, false);

    public PersonFreedomResult TryApplyDeployedRuler(GameState postCapture, int personId,
        CityState beforeCaptureCity) => Apply(postCapture, personId, beforeCaptureCity, true);

    public PersonFreedomResult TryApplyDeployedResident(GameState postCapture, int personId,
        CityState beforeCaptureCity) => Apply(postCapture, personId, beforeCaptureCity, true, true, false);

    public PersonFreedomResult TryApplyGovernorResident(GameState postCapture, int personId,
        CityState beforeCaptureCity) => Apply(postCapture, personId, beforeCaptureCity, false, true, false, 3);

    public PersonFreedomResult TryApplyReturningGovernor(GameState postCapture, int personId,
        CityState beforeCaptureCity) => Apply(postCapture, personId, beforeCaptureCity, false, true, true, 3);

    public PersonFreedomResult TryApplyDeployedGovernor(GameState postCapture, int personId,
        CityState beforeCaptureCity) => Apply(postCapture, personId, beforeCaptureCity, true, true, false, 3);

    /// <summary>Ordinary status4 resident, with no effective
    /// commander Army. The source may legitimately have no associated Return.</summary>
    public PersonFreedomResult TryApplyResident(GameState postCapture, int personId,
        CityState beforeCaptureCity) => Apply(postCapture, personId, beforeCaptureCity, false, true, false);

    /// <summary>Ordinary resident ruler with no commander Army. An existing
    /// associated receiver is consumed only when22AC00 actually finds one.</summary>
    public PersonFreedomResult TryApplyRulerResident(GameState postCapture, int personId,
        CityState beforeCaptureCity) => Apply(postCapture, personId, beforeCaptureCity, false, false, false);

    private PersonFreedomResult Apply(GameState postCapture, int personId,
        CityState beforeCaptureCity, bool deployedArmy, bool resident = false, bool requireReturn = true, byte residentStatus = 4)
    {
        try
        {
            if (!postCapture.Officers.TryGetValue(personId, out var person) || person.Deployed != deployedArmy ||
                (requireReturn && !deployedArmy && !PlayableOfficerAvailability.IsSourceBusy(postCapture, person)) || person.LocationId != beforeCaptureCity.Id ||
                person.ForceId != beforeCaptureCity.ForceId || !postCapture.Cities.TryGetValue(beforeCaptureCity.Id, out var captured))
                throw Missing("branch.265250_nonordinary_returning_Person");
            var input = postCapture with { Cities = new(postCapture.Cities) { [captured.Id] = captured with
                { ForceId = beforeCaptureCity.ForceId, DistrictId = beforeCaptureCity.DistrictId } } };
            var world = package.CaptureOwnedArmyReturnWorld(input); var current = world.Context;
            int physical = current.ReadPersonLookupReference(personId)?.PhysicalSlot ?? throw Missing("current.freedom_Person_pointer");
            var force = current.ReadPersonForce(physical) ?? throw Missing("current.freedom_original_Force");
            int originalForce = K(current.ReadForcePhysicalSlot(force), "current.freedom_original_Force_physical");
            byte status = K(current.ReadPersonStatus(physical), "current.freedom_PersonE6");
            ushort ruler = K(current.ReadForceRecord(originalForce)?.RawRuler10, "current.freedom_Force_ruler10");
            if (current.ReadForceNativeValidity(force) != true || current.ReadPersonNativeValidity(physical) != true ||
                originalForce is < 1 or > 45 || originalForce != beforeCaptureCity.ForceId ||
                (resident ? status != residentStatus || ruler == personId : status != 1 || ruler != personId))
                throw Missing(resident ? $"branch.265250_ordinary_status{residentStatus}_resident" : "branch.265250_ordinary_ruler_only");
            var currentPlayer = current.ReadCurrentPlayerForce() ?? throw Missing("current.freedom_PlayerForce32EFA0");
            int playerPhysical = K(current.ReadForcePhysicalSlot(currentPlayer), "current.freedom_PlayerForce_physical");
            ushort flags = K(current.ReadArmyReturnPersonFlags196(physical), "current.freedom_Person15E");
            if (OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(physical)) == false) throw Missing("branch.265250_appendix_World_unregister");
            ushort first = K(current.ReadPersonFirstLocation(physical), "current.freedom_PersonE2");
            var oldHome = BaseLookup.Find(first, current);
            if (oldHome.Kind != BaseKind.City || oldHome.PhysicalSlot != captured.Id || current.ReadBaseNativeValidity(oldHome) != true)
                throw Missing("branch.265250_valid_old_City");
            var second = PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(physical), "current.freedom_PersonE4"));
            if (deployedArmy != (second.Kind == 27)) throw Missing("branch.265250_CommanderArmy_scope");
            if (deployedArmy && (!K(current.ReadArmyUnitNativeValidity(new(second.Index)), "freedom_CommanderArmy_validity") ||
                current.ReadArmyUnitCommanderId(new(second.Index)) != personId)) throw Missing("branch.265250_not_effective_CommanderArmy");
            int cities = 0;
            for (int i = 0; i < 52; i++)
                if (K(current.ReadBaseForceSlot(current.ReadCityTableReference(i) ?? throw Missing("current.freedom_City_table")), "current.freedom_CityForce") == originalForce) cities++;
            if (cities != 1) throw Missing("branch.2851B0_not_last_old_City");
            RequireNoItems(current, (ushort)personId);
            var effects = new List<string> { "2851B0.last_old_City", "2EACB0.empty_owned_Item_iterator" };
            //Source233780(Person,3) short-circuits the raw2E comparison.
            //Status4 reaches that comparison, independently of its Core role.
            bool repairGovernor = resident && (status == 3 ||
                K(current.ReadArmyReturnBaseGovernor2E(oldHome), "current.freedom_old_Home2E") == personId);
            if (repairGovernor)
            {
                current.SetArmyReturnBaseGovernor2E(oldHome, 0);
                effects.Add("265357.old_Base2E_zero_bVar3");
            }
            //Ruler233780(Person,1) skips this prefix entirely. Neither status3
            //nor status4 reaches the separate status2 District callback.
            PersonAdvisorResetReceipt? advisorReset = null;
            current.AssignmentAdvisorResetCallbacks = resetForce =>
            {
                var reset = PersonAdvisorResetSource.ApplyCurrent(input, current, world.Pool, resetForce);
                input = reset.State ?? throw Missing(reset.UnresolvedOwner ?? "current.freedom_advisor_reset");
                advisorReset = reset.Receipt;
                effects.Add("29C430.cleared52→27BE40_morale→27AA40_level15E→2FF040_wall66");
                return true;
            };
            var appointments = PersonAssignmentPrecallSource.Apply29C430ForCapture(current, physical);
            if (appointments.NativeReturn is null) throw Missing(appointments.UnresolvedOwner ?? "current.freedom_29C430");
            input = PersonAssignmentPrecallInputs.PublishCurrent(input, current);
            var departments = PersonCaptureForceDepartments.ApplyCurrent(input, current, force, physical);
            input = departments.State ?? throw Missing(departments.UnresolvedOwner ?? "current.freedom_2E8160");
            effects.Add("2652xx.29C430_then2E8160");
            //29C430's reached player advisor writer updates the same World
            //flag as2E8160. An empty department loop must not discard it.
            if (playerPhysical == originalForce && current.ReadWorldTransferServiceFlags() is uint writtenFlags &&
                OwnedArmyReturnServiceInputs.ReadCurrent(input) != writtenFlags)
            {
                var simulation = input.NativeArmySimulation!;
                input = input with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                { Int32Values = new(simulation.Parameters.Int32Values)
                    { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)writtenFlags) } } } };
            }
            PersonCaptureWorldCandidatesReceipt? playerCandidates = null;
            if (playerPhysical == originalForce)
            {
                var removed = PersonCaptureWorldCandidates.ApplyCurrent(input, current, physical,
                    PersonCaptureCandidatesPresentationAbsent.IndependentClient);
                input = removed.State ?? throw Missing(removed.UnresolvedOwner ?? "current.freedom_29CCB0");
                playerCandidates = removed.Receipt;
                effects.Add("2653xx.29CCB0_player_selected_Person_clear_after_departments");
            }
            short fame = K(current.ReadRulerReleasePopularity1BE(originalForce), "current.freedom_Force1BE");
            int penalty = K(ArmyDestructionParameterSeed.ReadCurrent(input, SimulationParameterKeys.ArmyDestruction.PersonFreedomPopularityPenalty), "current.freedom_popularity_parameter");
            fame = (short)Math.Clamp(unchecked(fame - penalty), -1000, 1000);
            current.SetRulerReleasePopularity1BE(originalForce, fame);
            var native = input.NativeArmySimulation!; var forceState = native.Forces[originalForce];
            input = input with { NativeArmySimulation = native with { Forces = new(native.Forces) { [originalForce] = forceState with
                { NormalAftermath = forceState.NormalAftermath! with { Development1BE = fame } } } } };
            effects.Add("265488.223D40_original_Force_popularity");
            var auxiliaries = current.ReadPersonDestructionRuntimeFields(physical) ?? throw Missing("current.freedom_Person160_owner");
            current.SetPersonDestructionRuntimeFields(physical, auxiliaries with { Byte19D = 0 });
            current.WritePersonStatus(physical, 5);
            PersonDistrictTransfer.Apply(physical, 0, new Transfer(current));
            current.WritePersonFirstLocation(physical, 0);
            effects.Add("26548D.165_zero→241D30.status5→240DB0.District0→240A30.Home0");
            var selected = homeSource.Resolve(input, current, physical, oldHome,
                package.ReadArmyDestructionSampler(input, current) ?? throw Missing("current.freedom_sampler_world"));
            if (selected.NativeReturn != 1 || selected.Area?.PhysicalSlot is not int areaPhysical)
                throw Missing(selected.UnresolvedOwner ?? "branch.265250_no_City_or_Area_candidate");
            effects.Add("2655xx.22F430_shuffle254F70_then_AreaB0_shuffle");
            ArmyCause4Receipt? armyCleanup = null;
            if (deployedArmy)
            {
                var destroyed = new OwnedArmyDestructionStage(package).ApplyFreeCommanderCause4Current(input, current, world.Pool, second.Index);
                input = destroyed.State ?? throw Missing(destroyed.UnresolvedOwner ?? "current.freedom_direct_cause4");
                armyCleanup = destroyed.Receipt;
                effects.Add("265C14.direct259360_mode0_cause4_actual_free_Commander");
            }
            //22AC00→228890 is reached after the optional direct Army
            //destructor; removing a Return member does not retire its receiver.
            var detached = PersonReturnCommands.DetachPersonFromFirstAssociated(input, current, physical);
            if (detached.Owner is null || detached.Receipt is null) throw Missing(detached.UnresolvedOwner ?? "current.freedom_228890");
            effects.Add("265C1C.22AC00→228890_member_only_detachment");
            current.WritePersonSecondLocation(physical, (ushort)Math.Clamp(areaPhysical + 561, 0, 1061));
            byte areaCity = K(current.ReadAreaCitySlot(selected.Area), "current.freedom_selected_Area35");
            var finalHome = current.ReadCityTableReference(areaCity < 52 ? areaCity : 0) ?? throw Missing("current.freedom_final_City_pointer");
            if (!K(current.ReadBaseNativeValidity(finalHome), "current.freedom_Area35_City_validity"))
            {
                byte originalCity = K(current.ReadAreaValidityCode(selected.Area), "current.freedom_selected_Area34");
                finalHome = current.ReadCityTableReference(originalCity < 52 ? originalCity : 0) ?? throw Missing("current.freedom_Area34_City_pointer");
            }
            ushort destination = (ushort)Math.Min(K(current.ReadBaseObjectLink(finalHome), "current.freedom_final_City4E"), (ushort)61);
            current.WritePersonFirstLocation(physical, destination);
            current.SetPersonDestructionRuntimeFields(physical, auxiliaries with { Byte19D = 0, Byte1A0 = 0 });
            byte loyalty = OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(physical)) == false ? (byte)0 : (byte)255;
            current.SetPersonDestructionLoyaltyE8(physical, loyalty);
            int? selector = BattleRemakeCurrentSettings.ReadSelectorE58(input);
            int? option = selector is int currentSelector
                ? BattleRemakeCurrentSettings.ReadInt32(input, currentSelector == 0 ? 0x4C4 : 0x128) : null;
            if (option is null) throw Missing("current.freedom_3EAF80_SurrenderedRank");
            var ranks = source.Snapshot.CreditRankParameters?.Ranks;
            if (ranks is not { Count: 7 }) throw Missing("current.freedom_CCreditRank_word30");
            short merit = (short)Math.Clamp((int)ranks[0].SignedThreshold, ranks[option == 1 ? 2 : 1].SignedThreshold, ranks[6].SignedThreshold);
            //The prefix reached no Primary callback. Publish only its actual
            //Person and appointment/department writes before the optional
            //governor tail; reading
            //all unrelated City raw Districts would add an unneeded dependency.
            var result = PersonAssignmentPrecallInputs.PublishCurrent(input, current);
            person = result.Officers[personId];
            var values = new Dictionary<string, int>(result.NativeArmySimulation!.Parameters.Int32Values)
                { [OwnedArmyGovernorComparatorInputs.PersonRawE8Key(physical)] = loyalty,
                    [OwnedArmyGovernorComparatorInputs.PersonWord166Key(physical)] = 0 };
            result = result with
            {
                Officers = new(result.Officers) { [personId] = person with
                {
                    ForceId = null, DistrictId = null, Role = OfficerRole.Free, Appointment = OfficerAppointment.Synchronize(73), Loyalty = loyalty <= 100 ? loyalty : null,
                    LocationId = destination,
                    NativeLocationRuntime = person.NativeLocationRuntime! with { District118 = 0,
                        Second11C = K(current.ReadPersonSecondLocation(physical), "current.freedom_final_PersonE4") },
                    ServiceStatus = OfficerServiceStatus.Free,
                    Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(person.Behavior, OfficerBehaviorSourceAdapter.ClearFreedomState(flags)),
                    MeritState = OfficerMeritRules.ResetAfterRelease(merit),
                    NativeDestructionRuntime = auxiliaries with { Byte19D = 0, Byte1A0 = 0 }
                } },
                Cities = new(result.Cities) { [captured.Id] = result.Cities[captured.Id] with
                    { ForceId = captured.ForceId, DistrictId = captured.DistrictId, GovernorId = captured.GovernorId } },
                NativeArmySimulation = result.NativeArmySimulation with { Parameters = result.NativeArmySimulation.Parameters with { Int32Values = values } },
                PersonnelReturns = detached.Owner,
                NativeArmyLifecycle = result.NativeArmyLifecycle! with { Indexes = world.Pool.ExportOwnedState() }
            };
            effects.Add("265C50.Area_E4→Home_E2→Office73_Loyalty0_or255→actual_raw_resets");
            ArmyReturnGovernorPacket? governorRepair = null;
            if (repairGovernor && K(current.ReadArmyReturnBaseGovernor2E(oldHome), "current.freedom_old_Home2E_tail") == 0 &&
                K(current.ReadBaseNativeValidity(oldHome), "current.freedom_old_Home_validity_tail"))
            {
                governorRepair = package.ApplyPersonFreedomGovernorCurrent(result, current, oldHome);
                var officers = new Dictionary<int, OfficerState>(result.Officers);
                foreach (var write in governorRepair.PersonWrites)
                {
                    ushort id = K(current.ReadPersonIdentity(write.PersonPhysical), "current.freedom_reselected_Governor_ID");
                    var changed = officers[id];
                    officers[id] = changed with { ServiceStatus = OfficerServiceStatusSourceAdapter.Decode(write.StatusAfter),
                        Role = write.StatusAfter == 3 ? OfficerRole.Governor : OfficerRole.Officer };
                }
                var city = result.Cities[captured.Id];
                result = result with { Officers = officers, Cities = new(result.Cities) { [captured.Id] = city with
                    { NativeReturnRuntime = city.NativeReturnRuntime! with { Governor2E = governorRepair.GovernorWord2EAfter } } },
                    NativeArmyLifecycle = result.NativeArmyLifecycle! with { Indexes = world.Pool.ExportOwnedState() } };
                effects.Add("265DBx.old_Base2E_still0_valid→22ACF0→282C10→26D090_actual_Governor");
            }
            StateValidator.Validate(result);
            if (deployedArmy)
            {
                result = package.ConsumeArmyLifecyclePending(result) ?? throw Missing("current.freedom_28E6D0_pending_cleanup");
                effects.Add("owned.cleanup_window.28E6D0_after_complete_free_Person");
                StateValidator.Validate(result);
            }
            return new(result, new(personId, physical, originalForce, first, destination, areaPhysical,
                merit, loyalty, detached.Receipt, effects) { ArmyCleanup = armyCleanup, GovernorRepair = governorRepair,
                    AdvisorReset = advisorReset, PlayerCandidates = playerCandidates }, null);
        }
        catch (InvalidDataException unknown) { return new(null, null, unknown.Message); }
    }

    private static void RequireNoItems(SourceWorldContext current, ushort id)
    {
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Item) ?? throw Missing("current.freedom_Item_manager");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("current.freedom_Item_head");
        while (node.Identity.HasValue)
        {
            var item = current.ReadRegistryNodePayload(node) ?? throw Missing("current.freedom_Item_payload");
            if (item.Kind != RegistryKind.Item || item.PhysicalSlot is not int physical) throw Missing("current.freedom_Item_pointer");
            if (K(current.ReadCaptureItemOwner4A(physical), "current.freedom_Item4A") == id) throw Missing("branch.265250_owned_Item324580");
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("current.freedom_Item_Next");
        }
    }
    private sealed class Transfer(SourceWorldContext current) : IPersonDistrictTransferState
    {
        public byte? ReadPersonDistrictSlot(int p) => current.ReadPersonDistrictSlot(p);
        public ForceReference? ReadPersonForce(int p) => current.ReadPersonForce(p);
        public ForceReference? ReadRequestedDistrictForce(int d) => current.ReadRequestedDistrictForce(d);
        public int? ReadForcePhysicalSlot(ForceReference f) => current.ReadForcePhysicalSlot(f);
        public void WritePersonResetWord1FF(int p, ushort v) => current.WritePersonResetWord1FF(p, v);
        public void WritePersonResetByte1FE(int p, byte v) => current.WritePersonResetByte1FE(p, v);
        public void WritePersonDistrictSlot(int p, byte v) => current.WritePersonDistrictSlot(p, v);
        public byte? ReadDistrictForceWithoutValidity(int d) => current.ReadDistrictForceWithoutValidity(d);
        public ForceReference? ReadCurrentPlayerForce() => current.ReadCurrentPlayerForce();
        public void RefreshTransferService(int a, int b, int c) => throw Missing("branch.freedom_District0_service_not_reached");
        public void RemoveForcePersonAssociation(ForceReference f, int p)
        {
            var result = PersonAssignmentPrecallSource.Apply2783B0ForCapture(current, f, p);
            if (result.NativeReturn is null) throw Missing(result.UnresolvedOwner ?? "current.freedom_2783B0");
        }
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string owner) => new(owner);
}
