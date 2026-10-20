using San14.Core;

namespace San14.Data;

public sealed record ArmyMovementManagerActor(int UnitId, int NativePoolSlot);
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
    /// <summary>The reached full-minimum3014C0 leaf for Next=None uses the
    /// actual fallback Hex0 receiver. Other fallback branches stay unresolved.</summary>
    bool? ReadOrdinaryNoStepFallback(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage8Moment) => null;
    ArmyMarchCurrentInputs? ReadOrdinaryCurrentInputs(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage8Moment);
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
    public ArmyMarchSourceRuntime(IArmyMarchCurrentQueries currentQueries, ArmyMarchCrossingSource? crossingSource = null)
    {
        queries = currentQueries ?? throw new ArgumentNullException(nameof(currentQueries));
        crossings = crossingSource;
    }

    public ArmyMovementQueue? PrepareStage7(GameState state, NativeArmyProgressStageMoment moment)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(moment);
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.PrepareMovement || moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Movement source list must be prepared at its reached stage7.");
        ValidateCurrentMoment(state, moment);
        var chain = queries.ReadCompleteCurrentManager60Chain(state, moment);
        if (chain is null) return null;
        var expected = state.Units.Values.Where(u => u.NativeArmyOrigin is not null).Select(u => u.Id).ToArray();
        if (chain.Any(a => a is null) || chain.Select(a => a.UnitId).Distinct().Count() != expected.Length ||
            chain.Select(a => a.NativePoolSlot).Distinct().Count() != chain.Count ||
            chain.Count != expected.Length || !chain.Select(a => a.UnitId).Order().SequenceEqual(expected.Order()))
            throw new InvalidDataException("Source stage7 requires its complete current manager chain, without ID sorting as an ordering substitute.");
        var selected = new List<ArmyMovementQueueActor>();
        foreach (var actor in chain)
        {
            UnitState army = state.Units[actor.UnitId];
            if (!HasCurrentRules(army)) return null;
            if (army.NativeArmyOrigin?.NativeSlotIndex != actor.NativePoolSlot)
                throw new InvalidDataException("Source stage7 chain names another native Army owner.");
            bool? eligible = queries.ReadSourceMovementEligibility2C7350(state, army, moment);
            if (eligible is null) return null;
            if (!eligible.Value) continue;
            if (army.March is not { MovementCredit: int credit } march ||
                ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort hex || hex != army.HexId) return null;
            ArmyReceiverId? receiver = state.ReadArmyLifecycle() is { } arena
                ? arena.ReceiverSlots().Single(slot => slot.ActiveUnitId == army.Id).ReceiverId : null;
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
            army.OverrideTarget?.Kind is not (ArmyOverrideTargetKind.None or ArmyOverrideTargetKind.ArmyReceiver) ||
            next > ArmyMarchSourceAdapter.SourceHexCount)
            return null;
        var working = state;
        //2D99C0 consumes a first waypoint already at the current position
        //before reading its effective target or invoking any path preparation.
        if (ArmyMarchSourceAdapter.EncodeDestination(march.Target) == current)
        {
            if (ArmyWaypointArrivalSource.Consume(state, army) is not { } advanced) return null;
            army = army with { March = advanced };
            working = state with { Units = new(state.Units) { [army.Id] = army } };
        }
        if (OrdinaryArmyRouteSource.ReadCurrentTarget(working, army) is not ushort target) return null;
        if (target == ArmyMarchSourceAdapter.SourceHexCount) return new(formerCredit, working);
        if (target > ArmyMarchSourceAdapter.SourceHexCount || target == current) return null;
        if (next == ArmyMarchSourceAdapter.SourceHexCount)
        {
            // Unknown Next was rejected above. Only the actual encoded None
            // reaches this prefix, before speed and3014C0.
            var prepared = queries.PrepareOrdinaryNextStep(working, army, moment);
            if (prepared is null || !prepared.Units.TryGetValue(army.Id, out var routed) ||
                ArmyMarchSourceAdapter.EncodeDestination(routed.March?.NextStep) is not ushort routedNext ||
                routedNext > ArmyMarchSourceAdapter.SourceHexCount) return null;
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
                        preparation.SpeedScale2695D0, preparation.Penalty2B5C10) is not int noStepSpeed) return null;
                working = preparation.PreparedState ?? working;
                if (unchecked(formerCredit + noStepSpeed) >= preparation.CachedMinimumTerrainCost1A24010 &&
                    queries.ReadOrdinaryNoStepFallback(working, working.Units[army.Id], moment) != true) return null;
                //Both proved3014C0 return1 leaves reach2D99C0's Next=None
                //exit before destination cost, credit accumulation and29DB90.
                return new(formerCredit, working);
            }
        }
        var input = queries.ReadOrdinaryCurrentInputs(working, army, moment);
        if (!UsableInputs(input)) return null;
        working = input.PreparedState ?? working;
        int? speed = ArmyMarchSourceMath.ResolveCycleSpeed(input.Mobility2B0660, input.SpeedScale2695D0, input.Penalty2B5C10);
        if (speed is not int reachedSpeed) return null;
        bool reachedMinimum = unchecked(formerCredit + reachedSpeed) >= input.CachedMinimumTerrainCost1A24010;
        if (next == current)
        {
            if (army.NativeTacticRuntime?.Status10 != 1 || input.DestinationHexObjectWord1A != 0) return null;
            if (!reachedMinimum)
            {
                ushort accumulated = ArmyMarchSourceMath.AccumulateCredit(formerCredit, reachedSpeed, input.DestinationTerrainCostBDDD0);
                //3014C0's early return preserves Next=current.2D99C0 then
                //takes the self-occupied branch, whose mandatory29DB90 tail
                //must complete even though no spatial step is taken.
                return crossings?.TryAccumulateAtCurrentHex(working, working.Units[army.Id], accumulated,
                    input.DestinationTerrainCostBDDD0);
            }
            var clearedArmy = working.Units[army.Id] with
            { March = working.Units[army.Id].March! with { NextStep = ArmyMarchDestination.None } };
            var cleared = working with { Units = new(working.Units) { [army.Id] = clearedArmy } };
            var prepared = queries.PrepareOrdinaryNextStep(cleared, clearedArmy, moment);
            if (prepared is null || !prepared.Units.TryGetValue(army.Id, out var routed) ||
                ArmyMarchSourceAdapter.EncodeDestination(routed.March?.NextStep) is not ushort routedNext ||
                routedNext > ArmyMarchSourceAdapter.SourceHexCount) return null;
            working = prepared; army = routed; next = routedNext;
            //3014C0 returns0 when the reached rewrite leaves Next=None.
            //That is a completed source branch: keep its clear/cache/pool
            //writes, but do not add credit or execute the blocked-step tail.
            if (next == ArmyMarchSourceAdapter.SourceHexCount) return new(formerCredit, working);
            if (next == current) return null;
            input = queries.ReadOrdinaryCurrentInputs(working, army, moment);
            if (!UsableInputs(input)) return null;
            working = input.PreparedState ?? working;
            //The original2695D0 speed was captured before3014C0. Only the
            //destination cost is re-read after a successful route replacement.
        }
        if (working.ArmyMapOccupancy?.HasArmyMarker(next) != false || input.DestinationHexObjectWord1A != 0)
            return null;
        //3014C0 has a real success branch beyond the minimum-cost early return:
        //an ordinary status1 actor with a distinct, unoccupied, object-free next
        //cell retains its selected step. Other reached route branches need owners.
        if (reachedMinimum && army.NativeTacticRuntime?.Status10 != 1) return null;
        ushort credit = ArmyMarchSourceMath.AccumulateCredit(formerCredit, reachedSpeed, input.DestinationTerrainCostBDDD0);
        if (credit >= input.DestinationTerrainCostBDDD0)
            return crossings?.TryCross(working, working.Units[army.Id], credit, input.DestinationTerrainCostBDDD0, moment);
        return new(credit, ReferenceEquals(working, state) ? null : working);
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
