using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One selected16DF00/1753E0 actor, captured before any actor's
/// effects. The primary actor is owned by the caller and is not repeated here.</summary>
internal sealed record AutomaticLinkedActorCapture(int UnitId, int PhysicalSlot, int SourceHex,
    AutomaticTacticBaseCandidateSource Selected);

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Complete ordinary16DF00: collect the main local list, expand
    /// only its first snapshot once, remove duplicate receiver pointers, sort
    /// by distance/commander Leadership/native ordinal, then1753E0's linked
    /// trials and random choice among nonempty current tactic vectors.
    /// All samples are pure3FCA40; mutable campaign random state is unchanged.</summary>
    internal AutomaticLinkedActorCapture[] CaptureAutomaticLinkedActorsCurrent(GameState state,
        SourceWorldContext current, int mainUnitId, int primaryHex,
        NativeMapSeasonDate? operationDate = null, bool manual = false)
    {
        if (!ReferenceEquals(current, _definitions) || !Bound(state) ||
            !state.Units.TryGetValue(mainUnitId, out var main) || ArmyValid(state, main) != true)
            throw LinkedActorMissing("main_current_receiver");
        int center = Hex(main);
        var candidates = new List<ArmyChainCurrentCandidate>();
        var seen = new HashSet<int>();
        Append(main);
        var first = candidates.ToArray(); //7BDC20 copies the initial list.
        foreach (var neighbor in first) Append(Image(neighbor));
        if (candidates.Count == 0) return [];

        //167390 is stable insertion over the owned list. Do not replace
        //the last key with gameplay UnitId or sort only by encounter order.
        for (int index = 1; index < candidates.Count; index++)
        {
            var value = candidates[index];
            int at = index;
            while (at > 0 && Before(value, candidates[at - 1]))
            { candidates[at] = candidates[at - 1]; at--; }
            candidates[at] = value;
        }
        int mainPerson = Person(main);
        var player = current.ReadCurrentPlayerForce() ?? throw LinkedActorMissing("World_player_Force");
        var mainForce = current.ReadPersonForce(mainPerson) ?? throw LinkedActorMissing("main_Person_Force");
        bool own = Need(current.ReadForcePhysicalSlot(mainForce), "main_Person_Force_physical") ==
            Need(current.ReadForcePhysicalSlot(player), "player_Force_physical");
        int difficulty = 0;
        if (own)
        {
            int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
            difficulty = Math.Min(2, Need(BattleRemakeCurrentSettings.ReadInt32(state,
                selector == 0 ? 0x134 : 0x28), "settings_difficulty"));
            if (difficulty < 0) throw LinkedActorMissing("negative_difficulty");
        }
        var selected = new List<AutomaticLinkedActorCapture>();
        foreach (var candidate in candidates)
        {
            var actor = Image(candidate);
            int person = Person(actor);
            if (Need(current.ReadPersonIdentity(person), "candidate_Person10") ==
                Need(current.ReadPersonIdentity(mainPerson), "main_Person10")) continue;
            int formation = EffectiveFormation(state, actor) ?? throw LinkedActorMissing("candidate_effective_formation");
            var available = AvailableTactics(state, actor, formation) ?? throw LinkedActorMissing("candidate_available_tactics");
            if (available.Length == 0) continue;
            if (!manual)
            {
                bool waiting = ReadSelectedCommanderWaitingMode(state, actor, operationDate)
                    ?? throw LinkedActorMissing("candidate_selected_commander_waiting_mode");
                if (waiting && (Need(current.ReadArmyReturnPersonFlags196(person), "candidate_Person_flags") & 0x20) == 0)
                    continue;
            }
            int tier = Need(current.ReadDestructionRelationTier2A6EE0(mainPerson, person, 1), "root_commander_relation_tier");
            if (tier is < 1 or > 5) continue;
            float rate = LinkedActorRate(state, own, difficulty, tier);
            if (Need(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "advanced_settings"))
                rate = (float)Need(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 0x19), "advanced_linked_rate25") * rate / 100f;
            rate = rate <= 100f ? rate : 100f;
            bool accepted = rate > 0f && Sample(10000, candidate.PhysicalSlot, selected.Count) < (int)(rate * 10000f / 100f);
            bool forced = TacticRemakeCurrentBattleControl.Read(state, 73)
                ?? throw LinkedActorMissing("tutorial_control_bit73");
            if (forced) accepted = true;
            if (!accepted) continue;
            int sourceHex = Hex(actor);
            var usable = new List<AutomaticTacticBaseCandidateSource>();
            foreach (int tactic in available)
            {
                var row = _catalog.Rows[tactic];
                var program = ReadCurrentProgram(state, row) ?? throw LinkedActorMissing("candidate_tactic_program");
                if (program.Instructions.Count != 2 || !program.Instructions.Select(value => value.Index).SequenceEqual([0, 1]))
                    throw LinkedActorMissing("candidate_two_instruction_program");
                var vectors = program.Instructions.Select(instruction =>
                    ResolveAutomaticInstructionHexTargets(state, actor, row, instruction, sourceHex, primaryHex)
                    ?? throw LinkedActorMissing("candidate_current_Hex_vectors")).ToArray();
                bool tutorialAllowed = TutorialTacticEligibilitySource.AllowsCurrent(state, program)
                    ?? throw LinkedActorMissing("tutorial_current_lesson499BC0");
                if (!tutorialAllowed || vectors.All(vector => vector.OrderedTargets.Length == 0)) continue;
                usable.Add(new(tactic, program, vectors));
            }
            if (usable.Count == 0) continue;
            var chosen = usable[Sample(usable.Count, candidate.PhysicalSlot, selected.Count)];
            selected.Add(new(actor.Id, candidate.PhysicalSlot, sourceHex, chosen));
            if (selected.Count == 10) break;
        }
        return selected.ToArray();

        void Append(UnitState source)
        {
            var gathered = ArmyChainCurrentCandidates.Read(state, current, source);
            if (gathered.Count is null) throw LinkedActorMissing(gathered.UnresolvedOwner ?? "local_candidate_list");
            foreach (var candidate in gathered.ReachedPrefix)
                if (seen.Add(candidate.PhysicalSlot)) candidates.Add(candidate);
        }
        UnitState Image(ArmyChainCurrentCandidate candidate)
        {
            var actor = state.Units.GetValueOrDefault(candidate.UnitId) ?? throw LinkedActorMissing("candidate_image");
            if (actor.NativeArmyOrigin?.NativeSlotIndex != candidate.PhysicalSlot)
                throw LinkedActorMissing("candidate_receiver_identity");
            return actor;
        }
        int Person(UnitState actor)
        {
            var army = current.ReadArmyUnitTableReference(Need(actor.NativeArmyOrigin?.NativeSlotIndex, "Army_physical"))
                ?? throw LinkedActorMissing("Army_pointer");
            ushort raw = Need(current.ReadArmyUnitCommanderId(army), "Commander12");
            var person = raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0);
            int physical = Need(person?.PhysicalSlot, "Commander_pointer");
            if (!Need(current.ReadPersonNativeValidity(physical), "Commander_validity")) throw LinkedActorMissing("invalid_Commander");
            return physical;
        }
        int Hex(UnitState actor)
        {
            int raw = Need(ArmyMarchSourceAdapter.ReadCurrentHex(state, actor), "Army2A");
            return Need(current.ReadHexTableReference(raw < 48400 ? raw : 0)?.PhysicalSlot, "Hex_physical");
        }
        bool Before(ArmyChainCurrentCandidate firstCandidate, ArmyChainCurrentCandidate secondCandidate)
        {
            var a = Image(firstCandidate); var b = Image(secondCandidate);
            int firstDistance = LinkedDistance(center, Hex(a)), secondDistance = LinkedDistance(center, Hex(b));
            if (firstDistance != secondDistance) return firstDistance < secondDistance;
            if (_abilities is not TacticRemakeCurrentAbilities abilities) throw LinkedActorMissing("current_Leadership_provider");
            int firstAbility = abilities.ResolveDimension(state, Need(current.ReadPersonIdentity(Person(a)), "first_Commander10"), 0)
                ?? throw LinkedActorMissing("first_current_Leadership");
            int secondAbility = abilities.ResolveDimension(state, Need(current.ReadPersonIdentity(Person(b)), "second_Commander10"), 0)
                ?? throw LinkedActorMissing("second_current_Leadership");
            return firstAbility != secondAbility ? firstAbility > secondAbility : firstCandidate.PhysicalSlot < secondCandidate.PhysicalSlot;
        }
        int Sample(int bound, int physical, int ordinal)
            => TacticEffectSourceSamplerMath.Sample(bound, 1, physical, ordinal, 0, 0,
                bound < 2 ? null : ReadCurrentSamplerWorld(state) ?? throw LinkedActorMissing("World450_45C"));
    }

    internal ushort ReadAutomaticActorWaitingContributionCurrent(GameState state, int tacticSlot)
        => Bound(state) ? _programs.ReadCurrentWaitingContribution(_catalog.Rows[tacticSlot])
            ?? throw LinkedActorMissing("current_tactic42") : throw LinkedActorMissing("current_context");

    //173CF2 captures this from the MAIN tactic/source Force before16DF00.
    //Every actor later receives this same reduction, regardless of its tactic.
    internal int ReadAutomaticLinkedWaitingPolicyCurrent(GameState state, UnitState main, int tacticSlot, bool manual = false)
    {
        if (manual) return 0; //174020 has no category policy subtraction.
        var force = _definitions.ReadForceTableReference(main.ForceId) ?? throw LinkedActorMissing("main_Force_pointer");
        if (!Need(_definitions.ReadForceNativeValidity(force), "main_Force_validity")) return 0;
        var tactic = _definitions.ReadSkillTacticReference(tacticSlot) ?? throw LinkedActorMissing("main_tactic_pointer");
        byte category = Need(_definitions.ReadSkillTacticRawByte(tactic, 0x40), "main_tactic_category40");
        return category is 5 or 6 ? Policy(state, main.ForceId, category == 5 ? 0x24 : 0x25)
            ?? throw LinkedActorMissing("main_category_policy") : 0;
    }

    internal NativeTacticWaitingAssignment ResolveLinkedActorActivationWaitingCurrent(GameState state,
        UnitState actor, int tacticSlot, int waitingSum, int linkedCount, int categoryPolicyReduction, bool manual = false)
    {
        if (linkedCount is < 0 or > 10 || ArmyValid(state, actor) != true)
            throw LinkedActorMissing("waiting_current_actor_or_chain_count");
        int formation = EffectiveFormation(state, actor) ?? throw LinkedActorMissing("waiting_formation");
        byte flags = state.NativeArmySimulation!.Forces.GetValueOrDefault(actor.ForceId)?.Flags193
            ?? throw LinkedActorMissing("waiting_Force193");
        string? key = (manual, formation) switch
        {
            (false, 1) => SimulationParameterKeys.Tactics.Formation1AutomaticWaitingAdjustment,
            (false, 2) => SimulationParameterKeys.Tactics.Formation2AutomaticWaitingAdjustment,
            (false, 14) => SimulationParameterKeys.Tactics.Formation14AutomaticWaitingAdjustment,
            (true, 1) => SimulationParameterKeys.Tactics.Formation1ManualWaitingAdjustment,
            (true, 2) => SimulationParameterKeys.Tactics.Formation2ManualWaitingAdjustment,
            (true, 14) => SimulationParameterKeys.Tactics.Formation14ManualWaitingAdjustment, _ => null
        };
        int adjustment = key is null ? 0 : P(key);
        int reduction = (flags & 0x40) != 0 ? P(SimulationParameterKeys.Tactics.ForceFlagWaitingReduction) : 0;
        var assigned = TacticEffectSourceWaiting.AfterActivation(new(waitingSum, linkedCount, adjustment,
            reduction, manual ? 0 : categoryPolicyReduction, P(SimulationParameterKeys.Tactics.PrimaryWaitingCap),
            P(SimulationParameterKeys.Tactics.SecondaryWaitingCap), P(SimulationParameterKeys.Tactics.TertiaryWaitingCap)));
        return new(assigned.Primary20, assigned.Second21, assigned.Third22, $"linked-current:{Revision}");
        int P(string parameter) => Parameter(state, parameter) ?? throw LinkedActorMissing(parameter);
    }

    private static int LinkedDistance(int first, int second)
    {
        int x1 = first % 220, y1 = first / 220, x2 = second % 220, y2 = second / 220;
        int dx = Math.Abs(x2 - x1), dy = Math.Abs(2 * (y2 - y1) - (x1 % 2 == 0 ? 1 : 0) + (x2 % 2 == 0 ? 1 : 0));
        return dy < dx ? dx : dx + (dy - dx) / 2;
    }
    internal static string LinkedActorRateKey(bool player, int difficulty, int tier)
        => $"simulation.tactics.linked.{(player ? "player" + difficulty : "nonplayer")}.relation{tier}-percent";
    private static int LinkedActorRate(GameState state, bool player, int difficulty, int tier)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw LinkedActorMissing("parameters");
        string key = LinkedActorRateKey(player, difficulty, tier);
        return !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
            ? value : throw LinkedActorMissing(key);
    }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw LinkedActorMissing(field);
    private static InvalidDataException LinkedActorMissing(string field) => new("automatic_linked_actor." + field);
}
