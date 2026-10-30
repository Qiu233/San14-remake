using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //TC27BFB0/2B0FC0 and SC27BCB0/2B0DE0 retain the caller's actual pointer lists.
    //Allocation suppression affects exclusions, candidates and final followers.
    internal bool TrySelectForceEmergenceFollowersCurrent(ref GameState state,
        SourceWorldContext current, int leaderPhysical, HexListHandle followers)
    {
        var latest = state;
        Lock(true);
        try { current.ClearTemporaryHexList(followers); }
        finally { Lock(false); }
        try
        {
            int leader = leaderPhysical;
            if (!Valid(leader) || Status(leader) is not (5 or 8)) return false;
            var home = ResolveForceEmergenceHomeCurrent(current, leader);
            if (!K(current.ReadBaseNativeValidity(home), "leader_City_validity") ||
                K(current.ReadBaseForceSlot(home), "leader_City_Force") != 0) return false;
            var emperorCity = EmperorCity();
            if (K(current.ReadBaseNativeValidity(emperorCity), "Emperor_City_validity") && emperorCity == home) return false;
            int homePhysical = K(home.PhysicalSlot, "leader_City_pointer");
            foreach (var entry in Registry(RegistryKind.Area))
            {
                var area = new AreaReference(K(entry.PhysicalSlot, "CityArea_pointer"));
                if (K(current.ReadAreaCitySlot(area), "CityArea35") == homePhysical &&
                    (K(current.ReadAreaFlags38(area), "CityArea38") & 5) != 0) return false;
            }
            if (SpecialArmyPresent(homePhysical, true) || SpecialArmyPresent(homePhysical, false)) return false;
            ushort id = Id(leader);
            if (id == 147 && Status(Lookup(952)) != 9 ||
                id == 656 && (Status(Lookup(952)) != 9 || Status(Lookup(147)) != 9)) return false;

            var excluded = Temporary();
            try
            {
                var queries = new PersonReturnGovernorQueries(latest, _governorInputs, current);
                foreach (var task in current.ReadCurrentPersonLinkedEntities() ?? throw Missing("associated_registry18"))
                {
                    if (task is null) throw Missing("associated_receiver");
                    if (K(queries.ReadCurrentAssociatedVirtual98(task), "associated_virtual98") != 16) continue;
                    var members = current.ReadLinkedEntityMemberIds(task) ?? throw Missing("Disappearances_members");
                    if (members.Count != 0)
                    {
                        uint member = K(members[0], "Disappearances_first_member");
                        Append(excluded, new(RegistryKind.Person, Lookup(member < 6001 ? (int)member : 6001)));
                    }
                }
                var candidates = Temporary();
                try
                {
                    foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
                    {
                        int person = K(entry, "candidate_pointer");
                        if (person == leader || ExcludedSpecial(person) || Contains(excluded, person)) continue;
                        int kind = PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(person), "candidate_E4")).Kind;
                        if (Status(person) != 1 && kind is 5 or 6 &&
                            (StrategicIndependenceSwornCurrent(latest, current, leader, person) ||
                             StrategicIndependenceSpouseCurrent(latest, current, leader, person)))
                            Append(followers, new(RegistryKind.Person, person));
                        else if (Compatible(leader, person)) Append(candidates, new(RegistryKind.Person, person));
                    }
                    var nodes = Read(candidates);
                    if (nodes.Count != 0)
                    {
                        PersonFreedomHomeSource.Shuffle(current, nodes,
                            ReadArmyDestructionSampler(latest, current) ?? throw Missing("World450_45C"));
                        var saved = latest.CampaignAppRandom32 ?? throw Missing("App32");
                        var random = new SharedSourceRandom32(saved.State);
                        int quota = Math.Min(nodes.Count, random.Next(4) + 3);
                        latest = latest with { CampaignAppRandom32 = new(random.ReadCurrentState()) };
                        for (int index = 0; index < quota; index++) Append(followers, nodes[index].Payload);
                    }
                    return K(current.ReadTemporaryHexListCount(followers), "followers_count") > 2;
                }
                finally { Release(candidates); }
            }
            finally { Release(excluded); }
        }
        finally { state = latest; }

        bool Compatible(int leader, int person)
        {
            var leaderHome = ResolveForceEmergenceHomeCurrent(current, leader);
            var personHome = ResolveForceEmergenceHomeCurrent(current, person);
            if (leaderHome != personHome && !Neighbor(personHome, leaderHome)) return false;
            byte status = Status(person);
            bool admitted = status is 5 or 8;
            if (status is >= 2 and <= 4)
            {
                int force = PersonForce(person);
                if (force != 51 && force is not (>= 46 and <= 50) &&
                    !PersonCurrentBusySource.Read(current, person, Missing) &&
                    K(ArmyDestructionLoyalty.ReadCurrentRawE8(latest, current, person, _governorInputs), "candidate_E8") <=
                    Rule("serving-loyalty-limit", 75)) admitted = true;
            }
            if (!admitted || OfficeRelationList(latest, current, person, leader, 0xD0) ||
                OfficeRelationList(latest, current, leader, person, 0xD0)) return false;
            return Affinity(person, leader) < Rule("affinity-limit", 10) ||
                OfficeRelationList(latest, current, person, leader, 0xC0);
        }
        bool Neighbor(BaseReference first, BaseReference second)
        {
            var temporary = Temporary();
            try
            {
                (_eventCityNeighborhoods ??= new(_scenario)).FillCities22F430(current, first, temporary);
                return Read(temporary).Any(entry => entry.Payload.Kind == RegistryKind.City && entry.Payload.PhysicalSlot == second.PhysicalSlot);
            }
            finally { Release(temporary); }
        }
        bool ExcludedSpecial(int person)
        {
            //233240(Person,0), then232A50: both require an actual Base E2.
            bool band = BattleRemakeCurrentRegionFactor.IsNativeBandPerson(Id(person));
            int location = K(current.ReadPersonFirstLocation(person), "candidate_E2");
            if (location is < 1 or > 61) return false;
            int force = PersonForce(person);
            if (band && force is not (>= 46 and <= 50)) return true;
            return force is >= 46 and <= 50;
        }
        bool SpecialArmyPresent(int city, bool wandering)
        {
            foreach (var entry in Registry(RegistryKind.Army))
            {
                if (K(current.ReadOwnedArmyExclusion233560(entry), "Army233560")) continue;
                var army = new ArmyUnitReference(K(entry.PhysicalSlot, "Army_pointer"));
                int force = K(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army)), "Army_Force");
                if (wandering ? force != 51 : force is not (>= 46 and <= 50)) continue;
                int rawHex = K(current.ReadArmyUnitRawHex2A(army), "Army2A");
                var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw Missing("Army_Hex_pointer");
                int rawArea = K(current.ReadHexAreaSlot(hex), "Army_Hex12");
                var area = current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0) ?? throw Missing("Army_Area_pointer");
                if (K(current.ReadAreaNativeValidity(area), "Army_Area_validity") &&
                    K(current.ReadAreaCitySlot(area), "Army_Area35") == city) return true;
            }
            return false;
        }
        BaseReference EmperorCity()
        {
            int emperor = Lookup(K(CityCaptureTerritoryInputs.ReadEmperor1654(latest), "World1654_Emperor"));
            int city = 0;
            if (Valid(emperor))
            {
                var position = PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(emperor), "Emperor_E4"));
                if (position.Kind == 5) city = position.Index < 52 ? position.Index : 0;
            }
            return current.ReadCityTableReference(city) ?? throw Missing("Emperor_City_pointer");
        }
        int Affinity(int first, int second)
        {
            if (!Valid(first) || !Valid(second)) return 0;
            int difference = Math.Abs(K(OwnedPersonRelationInputs.ReadCurrentAffinity(latest, first), "affinity_first") -
                K(OwnedPersonRelationInputs.ReadCurrentAffinity(latest, second), "affinity_second"));
            return Math.Min(75, Math.Min(difference, 150 - difference));
        }
        bool Contains(HexListHandle list, int person)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("exclusion_head");
            while (node.Identity.HasValue)
            {
                var value = current.ReadRegistryNodePayload(node) ?? throw Missing("exclusion_payload");
                if (value.Kind == RegistryKind.Person && value.PhysicalSlot == person) return true;
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("exclusion_next");
            }
            return false;
        }
        List<PersonFreedomHomeSource.Entry> Read(HexListHandle list)
        {
            var result = new List<PersonFreedomHomeSource.Entry>();
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("temporary_head");
            while (node.Identity.HasValue)
            {
                result.Add(new(new(node.Identity), current.ReadRegistryNodePayload(node) ?? throw Missing("temporary_payload")));
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("temporary_next");
            }
            return result;
        }
        IEnumerable<RegistryObjectReference> Registry(RegistryKind kind)
        {
            foreach (var entry in current.ReadRegistryReferences(kind) ?? throw Missing("registry_" + kind))
                yield return entry is not null && entry.Kind == kind ? entry : throw Missing("registry_payload_" + kind);
        }
        int Rule(string name, int value) => NormalTurnForceEmergenceParameters.Read(latest, "force-emergence-followers." + name, value);
        int PersonForce(int person) => K(current.ReadForcePhysicalSlot(current.ReadPersonForce(person) ?? throw Missing("Person_Force_pointer")), "Person_Force");
        byte Status(int person) => K(current.ReadPersonStatus(person), "PersonE6");
        ushort Id(int person) => K(current.ReadPersonIdentity(person), "Person10");
        bool Valid(int person) => K(current.ReadPersonNativeValidity(person), "Person_validity");
        int Lookup(int id) => K((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        HexListHandle Temporary()
        {
            Lock(true);
            try { return current.CreateTemporaryHexList() ?? throw Missing("temporary_owner"); }
            finally { Lock(false); }
        }
        void Append(HexListHandle list, RegistryObjectReference payload)
        {
            Lock(true);
            try
            {
                var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node_owner");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, payload);
            }
            finally { Lock(false); }
        }
        void Release(HexListHandle list)
        {
            Lock(true);
            try { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
            finally { Lock(false); }
        }
        void Lock(bool enter)
        {
            if (!K(current.ReadRegistryLockEnabled(), "registry_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => ForceEmergenceCandidatesMissing(field);
    }

    //2A9DC0 uses raw E4, including Army current Home, rather than a route's
    //interpolated map position or the person's saved first-location fallback.
    internal BaseReference ResolveForceEmergenceHomeCurrent(SourceWorldContext current, int person)
    {
        int raw = current.ReadPersonSecondLocation(person) ?? throw ForceEmergenceCandidatesMissing("Home_PersonE4");
        if (raw < 52) return City(raw);
        if (raw <= 61) return CurrentGateMapQueries.FindCity(current.ReadGateTableReference(raw - 51)
            ?? throw ForceEmergenceCandidatesMissing("Home_Gate_pointer"), current);
        if (raw is >= 562 and <= 1061)
        {
            var area = current.ReadAreaTableReference(raw - 561) ?? throw ForceEmergenceCandidatesMissing("Home_Area_pointer");
            int city = current.ReadAreaCitySlot(area) ?? throw ForceEmergenceCandidatesMissing("Home_Area35");
            return City(city < 52 ? city : 0);
        }
        if (raw <= 561)
        {
            var army = current.ReadArmyUnitTableReference(raw - 61) ?? throw ForceEmergenceCandidatesMissing("Home_Army_pointer");
            if (current.ReadArmyUnitNativeValidity(army) ?? throw ForceEmergenceCandidatesMissing("Home_Army_validity"))
                return CurrentGateMapQueries.FindCity(current.FindArmyUnitCurrentBase(army), current);
        }
        return City(0);
        BaseReference City(int value) => current.ReadCityTableReference(value) ?? throw ForceEmergenceCandidatesMissing("Home_City_pointer");
    }
    private static InvalidDataException ForceEmergenceCandidatesMissing(string field) => new("normal-turn.force-emergence.followers." + field);
}
