using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private PersonFreedomHomeSource? _captiveFreedomHomes;
    internal const string CaptiveResentmentRetentionPercentKey = "simulation.person-captive-release.resentment-retention-percent";

    //Only2E7890's initial no-origin-City branch adjusts resentment. Later
    //2EA9A0 ownership failures enter265250 directly through the other API.
    internal GameState ApplyCaptiveNoHomeFreedomCurrent(GameState input, SourceWorldContext current,
        WorldIndexContext pool, int personPhysical, NativeMapSeasonDate? sourceDate,
        Func<int> readDay450, Func<TacticSourceSamplerWorld> readSampler, List<CampaignEffectRequest> sink)
    {
        var target = current.ReadResentmentTarget(personPhysical) ?? throw CaptiveFreedomMissing("resentment162_lookup");
        var working = input;
        if (target.PhysicalSlot is int targetPhysical && CaptiveFreedomNeed(current.ReadPersonNativeValidity(targetPhysical), "resentment_target_validity"))
        {
            int disposition = CaptiveFreedomNeed(current.ReadAllegianceDisposition(personPhysical), "disposition16C");
            if (disposition < 10)
            {
                var before = current.ReadOfficerAftermath(personPhysical) ?? throw CaptiveFreedomMissing("resentment_owner");
                int personId = CaptiveFreedomNeed(current.ReadPersonIdentity(personPhysical), "resentment_Person10");
                if (disposition < -9)
                    current.SetOfficerAftermath(personPhysical, before with { Resentment = new(0, 0),
                        SourceKnowledge = (before.SourceKnowledge ?? new(personId, null, null, null)) with { UnmappedResentmentTarget = null } });
                else
                {
                    //TC141A4A9B8 is an independent signed i32. No trusted
                    //fresh initializer/current value is installed here.
                    var parameters = working.NativeArmySimulation?.Parameters.Int32Values;
                    if (parameters is null || !parameters.TryGetValue(CaptiveResentmentRetentionPercentKey, out int percent))
                        throw CaptiveFreedomMissing("current_parameter_" + CaptiveResentmentRetentionPercentKey + "_TC141A4A9B8");
                    int intensity = CaptiveFreedomNeed(before.Resentment?.Intensity, "resentment164");
                    if ((uint)intensity > 255) throw CaptiveFreedomMissing("resentment164_width");
                    int adjusted = Math.Clamp(Math.Max(1, unchecked(intensity * percent) / 100), 1, 255);
                    current.SetOfficerAftermath(personPhysical, before with { Resentment =
                        new(before.Resentment?.TargetOfficerId, adjusted) });
                }
                working = current.PublishOfficerAftermath(working, personId);
            }
        }
        return ApplyCaptiveFreedomCurrent(working, current, pool, personPhysical, sourceDate, readDay450, readSampler, sink);
    }

    //265250(Person,null,null), ordinary service-loss and captive branches.
    //The outer operation owns the live pool, final Export and pending queue.
    internal GameState ApplyCaptiveFreedomCurrent(GameState input, SourceWorldContext current,
        WorldIndexContext pool, int personPhysical, NativeMapSeasonDate? sourceDate,
        Func<int> readDay450, Func<TacticSourceSamplerWorld> readSampler, List<CampaignEffectRequest> sink,
        TransferPersonReference? formerRuler = null, BaseReference? preferredHome = null)
    {
        T Need<T>(T? value, string field) where T : struct => value ?? throw CaptiveFreedomMissing(field);
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw CaptiveFreedomMissing("current_pool_binding");
        var working = input;
        ushort id = Need(current.ReadPersonIdentity(personPhysical), "Person10");
        ushort first = Need(current.ReadPersonFirstLocation(personPhysical), "old_E2");
        var oldHome = BaseLookup.Find(first, current);
        var oldArea = PersonFreedomHomeSource.FindBaseArea229FD0(current, oldHome);
        var originalForce = current.ReadPersonForce(personPhysical) ?? throw CaptiveFreedomMissing("original_Force22CB40");
        byte status = Need(current.ReadPersonStatus(personPhysical), "prefix_statusE6");
        //265250 has no enclosing status filter: Story238 may reach the
        //same ordinary body for free, absent or deceased stored Persons.
        //Only the actual leader/ruler/governor prefix below is conditional.
        if (status == 2)
        {
            int districtId = Need(current.ReadPersonDistrictSlot(personPhysical), "leader_DistrictE0");
            var district = current.ReadDistrictTableReference(districtId <= 51 ? districtId : 0)
                ?? throw CaptiveFreedomMissing("leader_District_pointer");
            int physical = Need(district.PhysicalSlot, "leader_District_physical");
            current.WriteDistrictLeader(physical, 0);
            current.WritePersonStatus(personPhysical, 5);
            var gates = new HashSet<int>();
            var operation = new ArmyReturnDistrictOperationQueries(
                new PersonReturnGovernorQueries(working, _governorInputs, current), () => working,
                value => working = value, (person, result) => working = ApplyPersonAssociationRemovalReports(
                    working, current, person, result, readDay450, sink), current, gates, this, sourceDate);
            var maintenance = ArmyReturnPrimaryDistrictUpdater.ApplyCurrent(district, current, operation);
            if (maintenance.Packet is null)
                throw CaptiveFreedomMissing(maintenance.UnresolvedOwner ?? "leader_2E9140");
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
            working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current,
                operation.ChangedAreas, operation.ChangedGates);
            current.WritePersonStatus(personPhysical, 4);
            status = 4;
        }
        //265250 excludes the ruler from both governor-prefix tests.
        bool repairGovernor = status != 1 && (status == 3 || Need(current.ReadArmyReturnBaseGovernor2E(oldHome), "old_Home2E") == id);
        if (repairGovernor) { current.SetArmyReturnBaseGovernor2E(oldHome, 0); PublishGovernor(0); }
        if (Need(current.ReadForceNativeValidity(originalForce), "original_Force_validity"))
        {
            if (Need(current.ReadBaseNativeValidity(oldHome), "old_Home_validity"))
            {
                var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
                var oldAreaRemoval = current.AssignmentAreaOfficerRemoval;
                current.AssignmentAdvisorResetCallbacks = force =>
                {
                    var reset = PersonAdvisorResetSource.ApplyWithinPersonCapture(working, current, pool, force);
                    working = reset.State ?? throw CaptiveFreedomMissing(reset.UnresolvedOwner ?? "advisor_reset");
                    BindPolicy(); return true;
                };
                current.AssignmentAreaOfficerRemoval = (area, oldId) =>
                {
                    var removed = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, oldId);
                    if (removed.NativeReturn is null) throw CaptiveFreedomMissing(removed.UnresolvedOwner ?? "area_assignment_removal");
                    if (removed.NativeReturn != 0 && removed.PersonPhysical is int touched)
                        working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, touched, [], publishVector: true);
                    return removed.NativeReturn != 0;
                };
                try
                {
                    var jobs = PersonAssignmentPrecallSource.Apply29C430ForCapture(current, personPhysical);
                    if (jobs.NativeReturn is null) throw CaptiveFreedomMissing(jobs.UnresolvedOwner ?? "29C430");
                    working = ApplyPersonAssociationRemovalReports(working, current, personPhysical, jobs, readDay450, sink);
                    working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, personPhysical, [jobs]);
                }
                finally { current.AssignmentAdvisorResetCallbacks = oldAdvisor; current.AssignmentAreaOfficerRemoval = oldAreaRemoval; BindPolicy(); }
            }
            var departments = PersonCaptureForceDepartments.ApplyCurrent(working, current, originalForce, personPhysical);
            working = departments.State ?? throw CaptiveFreedomMissing(departments.UnresolvedOwner ?? "2E8160"); BindPolicy();
            if (IsPlayer(originalForce))
            {
                var removed = CommanderSelectionSource.RemoveCurrentOfficer(working, current, personPhysical);
                working = removed.State ?? throw CaptiveFreedomMissing(removed.UnresolvedOwner ?? "29CCB0");
            }
        }
        ushort flags = Need(current.ReadArmyReturnPersonFlags196(personPhysical), "Person15E");
        if ((flags & 0x4000) != 0)
            return ApplySpecialCaptiveFreedomTailCurrent(working, current, pool, personPhysical,
                sourceDate, readDay450, readSampler, sink);
        if (Need(current.ReadForceNativeValidity(originalForce), "popularity_Force_validity"))
        {
            int firstForce = Need(current.ReadForcePhysicalSlot(originalForce), "popularity_Force_first_ordinal");
            if (firstForce != 0)
            {
                int secondForce = Need(current.ReadForcePhysicalSlot(originalForce), "popularity_Force_second_ordinal");
                if (secondForce != 51 && unchecked((uint)(secondForce - 46)) > 4)
                {
                    int penalty = Need(ArmyDestructionParameterSeed.ReadCurrent(working,
                        SimulationParameterKeys.ArmyDestruction.PersonFreedomPopularityPenalty), "popularity_penalty_current");
                    working = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(working, current, secondForce, unchecked(-penalty));
                }
            }
        }
        current.WriteFormerForceKnowledge(personPhysical, 0);
        current.WritePersonStatus(personPhysical, 5);
        var associations = new List<PersonAssignmentPrecallResult>();
        PersonDistrictTransfer.Apply(personPhysical, 0,
            ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, associations, result =>
                working = ApplyPersonAssociationRemovalReports(working, current, personPhysical, result, readDay450, sink)));
        if (associations.Count != 0)
            working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, personPhysical, associations);
        current.WritePersonFirstLocation(personPhysical, 0);
        working = PublishPerson(working);
        working = current.PublishOfficerAftermath(working, id);
        //265250's nonnull placement argument selects that Base's actual
        //representative Area directly, without City/Area shuffle draws.
        var selected = preferredHome is not null && Need(current.ReadBaseNativeValidity(preferredHome), "preferred_Home_validity")
            ? new PersonFreedomHomeResult(1, null, PersonFreedomHomeSource.FindBaseArea229FD0(current, preferredHome), null, null)
            : (_captiveFreedomHomes ??= new(_scenario)).ResolveBorrowed(working, current,
                personPhysical, oldHome, oldArea, readSampler);
        if (selected.NativeReturn == 0) return working; //Real prefix survives the no-City return0.
        if (selected.NativeReturn != 1 || selected.Area?.PhysicalSlot is not int areaPhysical)
            throw CaptiveFreedomMissing(selected.UnresolvedOwner ?? "null_placement_City_or_Area");
        working = PersonFreedomSource.DropFreedomItems(working, current, personPhysical, areaPhysical);
        //22A200(Person,1) requires the actual Army12 identity. A captive's
        //custodian is therefore not mistaken for the released person's Army.
        ushort second = Need(current.ReadPersonSecondLocation(personPhysical), "own_Army_E4");
        var army = current.ReadArmyUnitTableReference(second is >= 62 and <= 561 ? second - 61 : 0)
            ?? throw CaptiveFreedomMissing("own_Army_lookup");
        if (Need(current.ReadArmyUnitCommanderId(army), "own_Army12") != id)
            army = current.ReadArmyUnitTableReference(0) ?? throw CaptiveFreedomMissing("own_Army_fallback");
        if (Need(current.ReadArmyUnitNativeValidity(army), "own_Army_validity") &&
            !Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, Need(army.PhysicalSlot, "own_Army_pointer")), "own_Army233560"))
        {
            var destroyed = new OwnedArmyDestructionStage(this).ApplyFreeCommanderCause4WithinOperationCurrent(
                working, current, pool, Need(army.PhysicalSlot, "own_Army_pointer"), sourceDate, sink);
            working = destroyed.State ?? throw CaptiveFreedomMissing(destroyed.UnresolvedOwner ?? "own_Commander_cause4");
        }
        var linked = current.ReadPersonLinkedEntity(personPhysical) ?? throw CaptiveFreedomMissing("associated_receiver");
        if (Need(current.ReadLinkedEntityNativeValidity(linked), "associated_receiver_validity"))
        {
            var detached = PersonReturnCommands.DetachPersonFromFirstAssociated(working, current, personPhysical);
            working = working with { PersonnelCommands = detached.Owner ?? throw CaptiveFreedomMissing(detached.UnresolvedOwner ?? "228890_member_detachment") };
        }
        current.WritePersonSecondLocation(personPhysical, (ushort)Math.Clamp(areaPhysical + 561, 0, 1061));
        byte rawCity = Need(current.ReadAreaCitySlot(selected.Area), "selected_Area35");
        var home = current.ReadCityTableReference(rawCity <= 51 ? rawCity : 0) ?? throw CaptiveFreedomMissing("final_City_lookup");
        if (!Need(current.ReadBaseNativeValidity(home), "final_City_validity"))
        {
            byte fallback = Need(current.ReadAreaValidityCode(selected.Area), "selected_Area34");
            home = current.ReadCityTableReference(fallback <= 51 ? fallback : 0) ?? throw CaptiveFreedomMissing("fallback_City_lookup");
        }
        //240A30(Person,5,22DC80(City)) writes the City ordinal, not Base4E.
        int destination = Need(home.PhysicalSlot, "final_City22DC80");
        current.WritePersonFirstLocation(personPhysical, (ushort)(destination < 52 ? destination : 0));
        current.SetMoraleWavePersonPostE7(personPhysical, 73);
        current.SetPersonDestructionLoyaltyE8(personPhysical, 0);
        ushort loyaltyFlags = Need(current.ReadArmyReturnPersonFlags196(personPhysical), "loyalty_Person15E");
        byte loyalty = (loyaltyFlags & 0x2000) != 0 ? (byte)255 : (byte)0;
        current.SetPersonDestructionLoyaltyE8(personPhysical, loyalty);
        int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(working), "rank_selectorE58");
        int option = Need(BattleRemakeCurrentSettings.ReadInt32(working, selector == 0 ? 0x4C4 : 0x128), "rank_SurrenderedRank");
        var ranks = _scenario.Snapshot.CreditRankParameters?.Ranks;
        if (ranks is not { Count: 7 }) throw CaptiveFreedomMissing("rank_CCreditRank_word30");
        int initial = ranks[0].SignedThreshold, lower = ranks[option == 1 ? 2 : 1].SignedThreshold;
        short merit = (short)(initial < lower ? lower : Math.Min(initial, ranks[6].SignedThreshold));
        ushort finalFlags = Need(current.ReadArmyReturnPersonFlags196(personPhysical), "reset_Person15E");
        current.WriteArmyReturnPersonFlags196(personPhysical, OfficerBehaviorSourceAdapter.ClearFreedomState(finalFlags));
        current.ClearCaptureResetKnowledge(personPhysical);
        var native = working.NativeArmySimulation ?? throw CaptiveFreedomMissing("reset_governor_owner");
        if (native.Parameters.Int32Values.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) != 1)
            throw CaptiveFreedomMissing("reset_governor_version");
        working = working with { NativeArmySimulation = native with { Parameters = native.Parameters with
            { Int32Values = new(native.Parameters.Int32Values) { [OwnedArmyGovernorComparatorInputs.PersonWord166Key(personPhysical)] = 0 } } },
            Officers = new(working.Officers) { [id] = working.Officers[id] with
            { Appointment = OfficerAppointment.Synchronize(73), MeritState = OfficerMeritRules.ResetAfterRelease(merit) } } };
        working = ArmyDestructionLoyalty.PublishCurrentRawE8(working, current, personPhysical, loyalty);
        working = PublishPerson(working);
        if (formerRuler?.PhysicalSlot is int former && Need(current.ReadPersonNativeValidity(former), "former_ruler_validity"))
        {
            var aftermath = current.ReadOfficerAftermath(personPhysical) ?? throw CaptiveFreedomMissing("former_ruler_resentment_owner");
            current.SetOfficerAftermath(personPhysical, aftermath with
            { Resentment = new(Math.Clamp((int)Need(current.ReadPersonIdentity(former), "former_ruler_identity"), 0, 6000), 12) });
        }
        working = current.PublishOfficerAftermath(working, id);
        if (repairGovernor && Need(current.ReadArmyReturnBaseGovernor2E(oldHome), "tail_old_Home2E") == 0 &&
            Need(current.ReadBaseNativeValidity(oldHome), "tail_old_Home_validity"))
        {
            var governor = ApplyPersonFreedomGovernorCurrent(working, current, oldHome);
            foreach (var write in governor.PersonWrites)
            {
                int changedId = Need(current.ReadPersonIdentity(write.PersonPhysical), "tail_Governor_Person10");
                var changed = working.Officers[changedId];
                var changedStatus = OfficerServiceStatusSourceAdapter.Decode(write.StatusAfter);
                working = working with { Officers = new(working.Officers) { [changedId] = changed with
                { ServiceStatus = changedStatus, Role = OfficerServiceRules.PublishedRole(changedStatus, changed.Role) } } };
            }
            PublishGovernor(governor.GovernorWord2EAfter);
        }
        return working;

        bool IsPlayer(ForceReference force)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current) ?? throw CaptiveFreedomMissing("Player_Force");
            int slot = Need(current.ReadForcePhysicalSlot(player), "Player_Force_ordinal");
            return slot != 0 && slot == Need(current.ReadForcePhysicalSlot(force), "compared_Force_ordinal");
        }
        void BindPolicy() => current.BaseForcePolicyEffectReader = (force, effect) =>
            ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;
        void PublishGovernor(ushort governor)
        {
            int at = Need(oldHome.PhysicalSlot, "governor_old_Home_pointer");
            if (oldHome.Kind == BaseKind.City && working.Cities.TryGetValue(at, out var city))
                working = working with { Cities = new(working.Cities) { [at] = city with
                    { GovernorId = governor == 0 ? null : governor, NativeReturnRuntime =
                        (city.NativeReturnRuntime ?? throw CaptiveFreedomMissing("governor_City_runtime")) with { Governor2E = governor } } } };
            else if (oldHome.Kind == BaseKind.Gate && working.Gates.TryGetValue(at + 51, out var gate))
            {
                var simulation = working.NativeArmySimulation ?? throw CaptiveFreedomMissing("governor_Gate_owner");
                working = working with { Gates = new(working.Gates) { [gate.Id] = gate with { GovernorId = governor == 0 ? null : governor } },
                    NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = new(simulation.Parameters.Int32Values)
                        { [OwnedArmyLifecycleDetachedInputs.Key("Gate", at, "2E")] = governor } } } };
            }
            else throw CaptiveFreedomMissing("governor_saved_Base");
        }
        GameState PublishPerson(GameState state)
        {
            var person = state.Officers.GetValueOrDefault(id) ?? throw CaptiveFreedomMissing("publish_Person");
            var native = state.NativeArmySimulation ?? throw CaptiveFreedomMissing("publish_detached_owner");
            if (native.Parameters.Int32Values.GetValueOrDefault(OwnedArmyLifecycleDetachedInputs.VersionKey) != 1)
                throw CaptiveFreedomMissing("publish_detached_version");
            ushort home = Need(current.ReadPersonFirstLocation(personPhysical), "publish_E2");
            byte district = Need(current.ReadPersonDistrictSlot(personPhysical), "publish_E0");
            var status = OfficerServiceStatusSourceAdapter.Decode(Need(current.ReadPersonStatus(personPhysical), "publish_E6"));
            var location = current.ReadOfficerLocation(id) ?? throw CaptiveFreedomMissing("publish_location");
            var result = state with { Officers = new(state.Officers) { [id] = person with
                { LocationId = home == 0 ? null : home, DistrictId = null, ForceId = null,
                    LocationState = location, ServiceStatus = status, Role = OfficerServiceRules.PublishedRole(status, person.Role),
                    Behavior = current.ReadPersonBehavior(personPhysical) } },
                NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = new(native.Parameters.Int32Values)
                    { [OwnedArmyLifecycleDetachedInputs.Key("Person", personPhysical, "E2")] = home,
                      [OwnedArmyLifecycleDetachedInputs.Key("Person", personPhysical, "E0")] = district } } } };
            return OfficerLocationSourceAdapter.PublishPosition(result, id, current);
        }
    }
    private static T CaptiveFreedomNeed<T>(T? value, string field) where T : struct => value ?? throw CaptiveFreedomMissing(field);
    private static InvalidDataException CaptiveFreedomMissing(string field) => new("person-captive-freedom." + field);
}
