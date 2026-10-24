using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // TC2CAC80: positive eligible count is an input to an actual opportunity
    // draw. A rejected draw owns no duel history/service mutation.
    private GameState AdvanceDuelEligibleOpportunities(GameState state, NativeArmyProgressStageMoment moment)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        _baseCapacityInputs.BindCurrent(state, current);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        OwnedArmyDestructionCurrent.BindOwnedCurrentContext(state, current);
        var pool = current.ReadOwnedCurrentListPool() ?? throw DuelMissing("primary_pool");
        var eligible = current.CreateNearbyTemporary();
        try
        {
            var registry = current.ReadRegistryReferences(RegistryKind.Army) ?? throw DuelMissing("complete_manager60");
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            foreach (var entry in registry)
            {
                if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                    throw DuelMissing("registry_Army_receiver");
                var army = new ArmyUnitReference(physical);
                if (DuelOpportunityEligibility.IsEligible(state, current, army, abilities, _baseCapacityInputs))
                    current.AppendNearbyArmyTemporary(eligible, army);
            }
            int count = current.ReadTemporaryHexListCount(eligible) ?? throw DuelMissing("eligible_count");
            if (count > 0)
            {
                int limitedCount = Math.Min(count, 50), firstTraits = 0, secondTraits = 0;
                foreach (var army in DuelTemporaryArmies(current, eligible))
                {
                    int? person = DuelCommander(current, army);
                    if (person is not int physical) continue;
                    if (DuelHasTrait(current, physical, 61) ||
                        (TacticRemakeCurrentTraits.CommanderHasEffect(current, physical, 29) ?? throw DuelMissing("chance_effect29")))
                        firstTraits = unchecked(firstTraits + 1);
                    if (DuelHasTrait(current, physical, 62)) secondTraits = unchecked(secondTraits + 1);
                }
                // The unknown signed coefficients are irrelevant for a zero
                // multiplier. They are never installed as fabricated zeroes.
                int first = firstTraits == 0 ? 0 : unchecked(firstTraits * DuelOpportunityParameters.Read(state, DuelOpportunityParameters.Trait61OrEffect29Chance));
                int second = secondTraits == 0 ? 0 : unchecked(secondTraits * DuelOpportunityParameters.Read(state, DuelOpportunityParameters.Trait62Chance));
                int numerator = unchecked(second + first + limitedCount + DuelOpportunityParameters.Read(state, DuelOpportunityParameters.BaseChance));
                byte opportunities = state.DuelOpportunityCount ?? throw DuelMissing("count");
                int decay = unchecked(opportunities * DuelOpportunityParameters.Read(state, DuelOpportunityParameters.CountDecayPercent));
                float denominator = (float)decay / 100.0f + 1.0f;
                float quotient = (float)numerator / denominator;
                int chance = quotient is >= int.MinValue and < 2147483648f ? (int)quotient : int.MinValue;
                int selector = BattleRemakeCurrentSettings.ReadSelectorE58(state) ?? throw DuelMissing("chance_settings_selector");
                if (selector != 0) chance = Math.Min(chance, 200);
                else if (BattleRemakeCurrentSettings.ReadAdvancedSettings(state) ?? throw DuelMissing("chance_advanced_settings"))
                {
                    int percent = BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 3) ?? throw DuelMissing("chance_advanced_percent");
                    chance = Math.Min(unchecked(percent * chance) / 100, 300);
                }
                var date = moment.PreWriterThirdDate;
                int sample = TacticEffectSourceSamplerMath.Sample(1000, 1, opportunities, limitedCount,
                    date.Year, date.Day, FSampler(state, moment));
                if (sample <= chance)
                {
                    var pair = AdvanceDuelWeightedActors(state, current, eligible, moment);
                    if (pair is not null) state = RegisterSelectedDuel(state, current, pair, moment);
                }
            }
        }
        finally { current.ClearNearbyTemporary(eligible, true); }
        //The manager's local candidate descriptors are gone before its real
        //phase21 consumer. Continue this same current/cycle; no waiting or
        //calendar catch-up is replayed around the registered duel.
        if (state.CampaignDuelService?.Registered is not null)
            state = CompleteDuelAftermathDuringArmyOperation(state, current, moment);
        var arena = state.ReadArmyLifecycle() ?? throw DuelMissing("saved_arena");
        return state.WithArmyLifecycle(arena with { Indexes = pool.ExportOwnedState() });
    }

    private static IEnumerable<ArmyUnitReference> DuelTemporaryArmies(SourceWorldContext current, HexListHandle temporary)
    {
        var node = current.ReadSkillTemporaryHead(temporary) ?? throw DuelMissing("temporary_head");
        while (node.Identity.HasValue)
        {
            var entry = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw DuelMissing("temporary_payload");
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw DuelMissing("temporary_Army_receiver");
            yield return new ArmyUnitReference(physical);
            node = current.ReadHexListNodeNext(node) ?? throw DuelMissing("temporary_next");
        }
    }

    private static int? DuelCommander(SourceWorldContext current, ArmyUnitReference army)
    {
        ushort commander = current.ReadArmyUnitCommanderId(army) ?? throw DuelMissing("chance_commander12");
        var reference = (commander <= 6000 ? current.ReadPersonLookupReference(commander) : current.ReadTransferPersonTableReference(0))
            ?? throw DuelMissing("chance_Person_pointer");
        if (reference.PhysicalSlot is not int person) return null;
        return (current.ReadPersonNativeValidity(person) ?? throw DuelMissing("chance_Person_validity")) ? person : null;
    }

    private static bool DuelHasTrait(SourceWorldContext current, int person, int key)
    {
        // TC232E30's filtered current nine-slot query, including custom-person
        // first-five behavior, already has an owned reader (TC230730).
        for (int index = 0; index < 9; index++)
            if ((TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person, index)
                ?? throw DuelMissing("chance_trait_slot")) == key) return true;
        return false;
    }

    private ArmyDuelSelectedPair? AdvanceDuelWeightedActors(GameState state, SourceWorldContext current,
        HexListHandle eligible, NativeArmyProgressStageMoment moment)
    {
        var candidates = current.CreateNearbyTemporary();
        try
        {
            var drawn = current.CreateNearbyTemporary();
            try
            {
                int totalWeight = 0;
                foreach (var army in DuelTemporaryArmies(current, eligible))
                {
                    if (DuelCommander(current, army) is not int person) continue;
                    var prefix = ReadDuelActorWeightPrefix(state, current, army, person, moment);
                    int district = prefix.District, contacts = 0;
                    var nearby = FillDuelNearby(current, state, army);
                    try
                    {
                        foreach (var neighbor in DuelTemporaryArmies(current, nearby))
                        {
                            int candidateHex = current.ReadArmyUnitRawHex2A(neighbor) ?? throw DuelMissing("contact_neighbor2A");
                            int actorHex = current.ReadArmyUnitRawHex2A(army) ?? throw DuelMissing("contact_actor2A");
                            bool threatens;
                            try { threatens = PersonArmyCaptureSource.Threatens(state, current, candidateHex, actorHex); }
                            catch (PersonArmyCaptureSource.MissingOwner missing)
                            { throw DuelMissing("contact_2CA7B0:" + missing.Message); }
                            if (!threatens || DuelPreviouslyPaired(state, current, army, neighbor)) continue;
                            contacts = unchecked(contacts + 1);
                            if (DuelArmyPlayerRulerDistrict(state, current, neighbor))
                                district = DuelActorWeightParameters.Read(state, "neighbor-player-district");
                        }
                        if (contacts > 0)
                        {
                            int contactBonus = contacts >= 3 ? DuelActorWeightParameters.Read(state, "three-contacts") :
                                contacts == 2 ? DuelActorWeightParameters.Read(state, "two-contacts") : 0;
                            int weight = Math.Clamp(unchecked(prefix.Scalar + district + contactBonus), 1, 240);
                            current.AppendDuelWeightedArmyCandidate(candidates, new(army, weight));
                            totalWeight = unchecked(totalWeight + weight);
                        }
                    }
                    finally { current.ClearNearbyTemporary(nearby, true); }
                }
                return DrawDuelActors(state, current, candidates, drawn, totalWeight, moment);
            }
            finally { current.ClearNearbyTemporary(drawn, true); }
        }
        finally { current.ClearNearbyTemporary(candidates, true); }
    }

    // 375640 is a read-only pair-history query. KnownEmpty is the actual
    // current TC fresh service list, not an inferred lack of prior duels.
    private static bool DuelPreviouslyPaired(GameState state, SourceWorldContext current,
        ArmyUnitReference actor, ArmyUnitReference neighbor)
    {
        if (!(current.ReadArmyUnitNativeValidity(actor) ?? throw DuelMissing("pair_actor_validity")) ||
            !(current.ReadArmyUnitNativeValidity(neighbor) ?? throw DuelMissing("pair_neighbor_validity"))) return false;
        if (DuelCommander(current, actor) is not int actorPerson || DuelCommander(current, neighbor) is not int neighborPerson) return false;
        var service = state.CampaignDuelService ?? throw DuelMissing("pair_service_owner");
        if (service.History == CampaignDuelHistoryKnowledge.KnownEmpty) return false;
        if (service.History != CampaignDuelHistoryKnowledge.KnownEntries)
            throw DuelMissing("pair_history_375640_current_entries");
        var entries = service.HistoricalPairs ?? throw DuelMissing("pair_history_entries");
        int first = current.ReadPersonIdentity(actorPerson) ?? throw DuelMissing("pair_actor_identity");
        int second = current.ReadPersonIdentity(neighborPerson) ?? throw DuelMissing("pair_neighbor_identity");
        return entries.Any(pair => pair.Matches(first, second));
    }

    private static HexListHandle FillDuelNearby(SourceWorldContext current, GameState state, ArmyUnitReference army)
    {
        // TC2A82F0: ring Hex descriptor is created first; output Army descriptor
        // follows the ring fill. This differs from TC2A78D0's ownership/order.
        ushort raw = current.ReadArmyUnitRawHex2A(army) ?? throw DuelMissing("nearby_center2A");
        var center = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw DuelMissing("nearby_center_pointer");
        var hexes = current.CreateNearbyTemporary();
        HexListHandle? output = null;
        try
        {
            _ = NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, 1, hexes);
            output = current.CreateNearbyTemporary();
            var node = current.ReadSkillTemporaryHead(hexes) ?? throw DuelMissing("nearby_hex_head");
            while (node.Identity.HasValue)
            {
                var hex = current.ReadHexListNodePayload(node) ?? throw DuelMissing("nearby_hex_payload");
                ushort rawArmy = current.ReadHexRaw1C(hex) ?? throw DuelMissing("nearby_hex_Army1C");
                int physical = unchecked((short)rawArmy);
                if (physical != 0)
                {
                    // The native signed negative table-index branch has no
                    // owned safe pointer projection; it is not normalized.
                    if (physical < 0) throw DuelMissing("nearby_negative_Army_pointer");
                    var candidate = current.ReadArmyUnitTableReference(physical < 501 ? physical : 0)
                        ?? throw DuelMissing("nearby_Army_pointer");
                    bool valid = current.ReadArmyUnitNativeValidity(candidate) ?? throw DuelMissing("nearby_Army_validity");
                    if (valid)
                    {
                        int actual = candidate.PhysicalSlot ?? throw DuelMissing("nearby_Army_physical");
                        bool excluded = current.ReadArmyExclusionForQuerySnapshot(state, actual) ?? throw DuelMissing("nearby_Army_exclusion");
                        if (!excluded) current.AppendNearbyArmyTemporary(output, candidate);
                    }
                }
                node = current.ReadHexListNodeNext(node) ?? throw DuelMissing("nearby_hex_next");
            }
            return output;
        }
        catch
        {
            if (output is not null) current.ClearNearbyTemporary(output, true);
            throw;
        }
        finally { current.ClearNearbyTemporary(hexes, true); }
    }
}
