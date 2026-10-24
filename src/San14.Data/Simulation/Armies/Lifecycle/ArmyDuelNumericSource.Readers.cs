using San14.Core;

namespace San14.Data;

public static partial class ArmyDuelNumericSource
{
    private sealed partial class Evaluation
    {
        private int P(string name) => DuelNumericParameters.Read(state, name);
        private int Setting(int member) => Need(BattleRemakeCurrentSettings.ReadInt32(state, member), $"setting_{member:X}");
        private bool Advanced() => Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "setting_selector") == 0;
        private int AdvancedParameter(int index) => Need(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, index), $"advanced_{index}");
        private int Sample(int bound, int a, int b, int c, int d) => TacticEffectSourceSamplerMath.Sample(bound, 1, a, b, c, d, sampler);
        private bool Valid(int person) => Need(current.ReadPersonNativeValidity(person), "Person_validity");
        private ushort Id(int person) => Need(current.ReadPersonIdentity(person), "Person_identity");
        private int Person(int id)
        {
            var reference = (id is >= 0 and <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("Person_pointer");
            return reference.PhysicalSlot ?? throw Missing("Person_receiver");
        }
        private int Ability(int person, int dimension) => Need(effectiveAbility(Id(person), dimension), $"effective_ability_{dimension}");
        private byte Readiness(int person)
        {
            int id = Id(person);
            foreach (var side in sides) if (side.PersonId == id && side.Write) return side.After;
            return state.Officers.GetValueOrDefault(id)?.DuelCondition?.Readiness ?? throw Missing("Person_current_readiness");
        }
        private bool HasEffect(int person, int effect) => Valid(person) &&
            Need(TacticRemakeCurrentTraits.CommanderHasEffect(current, person, effect), $"effect_{effect}_presence");
        private int SumEffect(int person, int effect) => !Valid(person) ? 0 :
            Need(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, effect), $"effect_{effect}_sum");

        private int Age(int person)
        {
            int mode = state.CampaignEventWorld?.ScenarioMode ?? throw Missing("age_world_mode");
            bool special = mode == 2;
            if (mode == 3) special = P("mode3-age-control") != 0;
            int id = Id(person);
            if (special)
            {
                for (int index = 0; index < 2; index++)
                    if (id == P("special-age-person:" + index)) return 80;
                //A known trait144 proves80 independently of the five-ID table.
                //The missing table tail is reached only if it can affect age.
                for (int index = 0; index < 9; index++)
                    if (Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person, index), "age_trait_slot") == 144) return 80;
                for (int index = 2; index < 5; index++)
                    if (id == P("special-age-person:" + index)) return 80;
                return 20;
            }
            int birth = state.Officers.GetValueOrDefault(id)?.BirthYear ?? throw Missing("age_birth_year");
            return Math.Max(0, unchecked((short)date.Year - birth + 1));
        }
        private int AgeBonus(int person)
        {
            if (!HasEffect(person, 31)) return 0;
            int age = Age(person), baseline = P("effect31-age-base");
            if (age >= unchecked(baseline + 30)) return P("effect31-age-plus30");
            if (age >= unchecked(baseline + 20)) return P("effect31-age-plus20");
            if (age >= unchecked(baseline + 10)) return P("effect31-age-plus10");
            if (age >= unchecked(baseline + 5)) return P("effect31-age-plus5");
            return age >= baseline ? P("effect31-age-base-bonus") : P("young-effect31");
        }

        private bool IsPlayerForce(ForceReference force)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_Force_pointer");
            int identity = Need(current.ReadForcePhysicalSlot(player), "player_Force_identity");
            return identity != 0 && identity == Need(current.ReadForcePhysicalSlot(force), "compared_Force_identity");
        }
        private ArmyUnitReference PersonArmy(int person)
        {
            ushort position = Need(current.ReadPersonSecondLocation(person), "Person_Army_E4");
            var army = current.ReadArmyUnitTableReference(position is >= 62 and <= 561 ? position - 61 : 0)
                ?? throw Missing("Person_Army_pointer");
            return Need(current.ReadArmyUnitCommanderId(army), "Person_Army_commander") == Id(person) ? army :
                current.ReadArmyUnitTableReference(0) ?? throw Missing("Army0_pointer");
        }
        private int ArmyEffect(ArmyUnitReference army, int effect)
        {
            ushort commander = Need(current.ReadArmyUnitCommanderId(army), "effect_Army_commander");
            int person = Person(commander);
            var contribution = TacticRemakeCurrentTraits.ArmyContributions(current, person,
                current.ReadArmyReturnTraitFields(army)?.Traits64) ?? throw Missing($"Army_effect_{effect}");
            return contribution[effect];
        }
        private bool CachedArmyHasEffect(ArmyUnitReference army, byte effect)
        {
            var traits = current.ReadArmyReturnTraitFields(army)?.Traits64;
            if (traits is not { Count: 36 }) throw Missing("Army_cached_traits36");
            foreach (ushort key in traits)
            {
                if (key is < 1 or > 355) continue;
                var trait = current.ReadPersonalityTableReference(key) ?? throw Missing("Army_cached_trait_pointer");
                if (!Need(current.ReadSkillPersonalityValidity(trait), "Army_cached_trait_validity")) continue;
                if (Need(current.ReadPersonalityByte(trait, 0xB6), "Army_cached_effect1") == effect ||
                    Need(current.ReadPersonalityByte(trait, 0xBA), "Army_cached_effect2") == effect) return true;
            }
            return false;
        }
        private int FirstEffect(PersonalityReference trait, byte effect)
        {
            for (int index = 0; index < 2; index++)
            {
                if (Need(current.ReadPersonalityByte(trait, index == 0 ? 0xB6 : 0xBA), "Personality_effect_code") != effect) continue;
                return Math.Clamp((int)unchecked((short)Need(current.ReadPersonalityWord(trait, index == 0 ? 0xB8 : 0xBC), "Personality_effect_value")), -100, 2000);
            }
            return 0;
        }
        private int Radius(PersonalityReference trait)
        {
            byte kind = Need(current.ReadPersonalityByte(trait, 0xA4), "Personality_radius_kind");
            if (kind is 2 or 3 or 0x17 or 0x18) return 1;
            if (kind is not (9 or 10 or 0x15 or 0x16 or 0x1F or 0x20 or 0x27 or 0x28)) return 0;
            if (trait.PhysicalSlot is >= 256 and <= 355)
            {
                int radius = 0;
                for (int index = 0; index < 4; index++) radius |= Need(current.ReadPersonalityByte(trait, 0xA8 + index), "Personality_custom_radius") << (index * 8);
                if (radius >= 1) return radius;
            }
            return Need(ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius), "Personality_default_radius");
        }

        private ushort Relation(int person, int field) => Need(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, person, field), $"relationship_{field:X}");
        private bool Kin(int first, int second, int field)
        {
            if (!Valid(first) || !Valid(second) || Id(first) == Id(second)) return false;
            ushort value = Relation(first, field);
            return value != 0 && value == (field == 0xBC ? Relation(second, field) : Id(second));
        }
        private bool Parent(int first, int second) => Kin(first, second, 0xB4) || Kin(first, second, 0xB6);
        private bool Like(int first, int second)
        {
            if (!Valid(second)) return false;
            ushort id = Id(second);
            for (int index = 0; index < 8; index++)
                if (Need(OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, first, index), "like_slot") == id) return true;
            return false;
        }
        private bool SharedParent(int first, int second)
        {
            if (!Valid(first) || !Valid(second) || Id(first) == Id(second)) return false;
            ushort firstParent = Relation(first, 0xB4);
            if (firstParent != 0 && firstParent == Relation(second, 0xB4)) return true;
            ushort secondParent = Relation(first, 0xB6);
            return secondParent != 0 && secondParent == Relation(second, 0xB6);
        }
        private bool Related(int first, int second)
        {
            if (!Valid(first) || !Valid(second)) return false;
            bool Serves(int p) => Need(current.ReadPersonStatus(p), "related_status") is >= 1 and <= 4;
            if (Serves(first) && Serves(second) &&
                Need(current.ReadPersonOldForceSlot(first), "related_first_Force") != Need(current.ReadPersonOldForceSlot(second), "related_second_Force")) return false;
            if (Parent(first, second) || Parent(second, first) || Kin(first, second, 0xBA) || SharedParent(first, second) || Kin(first, second, 0xBC)) return true;
            ushort blood = Relation(first, 0xB2);
            return blood != 0 && blood == Relation(second, 0xB2);
        }

        private IEnumerable<RegistryObjectReference> OwnedItems(int person)
        {
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Item) ?? throw Missing("Item_registry110");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("Item_registry_head");
            while (node.Identity.HasValue)
            {
                ushort id = Id(person);
                if (id is >= 1 and <= 6000)
                {
                    var item = current.ReadRegistryNodePayload(node) ?? throw Missing("Item_registry_payload");
                    if (item.Kind != RegistryKind.Item || item.PhysicalSlot is not int physical) throw Missing("Item_registry_receiver");
                    if (Need(current.ReadCaptureItemOwner4A(physical), "Item_current_owner") == id) yield return item;
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("Item_registry_next");
            }
        }

        //375240 is eligibility for the DUEL death predicates. It is not the
        //Army27D900 injury path or the unrelated full death settlement writer.
        private bool DeathEligible(int defeated, int victor)
        {
            if (!Valid(defeated) || !Valid(victor) || Readiness(defeated) != 0) return false;
            if (Advanced()) { if (AdvancedParameter(1) <= 0) return false; }
            else if (Need(BattleRemakeCurrentSettings.ReadBattleDeathFrequency(state), "death_frequency") == 3) return false;
            foreach (var item in OwnedItems(defeated))
                if (DuelNumericParameters.ReadItemProtectionCategory(state, item.PhysicalSlot!.Value) == 11) return false;
            if (Need(current.ReadPersonStatus(defeated), "death_eligibility_status") is >= 1 and <= 3) return false;
            if (Need(current.ReadPersonOldForceSlot(defeated), "death_eligibility_defeated_Force") ==
                Need(current.ReadPersonOldForceSlot(victor), "death_eligibility_victor_Force")) return false;
            if (Parent(defeated, victor) || Parent(victor, defeated) || Related(defeated, victor)) return false;
            ushort blood = Relation(defeated, 0xB2);
            if (blood != 0 && Relation(victor, 0xB2) == blood) return false;
            if (Kin(defeated, victor, 0xBA) || Kin(defeated, victor, 0xBC) || Like(victor, defeated) || Like(defeated, victor)) return false;
            if ((Need(current.ReadArmyReturnPersonFlags196(defeated), "death_eligibility_flags15E") & 0x200) != 0) return false;
            var temporary = current.CreatePersonInjuryTemporary();
            try
            {
                //2307D0 scans only personality nodes that actually allocated.
                current.ClearPersonInjuryTemporary(temporary, false);
                for (int index = 0; index < 9; index++)
                {
                    var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, defeated, index) ?? throw Missing("death_trait_pointer");
                    if (Need(current.ReadSkillPersonalityValidity(trait), "death_trait_validity")) current.AppendPersonInjuryTemporary(temporary, trait);
                }
                var node = current.ReadSkillTemporaryHead(temporary) ?? throw Missing("death_traits_head");
                while (node.Identity.HasValue)
                {
                    var trait = current.ReadSkillTemporaryPayload(node) ?? throw Missing("death_traits_payload");
                    if (FirstEffect(trait, 0x35) > 0 && Radius(trait) == 0) return false;
                    node = current.ReadHexListNodeNext(node) ?? throw Missing("death_traits_next");
                }
                var army = PersonArmy(defeated);
                return !Need(current.ReadArmyUnitNativeValidity(army), "death_own_Army_validity") || !CachedArmyHasEffect(army, 0x35);
            }
            finally { current.ClearPersonInjuryTemporary(temporary, true); }
        }

        private bool DeathChance(int defeated, int victor, bool instantaneous)
        {
            if (!Valid(defeated) || !Valid(victor) || !DeathEligible(defeated, victor)) return false;
            int multiplier = instantaneous ? P("instant-death-multiplier") : 1;
            int maximum = 0;
            for (int dimension = 0; dimension < 5; dimension++) maximum = Math.Max(maximum, Ability(defeated, dimension));
            int frequency = Need(BattleRemakeCurrentSettings.ReadBattleDeathFrequency(state), "death_frequency");
            int factor = frequency == 0 ? P("death-frequency0") : frequency == 1 ? P("death-frequency1") : frequency == 2 ? P("death-frequency2") : 0;
            if (Advanced()) factor = P("death-frequency1");
            int difference = Math.Max(0, unchecked(P("death-maximum-ability") - maximum));
            byte health = Need(OfficerHealthSourceAdapter.ReadByte(current, defeated), "death_health161");
            int probability = unchecked(difference * (health + 1) * factor * multiplier) / unchecked(P("death-divisor") * 2);
            probability = Math.Clamp(probability, 0, instantaneous ? 60 : 30);
            var winningArmy = PersonArmy(victor);
            int effect = Need(current.ReadArmyUnitNativeValidity(winningArmy), "death_winner_Army_validity") ? ArmyEffect(winningArmy, 0x52) : 0;
            probability = unchecked(probability + effect);
            if (Advanced()) probability = unchecked(AdvancedParameter(1) * probability) / 100;
            if (instantaneous)
                return Sample(100, Id(defeated), Need(current.ReadPersonFirstLocation(victor), "instant_death_victor_E2"), sampler.Day450, 0) < probability;
            int armyIndex = PersonArmy(defeated).PhysicalSlot ?? throw Missing("death_own_Army_index");
            return Sample(100, Id(defeated), armyIndex, 0, 0) < probability;
        }

        private ArmyDuelAftermath ResolveAftermath(int defeated, int victor)
        {
            if (!instant && DeathChance(defeated, victor, false)) return ArmyDuelAftermath.Death;
            if (Readiness(defeated) != 0) return ArmyDuelAftermath.None;
            //373490 has already written F1 before27E870. A two-person overlay
            //preserves that ordering without committing any saved owner.
            var people = new Dictionary<int, OfficerState>(state.Officers);
            foreach (var side in sides)
                if (side.Write)
                {
                    var person = people.GetValueOrDefault(side.PersonId) ?? throw Missing("planned_F1_Person");
                    var condition = person.DuelCondition ?? throw Missing("planned_F1_condition");
                    people[person.Id] = person with { DuelCondition = condition with { Readiness = side.After } };
                }
            var planned = state with { Officers = people };
            var result = PersonArmyCaptureSource.Evaluate(planned, current, defeated, new(victor),
                new CallbackAbilities(effectiveAbility), date, sampler, captureOwners);
            if (result.Captured is not bool captured) throw Missing("capture_27E870:" + result.UnresolvedOwner);
            captureReceipt = result.Receipt;
            return captured ? ArmyDuelAftermath.Capture : ArmyDuelAftermath.None;
        }
    }

    private sealed class CallbackAbilities(Func<int, int, int?> read) : IEffectiveOfficerAbilities
    {
        public OfficerAbilities? Resolve(GameState world, int personId)
        {
            if (read(personId, 0) is not int leadership || read(personId, 1) is not int strength ||
                read(personId, 2) is not int intelligence || read(personId, 3) is not int politics || read(personId, 4) is not int charm) return null;
            return new(leadership, strength, intelligence, politics, charm);
        }
    }
}
