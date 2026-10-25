using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The same reached predicates are used by the pure trial and by the
    //post-trial stored-result override. They never capture another world.
    private sealed class StrategicResolutionPredicates(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current)
    {
        internal bool Blocked(int kind, int executor, int target, int? second)
        {
            if (!Valid(executor) || !TargetValid(kind, target)) return true;
            if (kind == 10)
            {
                if (!StrategyNeed(current.ReadForceNativeValidity(Force(second ?? throw StrategyMissing("second_Force"))), "second_Force_validity")) return true;
                _ = Intelligence(executor); return false;
            }
            if (kind is 14 or 20) { if (kind == 14) _ = Intelligence(executor); return false; }
            if (kind == 24) return false;
            _ = Intelligence(executor);
            if (kind == 12)
            {
                int ruler = Ruler(current.ReadPersonForce(target) ?? throw StrategyMissing("separation_Force"));
                return Protected(target, ruler) ||
                    StrategyNeed(current.ReadAllegianceDisposition(target), "separation_disposition") >=
                    StrategicParameter(state, "resolution.separation-disposition-threshold", 10) &&
                    Loyalty(target) > StrategicParameter(state, "resolution.separation-loyalty-threshold", 98);
            }
            if (kind == 11)
            {
                var city = current.ReadCityTableReference(target) ?? throw StrategyMissing("independence_City_pointer");
                int governor = Lookup(StrategyNeed(current.ReadArmyReturnBaseGovernor2E(city), "independence_governor2E"));
                var cityForce = Force(StrategyNeed(current.ReadBaseForceSlot(city), "independence_Force"));
                int ruler = Ruler(cityForce);
                if (!HasUnusedOrdinaryForce()) return true;
                if (Protected(governor, ruler) ||
                    StrategyNeed(current.ReadAllegianceDisposition(governor), "independence_disposition") >=
                    StrategicParameter(state, "resolution.separation-disposition-threshold", 10) &&
                    Loyalty(governor) > StrategicParameter(state, "resolution.separation-loyalty-threshold", 98) ||
                    Loyalty(governor) >= 100) return true;
                //32D1D0 is the actual Emperor's current receiving City.
                ushort emperorId = StrategyNeed(CityCaptureTerritoryInputs.ReadEmperor1654(state), "independence_Emperor1654");
                int emperorPerson = Lookup(emperorId), emperorCity = 0;
                if (Valid(emperorPerson))
                {
                    var position = PersonPositionRepair.DecodeSecondLocation(StrategyNeed(current.ReadPersonSecondLocation(emperorPerson), "independence_Emperor_E4"));
                    if (position.Kind == 5) emperorCity = position.Index < 52 ? position.Index : 0;
                }
                var emperor = current.ReadCityTableReference(emperorCity) ?? throw StrategyMissing("independence_Emperor_City");
                return StrategyNeed(current.ReadBaseNativeValidity(emperor), "independence_Emperor_Base_validity") &&
                    emperor.PhysicalSlot == city.PhysicalSlot;
            }
            if (kind == 13)
            {
                var area = new AreaReference(target);
                int hexId = StrategyNeed(current.ReadAreaHexIndex(area), "poison_Area3A");
                var hex = current.ReadHexTableReference(hexId < 48400 ? hexId : 0) ?? throw StrategyMissing("poison_Hex_pointer");
                if (unchecked((short)StrategyNeed(current.ReadHexRaw1C(hex), "poison_Hex1C")) is >= 1 and <= 500) return true;
                //The metropolitan/port branches ask the actual Base virtualB0
                //(current Force ordinal), before the governor trait.
                byte cityId = StrategyNeed(current.ReadAreaCitySlot(area), "poison_Area35");
                var city = current.ReadCityTableReference(cityId < 52 ? cityId : 0) ?? throw StrategyMissing("poison_City_pointer");
                bool capital = StrategyNeed(current.ReadBaseNativeValidity(city), "poison_City_validity") &&
                    StrategyNeed(current.ReadBaseObjectLink(city), "poison_City4E") is ushort link && link <= 3000 &&
                    StrategyNeed(current.ReadObjectNativeValidity(new(link)), "poison_City_Object_validity") &&
                    StrategyNeed(current.ReadObjectHexIndex(new(link)), "poison_City_Object10") == hexId;
                if (capital || StrategyNeed(current.ReadHexSurfaceCode(hex), "poison_Hex10") == 17)
                {
                    ushort objectSlot = StrategyNeed(current.ReadHexObjectLink(hex), "poison_Hex1A");
                    var home = BaseLookup.Find(objectSlot < 62 ? objectSlot : 0, current);
                    if (StrategyNeed(current.ReadBaseNativeValidity(home), "poison_Base_validity") &&
                        StrategyNeed(current.ReadBaseForceSlot(home), "poison_Base_virtualB0") != 0) return true;
                }
                int overseer = Lookup(StrategyNeed(current.ReadAssignmentArea36(target), "poison_Area36"));
                if (Valid(overseer) && StrategyNeed(TacticRemakeCurrentTraits.PersonContribution(current, overseer, 100), "poison_governor_trait100") != 0) return true;
                byte force = StrategyNeed(current.ReadHexForceSlot(hex), "poison_Hex14");
                int security = state.Areas.GetValueOrDefault(target)?.Security ?? throw StrategyMissing("poison_Area54");
                return force is >= 1 and <= 51 && security >= StrategicParameter(state, "resolution.poison-security-threshold", 95);
            }
            throw StrategyMissing("unsupported_resolution_predicate");
        }

        private bool TargetValid(int kind, int target) => kind switch
        {
            10 => StrategyNeed(current.ReadForceNativeValidity(Force(target)), "target_Force_validity"),
            11 => StrategyNeed(current.ReadBaseNativeValidity(current.ReadCityTableReference(target)
                ?? throw StrategyMissing("target_City_pointer")), "target_City_validity"),
            12 or 24 => Valid(target),
            13 or 14 or 20 => StrategyNeed(current.ReadAreaNativeValidity(new(target)), "target_Area_validity"),
            _ => throw StrategyMissing("unsupported_resolution_kind")
        };
        private int Intelligence(int person)
        {
            int value = StrategyNeed(new TacticRemakeCurrentAbilities(current, package._scenario,
                package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language)).ResolveDimension(state,
                Id(person), 2, (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect)), "predicate_Intelligence");
            int trait = StrategyNeed(TacticRemakeCurrentTraits.PersonContribution(current, person, 65), "predicate_trait65");
            return unchecked(value + unchecked(value * trait * StrategicParameter(state, "resolution.trait-intelligence-percent", 1)) / 100);
        }
        private bool HasUnusedOrdinaryForce()
        {
            //2A9960 returns some eligible free receiver. This boolean caller
            //does not consume its preference ordering or selected identity.
            for (int force = 1; force <= 45; force++)
                if (!StrategyNeed(current.ReadForceNativeValidity(Force(force)), "unused_Force_validity")) return true;
            return false;
        }
        private bool Protected(int person, int ruler) => Sworn(person, ruler) || Spouse(person, ruler) ||
            Grandparent(person, ruler) || Grandparent(ruler, person) || Parent(person, ruler) || Parent(ruler, person) || Likes(person, ruler);
        private bool Both(int first, int second) => Valid(first) && Valid(second) && Id(first) != Id(second);
        private bool Sworn(int first, int second) => Both(first, second) && Word(first, 0xBC) != 0 && Word(first, 0xBC) == Word(second, 0xBC);
        private bool Spouse(int first, int second) => Both(first, second) && Word(first, 0xBA) != 0 && Word(first, 0xBA) == Id(second);
        private bool Parent(int first, int second) => Both(first, second) &&
            (Word(first, 0xB4) != 0 && Word(first, 0xB4) == Id(second) || Word(first, 0xB6) != 0 && Word(first, 0xB6) == Id(second));
        private bool Grandparent(int first, int second)
        {
            if (!Both(first, second)) return false;
            int father = Lookup(Word(first, 0xB4));
            if (Valid(father) && Parent(father, second)) return true;
            int mother = Lookup(Word(first, 0xB6));
            return Valid(mother) && Parent(mother, second);
        }
        private bool Likes(int first, int second)
        {
            if (!Valid(second)) return false;
            for (int i = 0; i < 8; i++)
                if (StrategyNeed(OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, first, i), "predicate_like") == Id(second)) return true;
            return false;
        }
        private ForceReference Force(int slot) => current.ReadForceTableReference(slot <= 51 ? slot : 0) ?? throw StrategyMissing("predicate_Force_pointer");
        private int Ruler(ForceReference force) => Lookup(StrategyNeed(current.ReadForceRecord(
            StrategyNeed(current.ReadForcePhysicalSlot(force), "predicate_Force_identity"))?.RawRuler10, "predicate_ruler10"));
        private int Lookup(int identity) => (identity <= 6000 ? current.ReadPersonLookupReference(identity)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot ?? throw StrategyMissing("predicate_Person_pointer");
        private bool Valid(int person) => StrategyNeed(current.ReadPersonNativeValidity(person), "predicate_Person_validity");
        private ushort Id(int person) => StrategyNeed(current.ReadPersonIdentity(person), "predicate_Person_identity");
        private ushort Word(int person, int field) => StrategyNeed(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, person, field), "predicate_relation");
        private byte Loyalty(int person) => StrategyNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, package._governorInputs), "predicate_loyalty");
    }
}
