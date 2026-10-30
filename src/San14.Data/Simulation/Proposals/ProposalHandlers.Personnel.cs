using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    internal bool TryGeneratePersonnel(ProposalKind kind, int proposerPersonId, int cost,
        out ProposalArguments arguments)
    {
        arguments = default;
        int proposer = PersonPhysical(proposerPersonId), force = PersonnelForce(proposer);
        var people = new List<int>();
        if (kind == ProposalKind.RecommendPerson)
        {
            if (PersonAbility(proposerPersonId, 4) <= Rule("recommend-minimum-charm", 70)) return false;
            int ruler = K(Current.ReadMoraleWaveRuler22EAC0(Current.ReadPersonForce(proposer)
                ?? throw Missing("recommend_proposer_force")), "recommend_ruler");
            var home = CityPersonCity(proposer);
            foreach (int person in PersonnelPeople())
            {
                if (!K(Current.ReadPersonNativeValidity(person), "recommend_validity") ||
                    K(Current.ReadPersonStatus(person), "recommend_status") != 8 ||
                    PersonnelDislikes(proposer, person) || PersonnelHasAssociated(person)) continue;
                var area = PersonnelPositionArea(person);
                if (area is null || !K(Current.ReadAreaNativeValidity(area), "recommend_area_validity") ||
                    K(Current.ReadAreaCurrentForceSlot(area), "recommend_area_force") != force) continue;
                int areaId = K(area.PhysicalSlot, "recommend_area_identity"), id = PersonIdentity(person);
                if (PersonnelDuplicate(kind, 2, areaId) || PersonnelDuplicate(kind, 3, id)) continue;
                int cityId = K(Current.ReadAreaCitySlot(area), "recommend_area_city");
                if (home.PhysicalSlot != cityId && !PersonnelNeighborCities(home).Any(city => city.PhysicalSlot == cityId)) continue;
                if (PersonnelDislikes(person, proposer) || PersonnelDislikes(person, PersonPhysical(ruler))) continue;
                people.Add(person);
            }
            if (people.Count == 0) return false;
            int[] ranks = CreateRanks(6001);
            int selected = people.OrderBy(person => ranks[PersonIdentity(person)]).First();
            arguments = new(Math.Max(0, cost), 0,
                K(PersonnelPositionArea(selected)?.PhysicalSlot, "recommend_selected_area"), PersonIdentity(selected), 0);
            return true;
        }
        if (kind == ProposalKind.Banquet)
        {
            var candidates = CityBases().Where(home => K(Current.ReadBaseNativeValidity(home), "banquet_base_validity") &&
                K(Current.ReadBaseDistrictSlot(home), "banquet_base_district") == K(Current.ReadPersonDistrictSlot(proposer), "banquet_district") &&
                !PersonnelDuplicate(kind, 2, K(Current.ReadBaseObjectLink(home), "banquet_base_identity")) &&
                PersonnelBanquetPeople(home).Count() >= Rule("banquet-minimum-people", 3)).ToArray();
            if (candidates.Length == 0) return false;
            int[] ranks = CreateRanks(62);
            int sourceHome = K(Current.ReadPersonFirstLocation(proposer), "banquet_proposer_home");
            int target = candidates.OrderByDescending(home => Current.ReadBaseObjectLink(home) == sourceHome)
                .ThenBy(home => ranks[K(Current.ReadBaseObjectLink(home), "banquet_rank_identity")]).Select(home =>
                    (int)K(Current.ReadBaseObjectLink(home), "banquet_selected_identity")).First();
            arguments = new(Math.Max(0, cost), 0, target, 0, 0);
            return true;
        }
        if (kind == ProposalKind.CurePerson)
        {
            if (!K(TacticRemakeCurrentTraits.CommanderHasEffect(Current, proposer, 44), "cure_proposer_trait44")) return false;
            foreach (int person in PersonnelPeople())
                if (K(Current.ReadPersonNativeValidity(person), "cure_validity") && PersonnelForce(person) == force &&
                    PersonnelServing(person) && PersonnelHealth(person) != 0 && PersonnelAtBase(person) &&
                    !PersonnelDuplicate(kind, 2, PersonIdentity(person))) people.Add(person);
            if (people.Count == 0) return false;
            int[] ranks = CreateRanks(6001);
            int selected = people.OrderByDescending(PersonnelHealth).ThenByDescending(person => Current.ReadPersonStatus(person) == 1)
                .ThenBy(person => ranks[PersonIdentity(person)]).First();
            arguments = new(Math.Max(0, cost), 0, PersonIdentity(selected), 0, 0);
            return true;
        }
        if (kind == ProposalKind.FreePrisoner)
        {
            if (PersonAbility(proposerPersonId, 3) <= Rule("release-minimum-politics", 50)) return false;
            foreach (int person in PersonnelPeople())
            {
                if (!K(Current.ReadPersonNativeValidity(person), "release_validity") ||
                    Current.ReadPersonStatus(person) != 6) continue;
                int former = PersonnelFormerForce(person);
                var original = Current.ReadForceTableReference(former <= 51 ? former : 0) ?? throw Missing("release_original_force");
                if (!K(Current.ReadForceNativeValidity(original), "release_original_validity") || former == force ||
                    PersonnelSelectedForceRestricted(force, former) || PersonnelDuplicate(kind, 2, PersonIdentity(person)) ||
                    PersonnelDuplicate(kind, 3, former)) continue;
                var home = CityBase(K(Current.ReadPersonFirstLocation(person), "release_home"));
                if (K(Current.ReadBaseForceSlot(home), "release_home_force") != force || !PersonnelAtBase(person)) continue;
                if (PersonnelRelation(former, force) >= Rule("release-maximum-relation", 100)) continue;
                people.Add(person);
            }
            if (people.Count == 0) return false;
            int[] ranks = CreateRanks(6001);
            people.Sort((left, right) => PersonnelReleaseBefore(left, right, force, ranks) ? -1 :
                PersonnelReleaseBefore(right, left, force, ranks) ? 1 : 0);
            int selected = people[0];
            arguments = new(Math.Max(0, cost), 0, PersonIdentity(selected), PersonnelFormerForce(selected), 0);
            return true;
        }
        return false;
    }

    internal bool IsPersonnelValid(ProposalEntry entry, bool completed)
    {
        if (completed) return true;
        int proposer = PersonPhysical(entry.ProposerPersonId), force = PersonnelForce(proposer);
        if (entry.Status == ProposalStatus.Offered)
        { if (!IsProposerEligible(entry.ProposerPersonId)) return false; }
        else if (entry.Status != ProposalStatus.Pending || force != ForceId ||
            K(Current.ReadPersonStatus(proposer), "personnel_pending_status") is < 2 or > 4) return false;
        int target = entry.Arguments.Parameter2;
        if (entry.Kind == ProposalKind.Banquet)
        {
            var home = CityBase(target);
            return K(Current.ReadBaseForceSlot(home), "banquet_current_force") == force && PersonnelBanquetPeople(home).Any();
        }
        int person = PersonPhysical(entry.Kind == ProposalKind.RecommendPerson ? entry.Arguments.Parameter3 : target);
        if (entry.Kind == ProposalKind.RecommendPerson)
        {
            if (Current.ReadPersonStatus(person) != 8 || PersonnelHasAssociated(person)) return false;
            var area = PersonnelPositionArea(person);
            return area is not null && K(Current.ReadAreaNativeValidity(area), "recommend_current_area_validity") &&
                K(Current.ReadAreaCurrentForceSlot(area), "recommend_current_area_force") == force;
        }
        if (entry.Kind == ProposalKind.CurePerson) return PersonnelForce(person) == force && PersonnelHealth(person) != 0;
        if (entry.Kind != ProposalKind.FreePrisoner) return false;
        int former = PersonnelFormerForce(person);
        var original = Current.ReadForceTableReference(former <= 51 ? former : 0) ?? throw Missing("release_current_original");
        return K(Current.ReadForceNativeValidity(original), "release_current_validity") &&
            K(Current.ReadBaseForceSlot(CityBase(K(Current.ReadPersonFirstLocation(person), "release_current_home"))), "release_current_home_force") == force &&
            former != force && !PersonnelSelectedForceRestricted(force, former) &&
            (K(State.NativeArmySimulation?.Forces.GetValueOrDefault(former)?.DiplomacyFlags178, "release_original_flags") & 1) == 0 &&
            K(Current.ReadPersonNativeValidity(person), "release_current_person_validity") && Current.ReadPersonStatus(person) == 6;
    }

    internal void ApplyPersonnel(ProposalEntry entry)
    {
        int person = entry.ProposerPersonId, proposer = PersonPhysical(person);
        var effects = new List<CampaignEffectRequest>();
        int Day() => (Package.ReadArmyDestructionSampler(State, Current) ?? throw Missing("personnel_current_day")).Day450;
        var date = State.NativeMapSeason?.ThirdDate ?? throw Missing("personnel_current_date");
        void PublishEffects()
        {
            if (effects.Count == 0) return;
            State = State with { CampaignEffectRequests = (State.CampaignEffectRequests ?? throw Missing("personnel_effect_owner"))
                .Append(CampaignEffectSource.ProposalCommands, State.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("personnel_effect_revision"), effects) };
            effects.Clear();
        }
        if (entry.Kind == ProposalKind.CurePerson)
        {
            int target = entry.Arguments.Parameter2;
            var owner = State.Officers.GetValueOrDefault(target) ?? throw Missing("cure_target_owner");
            ushort home = K(Current.ReadPersonFirstLocation(PersonPhysical(target)), "cure_report_home");
            State = State with { Officers = new(State.Officers) { [target] = owner with { Health = OfficerHealth.Healthy } } };
            EnterScene(35);
            Speech(entry, 0x4F9F, new(Person0: person, Person1: PersonnelRuler(proposer), Person2: target, Base0: home));
            ExitScene();
            int hex = K(PersonReturnCommands.ResolvePersonPosition(State, Current, PersonPhysical(target), date).PhysicalSlot, "cure_report_hex");
            History(entry, 0x4FA6, new(Person0: person, Person1: target, Base0: home), hex);
            Reward(person, entry.Kind, 4, PersonAbility(person, 4) / Rule("cure-reward-divisor", 20) + Rule("cure-reward-base", 10), true);
        }
        else if (entry.Kind == ProposalKind.Banquet)
        {
            int charm = PersonAbility(person, 4);
            int amount = unchecked(Rule("banquet-loyalty-base", 2) + Next(charm / Rule("banquet-charm-divisor", 25)));
            int affected = 0;
            foreach (int target in PersonnelBanquetPeople(CityBase(entry.Arguments.Parameter2)).ToArray())
            {
                byte before = K(Package.ReadProposalRawLoyalty(State, Current, target), "banquet_loyalty_before");
                int change = Math.Min(Rule("banquet-loyalty-threshold", 100) - Math.Min(100, (int)before), amount);
                byte after = (byte)Math.Min(255, unchecked(before + (byte)change));
                if ((K(Current.ReadArmyReturnPersonFlags196(target), "banquet_loyalty_flags") & 0x2000) != 0) after = 255;
                Current.SetPersonDestructionLoyaltyE8(target, after);
                State = ArmyDestructionLoyalty.PublishCurrentRawE8(State, Current, target, after);
                affected++;
            }
            ushort location = checked((ushort)entry.Arguments.Parameter2);
            EnterScene(44);
            Speech(entry, 0x4F96, new(Person0: person, Person1: PersonnelRuler(proposer), Base0: location));
            Announcement(0x4F9E, new(Base0: location, Integers: [affected]));
            ExitScene();
            History(entry, 0x4F9D, new(Person0: person, Base0: location), Current.ReadBaseCurrentHexIndex(CityBase(location)));
            Reward(person, entry.Kind, 4, charm / Rule("banquet-reward-divisor", 30) + Rule("banquet-reward-base", 10), true);
        }
        else if (entry.Kind == ProposalKind.RecommendPerson)
        {
            int target = PersonPhysical(entry.Arguments.Parameter3);
            var force = Current.ReadPersonForce(proposer) ?? throw Missing("recommend_apply_force");
            var capital = PersonnelCapital(force);
            var district = Current.ReadBaseDistrictReference(capital) ?? throw Missing("recommend_apply_district");
            State = Package.ApplyRecruitJoinCurrent(State, Current, target, district, capital, date, Day, effects).State;
            var ruler = Current.ReadPersonLookupReference(K(Current.ReadMoraleWaveRuler22EAC0(force), "recommend_apply_ruler"))
                ?? throw Missing("recommend_apply_ruler_pointer");
            State = Package.ApplyRecruitInitialLoyaltyCurrent(State, Current, target, ruler);
            int start = K(PersonReturnCommands.ResolvePersonPosition(State, Current, target, date).PhysicalSlot, "recommend_return_start");
            if (start == Current.ReadBaseCurrentHexIndex(capital))
                Current.WritePersonSecondLocation(target, Math.Min(K(Current.ReadBaseObjectLink(capital), "recommend_return_home"), (ushort)1061));
            else
            {
                var prepared = PersonReturnCommands.Prepare(State, Current, target, date);
                var returned = PersonReturnCommands.Apply(State, Current, prepared.Packet ?? throw Missing(prepared.UnresolvedOwner ?? "recommend_return_prepare"));
                State = returned.State ?? throw Missing(returned.UnresolvedOwner ?? "recommend_return_apply");
            }
            ushort flags = K(Current.ReadArmyReturnPersonFlags196(target), "recommend_target_flags");
            Current.WriteArmyReturnPersonFlags196(target, (ushort)(flags | 2));
            PublishEffects();
            Report(entry, 0x4F8E, new(Person0: person, Person1: PersonnelRuler(proposer), Person2: entry.Arguments.Parameter3, Area0: entry.Arguments.Parameter2),
                0x4F95, new(Person0: person, Person1: entry.Arguments.Parameter3, Area0: entry.Arguments.Parameter2),
                K(Current.ReadAreaHexIndex(new(entry.Arguments.Parameter2)), "recommend_report_hex"));
            Reward(person, entry.Kind, 4, PersonAbility(person, 4) / Rule("recommend-reward-divisor", 20) + Rule("recommend-reward-base", 10), true);
            ShowOfficerDetail(entry.Arguments.Parameter3);
        }
        else if (entry.Kind == ProposalKind.FreePrisoner)
        {
            int target = PersonPhysical(entry.Arguments.Parameter2), original = PersonnelFormerForce(target);
            var originalForce = Current.ReadForceTableReference(original) ?? throw Missing("release_report_force");
            var originalCapital = PersonnelCapital(originalForce);
            ushort originalHome = K(Current.ReadBaseObjectLink(originalCapital), "release_report_original_home");
            int force = PersonnelForce(proposer), beforeRelation = PersonnelRelation(force, original);
            int ability = 0;
            for (int dimension = 0; dimension < 5; dimension++) ability = unchecked(ability + PersonAbility(entry.Arguments.Parameter2, dimension));
            var sampler = Package.ReadArmyDestructionSampler(State, Current) ?? throw Missing("release_sampler");
            State = Package.ApplyCaptiveReleaseCurrent(State, Current, target, date, Day, effects, sampler, false);
            if (force is > 0 and < 46)
                State = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(State, Current, force, Rule("release-popularity", 1));
            PublishEffects();
            EnterScene(9);
            Speech(entry, 0x4FA7, new(Person0: person, Person1: PersonnelRuler(proposer), Person2: entry.Arguments.Parameter2,
                Force0: original, Base0: originalHome, Integers: [beforeRelation]));
            ExitScene();
            State = Package.ApplyDiplomacyRelationCurrent(State, Current, force, original,
                unchecked(Rule("release-relation-base", 10) + ability / Rule("release-relation-divisor", 100)), effects);
            PublishEffects();
            History(entry, 0x4FAF, new(Person0: person, Person1: entry.Arguments.Parameter2, Force0: original, Base0: originalHome),
                Current.ReadBaseCurrentHexIndex(originalCapital));
            Reward(person, entry.Kind, 4, PersonAbility(person, 4) / Rule("release-reward-divisor", 10) + Rule("release-reward-base", 10), true);
        }
        else throw Missing("personnel_handler_kind");
        PublishEffects();
    }

    private IEnumerable<int> PersonnelPeople() => (Current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("personnel_registry"))
        .Select(person => person ?? throw Missing("personnel_registry_pointer"));
    private int PersonnelForce(int person) => K(Current.ReadPersonOldForceSlot(person), "personnel_force");
    private int PersonnelRuler(int person) => K(Current.ReadMoraleWaveRuler22EAC0(Current.ReadPersonForce(person)
        ?? throw Missing("personnel_report_force")), "personnel_report_ruler");
    private bool PersonnelServing(int person) => K(Current.ReadPersonStatus(person), "personnel_serving_status") is >= 1 and <= 4;
    private bool PersonnelAtBase(int person) => PersonPositionRepair.DecodeSecondLocation(K(Current.ReadPersonSecondLocation(person), "personnel_position")).Kind is 5 or 6;
    private int PersonnelHealth(int person) => K(OfficerHealthSourceAdapter.Encode(State.Officers.GetValueOrDefault(PersonIdentity(person))?.Health), "personnel_health");
    private bool PersonnelHasAssociated(int person) => (Current.ReadPersonLinkedEntity(person) ?? throw Missing("personnel_associated")).Identity.HasValue;
    private bool PersonnelDuplicate(ProposalKind kind, int parameter, int value) => (State.Proposals ?? throw Missing("personnel_board")).Slots
        .Any(entry => entry.SlotId != 0 && entry.Kind == kind && entry.Arguments[parameter] == value && entry.Status != ProposalStatus.Empty);
    private bool PersonnelDislikes(int person, int other)
    {
        if (!K(Current.ReadPersonNativeValidity(other), "dislike_target_validity")) return false;
        int id = PersonIdentity(other);
        for (int slot = 0; slot < 8; slot++)
            if (K(OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(State, person, slot), "dislike_slot") == id) return true;
        return false;
    }
    private AreaReference? PersonnelPositionArea(int person)
    {
        var position = PersonPositionRepair.DecodeSecondLocation(K(Current.ReadPersonSecondLocation(person), "personnel_current_position"));
        return position.Kind switch
        {
            5 => PersonFreedomHomeSource.FindBaseArea229FD0(Current, Current.ReadCityTableReference(position.Index < 52 ? position.Index : 0) ?? throw Missing("personnel_position_city")),
            6 => PersonFreedomHomeSource.FindBaseArea229FD0(Current, Current.ReadGateTableReference(position.Index < 11 ? position.Index : 0) ?? throw Missing("personnel_position_gate")),
            10 => Current.ReadAreaTableReference(position.Index < 501 ? position.Index : 0),
            _ => null
        };
    }
    private IEnumerable<int> PersonnelBanquetPeople(BaseReference home)
    {
        int location = K(Current.ReadBaseObjectLink(home), "banquet_people_home");
        foreach (int person in PersonnelPeople())
            if (K(Current.ReadPersonFirstLocation(person), "banquet_people_person_home") == location &&
                K(Current.ReadPersonNativeValidity(person), "banquet_people_validity") && PersonnelHealth(person) == 0 &&
                PersonnelAtBase(person) && Math.Min(100, (int)K(Package.ReadProposalRawLoyalty(State, Current, person), "banquet_people_loyalty")) < Rule("banquet-loyalty-threshold", 100) &&
                Current.ReadPersonStatus(person) != 1 && PersonnelServing(person)) yield return person;
    }
    private int PersonnelFormerForce(int person) => K(Current.ReadOfficerAftermath(person)?.SourceKnowledge?.PreviousForceCode, "release_former_force");
    private bool PersonnelSelectedForceRestricted(int first, int second)
    {
        var simulation = State.NativeArmySimulation ?? throw Missing("personnel_force_bank");
        int selected = K(simulation.World.SelectedForce1656, "personnel_selected_force");
        return first == selected && (K(simulation.Forces.GetValueOrDefault(second)?.Flags12, "personnel_other_flags") & 2) != 0 ||
            second == selected && (K(simulation.Forces.GetValueOrDefault(first)?.Flags12, "personnel_own_flags") & 2) != 0;
    }
    private int PersonnelRelation(int first, int second) => (State.NativeArmySimulation?.Forces.GetValueOrDefault(first)?.NormalAftermath?.Relations13
        ?? throw Missing("personnel_relation_array"))[second];
    private BaseReference PersonnelCapital(ForceReference force)
    {
        int ruler = K(Current.ReadMoraleWaveRuler22EAC0(force), "personnel_capital_ruler");
        return CityPersonCity(PersonPhysical(ruler));
    }
    private IEnumerable<BaseReference> PersonnelNeighborCities(BaseReference home)
    {
        IReadOnlyList<byte> Links(BaseReference receiver) => BaseNeighborLinksSource.Read(Scenario, receiver);
        int origin = K(Current.ReadBaseObjectLink(home), "neighbor_origin");
        var seen = new HashSet<BaseReference>();
        foreach (byte first in Links(home))
        {
            if (first == 0 || first == origin) continue;
            var one = CityBase(first);
            if (!K(Current.ReadBaseNativeValidity(one), "neighbor_first_validity")) continue;
            if (first < 52) { if (seen.Add(one)) yield return one; continue; }
            foreach (byte second in Links(one))
            {
                if (second == 0 || second == origin) continue;
                var two = CityBase(second);
                if (!K(Current.ReadBaseNativeValidity(two), "neighbor_second_validity")) continue;
                if (second < 52) { if (seen.Add(two)) yield return two; continue; }
                foreach (byte third in Links(two))
                {
                    if (third == 0 || third == origin || third == first) continue;
                    var three = CityBase(third);
                    if (K(Current.ReadBaseNativeValidity(three), "neighbor_third_validity") && third < 52 && seen.Add(three)) yield return three;
                }
            }
        }
    }
    private bool PersonnelReleaseBefore(int left, int right, int force, int[] ranks)
    {
        int first = PersonnelFormerForce(left), second = PersonnelFormerForce(right);
        bool Friendly(int other)
        {
            int value = PersonnelRelation(force, other);
            if (State.NativeArmySimulation?.Parameters.Int32Values is not { } bank ||
                !bank.TryGetValue(SimulationParameterKeys.ForceRelations.LevelThreshold(1), out int threshold))
                throw Missing("release_relation_threshold");
            return value >= threshold;
        }
        bool Border(int other) => State.Cities.Values.Where(city => Current.ReadBaseForceSlot(CityReference(city.Id)) == force)
            .Any(city => PersonnelNeighborCities(CityReference(city.Id)).Any(neighbor => Current.ReadBaseForceSlot(neighbor) == other));
        if (Friendly(first) != Friendly(second)) return Friendly(first);
        if (Border(first) != Border(second)) return Border(first);
        int a = K(PersonArmyCaptureSource.ReadOwnedCityCount(Current, new(first)), "release_first_city_count");
        int b = K(PersonArmyCaptureSource.ReadOwnedCityCount(Current, new(second)), "release_second_city_count");
        if (a != b) return a >= b;
        a = PersonnelRelation(force, first); b = PersonnelRelation(force, second);
        if (a != b) return a < b;
        //41CA30 compares the left person's capacity with itself; retain that
        //observed no-op and use the actual shuffled rank as the final key.
        return ranks[PersonIdentity(left)] < ranks[PersonIdentity(right)];
    }
}
