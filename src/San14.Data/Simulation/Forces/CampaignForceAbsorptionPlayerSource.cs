using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2D4480 is the anti-player consolidation action. All temporary force and
    //neighbor lists borrow the enclosing Turn's current and primary pool.
    internal GameState ApplyNormalTurnPlayerForceAbsorptionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference player)
        => new PlayerForceAbsorptionSource(this, state, current, pool, player).Run();

    //The six doctrine reserves already have a saved owner shared with research
    //and personnel credit. These are precisely Force1C5..1CA, not new AI data.
    internal GameState ApplyNormalTurnForceDevelopmentMomentumCurrent(GameState state,
        SourceWorldContext current, ForceReference force, int amount)
    {
        int id = PlayerAbsorptionNeed(current.ReadForcePhysicalSlot(force), "reserve_Force");
        var owner = state.ForceCreditReserves ?? throw PlayerAbsorptionMissing("reserve_owner");
        if (!owner.PointsByForce.TryGetValue(id, out var values) || values.IsDefault || values.Length != 6)
            throw PlayerAbsorptionMissing("six_doctrine_reserves");
        return state with { ForceCreditReserves = owner with { PointsByForce = owner.PointsByForce.SetItem(id,
            values.Select(value => (byte)Math.Clamp(unchecked(value + amount), 0, 99)).ToImmutableArray()) } };
    }

    private sealed class PlayerForceAbsorptionSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool, ForceReference player)
    {
        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("shared_current_pool");
            if (package.Seed.Language != "TC") throw Missing("locale_rules");
            int settings = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "difficulty_settings");
            int difficulty = Need(BattleRemakeCurrentSettings.ReadInt32(state, settings == 0 ? 0x134 : 0x28), "difficulty");
            var season = state.NativeMapSeason ?? throw Missing("calendar");
            int elapsed = unchecked((state.CampaignEventWorld ?? throw Missing("elapsed_owner")).ElapsedPeriodOffset +
                (int)season.AdvancedPeriods);
            int minimum = difficulty switch { 0 => 108, 1 => 90, 2 => 72, 3 => 54, _ => 36 };
            if (difficulty < 0 || elapsed < minimum) return state;
            int chance = difficulty switch { 0 or 1 => 5, 2 => 6, 3 => 8, 4 => 10, _ => 5 };
            //3FCBE0 consumes no App draw. It adds World454 and the immutable
            //TC1A48B70 value1, independently of World450/45C samplers.
            uint bits = SourceRandom32Mix.Advance(unchecked((uint)(Ordinal(player) + elapsed + 1)));
            if ((bits & 0x7FFF_FFFFu) % 100 >= chance) return state;
            uint playerTroops = Troops(player);
            //1A4ABC4=500000 and file-backed1A4B404=-300000.
            if (playerTroops < 200000) return state;

            var ranking = Temporary();
            foreach (var force in Forces()) if (Ordinal(force) is not (>= 46 and <= 51)) Append(ranking, force);
            Sort(ranking, (a, b) => Troops(a) > Troops(b));
            var ranked = Values(ranking);
            if (ranked.Count < 2) { Release(ranking); return state; }
            uint secondTroops = Troops(ranked[1]);
            if (Troops(player) <= unchecked(200u * secondTroops) / 100u) { Release(ranking); return state; }
            Release(ranking);

            var candidates = Temporary();
            foreach (var force in Forces())
            {
                //The existing coalition head anywhere in the chain cancels
                //the action, including when encountered after added entries.
                if ((Flags(force) & 1) != 0) { Release(candidates); return state; }
                if (force == player || Ordinal(force) is >= 46 and <= 51 || !Hostile(force, player)) continue;
                if (PersonListHit(Ruler(force), Ruler(player), like: true) || Relation(force, player) >= 70) continue;
                Append(candidates, force);
                if (Count(candidates) >= 15) break;
            }
            var choices = Values(candidates);
            if (choices.Count == 0) { Release(candidates); return state; }
            var members = Temporary();
            ForceReference? head = null;
            foreach (var candidate in choices)
            {
                int friends = 0;
                foreach (var other in choices)
                {
                    if (candidate == other || PersonListHit(Ruler(candidate), Ruler(other), false) ||
                        PersonListHit(Ruler(other), Ruler(candidate), false)) continue;
                    if (Relation(candidate, other) >= 1) friends++;
                }
                if (friends > 1 && (head is null || Stronger(candidate, head))) head = candidate;
            }
            if (head is null) { Release(members); Release(candidates); return state; }
            Append(members, head);
            foreach (var other in choices)
                if (other != head && ValidPerson(Ruler(other)) &&
                    !PersonListHit(Ruler(other), Ruler(head), false) && !PersonListHit(Ruler(head), Ruler(other), false) &&
                    Relation(head, other) >= 1) Append(members, other);
            var absorbed = Values(members);
            SetCoalitionFlag(head);
            bool visible = IsPlayer(player);
            string? membershipText = null;
            if (visible)
            {
                state = package.AppendNormalTurnCoalitionCameraCurrent(state, HomeHex(head), true);
                Sound(7);
                Speaker(RulerIdentity(head), Format(0x5EB1, new(Person0: RulerIdentity(head), Person1: 5012,
                        Person2: RulerIdentity(player))));
                membershipText = Format(absorbed.Count > 4 ? 0x5EB5 : absorbed.Count > 3 ? 0x5EB4 : 0x5EB3,
                    MemberOperands(absorbed, includePlayer: true));
                string report = Format(absorbed.Count > 4 ? 0x53B1 : absorbed.Count > 3 ? 0x53B0 : 0x53AF,
                    MemberOperands(absorbed, includePlayer: false));
                History(head, report, null);
                foreach (var member in absorbed.Skip(1))
                    History(head, string.Empty, Format(0x5EB0, new(Person0: RulerIdentity(head),
                        Force0: Ordinal(head), Force1: Ordinal(member), Integers: [absorbed.Count - 1])));
            }
            foreach (var member in absorbed) if (member != head)
                state = package.ApplyNormalTurnForceAbsorptionCurrent(state, current, pool, member, head);

            //32C0F0/33B9E0 use the source's 360-day calendar and clear hour.
            int ordinal = unchecked(season.ThirdDate.Year * 360 + (season.ThirdDate.Month - 1) * 30 +
                season.ThirdDate.Day - 1 + 90);
            int year = ordinal / 360, rest = Math.Max(ordinal - year * 360, 0);
            var native = state.NativeArmySimulation ?? throw Missing("world_owner");
            state = state with { NativeArmySimulation = native with { World = native.World with
                { PlayerCoalitionCooldownDate = new(checked((short)year), checked((sbyte)(rest / 30 + 1)),
                    checked((sbyte)(rest % 30 + 1)), 0) } } };
            if (visible)
            {
                state = package.AppendNormalTurnCoalitionEventCurrent(state, true);
                Sound(129);
                Narration(Format(0x5EB2, new(Force0: Ordinal(head), Force1: Ordinal(player))));
                Narration(membershipText ?? throw Missing("membership_text"));
                state = package.AppendNormalTurnCoalitionEventCurrent(state, false);
            }
            int beforeRelation = Relation(head, player);
            RelationChange(player, head, Math.Max(unchecked(beforeRelation - 100), 0) - beforeRelation);
            foreach (var special in Forces())
            {
                if (Ordinal(special) is not (>= 46 and <= 50) ||
                    Need(NativeForce(special).Flags194, "special194") == Ordinal(player)) continue;
                bool adjacent = false;
                foreach (var ownBase in Bases(special))
                {
                    var neighbors = Temporary();
                    (package._captiveFreedomHomes ??= new(package._scenario)).FillBases22F720(current, ownBase, neighbors);
                    var at = current.ReadRegistryHead(new(neighbors.Identity)) ?? throw Missing("neighbor_head");
                    while (at.Identity.HasValue && !adjacent)
                    {
                        var neighbor = current.ReadBaseRegistryNodePayload(at) ?? throw Missing("neighbor_payload");
                        adjacent = Need(current.ReadBaseForceSlot(neighbor), "neighbor_force") == Ordinal(player);
                        at = current.ReadRegistryNodeNext(at) ?? throw Missing("neighbor_next");
                    }
                    Release(neighbors);
                    if (adjacent) break;
                }
                if (!adjacent) continue;
                int original = Math.Min(Relation(special, player), 100);
                RelationChange(player, special, Math.Max(unchecked(original - 70), 0) - original);
                if (IsPlayer(player))
                {
                    Sound(6);
                    int level = package.ReadDiplomacyRelationCurrent(state, current, Ordinal(player), Ordinal(special)).Level;
                    Narration(Format(level == 0 ? 0x5EB7 : level == 1 ? 0x5EB8 : 0x5EB9,
                        new(Force0: Ordinal(head), Force1: Ordinal(special))));
                }
            }
            if (visible) state = package.AppendNormalTurnCoalitionCameraCurrent(state, HomeHex(head), false);
            foreach (var location in Bases(head)) Bonus(location);
            Experience(head, 200000);
            foreach (int person in Persons(head))
            {
                if (Foreign(person)) continue;
                WritePreference(person, 0x16A, 20); WritePreference(person, 0x17C, 20); WritePreference(person, 0x17E, 20);
            }
            int rank = Need(current.ReadMoraleWaveForceRank47(Ordinal(head)), "rank");
            if (rank is >= 3 and <= 9 or > 12)
                state = package.ApplyNormalTurnCoalitionKingPromotionCurrent(state, current, pool, head);
            RaiseLoyalty(head, 100);
            var district = ForcePrimaryDistrictSource.Resolve(current, head);
            int districtId = Need(district.PhysicalSlot, "head_district");
            int budget = ReadDiplomacyDistrictBudget(current, districtId);
            int forceRank = Need(current.ReadMoraleWaveForceRank47(Ordinal(head)), "district_rank");
            int cap = forceRank switch { 1 or 2 or 3 or 10 or 11 or 12 => 200000, 4 or 5 => 150000,
                6 or 7 => 100000, _ => 50000 };
            //File-backed TC1A49F84 is20000; this treasury is distinct from
            //the gold granted to each city above.
            int target = Math.Min(unchecked(budget + 20000), cap);
            state = SpendDiplomacyDistrictCurrent(state, current, district, budget - target);
            state = package.ApplyNormalTurnForceDevelopmentMomentumCurrent(state, current, head, 6);
            RaiseLoyalty(player, 100);
            Release(members); Release(candidates);
            return state;
        }

        private HistoryMessageOperands MemberOperands(IReadOnlyList<ForceReference> members, bool includePlayer)
        {
            int at(int index) => members.Count > index ? Ordinal(members[index]) : 0;
            return includePlayer ? new(Force0: at(0), Force1: Ordinal(player), Force2: at(1), Force3: at(2),
                Force4: at(3), Integers: [members.Count - 1]) : new(Person0: RulerIdentity(members[0]),
                Force0: at(0), Force1: at(1), Force2: at(2), Force3: at(3), Integers: [members.Count - 1]);
        }
        private void History(ForceReference force, string main, string? detail)
        {
            int district = Need(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "report_district");
            int hex = HomeHex(force);
            int day = (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("report_day450")).Day450;
            var effects = new List<CampaignEffectRequest>();
            state = package.WriteHistory(state, current, new(1, district, hex, day, main,
                detail ?? CampaignHistoryWriter.Supplemental(state), 5012, MarkBit0: 1), effects);
            Append(effects);
        }
        private void Bonus(BaseReference location)
        {
            package._baseCapacityInputs.BindCurrent(state, current);
            int link = Need(current.ReadBaseObjectLink(location), "bonus_location");
            var caps = BasePostLoad.ReadResourceCapacities(location, current);
            if (link < 52)
            {
                current.SetBaseResource(location, BaseResourceKind.Gold, Math.Clamp(unchecked(
                    Need(current.ReadBaseResource(location, BaseResourceKind.Gold), "bonus_gold") + 20000), 0, caps.Gold));
                current.SetBaseResource(location, BaseResourceKind.Supplies, Math.Clamp(unchecked(
                    Need(current.ReadBaseResource(location, BaseResourceKind.Supplies), "bonus_food") + 50000), 0, caps.Supplies));
            }
            current.SetBaseResource(location, BaseResourceKind.Troops, Math.Clamp(unchecked(
                Need(current.ReadBaseResource(location, BaseResourceKind.Troops), "bonus_troops") + 10000), 0, caps.Troops));
            state = PublishCurrentBaseCombatQuantities(state, current, location, true, false, false);
            if (link < 52)
            {
                var city = state.Cities.GetValueOrDefault(link) ?? throw Missing("bonus_city_owner");
                state = state with { Cities = new(state.Cities) { [link] = city with
                    { Gold = Need(current.ReadBaseResource(location, BaseResourceKind.Gold), "published_gold"),
                      Supplies = Need(current.ReadBaseResource(location, BaseResourceKind.Supplies), "published_food") } } };
            }
        }
        private void Experience(ForceReference force, int amount)
        {
            if (!Need(current.ReadForceNativeValidity(force), "experience_validity") || amount < 0) return;
            package._force164Inputs.BindCurrent(state, current);
            int before = Need(current.ReadForce164(force), "experience_current");
            var packet = new ArmyDestructionForce164Packet(current, force, requested: amount, before: before,
                after: Math.Clamp(unchecked(before + amount), 0, 9999999));
            ArmyDestructionForce164.ApplyContext(packet); state = ArmyDestructionForce164.PublishCurrent(state, packet);
        }
        private void RaiseLoyalty(ForceReference force, int minimum)
        {
            int ruler = Ruler(force);
            foreach (int person in Persons(force))
            {
                if (person == ruler || Foreign(person)) continue;
                byte before = Need(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, package._governorInputs), "loyalty");
                if (before >= minimum) continue;
                byte after = (byte)Math.Clamp(before + Math.Min(minimum - before, minimum), 0, 255);
                if (Need(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "loyalty_policy")) after = 255;
                current.SetPersonDestructionLoyaltyE8(person, after);
                state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, after);
            }
        }
        private void WritePreference(int person, int field, short value)
        {
            var simulation = state.NativeArmySimulation ?? throw Missing("preference_owner");
            if (RulerCaptureReleaseInputs.ReadCurrentWord(state, person, field) is null) throw Missing("preference_field");
            state = state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                { Int32Values = new(simulation.Parameters.Int32Values) { [RulerCaptureReleaseInputs.PersonWordKey(person, field)] = value } } } };
        }
        private bool Stronger(ForceReference a, ForceReference b)
        {
            //3082E0(mode1): registered City count, registered serving people,
            //City money, City food, then lower physical Force ordinal.
            var first = Strength(a); var second = Strength(b);
            if (first.Cities != second.Cities) return first.Cities > second.Cities;
            if (first.People != second.People) return first.People > second.People;
            if (first.Gold != second.Gold) return first.Gold > second.Gold;
            if (first.Food != second.Food) return first.Food > second.Food;
            return Ordinal(a) < Ordinal(b);
        }
        private (int Cities, int People, int Gold, int Food) Strength(ForceReference force)
        {
            int cities = 0, gold = 0, food = 0;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("City_registry"))
            {
                if (entry is not { Kind: RegistryKind.City, PhysicalSlot: int at }) throw Missing("City_payload");
                var city = new BaseReference(BaseKind.City, at);
                if (Need(current.ReadBaseForceSlot(city), "City_force") != Ordinal(force)) continue;
                cities++; gold = unchecked(gold + Need(current.ReadBaseResource(city, BaseResourceKind.Gold), "City_gold"));
                food = unchecked(food + Need(current.ReadBaseResource(city, BaseResourceKind.Supplies), "City_food"));
            }
            return (cities, Persons(force).Count(), gold, food);
        }
        private uint Troops(ForceReference force)
        { uint sum = 0; foreach (var at in Bases(force)) sum = unchecked(sum + (uint)Need(current.ReadBaseResource(at, BaseResourceKind.Troops), "troops")); return sum; }
        private IEnumerable<BaseReference> Bases(ForceReference force)
        {
            int ordinal = Ordinal(force);
            if (ordinal is < 1 or > 51) yield break;
            foreach (var entry in current.ReadRegisteredBases() ?? throw Missing("Base80_registry"))
            {
                var at = entry ?? throw Missing("Base80_payload");
                if (Need(current.ReadBaseForceSlot(at), "Base80_force") == ordinal) yield return at;
            }
        }
        private IEnumerable<int> Persons(ForceReference force)
        {
            int ordinal = Ordinal(force);
            if (ordinal is < 1 or > 51) yield break;
            foreach (var at in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person_registry"))
            {
                int person = Need(at, "Person_pointer");
                if (Need(current.ReadPersonOldForceSlot(person), "Person_oldForce") != ordinal) continue;
                if (Need(current.ReadPersonStatus(person), "Person_status") is >= 1 and <= 4) yield return person;
            }
        }
        private IEnumerable<ForceReference> Forces()
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("Force_registry"))
                yield return entry is { Kind: RegistryKind.Force, PhysicalSlot: int at } ? new(at) : throw Missing("Force_payload");
        }
        private NativeArmyForceState NativeForce(ForceReference force) =>
            state.NativeArmySimulation?.Forces.GetValueOrDefault(Need(force.PhysicalSlot, "native_Force_pointer")) ?? throw Missing("native_Force");
        private ushort Flags(ForceReference force) => Need(NativeForce(force).DiplomacyFlags178, "Force178");
        private void SetCoalitionFlag(ForceReference force)
        {
            var simulation = state.NativeArmySimulation ?? throw Missing("flags_owner");
            int at = Need(force.PhysicalSlot, "flags_pointer");
            if (!simulation.Parameters.Int32Values.ContainsKey(OwnedPersonRelationInputs.Force178Key(at)))
                throw Missing("relation_flags_owner");
            ushort flags = (ushort)(Flags(force) | 1);
            state = state with { NativeArmySimulation = simulation with
                { Forces = new(simulation.Forces) { [at] = NativeForce(force) with { DiplomacyFlags178 = flags } },
                  Parameters = simulation.Parameters with { Int32Values = new(simulation.Parameters.Int32Values)
                    { [OwnedPersonRelationInputs.Force178Key(at)] = flags & 255 } } } };
        }
        private bool Hostile(ForceReference a, ForceReference b)
        {
            int first = Ordinal(a), second = Ordinal(b);
            if (first == 0 || a == b) return false;
            if (second == 0 && first is >= 46 and <= 51) return false;
            byte flags = Need(state.NativeArmySimulation?.World.Flags165C, "World165C");
            if (flags != 0 && (Need(NativeForce(a).Flags12, "first12") & 2) != 0 &&
                (Need(NativeForce(b).Flags12, "second12") & 2) != 0) return false;
            if (first == 51 || second is 0 or 51) return true;
            if (Need(NativeForce(a).Flags194, "first194") == second || Need(NativeForce(b).Flags194, "second194") == first) return false;
            var relations = NativeForce(a).RelationsEA is { Length: 52 } values ? values : throw Missing("RelationsEA");
            return (relations[second] & 127) == 0;
        }
        private int Relation(ForceReference a, ForceReference b) => Ordinal(b) is int index && index < 52
            ? DRelations(state, Need(a.PhysicalSlot, "relation_pointer"))[index] : 0;
        private void RelationChange(ForceReference a, ForceReference b, int amount) => state =
            package.ApplyNormalTurnForceRelationCurrent(state, current, pool, a, b, amount);
        private int RulerIdentity(ForceReference force) => Ordinal(force) == 0 ? 5014 :
            Need(current.ReadForceRecord(Need(force.PhysicalSlot, "ruler_force"))?.RawRuler10, "ruler_ID");
        private int Ruler(ForceReference force) => Need((RulerIdentity(force) <= 6000 ? current.ReadPersonLookupReference(RulerIdentity(force)) :
            current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "ruler_pointer");
        private int Home(ForceReference force) => Need(current.ReadPersonFirstLocation(Ruler(force)), "ruler_home");
        private int HomeHex(ForceReference force)
        {
            //22D880 returns the representative object's raw hex, including
            //its48400 sentinel;22C960's clipped Hex0 is another getter.
            var home = BaseLookup.Find(Home(force), current);
            int raw = Need(current.ReadBaseObjectLink(home), "home_object_link");
            var obj = current.ReadObjectTableReference(raw < 3001 ? raw : 0) ?? throw Missing("home_object");
            return Need(current.ReadObjectNativeValidity(obj), "home_object_validity")
                ? Need(current.ReadObjectHexIndex(obj), "home_object_hex") : 48400;
        }
        private bool ValidPerson(int person) => Need(current.ReadPersonNativeValidity(person), "person_validity");
        private bool Foreign(int person) => FreshResearchForeignPerson(Need(current.ReadPersonIdentity(person), "person_ID"));
        private bool PersonListHit(int first, int second, bool like)
        {
            if (!ValidPerson(second)) return false;
            int identity = Need(current.ReadPersonIdentity(second), "other_person_ID");
            for (int i = 0; i < 8; i++)
                if (Need(like ? OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, first, i) :
                    OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, first, i), "person_relation_slot") == identity) return true;
            return false;
        }
        private int Ordinal(ForceReference force) => Need(current.ReadForcePhysicalSlot(force), "Force60");
        private bool IsPlayer(ForceReference force)
        {
            var selected = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("selected_player");
            return Ordinal(selected) != 0 && selected == force;
        }
        private HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw Missing("temporary");
        private void Append(HexListHandle list, ForceReference force)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
            current.WriteRegistryNodePayload(node, new(RegistryKind.Force, force.PhysicalSlot));
        }
        private int Count(HexListHandle list) => Need(current.ReadTemporaryHexListCount(list), "temporary_count");
        private List<ForceReference> Values(HexListHandle list)
        {
            var result = new List<ForceReference>();
            var at = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("temporary_head");
            while (at.Identity.HasValue)
            {
                var value = current.ReadRegistryNodePayload(at);
                result.Add(value is { Kind: RegistryKind.Force, PhysicalSlot: int physical } ? new(physical) : throw Missing("temporary_payload"));
                at = current.ReadRegistryNodeNext(at) ?? throw Missing("temporary_next");
            }
            return result;
        }
        private void Sort(HexListHandle list, Func<ForceReference, ForceReference, bool> before)
        {
            var order = new List<HexListNodeReference>();
            var node = current.ReadHexListHead(list) ?? throw Missing("sort_head");
            while (node.Identity.HasValue) { order.Add(node); node = current.ReadHexListNodeNext(node) ?? throw Missing("sort_next"); }
            ForceReference Value(HexListNodeReference at) => new(Need(current.ReadRegistryNodePayload(new(at.Identity))?.PhysicalSlot, "sort_payload"));
            for (int i = 1; i < order.Count; i++)
            { var item = order[i]; int at = i; while (at > 0 && before(Value(item), Value(order[at - 1]))) { order[at] = order[at - 1]; at--; } order[at] = item; }
            current.ReorderCurrentList(list, order);
        }
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private string Format(int root, HistoryMessageOperands operands)
        { var result = package.FormatNormalTurnReport(state, current, root, operands); state = result.State; return result.Text; }
        private void Sound(int id) => Append([new(null, null) { TalentCommandPresentation =
            new(TalentCommandPresentationOperation.SoundCue, id, null, null, null, null) }]);
        private void Speaker(int person, string text) => Append([new(null, null) { MonthlyMaintenance =
            new(MonthlyMaintenanceOperation.ConditionalSpeakerText, null, checked((ushort)person), text) }]);
        private void Narration(string text) => Append([new(null, null) { MonthlyMaintenance =
            new(MonthlyMaintenanceOperation.NarrationText, null, null, text) }]);
        private void Append(IReadOnlyList<CampaignEffectRequest> effects)
        {
            if (effects.Count == 0) return;
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("pending_owner")).Append(
                CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("revision"), effects) };
        }
        private static T Need<T>(T? value, string field) where T : struct => PlayerAbsorptionNeed(value, field);
        private static InvalidDataException Missing(string field) => PlayerAbsorptionMissing(field);
    }
    private static T PlayerAbsorptionNeed<T>(T? value, string field) where T : struct => value ?? throw PlayerAbsorptionMissing(field);
    private static InvalidDataException PlayerAbsorptionMissing(string field) => new("normal-turn.player-force-absorption." + field);
}
