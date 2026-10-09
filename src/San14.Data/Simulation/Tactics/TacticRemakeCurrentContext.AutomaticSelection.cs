using San14.Core;

namespace San14.Data;

public enum AutomaticTacticSelectionStatus
{
    NativeGateSkipsTactic, NoEffectTargets, MultiCandidateScoringRequired, RateRejected, Selected
}

/// <summary>A finite automatic selection, not an effect/activation receipt.
/// A selected item retains both vectors for the later actual effect dispatcher.</summary>
public sealed record AutomaticTacticCurrentSelection(AutomaticTacticSelectionStatus Status,
    AutomaticTacticCurrentCandidates? Candidates,
    AutomaticTacticCandidateSource? SelectedCandidate,
    TacticAutomaticActivationCurrentTrial? Trial);

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Ordinary17D450/17D5D0 finite gate, then172C10's complete
    /// no-candidate/single-candidate path. Multi-candidate scoring remains
    /// explicitly unimplemented. This never enters the manual usage selector,
    /// writes a pending use, mutates gameplay RNG or dispatches effects.</summary>
    public AutomaticTacticCurrentSelection? ResolveAutomaticCurrentSelection(GameState state,
        NormalArmyCombatPair pair, BattleRemakeCurrentRegionFactor region)
    {
        ArgumentNullException.ThrowIfNull(pair); ArgumentNullException.ThrowIfNull(region);
        if (!state.Units.TryGetValue(pair.Attacker.ArmyId, out UnitState? caster) ||
            !state.Units.TryGetValue(pair.Target.ArmyId, out UnitState? primary) ||
            ArmyValid(state, caster) != true || ArmyValid(state, primary) != true ||
            caster.NativeArmyOrigin?.NativeSlotIndex != pair.Attacker.NativePoolSlot ||
            primary.NativeArmyOrigin?.NativeSlotIndex != pair.Target.NativePoolSlot ||
            state.CommanderSelection?.ActiveCount != 0 ||
            caster.NativeTacticRuntime is not { } native) return null;
        if (native.Status10 != 1) return Skipped();
        if (EffectiveFormation(state, caster) is not int formation ||
            AvailableTactics(state, caster, formation) is not int[] available) return null;
        if (available.Length == 0 || native.Waiting20 != 0 || native.Abnormal23 != 0) return Skipped();
        //For25==0 and actions outside4/5/7, either native target-equality
        //outcome passes. Other reached3C/3E/38/3A paths remain unknown here.
        if (native.Abnormal25 != 0 || caster.NativeArmyConfiguration?.Action37 is not byte action ||
            action is 4 or 5 or 7) return null;
        //17D4FF reaches the World player getter before checking selected mode.
        //Even when the known-empty commander list makes that mode false, a
        //missing actual Force receiver cannot be called a completed source gate.
        if (_definitions.ReadCurrentPlayerForce()?.PhysicalSlot is not int) return null;
        AutomaticTacticCurrentCandidates? candidates = ResolveAutomaticCurrentCandidates(state, pair);
        if (candidates is null) return null;
        if (candidates.CandidatesAfterTargetFiltering.Length == 0)
            return new(AutomaticTacticSelectionStatus.NoEffectTargets, candidates, null, null);
        if (candidates.RequiresNativeMultiCandidateScoring)
            return new(AutomaticTacticSelectionStatus.MultiCandidateScoringRequired, candidates, null, null);
        var selected = candidates.CandidatesAfterTargetFiltering[0];
        //Exactly one survivor skips scoring, not the source probability call.
        var trial = ResolveAutomaticTacticTrial(state, caster, primary, selected.TacticSlot, 5, region);
        if (trial is null) return null;
        return trial.Trial.Accepted
            ? new(AutomaticTacticSelectionStatus.Selected, candidates, selected, trial)
            : new(AutomaticTacticSelectionStatus.RateRejected, candidates, null, trial);

        static AutomaticTacticCurrentSelection Skipped() =>
            new(AutomaticTacticSelectionStatus.NativeGateSkipsTactic, null, null, null);
    }
}
