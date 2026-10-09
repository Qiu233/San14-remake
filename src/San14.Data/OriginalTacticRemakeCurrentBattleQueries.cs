using San14.Core;

namespace San14.Data;

/// <summary>Remaining source factors with independent current producers.
/// Missing methods stay unknown; implementations may not substitute displayed
/// Core morale, region identity, difficulty, or selected-target count.</summary>
public interface IOriginalTacticRemakeCurrentBattleFactors
{
    bool? ReadAlternateMoraleGlobals(GameState state, UnitState army) => null;
    float? ReadRegionFactor(GameState state, UnitState army) => null;
    float? ReadNeighborFactor(GameState state, UnitState army) => null;
}

public sealed class OriginalTacticRemakeCurrentBattleFactors : IOriginalTacticRemakeCurrentBattleFactors
{
    private readonly OriginalTacticRemakeCurrentContext context;
    private readonly OriginalBattleRemakeCurrentRegionFactor region;
    public OriginalTacticRemakeCurrentBattleFactors(OriginalTacticRemakeCurrentContext current,
        OriginalEventInitializationContext definitions, OriginalBattleRemakeCurrentRegionParameterSeed seed, string revision)
    {
        context = current;
        region = new(seed, current, definitions, revision);
    }
    public bool? ReadAlternateMoraleGlobals(GameState state, UnitState army) =>
        context.ArmyValid(state, army) == true ? OriginalBattleRemakeCurrentSettings.ReadAlternateMoraleGlobals(state, army) : null;
    public float? ReadRegionFactor(GameState state, UnitState army) => region.Resolve(state, army);
    public float? ReadNeighborFactor(GameState state, UnitState army) => context.ReadNativeEmptyNeighborFactor(state, army);
}

/// <summary>Production Army facts shared by the current normal-attack pipeline
/// and the tactic primary gate. All queries consume one operation capture and
/// saved current owners. Unclosed factors reject the complete damage receipt.</summary>
public sealed class OriginalTacticRemakeCurrentBattleQueries :
    IOriginalBattleRemakeCurrentAttributeQueries, IOriginalBattleRemakeCurrentDamageBodyQueries
{
    private readonly OriginalTacticRemakeCurrentContext context;
    private readonly OriginalEventInitializationContext definitions;
    private readonly IOriginalTacticRemakeCurrentBattleFactors? factors;
    private readonly OriginalBattleRemakeCurrentDamageBodyQueries body;
    private readonly IReadOnlyDictionary<int, OriginalNormalArmyCombatDescriptor>? phaseDescriptors;
    public string Revision { get; }
    public OriginalTacticRemakeCurrentBattleQueries(OriginalTacticRemakeCurrentContext current,
        OriginalEventInitializationContext currentDefinitions, string revision,
        IOriginalTacticRemakeCurrentBattleFactors? remainingFactors = null,
        IReadOnlyDictionary<int, OriginalNormalArmyCombatDescriptor>? currentPhaseDescriptors = null)
    {
        context = current ?? throw new ArgumentNullException(nameof(current));
        definitions = currentDefinitions ?? throw new ArgumentNullException(nameof(currentDefinitions));
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Current battle queries need a revision.");
        Revision = revision; factors = remainingFactors;
        phaseDescriptors = currentPhaseDescriptors?.ToDictionary(row => row.Key, row => row.Value with { });
        body = new(context, definitions, revision);
    }
    public bool? ReadArmyValidity(GameState state, UnitState army) => context.ArmyValid(state, army);
    public int? ReadSelectedFormationPhysicalSlot(GameState state, UnitState army) => context.EffectiveFormation(state, army);
    public int? ReadArmyForcePhysicalSlot(GameState state, UnitState army) =>
        context.ArmyValid(state, army) == true && army.ForceId is >= 1 and <= 51 ? army.ForceId : null;

    public OriginalBattleRemakeCurrentCommonQueries? ReadCommonQueries(GameState state, UnitState army, UnitState opponent)
    {
        if (context.ArmyValid(state, army) != true || context.ArmyValid(state, opponent) != true ||
            ReadFinalDescriptorDivisor(state, army) is not byte divisor) return null;
        return ReadCommonQueriesForDescriptor(state, army, divisor);
    }

    /// <summary>Army Common fields after the source descriptor14/cache/effect55
    /// step has resolved its final divisor. A Base opponent needs this entry;
    /// it must not rescan Army-only candidates and lose its accepted targets.</summary>
    public OriginalBattleRemakeCurrentCommonQueries? ReadCommonQueriesForDescriptor(GameState state,
        UnitState army, byte finalDescriptorDivisor14)
    {
        if (context.ArmyValid(state, army) != true || finalDescriptorDivisor14 == 0 ||
            army.NativeMorale1A is not byte morale || context.ForceMoraleMaximum(state, army.ForceId) is not int cap ||
            cap is < 0 or > byte.MaxValue ||
            factors?.ReadAlternateMoraleGlobals(state, army) is not bool alternate ||
            factors.ReadRegionFactor(state, army) is not float region ||
            factors.ReadNeighborFactor(state, army) is not float neighbor ||
            definitions.ReadHexTableReference(army.HexId) is not { PhysicalSlot: int physical } hex ||
            physical != army.HexId || definitions.ReadHexSurfaceCode(hex) is not byte terrain) return null;
        return new(finalDescriptorDivisor14, Math.Min(morale, cap), alternate, region, terrain, neighbor, Revision);
    }

    /// <summary>1701AC stores the complete accepted count. Empty candidates
    /// remove the cached descriptor at1701E7; stage2 then uses its explicit1.
    /// Current active effect55 independently overrides that count to1.</summary>
    public byte? ReadFinalDescriptorDivisor(GameState state, UnitState army)
    {
        if (CurrentCommander(state, army) is not { } commander ||
            OriginalTacticRemakeCurrentTraits.CommanderImmune(definitions, commander.PhysicalSlot, 55) is not bool one) return null;
        byte count;
        if (phaseDescriptors is not null)
        {
            if (!phaseDescriptors.TryGetValue(army.Id, out var descriptor) || descriptor.ArmyId != army.Id ||
                descriptor.NativePoolSlot != army.NativeArmyOrigin?.NativeSlotIndex) return null;
            count = descriptor.AcceptedPrimaryCount14;
        }
        else
        {
            if (context.ResolveArmyNativePrimaryCandidates(state, army) is not { } candidates) return null;
            if (candidates.OrderedArmyIds.Length != 0 && candidates.OrderedArmyIds.Length % 256 == 0) return null;
            count = unchecked((byte)candidates.OrderedArmyIds.Length);
        }
        //169E67..169EA4 installs1 for a missing descriptor count, then reads
        //the phase cache. Effect55 remains a fresh commander query per pair.
        if (one || count == 0) return 1;
        return count;
    }
    public OriginalBattleRemakeCurrentFormationCoefficient? ReadFormationCoefficient(GameState state, UnitState army,
        int formationPhysicalSlot, OriginalBattleUnitArmyStatKind kind) =>
        context.ArmyValid(state, army) == true && context.EffectiveFormation(state, army) == formationPhysicalSlot
            ? OriginalBattleRemakeCurrentFormationCoefficientQueries.Read(state, formationPhysicalSlot, kind) : null;
    public int? ReadForceFormationEffect(GameState state, UnitState army, int nativeEffectIndex) =>
        context.ArmyValid(state, army) == true && state.Forces.TryGetValue(army.ForceId, out ForceState? force)
            ? OriginalBattleRemakeCurrentForcePolicies.ReadForceEffect(state, force, nativeEffectIndex) : null;
    public int? ReadCommandEffect(GameState state, UnitState army) => null;
    public int? ReadActiveForcePolicy(GameState state, UnitState army, int nativeEffectIndex) => null;
    public bool? ReadDoctrineAttackEligibility(GameState state, UnitState army) => null;

    public ushort? ReadRawProvocationTargetWord3E(GameState state, UnitState army)
    {
        return context.ArmyValid(state, army) == true ? army.NativeOverrideTarget?.Identity3E : null;
    }
    public bool? ReadCurvePermission(GameState state, UnitState caster, UnitState target) =>
        body.ReadCurvePermission(state, caster, target);
    public OriginalBattleRemakeCurrentCityDamageBranch? ReadAttackingCityBranch(GameState state, UnitState caster) =>
        body.ReadAttackingCityBranch(state, caster);
    public bool? ReadTargetHalfDamageTrait(GameState state, UnitState target) => body.ReadTargetHalfDamageTrait(state, target);

    private NativePersonAbilityRuntime? CurrentCommander(GameState state, UnitState army)
    {
        if (context.ArmyValid(state, army) != true || state.NativeArmySimulation?.Parameters is not { } parameters ||
            state.Officers.GetValueOrDefault(army.OfficerId)?.NativeAbilityRuntime is not { } commander ||
            commander.Revision != OriginalTacticRemakeCurrentAbilities.ProfileId ||
            commander.Language != parameters.Language || commander.RuleVersion != parameters.RuleVersion ||
            definitions.StaticSource is not { } source || commander.SourceScenarioUri != source.ResourceUri ||
            commander.SourceScenarioSha256 != source.Sha256 || definitions.ReadPersonIdentity(commander.PhysicalSlot) != army.OfficerId) return null;
        return commander;
    }
}
