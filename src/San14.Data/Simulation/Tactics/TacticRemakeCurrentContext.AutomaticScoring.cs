using San14.Core;

namespace San14.Data;

internal sealed record AutomaticTacticCandidateScore(int TacticSlot, int Score);
internal sealed record AutomaticTacticBaseCurrentSelection(AutomaticTacticSelectionStatus Status,
    AutomaticTacticBaseCurrentCandidates Candidates,
    AutomaticTacticBaseCandidateSource[] OrderedCandidates,
    AutomaticTacticBaseCandidateSource? SelectedCandidate,
    TacticAutomaticActivationCurrentTrial[] Trials,
    AutomaticTacticCandidateScore[] Scores);

public sealed partial class TacticRemakeCurrentContext
{
    //Mutable current operands, not image defaults. No installer or missing-as-zero
    //fallback is introduced by this consumer. The source mapping is in README.
    internal const string AutomaticTargetCountScoreKey = "simulation.tactics.automatic-target-count-score";
    internal const string AutomaticTerrainContextScoreKey = "simulation.tactics.automatic-terrain-context-score";
    internal const string AutomaticEnemyNearOwnTerrainScoreKey = "simulation.tactics.automatic-enemy-near-own-terrain-score";
    internal const string AutomaticSpecialDefinitionScoreKey = "simulation.tactics.automatic-special-definition-score";

    /// <summary>Reached172EB0 ordering then16B910's first accepted trial.
    /// Uses the original captured actor Force and real operation hour. Neither
    /// this selector nor the pure sampler advances the mutable campaign RNG.
    /// A missing input invalidates the caller's unpublished operation.</summary>
    internal AutomaticTacticBaseCurrentSelection ResolveAutomaticBaseCurrentSelection(GameState state,
        NormalCombatMixedPair pair, AutomaticTacticBaseCurrentCandidates candidates,
        NativeMapSeasonDate operationDate, BattleRemakeCurrentRegionFactor region)
    {
        if (!Bound(state) || pair.Actor.Kind != 27 || pair.Target.Kind is not (5 or 6) ||
            pair.Actor.UnitId != candidates.SourceArmyId ||
            !state.Units.TryGetValue(candidates.SourceArmyId, out var caster) ||
            caster.NativeArmyOrigin?.NativeSlotIndex != pair.Actor.PhysicalSlot || ArmyValid(state, caster) != true)
            throw AutomaticScoreMissing("current_actor");
        var input = candidates.CandidatesAfterTargetFiltering;
        if (input.Length == 0) return Result(AutomaticTacticSelectionStatus.NoEffectTargets, [], null, [], []);
        var (ordered, orderedScores) = OrderAutomaticCurrentCandidates(state, input,
            pair.Actor.PhysicalSlot, pair.Actor.ForceSlot, operationDate, candidate => candidate);
        var target = new BaseReference(pair.Target.Kind == 5 ? BaseKind.City : BaseKind.Gate,
            pair.Target.PhysicalSlot);
        var trials = new List<TacticAutomaticActivationCurrentTrial>();
        int ordinal = 5;
        foreach (var candidate in ordered)
        {
            var trial = ResolveAutomaticTacticTrial(state, caster, target, candidate.TacticSlot, ordinal, region)
                ?? throw AutomaticScoreMissing("current_rate_trial");
            trials.Add(trial);
            if (trial.Trial.Accepted)
                return Result(AutomaticTacticSelectionStatus.Selected, ordered, candidate, trials.ToArray(), orderedScores);
            ordinal++;
        }
        return Result(AutomaticTacticSelectionStatus.RateRejected, ordered, null, trials.ToArray(), orderedScores);

        AutomaticTacticBaseCurrentSelection Result(AutomaticTacticSelectionStatus status,
            AutomaticTacticBaseCandidateSource[] sequence, AutomaticTacticBaseCandidateSource? selected,
            TacticAutomaticActivationCurrentTrial[] attempts, AutomaticTacticCandidateScore[] actualScores)
            => new(status, candidates, sequence, selected, attempts, actualScores);
    }

    //Shared172EB0 ordering for Army and Base primaries. This receives the
    //captured actor identity/Force and original two selected Hex vectors; it
    //does not capture, reselect targets, run rate trials or mutate any RNG.
    private (T[] Ordered, AutomaticTacticCandidateScore[] Scores) OrderAutomaticCurrentCandidates<T>(
        GameState state, T[] input, int actorPhysicalSlot, int actorForceSlot,
        NativeMapSeasonDate? operationDate, Func<T, AutomaticTacticBaseCandidateSource> readCandidate)
    {
        T[] ordered;
        var scores = new List<AutomaticTacticCandidateScore>();
        if (input.Length == 1) ordered = [input[0]]; //No difficulty/ordering query.
        else
        {
            int selector = BattleRemakeCurrentSettings.ReadSelectorE58(state)
                ?? throw AutomaticScoreMissing("current_settings_selector");
            int difficulty = BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28)
                ?? throw AutomaticScoreMissing("current_difficulty");
            if (difficulty < 4)
            {
                var remaining = input.ToList();
                var randomized = new List<T>(remaining.Count);
                int hour = operationDate?.Hour ?? throw AutomaticScoreMissing("current_operation_hour");
                var world = ReadCurrentSamplerWorld(state) ?? throw AutomaticScoreMissing("current_sampler_World450_45C");
                int counter = 0;
                //173ACD..AE1 sets all four salts explicitly, including signed
                //World38 hour and trailing0. Draw/removal order preserves repeats.
                while (remaining.Count != 0)
                {
                    int at = TacticEffectSourceSamplerMath.Sample(remaining.Count, 1,
                        actorPhysicalSlot, counter, hour, 0, world);
                    randomized.Add(remaining[at]); remaining.RemoveAt(at); counter++;
                }
                ordered = randomized.ToArray();
            }
            else
            {
                var ranked = new List<(T Candidate, int Score)>();
                foreach (var candidate in input)
                {
                    var vectors = readCandidate(candidate);
                    int score = Score(vectors);
                    scores.Add(new(vectors.TacticSlot, score));
                    //167600 moves only when previous.score < current.score:
                    //descending and stable for equal scores, including repeats.
                    int at = ranked.Count;
                    while (at > 0 && ranked[at - 1].Score < score) at--;
                    ranked.Insert(at, (candidate, score));
                }
                ordered = ranked.Select(value => value.Candidate).ToArray();
            }
        }
        return (ordered, scores.ToArray());

        int Score(AutomaticTacticBaseCandidateSource candidate)
        {
            int countCoefficient = Operand(AutomaticTargetCountScoreKey);
            int score = 0;
            foreach (var vector in candidate.Instructions)
                score = unchecked(score + unchecked(vector.OrderedTargets.Length * countCoefficient));
            byte flags = ReadCurrentTargetFlags(state, _catalog.Rows[candidate.TacticSlot])
                ?? throw AutomaticScoreMissing("current_definition_target_flags");
            bool terrainContext = false;
            if ((flags & 1) != 0)
                foreach (var vector in candidate.Instructions)
                {
                    int opcode = candidate.Program.Instructions[vector.InstructionIndex].Opcode;
                    if (opcode is not (1 or 17)) continue;
                    foreach (var targetHex in vector.OrderedTargets)
                        if (IsSpecialTerrain(Hex(targetHex.HexId))) { terrainContext = true; break; }
                    if (terrainContext) break;
                }
            if (!terrainContext)
                foreach (var vector in candidate.Instructions)
                {
                    int opcode = candidate.Program.Instructions[vector.InstructionIndex].Opcode;
                    if (opcode is not (7 or 9 or 18)) continue;
                    foreach (var targetHex in vector.OrderedTargets)
                    {
                        for (int direction = 1; direction <= 6; direction++)
                        {
                            var neighbor = Neighbor(targetHex.HexId, direction);
                            if (neighbor is not null && IsSpecialTerrain(neighbor) &&
                                Hostile(Need(_definitions.ReadHexForceSlot(neighbor), "neighbor_Hex14"), actorForceSlot))
                            { terrainContext = true; break; }
                        }
                        if (terrainContext) break;
                    }
                    if (terrainContext) break;
                }
            if (terrainContext) score = unchecked(score + Operand(AutomaticTerrainContextScoreKey));
            bool enemyNearOwnTerrain = false;
            foreach (var vector in candidate.Instructions)
            {
                int opcode = candidate.Program.Instructions[vector.InstructionIndex].Opcode;
                if (opcode is not (12 or 14)) continue;
                foreach (var targetHex in vector.OrderedTargets)
                {
                    var hex = Hex(targetHex.HexId);
                    int raw = unchecked((short)Need(_definitions.ReadHexRaw1C(hex), "target_Hex1C"));
                    if (raw < 0) throw AutomaticScoreMissing("signed_negative_2AF210_Army_lookup");
                    var army = _definitions.ReadArmyUnitTableReference(raw < 501 ? raw : 0)
                        ?? throw AutomaticScoreMissing("target_2AF210_Army_pointer");
                    //2AF210 returns the table pointer;172EB0 adds no native-validity gate.
                    var force = _definitions.ReadArmyUnitForceReference(army);
                    int forceId = Need(_definitions.ReadForcePhysicalSlot(force), "target_Army_Force");
                    if (!Hostile(forceId, actorForceSlot)) continue;
                    for (int direction = 1; direction <= 6; direction++)
                    {
                        var neighbor = Neighbor(targetHex.HexId, direction);
                        if (neighbor is not null && IsSpecialTerrain(neighbor) &&
                            Need(_definitions.ReadHexForceSlot(neighbor), "own_neighbor_Hex14") == actorForceSlot)
                        { enemyNearOwnTerrain = true; break; }
                    }
                    if (enemyNearOwnTerrain) break;
                }
                if (enemyNearOwnTerrain) break;
            }
            if (enemyNearOwnTerrain) score = unchecked(score + Operand(AutomaticEnemyNearOwnTerrainScoreKey));
            var reference = _definitions.ReadSkillTacticReference(candidate.TacticSlot)
                ?? throw AutomaticScoreMissing("current_Tactic_pointer");
            int physical = Need(_definitions.ReadSkillTacticPhysicalSlot(reference), "current_Tactic_physical");
            if (physical is >= 31 and <= 130 or >= 140 and <= 200)
                score = unchecked(score + Operand(AutomaticSpecialDefinitionScoreKey));
            return score;
        }
        HexReference Hex(int key) => _definitions.ReadHexTableReference(key < 48400 ? key : 0)
            ?? throw AutomaticScoreMissing("current_Hex_pointer");
        HexReference? Neighbor(int key, int direction)
        {
            int next = PersonReturnPathSource.Neighbor(key, direction);
            return next < 48400 ? Hex(next) : null; //2A7B30 returns null off map.
        }
        bool IsSpecialTerrain(HexReference hex) => Need(_definitions.ReadHexSurfaceCode(hex), "Hex10") is 16 or 17;
        bool Hostile(int first, int second) => Need(ArmyLogisticsPreparation.ReadNativeRelationGate(state, first, second), "2CA650");
        int Operand(string key)
        {
            var bank = state.NativeArmySimulation?.Parameters ?? throw AutomaticScoreMissing("current_parameter_owner");
            return !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
                ? value : throw AutomaticScoreMissing(key);
        }
        TValue Need<TValue>(TValue? value, string field) where TValue : struct => value ?? throw AutomaticScoreMissing(field);
    }
    private static InvalidDataException AutomaticScoreMissing(string field) => new("automatic_tactic_selection." + field);
}
