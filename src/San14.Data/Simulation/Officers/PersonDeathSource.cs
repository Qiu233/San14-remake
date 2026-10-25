using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary, duel and execution death on the enclosing operation's
    /// actual current world and pool. Failure discards the candidate.</summary>
    internal sealed partial class PersonDeathSource
    {
        private readonly ArmyCampaignRuleAdapter package;
        private GameState latest;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        private readonly Action<string, GameState>? observe;
        private readonly List<PersonAssignmentPrecallResult> associations = [];
        private int deathCause;
        private TransferPersonReference killer = new(null);
        private bool retireAssociatedArmy = true;
        private bool emitReports = true;
        private bool emitHistory = true;
        private bool suppressAllReports;
        private ForceReference? deathOriginalForce;

        internal PersonDeathSource(ArmyCampaignRuleAdapter package, GameState state,
            SourceWorldContext current, WorldIndexContext pool, Action<string, GameState>? observe = null)
        {
            this.package = package; latest = state; this.current = current; this.pool = pool; this.observe = observe;
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("current_pool_binding");
            package._personRelationInputs.BindCurrent(() => latest, current);
            current.MoraleWavePersonPostE7Reader = physical =>
                OwnedArmyMoraleWaveInputs.ReadPersonPostE7(latest, current, physical);
            current.RulerReleasePopularity1BEReader = force =>
                latest.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.NormalAftermath?.Development1BE;
            BindLatestPolicyReader();
        }

        internal (GameState State, int? NativeReturn) ApplyAnnualOrdinary(int person)
            => ApplyOrdinary(person);

        /// <summary>295EE0's ordinary battle, duel or execution caller, with its actual second Person
        /// receiver. The duel caller already owns Army destruction (argument4=0).
        /// Execution has argument4=1 and retains the ordinary associated-Army gate.</summary>
        internal (GameState State, int? NativeReturn) ApplyCombatDeath(int person,
            TransferPersonReference killingPerson, int cause, bool destroyAssociatedArmy,
            bool report = true, bool history = true, bool suppressReports = false)
        {
            if (cause is not (0 or 1 or 2)) throw new ArgumentOutOfRangeException(nameof(cause));
            deathCause = cause;
            killer = killingPerson;
            retireAssociatedArmy = destroyAssociatedArmy;
            emitReports = report;
            emitHistory = history;
            suppressAllReports = suppressReports;
            return ApplyOrdinary(person);
        }

        private (GameState State, int? NativeReturn) ApplyOrdinary(int person)
        {
            try
            {
                ushort first = Known(current.ReadPersonFirstLocation(person), "captured_E2");
                var oldBase = BaseLookup.Find(first, current);
                deathOriginalCity = CurrentGateMapQueries.FindCity(oldBase, current); //22A940 precedes reports.
                var oldPosition = Position(person);
                byte capturedStatus = Status(person);
                int office = Known(current.ReadMoraleWavePersonPostE7(person), "captured_office_E7");
                var oldForce = Force(person);
                deathOriginalForce = oldForce;
                ushort bc = Relation(person, 0xBC);
                var capturedBC = (bc <= 6000 ? current.ReadPersonLookupReference(bc) : current.ReadTransferPersonTableReference(0))
                    ?? throw Missing("captured_BC_lookup");

                int stage = 0;
                if (Status(person) != 1 && !BandPersonAtOrdinaryBase(person) &&
                    (Known(current.ReadOwnedReturnPersonFlags(person), "captured_flags15E") & 0x4000) == 0)
                    stage = OfficerMeritSourceAdapter.EncodeStage(Owner(person).MeritState) ?? throw Missing("captured_rank_stage");
                bool reports = Status(person) == 1;
                if (Known(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(latest, office), "office_validity") &&
                    Known(OwnedArmyMoraleWaveInputs.ReadPost37(latest, office), "office_rank37") < Parameter("office-report-rank-threshold"))
                    reports = true;
                if (Known(current.ReadForceNativeValidity(oldForce), "captured_force_validity") && IsPlayer(oldForce)) reports = true;
                if (Status(person) == 6)
                {
                    //22CB10 is a reached current Hex-to-Force lookup, not the
                    //Person's employment Force. No owned report adapter yet.
                    byte slot = Known(current.ReadHexForceSlot(Position(person)), "captive_position_force");
                    var positionForce = current.ReadForceTableReference(slot <= 51 ? slot : 0) ?? throw Missing("captive_position_force_lookup");
                    if (IsPlayer(positionForce)) reports = true;
                }
                if (reports && !suppressAllReports) ApplyInitialReports(person, capturedStatus, office, oldForce, oldPosition);
                if (Serving(person) && Known(current.ReadForceNativeValidity(oldForce), "job_force_validity"))
                {
                    var prior = current.AssignmentAdvisorResetCallbacks;
                    var priorRemoval = current.AssignmentAreaOfficerRemoval;
                    current.AssignmentAdvisorResetCallbacks = force =>
                    {
                        var reset = PersonAdvisorResetSource.ApplyWithinPersonDeath(latest, current, pool, force);
                        latest = reset.State ?? throw Missing(reset.UnresolvedOwner ?? "advisor_refresh");
                        BindLatestPolicyReader();
                        return true;
                    };
                    current.AssignmentAreaOfficerRemoval = (area, old) =>
                    {
                        var removal = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, old);
                        if (removal.NativeReturn is null) throw Missing(removal.UnresolvedOwner ?? "area_removal");
                        if (removal.NativeReturn != 0 && removal.PersonPhysical is int touched)
                            latest = PersonAssignmentPrecallInputs.PublishDeathTouched(latest, current, touched, [], publishVector: true);
                        return removal.NativeReturn != 0;
                    };
                    try
                    {
                        var jobs = PersonAssignmentPrecallSource.Apply29C430ForCapture(current, person);
                        RequireReports(jobs);
                        if (jobs.NativeReturn is null) throw Missing(jobs.UnresolvedOwner ?? "job_reset");
                        latest = PersonAssignmentPrecallInputs.PublishDeathTouched(latest, current, person, [jobs]);
                    }
                    finally
                    {
                        current.AssignmentAdvisorResetCallbacks = prior;
                        current.AssignmentAreaOfficerRemoval = priorRemoval;
                        BindLatestPolicyReader();
                    }
                    var departments = PersonCaptureForceDepartments.ApplyCurrent(latest, current, oldForce, person);
                    latest = departments.State ?? throw Missing(departments.UnresolvedOwner ?? "force_departments");
                    if (IsPlayer(oldForce))
                    {
                        var selection = CommanderSelectionSource.RemoveCurrentOfficer(latest, current, person);
                        latest = selection.State ?? throw Missing(selection.UnresolvedOwner ?? "player_candidates");
                    }
                    Observe("death.jobs-cleared");
                }

                var army = PersonArmy(person);
                if (Known(current.ReadArmyUnitNativeValidity(army), "associated_army_validity") &&
                    !Known(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(latest, current, Known(army.PhysicalSlot, "army_pointer")), "associated_army_excluded") &&
                    Known(current.ReadArmyUnitCommanderId(army), "associated_army_commander") == Identity(person) && retireAssociatedArmy)
                {
                    var effects = new List<CampaignEffectRequest>();
                    var destroyed = new OwnedArmyDestructionStage(package).ApplyDeathCommanderCause4Current(
                        latest, current, pool, Known(army.PhysicalSlot, "death_Army_pointer"),
                        latest.NativeMapSeason?.ThirdDate ?? throw Missing("death_Army_date"), effects);
                    latest = destroyed.State ?? throw Missing(destroyed.UnresolvedOwner ?? "death_Army_destruction");
                    if (effects.Count != 0)
                        latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                            ?? throw Missing("death_Army_effects")).Append(CampaignEffectSource.ArmyBattleDefeat,
                                latest.NativeClock?.UpdateOrdinal ?? throw Missing("death_Army_revision"), effects) };
                    BindLatestPolicyReader();
                    Observe("death.associated-Army-destroyed");
                }

                var associated = current.ReadPersonLinkedEntity(person) ?? throw Missing("associated_receiver");
                if (Known(current.ReadLinkedEntityNativeValidity(associated), "associated_validity"))
                {
                    if (emitHistory && IsPlayer(oldForce))
                    {
                        byte raw = Known(current.ReadPersonDistrictSlot(person), "associated_PersonE0");
                        var district = current.ReadDistrictTableReference(raw < 52 ? raw : 0)
                            ?? throw Missing("associated_District_lookup");
                        if (BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, Missing))
                        {
                            int kind = new PersonReturnGovernorQueries(latest, package._governorInputs, current)
                                .ReadCurrentAssociatedVirtual98(associated) ?? throw Missing("associated_virtual98");
                            if (kind == 2) throw Missing("associated_type2_report1E6990");
                        }
                    }
                    var detached = PersonReturnCommands.DetachPersonFromFirstAssociated(latest, current, person);
                    latest = latest with { PersonnelCommands = detached.Owner ?? throw Missing(detached.UnresolvedOwner ?? "associated_member_detach") };
                    Observe("death.member-detached");
                }
                // Captured identities are tested at this actual source point,
                // after the ordinary prefix; never as an entry prefilter.
                bool willUnregister = capturedStatus == 1 && ReadRulerWillUnregister(person, deathCause);
                ApplyKillerForceRelations(person, capturedStatus, office, oldForce, willUnregister);
                if (capturedStatus == 1)
                {
                    continuationFactory = request => CaptureDeathContinuation(person, oldBase, oldPosition,
                        oldForce, capturedStatus, office, capturedBC, stage, associated, request);
                    int? rulerResult = ApplyRulerSuccession(person, killer, willUnregister);
                    if (rulerResult is null) return (latest, null);
                    if (rulerResult != 0) return (latest, rulerResult);
                }
                if (capturedStatus == 2) MergeDeceasedLeaderDistrict(person, oldForce);
                return FinishOrdinaryDeath(person, oldBase, oldForce, capturedStatus, capturedBC, stage, associated);
            }
            finally { ReleaseRelationships(); }
        }

        private (GameState State, int? NativeReturn) FinishOrdinaryDeath(int person, BaseReference oldBase,
            ForceReference oldForce, byte capturedStatus, TransferPersonReference capturedBC, int stage,
            PersonLinkedEntityReference associated)
        {
            if (capturedStatus == 6) ApplyCapturedItems(person, oldForce);
            if ((Known(current.ReadOwnedReturnPersonFlags(person), "tail_flags15E") & 0x4000) != 0 && Status(person) != 5)
                throw Missing("appendix_World_unregister");
            ApplyItems(person, associated, killer);
            ResetPerson(person);
            SetStatus(person, 9);
            Change(person, p => p with { Presence = OfficerPresence.Dead, Deployed = false });
            Observe("death.status9");

            if (Known(current.ReadForceNativeValidity(oldForce), "popularity_force_validity"))
            {
                int firstOrdinal = Known(current.ReadForcePhysicalSlot(oldForce), "popularity_force_kind_first");
                if (firstOrdinal != 0)
                {
                    int second = Known(current.ReadForcePhysicalSlot(oldForce), "popularity_force_kind_second");
                    if (second != 51 && unchecked((uint)(second - 46)) > 4)
                    {
                        int scale = capturedStatus == 1 ? DeathCombatParameter("ruler-popularity-loss", 15) :
                            PersonDeathReportInputs.ReadCurrentPopularityScale(latest)
                                ?? throw Missing("current_parameter_general-popularity-loss-scale");
                        int adjustment = unchecked(-(capturedStatus == 1 ? scale : Math.Max(1, stage) * scale));
                        latest = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(
                            latest, current, second, adjustment);
                        Observe("death.popularity-adjusted");
                    }
                }
            }
            ApplyRelationships(person, capturedBC);
            if (Known(current.ReadBaseNativeValidity(oldBase), "old_Base_validity_tail"))
            {
                var packet = package.ApplyPersonFreedomGovernorCurrent(latest, current, oldBase);
                foreach (var write in packet.PersonWrites) SetStatus(write.PersonPhysical, write.StatusAfter);
                int at = Known(oldBase.PhysicalSlot, "governor_Base_pointer");
                if (oldBase.Kind == BaseKind.City && latest.Cities.TryGetValue(at, out var city))
                    latest = latest with { Cities = new(latest.Cities) { [at] = city with
                    { GovernorId = packet.GovernorWord2EAfter == 0 ? null : packet.GovernorWord2EAfter,
                        NativeReturnRuntime = (city.NativeReturnRuntime ?? throw Missing("governor_City_owner")) with { Governor2E = packet.GovernorWord2EAfter } } } };
                else if (oldBase.Kind == BaseKind.Gate && latest.Gates.TryGetValue(at + 51, out var gate))
                {
                    var simulation = latest.NativeArmySimulation ?? throw Missing("governor_Gate_owner");
                    latest = latest with { Gates = new(latest.Gates) { [gate.Id] = gate with
                        { GovernorId = packet.GovernorWord2EAfter == 0 ? null : packet.GovernorWord2EAfter } },
                        NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                        { Int32Values = new(simulation.Parameters.Int32Values)
                            { [OwnedArmyLifecycleDetachedInputs.Key("Gate", at, "2E")] = packet.GovernorWord2EAfter } } } };
                }
                else throw Missing("governor_Base_publication");
                Observe("death.governor-tail");
            }
            RemoveRegistration(person);
            Observe("death.unregistered");
            return (latest, 0);
        }

        private void RemoveRegistration(int person)
        {
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person) ?? throw Missing("person_registry_descriptor");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("person_registry_head");
            var target = new RegistryObjectReference(RegistryKind.Person, person);
            while (node.Identity.HasValue)
            {
                if ((current.ReadRegistryNodePayload(node) ?? throw Missing("person_registry_payload")) == target)
                { _ = current.RemoveAllRegisteredPersonReferences(target); return; }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("person_registry_next");
            }
        }

        private int Parameter(string name) => latest.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(
            "simulation.person-death." + name, out int value) == true ? value : throw Missing("current_parameter_" + name);
        private void BindLatestPolicyReader() => current.BaseForcePolicyEffectReader = (force, effect) =>
            ForcePolicyScratchCurrent.ReadBeforeFreedom(latest, current, force, effect, true).Value;
        private bool BandPersonAtOrdinaryBase(int person)
        {
            if (!BattleRemakeCurrentRegionFactor.IsNativeBandPerson(Identity(person))) return false;
            ushort first = Known(current.ReadPersonFirstLocation(person), "band_first_location");
            if (first is < 1 or > 61) return false;
            return Known(current.ReadForcePhysicalSlot(Force(person)), "band_force_kind") is not (>= 46 and <= 50);
        }
        private ArmyUnitReference PersonArmy(int person)
        {
            ushort second = Known(current.ReadPersonSecondLocation(person), "PersonE4_for_Army");
            var army = current.ReadArmyUnitTableReference(second is >= 62 and <= 561 ? second - 61 : 0) ?? throw Missing("Person_Army_lookup");
            return Known(current.ReadArmyUnitCommanderId(army), "Person_Army_commander") == Identity(person)
                ? army : current.ReadArmyUnitTableReference(0) ?? throw Missing("Person_Army_fallback");
        }
        private HexReference Position(int person)
        {
            var result = PersonReturnCommands.ResolvePersonPosition(latest, current, person,
                latest.NativeMapSeason?.ThirdDate ?? throw Missing("current_ThirdDate"));
            if (current.ReadOwnedPersonCommandManager() is { } commands) latest = latest with { PersonnelCommands = commands };
            return result;
        }
        private ushort Identity(int physical) => Known(current.ReadPersonIdentity(physical), "Person_ID10");
        private byte Status(int physical) => Known(current.ReadPersonStatus(physical), "Person_status_E6");
        private bool Serving(int physical) => Known(current.ReadPersonNativeValidity(physical), "serving_validity") && Status(physical) is >= 1 and <= 4;
        private bool Valid(TransferPersonReference pointer) => pointer.PhysicalSlot is int at && Known(current.ReadPersonNativeValidity(at), "Person_validity");
        private static int Physical(TransferPersonReference pointer, string field) => Known(pointer.PhysicalSlot, field);
        private ForceReference Force(int physical) => current.ReadPersonForce(physical) ?? throw Missing("Person_force");
        private bool IsPlayer(ForceReference force)
        {
            var player = current.ReadCurrentPlayerForce() ?? throw Missing("current_player");
            int id = Known(current.ReadForcePhysicalSlot(player), "current_player_identity");
            return id != 0 && id == Known(current.ReadForcePhysicalSlot(force), "compared_force_identity");
        }
        private ushort Relation(int physical, int field) => Known(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(latest, physical, field), "relation_word");
        private int Age(int physical)
        {
            int mode = latest.CampaignEventWorld?.ScenarioMode ?? throw Missing("age_world_mode");
            if (mode is 2 or 3) throw Missing("age_special_mode");
            int birth = Owner(physical).BirthYear ?? throw Missing("age_birth_AC");
            int year = latest.NativeMapSeason?.ThirdDate.Year ?? throw Missing("age_ThirdDate_year");
            return Math.Max(0, unchecked((short)year - birth + 1));
        }
        private OfficerState Owner(int physical)
        {
            int id = Identity(physical);
            return latest.Officers.TryGetValue(id, out var person) && current.ResolveOfficerSource(id) == physical
                ? person : throw Missing("canonical_Person_owner");
        }
        private void Change(int physical, Func<OfficerState, OfficerState> update)
        { var owner = Owner(physical); latest = latest with { Officers = new(latest.Officers) { [owner.Id] = update(owner) } }; }
        private void SetStatus(int physical, byte status)
        {
            current.WritePersonStatus(physical, status);
            var value = OfficerServiceStatusSourceAdapter.Decode(status);
            Change(physical, p => p with { ServiceStatus = value, Role = OfficerServiceRules.PublishedRole(value, p.Role) });
        }
        private static void RequireReports(PersonAssignmentPrecallResult result)
        {
            if (result.Reports.Count != 0) throw Missing("assignment_report_" + result.Reports[0].NativeMessageCode.ToString("X4"));
        }
        private HexListHandle Temporary()
        {
            Enter(); var result = current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor"); Exit();
            if (!result.Identity.HasValue) throw Missing("temporary_allocation_profile"); return result;
        }
        private void Append(HexListHandle list, RegistryObjectReference payload)
        { Enter(); var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, payload); Exit(); }
        private void ClearRelease(HexListHandle list)
        { Enter(); current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); Exit(); }
        private void Enter() { if (Known(current.ReadRegistryLockEnabled(), "list_lock")) current.EnterRegistryLock(); }
        private void Exit() { if (Known(current.ReadRegistryLockEnabled(), "list_unlock")) current.ExitRegistryLock(); }
        private void Observe(string point) { if (observe is not null) observe(point, GameStateCopy.Copy(latest)); }
        private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("person-death." + field);
    }
}
