using San14.Core;

namespace San14.Data;

public interface IBattleRemakeCurrentRegionRemaining
{
    bool? ReadNativeForceIsPlayer(GameState state, int nativeForceSlot) => null;
    int? ReadActiveForcePolicy(GameState state, int nativeForceSlot, int nativeEffectIndex) => null;
}

public sealed record BattleRemakeCurrentRegionReceipt(float Factor, int? SumOfAreaPercent,
    float? CurrentCoefficient, ushort? SavedHex152, int? AreaPhysicalSlot, string Revision);
public sealed record BattleRemakeCurrentHexRegionReceipt(float Factor, int? SumOfAreaPercent,
    float? CurrentCoefficient, ushort? CurrentHex2A, int? AreaPhysicalSlot, string Revision);

/// <summary>2A8FD0's reached region operand and the exact2B09B0 scalar float
/// pipeline. Saved Army152 selects the Hex; current saved Area68/76/78/66
/// supply the counters. Core.RegionId and the Army's current Hex do not select
/// its region. Unknown positive policy/player branches remain explicit.</summary>
public sealed class BattleRemakeCurrentRegionFactor
{
    private readonly BattleRemakeCurrentRegionParameterSeed seed;
    private readonly TacticRemakeCurrentContext context;
    private readonly SourceWorldContext definitions;
    private readonly IBattleRemakeCurrentRegionRemaining? remaining;
    public string Revision { get; }
    public BattleRemakeCurrentRegionFactor(BattleRemakeCurrentRegionParameterSeed sourceSeed,
        TacticRemakeCurrentContext current, SourceWorldContext currentDefinitions,
        string revision, IBattleRemakeCurrentRegionRemaining? unresolved = null)
    {
        seed = sourceSeed ?? throw new ArgumentNullException(nameof(sourceSeed));
        context = current ?? throw new ArgumentNullException(nameof(current));
        definitions = currentDefinitions ?? throw new ArgumentNullException(nameof(currentDefinitions));
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Current region factor revision is missing.");
        Revision = revision; remaining = unresolved;
    }
    public float? Resolve(GameState state, UnitState army) => ResolveReceipt(state, army)?.Factor;

    public BattleRemakeCurrentRegionReceipt? ResolveReceipt(GameState state, UnitState army)
        => ResolveReceiptCore(state, army, useCurrentHex: false);

    /// <summary>16B670 reaches the same2B09B0 scalar pipeline with the Army's
    /// current Hex2A. It does not write or substitute the saved region word152.</summary>
    public BattleRemakeCurrentHexRegionReceipt? ResolveCurrentHexReceipt(GameState state, UnitState army)
    {
        BattleRemakeCurrentRegionReceipt? result = ResolveReceiptCore(state, army, useCurrentHex: true);
        return result is null ? null : new(result.Factor, result.SumOfAreaPercent,
            result.CurrentCoefficient, result.SavedHex152, result.AreaPhysicalSlot, result.Revision);
    }

    private BattleRemakeCurrentRegionReceipt? ResolveReceiptCore(GameState state, UnitState army, bool useCurrentHex)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != seed.Language ||
            parameters.RuleVersion != seed.Source.RuleVersion || context.ArmyValid(state, army) != true ||
            state.Officers.GetValueOrDefault(army.OfficerId)?.NativeAbilityRuntime is not { } person ||
            person.Revision != TacticRemakeCurrentAbilities.ProfileId || person.Language != parameters.Language ||
            person.RuleVersion != parameters.RuleVersion || definitions.StaticSource is not { } source ||
            person.SourceScenarioUri != source.ResourceUri || person.SourceScenarioSha256 != source.Sha256 ||
            state.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256 || person.PhysicalSlot is < 1 or > 1850 ||
            definitions.ReadPersonLookupReference(army.OfficerId)?.PhysicalSlot != person.PhysicalSlot ||
            definitions.ReadPersonIdentity(person.PhysicalSlot) != army.OfficerId) return null;
        string revision = $"{Revision}:{parameters.Revision}" + (useCurrentHex ? ":current-hex" : "");
        ushort? selectedHex = useCurrentHex
            ? army.HexId is >= 0 and <= 48399 ? (ushort)army.HexId : null
            : army.NativeSavedRegionHex152;
        //232760/232F10 use the current Person's district-derived Force ID;
        //the current Army coherence guard has already bound that same owner.
        if (army.ForceId is >= 46 and <= 51) return One("special-force");
        //2334A0 first calls the pure2C70D0 ID bands. Ordinary607 returnsfalse
        //before reading firstLocation. A reached band reads that actual field.
        if (IsNativeBandPerson(army.OfficerId))
        {
            if (definitions.ReadPersonFirstLocation(person.PhysicalSlot) is not ushort first) return null;
            if (first is >= 1 and <= 61) return One("band-person");
        }
        return ResolveHexForceCore(state, army.ForceId, selectedHex, revision);

        BattleRemakeCurrentRegionReceipt One(string why) => new(1f, null, null,
            selectedHex, null, revision + ":" + why);
    }

    /// <summary>The actual2B09B0 Hex/Force receiver shared by Army and Base.
    /// Base callers do not execute the Army commander's special-person gates.</summary>
    public BattleRemakeCurrentRegionReceipt? ResolveHexForceReceipt(GameState state,
        int nativeForceSlot, ushort? currentHex)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != seed.Language ||
            parameters.RuleVersion != seed.Source.RuleVersion || !context.IsBoundCurrentState(state) ||
            definitions.StaticSource is not { } source ||
            state.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256) return null;
        return ResolveHexForceCore(state, nativeForceSlot, currentHex,
            $"{Revision}:{parameters.Revision}:hex-force");
    }

    private BattleRemakeCurrentRegionReceipt? ResolveHexForceCore(GameState state,
        int nativeForceSlot, ushort? selectedHex, string revision)
    {
        if (selectedHex is not ushort saved) return null;
        ArmySavedRegionHex? hex = ArmyCurrentRegionSource.ResolveSavedHex(saved, definitions);
        if (hex is null) return null;
        //Every nonnull CHex slot3 is true, including physical0. A known null
        //receiver is the source early-return1 branch of2B09B0.
        if (!hex.Hex.PhysicalSlot.HasValue) return One("null-saved-hex");
        if (nativeForceSlot is < 1 or > 51) return One("raw-force-range");
        ForceReference? force = definitions.ReadForceTableReference(nativeForceSlot);
        if (force is null || definitions.ReadForceNativeValidity(force) is not bool forceValid) return null;
        if (!forceValid) return One("invalid-force");
        AreaMapStatisticsState? snapshot = state.RegionStatistics;
        if (snapshot is null) return null;
        ArmySavedRegionArea? reached = ArmyCurrentRegionSource.ResolveReachedArea(hex, definitions);
        if (reached is null || definitions.ReadAreaNativeValidity(reached.Area) is not bool areaValid) return null;
        if (!areaValid) return One("invalid-area");
        if (reached.Area.PhysicalSlot is not int physical ||
            !snapshot.Entries.TryGetValue(physical, out var area)) return null;
        int sum = 0;
        bool? own = Eligible(area);
        if (own is null) return null;
        if (own.Value)
        {
            if (!area.IsOwnedCountCurrent) return null;
            sum = NativeAreaPercent(area.OwnedCellCount, area.TerrainCellCount);
        }
        for (int index = 0; index < 8; index++)
        {
            //The fixed definition mask proves this contribution absent. Its
            //source generic validity queries are pure; no counter is read.
            if ((area.NeighborMask & (1 << index)) == 0) continue;
            if (definitions.ReadAreaNeighborSlot(reached.Area, index) is not ushort requested) return null;
            AreaReference? neighbor = definitions.ReadAreaTableReference(requested <= 500 ? requested : 0);
            if (neighbor is null || definitions.ReadAreaNativeValidity(neighbor) is not bool valid) return null;
            if (!valid) continue;
            if (neighbor.PhysicalSlot is not int other ||
                !snapshot.Entries.TryGetValue(other, out var record)) return null;
            bool? eligible = Eligible(record);
            if (eligible is null) return null;
            if (eligible.Value)
            {
                if (!record.IsOwnedCountCurrent) return null;
                sum = unchecked(sum + NativeAreaPercent(record.OwnedCellCount, record.TerrainCellCount));
            }
        }
        if (sum == 0) return new(1f, 0, null, saved, physical, revision + ":zero-area-sum");
        if (Parameter(state, SimulationParameterKeys.BattleRegion.BaselineRegionCoefficient) is not int baseline ||
            BattleRemakeCurrentSettings.ReadSelectorE58(state) is not int selector) return null;
        float coefficient = baseline;
        if (selector != 0)
        {
            if (BattleRemakeCurrentSettings.ReadInt32(state, 0x28) is not int difficulty) return null;
            //For difficulty<3 both possible pure233B30 outcomes keep the same
            //coefficient; no guessed current-player owner is required.
            if (difficulty >= 3)
            {
                if (remaining?.ReadNativeForceIsPlayer(state, nativeForceSlot) is not bool player) return null;
                if (!player)
                {
                    if (Parameter(state, difficulty == 3 ? SimulationParameterKeys.BattleRegion.DifficultyThreeNonplayerCoefficient : SimulationParameterKeys.BattleRegion.HigherDifficultyNonplayerCoefficient) is not int selected) return null;
                    coefficient = selected;
                }
            }
        }
        if (Policy(70) is not int policy70) return null;
        coefficient = coefficient + (float)policy70;
        float factor = CalculateBase(sum, coefficient);
        if (selector != 0)
        {
            if (factor > 1f)
            {
                //3EB970 reads Environment.C4, not EC or a difficulty enum.
                if (BattleRemakeCurrentSettings.ReadInt32(state, 0xC4) is not int multiplierMode) return null;
                if (multiplierMode != 0)
                {
                    int multiplier;
                    if (multiplierMode is 1 or 2)
                    {
                        if (Parameter(state, multiplierMode == 1 ? SimulationParameterKeys.BattleRegion.RegionMultiplierModeOnePercent : SimulationParameterKeys.BattleRegion.RegionMultiplierModeTwoPercent) is not int selected) return null;
                        multiplier = selected;
                    }
                    else multiplier = 0;
                    factor = (float)multiplier * factor;
                    factor = factor / 100f;
                }
            }
        }
        else
        {
            if (remaining?.ReadNativeForceIsPlayer(state, nativeForceSlot) is not bool player ||
                BattleRemakeCurrentSettings.ReadAdvancedParameter(state, player ? 4 : 5) is not int percent) return null;
            factor = CalculateAdvanced(sum, coefficient, percent);
            //367EC0 release diagnostic is the independently proved read-only
            //settings/text chain. No game state/RNG update is omitted here.
        }
        return new(factor, sum, coefficient, saved, physical, revision);

        BattleRemakeCurrentRegionReceipt One(string why) => new(1f, null, null,
            selectedHex, null, revision + ":" + why);
        bool? Eligible(AreaMapStatisticsEntry candidate)
        {
            int forceId = candidate.ForceId ?? 0;
            if (forceId == nativeForceSlot) return true;
            if (Policy(39) is not int amount) return null;
            if (amount <= 0) return false;
            NativeArmyForceState? native = state.NativeArmySimulation!.Forces.GetValueOrDefault(nativeForceSlot);
            return native?.RelationsEA is { Length: 52 } relation && forceId <= 51
                ? (relation[forceId] & 0x7F) != 0 : null;
        }
        int? Policy(int effect)
        {
            NativeArmyForceState? native = state.NativeArmySimulation!.Forces.GetValueOrDefault(nativeForceSlot);
            if (native is null || state.Forces.GetValueOrDefault(nativeForceSlot)?.PolicyEffects?.IsEnabled(effect) is not bool enabled)
                return null;
            if (!enabled) return 0;
            return remaining?.ReadActiveForcePolicy(state, nativeForceSlot, effect);
        }
    }

    public static bool IsNativeBandPerson(int identity) => new[] { 0x100F, 0x1023, 0x1037, 0x104B, 0x105F }
        .Any(first => unchecked((uint)(identity - first)) <= 9);
    public static int NativeAreaPercent(int ownedCellCount, int terrainCellCount)
    {
        float value = (float)ownedCellCount / (float)terrainCellCount;
        value = value * 100f;
        return !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f ? int.MinValue : (int)value;
    }
    public static float CalculateBase(int percentSum, float coefficient)
    {
        float scaled = coefficient / 100f;
        float value = (float)percentSum * scaled;
        value = value / 100f;
        return value + 1f;
    }
    public static float CalculateAdvanced(int percentSum, float coefficient, int advancedPercent)
    {
        float scaled = coefficient / 100f;
        float value = (float)percentSum * scaled;
        value = value / 100f;
        value = value * (float)advancedPercent;
        value = value / 100f;
        return value + 1f;
    }
    private static int? Parameter(GameState state, string key) => state.NativeArmySimulation!.Parameters.Int32Values
        .TryGetValue(key, out int value) ? value : null;
}
