using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual monthly pay, reached once per City before Turn food.
    /// The same operation-local current world owns its Gate recursion and
    /// temporary lists. Unimplemented shortage effects stop the private turn.</summary>
    private GameState CompleteNativeMonthlySalary(GameState input, SourceWorldContext current, BaseReference city)
    {
        // These read-only rule constants are qualified against the TC typed
        // data and reference cache. No original file is consulted at runtime.
        if (Seed.Language != "TC") throw SalaryMissing("SC_rule_constants");
        const int captivePay = 50;
        var working = input;
        TacticRemakeCurrentAbilities? abilities = null;
        PayBase(city);
        return working;

        void PayBase(BaseReference receiver)
        {
            int location = Known(current.ReadBaseObjectLink(receiver), "Base_location");
            var employed = current.CreateNearbyTemporary();
            var captives = current.CreateNearbyTemporary();
            var releaseCaptives = current.CreateNearbyTemporary();
            var departingSpecialPeople = current.CreateNearbyTemporary();
            try
            {
                // CFootholdPersonIterator mode4 filters only E2 against the
                // Base's location1..61; this is not the Base88 home list.
                foreach (var entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw SalaryMissing("Person_registry"))
                {
                    int person = entry ?? throw SalaryMissing("Person_pointer");
                    if (location is < 1 or > 61 || Known(current.ReadPersonFirstLocation(person), "PersonE2") != location) continue;
                    int rawForce = Known(current.ReadBaseForceSlot(receiver), "Base_Force");
                    int personForce = Known(current.ReadPersonOldForceSlot(person), "Person_District_Force");
                    byte status = Known(current.ReadPersonStatus(person), "PersonE6");
                    // 22CF60 returns the raw district Force byte. Do not
                    // replace this source comparison with pointer identity.
                    if (personForce == rawForce && status is >= 2 and <= 4) Append(employed, person);
                    else if (status == 6) Append(captives, person);
                }

                int available = CollectorGold(receiver), paid = 0;
                Sort(employed, EmployedBefore);
                foreach (int person in People(employed))
                {
                    int due = Salary(person);
                    if (available < due)
                        throw SalaryMissing($"shortage_Person{PersonId(person)}_loyalty_relationships_reports");
                    available = unchecked(available - due);
                    paid = unchecked(paid + due);
                    if (!Known(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(person)), "Person_flags14"))
                    {
                        byte loyalty = Known(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "Person_flags13")
                            ? (byte)255 : (byte)120;
                        current.SetPersonDestructionLoyaltyE8(person, loyalty);
                        working = ArmyDestructionLoyalty.PublishCurrentRawE8(working, current, person, loyalty);
                    }
                }
                Sort(captives, CaptiveBefore);
                foreach (int person in People(captives))
                {
                    if (available < captivePay)
                        throw SalaryMissing($"shortage_captive{PersonId(person)}_release_2E7890");
                    available = unchecked(available - captivePay);
                    paid = unchecked(paid + captivePay);
                }

                // Gate virtual Gold90 delegates to its mapped City's getter.
                // The final debit always resolves the original receiver's
                // mapped City, and increments that City's expense by paid.
                var payer = CurrentGateMapQueries.FindCity(receiver, current);
                if (Known(current.ReadBaseNativeValidity(payer), "payer_validity"))
                {
                    int id = Known(payer.PhysicalSlot, "payer_identity");
                    if (payer.Kind != BaseKind.City || !working.Cities.TryGetValue(id, out var owner))
                        throw SalaryMissing("payer_domain_City");
                    int before = Known(current.ReadBaseResource(payer, BaseResourceKind.Gold), "payer_Gold");
                    int cap = BasePostLoad.ReadResourceCapacities(payer, current).Gold;
                    int after = Math.Clamp(unchecked(before - paid), 0, cap);
                    current.SetBaseResource(payer, BaseResourceKind.Gold, after);
                    int expense = owner.PeriodGoldExpenditure ?? throw SalaryMissing("expense_reset_order");
                    working.Cities[id] = owner with { Gold = after, PeriodGoldExpenditure = unchecked(expense + paid) };
                }

                if (location < 52)
                {
                    var gates = current.CreateNearbyTemporary();
                    try
                    {
                        foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw SalaryMissing("Gate_registry"))
                        {
                            var gate = entry ?? throw SalaryMissing("Gate_pointer");
                            if (CurrentGateMapQueries.FindCity(gate, current) != receiver) continue;
                            var node = current.AllocateBaseRegistryNode(new(gates.Identity)) ?? throw SalaryMissing("Gate_node");
                            if (node.Identity.HasValue) current.WriteBaseRegistryNodePayload(node, gate);
                        }
                        var cursor = current.ReadRegistryHead(new(gates.Identity)) ?? throw SalaryMissing("Gate_list_head");
                        while (cursor.Identity.HasValue)
                        {
                            PayBase(current.ReadBaseRegistryNodePayload(cursor) ?? throw SalaryMissing("Gate_list_payload"));
                            cursor = current.ReadRegistryNodeNext(cursor) ?? throw SalaryMissing("Gate_list_next");
                        }
                    }
                    finally { current.ClearNearbyTemporary(gates, true); }
                }
            }
            finally
            {
                // Match the source descriptor release order after recursion.
                current.ClearNearbyTemporary(departingSpecialPeople, true);
                current.ClearNearbyTemporary(releaseCaptives, true);
                current.ClearNearbyTemporary(captives, true);
                current.ClearNearbyTemporary(employed, true);
            }
        }

        int Salary(int person) => ReadNativeMonthlyOfficerSalaryCurrent(working, current, person);
        int CollectorGold(BaseReference receiver)
        {
            var source = receiver.Kind == BaseKind.Gate ? CurrentGateMapQueries.FindCity(receiver, current) : receiver;
            return Known(current.ReadBaseResource(source, BaseResourceKind.Gold), "collector_Gold");
        }
        byte Office(int person)
        {
            var owner = working.Officers.GetValueOrDefault(PersonId(person)) ?? throw SalaryMissing("Person_domain_owner");
            return Known(OfficerAppointmentSourceAdapter.ReadOfficeId(owner.Appointment), "Person_officeE7");
        }
        int PersonId(int person) => Known(current.ReadPersonIdentity(person), "Person_identity");
        bool EmployedBefore(int left, int right)
        {
            byte a = Office(left), b = Office(right);
            if (a != b) return a < b;
            byte al = Known(ArmyDestructionLoyalty.ReadCurrentRawE8(working, current, left, _governorInputs), "sort_left_E8");
            byte bl = Known(ArmyDestructionLoyalty.ReadCurrentRawE8(working, current, right, _governorInputs), "sort_right_E8");
            return al != bl ? al > bl : PersonId(left) < PersonId(right);
        }
        bool CaptiveBefore(int left, int right)
        {
            var a = CaptiveAbilities(left); var b = CaptiveAbilities(right);
            int am = Math.Max(0, a.Max()), bm = Math.Max(0, b.Max());
            if (am != bm) return am > bm;
            int sumA = 0, sumB = 0;
            foreach (int value in a) sumA = unchecked(sumA + value);
            foreach (int value in b) sumB = unchecked(sumB + value);
            return sumA != sumB ? sumA > sumB : PersonId(left) < PersonId(right);
        }
        int[] CaptiveAbilities(int person)
        {
            abilities ??= new(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            return Enumerable.Range(0, 5).Select(dimension => Known(abilities.ResolveDimension(working, PersonId(person), dimension,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(working, force, effect)), "captive_sort_ability")).ToArray();
        }
        void Append(HexListHandle list, int person)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw SalaryMissing("Person_list_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, person));
        }
        IEnumerable<int> People(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw SalaryMissing("Person_list_head");
            while (node.Identity.HasValue)
            {
                var value = current.ReadRegistryNodePayload(node) ?? throw SalaryMissing("Person_list_payload");
                if (value.Kind != RegistryKind.Person) throw SalaryMissing("Person_list_kind");
                yield return Known(value.PhysicalSlot, "Person_list_pointer");
                node = current.ReadRegistryNodeNext(node) ?? throw SalaryMissing("Person_list_next");
            }
        }
        void Sort(HexListHandle list, Func<int, int, bool> before)
        {
            var nodes = new List<HexListNodeReference>();
            var node = current.ReadHexListHead(list) ?? throw SalaryMissing("sort_head");
            while (node.Identity.HasValue)
            {
                nodes.Add(node);
                node = current.ReadHexListNodeNext(node) ?? throw SalaryMissing("sort_next");
            }
            int Person(HexListNodeReference at)
            {
                var value = current.ReadRegistryNodePayload(new(at.Identity)) ?? throw SalaryMissing("sort_payload");
                if (value.Kind != RegistryKind.Person) throw SalaryMissing("sort_payload_kind");
                return Known(value.PhysicalSlot, "sort_pointer");
            }
            for (int index = 1; index < nodes.Count; index++)
            {
                var moving = nodes[index];
                int at = index;
                while (at > 0 && before(Person(moving), Person(nodes[at - 1]))) at--;
                if (at == index) continue;
                nodes.RemoveAt(index); nodes.Insert(at, moving);
                current.ReorderCurrentList(list, nodes);
            }
        }
        T Known<T>(T? value, string field) where T : struct => value ?? throw SalaryMissing(field);
    }

    internal int ReadNativeMonthlyOfficerSalaryCurrent(GameState state, SourceWorldContext current, int person)
    {
        if (Seed.Language != "TC") throw SalaryMissing("SC_rule_constants");
        if (!K(current.ReadPersonNativeValidity(person), "salary_Person_validity")) return 0;
        int identity = K(current.ReadPersonIdentity(person), "Person_identity");
        var owner = state.Officers.GetValueOrDefault(identity) ?? throw SalaryMissing("Person_domain_owner");
        byte office = K(OfficerAppointmentSourceAdapter.ReadOfficeId(owner.Appointment), "Person_officeE7");
        if (!K(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, office), "salary_office_validity")) office = 73;
        var parameters = state.NativeArmySimulation?.Parameters ?? throw SalaryMissing("saved_parameters");
        string key = OwnedArmyGovernorComparatorInputs.OfficeGuard36Key(office);
        if (parameters.Int32Values.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) != 1 ||
            parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key, out int officePay) ||
            officePay is < 0 or > 255) throw SalaryMissing("salary_office36");
        int basis = unchecked(officePay * 10);
        if (!K(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(person)), "salary_Person_flags14")) basis = 10;
        int trait = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 46), "salary_trait46");
        var force = current.ReadPersonForce(person) ?? throw SalaryMissing("salary_Force_pointer");
        int policy = K(current.ReadForceNativeValidity(force), "salary_Force_validity")
            ? K(_baseCapacityInputs.ReadPanelModifier(state, K(force.PhysicalSlot, "salary_Force_identity"), 12), "salary_policy12") : 0;
        return unchecked(basis - unchecked(policy * basis) / 100 - unchecked(trait * basis) / 100);

        static T K<T>(T? value, string field) where T : struct => value ?? throw SalaryMissing(field);
    }

    private static InvalidDataException SalaryMissing(string owner) => new("army.Turn_salary." + owner);
}
