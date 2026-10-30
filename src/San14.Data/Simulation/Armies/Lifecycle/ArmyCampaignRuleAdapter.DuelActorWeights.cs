using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static (int Scalar, int District) ReadDuelActorWeightPrefix(GameState state,
        SourceWorldContext current, ArmyUnitReference army, int person,
        NativeArmyProgressStageMoment moment)
    {
        int identity = current.ReadPersonIdentity(person) ?? throw DuelMissing("actor_weight_Person_identity");
        short adjustment = state.Officers.GetValueOrDefault(identity)?.DuelCondition?.QualificationAdjustment
            ?? throw DuelMissing("actor_weight_adjustment");
        int special;
        if (DuelHasTrait(current, person, 61)) special = P("trait61");
        else if (DuelHasTrait(current, person, 62)) special = P("trait62");
        else if (DuelHasTrait(current, person, 144))
        {
            // The 2A62E0 thunk reads AGE, not effective strength.
            int age = DuelTrait144Age(state, current, person, moment);
            special = age >= 60 ? P("older-trait144") : age >= 50 ? P("middle-trait144") : 0;
        }
        else special = (TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 29)
            ?? throw DuelMissing("actor_weight_effect29")) ? P("effect29") : 0;
        int personality = DuelHasTrait(current, person, 1) ? P("trait1") :
            DuelHasTrait(current, person, 3) ? P("trait3") : 0;
        ushort ready = current.ReadArmyReturnResourceFields(army)?.Ready16
            ?? throw DuelMissing("actor_weight_ready");
        int smallArmy = ready <= P("small-army-threshold") ? P("small-army") : 0;
        int district = DuelArmyPlayerRulerDistrict(state, current, army) ? P("actor-player-district") : 0;
        return (unchecked(P("base") + adjustment + special + personality + smallArmy), district);
        int P(string name) => DuelActorWeightParameters.Read(state, name);
    }

    private static int DuelTrait144Age(GameState state, SourceWorldContext current, int person,
        NativeArmyProgressStageMoment moment)
    {
        int mode = state.CampaignEventWorld?.ScenarioMode ?? throw DuelMissing("actor_age_world_mode");
        // This helper is reached only after the current trait144 query succeeded.
        // Its special-mode branch therefore returns80 regardless of the five-ID table.
        if (mode == 2) return 80;
        if (mode == 3) throw DuelMissing("actor_age_mode3_control_334460");
        int identity = current.ReadPersonIdentity(person) ?? throw DuelMissing("actor_age_Person_identity");
        int birth = state.Officers.GetValueOrDefault(identity)?.BirthYear ?? throw DuelMissing("actor_age_birth_year");
        return Math.Max(0, unchecked((short)moment.PreWriterThirdDate.Year - birth + 1));
    }

    private static bool DuelArmyPlayerRulerDistrict(GameState state, SourceWorldContext current, ArmyUnitReference army)
    {
        int? commander = DuelCommander(current, army);
        byte raw = commander is int person
            ? current.ReadPersonDistrictSlot(person) ?? throw DuelMissing("weight_District_Person_slot") : (byte)0;
        var district = current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
            ?? throw DuelMissing("weight_District_pointer");
        if (!(current.ReadDistrictNativeValidity(district) ?? throw DuelMissing("weight_District_validity"))) return false;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw DuelMissing("weight_player_Force_pointer");
        if (!(current.ReadForceNativeValidity(player) ?? throw DuelMissing("weight_player_Force_validity"))) return false;
        if ((current.ReadDistrictForceSlot(district) ?? throw DuelMissing("weight_District_Force")) !=
            (current.ReadForcePhysicalSlot(player) ?? throw DuelMissing("weight_player_Force_identity"))) return false;
        int physical = district.PhysicalSlot ?? throw DuelMissing("weight_District_physical");
        ushort leader = current.ReadDistrictLeader(physical) ?? throw DuelMissing("weight_District_leader");
        var reference = (leader <= 6000 ? current.ReadPersonLookupReference(leader) : current.ReadTransferPersonTableReference(0))
            ?? throw DuelMissing("weight_District_leader_pointer");
        return reference.PhysicalSlot is int at &&
            (current.ReadPersonNativeValidity(at) ?? throw DuelMissing("weight_District_leader_validity")) &&
            (current.ReadPersonStatus(at) ?? throw DuelMissing("weight_District_leader_status")) == 1;
    }

    private ArmyDuelSelectedPair? DrawDuelActors(GameState state, SourceWorldContext current,
        HexListHandle candidates, HexListHandle drawn, int totalWeight, NativeArmyProgressStageMoment moment)
    {
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        while (true)
        {
            int count = current.ReadTemporaryHexListCount(candidates) ?? throw DuelMissing("actor_draw_count");
            if (count == 0) return null;
            byte opportunities = state.DuelOpportunityCount ?? throw DuelMissing("actor_draw_count1698");
            var date = moment.PreWriterThirdDate;
            int sample = TacticEffectSourceSamplerMath.Sample(totalWeight, 1, opportunities, count,
                unchecked((short)date.Year), unchecked((sbyte)date.Day), FSampler(state, moment));
            // The source allocates this descriptor AFTER sampling and BEFORE scanning.
            var selected = current.CreateNearbyTemporary();
            try
            {
                int cumulative = 0;
                DuelWeightedArmyCandidate? actor = null;
                foreach (var row in current.ReadDuelWeightedArmyCandidates(candidates))
                {
                    cumulative = unchecked(cumulative + row.Candidate.Weight);
                    if (sample > cumulative) continue;
                    actor = row.Candidate;
                    current.AppendDuelWeightedArmyCandidate(selected, actor);
                    current.AppendDuelWeightedArmyCandidate(drawn, actor);
                    totalWeight = unchecked(totalWeight - actor.Weight);
                    break;
                }
                if (actor is null) throw DuelMissing("actor_sample_has_no_candidate");
                // Candidate payload identity is distinct from Army identity: two
                // occurrences of one Army are two separately weighted candidates.
                current.RemoveDuelWeightedArmyCandidateReferences(candidates, actor);
                var opponents = current.CreateNearbyTemporary();
                try
                {
                    var nearby = current.CreateNearbyTemporary();
                    try
                    {
                        NearbyArmyTemporarySource.FillNearbyArmyTemporary(current, actor.Army, 1, nearby);
                        foreach (var neighbor in DuelTemporaryArmies(current, nearby))
                        {
                            if (!IsDuelOpponentEligible(state, current, actor.Army, neighbor, abilities)) continue;
                            var candidate = BuildDuelOpponentCandidate(state, current, actor.Army, neighbor, moment);
                            if (candidate is not null) current.AppendDuelWeightedArmyCandidate(opponents, candidate);
                        }
                        var chosen = SelectDuelOpponent(state, current, opponents, abilities, moment);
                        if (chosen is not null)
                        {
                            var pair = CompleteDuelPairSelection(state, current, actor.Army, chosen, abilities);
                            if (pair is not null) return pair;
                        }
                    }
                    finally { current.ClearNearbyTemporary(nearby, true); }
                }
                finally { current.ClearNearbyTemporary(opponents, true); }
            }
            finally { current.ClearNearbyTemporary(selected, true); }
            // No eligible opponent consumes this actor and draws the next.
        }
    }
}

// These immutable values came from the existing file-backed TC catalog.
// They are installed only at genuine fresh construction; queries read the
// saved bank. Missing current chance coefficients are a separate owner.
internal static class DuelActorWeightParameters
{
    private const string Prefix = "duel-opportunity:actor-weight:";
    private static readonly (string Name, int Value)[] FreshTc =
    [
        ("base", 20), ("trait61", 80), ("trait62", 160),
        ("older-trait144", 160), ("middle-trait144", 80), ("effect29", 80),
        ("trait1", 20), ("trait3", 10), ("small-army-threshold", 1000),
        ("small-army", 40), ("three-contacts", 40), ("two-contacts", 20),
        ("actor-player-district", 30), ("neighbor-player-district", 30)
    ];
    // Independently extracted SC file-backed words through the actual
    // paired campaign consumer. This table does not borrow the TC values.
    private static readonly (string Name, int Value)[] FreshSc =
    [
        ("base", 20),
        ("trait61", 80),
        ("trait62", 160),
        ("older-trait144", 160),
        ("middle-trait144", 80),
        ("effect29", 80),
        ("trait1", 20),
        ("trait3", 10),
        ("small-army-threshold", 1000),
        ("small-army", 40),
        ("three-contacts", 40),
        ("two-contacts", 20),
        ("actor-player-district", 30),
        ("neighbor-player-district", 30),
    ];
    internal static void AddFresh(string language, Dictionary<string, int> values,
        IReadOnlyDictionary<string, float> floating)
    {
        if (language is not ("TC" or "SC")) return;
        foreach (var row in language == "SC" ? FreshSc : FreshTc)
        {
            string key = Prefix + row.Name;
            if (values.ContainsKey(key) || floating.ContainsKey(key)) throw DuelOpportunityParameters.Missing("fresh_actor_weight_owner");
            values.Add(key, row.Value);
        }
    }
    internal static int Read(GameState state, string name) => DuelOpportunityParameters.Read(state, Prefix + name);
}

// Scoped candidate identity, owned only by the manager's temporary lists.
// It is not a saved memory image or another global object registry.
internal sealed class DuelWeightedArmyCandidate(ArmyUnitReference army, int weight)
{
    internal ArmyUnitReference Army { get; set; } = army;
    internal int Weight { get; set; } = weight;
}

public sealed partial class SourceWorldContext
{
    internal void AppendDuelWeightedArmyCandidate(HexListHandle list, DuelWeightedArmyCandidate candidate)
    {
        ConditionalRegistryEnter();
        try
        {
            var node = AllocateHexListNode(list);
            if (node.Identity is int cursor) WriteCurrentListPayload(cursor, candidate);
        }
        finally { ConditionalRegistryExit(); }
    }
    internal IEnumerable<(HexListNodeReference Node, DuelWeightedArmyCandidate Candidate)> ReadDuelWeightedArmyCandidates(HexListHandle list)
    {
        var node = ReadSkillTemporaryHead(list) ?? throw DuelOpportunityParameters.Missing("actor_candidate_head");
        while (node.Identity is int cursor)
        {
            var candidate = ReadCurrentListPayload(cursor) as DuelWeightedArmyCandidate
                ?? throw DuelOpportunityParameters.Missing("actor_candidate_payload");
            yield return (node, candidate);
            node = ReadHexListNodeNext(node) ?? throw DuelOpportunityParameters.Missing("actor_candidate_next");
        }
    }
    internal void RemoveDuelWeightedArmyCandidateReferences(HexListHandle list, DuelWeightedArmyCandidate candidate)
    {
        ConditionalRegistryEnter();
        try
        {
            var node = ReadSkillTemporaryHead(list) ?? throw DuelOpportunityParameters.Missing("actor_remove_head");
            while (node.Identity is int cursor)
            {
                var next = ReadHexListNodeNext(node) ?? throw DuelOpportunityParameters.Missing("actor_remove_next");
                if (ReferenceEquals(ReadCurrentListPayload(cursor), candidate))
                    _orderedLists.Remove(list.Identity!.Value, cursor);
                node = next;
            }
        }
        finally { ConditionalRegistryExit(); }
    }
}
