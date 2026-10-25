using San14.Core;

namespace San14.Data;

internal sealed record NormalTurnSelfRecommendationTarget(BaseReference Home, ForceReference Force, int? RulerPhysical);
internal sealed record NormalTurnFreePersonApplication(GameState State, bool PlayerApplication)
{
    internal NormalTurnSelfRecommendationTarget? Target { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal NormalTurnFreePersonApplication ApplyNormalTurnFreePersonCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int personPhysical)
        => new NormalFreePersonCurrent(this, state, current, personPhysical).Run();

    internal NormalTurnSelfRecommendationTarget ReadNormalTurnSelfRecommendationTargetCurrent(
        GameState state, SourceWorldContext current, int personPhysical)
        => new NormalFreePersonCurrent(this, state, current, personPhysical).Target();

    //29E080 takes a City, then reads its virtual78 District. It is not271C20's
    //recruit/captive transfer. Both NPC admission and the player's later answer
    //use this same current writer without another world capture or pool export.
    internal GameState JoinNormalTurnSelfRecommendationCurrent(GameState state,
        SourceWorldContext current, int personPhysical, BaseReference home)
    {
        var pool = current.OwnedIndexes ?? throw FreePersonMissing("join_shared_pool");
        int forceId = FreePersonNeed(current.ReadBaseForceSlot(home), "join_Base_Force");
        var force = current.ReadForceTableReference(forceId < 52 ? forceId : 0)
            ?? throw FreePersonMissing("join_Force_pointer");
        var working = new PersonAppearanceSource(this, state, current, pool)
            .ApplyHireJoinCurrent(personPhysical, home, force);
        int id = FreePersonNeed(current.ReadPersonIdentity(personPhysical), "join_Person_ID");
        var owner = working.Officers[id];
        var merit = owner.MeritState ?? throw FreePersonMissing("join_credit_cycle");
        //29E080 resets the whole dword1C0 and byte1C4, including both choice
        //counts retained by the generated-person prefix's narrower caller.
        working = working with { Officers = new(working.Officers) { [id] = owner with
            { MeritState = new(merit.Merit, 0, 2, 0, 0, 0) } } };
        if (!(current.ReadRegisteredPersonPhysicalSlots() ?? throw FreePersonMissing("join_Person100")).Contains(personPhysical))
            _ = ObjectRegistration.Register(new(RegistryKind.Person, personPhysical), current);
        if (FreePersonNeed(current.ReadBaseNativeValidity(home), "join_Base_validity"))
        {
            var packet = ApplyPersonFreedomGovernorCurrent(working, current, home);
            foreach (var write in packet.PersonWrites)
            {
                int changedId = FreePersonNeed(current.ReadPersonIdentity(write.PersonPhysical), "join_governor_Person_ID");
                var changed = working.Officers[changedId];
                var status = OfficerServiceStatusSourceAdapter.Decode(write.StatusAfter);
                working = working with { Officers = new(working.Officers) { [changedId] = changed with
                    { ServiceStatus = status, Role = OfficerServiceRules.PublishedRole(status, changed.Role) } } };
            }
        }
        int rulerId = FreePersonNeed(current.ReadForceRecord(forceId < 52 ? forceId : 0)?.RawRuler10, "join_ruler_ID");
        var aftermath = current.ReadOfficerAftermath(personPhysical) ?? throw FreePersonMissing("join_resentment");
        if (aftermath.Resentment?.TargetOfficerId == rulerId)
        {
            current.SetOfficerAftermath(personPhysical, aftermath with { Resentment = new(0, 0),
                SourceKnowledge = aftermath.SourceKnowledge is { } residue
                    ? residue with { UnmappedResentmentTarget = null } : null });
            working = current.PublishOfficerAftermath(working, id);
        }
        return working;
    }

    private static T FreePersonNeed<T>(T? value, string owner) where T : struct => value ?? throw FreePersonMissing(owner);
    private static InvalidDataException FreePersonMissing(string owner) => new("normal.turn.free-person." + owner);

    private sealed class NormalFreePersonCurrent(ArmyCampaignRuleAdapter package, GameState state,
        SourceWorldContext current, int person)
    {
        private GameState latest = state;
        private TacticSourceSamplerWorld? sampler;
        private PersonFreedomHomeSource? neighborhoods;

        internal NormalTurnFreePersonApplication Run()
        {
            Move(); //2A4730 always reaches movement before29DD60.
            if (FreePersonNeed(package.ReadApplicationTutorialActive(), "recommendation_tutorial_service")) return new(latest, false);
            if (Period() <= Parameter("recommendation-start-period", 18)) return new(latest, false);
            var target = Target();
            int force = ForceId(target.Force);
            if (!FreePersonNeed(current.ReadForceNativeValidity(target.Force), "recommendation_Force_validity") || force is >= 46 and <= 50)
                return new(latest, false);
            int ruler = target.RulerPhysical ?? throw FreePersonMissing("recommendation_ruler_pointer");
            if (Relation(person, ruler, false) || Resentment(person) == Id(ruler) || Disappearing()) return new(latest, false);
            int chance = RecommendationChance();
            if (!Trial(chance)) return new(latest, false);
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(latest, current)
                ?? throw FreePersonMissing("recommendation_current_player");
            if (ForceId(player) != 0 && ForceId(player) == force)
                return new(latest, true) { Target = target };
            latest = package.JoinNormalTurnSelfRecommendationCurrent(latest, current, person, target.Home);
            return new(latest, false);
        }

        internal NormalTurnSelfRecommendationTarget Target()
        {
            var position = Position();
            var area = Area(position.Index);
            var home = City(FreePersonNeed(current.ReadAreaCitySlot(area), "target_Area35"));
            if (position.Kind == 5)
            {
                home = City(position.Index);
                area = PersonFreedomHomeSource.FindBaseArea229FD0(current, home);
            }
            else if (position.Kind == 6)
            {
                home = CurrentGateMapQueries.FindCity(Gate(position.Index), current);
                area = PersonFreedomHomeSource.FindBaseArea229FD0(current, home);
            }
            var force = AreaForce(area);
            int ordinal = ForceId(force);
            int? ruler = FreePersonNeed(current.ReadForceNativeValidity(force), "target_Force_validity") &&
                ordinal is not (>= 46 and <= 50) ? Ruler(force) : null;
            return new(home, force, ruler);
        }

        private void Move()
        {
            if (Period() <= Parameter("movement-start-period", 18) || Disappearing()) return;
            if (!Trial(MovementChance())) return;
            var oldCity = CurrentCity();
            var cities = Temporary();
            HexListHandle? areas = null;
            try
            {
                int priority = int.MaxValue;
                foreach (int physical in Registry(RegistryKind.City))
                {
                    var city = City(physical);
                    if (CityAreas(city).Any(area => (FreePersonNeed(current.ReadAreaFlags38(new(area)), "movement_Area38") & 5) != 0)) continue;
                    var force = Force(FreePersonNeed(current.ReadBaseForceSlot(city), "movement_City_Force"));
                    if (ForceId(force) is >= 46 and <= 50) continue;
                    if (FreePersonNeed(current.ReadForceNativeValidity(force), "movement_Force_validity"))
                    {
                        int ruler = Ruler(force);
                        if (Valid(ruler) && (Relation(person, ruler, false) || Resentment(person) == Id(ruler))) continue;
                    }
                    if (!Neighbor(oldCity, city)) continue;
                    int firstProvince = Province(oldCity), secondProvince = Province(city);
                    if (firstProvince == secondProvince)
                    {
                        if (priority > 1) { priority = 1; current.ClearTemporaryHexList(cities); }
                        Add(cities, RegistryKind.City, physical);
                    }
                    if (priority > 1 && Region(firstProvince) == Region(secondProvince))
                    {
                        if (priority > 2) { priority = 2; current.ClearTemporaryHexList(cities); }
                        Add(cities, RegistryKind.City, physical);
                    }
                    if (priority == int.MaxValue) Add(cities, RegistryKind.City, physical);
                }
                var cityValues = Values(cities);
                if (cityValues.Count == 0) return;
                PersonFreedomHomeSource.Shuffle(current, cityValues, Sampler());
                var chosen = City(FreePersonNeed(cityValues[0].Payload.PhysicalSlot, "movement_chosen_City"));
                var candidates = CityAreas(chosen).ToArray();
                //22A160→3FCF40 deliberately consumes this App draw even though
                //the source discards its result before the independent shuffle.
                App(candidates.Length);
                areas = Temporary();
                foreach (int area in candidates) Add(areas, RegistryKind.Area, area);
                var areaValues = Values(areas);
                if (areaValues.Count == 0) return;
                PersonFreedomHomeSource.Shuffle(current, areaValues, Sampler());
                int selected = FreePersonNeed(areaValues[0].Payload.PhysicalSlot, "movement_chosen_Area");
                var previous = PositionArea();
                if (previous is null || previous.PhysicalSlot == selected) return;
                ushort location = FreePersonNeed(current.ReadBaseObjectLink(chosen), "movement_City_ID");
                current.WritePersonFirstLocation(person, location);
                current.WritePersonSecondLocation(person, selected is >= 1 and <= 500 ? checked((ushort)(selected + 561)) : (ushort)0);
                int id = Id(person);
                latest = latest with { Officers = new(latest.Officers) { [id] = latest.Officers[id] with
                    { LocationId = location == 0 ? null : location } } };
                latest = OfficerLocationSourceAdapter.PublishPosition(latest, id, current);
            }
            finally
            {
                if (areas is not null) Release(areas);
                Release(cities);
            }
        }

        private int MovementChance()
        {
            var area = PositionArea();
            if (area is null) return 0;
            int ruler = Ruler(AreaForce(area));
            int resentment = Valid(ruler) && Resentment(person) == Id(ruler) ? 85 : 20;
            int relationship = !Valid(ruler) ? 20 : Relation(person, ruler, true) ? 0
                : Relation(person, ruler, false) ? 50 : Affinity(person, ruler) < 11 ? 5 : 20;
            int flags = FreePersonNeed(current.ReadAreaFlags38(area), "movement_chance_Area38");
            return Math.Clamp((int)unchecked((byte)(resentment + relationship + ((flags & 1) != 0 ? 20 : 0))), 20, 100);
        }

        private int RecommendationChance()
        {
            //267D80 uses decoded index as an Area even for a City/Gate position.
            //Do not replace this with the resolved admission target's Area.
            var area = Area(Position().Index);
            var force = AreaForce(area);
            int ruler = Ruler(force);
            int steward = Lookup(FreePersonNeed(current.ReadAssignmentArea36(FreePersonNeed(area.PhysicalSlot, "chance_Area_pointer")), "chance_Area36"));
            int rulerTerm = Relation(person, ruler, true) ? 30 : Affinity(person, ruler) < 11 ? 20 : 0;
            int stewardTerm = !Valid(steward) ? 0 : Relation(person, steward, true) ? 15
                : Relation(person, steward, false) ? 1 : Math.Min(11, Ability(steward, 4) / 10 + 1);
            int maximum = 0;
            for (int dimension = 0; dimension < 5; dimension++) maximum = Math.Max(maximum, Ability(person, dimension));
            maximum = Math.Clamp(maximum, 0, 100);
            int cityCount = FreePersonNeed(PersonArmyCaptureSource.ReadOwnedCityCount(current, force), "chance_Force_Cities");
            int people = ForceId(force) == 0 ? 0 : Registry(RegistryKind.Person).Count(other =>
                ForceId(current.ReadPersonForce(other) ?? throw FreePersonMissing("chance_roster_Force")) == ForceId(force) &&
                FreePersonNeed(current.ReadPersonStatus(other), "chance_roster_status") is >= 1 and <= 4);
            int need = people / Math.Max(1, cityCount) >= Parameter("people-per-city-threshold", 6) ? Math.Min(10, maximum / 10) : 10;
            int ambition = FreePersonNeed(RulerCaptureReleaseInputs.ReadCurrentWord(latest, person, 0x17C), "chance_Person17C");
            return Math.Clamp(ambition + need + stewardTerm + rulerTerm, 0, 70);
        }

        private bool Trial(int chance)
        {
            var date = (latest.NativeMapSeason ?? throw FreePersonMissing("World34_date")).ThirdDate;
            int salt = unchecked((short)date.Year + (sbyte)date.Month + (sbyte)date.Day);
            return TacticEffectSourceSamplerMath.Sample(100, 1, salt, Id(person), 0, 0, Sampler()) < chance;
        }
        private TacticSourceSamplerWorld Sampler() => sampler ??= package.ReadArmyDestructionSampler(latest, current)
            ?? throw FreePersonMissing("sampler_World450_45C");
        private bool Disappearing()
        {
            var linked = current.ReadPersonLinkedEntity(person) ?? throw FreePersonMissing("linked_command");
            if (!FreePersonNeed(current.ReadLinkedEntityNativeValidity(linked), "linked_command_validity")) return false;
            return FreePersonNeed(new PersonReturnGovernorQueries(latest, package._governorInputs, current)
                .ReadCurrentAssociatedVirtual98(linked), "linked_command_virtual98") == 16;
        }
        private int Ability(int physical, int dimension) => FreePersonNeed(new TacticRemakeCurrentAbilities(current,
            package._scenario, package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language))
            .ResolveDimension(latest, Id(physical), dimension,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(latest, force, effect)), "effective_ability");
        private int Affinity(int first, int second)
        {
            if (!Valid(first) || !Valid(second)) return 0;
            int distance = Math.Abs(FreePersonNeed(OwnedPersonRelationInputs.ReadCurrentAffinity(latest, first), "first_affinity") -
                FreePersonNeed(OwnedPersonRelationInputs.ReadCurrentAffinity(latest, second), "second_affinity"));
            return Math.Min(75, Math.Min(distance, 150 - distance));
        }
        private bool Relation(int first, int second, bool like)
        {
            if (!Valid(second)) return false;
            for (int index = 0; index < 8; index++)
                if (FreePersonNeed(like ? OwnedPersonRelationInputs.ReadCurrentLikeIdentity(latest, first, index)
                    : OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(latest, first, index), "relationship_slot") == Id(second)) return true;
            return false;
        }
        private int Resentment(int physical)
        {
            var value = current.ReadOfficerAftermath(physical) ?? throw FreePersonMissing("resentment_owner");
            return value.Resentment?.TargetOfficerId ?? value.SourceKnowledge?.UnmappedResentmentTarget
                ?? throw FreePersonMissing("resentment_162");
        }
        private bool Valid(int physical) => FreePersonNeed(current.ReadPersonNativeValidity(physical), "Person_validity");
        private int Id(int physical) => FreePersonNeed(current.ReadPersonIdentity(physical), "Person_ID");
        private int Lookup(int id) => (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw FreePersonMissing("Person_lookup");
        private int Ruler(ForceReference force) => Lookup(FreePersonNeed(current.ReadForceRecord(ForceId(force))?.RawRuler10, "Force_ruler"));
        private int ForceId(ForceReference force) => FreePersonNeed(current.ReadForcePhysicalSlot(force), "Force_pointer");
        private ForceReference Force(int id) => current.ReadForceTableReference(id < 52 ? id : 0) ?? throw FreePersonMissing("Force_table");
        private ForceReference AreaForce(AreaReference area) => Force(FreePersonNeed(current.ReadAreaCurrentForceSlot(area), "Area_Hex_Force"));
        private BaseReference City(int id) => current.ReadCityTableReference(id < 52 ? id : 0) ?? throw FreePersonMissing("City_table");
        private BaseReference Gate(int id) => current.ReadGateTableReference(id < 11 ? id : 0) ?? throw FreePersonMissing("Gate_table");
        private AreaReference Area(int id) => current.ReadAreaTableReference(id < 501 ? id : 0) ?? throw FreePersonMissing("Area_table");
        private NativeLocation Position() => PersonPositionRepair.DecodeSecondLocation(
            FreePersonNeed(current.ReadPersonSecondLocation(person), "Person_E4"));
        private AreaReference? PositionArea()
        {
            var p = Position();
            return p.Kind switch
            {
                10 => Area(p.Index),
                5 => PersonFreedomHomeSource.FindBaseArea229FD0(current, City(p.Index)),
                6 => PersonFreedomHomeSource.FindBaseArea229FD0(current, Gate(p.Index)),
                2 => PersonFreedomHomeSource.FindBaseArea229FD0(current, BaseLookup.Find(p.Index, current)),
                _ => null
            };
        }
        private BaseReference CurrentCity()
        {
            var p = Position();
            if (p.Kind == 5) return City(p.Index);
            if (p.Kind == 6) return CurrentGateMapQueries.FindCity(Gate(p.Index), current);
            if (p.Kind == 10) return City(FreePersonNeed(current.ReadAreaCitySlot(Area(p.Index)), "old_Area35"));
            if (p.Kind == 2) throw FreePersonMissing("movement_current_Army_City_2A9DC0");
            return City(0);
        }
        private int Province(BaseReference city) => FreePersonNeed(package._growthQueries.ReadCityState(latest,
            FreePersonNeed(city.PhysicalSlot, "City_pointer")), "City_province_CE");
        private int Region(int province) => FreePersonNeed(BaseCaptureRewardInputs.ReadStateRegion(latest, province < 23 ? province : 0), "province_region_2E");
        private int Period() => ReadCommanderSelectionPeriod(latest);
        private int Parameter(string name, int original)
        {
            string key = "simulation.free-person." + name;
            var bank = latest.NativeArmySimulation?.Parameters ?? throw FreePersonMissing("parameter_owner");
            if (bank.Float32Values.ContainsKey(key)) throw FreePersonMissing("parameter_type_" + name);
            if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
            //Matched TC file-backed .data i32s, extracted offline: movement
            //start18, recommendation start18, people/city threshold6. This is
            //remake rule data, not a runtime original executable dependency.
            if (bank.Language != "TC") throw FreePersonMissing("parameter_locale_" + name);
            return original;
        }
        private IEnumerable<int> Registry(RegistryKind kind)
        {
            foreach (var value in current.ReadRegistryReferences(kind) ?? throw FreePersonMissing("registry_" + kind))
            {
                if (value is null || value.Kind != kind) throw FreePersonMissing("registry_payload_" + kind);
                yield return FreePersonNeed(value.PhysicalSlot, "registry_pointer");
            }
        }
        private IEnumerable<int> CityAreas(BaseReference city)
        {
            int physical = FreePersonNeed(city.PhysicalSlot, "iterator_City");
            if (physical is < 1 or > 51) yield break;
            foreach (int area in Registry(RegistryKind.Area))
                if (FreePersonNeed(current.ReadAreaCitySlot(new(area)), "iterator_Area35") == physical) yield return area;
        }
        private bool Neighbor(BaseReference first, BaseReference second)
        {
            var list = Temporary();
            try
            {
                (neighborhoods ??= new(package._scenario)).FillCities22F430(current, first, list);
                return Values(list).Any(value => value.Payload.PhysicalSlot == second.PhysicalSlot);
            }
            finally { Release(list); }
        }
        private HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw FreePersonMissing("temporary_descriptor");
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private void Add(HexListHandle list, RegistryKind kind, int physical)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw FreePersonMissing("temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(kind, physical));
        }
        private List<PersonFreedomHomeSource.Entry> Values(HexListHandle list)
        {
            var result = new List<PersonFreedomHomeSource.Entry>();
            for (var node = current.ReadHexListHead(list) ?? throw FreePersonMissing("temporary_head"); node.Identity.HasValue;
                node = current.ReadHexListNodeNext(node) ?? throw FreePersonMissing("temporary_next"))
                result.Add(new(node, current.ReadRegistryNodePayload(new(node.Identity)) ?? throw FreePersonMissing("temporary_payload")));
            return result;
        }
        private void App(int bound)
        {
            if (bound < 2) return;
            var saved = latest.CampaignAppRandom32 ?? throw FreePersonMissing("movement_App32");
            var random = new SharedSourceRandom32(saved.State);
            _ = random.Next(bound);
            latest = latest with { CampaignAppRandom32 = new(random.ReadCurrentState()) };
        }
    }
}
