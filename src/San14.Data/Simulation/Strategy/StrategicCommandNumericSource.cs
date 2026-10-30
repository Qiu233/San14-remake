using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal (int ChancePercent, IReadOnlyList<StrategicCommandPreviewEffect> Effects)
        QueryStrategicCommandNumericCurrent(GameState state, SourceWorldContext current, int kind,
            int executorPhysical, int targetPhysical, int? secondForcePhysical, int military, int independence)
    {
        var source = new StrategicNumericCurrent(this, state, current, kind, executorPhysical, targetPhysical, secondForcePhysical);
        var basis = source.Basis();
        int chance = source.Chance(basis, military), before = source.Chance(basis, 0);
        return (chance, Array.AsReadOnly(new[] { new StrategicCommandPreviewEffect("ChancePercent", before, chance - before, chance) }));
    }

    internal int ReadStrategicRequiredMilitaryCurrent(GameState state, SourceWorldContext current, int kind,
        int executorPhysical, int targetPhysical, int? secondForcePhysical, int independence)
        => new StrategicNumericCurrent(this, state, current, kind, executorPhysical, targetPhysical, secondForcePhysical).RequiredMilitary();

    internal int ResolveStrategicCommandTrialCurrent(GameState state, SourceWorldContext current, int kind,
        int executorPhysical, int targetPhysical, int? secondForcePhysical, int military, int independence)
        => new StrategicNumericCurrent(this, state, current, kind, executorPhysical, targetPhysical, secondForcePhysical).Trial(military);

    internal bool StrategicCommandIncitementSuccessLimitedCurrent(GameState state, SourceWorldContext current, int areaPhysical)
        => new StrategicNumericCurrent(this, state, current, 14, 0, areaPhysical, null).IncitementLimited();

    internal bool HasStrategicSabotageTargetCurrent(GameState state, SourceWorldContext current,
        int areaPhysical, int forcePhysical) => ReadStrategicSabotageObjectsCurrent(state, current, areaPhysical, forcePhysical, 2).Any();

    // 2C5A20 and 2C5AB0 differ only in the actual definition category (2/4).
    // Both walk the current Area Hex iterator and resolve Hex1A through the
    // object table before the asymmetric 2CA650 actor-to-object relation.
    internal IEnumerable<ObjectReference> ReadStrategicSabotageObjectsCurrent(GameState state,
        SourceWorldContext current, int areaPhysical, int forcePhysical, byte category)
    {
        int cursor = AreaHexIterator.FindNextIndex(0, areaPhysical, current);
        while (cursor < 48400)
        {
            var hex = current.ReadHexTableReference(cursor) ?? throw StrategyMissing("sabotage_Hex_pointer");
            var obj = FacilityConstructionLegality.ObjectAt(current, hex);
            var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
            if (StrategyNeed(current.ReadObjectDefinitionRaw2C(definition), "sabotage_Object_category") == category)
            {
                int raw = StrategyNeed(current.ReadObjectForceSlot13(obj), "sabotage_Object_Force13");
                var other = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw StrategyMissing("sabotage_Object_Force_pointer");
                int otherForce = StrategyNeed(current.ReadForcePhysicalSlot(other), "sabotage_Object_Force_identity");
                if (StrategyNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, forcePhysical, otherForce), "sabotage_Object_relation_2CA650"))
                    yield return obj;
            }
            cursor = AreaHexIterator.FindNextIndex(cursor + 1, areaPhysical, current);
        }
    }

    // The integer constants below were extracted offline from the paired TC
    // readable_ghidra/data.tsv. The 1DF7F0..1E1D65 listing supplies the XMM
    // arguments omitted by the decompiler's pow prototypes. No code/global
    // addresses or original-program inputs are consumed by this runtime leaf.
    private sealed class StrategicNumericCurrent(ArmyCampaignRuleAdapter package, GameState state,
        SourceWorldContext current, int kind, int executor, int target, int? second)
    {
        internal readonly record struct NumericBasis(int Intelligence, int Raw);
        private TacticRemakeCurrentAbilities? abilities;
        private TacticSourceSamplerWorld? sampler;
        private readonly Dictionary<(int, int), int> abilityCache = [];
        private int P(string name, int original) => StrategicParameter(state, "numeric." + name, original);
        private int KindParameter(string name, int original) => P($"kind-{kind}." + name, original);

        internal NumericBasis Basis()
        {
            RequireStrategicKind(kind);
            int intelligence = Intelligence(executor), raw = kind switch
            {
                10 => Discord(intelligence), 11 => Independence(intelligence), 12 => Separation(intelligence),
                13 => Poison(intelligence), 14 => Incitement(intelligence), 20 => Sabotage(intelligence),
                24 => Rumor(intelligence), _ => throw StrategyMissing("numeric_kind")
            };
            return new(intelligence, raw);
        }

        private double MilitaryCoefficient(NumericBasis basis)
        {
            double positive = Math.Max(0, basis.Raw);
            double rawDivisor = KindParameter("military-basis-divisor", kind is 12 or 24 ? 13 : 100);
            double exponent = KindParameter("military-basis-exponent", 2);
            double baseTerm = Math.Pow(positive / rawDivisor, exponent) * KindParameter("military-basis-weight", 2);
            double intelligenceTerm = basis.Intelligence < P("military-intelligence-breakpoint", 100)
                ? Math.Pow((double)basis.Intelligence / P("military-intelligence-low-divisor", 120), exponent)
                : (double)basis.Intelligence / P("military-intelligence-high-divisor", 100);
            return KindParameter("military-baseline", 1) + baseTerm + intelligenceTerm * KindParameter("military-intelligence-weight", 2);
        }
        private int MilitaryScale => KindParameter("military-scale", kind switch
            { 10 => 70, 11 => 30, 12 or 24 => 80, 13 => 140, 14 => 240, 20 => 150, _ => throw StrategyMissing("numeric_military_kind") });
        private double ScaledBase(NumericBasis basis) => kind == 11
            ? (double)KindParameter("chance-numerator", 2) * basis.Raw / KindParameter("chance-denominator", 10)
            : basis.Raw;
        internal int Chance(NumericBasis basis, int military)
        {
            double amount = Math.Pow((double)military * MilitaryCoefficient(basis) / KindParameter("military-divisor", 10000),
                (double)KindParameter("military-exponent-numerator", 1) / KindParameter("military-exponent-denominator", 2));
            return Math.Clamp(Cvtt(amount * MilitaryScale + ScaledBase(basis)), 0, 100);
        }
        internal int RequiredMilitary()
        {
            NumericBasis basis = Basis();
            double baseline = ScaledBase(basis);
            // The source inverse functions return -1 when the requested
            // chance is below the unbounded base, then use ceil(pow(...)).
            if (100 < (kind == 11 ? Cvtt(baseline) : baseline)) return -1;
            double power = (double)KindParameter("military-exponent-denominator", 2) /
                KindParameter("military-exponent-numerator", 1);
            double required = Math.Ceiling(Math.Pow((100 - baseline) / MilitaryScale, power) *
                KindParameter("military-divisor", 10000) / MilitaryCoefficient(basis));
            return Cvtt(required);
        }

        private int Discord(int actor)
        {
            int other = second ?? throw StrategyMissing("numeric_second_Force");
            var firstForce = ForceAt(target); var secondForce = ForceAt(other); var own = Force(executor);
            int firstAdvisor = Intelligence(AdvisorOrRuler(firstForce)), otherAdvisor = Intelligence(AdvisorOrRuler(secondForce));
            int ours = Intelligence(AdvisorOrRuler(own));
            int relation = Relation(target, other), security = Security(Physical(PersonFreedomHomeSource.FindBaseArea229FD0(current, Capital(firstForce))));
            int deduction = unchecked(unchecked(relation + security) * KindParameter("relation-security-weight", 25)) / 100;
            int defense = unchecked(unchecked(firstAdvisor + otherAdvisor) * KindParameter("advisor-defense-percent", 50)) / 100;
            return unchecked(-deduction - defense + KindParameter("baseline", 70) +
                Sample(Divide(ours, KindParameter("advisor-sample-divisor", 3)), kind, Id(executor), target, other) + actor + Policy(own, 66));
        }
        private int Independence(int actor)
        {
            var city = new BaseReference(BaseKind.City, target);
            var targetForce = BaseForce(city); var own = Force(executor);
            int governor = Governor(city), ruler = Ruler(targetForce);
            int ours = Intelligence(AdvisorOrRuler(own)), theirs = Intelligence(AdvisorOrRuler(targetForce));
            int dislike = Dislikes(governor, ruler) ? KindParameter("dislike-bonus", 20) : 0;
            int disposition = StrategyNeed(current.ReadAllegianceDisposition(governor), "numeric_governor_disposition16C") <= P("disposition-threshold", -10)
                ? KindParameter("disposition-bonus", 20) : 0;
            int ambition = StrategyNeed(RulerCaptureReleaseInputs.ReadCurrentWord(state, governor, 0x17C), "numeric_governor_17C") >= P("independence-ambition-threshold", 10)
                ? KindParameter("ambition-bonus", 20) : 0;
            return unchecked(KindParameter("baseline", 80) - Loyalty(governor) - theirs +
                Sample(Divide(unchecked(ours + dislike), KindParameter("advisor-sample-divisor", 5)), kind, Id(executor), CityIdentity(city), 0) +
                ambition + disposition + actor - CounterIntelligence(city) + Policy(own, 66));
        }
        private int Separation(int actor)
        {
            var targetForce = Force(target); var own = Force(executor);
            int targetAdvisor = Advisor(targetForce), targetRuler = Ruler(targetForce);
            int defense = Valid(targetAdvisor) ? Intelligence(targetAdvisor) : Ability(targetRuler, 4);
            int ours = Intelligence(AdvisorOrRuler(own));
            if (Dislikes(target, targetRuler)) ours = unchecked(ours + KindParameter("dislike-bonus", 20));
            int a = Sample(Divide(ours, KindParameter("advisor-sample-divisor", 10)), kind, Id(executor), Id(target), 0);
            int b = Sample(Divide(defense, KindParameter("advisor-sample-divisor", 10)), kind, Id(executor), Id(target), 0);
            int raw = unchecked(a - unchecked(unchecked(b + Intelligence(target)) * KindParameter("defense-percent", 50)) / 100 + actor);
            return unchecked(Divide(raw, KindParameter("base-divisor", 12)) - CounterIntelligence(HomeCity(target)) + Policy(own, 66));
        }
        private int Rumor(int actor)
        {
            var own = Force(executor); var theirs = Force(target);
            int ownAdvisor = Advisor(own), ownRuler = PersonRuler(executor), theirAdvisor = Advisor(theirs), theirRuler = PersonRuler(target);
            int ours = Intelligence(Valid(ownAdvisor) ? ownAdvisor : ownRuler);
            int defense = Valid(theirAdvisor) ? Intelligence(theirAdvisor) : Ability(target, 4);
            int dislike = Dislikes(theirRuler, target) ? KindParameter("dislike-bonus", 20) : 0;
            int disposition = StrategyNeed(current.ReadAllegianceDisposition(theirRuler), "numeric_ruler_disposition16C") <= P("disposition-threshold", -10)
                ? KindParameter("disposition-bonus", 60) : 0;
            int a = Sample(unchecked(ours + dislike), kind, Id(executor), Id(target), Id(ownAdvisor));
            int b = Sample(Divide(defense, KindParameter("advisor-sample-divisor", 5)), kind, Id(executor), Id(target), Id(theirAdvisor));
            int raw = unchecked(unchecked(KindParameter("actor-weight", 3) * actor - Intelligence(theirRuler)) + a + disposition - b);
            return unchecked(Divide(raw, KindParameter("base-divisor", 30)) - CounterIntelligence(HomeCity(target)) + Policy(own, 66));
        }
        private int Incitement(int actor)
        {
            var areaForce = AreaForce(target); var own = Force(executor);
            int steward = Steward(target), governor = Governor(AreaCity(target)), ruler = Ruler(areaForce);
            int defense;
            if (Valid(steward)) defense = unchecked(Efficiency(steward) * Intelligence(steward)) / 100;
            else if (Valid(governor)) defense = unchecked(unchecked(Efficiency(governor) * Intelligence(governor)) / 100 * KindParameter("governor-defense-percent", 50)) / 100;
            else if (Valid(ruler)) defense = unchecked(unchecked(Efficiency(ruler) * Intelligence(ruler)) / 100 * KindParameter("ruler-defense-percent", 30)) / 100;
            else defense = 0;
            int theirAdvisor = Advisor(areaForce), ourAdvisor = Advisor(own);
            int theirs = Valid(theirAdvisor) ? Intelligence(theirAdvisor) : Ability(ruler, 4);
            int ours = Valid(ourAdvisor) ? Intelligence(ourAdvisor) : Ability(Ruler(own), 4);
            int divisor = KindParameter("advisor-sample-divisor", 10);
            int raw = unchecked(Sample(Divide(ours, divisor), kind, Id(executor), target, 0) -
                unchecked(Security(target) * KindParameter("security-weight", 40)) / 100 - defense + KindParameter("baseline", 10) + actor -
                Sample(Divide(theirs, divisor), kind, Id(executor), target, 0));
            return unchecked(raw - CounterIntelligence(AreaCity(target)) + Policy(own, 66));
        }
        private int Poison(int actor)
        {
            var theirs = AreaForce(target); var own = Force(executor);
            int defense = KindParameter("minimum-defense", 40);
            if (Valid(theirs))
            {
                int steward = Steward(target); defense = 0;
                if (Valid(steward))
                {
                    int percent = KindParameter("steward-defense-percent", 130);
                    defense = Math.Max(0, unchecked(unchecked(Efficiency(steward) * Intelligence(steward)) / 100 * percent) / 100);
                    defense = Math.Max(defense, unchecked(Ability(steward, 4) * percent) / 100);
                }
                int governor = Governor(AreaCity(target));
                if (Valid(governor)) defense = Math.Max(defense, unchecked(Efficiency(governor) * Intelligence(governor)) / 100);
                defense = Math.Max(defense, KindParameter("minimum-defense", 40));
            }
            int theirAdvisor = Advisor(theirs), ourAdvisor = Advisor(own);
            int theirAbility = Valid(theirAdvisor) ? Intelligence(theirAdvisor) : Ability(Ruler(theirs), 4);
            if (!Valid(theirs)) theirAbility = KindParameter("minimum-defense", 40);
            int ours = Valid(ourAdvisor) ? Intelligence(ourAdvisor) : Ability(Ruler(own), 4);
            int divisor = KindParameter("advisor-sample-divisor", 10);
            int difference = unchecked(Sample(Divide(ours, divisor), kind, Id(executor), target, 0) -
                unchecked(defense * KindParameter("defense-percent", 50)) / 100 + actor -
                Sample(Divide(theirAbility, divisor), kind, Id(executor), target, 0));
            int securityFactor = unchecked(unchecked(unchecked(100 - Security(target)) * KindParameter("security-weight", 200)) / 100 + KindParameter("baseline", 20));
            int raw = Divide(unchecked(securityFactor * Math.Max(0, difference)), KindParameter("base-divisor", 100));
            return unchecked(raw - CounterIntelligence(AreaCity(target)) + Policy(own, 66));
        }
        private int Sabotage(int actor)
        {
            var own = Force(executor); var theirs = AreaForce(target);
            bool same = own == theirs;
            int defense = 0, defenderAdvisor = 0;
            if (!same)
            {
                defense = KindParameter("minimum-defense", 40); defenderAdvisor = KindParameter("minimum-advisor", 40);
                if (Valid(theirs))
                {
                    int steward = Steward(target), governor = Governor(AreaCity(target));
                    if (Valid(steward)) defense = Math.Max(defense, unchecked(unchecked(Efficiency(steward) * Intelligence(steward)) / 100 * KindParameter("steward-defense-percent", 110)) / 100);
                    if (Valid(governor)) defense = Math.Max(defense, unchecked(unchecked(Efficiency(governor) * Intelligence(governor)) / 100 * KindParameter("governor-defense-percent", 100)) / 100);
                    int advisor = Advisor(theirs), ruler = Ruler(theirs);
                    if (Valid(advisor)) defenderAdvisor = Math.Max(defenderAdvisor, Intelligence(advisor));
                    if (Valid(ruler)) defenderAdvisor = Math.Max(defenderAdvisor, Intelligence(ruler));
                }
            }
            int security = same ? unchecked(100 - Security(target)) : Security(target);
            int divisor = KindParameter("advisor-sample-divisor", 10);
            int raw = unchecked(Sample(Divide(Intelligence(AdvisorOrRuler(own)), divisor), kind, Id(executor), target, 0) -
                unchecked(security * KindParameter("security-weight", 40)) / 100 - defense +
                (same ? KindParameter("own-area-bonus", 30) : 0) + KindParameter("baseline", 10) + actor -
                Sample(Divide(defenderAdvisor, divisor), kind, Id(executor), target, 0));
            return unchecked(raw - CounterIntelligence(AreaCity(target)) + Policy(own, 66));
        }

        internal int Trial(int military)
        {
            if (!Valid(executor) || !TargetValid()) return 0;
            if (kind != 24 && kind != 20 && package.StrategicCommandBlockedCurrent(state, current, kind, executor, target, second)) return 0;
            if (kind == 20 && SabotageServiceBypass()) return 1;
            NumericBasis basis = Basis(); int chance = Chance(basis, military);
            int targetSalt = kind is 12 or 24 ? Id(target) : kind == 11 ? CityIdentity(new(BaseKind.City, target)) : target;
            int draw = Sample(100, kind, Id(executor), targetSalt, kind == 10 ? second ?? throw StrategyMissing("trial_second_Force") : 0);
            if (kind == 10) return draw < Divide(unchecked(chance * KindParameter("great-success-numerator", 7)), KindParameter("great-success-denominator", 10)) ? 2 : draw < chance ? 1 : 0;
            if (kind == 11) return draw < chance ? 2 : draw < unchecked(chance * KindParameter("ordinary-success-weight", 3)) ? 1 : 0;
            if (draw >= chance) return 0;
            if (kind is 12 or 24) return 1 + (chance > P("person-great-success-threshold", 5) ? 1 : 0);
            if (kind == 13) return 1;
            if (kind == 14) return chance > KindParameter("great-success-threshold", 30) && Security(target) < KindParameter("great-success-security-limit", 80)
                ? IncitementLimited() ? 1 : 2 : 1;
            int count = SabotageMilitaryObjects();
            if (count == 0) return 1;
            int threshold = unchecked(Math.Max(KindParameter("occupied-object-minimum", 10), unchecked(KindParameter("occupied-object-weight", 5) * count)) + Divide(Intelligence(executor), KindParameter("occupied-object-intelligence-divisor", 2)));
            // 1EBA70's second source call salts are intentionally different:
            // kind is replaced by executor identity, followed by Area and zero.
            return Sample(KindParameter("occupied-object-sample-bound", 100), Id(executor), target, 0, 0) < threshold ? 2 : 1;
        }

        internal bool IncitementLimited()
        {
            int count = 0;
            foreach (var row in current.ReadRegistryReferences(RegistryKind.Army) ?? throw StrategyMissing("numeric_Army60"))
            {
                if (row is not { Kind: RegistryKind.Army }) throw StrategyMissing("numeric_Army_pointer");
                if (StrategyNeed(current.ReadOwnedArmyExclusion233560(row), "numeric_Army_excluded")) continue;
                var army = new ArmyUnitReference(row.PhysicalSlot);
                ushort raw = StrategyNeed(current.ReadArmyUnitRawHex2A(army), "numeric_Army2A");
                var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw StrategyMissing("numeric_Army_Hex");
                if (StrategyNeed(current.ReadHexAreaSlot(hex), "numeric_Army_Area") != target) continue;
                int owner = ForceOrdinal(current.ReadArmyUnitForceReference(army) ?? throw StrategyMissing("numeric_Army_Force"));
                if (owner is >= 46 and <= 51) count++;
            }
            if (count >= P("incitement-special-army-limit", 2)) return true;
            int home = CityIdentity(AreaCity(target));
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw StrategyMissing("numeric_Person100"))
            {
                int person = StrategyNeed(entry, "numeric_City_Person");
                if (Status(person) is < 1 or > 4 || Home(person) != home || PersonCurrentBusySource.Read(current, person, StrategyMissing)) continue;
                if (Trait(person, 66) > 0) return true;
            }
            return false;
        }

        private int SabotageMilitaryObjects() => Valid(AreaForce(target))
            ? package.ReadStrategicSabotageObjectsCurrent(state, current, target, ForceOrdinal(Force(executor)), 4).Count() : 0;
        private bool SabotageServiceBypass()
        {
            var app = package.ReadApplicationGameplay() ?? throw StrategyMissing("numeric_Application");
            if (!StrategyNeed(app.TutorialServicePresent, "numeric_tutorial_service")) return false;
            if (!StrategyNeed(app.TutorialActive, "numeric_tutorial_active")) return false;
            //1EBA70 reads570840, the tutorial's actual13-byte bitmap.
            //It is independent of the application's special-mode service.
            var control = package.ReadTutorialControlState(state);
            return TacticRemakeCurrentBattleControl.Read(control, 52)
                ?? throw StrategyMissing("numeric_tutorial_byte6_bit4");
        }
        private bool TargetValid() => kind switch
        {
            10 => Valid(ForceAt(target)) && Valid(ForceAt(second ?? throw StrategyMissing("numeric_second_Force"))),
            11 => StrategyNeed(current.ReadBaseNativeValidity(new(BaseKind.City, target)), "numeric_City_validity"),
            12 or 24 => Valid(target),
            _ => StrategyNeed(current.ReadAreaNativeValidity(new(target)), "numeric_Area_validity")
        };
        private int CounterIntelligence(BaseReference city)
        {
            if (!StrategyNeed(current.ReadBaseNativeValidity(city), "numeric_counter_City_validity")) return 0;
            var manager = current.ReadOwnedPersonCommandManager() ?? throw StrategyMissing("numeric_counter_command_manager");
            if (manager.Disappearances is null || manager.Talents is null || manager.Diplomacies is null || manager.Strategies is null)
                throw StrategyMissing("numeric_counter_command_kinds");
            // The owned shared dispatch container contains Return15, Absence16,
            // Search/Employ, Diplomacy and these Strategic10..24 tasks. None
            // has native virtual98=25, so 1EDFA0 finds no counterintelligence
            // receiver. This is absence in the actual manager, not a global
            // assumption that defensive tasks can never exist.
            foreach (var task in manager.Strategies.Values)
                if (task.Kind is not (10 or 11 or 12 or 13 or 14 or 20 or 24))
                    throw StrategyMissing("numeric_counter_unmapped_strategic_task");
            return 0;
        }
        private int Efficiency(int person)
        {
            if (!Valid(person)) return 0;
            if (Trait(person, 126) > 0) return P("assignment-full-efficiency", 100);
            int count = StrategyNeed(PersonAssignmentPrecallInputs.ReadCurrentAreaAssignmentActiveCount(state, person), "numeric_assignment_count");
            // 3348E0 is a temporary full-effort display override. Its only
            // 33C150 callers bracket calculations in 341D30 and3558xx with
            // true/false. Strategic queries are outside those display scopes.
            return count switch { 0 or 1 => P("assignment-full-efficiency", 100), 2 => P("assignment-two-efficiency", 64),
                3 => P("assignment-three-efficiency", 44), _ => P("assignment-other-efficiency", 0) };
        }
        private int Sample(int bound, int first, int secondSalt, int third, int fourth)
        {
            // The source's bound<2 leaf never consumes World450/45C.
            if (bound < 2) return 0;
            sampler ??= package.ReadArmyDestructionSampler(state, current) ?? throw StrategyMissing("numeric_sampler450_45C");
            return TacticEffectSourceSamplerMath.Sample(bound, 1, first, secondSalt, third, fourth, sampler);
        }
        private int Intelligence(int person)
        {
            if (!Valid(person)) return 0;
            int value = Ability(person, 2);
            return unchecked(value + unchecked(unchecked(Trait(person, 65) * value) * P("trait-intelligence-percent", 1)) / 100);
        }
        private int Ability(int person, int dimension)
        {
            if (!Valid(person)) return 0;
            if (abilityCache.TryGetValue((person, dimension), out int old)) return old;
            abilities ??= package.SearchAbilities(current);
            int value = StrategyNeed(abilities.ResolveDimension(state, Id(person), dimension,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect)), "numeric_current_ability");
            abilityCache.Add((person, dimension), value); return value;
        }
        private int Trait(int person, int effect) => StrategyNeed(TacticRemakeCurrentTraits.PersonContribution(current, person, effect), "numeric_Person_trait_" + effect);
        private bool Dislikes(int person, int other)
        {
            if (!Valid(other)) return false;
            ushort id = Id(other);
            for (int index = 0; index < 8; index++)
                if (StrategyNeed(OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, person, index), "numeric_dislike_slot") == id) return true;
            return false;
        }
        private int Policy(ForceReference force, int effect) => StrategyNeed(current.ReadBaseForceModifier(force, effect), "numeric_Force_effect_" + effect);
        private byte Relation(int first, int other)
        {
            if ((uint)other >= 52) return 0;
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(first, out var reset) == true)
                return StrategyNeed(reset[0x13 + other], "numeric_reset_Force_relation13");
            return state.NativeArmySimulation?.Forces.GetValueOrDefault(first)?.NormalAftermath?.Relations13 is { Length: 52 } values
                ? values[other] : throw StrategyMissing("numeric_Force_relations13");
        }
        private BaseReference Capital(ForceReference force) => BaseLookup.Find(Home(Ruler(force)), current);
        private BaseReference HomeCity(int person) => CurrentGateMapQueries.FindCity(BaseLookup.Find(Home(person), current), current);
        private ForceReference Force(int person) => current.ReadPersonForce(person) ?? throw StrategyMissing("numeric_Person_Force");
        private ForceReference ForceAt(int physical) => current.ReadForceTableReference((uint)physical < 52 ? physical : 0) ?? throw StrategyMissing("numeric_Force_pointer");
        private ForceReference BaseForce(BaseReference value) => ForceAt(StrategyNeed(current.ReadBaseForceSlot(value), "numeric_Base_Force"));
        private ForceReference AreaForce(int area) => ForceAt(StrategyNeed(current.ReadAreaCurrentForceSlot(new(area)), "numeric_Area_Force"));
        private BaseReference AreaCity(int area)
        {
            int raw = StrategyNeed(current.ReadAreaCitySlot(new(area)), "numeric_Area35");
            return current.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw StrategyMissing("numeric_Area_City_pointer");
        }
        private int Advisor(ForceReference force) => Person(StrategyNeed(current.ReadAssignmentForceAdvisor52(ForceOrdinal(force)), "numeric_Force_advisor52"));
        private int Ruler(ForceReference force) => Person(StrategyNeed(current.ReadForceRecord(ForceOrdinal(force))?.RawRuler10, "numeric_Force_ruler10"));
        private int PersonRuler(int person) { var force = Force(person); return Valid(force) ? Ruler(force) : Person(0); }
        private int AdvisorOrRuler(ForceReference force) { int advisor = Advisor(force); return Valid(advisor) ? advisor : Ruler(force); }
        private int Steward(int area) => Person(StrategyNeed(current.ReadAssignmentArea36(area), "numeric_Area_steward36"));
        private int Governor(BaseReference city) => Person(StrategyNeed(current.ReadArmyReturnBaseGovernor2E(city), "numeric_City_governor2E"));
        private int Person(ushort raw) => (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw StrategyMissing("numeric_Person_lookup");
        private int ForceOrdinal(ForceReference force) => StrategyNeed(current.ReadForcePhysicalSlot(force), "numeric_Force_identity");
        private static int Physical(AreaReference value) => StrategyNeed(value.PhysicalSlot, "numeric_Area_pointer");
        private bool Valid(ForceReference force) => StrategyNeed(current.ReadForceNativeValidity(force), "numeric_Force_validity");
        private bool Valid(int person) => StrategyNeed(current.ReadPersonNativeValidity(person), "numeric_Person_validity");
        private ushort Id(int person) => StrategyNeed(current.ReadPersonIdentity(person), "numeric_Person10");
        private ushort Home(int person) => StrategyNeed(current.ReadPersonFirstLocation(person), "numeric_PersonE2");
        private byte Status(int person) => StrategyNeed(current.ReadPersonStatus(person), "numeric_PersonE6");
        private int CityIdentity(BaseReference city) => StrategyNeed(city.PhysicalSlot, "numeric_City_identity");
        private int Security(int area) => StrategyNeed(state.Areas.GetValueOrDefault(area)?.Security, "numeric_Area_security54");
        private byte Loyalty(int person) => StrategyNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, package._governorInputs), "numeric_PersonE8");
        private static int Divide(int numerator, int denominator) => denominator == 0 || numerator == int.MinValue && denominator == -1
            ? throw StrategyMissing("numeric_division") : numerator / denominator;
        private static int Cvtt(double value) => double.IsNaN(value) || value >= 2147483648.0 || value < -2147483648.0 ? int.MinValue : (int)value;
    }
}
