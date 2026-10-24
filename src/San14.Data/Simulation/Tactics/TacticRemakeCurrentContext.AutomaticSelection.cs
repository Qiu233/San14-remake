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
    TacticAutomaticActivationCurrentTrial? Trial)
{
    internal AutomaticTacticCandidateSource[] OrderedCandidates { get; init; } = [];
    internal TacticAutomaticActivationCurrentTrial[] Trials { get; init; } = [];
    internal AutomaticTacticCandidateScore[] Scores { get; init; } = [];
}

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Ordinary17D450/17D5D0 finite gate, then172C10's complete
    /// candidate ordering and first accepted rate trial. A reached low-difficulty
    /// permutation requires the actual operation date supplied by the phase.
    /// This never enters the manual usage selector,
    /// writes a pending use, mutates gameplay RNG or dispatches effects.</summary>
    public AutomaticTacticCurrentSelection? ResolveAutomaticCurrentSelection(GameState state,
        NormalArmyCombatPair pair, BattleRemakeCurrentRegionFactor region)
        => ResolveAutomaticCurrentSelection(state, pair, region, out _);

    //Callers outside the phase may supply their actual pre-writer date. The
    //old overload remains available, but cannot invent the low-difficulty hour.
    public AutomaticTacticCurrentSelection? ResolveAutomaticCurrentSelection(GameState state,
        NormalArmyCombatPair pair, BattleRemakeCurrentRegionFactor region, NativeMapSeasonDate operationDate)
        => ResolveAutomaticCurrentSelection(state, pair, region, out _, operationDate);

    //The diagnostic comes from the already reached short-circuit, without
    //another selection, capture or probability draw.
    internal AutomaticTacticCurrentSelection? ResolveAutomaticCurrentSelection(GameState state,
        NormalArmyCombatPair pair, BattleRemakeCurrentRegionFactor region, out string? missingOwner,
        NativeMapSeasonDate? operationDate = null)
    {
        missingOwner = null;
        ArgumentNullException.ThrowIfNull(pair); ArgumentNullException.ThrowIfNull(region);
        if (!state.Units.TryGetValue(pair.Attacker.ArmyId, out UnitState? caster) ||
            !state.Units.TryGetValue(pair.Target.ArmyId, out UnitState? primary) ||
            ArmyValid(state, caster) != true || ArmyValid(state, primary) != true ||
            caster.NativeArmyOrigin?.NativeSlotIndex != pair.Attacker.NativePoolSlot ||
            primary.NativeArmyOrigin?.NativeSlotIndex != pair.Target.NativePoolSlot ||
            caster.NativeTacticRuntime is not { } native)
        { missingOwner = "automatic-selection.pair_identity_commander_selection_or_runtime"; return null; }
        if (native.Status10 != 1) return Skipped();
        if (EffectiveFormation(state, caster) is not int formation ||
            AvailableTactics(state, caster, formation) is not int[] available)
        { missingOwner = "automatic-selection.formation_or_available_tactics"; return null; }
        if (available.Length == 0 || native.Waiting20 != 0 || native.Abnormal23 != 0) return Skipped();
        //For25==0 and actions outside4/5/7, either native target-equality
        //outcome passes. Other reached3C/3E/38/3A paths remain unknown here.
        if (native.Abnormal25 != 0)
        { missingOwner = "automatic-selection.abnormal25_target_equality"; return null; }
        if (ArmyConfigurationSourceAdapter.ReadAction(state, caster) is not byte action)
        { missingOwner = "automatic-selection.action37"; return null; }
        if (action is 4 or 5 or 7)
        { missingOwner = "automatic-selection.action_" + action + "_target_equality"; return null; }
        bool? selectedGate = ReadSelectedCommanderAutomaticGate(state, caster, operationDate);
        if (selectedGate is null)
        { missingOwner = "automatic-selection.player_selected_commander_gate"; return null; }
        if (!selectedGate.Value) return Skipped();
        AutomaticTacticCurrentCandidates? candidates = ResolveAutomaticCurrentCandidates(state, pair, out var candidateMissing);
        if (candidates is null)
        { missingOwner = candidateMissing ?? "automatic-selection.candidate_inputs"; return null; }
        if (candidates.CandidatesAfterTargetFiltering.Length == 0)
            return new(AutomaticTacticSelectionStatus.NoEffectTargets, candidates, null, null);
        AutomaticTacticCandidateSource[] ordered;
        AutomaticTacticCandidateScore[] scores;
        try
        {
            (ordered, scores) = OrderAutomaticCurrentCandidates(state,
                candidates.CandidatesAfterTargetFiltering, pair.Attacker.NativePoolSlot,
                pair.Attacker.NativeForce10, operationDate, candidate =>
                    new AutomaticTacticBaseCandidateSource(candidate.TacticSlot, candidate.Program,
                        candidate.HexInstructions ?? throw AutomaticScoreMissing("original_Hex_vectors")));
        }
        catch (InvalidDataException error)
        { missingOwner = error.Message; return null; }
        var trials = new List<TacticAutomaticActivationCurrentTrial>();
        int ordinal = 5;
        foreach (var candidate in ordered)
        {
            //16B910 increments only after a rejected trial and stops on the
            //first accepted candidate. A single survivor skips ordering only.
            var trial = ResolveAutomaticTacticTrial(state, caster, primary, candidate.TacticSlot, ordinal, region);
            if (trial is null)
            { missingOwner = "automatic-selection.activation_trial_inputs.tactic_" + candidate.TacticSlot +
                ".ordinal_" + ordinal; return null; }
            trials.Add(trial);
            if (trial.Trial.Accepted)
                return Result(AutomaticTacticSelectionStatus.Selected, candidate, trial);
            ordinal++;
        }
        return Result(AutomaticTacticSelectionStatus.RateRejected, null, trials[^1]);

        AutomaticTacticCurrentSelection Result(AutomaticTacticSelectionStatus status,
            AutomaticTacticCandidateSource? selected, TacticAutomaticActivationCurrentTrial trial)
            => new(status, candidates, selected, trial)
            { OrderedCandidates = ordered, Trials = trials.ToArray(), Scores = scores };

        static AutomaticTacticCurrentSelection Skipped() =>
            new(AutomaticTacticSelectionStatus.NativeGateSkipsTactic, null, null, null);
    }
}
