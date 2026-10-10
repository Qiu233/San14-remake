using San14.Core;

namespace San14.Data;

public sealed record ArmyMovementManagerActor(int UnitId, int NativePoolSlot);
/// <summary>Results from the reached source owners, never displayed movement
/// or route budgets:2B0660 mobility,2B5C10 penalty,2695D0 current scale,
/// cached minimum1A24010 and current destination Terrain getterBDDD0.
/// Dynamic Hex1C is signed; Hex1A is an independent current object field.</summary>
public sealed record ArmyMarchCurrentInputs(bool ArmyValid, float Mobility2B0660,
    int SpeedScale2695D0, int Penalty2B5C10, int CachedMinimumTerrainCost1A24010,
    int DestinationTerrainCostBDDD0, bool CurrentTerrainPointerAvailable,
    bool DestinationHexValid, ushort DestinationHexObjectWord1A,
    string SourceProvenance);
public interface IArmyMarchCurrentQueries
{
    /// <summary>Queries borrow the supplied operation world for the duration of each call.</summary>
    IReadOnlyList<ArmyMovementManagerActor>? ReadCompleteCurrentManager60Chain(GameState state,
        NativeArmyProgressStageMoment stage7Moment);
    bool? ReadSourceMovementEligibility2C7350(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage7Moment);
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

/// <summary>Finite ordinary direct-target/insufficient-cost branch of source
///2D99C0. Reaching a cross-Hex commit, occupied destination, action8 or unknown
/// current owner rejects. This source does not claim pathfinding or full stage8.</summary>
public sealed class ArmyMarchSourceRuntime : IArmyMovementRules
{
    public const string Revision = "san14:ce-stage8-ordinary-insufficient-cost-march-20261003";
    private readonly IArmyMarchCurrentQueries queries;
    public ArmyMarchSourceRuntime(IArmyMarchCurrentQueries currentQueries)
        => queries = currentQueries ?? throw new ArgumentNullException(nameof(currentQueries));

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
        if (state.Units.GetValueOrDefault(army.Id) != army || army.NativeArmyOrigin is null ||
            !HasCurrentRules(army) ||
            army.March is not { } march || ArmyConfigurationSourceAdapter.ReadAction(state, army) is not byte action ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort current ||
            current != army.HexId || current >= ArmyMarchSourceAdapter.SourceHexCount ||
            ArmyMarchSourceAdapter.EncodeCredit(march) is not ushort formerCredit ||
            ArmyMarchSourceAdapter.EncodeDestination(march.Target) is not ushort target ||
            ArmyMarchSourceAdapter.EncodeDestination(march.NextStep) is not ushort next ||
            army.OverrideTarget?.Kind != ArmyOverrideTargetKind.None ||
            target >= ArmyMarchSourceAdapter.SourceHexCount || current == target || next >= ArmyMarchSourceAdapter.SourceHexCount || action == 8)
            return null;
        var input = queries.ReadOrdinaryCurrentInputs(state, army, moment);
        if (input is null || !input.ArmyValid || string.IsNullOrWhiteSpace(input.SourceProvenance) ||
            !input.CurrentTerrainPointerAvailable || !input.DestinationHexValid ||
            input.CachedMinimumTerrainCost1A24010 <= 0 || input.CachedMinimumTerrainCost1A24010 == int.MaxValue ||
            input.DestinationTerrainCostBDDD0 <= 0 ||
            state.ArmyMapOccupancy?.HasArmyMarker(next) != false || input.DestinationHexObjectWord1A != 0)
            return null;
        int? speed = ArmyMarchSourceMath.ResolveCycleSpeed(input.Mobility2B0660, input.SpeedScale2695D0, input.Penalty2B5C10);
        if (speed is not int reachedSpeed || unchecked(formerCredit + reachedSpeed) >= input.CachedMinimumTerrainCost1A24010)
            return null;
        ushort credit = ArmyMarchSourceMath.AccumulateCredit(formerCredit, reachedSpeed, input.DestinationTerrainCostBDDD0);
        if (credit >= input.DestinationTerrainCostBDDD0) return null;
        return new(credit);
    }
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
