using San14.Core;

namespace San14.Data;

/// <summary>The actual selected participants before duel registration. This
/// operation-local result does not make the campaign service active or ready.</summary>
public sealed record ArmyDuelSelectedPair(int ActorUnitId, int OpponentUnitId, int? AssistUnitId,
    int ActorPersonId, int OpponentPersonId, int? AssistPersonId,
    byte ActorReadiness, byte OpponentReadiness, int PlayerSide, int? CurrentSetting50);

public sealed partial class ArmyCampaignRuleAdapter
{
    private static InvalidDataException MissingDuelRegistration(ArmyDuelSelectedPair pair)
    {
        var error = DuelMissing("registration_374D20_complete_service_owner");
        error.Data[nameof(ArmyDuelSelectedPair)] = pair;
        return error;
    }
    private DuelWeightedArmyCandidate? BuildDuelOpponentCandidate(GameState state, SourceWorldContext current,
        ArmyUnitReference actor, ArmyUnitReference opponent, NativeArmyProgressStageMoment moment)
    {
        if (DuelCommander(current, opponent) is not int person) return null;
        int id = current.ReadPersonIdentity(person) ?? throw DuelMissing("opponent_weight_Person_identity");
        short adjustment = state.Officers.GetValueOrDefault(id)?.DuelCondition?.QualificationAdjustment
            ?? throw DuelMissing("opponent_weight_adjustment");
        int special;
        if (DuelHasTrait(current, person, 61)) special = DuelActorWeightParameters.Read(state, "effect29");
        else if (DuelHasTrait(current, person, 62)) special = P("trait62");
        else if (DuelHasTrait(current, person, 144))
        {
            int age = DuelTrait144Age(state, current, person, moment);
            special = age >= 60 ? P("older-trait144") : age >= 50 ? P("middle-trait144") : 0;
        }
        else special = (TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 29)
            ?? throw DuelMissing("opponent_weight_effect29")) ? DuelActorWeightParameters.Read(state, "effect29") : 0;
        int personality = DuelHasTrait(current, person, 1) ? P("trait1") : DuelHasTrait(current, person, 3) ? P("trait3") : 0;
        int actorPerson = DuelCommander(current, actor) ?? throw DuelMissing("opponent_weight_actor_commander");
        int relation = DuelPersonListContains(state, current, actorPerson, person, false) ? P("liked") :
            DuelPersonListContains(state, current, actorPerson, person, true) ? P("disliked") : 0;
        var runtime = DuelSelectionImage(state, opponent).NativeTacticRuntime ?? throw DuelMissing("opponent_weight_abnormal");
        int abnormal = 0;
        if (runtime.Abnormal23 != 0) abnormal = unchecked(abnormal + P("abnormal"));
        if (runtime.Abnormal24 != 0) abnormal = unchecked(abnormal + P("abnormal"));
        if (runtime.Abnormal25 != 0) abnormal = unchecked(abnormal + P("abnormal"));
        // A single opponent retains this un-clamped signed weight and receives
        // no extrema bonuses. Clamping belongs only to the multi-opponent pass.
        return new(opponent, unchecked(P("base") + adjustment + special + personality + relation + Math.Clamp(abnormal, 0, 60)));
        int P(string name) => DuelSelectionParameters.Read(state, name);
    }

    private DuelWeightedArmyCandidate? SelectDuelOpponent(GameState state, SourceWorldContext current,
        HexListHandle opponents, TacticRemakeCurrentAbilities abilities, NativeArmyProgressStageMoment moment)
    {
        int count = current.ReadTemporaryHexListCount(opponents) ?? throw DuelMissing("opponent_candidate_count");
        if (count == 0) return null;
        if (count == 1) return current.ReadDuelWeightedArmyCandidates(opponents).First().Candidate;

        int maxTroops = 0, maxMorale = 0, maxReadiness = 0, maxStrength = 0;
        var rows = new List<(DuelWeightedArmyCandidate Candidate, ushort Troops, int Morale, byte? Readiness, int? Strength)>();
        foreach (var row in current.ReadDuelWeightedArmyCandidates(opponents))
        {
            var army = row.Candidate.Army;
            var resources = current.ReadArmyReturnResourceFields(army) ?? throw DuelMissing("opponent_extrema_resources");
            ushort troops = resources.Ready16 ?? throw DuelMissing("opponent_extrema_troops");
            int morale = Math.Min(BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current),
                resources.Morale1A ?? throw DuelMissing("opponent_extrema_morale"));
            byte? readiness = null; int? strength = null;
            if (DuelCommander(current, army) is int person)
            {
                int id = current.ReadPersonIdentity(person) ?? throw DuelMissing("opponent_extrema_Person_identity");
                readiness = state.Officers.GetValueOrDefault(id)?.DuelCondition?.Readiness
                    ?? throw DuelMissing("opponent_extrema_readiness");
                strength = DuelSelectedStrength(state, abilities, id);
                maxReadiness = Math.Max(maxReadiness, readiness.Value);
                maxStrength = Math.Max(maxStrength, strength.Value);
            }
            rows.Add((row.Candidate, troops, morale, readiness, strength));
            maxTroops = Math.Max(maxTroops, troops);
            maxMorale = Math.Max(maxMorale, morale);
        }
        int lastWeight = 0;
        foreach (var row in rows)
        {
            int weight = row.Candidate.Weight;
            if (row.Troops == maxTroops) weight = unchecked(weight + P("most-troops"));
            if (row.Morale == maxMorale) weight = unchecked(weight + P("most-morale"));
            if (row.Readiness == maxReadiness) weight = unchecked(weight + P("most-readiness"));
            if (row.Strength == maxStrength) weight = unchecked(weight + P("most-strength"));
            row.Candidate.Weight = Math.Clamp(weight, 1, 370);
            // Original MOV overwrites the bound on every iteration. This is
            // deliberately the LAST clamped weight, not a roulette total.
            lastWeight = row.Candidate.Weight;
        }
        byte opportunities = state.DuelOpportunityCount ?? throw DuelMissing("opponent_draw_count1698");
        var date = moment.PreWriterThirdDate;
        int sample = TacticEffectSourceSamplerMath.Sample(lastWeight, 1, opportunities, count,
            unchecked((short)date.Year), unchecked((sbyte)date.Day), FSampler(state, moment));
        int cumulative = 0;
        foreach (var row in rows)
        {
            cumulative = unchecked(cumulative + row.Candidate.Weight);
            if (sample <= cumulative) return row.Candidate;
        }
        return null;
        int P(string name) => DuelSelectionParameters.Read(state, name);
    }

    private ArmyDuelSelectedPair? CompleteDuelPairSelection(GameState state, SourceWorldContext current,
        ArmyUnitReference actor, DuelWeightedArmyCandidate selectedOpponent,
        TacticRemakeCurrentAbilities abilities)
    {
        var original = selectedOpponent.Army;
        ArmyUnitReference? assist = null;
        // This second neighborhood is centered on the original opponent and
        // uses the same already-owned default radius as the original constant.
        var friendly = current.CreateNearbyTemporary();
        try
        {
            var nearby = current.CreateNearbyTemporary();
            try
            {
                int radius = ArmyDestructionParameterSeed.ReadCurrent(state,
                    SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius)
                    ?? throw DuelMissing("replacement_radius");
                NearbyArmyTemporarySource.FillNearbyArmyTemporary(current, original, radius, nearby);
                var retained = new HashSet<int>();
                byte originalForce = current.ReadArmyUnitRawForceSlot(original);
                foreach (var neighbor in DuelTemporaryArmies(current, nearby))
                {
                    int at = neighbor.PhysicalSlot ?? throw DuelMissing("replacement_Army_receiver");
                    if (current.ReadArmyExclusionForQuerySnapshot(state, at) ?? throw DuelMissing("replacement_excluded")) continue;
                    byte force = current.ReadArmyUnitRawForceSlot(neighbor);
                    if (ArmyLogisticsPreparation.ReadNativeRelationGate(state, originalForce, force)
                        ?? throw DuelMissing("replacement_relation2CA650")) continue;
                    if (retained.Add(at)) current.AppendNearbyArmyTemporary(friendly, neighbor);
                }
            }
            finally { current.ClearNearbyTemporary(nearby, true); }
            ArmyUnitReference? replacement = null;
            int best = -1;
            foreach (var neighbor in DuelTemporaryArmies(current, friendly))
            {
                if (current.ReadArmyUnitRawForceSlot(neighbor) != current.ReadArmyUnitRawForceSlot(original)) continue;
                int person = DuelCommander(current, neighbor) ?? throw DuelMissing("replacement_commander");
                if (!(TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 106)
                    ?? throw DuelMissing("replacement_effect106"))) continue;
                int id = current.ReadPersonIdentity(person) ?? throw DuelMissing("replacement_Person_identity");
                byte readiness = state.Officers.GetValueOrDefault(id)?.DuelCondition?.Readiness
                    ?? throw DuelMissing("replacement_readiness");
                if (readiness < 80) continue;
                int originalPerson = DuelCommander(current, original) ?? throw DuelMissing("replacement_original_commander");
                if (DuelPersonListContains(state, current, person, originalPerson, true)) continue;
                int strength = DuelSelectedStrength(state, abilities, id);
                int originalId = current.ReadPersonIdentity(originalPerson) ?? throw DuelMissing("replacement_original_identity");
                if (strength <= DuelSelectedStrength(state, abilities, originalId)) continue;
                int score = unchecked(readiness + 2 * strength);
                if (score > best) { best = score; replacement = neighbor; }
            }
            if (replacement is not null &&
                (current.ReadArmyUnitNativeValidity(replacement) ?? throw DuelMissing("replacement_validity")))
            {
                assist = original;
                selectedOpponent.Army = replacement;
            }
        }
        finally { current.ClearNearbyTemporary(friendly, true); }
        var opponent = selectedOpponent.Army;
        if (!(current.ReadArmyUnitNativeValidity(actor) ?? throw DuelMissing("selected_actor_validity")) ||
            !(current.ReadArmyUnitNativeValidity(opponent) ?? throw DuelMissing("selected_opponent_validity"))) return null;
        int side = -1;
        if (DuelForceIsPlayer(state, current, current.ReadArmyUnitForceReference(actor)))
        {
            if (DuelArmyPlayerRulerDistrict(state, current, actor)) side = 0;
        }
        else if (DuelForceIsPlayer(state, current, current.ReadArmyUnitForceReference(opponent)) &&
            DuelArmyPlayerRulerDistrict(state, current, opponent)) side = 1;
        int actorPersonId = PersonId(actor), opponentPersonId = PersonId(opponent);
        int? assistPersonId = assist is not null &&
            (current.ReadArmyUnitNativeValidity(assist) ?? throw DuelMissing("assist_validity")) ? PersonId(assist) : null;
        byte actorReadiness = state.Officers.GetValueOrDefault(actorPersonId)?.DuelCondition?.Readiness
            ?? throw DuelMissing("selected_actor_readiness");
        byte opponentReadiness = state.Officers.GetValueOrDefault(opponentPersonId)?.DuelCondition?.Readiness
            ?? throw DuelMissing("selected_opponent_readiness");
        // The original primary +0x50 is a persistent presentation preference,
        // outside the scenario serializer. No preference owner has supplied it
        // here; preserve Unknown instead of blocking numerical registration or
        // inventing an off/default value from an unrelated scenario field.
        int? setting = null;
        return new(UnitId(actor), UnitId(opponent), assistPersonId is null ? null : UnitId(assist!),
            actorPersonId, opponentPersonId, assistPersonId, actorReadiness, opponentReadiness, side, setting);
        int UnitId(ArmyUnitReference army) => ArmyDestructionQueue.ResolveUnitId(state,
            army.PhysicalSlot ?? throw DuelMissing("selected_Army_receiver")) ?? throw DuelMissing("selected_Unit_identity");
        int PersonId(ArmyUnitReference army) => current.ReadPersonIdentity(
            DuelCommander(current, army) ?? throw DuelMissing("selected_commander")) ?? throw DuelMissing("selected_Person_identity");
    }

    private static bool DuelPersonListContains(GameState state, SourceWorldContext current,
        int first, int second, bool dislike)
    {
        if (!(current.ReadPersonNativeValidity(second) ?? throw DuelMissing("relation_target_validity"))) return false;
        ushort id = current.ReadPersonIdentity(second) ?? throw DuelMissing("relation_target_identity");
        for (int i = 0; i < 8; i++)
        {
            ushort value = (dislike ? OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, first, i) :
                OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, first, i)) ?? throw DuelMissing(dislike ? "relation_dislike_slot" : "relation_like_slot");
            if (value == id) return true;
        }
        return false;
    }
    private int DuelSelectedStrength(GameState state, TacticRemakeCurrentAbilities abilities, int identity) =>
        abilities.ResolveDimension(state, identity, 1, (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect))
            ?? throw DuelMissing("selected_effective_strength");
    private static bool DuelForceIsPlayer(GameState state, SourceWorldContext current, ForceReference force)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw DuelMissing("selection_player_Force");
        int identity = current.ReadForcePhysicalSlot(player) ?? throw DuelMissing("selection_player_identity");
        return identity != 0 && identity == (current.ReadForcePhysicalSlot(force) ?? throw DuelMissing("selection_Force_identity"));
    }
    private static UnitState DuelSelectionImage(GameState state, ArmyUnitReference army)
    {
        int physical = army.PhysicalSlot ?? throw DuelMissing("selection_Army_receiver");
        var slot = (state.ReadArmyLifecycle() ?? throw DuelMissing("selection_arena")).ReceiverSlots()[physical];
        return (slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage)
            ?? throw DuelMissing("selection_Army_image");
    }
}

internal static class DuelSelectionParameters
{
    private const string Prefix = "duel-opportunity:opponent-weight:";
    private static readonly (string Name, int Value)[] FreshTc =
    [
        ("base", 20), ("trait62", 160), ("older-trait144", 160), ("middle-trait144", 80),
        ("trait1", 20), ("trait3", 10), ("liked", 50), ("disliked", -50), ("abnormal", 20),
        ("most-troops", 40), ("most-morale", 20), ("most-readiness", 20), ("most-strength", 20)
    ];
    internal static void AddFresh(string language, Dictionary<string, int> values, IReadOnlyDictionary<string, float> floating)
    {
        if (language != "TC") return;
        foreach (var row in FreshTc)
        {
            string key = Prefix + row.Name;
            if (values.ContainsKey(key) || floating.ContainsKey(key)) throw DuelOpportunityParameters.Missing("fresh_opponent_weight_owner");
            values.Add(key, row.Value);
        }
    }
    internal static int Read(GameState state, string name) => DuelOpportunityParameters.Read(state, Prefix + name);
}
