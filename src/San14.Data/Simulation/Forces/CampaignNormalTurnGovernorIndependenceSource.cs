using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //NormalTurn20 calls2E5DF0, whose retained Force list predates every
    //independence callback. A newly created Force is not revisited this pass.
    internal GameState ApplyNormalTurnMonthlyGovernorIndependenceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        var effects = new List<CampaignEffectRequest>();
        var forces = Temporary();
        try
        {
            foreach (var row in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("Force_C0"))
            {
                if (row is not { Kind: RegistryKind.Force, PhysicalSlot: int force }) throw Missing("Force_payload");
                //The source tests these identities, not player ownership or
                //Force178. Force0, if actually registered, has City count0.
                if (force is >= 46 and <= 50 || force == 51) continue;
                Append(forces, RegistryKind.Force, force);
            }
            var forceNode = Head(forces);
            while (forceNode.Identity.HasValue)
            {
                var row = current.ReadRegistryNodePayload(new(forceNode.Identity)) ?? throw Missing("Force_temporary_payload");
                if (row is not { Kind: RegistryKind.Force, PhysicalSlot: int force }) throw Missing("Force_temporary_kind");
                SelectAndApply(force);
                forceNode = Next(forceNode);
            }
        }
        finally { Release(forces); }
        if (effects.Count != 0)
        {
            var pending = state.CampaignEffectRequests ?? throw Missing("pending_effect_owner");
            long revision = state.NativeClock?.UpdateOrdinal ?? throw Missing("effect_revision");
            state = state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance, revision, effects) };
        }
        return state.WithArmyLifecycle((state.ReadArmyLifecycle() ?? throw Missing("lifecycle")) with { Indexes = pool.ProjectOwnedRoles() });

        void SelectAndApply(int force)
        {
            //22AB60/2243A0 traverses the fixed52 City pointers. It has no
            //City native-validity condition; the target Force must be1..51.
            if (force is < 1 or > 51) return;
            int count = 0;
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw Missing("count_City_pointer");
                if (K(current.ReadBaseForceSlot(city), "count_City_Force") == force) count++;
            }
            if (count < 3) return;
            var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");
            var emperorCity = EmperorCity();
            int ruler = Lookup(K(current.ReadForceRecord(force)?.RawRuler10, "Force_ruler10"));
            var candidates = Temporary();
            try
            {
                foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
                {
                    int person = K(entry, "Person_pointer");
                    if (K(current.ReadPersonOldForceSlot(person), "candidate_Force") != force ||
                        Status(person) is < 1 or > 4 || Status(person) is not (2 or 3)) continue;
                    var home = BaseLookup.Find(K(current.ReadPersonFirstLocation(person), "candidate_E2"), current);
                    int homeCode = K(current.ReadBaseObjectLink(home), "candidate_Home4E");
                    if (homeCode is >= 52 and <= 61) continue;
                    var rulerCity = CurrentGateMapQueries.FindCity(BaseLookup.Find(K(current.ReadPersonFirstLocation(ruler), "ruler_E2"), current), current);
                    var personCity = CurrentGateMapQueries.FindCity(home, current);
                    if (K(current.ReadBaseObjectLink(rulerCity), "ruler_City4E") == K(current.ReadBaseObjectLink(personCity), "candidate_City4E")) continue;
                    if (K(current.ReadBaseNativeValidity(emperorCity), "Emperor_City_validity") &&
                        K(current.ReadBaseObjectLink(emperorCity), "Emperor_City4E") == K(current.ReadBaseObjectLink(personCity), "candidate_Emperor_comparison4E")) continue;
                    if (NearbyEnemy(home) || StrategicIndependenceSpouseCurrent(state, current, person, ruler) ||
                        StrategicIndependenceSwornCurrent(state, current, person, ruler) ||
                        Ancestor(person, ruler) || Ancestor(ruler, person)) continue;
                    bool dislikes = Dislikes(person, ruler);
                    if (OfficeRelatedCurrent(state, current, person, ruler) && !dislikes ||
                        OfficeRelationList(state, current, person, ruler, 0xC0) || Affinity(person, ruler) <= 5 ||
                        PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(person), "candidate_E4")).Kind != 5) continue;
                    var associated = current.ReadPersonLinkedEntity(person) ?? throw Missing("candidate_associated22AC00");
                    if (K(current.ReadLinkedEntityNativeValidity(associated), "candidate_associated_validity")) continue;
                    if (unchecked(Disposition(person) / Rule("loyalty-disposition-divisor", 4) + Loyalty(person)) >= Rule("loyalty-threshold", 90)) continue;
                    if (!dislikes && (Preference(person) < Rule("ambition-threshold", 10) ||
                        Affinity(person, ruler) <= Rule("ambition-affinity-threshold", 5))) continue;
                    Append(candidates, RegistryKind.Person, person);
                }
                var order = new List<HexListNodeReference>();
                for (var at = Head(candidates); at.Identity.HasValue; at = Next(at)) order.Add(at);
                if (order.Count == 0) return;
                //24F2A0/256220: dislikes first, lower raw loyalty, higher
                //nonzero office only when both are nonzero, then Person ID.
                for (int index = 1; index < order.Count; index++)
                {
                    var value = order[index]; int at = index;
                    while (at > 0 && Before(PersonAt(value), PersonAt(order[at - 1]))) { order[at] = order[at - 1]; at--; }
                    order[at] = value;
                }
                current.ReorderCurrentList(candidates, order);
                int rebel = PersonAt(order[0]);
                int title = K(current.ReadForceKingdomNameIndex(force), "Force48");
                //CKingdomData234270 validates a nonzero physical ordinal;
                //22DF50 returns that ordinal. Out-of-range Force48 selects0.
                int kingdom = title < 101 ? title : 0;
                int historicalPenalty = kingdom == 6 && Id(ruler) == 607 ? Rule("historical-kingdom-penalty", 30) : 0;
                //2E5DF0 supplies param2=null. Cached2E5C5A confirms the
                //optional15 bonus tests that excluded Person, so it is absent.
                int chance = Math.Clamp(unchecked(Preference(rebel) / Rule("chance-ambition-divisor", 4) -
                    historicalPenalty - Disposition(rebel) / Rule("chance-disposition-divisor", 4) +
                    Rule("chance-loyalty-base", 75) - Loyalty(rebel)), 0, 70);
                var sampler = ReadArmyDestructionSampler(state, current) ?? throw Missing("World450_45C");
                int sample = TacticEffectSourceSamplerMath.Sample(100, 1, unchecked(date.Year + date.Month), date.Day, Id(rebel), 0, sampler);
                if (sample >= chance) return;
                var applied = ApplyForceIndependenceCurrent(state, current, pool, new(force), rebel, null,
                    date, effects, (image, root, person) => ApplyNormalTurnIndependenceReportCurrent(
                        image, current, force, rebel, root, person, effects));
                state = applied.State;

                bool Before(int a, int b)
                {
                    bool first = Dislikes(a, ruler), second = Dislikes(b, ruler);
                    if (first != second) return first;
                    int loyaltyA = Loyalty(a), loyaltyB = Loyalty(b);
                    if (loyaltyA != loyaltyB) return loyaltyA < loyaltyB;
                    byte officeA = K(current.ReadMoraleWavePersonPostE7(a), "sort_first_E7"), officeB = K(current.ReadMoraleWavePersonPostE7(b), "sort_second_E7");
                    if (officeA != 0 && officeB != 0 && officeA != officeB) return officeA > officeB;
                    return Id(a) < Id(b);
                }
            }
            finally { Release(candidates); }
        }
        int Rule(string name, int literal) => StrategicParameter(state, "normal-governor-independence." + name, literal);
        int Disposition(int person) => K(current.ReadAllegianceDisposition(person), "Person16C");
        int Preference(int person) => K(RulerCaptureReleaseInputs.ReadCurrentWord(state, person, 0x17C), "Person17C");
        int Loyalty(int person) => K(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, _governorInputs), "PersonE8");
        byte Status(int person) => K(current.ReadPersonStatus(person), "PersonE6");
        ushort Id(int person) => K(current.ReadPersonIdentity(person), "Person10");
        int Lookup(int id) => K((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        bool Valid(int person) => K(current.ReadPersonNativeValidity(person), "Person_validity");
        bool Dislikes(int a, int b) => OfficeRelationList(state, current, a, b, 0xD0);
        bool Parent(int a, int b) => Valid(a) && Valid(b) && Id(a) != Id(b) &&
            (OfficeWord(state, a, 0xB4) != 0 && OfficeWord(state, a, 0xB4) == Id(b) ||
             OfficeWord(state, a, 0xB6) != 0 && OfficeWord(state, a, 0xB6) == Id(b));
        bool Ancestor(int a, int b) => Parent(a, b) || Parent(Lookup(OfficeWord(state, a, 0xB4)), b) || Parent(Lookup(OfficeWord(state, a, 0xB6)), b);
        int Affinity(int a, int b)
        {
            if (!Valid(a) || !Valid(b)) return 0;
            int difference = Math.Abs(K(OwnedPersonRelationInputs.ReadCurrentAffinity(state, a), "affinity_first_BE") - K(OwnedPersonRelationInputs.ReadCurrentAffinity(state, b), "affinity_second_BE"));
            return Math.Min(75, Math.Min(difference, 150 - difference));
        }
        bool NearbyEnemy(BaseReference home)
        {
            int at = K(home.PhysicalSlot, "Home_pointer");
            bool? value = home.Kind == BaseKind.City
                ? at == 0 ? state.UnassignedCityHasNearbyEnemyArmy : state.Cities.GetValueOrDefault(at)?.HasNearbyEnemyArmy
                : at == 0 ? state.UnassignedGateHasNearbyEnemyArmy : state.Gates.GetValueOrDefault(at + 51)?.HasNearbyEnemyArmy;
            return K(value, "Home67_nearby_enemy");
        }
        BaseReference EmperorCity()
        {
            int emperor = Lookup(K(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654_Emperor"));
            int city = 0;
            if (Valid(emperor))
            {
                var position = PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(emperor), "Emperor_E4"));
                if (position.Kind == 5) city = position.Index < 52 ? position.Index : 0;
            }
            return current.ReadCityTableReference(city) ?? throw Missing("Emperor_City_pointer");
        }
        HexListHandle Temporary()
        {
            Lock(true);
            try { return current.CreateTemporaryHexList() ?? throw Missing("temporary_owner"); }
            finally { Lock(false); }
        }
        void Append(HexListHandle list, RegistryKind kind, int physical)
        {
            Lock(true);
            try
            {
                var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node_owner");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(kind, physical));
            }
            finally { Lock(false); }
        }
        void Release(HexListHandle list)
        {
            Lock(true);
            try { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
            finally { Lock(false); }
        }
        HexListNodeReference Head(HexListHandle list) => current.ReadHexListHead(list) ?? throw Missing("temporary_head");
        HexListNodeReference Next(HexListNodeReference node) => current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next");
        int PersonAt(HexListNodeReference node)
        {
            var value = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("candidate_payload");
            return value is { Kind: RegistryKind.Person, PhysicalSlot: int person } ? person : throw Missing("candidate_kind");
        }
        void Lock(bool enter)
        {
            if (!K(current.ReadRegistryLockEnabled(), "registry_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => new("normal-turn.governor-independence." + field);
    }
}
