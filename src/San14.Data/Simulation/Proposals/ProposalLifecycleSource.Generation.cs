using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    internal void Generate()
    {
        State = State with { Proposals = ProposalBoard.Empty() };
        var eligible = new List<int>();
        foreach (int? slot in Current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("generation_Person_registry"))
        {
            int person = K(slot, "generation_Person_pointer");
            var force = Current.ReadPersonForce(person) ?? throw Missing("generation_Person_Force");
            if (K(force.PhysicalSlot, "generation_Force_pointer") != ForceId) continue;
            int identity = PersonIdentity(person);
            if (IsProposerEligible(identity)) eligible.Add(identity);
        }
        if (eligible.Count == 0) return;
        //The tutorial controller owns its separate527BD0 override. Its
        //initialized lesson flag is checked by the provider, beyond active.
        if (K(Package.ReadApplicationTutorialActive(), "tutorial_controller") && TryGenerateTutorialOffer()) return;
        var duties = new List<int>();
        int? priority = GeneratePriority(eligible, duties);
        int budget = Math.Min(Rule("general-offer-count", 6) - (priority.HasValue ? 1 : 0), eligible.Count);
        if (priority.HasValue) eligible.Remove(priority.Value);
        int count = 0;
        var preferred = eligible.Select(identity => (Identity: identity, Weight: GenerationTrait50(identity)))
            .Where(row => row.Weight >= 0).OrderByDescending(row => row.Weight).ThenBy(row => row.Identity)
            .Take(Math.Max(0, budget)).ToArray();
        foreach (var row in preferred)
        {
            eligible.Remove(row.Identity);
            if (!GenerateGeneral(row.Identity)) continue;
            count++;
            duties.Remove(row.Identity);
        }
        int cycle = 0;
        int[] dimensions = [4, 0, 3, 1, 2, 5];
        while (count < budget && eligible.Count != 0 && FreeSlot() is not null)
        {
            int identity;
            int dimension = dimensions[cycle % dimensions.Length];
            if (dimension == 5)
            {
                Shuffle(eligible);
                identity = eligible[0];
            }
            else
            {
                var scores = new List<(int Identity, int Score)>();
                foreach (int candidate in eligible)
                    scores.Add((candidate, unchecked(Rule("trait-count-weight", 10) * GenerationTraitCount(candidate) +
                        PersonAbility(candidate, dimension) + Next(50))));
                var shortlist = scores.OrderByDescending(row => row.Score).ThenBy(row => row.Identity)
                    .Take(10).Select(row => row.Identity).ToList();
                Shuffle(shortlist);
                identity = shortlist[0];
            }
            eligible.Remove(identity);
            if (!GenerateGeneral(identity)) continue;
            count++; cycle++;
            duties.Remove(identity);
        }
        Shuffle(duties);
        int ordinaryStart = duties.FindIndex(identity => GenerationTrait50(identity) < 0);
        if (ordinaryStart >= 0) Shuffle(duties, ordinaryStart);
        int dutyBudget = Math.Min(Rule("duty-offer-count", 12), duties.Count), dutyCount = 0;
        foreach (int identity in duties)
        {
            if (dutyCount >= dutyBudget || FreeSlot() is null) break;
            if (GenerateDuty(identity)) dutyCount++;
        }
    }

    private bool GenerateGeneral(int proposer)
    {
        if (FreeSlot() is null) return false;
        int physical = PersonPhysical(proposer);
        int[] preferences = new int[12];
        for (int index = 0; index < preferences.Length; index++)
            preferences[index] = index == 1
                ? K(Current.ReadAllegianceDisposition(physical), "Person_allegiance_disposition")
                : K(RulerCaptureReleaseInputs.ReadCurrentWord(State, physical, 0x16A + index * 2),
                    "Person_proposal_preference_" + index);
        var categories = Enumerable.Range(0, 12).ToList();
        Shuffle(categories);
        categories = categories.OrderByDescending(category => preferences[category]).ThenBy(category => category).ToList();
        var kinds = new List<ProposalKind>();
        bool restricted = Package.ReadApplicationInformationRestriction(State) ?? throw Missing("selection_service");
        for (int value = 1; value <= 24; value++)
        {
            var kind = (ProposalKind)value;
            if (IsOfficerProposal(kind)) continue;
            if (ProposalDifficulty() == 3 && kind is ProposalKind.RecommendPerson or ProposalKind.RepairEndurance or ProposalKind.FillTroops) continue;
            if (restricted && kind == ProposalKind.RecommendPerson) continue;
            int offered = State.Proposals!.Slots.Count(row => row.SlotId != 0 && row.Kind == kind && row.Status == ProposalStatus.Offered);
            if (offered < Rule("same-kind-offer-limit", 3)) kinds.Add(kind);
        }
        Shuffle(kinds);
        foreach (int category in categories)
            foreach (ProposalKind kind in kinds)
            {
                if (Category(kind) != category) continue;
                int cost = BaseCost(kind);
                if (cost <= AvailableGold && TryGenerateKind(kind, proposer, cost, out var arguments))
                    return StoreOffer(kind, proposer, arguments);
            }
        return false;
    }

    internal bool GenerateDuty(int proposer)
    {
        if (FreeSlot() is null) return false;
        int physical = PersonPhysical(proposer), duty = CityDuty(physical);
        var kinds = duty switch
        {
            1 => new List<ProposalKind> { ProposalKind.DevelopmentUp, ProposalKind.SecurityUp, ProposalKind.Investment },
            2 => new List<ProposalKind> { ProposalKind.Conscription },
            3 => new List<ProposalKind> { ProposalKind.SpecialTraining },
            _ => []
        };
        Shuffle(kinds);
        if (duty == 1)
        {
            int area = CityAssignedArea(physical);
            if (K(CityArea(area).Security, "duty_Area_security") < Rule("duty-security-priority", 60))
                MoveFirst(ProposalKind.SecurityUp);
            else
            {
                int politics = PersonAbility(proposer, 3), intelligence = PersonAbility(proposer, 2);
                int gold = CityLevyQuote(area, proposer, true), food = CityLevyQuote(area, proposer, false);
                if (Rule("duty-development-ability-threshold", 100) < unchecked(politics + intelligence) &&
                    unchecked(gold + unchecked(food * 10) / Rule("duty-food-value-divisor", 50)) /
                    Rule("duty-income-value-divisor", 10) < BaseCost(ProposalKind.DevelopmentUp))
                    MoveFirst(ProposalKind.DevelopmentUp);
            }
        }
        foreach (ProposalKind kind in kinds)
        {
            int cost = BaseCost(kind);
            if (cost <= AvailableGold && TryGenerateKind(kind, proposer, cost, out var arguments))
                return StoreOffer(kind, proposer, arguments);
        }
        return false;
        void MoveFirst(ProposalKind kind) { if (kinds.Remove(kind)) kinds.Insert(0, kind); }
    }

    private bool TryGenerateKind(ProposalKind kind, int proposer, int cost, out ProposalArguments arguments)
    {
        return kind switch
        {
            >= ProposalKind.DevelopmentUp and <= ProposalKind.SecurityDown or ProposalKind.BuyFood or ProposalKind.SellFood
                => TryGenerateCity(kind, proposer, cost, out arguments),
            >= ProposalKind.RecommendPerson and <= ProposalKind.FreePrisoner => TryGeneratePersonnel(kind, proposer, cost, out arguments),
            >= ProposalKind.MoraleUp and <= ProposalKind.RecoverWoundedSoldiers or >= ProposalKind.BefriendAlly and <= ProposalKind.FillTroops
                => TryGenerateArmy(kind, proposer, cost, out arguments),
            _ => throw Missing("generation_kind")
        };
    }

    internal int BaseCost(ProposalKind kind)
    {
        int initial = kind switch
        {
            ProposalKind.DevelopmentUp or ProposalKind.SecurityUp => 200,
            ProposalKind.Investment or ProposalKind.Conscription => 100,
            ProposalKind.SpecialTraining or ProposalKind.SecurityDown or ProposalKind.Banquet or ProposalKind.CurePerson => 400,
            ProposalKind.RepairEndurance or ProposalKind.RecommendPerson => 800,
            ProposalKind.MoraleUp or ProposalKind.MoraleDown or ProposalKind.RecoverWoundedSoldiers => 900,
            ProposalKind.BefriendAlly or ProposalKind.StunEnemy => ProposalDifficulty() == 3 ? 1000 : 500,
            ProposalKind.ConfuseEnemy => ProposalDifficulty() == 3 ? 1600 : 500,
            ProposalKind.BurnEnemy or ProposalKind.ExtinguishFriendlyFire => 500,
            ProposalKind.FillTroops => ProposalDifficulty() == 3 ? 1800 : 900,
            ProposalKind.Exploit or ProposalKind.Requisition or ProposalKind.BuyFood or ProposalKind.SellFood or ProposalKind.FreePrisoner => 0,
            _ => throw Missing("base_cost_kind")
        };
        return Rule("kind-" + (int)kind + ".gold-cost", initial);
    }

    private static int Category(ProposalKind kind) => kind switch
    {
        ProposalKind.MoraleDown or ProposalKind.ConfuseEnemy => 0,
        ProposalKind.RecoverWoundedSoldiers or ProposalKind.ExtinguishFriendlyFire => 1,
        ProposalKind.BefriendAlly or ProposalKind.BurnEnemy => 2,
        ProposalKind.Exploit or ProposalKind.SellFood => 3,
        ProposalKind.Requisition or ProposalKind.BuyFood => 4,
        ProposalKind.StunEnemy or ProposalKind.FillTroops => 5,
        ProposalKind.RecommendPerson or ProposalKind.CurePerson => 6,
        ProposalKind.FreePrisoner => 7,
        ProposalKind.RepairEndurance or ProposalKind.Banquet => 8,
        ProposalKind.SecurityDown or ProposalKind.MoraleUp => 9,
        _ => -1
    };

    private int? FreeSlot()
    {
        for (int slot = 1; slot < ProposalBoard.SlotCount; slot++)
            if (State.Proposals!.Read(slot).Kind == ProposalKind.None) return slot;
        return null;
    }
    private bool StoreOffer(ProposalKind kind, int proposer, ProposalArguments arguments)
    {
        if (FreeSlot() is not int slot) return false;
        SetEntry(new(slot, kind, ProposalStatus.Offered, proposer, arguments));
        return true;
    }
    private void Shuffle<T>(IList<T> values, int begin = 0)
    {
        int count = values.Count - begin;
        for (int index = begin; index < values.Count; index++)
        {
            int other = begin + Next(count);
            (values[index], values[other]) = (values[other], values[index]);
        }
    }
    private int GenerationTraitCount(int identity)
    {
        int count = 0, physical = PersonPhysical(identity);
        for (int index = 0; index < 9; index++)
            if (K(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(Current, physical, index), "trait_count_lookup") != 0) count++;
        return count;
    }
    private int GenerationTrait50(int identity)
    {
        int physical = PersonPhysical(identity), maximum = -1;
        for (int index = 0; index < 9; index++)
        {
            int key = K(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(Current, physical, index), "proposal_trait_lookup");
            if (key == 0) continue;
            var trait = Current.ReadPersonalityTableReference(key < 356 ? key : 0) ?? throw Missing("proposal_trait_pointer");
            for (int pair = 0; pair < 2; pair++)
            {
                if (K(Current.ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA), "proposal_trait_effect") != 50) continue;
                int value = Math.Clamp((int)unchecked((short)K(Current.ReadPersonalityWord(trait, pair == 0 ? 0xB8 : 0xBC), "proposal_trait_value")), -100, 2000);
                maximum = Math.Max(maximum, value);
                break;
            }
        }
        return maximum < 0 ? int.MinValue : maximum;
    }
}
