using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2C1C20: ordinary non-player consolidation. The Turn owns the current and
    //temporary-node pool throughout selection, transfer and the reward tail.
    internal GameState ApplyNormalTurnNpcForceAbsorptionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference player)
        => new NpcForceAbsorptionSource(this, state, current, pool, player).Run();

    private sealed class NpcForceAbsorptionSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool, ForceReference player)
    {
        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("shared_current_pool");
            if (package.Seed.Language != "TC") throw Missing("locale_rules");
            int difficulty = Difficulty();
            var season = state.NativeMapSeason ?? throw Missing("calendar");
            int elapsed = unchecked((state.CampaignEventWorld ?? throw Missing("elapsed_owner")).ElapsedPeriodOffset +
                (int)season.AdvancedPeriods);
            //TC immutable data1A4B288/294/2A0/2AC/3B4 and2C4/2D0/2DC/2E8.
            int minimum = difficulty switch { 0 => 90, 1 => 72, 2 => 54, 3 => 36, _ => 36 };
            if (difficulty < 0 || elapsed < minimum) return state;
            difficulty = Difficulty();
            int chance = difficulty switch { 0 or 1 => 5, 2 => 6, 3 => 8, 4 => 10, _ => 5 };
            //3FCBE0 is a pure trial: Force60+World454+immutable1A48B70(1).
            //It neither reads World450/45C nor advances the App generator.
            uint bits = SourceRandom32Mix.Advance(unchecked((uint)(Ordinal(player) + elapsed + 1)));
            if ((bits & 0x7FFF_FFFFu) % 100 >= chance) return state;

            var candidates = current.CreateTemporaryHexList() ?? throw Missing("temporary");
            foreach (var force in Forces())
            {
                //Any coalition head in registry order cancels this action,
                //even if earlier nodes have already been admitted.
                if ((Need(NativeForce(force).DiplomacyFlags178, "Force178") & 1) != 0)
                { Release(candidates); return state; }
                if (force == player || Ordinal(force) is >= 46 and <= 51 || !Hostile(force, player)) continue;
                if (PersonListHit(Ruler(force), Ruler(player), like: true)) continue;
                if (Relation(force, player) >= 70) continue;
                uint troops = Troops(force), playerTroops = Troops(player);
                if (unchecked(200u * troops) / 100u >= playerTroops) continue;
                var node = current.AllocateRegistryNode(new(candidates.Identity)) ?? throw Missing("temporary_node");
                current.WriteRegistryNodePayload(node, new(RegistryKind.Force, force.PhysicalSlot));
            }
            if (Need(current.ReadTemporaryHexListCount(candidates), "temporary_count") <= 1)
            { Release(candidates); return state; }

            Sort(candidates);
            var choices = Values(candidates);
            ForceReference? survivor = null, absorbed = null;
            foreach (var first in choices)
            {
                foreach (var second in choices)
                {
                    if (first == second || Relation(first, second) < 1) continue;
                    if (PersonListHit(Ruler(first), Ruler(second), false) ||
                        PersonListHit(Ruler(second), Ruler(first), false)) continue;
                    if (!(package._captiveFreedomHomes ??= new(package._scenario))
                        .AreForceCitiesAdjacent(current, Ordinal(first), Ordinal(second))) continue;
                    survivor = first; absorbed = second; break;
                }
                if (survivor is not null) break;
            }
            if (survivor is null || absorbed is null) { Release(candidates); return state; }

            bool visible = IsPlayer(player);
            string? narration = null;
            int cameraHex = 0;
            if (visible)
            {
                //00E940(4) is the zoom anchor getter, not a random draw.
                cameraHex = HomeHex(survivor);
                state = package.AppendNormalTurnCoalitionCameraCurrent(state, cameraHex, true);
                Sound(6);
                string text = Format(0x5EAE, new(Person0: 5012, Person1: RulerIdentity(player),
                    Person2: RulerIdentity(survivor), Person3: RulerIdentity(absorbed)));
                Monthly(new(MonthlyMaintenanceOperation.ConditionalSpeakerText, null, 5012, text));
                narration = Format(0x5EAF, new(Force0: Ordinal(player), Force1: Ordinal(survivor),
                    Force2: Ordinal(absorbed), Force3: 0, Force4: 0));
                var operands = new HistoryMessageOperands(Person0: RulerIdentity(survivor),
                    Force0: Ordinal(survivor), Force1: Ordinal(absorbed), Force2: 0, Force3: 0, Force4: 0);
                string main = Format(0x5EB0, operands);
                string detail = Format(0x53AE, operands);
                int district = Need(ForcePrimaryDistrictSource.Resolve(current, survivor).PhysicalSlot, "report_District");
                int hex = HomeHex(survivor);
                int day = (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("report_day450")).Day450;
                var history = new List<CampaignEffectRequest>();
                state = package.WriteHistory(state, current, new(1, district, hex, day,
                    main, detail, 5012, MarkBit0: 1, MarkBit4: 1), history);
                Append(history);
            }
            state = package.ApplyNormalTurnForceAbsorptionCurrent(state, current, pool, absorbed, survivor);
            if (visible)
            {
                state = package.AppendNormalTurnCoalitionEventCurrent(state, true);
                Sound(129);
                Monthly(new(MonthlyMaintenanceOperation.NarrationText, null, null, narration));
                state = package.AppendNormalTurnCoalitionEventCurrent(state, false);
                state = package.AppendNormalTurnCoalitionCameraCurrent(state, cameraHex, false);
            }

            //Source reads player->survivor, then applies the adjustment to
            //survivor->player; the two relation operands are intentionally distinct.
            int original = Relation(player, survivor);
            state = package.ApplyNormalTurnForceRelationCurrent(state, current, pool, survivor, player,
                unchecked(Math.Max(unchecked(original - 100), 0) - original));
            foreach (var location in Bases(survivor)) Bonus(location);
            Experience(survivor, 1000);
            RaiseLoyalty(survivor, 100);
            //1A3E60 only refreshes independent city view statistics284/288.
            //Those views are not simulation state and no presentation is ACKed.
            var treasury = ForcePrimaryDistrictSource.Resolve(current, survivor);
            int budget = ReadDiplomacyDistrictBudget(current, Need(treasury.PhysicalSlot, "reward_District"));
            int rank = Need(current.ReadMoraleWaveForceRank47(Ordinal(survivor)), "reward_rank47");
            int forceCap = rank switch { 1 or 2 or 3 or 10 or 11 or 12 => 200000,
                4 or 5 => 150000, 6 or 7 => 100000, _ => 50000 };
            int requested = Math.Min(unchecked(budget + 10000), forceCap);
            //The shared setter repeats231310 using the actual District's force.
            state = SpendDiplomacyDistrictCurrent(state, current, treasury, unchecked(budget - requested));
            state = package.ApplyNormalTurnForceDevelopmentMomentumCurrent(state, current, survivor, 3);
            Release(candidates);
            return state;
        }

        private void Bonus(BaseReference location)
        {
            package._baseCapacityInputs.BindCurrent(state, current);
            int link = Need(current.ReadBaseObjectLink(location), "reward_Base4E");
            if (link < 52)
            {
                int cap = BasePostLoad.ReadGoldCapacity(location, current);
                int gold = Bound(unchecked(Need(current.ReadBaseResource(location, BaseResourceKind.Gold), "reward_gold") + 5000), cap);
                //The virtual setter bounds again against its current capacity.
                current.SetBaseResource(location, BaseResourceKind.Gold, Bound(gold, BasePostLoad.ReadGoldCapacity(location, current)));
                cap = BasePostLoad.ReadSuppliesCapacity(location, current);
                int food = Bound(unchecked(Need(current.ReadBaseResource(location, BaseResourceKind.Supplies), "reward_food") + 10000), cap);
                current.SetBaseResource(location, BaseResourceKind.Supplies, Bound(food, BasePostLoad.ReadSuppliesCapacity(location, current)));
            }
            int before = Need(current.ReadBaseResource(location, BaseResourceKind.Troops), "reward_troops");
            int troops = Bound(unchecked(before + 10000), BasePostLoad.ReadTroopCapacity(location, current));
            current.SetBaseResource(location, BaseResourceKind.Troops, Bound(troops, BasePostLoad.ReadTroopCapacity(location, current)));
            state = PublishCurrentBaseCombatQuantities(state, current, location, true, false, false);
            if (link < 52)
            {
                var city = state.Cities.GetValueOrDefault(link) ?? throw Missing("reward_City_owner");
                state = state with { Cities = new(state.Cities) { [link] = city with
                { Gold = Need(current.ReadBaseResource(location, BaseResourceKind.Gold), "published_gold"),
                  Supplies = Need(current.ReadBaseResource(location, BaseResourceKind.Supplies), "published_food") } } };
            }
        }
        private static int Bound(int value, int maximum) => value < 0 ? 0 : value > maximum ? maximum : value;
        private void Experience(ForceReference force, int amount)
        {
            if (!Need(current.ReadForceNativeValidity(force), "experience_validity") || amount < 0) return;
            package._force164Inputs.BindCurrent(state, current);
            int before = Need(current.ReadForce164(force), "experience_current");
            var packet = new ArmyDestructionForce164Packet(current, force, requested: amount, before: before,
                after: Bound(unchecked(before + amount), 9999999));
            ArmyDestructionForce164.ApplyContext(packet);
            state = ArmyDestructionForce164.PublishCurrent(state, packet);
        }
        private void RaiseLoyalty(ForceReference force, int minimum)
        {
            foreach (int person in Persons(force))
            {
                if (person == Ruler(force) || FreshResearchForeignPerson(Need(current.ReadPersonIdentity(person), "loyalty_Person_ID"))) continue;
                byte before = Need(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, package._governorInputs), "loyalty_E8");
                if (before >= minimum) continue;
                byte addition = unchecked((byte)Math.Min(minimum, unchecked(minimum - before)));
                byte after = (byte)Math.Min(before + addition, 255);
                if (Need(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "loyalty_policy")) after = 255;
                current.SetPersonDestructionLoyaltyE8(person, after);
                state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, after);
            }
        }

        //2502B0's insertion compares each predecessor once.3082E0(mode0)
        //first reads signed ruler17C, then Cities, serving people, gold, food, ID.
        private void Sort(HexListHandle list)
        {
            var node = current.ReadHexListHead(list) ?? throw Missing("sort_head");
            if (!node.Identity.HasValue) return;
            node = Next(node);
            while (node.Identity.HasValue)
            {
                var next = Next(node);
                var previous = current.ReadHexListNodePrevious(node) ?? throw Missing("sort_previous");
                if (Before(Value(node), Value(previous)))
                {
                    var insertion = previous;
                    var earlier = current.ReadHexListNodePrevious(previous) ?? throw Missing("sort_previous");
                    while (earlier.Identity.HasValue && Before(Value(node), Value(earlier)))
                    { insertion = earlier; earlier = current.ReadHexListNodePrevious(earlier) ?? throw Missing("sort_previous"); }
                    var order = new List<HexListNodeReference>();
                    for (var at = current.ReadHexListHead(list) ?? throw Missing("sort_head"); at.Identity.HasValue; at = Next(at)) order.Add(at);
                    order.RemoveAll(at => at.Identity == node.Identity);
                    int position = order.FindIndex(at => at.Identity == insertion.Identity);
                    if (position < 0) throw Missing("sort_insertion");
                    order.Insert(position, node); current.ReorderCurrentList(list, order);
                }
                node = next;
            }
        }
        private bool Before(ForceReference first, ForceReference second)
        {
            short a = Need(RulerCaptureReleaseInputs.ReadCurrentWord(state, Ruler(first), 0x17C), "ruler_17C");
            short b = Need(RulerCaptureReleaseInputs.ReadCurrentWord(state, Ruler(second), 0x17C), "ruler_17C");
            if (a != b) return a > b;
            int firstCities = 0, secondCities = 0, firstGold = 0, secondGold = 0, firstFood = 0, secondFood = 0;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("sort_City_registry"))
            {
                var city = entry is { Kind: RegistryKind.City, PhysicalSlot: int at }
                    ? new BaseReference(BaseKind.City, at) : throw Missing("sort_City_payload");
                int owner = Need(current.ReadBaseForceSlot(city), "sort_City_force");
                if (owner == Ordinal(first))
                {
                    firstCities++; firstGold = unchecked(firstGold + Need(current.ReadBaseResource(city, BaseResourceKind.Gold), "sort_gold"));
                    firstFood = unchecked(firstFood + Need(current.ReadBaseResource(city, BaseResourceKind.Supplies), "sort_food"));
                }
                else if (owner == Ordinal(second))
                {
                    secondCities++; secondGold = unchecked(secondGold + Need(current.ReadBaseResource(city, BaseResourceKind.Gold), "sort_gold"));
                    secondFood = unchecked(secondFood + Need(current.ReadBaseResource(city, BaseResourceKind.Supplies), "sort_food"));
                }
            }
            if (firstCities != secondCities) return firstCities > secondCities;
            int firstPeople = Persons(first).Count(), secondPeople = Persons(second).Count();
            if (firstPeople != secondPeople) return firstPeople > secondPeople;
            if (firstGold != secondGold) return firstGold > secondGold;
            if (firstFood != secondFood) return firstFood > secondFood;
            return Ordinal(first) < Ordinal(second);
        }
        private uint Troops(ForceReference force)
        { uint total = 0; foreach (var at in Bases(force)) total = unchecked(total + (uint)Need(current.ReadBaseResource(at, BaseResourceKind.Troops), "Base_troops")); return total; }
        private IEnumerable<BaseReference> Bases(ForceReference force)
        {
            int id = Ordinal(force);
            if (id is < 1 or > 51) yield break;
            foreach (var entry in current.ReadRegisteredBases() ?? throw Missing("Base80_registry"))
            {
                var at = entry ?? throw Missing("Base80_payload");
                if (Need(current.ReadBaseForceSlot(at), "Base80_force") == id) yield return at;
            }
        }
        private IEnumerable<int> Persons(ForceReference force)
        {
            int id = Ordinal(force);
            if (id is < 1 or > 51) yield break;
            foreach (var at in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person_registry"))
            {
                int person = Need(at, "Person_pointer");
                if (Need(current.ReadPersonOldForceSlot(person), "Person_Force") != id) continue;
                if (Need(current.ReadPersonStatus(person), "Person_status") is >= 1 and <= 4) yield return person;
            }
        }
        private IEnumerable<ForceReference> Forces()
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("Force_registry"))
                yield return entry is { Kind: RegistryKind.Force, PhysicalSlot: int at } ? new(at) : throw Missing("Force_payload");
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
        private bool PersonListHit(int first, int second, bool like)
        {
            if (!Need(current.ReadPersonNativeValidity(second), "related_Person_validity")) return false;
            int id = Need(current.ReadPersonIdentity(second), "related_Person_ID");
            for (int i = 0; i < 8; i++)
                if (Need(like ? OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, first, i) :
                    OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, first, i), "Person_relation_slot") == id) return true;
            return false;
        }
        private int Relation(ForceReference first, ForceReference second)
        {
            int id = Ordinal(second);
            return id < 52 ? DRelations(state, Need(first.PhysicalSlot, "relation_pointer"))[id] : 0;
        }
        private NativeArmyForceState NativeForce(ForceReference force) => state.NativeArmySimulation?.Forces
            .GetValueOrDefault(Need(force.PhysicalSlot, "Force_pointer")) ?? throw Missing("Force_owner");
        private int Ordinal(ForceReference force) => Need(current.ReadForcePhysicalSlot(force), "Force60");
        private int RulerIdentity(ForceReference force) => Ordinal(force) == 0 ? 5014 :
            Need(current.ReadForceRecord(Need(force.PhysicalSlot, "ruler_Force"))?.RawRuler10, "ruler_ID");
        private int Ruler(ForceReference force)
        {
            int id = RulerIdentity(force);
            return Need((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "ruler_pointer");
        }
        private int HomeHex(ForceReference force) => Need(CurrentGateMapQueries.FindHex(
            BaseLookup.Find(Need(current.ReadPersonFirstLocation(Ruler(force)), "ruler_home"), current), current).PhysicalSlot, "ruler_home_Hex");
        private bool IsPlayer(ForceReference force)
        {
            var selected = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("selected_player");
            return Ordinal(selected) != 0 && selected == force;
        }
        private int Difficulty()
        {
            int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
            return Need(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28), "difficulty");
        }
        private ForceReference Value(HexListNodeReference node)
        {
            var value = current.ReadRegistryNodePayload(new(node.Identity));
            return value is { Kind: RegistryKind.Force, PhysicalSlot: int at } ? new(at) : throw Missing("temporary_payload");
        }
        private List<ForceReference> Values(HexListHandle list)
        {
            var result = new List<ForceReference>();
            for (var at = current.ReadHexListHead(list) ?? throw Missing("temporary_head"); at.Identity.HasValue; at = Next(at)) result.Add(Value(at));
            return result;
        }
        private HexListNodeReference Next(HexListNodeReference at) => current.ReadHexListNodeNext(at) ?? throw Missing("temporary_next");
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private string Format(int root, HistoryMessageOperands operands)
        { var result = package.FormatNormalTurnReport(state, current, root, operands); state = result.State; return result.Text; }
        private void Sound(int id) => Append([new(null, null) { TalentCommandPresentation =
            new(TalentCommandPresentationOperation.SoundCue, id, null, null, null, null) }]);
        private void Monthly(MonthlyMaintenanceRequest request) => Append([new(null, null) { MonthlyMaintenance = request }]);
        private void Append(IReadOnlyList<CampaignEffectRequest> effects)
        {
            if (effects.Count == 0) return;
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("pending_owner")).Append(
                CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("revision"), effects) };
        }
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal-turn.npc-force-absorption." + field);
    }
}
