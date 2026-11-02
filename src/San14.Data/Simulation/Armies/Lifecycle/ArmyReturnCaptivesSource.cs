using San14.Core;

namespace San14.Data;

internal interface IOfficerSalaryInputs
{
    int? ReadOffice(int personPhysical);
    bool? OfficeValid(int office);
    int? ReadOfficeGuard(int office);
    int? ReadParameter(string key);
    int? ReadForceAdvantage(ForceReference force, int advantage);
}

public sealed record ArmyReturnCaptivesOwners(bool? PlayerTreatmentEnabled);
public sealed record ArmyReturnCaptivesReceipt(int[] PersonOrder, int NativeReturn,
    PersonPendingMeritReceipt? CommanderMerit, int GuardRequirement, bool GuardShortage);
public sealed record ArmyReturnCaptivesResult(GameState? State,
    ArmyReturnCaptivesReceipt? Receipt, string? UnresolvedOwner)
{
    internal ArmyReturnPlayerCaptivePreparation? PlayerPreparation { get; init; }
}

/// <summary>2F8F00's noninteractive location-return branch and its ordinary
/// player/native-IGameState-absent branch. Both retain actual Person/Item
/// temporaries, pending merit and the reached guard query.
/// Prisoners stay status6 with their original service. No player treatment
/// decision or AI treatment is inferred. Nonempty Item transfers retain
/// their actual per-item history and direct ownership/status stores.</summary>
public sealed class ArmyReturnCaptivesSource(ArmyCampaignRuleAdapter package,
    GameState input, ArmyReturnCaptivesOwners owners)
{
    private GameState _working = input;
    private long? _returnRevision;
    private NativeMapSeasonDate? _returnDate;
    private Func<GameState, TacticSourceSamplerWorld>? _returnSampler;
    private Func<GameState, int>? _returnDay;

    internal GameState CurrentState => _working;
    internal void AdoptCurrentState(GameState state) => _working = state;

    internal void BindPersonnelServiceCurrent(SourceWorldContext current)
        => current.TransferServiceRefresher = (first, second, third) =>
            _working = package.RefreshPersonnelMilestoneCurrent(_working, current, first, second, third);

    internal GameState PreparePlayerSuspension(SourceWorldContext current, ArmyUnitReference army,
        BaseReference target, DistrictReference savedCommander, DistrictReference savedTarget,
        bool returningPerson, ArmyReturnPlayerCaptivePreparation preparation)
    {
        int physical = army.PhysicalSlot ?? throw Missing("player_Army_pointer");
        var slot = _working.ReadArmyLifecycle()?.ReceiverSlots()[physical] ?? throw Missing("player_Army_receiver");
        int unit = slot.ActiveUnitId ?? throw Missing("player_active_Army");
        var sampler = _returnSampler?.Invoke(_working) ?? package.ReadArmyDestructionSampler(_working, current)
            ?? throw Missing("player_return_sampler");
        var pending = new ArmyReturnCaptiveContinuation(slot.ReceiverId, unit, physical,
            K(current.ReadBaseForceSlot(target), "player_captor_Force"),
            K(current.ReadArmyUnitCommanderId(army), "player_Commander12"),
            new(target.Kind == BaseKind.City ? SettlementBaseKind.City : SettlementBaseKind.Gate,
                target.Kind == BaseKind.City ? K(target.PhysicalSlot, "player_City") : K(target.PhysicalSlot, "player_Gate") + 51),
            K(savedCommander.PhysicalSlot, "player_saved_Commander_District"), K(savedTarget.PhysicalSlot, "player_saved_target_District"),
            returningPerson, _returnDate ?? throw Missing("player_source_date"),
            _returnRevision ?? throw Missing("player_source_revision"), sampler.Day450, sampler.Salt45C,
            preparation.OriginalOfficerIds, preparation.OriginalOfficerIds, preparation.RecruitmentTrials,
            ArmyReturnCaptivePhase.AwaitingIntroduction, 0);
        _working = package.PreparePlayerReturnCaptiveIntroductionCurrent(_working, current, pending);
        return _working;
    }

    internal ArmyReturnDistrictOperationQueries BorrowDistrictOperationCurrent(SourceWorldContext current,
        IArmyReturnGovernorCurrentQueries queries, NativeMapSeasonDate? date, long revision,
        Func<GameState, int> readDay450, Func<GameState, TacticSourceSamplerWorld>? readSampler = null)
    {
        //The transaction draft retains the live associated list, while its
        //task metadata comes from this operation's actual saved task owner.
        PersonReturnCommands.BindCurrent(_working, current);
        _returnRevision = revision; _returnDay = readDay450; _returnDate = date; _returnSampler = readSampler;
        //The old standalone provider may know only comparator fields. Its
        //outer current is not this transaction draft's live task manager.
        //Use the actual draft for v98, retaining the supplied comparator.
        var currentQueries = package.CreateCaptureGovernorQueries(() => _working, current, queries);
        return new(currentQueries, () => _working, reached => _working = reached,
            (person, assignment) => ApplyAssignmentReports(current, person, assignment, revision, readDay450),
            current, [], package, date);
    }

    /// <summary>Real29C430/2783B0 report point, before its caller changes the
    /// Person's district or locations. Retain history, App draws and notification
    /// order in the same return operation owner used by subsequent merit.</summary>
    internal void ApplyAssignmentReports(SourceWorldContext current, int personPhysical,
        PersonAssignmentPrecallResult assignment, long revision, Func<GameState, int> readDay450)
    {
        if (assignment.NativeReturn is null) throw Missing(assignment.UnresolvedOwner ?? "return_assignment");
        if (assignment.Reports.Count == 0) return;
        var effects = new List<CampaignEffectRequest>();
        _working = package.ApplyPersonAssociationRemovalReports(_working, current, personPhysical, assignment,
            () => readDay450(_working), effects);
        if (effects.Count != 0)
            _working = _working with { CampaignEffectRequests = (_working.CampaignEffectRequests
                ?? throw Missing("return_assignment_effect_owner"))
                .Append(CampaignEffectSource.ForceAssociationRemoval, revision, effects) };
    }

    internal string? ApplyTransportPersonReturn(SourceWorldContext current, int commanderPhysical)
    {
        var returned = TransportPersonReturnSource.TryApplyCurrent(_working, current, commanderPhysical, _returnDate,
            reached => ApplyTransportPersonFreedomCurrent(reached, current, commanderPhysical));
        if (returned.State is not { } continued || returned.NativeReturn is null)
            return returned.UnresolvedOwner ?? "current.transport_PersonReturn2EA9A0";
        _working = continued;
        return null;
    }

    //2EA9A0's Force/Home failures call265250(Person,0,0), with no
    //2E7890 no-origin resentment prefix. The full freedom kernel also owns
    //its real cause4/pending70 tail; the enclosing return still owns stage26.
    private GameState ApplyTransportPersonFreedomCurrent(GameState reached,
        SourceWorldContext current, int personPhysical)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("transport_freedom_current_pool");
        _working = reached;
        int Day() => (_returnDay ?? throw Missing("transport_freedom_day450"))(_working);
        TacticSourceSamplerWorld Sampler() =>
            (_returnSampler ?? throw Missing("transport_freedom_sampler"))(_working);
        var effects = new List<CampaignEffectRequest>();
        var continued = package.ApplyCaptiveFreedomCurrent(reached, current, pool, personPhysical,
            _returnDate, Day, Sampler, effects);
        if (effects.Count != 0)
            continued = continued with { CampaignEffectRequests = (continued.CampaignEffectRequests
                ?? throw Missing("transport_freedom_effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                    _returnRevision ?? throw Missing("transport_freedom_revision"), effects) };
        return continued;
    }

    /// <summary>29F5xx type2 merit runs after morale and before cargo debit.
    /// Its returned state is retained through the later captive publication.</summary>
    internal string? ApplyTransportMerit(SourceWorldContext current,
        ArmyUnitReference army, int acceptedCargo)
    {
        try
        {
            int parameter = P(_working, SimulationParameterKeys.ArmyDestruction.TransportReturnCommanderMerit);
            float requested = ((float)acceptedCargo / 100000.0f + 1.0f) * unchecked(parameter * 10);
            int amount = !float.IsFinite(requested) || requested < int.MinValue || requested >= 2147483648f
                ? int.MinValue : (int)requested;
            GameState working = OwnedArmyReturnCurrentFields.PublishCurrent(_working, current);
            ushort commanderId = K(current.ReadArmyUnitCommanderId(army), "29F5xx Army12");
            var result = package.ApplyReturnCaptiveMerit(working, current, commanderId, amount);
            if (result.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved)
                return "current.transport1695C0";
            _working = result.State;
            return null;
        }
        catch (InvalidDataException error) { return error.Message; }
    }

    internal ArmyReturnCaptivesResult ApplyReport(SourceWorldContext current,
        HexListHandle output, ArmyUnitReference army, BaseReference target, int nativeMode)
    {
        try
        {
            int[] people = FillArmyAssociates(current, output, army);
            if (people.Length == 0) return new(_working, new([], 0, null, 0, false), null);
            //Home, traits and resource writes preceding2F8F00 are already in
            //this same private current. Merit must see those current Persons.
            GameState working = OwnedArmyReturnCurrentFields.PublishCurrent(_working, current);
            ushort commanderId = K(current.ReadArmyUnitCommanderId(army), "2F8F00 Army12");
            int commander = Person(current, commanderId);
            PersonPendingMeritReceipt? merit = null;
            if (K(current.ReadPersonNativeValidity(commander), "2F8F00 Commander validity"))
            {
                int amount = unchecked(P(working, SimulationParameterKeys.ArmyDestruction.CaptiveReturnCommanderMerit) * 10);
                var earned = package.ApplyReturnCaptiveMerit(working, current, commanderId, amount);
                if (earned.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved)
                    throw Missing("current.2F8F00_Commander1695C0");
                working = earned.State; merit = earned.Receipt;
            }
            _ = Person(current, K(current.ReadArmyReturnBaseGovernor2E(target), "2F8F00 Base2E lookup")); //22ACF0.
            int forceId = K(current.ReadBaseForceSlot(target), "2F8F00 BaseForceB0");
            var force = current.ReadForceTableReference(forceId) ?? throw Missing("2F8F00 Force pointer");
            int reportRuler = Person(current, current.ReadForceRecord(forceId)?.RawRuler10 ?? throw Missing("2F8F00 Force10 lookup"));
            int location = K(current.ReadBaseObjectLink(target), "2F8F00 Base4E");
            int requirement; bool shortage;
            if (location is >= 52 and <= 61)
            {
                int sum = 0;
                foreach (int person in RegisteredPersons(current))
                {
                    byte status = K(current.ReadPersonStatus(person), "234920 PersonE6");
                    //Dependency projection: ineligible status cannot enter the
                    //read-only Home iterator even when its E2 is not exposed.
                    if (status is < 1 or > 4) continue;
                    if (K(current.ReadPersonFirstLocation(person), "234920 PersonE2") != location) continue;
                    sum = unchecked(sum + ReadGuard(working, current, person));
                }
                var gateCaptives = NewTemporary(current);
                try
                {
                    current.ClearTemporaryHexList(gateCaptives);
                    int count = FillBaseAssociates(current, gateCaptives, location).Length;
                    requirement = unchecked(unchecked((count + 1) * P(working, SimulationParameterKeys.ArmyDestruction.CaptiveTroopRequirementAddend)) + unchecked(P(working, SimulationParameterKeys.ArmyDestruction.GateCaptiveGuardMultiplier) * sum));
                    shortage = K(current.ReadBaseResource(target, BaseResourceKind.Troops), "2F8F00 Gate virtual90") < requirement;
                }
                finally { Release(current, gateCaptives); }
            }
            else
            {
                //1017FE0 is a direct int32 Base50 getter, not268230.
                int ready = K(current.ReadBaseResource(target, BaseResourceKind.Troops), "2F8F00 City virtual90");
                int wounds = K(current.ReadArmyReturnBaseWounded40(target), "2F8F00 City wounded22DAA0");
                int raw50 = K(current.ReadBaseReturnField50(target), "current.2F8F00_City_raw50");
                requirement = unchecked(raw50 + P(working, SimulationParameterKeys.ArmyDestruction.CaptiveTroopRequirementAddend));
                shortage = unchecked(ready + wounds) < requirement;
            }
            var itemEffects = new List<CampaignEffectRequest>();
            foreach (int person in people)
                working = package.ApplyReturnCaptiveItemsCurrent(working, current, target, person, reportRuler,
                    _returnDay, itemEffects);
            if (itemEffects.Count != 0)
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw Missing("2F8F00 item_effect_owner")).Append(CampaignEffectSource.ArmyCommandMaintenance,
                        _returnRevision ?? working.NativeMapSeason?.AdvancedCalendarSteps
                            ?? throw Missing("2F8F00 item_operation_calendar"), itemEffects) };
            if (nativeMode != 0)
                working = ApplyNoninteractiveLocations(current, target, people, working);
            else
            {
                var player = current.ReadCurrentPlayerForce() ?? throw Missing("2F8F00 World3A PlayerForce");
                bool isPlayer = K(current.ReadForceNativeValidity(player), "2F8F00 Player validity") && player == force;
                if (!isPlayer)
                {
                    var date = _returnDate ?? working.NativeMapSeason?.ThirdDate ?? throw Missing("return_treatment_date");
                    int Day(GameState reached) => (_returnDay ?? throw Missing("return_treatment_day450"))(reached);
                    TacticSourceSamplerWorld Sampler() => _returnSampler?.Invoke(working)
                        ?? package.ReadArmyDestructionSampler(working, current) ?? throw Missing("return_treatment_sampler");
                    var treatmentEffects = new List<CampaignEffectRequest>();
                    foreach (int person in people)
                    {
                        var choice = package.SelectReturnCaptiveCurrent(working, current, person, reportRuler,
                            force, shortage, commanderId, Sampler);
                        working = package.ApplyOrdinaryCaptiveTreatmentCurrent(working, current, person, target,
                            choice, date, _returnRevision ?? throw Missing("return_treatment_revision"), Day, Sampler, treatmentEffects);
                    }
                    if (treatmentEffects.Count != 0)
                        working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                            ?? throw Missing("return_treatment_effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                                _returnRevision ?? throw Missing("return_treatment_revision"), treatmentEffects) };
                }
                else if (owners.PlayerTreatmentEnabled is true)
                {
                    _working = working;
                    TacticSourceSamplerWorld Sampler() => _returnSampler?.Invoke(_working)
                        ?? package.ReadArmyDestructionSampler(_working, current) ?? throw Missing("player_trial_sampler");
                    var preparation = package.PreparePlayerReturnCaptiveTrialsCurrent(working, current, people, target, Sampler);
                    //No native return receipt is manufactured while the real
                    //player operation is suspended in its batch controller.
                    return new(_working, null, null) { PlayerPreparation = preparation };
                }
                else if (owners.PlayerTreatmentEnabled is not false)
                    throw Missing("current.2F8F00_player_treatment_owner");
                //An explicitly disabled offline source profile keeps its
                //known-null native branch. Formal return owners enable pause.
            }
            //Both source branches reach the same final valid-Person flag loop.
            foreach (int person in people)
                if (K(current.ReadPersonNativeValidity(person), "2F8F00 final Person validity"))
                    current.WriteArmyReturnPersonFlags196(person, OfficerBehaviorSourceAdapter.FinishCaptiveProcessing(K(current.ReadArmyReturnPersonFlags196(person), "2F8F00 final15E")));
            _working = working;
            return new(_working, new(people, 1, merit, requirement, shortage), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    //2F9901's actual param3!=0 branch. It deliberately bypasses both
    //AI treatment and CTreatPrisonerState; it neither frees nor recruits.
    //There is no Person-validity gate before these source location stores.
    private static GameState ApplyNoninteractiveLocations(SourceWorldContext current,
        BaseReference target, IReadOnlyList<int> people, GameState working)
    {
        foreach (int person in people)
        {
            ushort second = Math.Min(K(current.ReadBaseObjectLink(target), "2F9901 Base4E_second"), (ushort)1061);
            current.WritePersonSecondLocation(person, second);
            ushort first = Math.Min(K(current.ReadBaseObjectLink(target), "2F9901 Base4E_first"), (ushort)61);
            current.WritePersonFirstLocation(person, first);
            int id = K(current.ReadPersonIdentity(person), "2F9901 Person10");
            var aftermath = current.ReadOfficerAftermath(person) ?? throw Missing("2F9901 aftermath_owner");
            int resentment = aftermath.Resentment?.TargetOfficerId ?? aftermath.SourceKnowledge?.UnmappedResentmentTarget
                ?? throw Missing("2F9901 Person162");
            if (resentment != id) continue;
            //Only162 is zeroed:164 strength and all unrelated knowledge
            //remain as observed, including an unknown strength.
            current.SetOfficerAftermath(person, aftermath with
            {
                Resentment = new(0, aftermath.Resentment?.Intensity),
                SourceKnowledge = aftermath.SourceKnowledge is { } residue
                    ? residue with { UnmappedResentmentTarget = null } : null
            });
            working = current.PublishOfficerAftermath(working, id);
        }
        return working;
    }

    internal static int[] FillArmyAssociates(SourceWorldContext current, HexListHandle output,
        ArmyUnitReference army) => FillBaseAssociates(current, output,
            army.PhysicalSlot is >= 1 and <= 500 ? army.PhysicalSlot.Value + 61 : 0);

    internal static int[] FillBaseAssociates(SourceWorldContext current, HexListHandle output, int location)
    {
        var people = new List<int>();
        foreach (int person in RegisteredPersons(current))
        {
            if (K(current.ReadPersonStatus(person), "230C70 PersonE6") != 6) continue;
            if (K(current.ReadPersonSecondLocation(person), "230C70 PersonE4") != location) continue;
            if (!current.AppendPersonReturnTemporary(output, person)) throw Missing("branch.230C70_node_allocation");
            people.Add(person);
        }
        return people.ToArray();
    }

    internal static int ReadGuard(GameState state, SourceWorldContext current, int person)
        => ReadGuard(state, current, person, new SavedSalaryInputs(state, current));

    internal static int ReadGuard(GameState state, SourceWorldContext current, int person, IOfficerSalaryInputs inputs)
    {
        if (!K(current.ReadPersonNativeValidity(person), "2B4DE0 Person validity")) return 0;
        int post = K(inputs.ReadOffice(person), "2B4DE0 PostE7");
        int selected = K(inputs.OfficeValid(post), "2B4DE0 Post validity") ? post : 73;
        int office = K(inputs.ReadOfficeGuard(selected), "current.2B4DE0_Post36");
        if (office is < 0 or > 255) throw Missing("current.2B4DE0_Post36");
        int amount = unchecked(office * Parameter(SimulationParameterKeys.ArmyDestruction.CaptiveGuardOfficeMultiplier));
        if (!K(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(person)), "2B4DE0 Person15E")) amount = Parameter(SimulationParameterKeys.ArmyDestruction.CaptiveGuardFlaggedAmount);
        int effect = K(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 46), "2B4DE0 effect46");
        int trait = unchecked(Parameter(SimulationParameterKeys.ArmyDestruction.CaptiveGuardTraitMultiplier) * effect);
        var force = current.ReadPersonForce(person) ?? throw Missing("2B4DE0 current PersonForce");
        int land = K(inputs.ReadForceAdvantage(force, 12), "2B4DE0 advantage12");
        int first = unchecked(unchecked(Parameter(SimulationParameterKeys.ArmyDestruction.CaptiveGuardLandMultiplier) * land) * amount);
        return unchecked(unchecked(amount - first / 100) - unchecked(trait * amount) / 100);

        int Parameter(string key) => K(inputs.ReadParameter(key), $"2F8F00 parameter TC{key}");
    }

    private sealed class SavedSalaryInputs(GameState state, SourceWorldContext current) : IOfficerSalaryInputs
    {
        public int? ReadOffice(int personPhysical) => OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, personPhysical);
        public bool? OfficeValid(int office) => OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, office);
        public int? ReadOfficeGuard(int office)
        {
            var values = state.NativeArmySimulation?.Parameters.Int32Values ?? throw Missing("2B4DE0 saved parameter owner");
            return values.TryGetValue(OwnedArmyGovernorComparatorInputs.OfficeGuard36Key(office), out int value) ? value : null;
        }
        public int? ReadParameter(string key) => ArmyDestructionParameterSeed.ReadCurrent(state, key);
        public int? ReadForceAdvantage(ForceReference force, int advantage)
        {
            var result = PersonReturnPathSource.ReadForceAdvantage(state, current, force, advantage);
            return K(result.Value, result.UnresolvedOwner ?? "2B4DE0 advantage12");
        }
    }

    private static int[] RegisteredPersons(SourceWorldContext current) =>
        (current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("230C70 manager100")).Select(x => x ?? throw Missing("230C70 Person payload")).ToArray();
    private static int Person(SourceWorldContext current, ushort id) =>
        (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot ?? throw Missing("2F8F00 Person pointer");
    private static HexListHandle NewTemporary(SourceWorldContext current)
    {
        var temporary = current.CreateTemporaryHexList() ?? throw Missing("2F8F00 temporary allocation");
        if (!temporary.Identity.HasValue) throw Missing("branch.2F8F00_descriptor_allocation");
        return temporary;
    }
    private static void Release(SourceWorldContext current, HexListHandle descriptor)
    { current.ClearTemporaryHexList(descriptor); current.ReleaseTemporaryHexList(descriptor); }
    private static int P(GameState state, string key) => K(ArmyDestructionParameterSeed.ReadCurrent(state, key), $"2F8F00 parameter TC{key}");
    private static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal PersonPendingMeritResult ApplyReturnCaptiveMerit(GameState state, SourceWorldContext current,
        int personId, int requested) => _pendingMerit.TryApplyWithinArmyOperation(state, current, personId, requested);
}
