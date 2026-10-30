using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    internal int PersonAbility(int identity, int dimension)
    {
        int person = ProposalPersonPhysical(identity);
        if (!PersonNeed(Current.ReadPersonNativeValidity(person), "ability.validity")) return 0;
        var abilities = new TacticRemakeCurrentAbilities(Current, Scenario, Language,
            SimulationRuleIdentity.ForLocale(Language));
        return PersonNeed(abilities.ResolveDimension(State, identity, dimension,
            (force, effect) => Panels.ReadPanelModifier(State, force, effect)), "ability.dimension");
    }

    internal int PairSample(int bound, int lhsId, int rhsId) => TacticEffectSourceSamplerMath.Sample(
        bound, 1, lhsId, rhsId, 0, 0,
        Package.ReadArmyDestructionSampler(State, Current) ?? throw ProposalPersonMissing("pair_sample_world"));

    internal void Reward(int personId, ProposalKind kind, int dimension, int amount, bool flag1 = true)
    {
        _ = kind; // The original common writer does not use this argument.
        State = Package.ApplyProposalPersonReward(State, Current, personId, dimension, amount,
            unchecked(Rule("command-merit", 25) * 10), flag1);
    }

    internal void Reward(int personId, int kind, int dimension, int amount, bool flag1 = true) =>
        Reward(personId, (ProposalKind)kind, dimension, amount, flag1);

    internal bool IsProposerEligible(int identity)
    {
        int person = ProposalPersonPhysical(identity);
        if (!PersonNeed(Current.ReadPersonNativeValidity(person), "eligibility.validity") ||
            PersonNeed(Current.ReadPersonStatus(person), "eligibility.status") == 1 ||
            (PersonNeed(Current.ReadArmyReturnPersonFlags196(person), "eligibility.flags") & 1) != 0)
            return false;
        if (ProposalSpecialPerson(person) ||
            (PersonNeed(Current.ReadArmyReturnPersonFlags196(person), "eligibility.special_flags") & 0x4000) != 0)
            return false;
        byte rawDistrict = PersonNeed(Current.ReadPersonDistrictSlot(person), "eligibility.district");
        var district = Current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
            ?? throw ProposalPersonMissing("eligibility.district_pointer");
        int districtPhysical = PersonNeed(district.PhysicalSlot, "eligibility.district_receiver");
        if (!PersonNeed(Current.ReadDistrictNativeValidity(districtPhysical), "eligibility.district_validity"))
            return false;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(State, Current)
            ?? throw ProposalPersonMissing("eligibility.current_player");
        if (!PersonNeed(Current.ReadForceNativeValidity(player), "eligibility.player_validity") ||
            PersonNeed(Current.ReadDistrictForceId(districtPhysical), "eligibility.district_force") !=
            PersonNeed(Current.ReadForcePhysicalSlot(player), "eligibility.player_identity")) return false;
        int leaderId = PersonNeed(Current.ReadDistrictLeader(districtPhysical), "eligibility.district_leader");
        int leader = ProposalPersonPhysical(leaderId);
        if (!PersonNeed(Current.ReadPersonNativeValidity(leader), "eligibility.leader_validity") ||
            PersonNeed(Current.ReadPersonStatus(leader), "eligibility.leader_status") != 1) return false;
        if (PersonPositionRepair.DecodeSecondLocation(PersonNeed(Current.ReadPersonSecondLocation(person),
                "eligibility.position")).Kind == 27) return false;
        var linked = Current.ReadPersonLinkedEntity(person)
            ?? throw ProposalPersonMissing("eligibility.associated_pointer");
        if (linked.Identity.HasValue) return false;
        var owner = State.Officers.GetValueOrDefault(identity)
            ?? throw ProposalPersonMissing("eligibility.person_owner");
        return PersonNeed(OfficerHealthSourceAdapter.Encode(owner.Health), "eligibility.health") == 0;
    }

    internal int PersonTroopCapacity(int identity)
    {
        var capacity = OfficerCommandCapacity.Resolve(State, Current, ProposalPersonPhysical(identity));
        return capacity.Capacity ?? throw ProposalPersonMissing(capacity.UnresolvedInput ?? "troop_capacity");
    }

    /// <summary>Each consecutive group of five integers is one independent
    /// trait predicate: effect, signed threshold, permanent-only, trait type,
    /// contribution. A predicate contributes once at its first matching pair.</summary>
    internal int PersonalityExpression(int identity, IReadOnlyList<int> instructions)
    {
        if (instructions.Count % 5 != 0)
            throw new InvalidDataException("Proposal trait predicates require five parameters each.");
        int person = ProposalPersonPhysical(identity), result = 0;
        for (int term = 0; term < instructions.Count; term += 5)
        {
            if (!PersonNeed(Current.ReadPersonNativeValidity(person), "traits.person_validity")) continue;
            bool matched = false;
            for (int slot = 0; slot < 9 && !matched; slot++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(Current, person, slot)
                    ?? throw ProposalPersonMissing("traits.pointer");
                if (!PersonNeed(Current.ReadSkillPersonalityValidity(trait), "traits.validity")) continue;
                if (instructions[term + 2] != 0 &&
                    unchecked((short)PersonNeed(Current.ReadPersonalityWord(trait, 0xB4), "traits.duration")) != -1)
                    continue;
                if (instructions[term + 3] != 0 &&
                    PersonNeed(Current.ReadPersonalityByte(trait, 0xA4), "traits.type") != instructions[term + 3])
                    continue;
                for (int pair = 0; pair < 2; pair++)
                {
                    if (PersonNeed(Current.ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA), "traits.effect") !=
                        instructions[term]) continue;
                    int threshold = instructions[term + 1];
                    if (threshold != 0)
                    {
                        int value = Math.Clamp((int)unchecked((short)PersonNeed(Current.ReadPersonalityWord(trait,
                            pair == 0 ? 0xB8 : 0xBC), "traits.value")), -100, 2000);
                        if (threshold > 0 ? value < threshold : value > threshold) continue;
                    }
                    result = unchecked(result + instructions[term + 4]);
                    matched = true;
                    break;
                }
            }
        }
        return result;
    }

    private int ProposalPersonPhysical(int identity)
    {
        var reference = (identity is >= 0 and <= 6000 ? Current.ReadPersonLookupReference(identity) :
            Current.ReadTransferPersonTableReference(0)) ?? throw ProposalPersonMissing("person_pointer");
        return PersonNeed(reference.PhysicalSlot, "person_receiver");
    }

    private bool ProposalSpecialPerson(int person)
    {
        int identity = PersonNeed(Current.ReadPersonIdentity(person), "special.identity");
        if (!BattleRemakeCurrentRegionFactor.IsNativeBandPerson(identity)) return false;
        int first = PersonNeed(Current.ReadPersonFirstLocation(person), "special.home");
        return first is >= 1 and <= 61 && ProposalPersonForce(person) is not (>= 46 and <= 50);
    }

    private int ProposalPersonForce(int person) => PersonNeed(Current.ReadForcePhysicalSlot(
        Current.ReadPersonForce(person) ?? throw ProposalPersonMissing("person_force_pointer")), "person_force_identity");
    private static T PersonNeed<T>(T? value, string field) where T : struct =>
        value ?? throw ProposalPersonMissing(field);
    private static InvalidDataException ProposalPersonMissing(string field) => new("proposal.person." + field);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal byte? ReadProposalRawLoyalty(GameState state, SourceWorldContext current, int physical) =>
        ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, physical, _governorInputs);

    internal GameState ApplyProposalPersonReward(GameState state, SourceWorldContext current,
        int identity, int dimension, int amount, int merit, bool publishGrowth)
    {
        int physical = (identity is >= 0 and <= 6000 ? current.ReadPersonLookupReference(identity) :
            current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw new InvalidDataException("proposal.reward.person_pointer");
        if (!(current.ReadPersonNativeValidity(physical) ?? throw new InvalidDataException("proposal.reward.validity")))
            return state;
        if (DuelAftermathSpecialForce(current, physical) || DuelAftermathSpecialPerson(current, physical)) return state;
        var working = state;
        if (dimension is >= 0 and <= 4)
        {
            var growth = _growthSource.TryApplyWithinArmyOperation(state, current, _growthQueries,
                identity, dimension, amount, 0);
            if (growth.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved)
                throw new InvalidDataException("proposal.reward.growth_owner");
            working = growth.State;
        }
        var pending = _pendingMerit.TryApplyWithinArmyOperation(working, current, identity, merit);
        if (pending.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved)
            throw new InvalidDataException("proposal.reward.pending_merit_owner");
        if (!publishGrowth) return pending.State;
        var publication = TryAdvanceProposalGrowthNotifications(pending.State, current);
        return publication.State ?? throw new InvalidDataException(publication.UnresolvedOwner ?? "proposal.reward.growth_reports");
    }
}
