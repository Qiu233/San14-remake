using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // 753D00 selects 6E1ED0 for Separation and 6E19D0 for Rumor.
    // The caller owns this current; constructing this list never captures another world.
    private IReadOnlyList<int> ReadStrategicPersonTargetsCurrent(GameState state,
        SourceWorldContext current, int forceId, int kind) =>
        new StrategicPersonTargetsCurrent(this, state, current, forceId).Read(kind);

    private sealed class StrategicPersonTargetsCurrent(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, int forceId)
    {
        private readonly Dictionary<(int Person, int Dimension), int> abilities = [];
        private TacticRemakeCurrentAbilities? abilitySource;

        //7114C0 is existential and6BEEB0 counts qualifying receivers. Neither
        //sorts a selector or reads its display names/abilities. The latter
        //owns a successful-append City list and passes a fresh pooled copy to
        //7144D0 for every Person while the original list remains alive.
        internal bool HasMenuTarget(int kind, int minimum,
            Func<ForceReference, ForceReference, bool> protection)
        {
            var people = current.ReadRegisteredPersonPhysicalSlots() ?? throw StrategyMissing("menu_target_Person100");
            if (kind == 12)
            {
                foreach (int? entry in people)
                    if (CanSeparate(StrategyNeed(entry, "menu_separation_Person"))) return true;
                return false;
            }
            if (kind != 24) throw new ArgumentOutOfRangeException(nameof(kind));
            var saturated = FinalizationTemporary(current);
            try
            {
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw StrategyMissing("menu_rumor_City90"))
                {
                    if (entry is not { Kind: RegistryKind.City, PhysicalSlot: int id }) throw StrategyMissing("menu_rumor_City_pointer");
                    var city = new BaseReference(BaseKind.City, id);
                    int home = StrategyNeed(current.ReadBaseObjectLink(city), "menu_rumor_City4E");
                    if (home is < 1 or > 61) continue;
                    int count = 0;
                    foreach (int? raw in people)
                    {
                        int person = StrategyNeed(raw, "menu_rumor_city_Person");
                        if (Status(person) is < 1 or > 4 || Home(person) != home) continue;
                        if ((Counters(person) & 255) == 0 || ++count <= 2) continue;
                        if (!MainStrategyList(current, saturated).Contains(entry)) FinalizationAppend(current, saturated, entry);
                        break;
                    }
                }
                int targets = 0;
                foreach (int? raw in people)
                {
                    var copy = FinalizationTemporary(current);
                    try
                    {
                        foreach (var entry in MainStrategyList(current, saturated)) FinalizationAppend(current, copy, entry);
                        var cities = MainStrategyList(current, copy).Select(entry =>
                            entry.Kind == RegistryKind.City ? new BaseReference(BaseKind.City,
                                StrategyNeed(entry.PhysicalSlot, "menu_rumor_copy_City")) : throw StrategyMissing("menu_rumor_copy_kind")).ToHashSet();
                        if (CanSpreadRumor(StrategyNeed(raw, "menu_rumor_Person"), cities, protection) && ++targets >= minimum) return true;
                    }
                    finally { FinalizationRelease(current, copy); }
                }
                return false;
            }
            finally { FinalizationRelease(current, saturated); }
        }

        internal IReadOnlyList<int> Read(int kind)
        {
            if (kind is not (12 or 24)) throw new ArgumentOutOfRangeException(nameof(kind));
            var people = current.ReadRegisteredPersonPhysicalSlots() ?? throw StrategyMissing("target_Person100");
            HashSet<BaseReference>? saturated = kind == 24 ? SaturatedCities(people) : null;
            var result = new List<int>();
            foreach (int? entry in people)
            {
                int person = StrategyNeed(entry, "target_Person_pointer");
                if (kind == 12 ? CanSeparate(person) : CanSpreadRumor(person, saturated!)) result.Add(person);
            }
            // Both original helpers perform strict insertion sorting. Only genuine
            // ties consume later keys, and equal keys retain registry order.
            for (int index = 1; index < result.Count; index++)
            {
                int person = result[index], previous = index - 1;
                while (previous >= 0 && Before(person, result[previous], kind))
                { result[previous + 1] = result[previous]; previous--; }
                result[previous + 1] = person;
            }
            return result.AsReadOnly();
        }

        private HashSet<BaseReference> SaturatedCities(IReadOnlyList<int?> people)
        {
            var result = new HashSet<BaseReference>();
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw StrategyMissing("rumor_City90"))
            {
                if (entry is not { Kind: RegistryKind.City }) throw StrategyMissing("rumor_City_pointer");
                var city = new BaseReference(BaseKind.City, entry.PhysicalSlot);
                int home = StrategyNeed(current.ReadBaseObjectLink(city), "rumor_City4E");
                if (home is < 1 or > 61) continue;
                int count = 0;
                foreach (int? raw in people)
                {
                    int person = StrategyNeed(raw, "rumor_iterator_Person");
                    // CFootholdPersonIterator(0) calls 232F10(0), hence only
                    // serving status1..4 and the exact first-location identity.
                    if (Status(person) is < 1 or > 4 || Home(person) != home) continue;
                    if ((Counters(person) & 255) != 0 && ++count >= 3)
                    { result.Add(city); break; }
                }
            }
            return result;
        }

        private bool CanSeparate(int person)
        {
            byte status = Status(person);
            if (status != 6)
            {
                if (status is < 1 or > 4) return false; //232460.
            }
            else
            {
                var home = BaseLookup.Find(Home(person), current);
                if (StrategyNeed(current.ReadBaseForceSlot(home), "separation_captive_home_Force") != forceId) return false;
                var position = Position(person);
                BaseReference at;
                if (position.Kind == 5)
                    at = current.ReadCityTableReference(position.Index < 52 ? position.Index : 0)
                        ?? throw StrategyMissing("separation_captive_City_pointer");
                else if (position.Kind == 6)
                    at = current.ReadGateTableReference(position.Index < 11 ? position.Index : 0)
                        ?? throw StrategyMissing("separation_captive_Gate_pointer");
                else return false;
                if (StrategyNeed(current.ReadBaseForceSlot(at), "separation_captive_position_Force") != forceId) return false;
            }
            int owner = ForceOrdinal(Force(person));
            if (owner is >= 46 and <= 50 || SpecialPerson(person, owner) ||
                (Flags(person) & 0x4000) != 0 || Status(person) == 1 ||
                PersonCurrentBusySource.Read(current, person, StrategyMissing)) return false;
            var force = Force(person);
            if (!StrategyNeed(current.ReadForceNativeValidity(force), "separation_target_Force_validity")) return false;
            owner = ForceOrdinal(force);
            return owner != forceId && (ForceRelation(owner, forceId) & 0x7F) == 0 &&
                ((ForceByte(forceId, 0x12) & 2) == 0 || (ForceByte(owner, 0x12) & 2) == 0);
        }

        private bool CanSpreadRumor(int person, HashSet<BaseReference> saturated,
            Func<ForceReference, ForceReference, bool>? protection = null)
        {
            if (Status(person) is < 1 or > 4 || Status(person) == 1) return false;
            int owner = ForceOrdinal(Force(person));
            if (owner is >= 46 and <= 50 || SpecialPerson(person, owner) || (Flags(person) & 0x4000) != 0) return false;
            if (Loyalty(person) >= RumorLoyaltyLimit()) return false;
            ushort counters = Counters(person);
            if ((counters & 255) != 0 || (counters >> 8) != 0) return false;
            var city = CurrentGateMapQueries.FindCity(BaseLookup.Find(Home(person), current), current);
            if (saturated.Contains(city) || Position(person).Kind == 27) return false;
            var force = Force(person);
            var ruler = StrategyNeed(current.ReadForceNativeValidity(force), "rumor_related_Force_validity")
                ? Lookup(StrategyNeed(current.ReadForceRecord(ForceOrdinal(force))?.RawRuler10, "rumor_Force_ruler10"))
                : Lookup(0);
            if (Sworn(person, ruler) || Spouse(person, ruler) || Grandparent(person, ruler) ||
                Grandparent(ruler, new(person)) || Parent(person, ruler) || Parent(ruler, new(person)) ||
                Likes(ruler, person)) return false;
            // This existing leaf implements the same 2C3CA0 protected-pair
            // predicate, including coalition, alliance and dependency owners.
            return protection is null
                ? !new DiplomacyCommandSource(state, current).HasTreaty(forceId, ForceOrdinal(Force(person)))
                : !protection(new(forceId), Force(person));
        }

        //1E9F50 rechecks the actual associated receiver, not the whole initial
        //selector. In particular only the low ongoing counter participates.
        internal bool RumorContinuation(int person, int originalForce)
        {
            if (!StrategyNeed(current.ReadPersonNativeValidity(person), "journey_rumor_Person_validity") ||
                Status(person) is 1 or 9 or 6) return false;
            var force = Force(person);
            if (!StrategyNeed(current.ReadForceNativeValidity(force), "journey_rumor_Force_validity")) return false;
            int owner = ForceOrdinal(force);
            var protection = new DiplomacyCommandSource(state, current);
            if (protection.HasTreaty(forceId, owner) || Position(person).Kind == 27 || owner != originalForce ||
                protection.HasTreaty(owner, forceId) || (Counters(person) & 255) != 0) return false;
            var city = CurrentGateMapQueries.FindCity(BaseLookup.Find(Home(person), current), current);
            int location = StrategyNeed(current.ReadBaseObjectLink(city), "journey_rumor_home_City4E");
            int count = 0;
            foreach (int? raw in current.ReadRegisteredPersonPhysicalSlots() ?? throw StrategyMissing("journey_rumor_Person100"))
            {
                int other = StrategyNeed(raw, "journey_rumor_iterator_Person");
                if (Status(other) is >= 1 and <= 4 && Home(other) == location &&
                    (Counters(other) & 255) != 0 && ++count > 2) return false;
            }
            var ruler = Lookup(StrategyNeed(current.ReadForceRecord(owner)?.RawRuler10, "journey_rumor_ruler10"));
            return !Likes(ruler, person) && !Sworn(person, ruler) && !Spouse(person, ruler) &&
                !Grandparent(person, ruler) && !Grandparent(ruler, new(person)) &&
                !Parent(person, ruler) && !Parent(ruler, new(person)) && Loyalty(person) < RumorLoyaltyLimit();
        }

        private bool Before(int first, int second, int kind)
        {
            if (kind == 12)
            {
                byte a = Loyalty(first), b = Loyalty(second);
                if (a != b) return a < b;
                int sumA = AbilitySum(first), sumB = AbilitySum(second);
                if (sumA != sumB) return sumA > sumB;
            }
            else
            {
                int a = Ability(first, 0), b = Ability(second, 0);
                if (a != b) return a > b;
                byte loyaltyA = Loyalty(first), loyaltyB = Loyalty(second);
                if (loyaltyA != loyaltyB) return loyaltyA < loyaltyB;
                a = Ability(first, 4); b = Ability(second, 4);
                if (a != b) return a < b;
            }
            return Identity(first) < Identity(second);
        }

        private int AbilitySum(int person)
        {
            int result = 0;
            for (int dimension = 0; dimension < 5; dimension++) result = unchecked(result + Ability(person, dimension));
            return result;
        }
        private int Ability(int person, int dimension)
        {
            if (abilities.TryGetValue((person, dimension), out int cached)) return cached;
            abilitySource ??= new(current, package._scenario, package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language));
            int value = StrategyNeed(abilitySource.ResolveDimension(state, Identity(person), dimension,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect)), "target_sort_ability");
            abilities.Add((person, dimension), value); return value;
        }

        private bool Sworn(int person, TransferPersonReference other) => DistinctValid(person, other) &&
            Word(person, 0xBC) != 0 && Word(person, 0xBC) == Word(Physical(other), 0xBC);
        private bool Spouse(int person, TransferPersonReference other) => DistinctValid(person, other) &&
            Word(person, 0xBA) != 0 && Word(person, 0xBA) == Identity(Physical(other));
        private bool Parent(int person, TransferPersonReference other) => DistinctValid(person, other) &&
            (Word(person, 0xB4) != 0 && Word(person, 0xB4) == Identity(Physical(other)) ||
             Word(person, 0xB6) != 0 && Word(person, 0xB6) == Identity(Physical(other)));
        private bool Parent(TransferPersonReference person, TransferPersonReference other) =>
            Valid(person) && Parent(Physical(person), other);
        private bool Grandparent(int person, TransferPersonReference other)
        {
            if (!DistinctValid(person, other)) return false;
            var father = Lookup(Word(person, 0xB4));
            if (Valid(father) && Parent(Physical(father), other)) return true;
            var mother = Lookup(Word(person, 0xB6));
            return Valid(mother) && Parent(Physical(mother), other);
        }
        private bool Grandparent(TransferPersonReference person, TransferPersonReference other) =>
            Valid(person) && Grandparent(Physical(person), other);
        private bool Likes(TransferPersonReference person, int other)
        {
            // 233010 only tests the second receiver's validity.
            if (!StrategyNeed(current.ReadPersonNativeValidity(other), "rumor_like_target_validity")) return false;
            int physical = Physical(person);
            for (int index = 0; index < 8; index++)
                if (StrategyNeed(OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, physical, index), "rumor_ruler_like_slot") == Identity(other)) return true;
            return false;
        }
        private bool DistinctValid(int person, TransferPersonReference other) =>
            StrategyNeed(current.ReadPersonNativeValidity(person), "rumor_relation_first_validity") &&
            Valid(other) && Identity(person) != Identity(Physical(other));
        private bool Valid(TransferPersonReference person) => person.PhysicalSlot is int physical &&
            StrategyNeed(current.ReadPersonNativeValidity(physical), "rumor_relation_second_validity");
        private bool SpecialPerson(int person, int owner) => BattleRemakeCurrentRegionFactor.IsNativeBandPerson(Identity(person)) &&
            Home(person) is >= 1 and <= 61 && owner is not (>= 46 and <= 50);
        private ushort Word(int person, int field) => StrategyNeed(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, person, field), "rumor_relation_" + field.ToString("X"));
        private TransferPersonReference Lookup(ushort identity) =>
            (identity <= 6000 ? current.ReadPersonLookupReference(identity) : current.ReadTransferPersonTableReference(0))
            ?? throw StrategyMissing("rumor_relation_Person_lookup");
        private static int Physical(TransferPersonReference person) => StrategyNeed(person.PhysicalSlot, "rumor_relation_Person_pointer");
        private ForceReference Force(int person) => current.ReadPersonForce(person) ?? throw StrategyMissing("target_Person_Force");
        private int ForceOrdinal(ForceReference force) => StrategyNeed(current.ReadForcePhysicalSlot(force), "target_Force_ordinal");
        private ushort Identity(int person) => StrategyNeed(current.ReadPersonIdentity(person), "target_Person_identity");
        private ushort Home(int person) => StrategyNeed(current.ReadPersonFirstLocation(person), "target_Person_E2");
        private byte Status(int person) => StrategyNeed(current.ReadPersonStatus(person), "target_Person_E6");
        private ushort Flags(int person) => StrategyNeed(current.ReadArmyReturnPersonFlags196(person), "target_Person_flags15E");
        private NativeLocation Position(int person) => PersonPositionRepair.DecodeSecondLocation(StrategyNeed(current.ReadPersonSecondLocation(person), "target_Person_E4"));
        private byte Loyalty(int person) => StrategyNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, package._governorInputs), "target_Person_E8");
        private ushort Counters(int person) => StrategyNeed(PersonAssignmentPrecallInputs.ReadCurrentResetWord1FF(state, current, person), "rumor_Person_1C7_1C8");
        // Offline readable_ghidra/data.tsv row254699: 7144D0's static
        // loyalty limit is 100. This rule does not read the original program.
        private static int RumorLoyaltyLimit() => 100;
        private byte ForceByte(int force, int field)
        {
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force, out var reset) == true)
                return StrategyNeed(reset[field], "target_Force_reset_" + field.ToString("X"));
            var fields = state.NativeArmySimulation?.Forces.GetValueOrDefault(force) ?? throw StrategyMissing("target_Force_current");
            return StrategyNeed(field == 0x12 ? fields.Flags12 : fields.Flags194, "target_Force_" + field.ToString("X"));
        }
        private byte ForceRelation(int first, int second)
        {
            if (second is < 0 or >= 52) throw StrategyMissing("target_relation_Force_index");
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(first, out var reset) == true)
                return StrategyNeed(reset[0xEA + second], "target_Force_reset_relation");
            return state.NativeArmySimulation?.Forces.GetValueOrDefault(first)?.RelationsEA is { Length: 52 } values
                ? values[second] : throw StrategyMissing("target_Force_RelationsEA");
        }
    }
}
