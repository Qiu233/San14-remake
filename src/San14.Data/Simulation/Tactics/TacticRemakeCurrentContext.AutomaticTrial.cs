using San14.Core;

namespace San14.Data;

public sealed record TacticAutomaticActivationCurrentTrial(int TacticSlot, byte NativeCategory40,
    int RateCategory, int EffectiveFormationSlot, int TerrainPhysicalSlot,
    int DifficultyOption, bool SourceForceIsPlayer, BattleRemakeCurrentHexRegionReceipt Region,
    TacticAutomaticActivationRateInput Input, TacticSourceSamplerWorld? SamplerWorld,
    TacticAutomaticActivationTrial Trial);

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Current Army→Army ordinary rate input producer. Only known
    /// tutorial header0==0 is supported. Missing player identity/current rate
    /// fields remain unresolved; this does not authorize automatic activation,
    /// reconstruct candidate lists or replay a past hour10 combat snapshot.</summary>
    public TacticAutomaticActivationCurrentTrial? ResolveAutomaticTacticTrial(GameState state,
        UnitState caster, UnitState target, int tacticSlot, int candidateOrdinal,
        BattleRemakeCurrentRegionFactor region)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (candidateOrdinal < 5) throw new ArgumentOutOfRangeException(nameof(candidateOrdinal));
        if (!Bound(state) || ArmyValid(state, caster) != true || ArmyValid(state, target) != true ||
            tacticSlot is < 0 or > 200 ||
            !state.NativeArmySimulation!.Parameters.Int32Values.TryGetValue(
                TacticRemakeCurrentBattleControl.VersionKey, out int version) || version != 1 ||
            !state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(
                TacticRemakeCurrentBattleControl.HeaderKey, out int header) || header != 0 ||
            _definitions.ReadSkillTacticReference(tacticSlot) is not { } tactic ||
            _definitions.ReadSkillTacticValidity(tactic) != true ||
            _definitions.ReadSkillTacticRawByte(tactic, 0x40) is not byte nativeCategory ||
            EffectiveFormation(state, caster) is not int formation ||
            CurrentSurfaceCode(caster) is not byte surfaceCode ||
            _definitions.ReadSurfaceTypeTableReference(surfaceCode <= 30 ? surfaceCode : 0) is not { PhysicalSlot: int terrain } surface)
            return null;
        int category = Math.Min((int)nativeCategory, 8);
        ushort? formationRate = TacticRemakeCurrentFormations.ReadRawWord(state, formation, 0x88 + 2 * category);
        ushort? terrainRate = _definitions.ReadSurfaceTypeTacticRateWord(surface, category);
        int? selector = BattleRemakeCurrentSettings.ReadSelectorE58(state);
        int? difficulty = selector is null ? null : BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28);
        ForceReference? sourceForce = _definitions.ReadForceTableReference(caster.ForceId);
        ForceReference? playerForce = _definitions.ReadCurrentPlayerForce();
        //This is the SC32F200/TC32EFA0 World reference comparison. The separate
        //region233B30 predicate, UI selection and command authority are not
        //substitutes for an unbound current World player receiver.
        bool? player = sourceForce is null || playerForce is null ? null : sourceForce == playerForce;
        if (formationRate is null || terrainRate is null || difficulty is null || player is null) return null;
        string baselineKey = player.Value ? difficulty switch
            { 0 => SimulationParameterKeys.Tactics.PlayerDifficulty0TrialRate, 1 => SimulationParameterKeys.Tactics.PlayerDifficulty1TrialRate, _ => SimulationParameterKeys.Tactics.PlayerDifficulty2TrialRate }
            : difficulty >= 4 ? SimulationParameterKeys.Tactics.HighDifficultyNonplayerTrialRate : SimulationParameterKeys.Tactics.NonplayerAutomaticTrialRate;
        if (Parameter(state, baselineKey) is not int baseline || region.ResolveCurrentHexReceipt(state, caster) is not { } multiplier)
            return null;
        var input = new TacticAutomaticActivationRateInput(formationRate.Value, baseline,
            terrainRate.Value, multiplier.Factor, caster.HexId, target.HexId, candidateOrdinal);
        float percent = TacticAutomaticActivationSourceMath.CalculateRate(input);
        TacticSourceSamplerWorld? world = TacticAutomaticActivationSourceMath.RequiresSample(percent)
            ? ReadCurrentSamplerWorld(state) : null;
        if (TacticAutomaticActivationSourceMath.RequiresSample(percent) && world is null) return null;
        return new(tacticSlot, nativeCategory, category, formation, terrain, difficulty.Value, player.Value,
            multiplier, input, world, TacticAutomaticActivationSourceMath.Trial(input, world));
    }
}
