using San14.Core;

namespace San14.Data;

public sealed record ArmyMovementManagerActor(int UnitId, int NativePoolSlot)
{
    //Operation-local borrow of the lifecycle's real image, never a new Unit.
    public UnitState? PendingImage { get; init; }
}
/// <summary>Inputs reached before3014C0, even when the route has no next Hex.
/// No destination Terrain getter is reached on that exit.</summary>
public sealed record ArmyMarchPreparationInputs(bool ArmyValid, float Mobility2B0660,
    int SpeedScale2695D0, int Penalty2B5C10, int CachedMinimumTerrainCost1A24010,
    bool CurrentTerrainPointerAvailable, string SourceProvenance, GameState? PreparedState = null);
/// <summary>Results from the reached source owners, never displayed movement
/// or route budgets:2B0660 mobility,2B5C10 penalty,2695D0 current scale,
/// cached minimum1A24010 and current destination Terrain getterBDDD0.
/// Dynamic Hex1C is signed; Hex1A is an independent current object field.</summary>
public sealed record ArmyMarchCurrentInputs(bool ArmyValid, float Mobility2B0660,
    int SpeedScale2695D0, int Penalty2B5C10, int CachedMinimumTerrainCost1A24010,
    int DestinationTerrainCostBDDD0, bool CurrentTerrainPointerAvailable,
    bool DestinationHexValid, ushort DestinationHexObjectWord1A,
    string SourceProvenance, GameState? PreparedState = null);
public interface IArmyMarchCurrentQueries
{
    /// <summary>Queries borrow the supplied operation world for the duration of each call.</summary>
    IReadOnlyList<ArmyMovementManagerActor>? ReadCompleteCurrentManager60Chain(GameState state,
        NativeArmyProgressStageMoment stage7Moment);
    bool? ReadSourceMovementEligibility2C7350(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage7Moment);
    /// <summary>2D99C0's reached Next=None prefix:301DD0(army,0,0,0).
    /// The returned private world includes the route cache and temporary pool;
    /// it is published only if the remaining march transaction succeeds.</summary>
    GameState? PrepareOrdinaryNextStep(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage8Moment) => null;
    ArmyMarchPreparationInputs? ReadOrdinaryPreparationInputs(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage8Moment) => null;
    /// <summary>The full-minimum3014C0 test for Next=None borrows Hex0.
    /// True retains None; false is the known type2 hostile fallback requiring
    /// its actual clear/301DD0 retry. Null is an unresolved receiver/input.</summary>
    bool? ReadOrdinaryNoStepFallback(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage8Moment) => null;
    ArmyMarchCurrentInputs? ReadOrdinaryCurrentInputs(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage8Moment);
    /// <summary>After a3014C0 route rewrite, reuse the already read2695D0
    /// receipt and read only the selected destination fields.</summary>
    ArmyMarchCurrentInputs? ReadOrdinaryDestinationInputs(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage8Moment, ArmyMarchPreparationInputs preparation) =>
        ReadOrdinaryCurrentInputs(state, army, stage8Moment);
}

public static class ArmyMarchSourceMath
{
    /// <summary>TC2695D0: separate scalar binary32 convert/multiply/divide,
    /// then CVTTSS2SI. Unordered/out-of-int-range results are unsupported here.</summary>
    public static int? ResolveCycleSpeed(float mobility, int scale, int penalty)
    {
        if (!float.IsFinite(mobility)) return null;
        float scaled = (float)scale * mobility;
        int denominator = unchecked(penalty + 100);
        float quotient = scaled / (float)denominator;
        if (!float.IsFinite(quotient) || quotient >= 2147483648f || quotient < -2147483648f) return null;
        return (int)quotient;
    }
    /// <summary>TC2D9ADF..2D9B09: compare old word14 to destination cost,
    /// add signed speed, select zero on negative32 result, store low word,
    /// then unsigned cap1000. No decimal/period distance conversion.</summary>
    public static ushort AccumulateCredit(ushort former, int cycleSpeed, int destinationCost)
    {
        if (former >= destinationCost) return former;
        int sum = unchecked(former + cycleSpeed);
        ushort word = sum < 0 ? (ushort)0 : unchecked((ushort)sum);
        return word > 1000 ? (ushort)1000 : word;
    }
}

/// <summary>Ordinary direct-target branch of source2D99C0. The optional crossing
/// owner commits a proved interior edge; occupied destinations
/// and unsupported tails remain unresolved.</summary>
public sealed class ArmyMarchSourceRuntime : IArmyMovementRules
{
    public const string Revision = "san14:ce-stage8-ordinary-repeated-no-path-20261017";
    private readonly IArmyMarchCurrentQueries queries;
    private readonly ArmyMarchCrossingSource? crossings;
    private readonly SourceWorldContext? operationCurrent;
    public ArmyMarchSourceRuntime(IArmyMarchCurrentQueries currentQueries, ArmyMarchCrossingSource? crossingSource = null,
        SourceWorldContext? operationCurrent = null)
    {
        queries = currentQueries ?? throw new ArgumentNullException(nameof(currentQueries));
        crossings = crossingSource;
        this.operationCurrent = operationCurrent;
    }

    public ArmyMovementQueue? PrepareStage7(GameState state, NativeArmyProgressStageMoment moment)
        => PrepareStage7Core(state, moment, true);

    internal ArmyMovementQueue? PrepareCurrentStage7(GameState state, NativeArmyProgressStageMoment moment)
        => PrepareStage7Core(state, moment, false);

    private ArmyMovementQueue? PrepareStage7Core(GameState state, NativeArmyProgressStageMoment moment, bool externalOrder)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(moment);
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.PrepareMovement || moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Movement source list must be prepared at its reached stage7.");
        ValidateCurrentMoment(state, moment);
        var chain = queries.ReadCompleteCurrentManager60Chain(state, moment);
        if (chain is null)
        {
            if (operationCurrent is not null)
                throw new InvalidDataException("army.march.missing.stage7.current_manager_chain;cycle=" + moment.CalendarCycleOrdinal);
            return null;
        }
        if (externalOrder)
        {
            var expected = state.Units.Values.Where(u => u.NativeArmyOrigin is not null).Select(u => u.Id).ToArray();
            if (chain.Any(a => a is null) || chain.Select(a => a.UnitId).Distinct().Count() != expected.Length ||
                chain.Select(a => a.NativePoolSlot).Distinct().Count() != chain.Count ||
                chain.Count != expected.Length || !chain.Select(a => a.UnitId).Order().SequenceEqual(expected.Order()))
                throw new InvalidDataException("Source stage7 requires its complete current manager chain, without ID sorting as an ordering substitute.");
        }
        var selected = new List<ArmyMovementQueueActor>();
        foreach (var actor in chain)
        {
            UnitState? army = state.Units.GetValueOrDefault(actor.UnitId) ?? actor.PendingImage;
            if (army is null)
            {
                if (operationCurrent is not null)
                    throw new InvalidDataException($"army.march.missing.stage7.manager_image;cycle={moment.CalendarCycleOrdinal};Army={actor.UnitId};physical={actor.NativePoolSlot}");
                return null;
            }
            if (!HasCurrentRules(army))
            {
                if (operationCurrent is not null) throw MissingBranch(state, army, moment, "stage7.rule_identity");
                return null;
            }
            if (army.NativeArmyOrigin?.NativeSlotIndex != actor.NativePoolSlot)
                throw new InvalidDataException("Source stage7 chain names another native Army owner.");
            bool? eligible = queries.ReadSourceMovementEligibility2C7350(state, army, moment);
            if (eligible is null)
            {
                if (operationCurrent is not null) throw MissingBranch(state, army, moment, "stage7.eligibility_inputs");
                return null;
            }
            if (!eligible.Value) continue;
            if (!state.Units.ContainsKey(actor.UnitId))
            {
                //A pending70 member should have reached the real233560 false
                //selection. Preserve a genuine missing lifecycle if it did not.
                if (operationCurrent is not null) throw MissingBranch(state, army, moment, "stage7.pending_receiver_selected");
                return null;
            }
            if (army.March is not { MovementCredit: int credit } march ||
                ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort hex || hex != army.HexId)
            {
                if (operationCurrent is not null) throw MissingBranch(state, army, moment, "stage7.current_march_Hex_and_credit");
                return null;
            }
            ArmyReceiverId? receiver = state.ReadArmyLifecycle() is { } arena
                ? arena.ReceiverSlots()[actor.NativePoolSlot].ReceiverId : null;
            selected.Add(new(actor.UnitId, receiver, credit, hex, march));
        }
        // TC24B260 only moves a node when its word14 is strictly greater than
        // its preceding node. Equal-credit chain order is retained.
        return new(moment, Array.AsReadOnly(selected.OrderByDescending(a => a.CreditAtPreparation).ToArray()));
    }

    public ArmyMovementResolution? AdvanceArmy(GameState state, UnitState army, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement || moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Ordinary source march requires its reached stage8.");
        ValidateCurrentMoment(state, moment);
        ushort? diagnosticTarget = null;
        //2D99C0 does not call2725E0 before movement. Saved-command-specific
        //movement costs are consumed by2B5C10; command maintenance belongs to
        //its real stage28 manager, after this cycle's movement/combat work.
        if (state.Units.GetValueOrDefault(army.Id) != army || army.NativeArmyOrigin is null ||
            !HasCurrentRules(army) ||
            army.March is not { } march || ArmyConfigurationSourceAdapter.ReadAction(state, army) is not byte action ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort current ||
            current != army.HexId || current >= ArmyMarchSourceAdapter.SourceHexCount ||
            ArmyMarchSourceAdapter.EncodeCredit(march) is not ushort formerCredit ||
            ArmyMarchSourceAdapter.EncodeDestination(march.NextStep) is not ushort next ||
            !OrdinaryArmyRouteSource.HasSupportedMovementOverride(army) ||
            next > ArmyMarchSourceAdapter.SourceHexCount)
            return MissingCurrent(state, army, moment, "runtime.entry_fields");
        var working = state;
        ArmyMarchPreparationInputs? retainedPreparation = null;
        int? retainedSpeed = null;
        bool minimumRerouteCompleted = false;
        //2D99C0 consumes a first waypoint already at the current position
        //before reading its effective target or invoking any path preparation.
        if (ArmyMarchSourceAdapter.ReadFirstWaypoint(state, army) == current)
        {
            if (ArmyWaypointArrivalSource.ConsumeCurrent(state, army) is not { } advanced) return Missing("runtime.consume_current_waypoint");
            working = advanced;
            army = working.Units[army.Id];
        }
        if (OrdinaryArmyRouteSource.ReadCurrentTarget(working, army, operationCurrent) is not ushort target) return Missing("runtime.effective_target");
        diagnosticTarget = target;
        if (target == ArmyMarchSourceAdapter.SourceHexCount) return new(formerCredit, working);
        //2D99C0 consumes a current first waypoint before resolving the goal.
        //A real City5 entry/fallback2/11 can also have its goal here.
        //Retain its301DD0/2695D0/3014C0 no-step path, not an arrival call.
        //Gate3/12 keeps the existing effective Kind6 gate below.
        if (target > ArmyMarchSourceAdapter.SourceHexCount || target == current && action is not (14 or 16) &&
            !(action is 2 or 11 && OrdinaryArmyRouteSource.ReadEffectiveSearchTarget(working, army) is { Kind: 5 }) &&
            !OrdinaryArmyRouteSource.UsesObjectCommandMovement(working, army) &&
            !OrdinaryArmyRouteSource.HasExplicitMovementOverride(army) &&
            OrdinaryArmyRouteSource.ReadEffectiveSearchTarget(working, army) is not { Kind: 6 }) return Missing("runtime.target_scope");
        if (next == ArmyMarchSourceAdapter.SourceHexCount)
        {
            // Unknown Next was rejected above. Only the actual encoded None
            // reaches this prefix, before speed and3014C0.
            var prepared = queries.PrepareOrdinaryNextStep(working, army, moment);
            if (prepared is null || !prepared.Units.TryGetValue(army.Id, out var routed) ||
                ArmyMarchSourceAdapter.EncodeDestination(routed.March?.NextStep) is not ushort routedNext ||
                routedNext > ArmyMarchSourceAdapter.SourceHexCount) return Missing("runtime.initial_route_output");
            working = prepared;
            army = routed;
            next = routedNext;
            if (next == ArmyMarchSourceAdapter.SourceHexCount)
            {
                var preparation = queries.ReadOrdinaryPreparationInputs(working, army, moment);
                if (preparation is not { ArmyValid: true, CurrentTerrainPointerAvailable: true,
                    CachedMinimumTerrainCost1A24010: > 0 and < int.MaxValue } ||
                    string.IsNullOrWhiteSpace(preparation.SourceProvenance) ||
                    ArmyMarchSourceMath.ResolveCycleSpeed(preparation.Mobility2B0660,
                        preparation.SpeedScale2695D0, preparation.Penalty2B5C10) is not int noStepSpeed) return Missing("runtime.no_step_preparation_or_speed");
                working = preparation.PreparedState ?? working;
                if (unchecked(formerCredit + noStepSpeed) < preparation.CachedMinimumTerrainCost1A24010)
                    return new(formerCredit, working);
                bool? retain = queries.ReadOrdinaryNoStepFallback(working, working.Units[army.Id], moment);
                if (retain is null) return Missing("no_step_fallback_current");
                if (retain.Value) return new(formerCredit, working);
                //The known type2 hostile Hex0 branch reaches301DD0 again.
                //A genuine return0 retains its route/cache/pool writes but
                //does not accumulate credit or execute29DB90.
                bool? rerouted = RetryType2Next();
                if (rerouted is null) return Missing("no_step_301DD0_retry");
                if (!rerouted.Value) return new(formerCredit, working);
                minimumRerouteCompleted = true;
                retainedPreparation = preparation;
                retainedSpeed = noStepSpeed;
            }
        }
        bool objectTarget = OrdinaryArmyRouteSource.UsesObjectCommandMovement(working, army) ||
            crossings?.ReadGateStepCandidate(working, next) == true;
        bool collision = next == current || working.ArmyMapOccupancy?.HasArmyMarker(next) == true;
        if ((objectTarget || collision) && !minimumRerouteCompleted)
        {
            //3014C0's Object gate precedes BDDD0. Read only preparation here;
            //an invalid Object can return0 without demanding destination cost.
            var preparation = retainedPreparation ?? queries.ReadOrdinaryPreparationInputs(working, army, moment);
            if (preparation is not { ArmyValid: true, CurrentTerrainPointerAvailable: true,
                    CachedMinimumTerrainCost1A24010: > 0 and < int.MaxValue } ||
                string.IsNullOrWhiteSpace(preparation.SourceProvenance) ||
                ArmyMarchSourceMath.ResolveCycleSpeed(preparation.Mobility2B0660,
                    preparation.SpeedScale2695D0, preparation.Penalty2B5C10) is not int objectSpeed)
                return Missing("runtime.Object_preparation_or_speed");
            working = preparation.PreparedState ?? working;
            retainedPreparation = preparation; retainedSpeed = objectSpeed;
            if (unchecked(formerCredit + objectSpeed) >= preparation.CachedMinimumTerrainCost1A24010)
            {
                var objectStep = crossings?.ReadObjectStep(working, next);
                if (objectStep is null) return Missing("runtime.Object_step_inputs");
                if (objectStep == ArmyMarchObjectStepKind.Invalid) return new(formerCredit, working);
                if (working.ArmyMapOccupancy?.HasArmyMarker(next) is not bool objectOccupied)
                    return Missing("runtime.Object_step_occupancy");
                if (next == current || objectOccupied || objectStep == ArmyMarchObjectStepKind.GenericBlocked)
                {
                    var cleared = army with { March = army.March! with { NextStep = ArmyMarchDestination.None } };
                    var draft = working with { Units = new(working.Units) { [army.Id] = cleared } };
                    var prepared = queries.PrepareOrdinaryNextStep(draft, cleared, moment);
                    if (prepared is null || !prepared.Units.TryGetValue(army.Id, out var routed) ||
                        ArmyMarchSourceAdapter.EncodeDestination(routed.March?.NextStep) is not ushort rewritten ||
                        rewritten > ArmyMarchSourceAdapter.SourceHexCount) return Missing("runtime.Object_retry_route_output");
                    working = prepared; army = routed; next = rewritten;
                    if (next == ArmyMarchSourceAdapter.SourceHexCount) return new(formerCredit, working);
                    //The source tests only the rewritten Next, then returns1.
                    minimumRerouteCompleted = true;
                }
            }
        }
        var input = retainedPreparation is null ? queries.ReadOrdinaryCurrentInputs(working, army, moment)
            : queries.ReadOrdinaryDestinationInputs(working, army, moment, retainedPreparation);
        if (!UsableInputs(input)) return Missing("runtime.destination_inputs");
        working = input.PreparedState ?? working;
        int? speed = retainedSpeed ?? ArmyMarchSourceMath.ResolveCycleSpeed(input.Mobility2B0660, input.SpeedScale2695D0, input.Penalty2B5C10);
        if (speed is not int reachedSpeed) return Missing("runtime.cycle_speed");
        bool reachedMinimum = unchecked(formerCredit + reachedSpeed) >= input.CachedMinimumTerrainCost1A24010;
        if (working.ArmyMapOccupancy?.HasArmyMarker(next) is not bool occupied) return Missing("runtime.next_occupancy");
        if (!minimumRerouteCompleted && (next == current || reachedMinimum && occupied))
        {
            if (army.NativeTacticRuntime?.Status10 is not (1 or 2)) return Missing("runtime.occupied_type_scope");
            if (next == current && !reachedMinimum)
            {
                ushort accumulated = ArmyMarchSourceMath.AccumulateCredit(formerCredit, reachedSpeed, input.DestinationTerrainCostBDDD0);
            //3014C0's early return preserves Next=current.2D99C0 then
                //takes the self-occupied branch, whose mandatory29DB90 tail
                //must complete even though no spatial step is taken.
                return crossings?.TryWaitForOccupiedArmy(working, working.Units[army.Id], accumulated,
                    input.DestinationTerrainCostBDDD0, moment, diagnosticTarget) ?? Missing("runtime.current_accumulation_owner");
            }
            var clearedArmy = working.Units[army.Id] with
            { March = working.Units[army.Id].March! with { NextStep = ArmyMarchDestination.None } };
            var cleared = working with { Units = new(working.Units) { [army.Id] = clearedArmy } };
            var prepared = queries.PrepareOrdinaryNextStep(cleared, clearedArmy, moment);
            if (prepared is null || !prepared.Units.TryGetValue(army.Id, out var routed) ||
                ArmyMarchSourceAdapter.EncodeDestination(routed.March?.NextStep) is not ushort routedNext ||
                routedNext > ArmyMarchSourceAdapter.SourceHexCount) return Missing("runtime.occupied_retry_route_output");
            working = prepared; army = routed; next = routedNext;
            //3014C0 returns0 when the reached rewrite leaves Next=None.
            //That is a completed source branch: keep its clear/cache/pool
            //writes, but do not add credit or execute the blocked-step tail.
            if (next == ArmyMarchSourceAdapter.SourceHexCount) return new(formerCredit, working);
            minimumRerouteCompleted = true;
            input = queries.ReadOrdinaryCurrentInputs(working, army, moment);
            if (!UsableInputs(input)) return Missing("runtime.occupied_retry_destination_inputs");
            working = input.PreparedState ?? working;
            //3014C0 also clears and routes an occupied destination once the
            //minimum is reached. Below that minimum it retains Next and the
            //occupied-Army branch below consumes the source waiting tail.
            //Keep the original2695D0 speed across this route replacement.
        }
        //3014C0 keeps only the rewritten Next test. A newly selected Gate
        //or Object must reach2D99C0's actual post-credit occupant classifier.
        objectTarget = objectTarget || crossings?.ReadGateStepCandidate(working, next) == true ||
            minimumRerouteCompleted && input.DestinationHexObjectWord1A != 0;
        if (working.ArmyMapOccupancy?.HasArmyMarker(next) is not bool finalOccupied ||
            !finalOccupied && !objectTarget && input.DestinationHexObjectWord1A != 0 && crossings?.ReadDirectBaseEntryStep(working, army, next) != true)
            return Missing("runtime.final_occupancy_or_object_entry");
        //3014C0 has a real success branch beyond the minimum-cost early return:
        //an ordinary status1 actor with a distinct, unoccupied, object-free next
        //cell retains its selected step. Other reached route branches need owners.
        if (reachedMinimum && army.NativeTacticRuntime?.Status10 is not (1 or 2)) return Missing("runtime.minimum_type_scope");
        if (reachedMinimum && army.NativeTacticRuntime?.Status10 == 2)
        {
            //3014C0's known hostile territory case clears48 and calls the
            //same301DD0(0,0,0). It tests only the resulting Next, not a
            //second territory predicate, before returning to2D99C0.
            if (!minimumRerouteCompleted)
            {
                if (operationCurrent is null || operationCurrent.ReadHexTableReference(next) is not { } nextHex ||
                    operationCurrent.ReadHexForceSlot(nextHex) is not byte owner ||
                    army.NativeArmyOrigin?.NativeSlotIndex is not int physical) return Missing("next_territory_current");
                bool? hostile = ArmyLogisticsPreparation.ReadNativeRelationGate(working,
                    operationCurrent.ReadArmyUnitRawForceSlot(new(physical)), owner);
                if (hostile is null) return Missing("next_territory_relation");
                if (hostile.Value)
                {
                    var preparation = new ArmyMarchPreparationInputs(input.ArmyValid, input.Mobility2B0660,
                        input.SpeedScale2695D0, input.Penalty2B5C10, input.CachedMinimumTerrainCost1A24010,
                        input.CurrentTerrainPointerAvailable, input.SourceProvenance);
                    bool? rerouted = RetryType2Next();
                    if (rerouted is null) return Missing("hostile_next_301DD0_retry");
                    if (!rerouted.Value) return new(formerCredit, working);
                    input = queries.ReadOrdinaryDestinationInputs(working, army, moment, preparation);
                    if (!UsableInputs(input)) return Missing("hostile_retry_destination");
                    working = input.PreparedState ?? working;
                    objectTarget = objectTarget || input.DestinationHexObjectWord1A != 0;
                    if (working.ArmyMapOccupancy?.HasArmyMarker(next) is not bool occupiedAfterRetry ||
                        !occupiedAfterRetry && !objectTarget && input.DestinationHexObjectWord1A != 0 &&
                            crossings?.ReadDirectBaseEntryStep(working, army, next) != true)
                        return Missing("hostile_retry_2D99C0_destination");
                    finalOccupied = occupiedAfterRetry;
                }
            }
        }
        ushort credit = ArmyMarchSourceMath.AccumulateCredit(formerCredit, reachedSpeed, input.DestinationTerrainCostBDDD0);
        if (finalOccupied)
            return crossings?.TryWaitForOccupiedArmy(working, working.Units[army.Id], credit, input.DestinationTerrainCostBDDD0, moment, diagnosticTarget)
                ?? Missing("runtime.occupied_wait_owner");
        if (objectTarget && input.DestinationHexObjectWord1A != 0)
            return crossings?.TryResolveObjectDestination(working, working.Units[army.Id], credit,
                input.DestinationTerrainCostBDDD0, moment, diagnosticTarget)
                ?? Missing("runtime.Object_destination_owner");
        if (credit >= input.DestinationTerrainCostBDDD0)
            return crossings?.TryCross(working, working.Units[army.Id], credit, input.DestinationTerrainCostBDDD0, moment, diagnosticTarget)
                ?? Missing("runtime.crossing_owner");
        return new(credit, ReferenceEquals(working, state) ? null : working);

        bool? RetryType2Next()
        {
            if (army.NativeTacticRuntime?.Status10 != 2) return null;
            var cleared = army with { March = army.March! with { NextStep = ArmyMarchDestination.None } };
            var draft = working with { Units = new(working.Units) { [army.Id] = cleared } };
            var prepared = queries.PrepareOrdinaryNextStep(draft, cleared, moment);
            if (prepared is null || !prepared.Units.TryGetValue(army.Id, out var routed) ||
                ArmyMarchSourceAdapter.EncodeDestination(routed.March?.NextStep) is not ushort rewritten ||
                rewritten > ArmyMarchSourceAdapter.SourceHexCount) return null;
            working = prepared; army = routed; next = rewritten;
            return next != ArmyMarchSourceAdapter.SourceHexCount;
        }
        ArmyMovementResolution? Missing(string branch) =>
            MissingCurrent(working, army, moment, branch, diagnosticTarget);
    }
    //Failure-only scalar context. Never captures, serializes or scans a world.
    internal static InvalidDataException MissingBranch(GameState state, UnitState army,
        NativeArmyProgressStageMoment? moment, string branch, ushort? effectiveTarget = null,
        int? attemptedCredit = null, int? destinationCost = null)
    {
        byte? action = ArmyConfigurationSourceAdapter.ReadAction(state, army);
        ushort? at = ArmyMarchSourceAdapter.ReadCurrentHex(state, army);
        ushort? next = ArmyMarchSourceAdapter.EncodeDestination(army.March?.NextStep);
        ushort? marchTarget = ArmyMarchSourceAdapter.EncodeDestination(army.March?.Target);
        return new InvalidDataException($"army.march.missing.{branch};cycle={moment?.CalendarCycleOrdinal};" +
            $"Army={army.Id};action={action};type={army.NativeTacticRuntime?.Status10};" +
            $"current={at};next={next};credit={army.March?.MovementCredit};" +
            $"target={effectiveTarget};marchTarget={marchTarget};destination={army.DestinationHexId};" +
            $"attemptedCredit={attemptedCredit};destinationCost={destinationCost}");
    }
    private ArmyMovementResolution? MissingCurrent(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment, string branch, ushort? target = null)
    {
        if (operationCurrent is not null) throw MissingBranch(state, army, moment, branch, target);
        return null;
    }
    private static bool UsableInputs([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] ArmyMarchCurrentInputs? input) =>
        input is { ArmyValid: true, CurrentTerrainPointerAvailable: true, DestinationHexValid: true,
            CachedMinimumTerrainCost1A24010: > 0 and < int.MaxValue, DestinationTerrainCostBDDD0: > 0 } &&
        !string.IsNullOrWhiteSpace(input.SourceProvenance);
    private static bool HasCurrentRules(UnitState army) => army.NativeArmyOrigin is { } origin &&
        origin.Language is "SC" or "TC" && origin.RuleVersion == SimulationRuleIdentity.Version;
    private static void ValidateCurrentMoment(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (state.NativeMapSeason is not { } season || moment.CycleWithinAtomicBlock is < 0 or > 2 ||
            moment.CalendarCycleOrdinal != checked(season.AdvancedCalendarSteps + moment.CycleWithinAtomicBlock) ||
            state.NativeClock?.UpdateOrdinal != checked(season.ProcessedNativeOrdinal + (moment.CycleWithinAtomicBlock == 2 ? 1 : 0)))
            throw new InvalidDataException("March source moment does not belong to the current owned calendar block.");
        MapSeasonRuntime.ValidateOwned(season);
        var date = season.ThirdDate;
        for (int i = 0; i < moment.CycleWithinAtomicBlock; i++) date = MapSeasonRuntime.StepCalendarWriter(date).Date;
        if (date != moment.PreWriterThirdDate)
            throw new InvalidDataException("March source moment has another pre-writer third date.");
    }
}
