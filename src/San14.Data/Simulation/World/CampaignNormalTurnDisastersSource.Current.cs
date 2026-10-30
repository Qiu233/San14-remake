using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnDisastersSource
    {
        private TacticSourceSamplerWorld? sampler;
        private TacticRemakeCurrentAbilities? abilities;
        private NativeMapSeasonDate Date => state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");
        private int DateSalt => unchecked(Date.Year + Date.Month);
        private bool SeasonBoundary => Date.Month is 1 or 4 or 7 or 10;
        private static byte Active(int mode) => mode == 0 ? (byte)1 : (byte)4;
        //The immutable two-u32 table contains bit indices1,3, not masks.
        private static byte Pending(int mode) => mode == 0 ? (byte)2 : (byte)8;

        private int RecoveryProbability(int area, int mode)
        {
            var development = Owner(area).Development ?? throw Missing("Area_development");
            int levels = CeAreaIncomeMath.Level(Word(development.Commerce, "commerce3C")) +
                CeAreaIncomeMath.Level(Word(development.Agriculture, "agriculture3E")) +
                CeAreaIncomeMath.Level(Word(development.Barracks, "barracks40"));
            int assigned = AssignedAbility(area);
            int security = Math.Clamp(Div(Security(area), P(mode == 0 ? "recovery-security-divisor" : "second-recovery-security-divisor")), 1, 25);
            return Math.Clamp(unchecked(assigned / 2 + security + levels), 21, 75);
        }
        private int SpreadProbability(int area, int mode)
        {
            if (mode != 0) return unchecked(Sample(P("second-spread-random-bound"), area, mode) + P("second-spread-offset"));
            int population = Math.Clamp(Div(Population(area), P("spread-population-divisor")), 1, 15);
            int value = unchecked(population - AssignedAbility(area) / 2 + 50);
            int minimum = P("spread-minimum");
            return value < minimum ? minimum : value > P("spread-maximum") ? P("spread-maximum") : value;
        }
        private int AssignedAbility(int area)
        {
            int person = Person(N(current.ReadAssignmentArea36(area), "Area36"));
            if (!N(current.ReadPersonNativeValidity(person), "assigned_Person_validity")) return 40;
            int politics = Ability(person, 3), intelligence = Ability(person, 2);
            int effect = N(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x7E), "assigned_trait126");
            int? count = effect > 0 ? null : (current.ReadAssignmentPersonAreaVector(person)
                ?? throw Missing("Person_assignment_vector")).ActiveCount1E8;
            int factor = CeEconomyRuntimeContext.ReadPersonTaskPercent(false, effect, count, null)
                ?? throw Missing("assigned_task_count_fallback");
            return Math.Clamp(Div(unchecked(factor * Math.Max(politics, intelligence)), 100), 50, 100);
        }
        private int OccurrenceProbability(BaseReference city, int mode)
        {
            int month = Date.Month;
            int season = mode == 0 ? month switch { 1 => 15, 4 => 5, 7 => 12, 10 => 9, _ => 0 }
                : month switch { 1 => P("second-winter-rate"), 4 => P("second-spring-rate"),
                    7 => P("second-summer-rate"), 10 => P("second-autumn-rate"), _ => 0 };
            int setting = Setting(0x84);
            int coefficient = setting == 1 ? 5 : setting == 2 ? 7 : 2;
            int value;
            if (mode == 0)
            {
                int wounded = N(current.ReadArmyReturnBaseWounded40(city), "occurrence_City40");
                int term = Div(wounded, P("occurrence-wounded-divisor"));
                term = term < 0 ? 0 : Math.Min(term, 10);
                value = Math.Clamp(Div(unchecked((term + season) * coefficient), 30), 0, 35);
                if (Selector() == 0) value = Math.Max(Div(unchecked(Div(unchecked((term + season) * 5), 30) * Advanced()), 100), 0);
            }
            else
            {
                value = Math.Clamp(Div(unchecked(coefficient * season), 30), 0, 23);
                if (Selector() == 0) value = Math.Max(Div(unchecked(Div(unchecked(5 * season), 30) * Advanced()), 100), 0);
            }
            return value;
        }
        private int Ability(int physical, int dimension)
        {
            abilities ??= new(current, package._scenario, package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
            return N(abilities.ResolveDimension(state, N(current.ReadPersonIdentity(physical), "ability_Person_ID"), dimension,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect)), "ability_current");
        }
        private int Selector() => N(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_E58");
        private int Setting(int offset) => N(BattleRemakeCurrentSettings.ReadInt32(state, offset), "settings_" + offset.ToString("X"));
        private int Advanced() => N(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 0), "advanced_disaster_percentage");
        private int AreaSample(int area, int bound, int first)
        {
            var row = Owner(area).Development ?? throw Missing("sample_development");
            int sum = unchecked(Security(area) + Word(row.Commerce, "sample3C") + Word(row.Agriculture, "sample3E") + Word(row.Barracks, "sample40"));
            return Sample(bound, first, N(current.ReadAreaHexIndex(new(area)), "sample_Hex3A"), Population(area), sum);
        }
        private int CitySample(BaseReference city, int bound)
        {
            int troops = N(current.ReadBaseResource(city, BaseResourceKind.Troops), "sample_City3C");
            int wounded = N(current.ReadArmyReturnBaseWounded40(city), "sample_City40");
            int governor = N(current.ReadArmyReturnBaseGovernor2E(city), "sample_City2E");
            int food = N(current.ReadBaseResource(city, BaseResourceKind.Supplies), "sample_City_food");
            int gold = N(current.ReadBaseResource(city, BaseResourceKind.Gold), "sample_City_gold");
            int rawObject = N(current.ReadBaseObjectLink(city), "sample_City4E");
            var obj = current.ReadObjectTableReference(rawObject < 3001 ? rawObject : 0) ?? throw Missing("sample_Object_pointer");
            //22C800: the linked Object's current14, or the actual invalid
            //receiver result6000. This is independent of Base morale44.
            int durability = N(current.ReadObjectNativeValidity(obj), "sample_Object_validity")
                ? N(current.ReadObjectRawField14(obj), "sample_Object14") : 6000;
            return Sample(bound, DateSalt, Physical(city), governor,
                unchecked(food + gold + durability + wounded + troops));
        }
        private int Sample(int bound, int first = 0, int second = 0, int third = 0, int fourth = 0)
        {
            if (bound < 2) return 0;
            sampler ??= package.ReadArmyDestructionSampler(state, current) ?? throw Missing("World450_45C");
            return TacticEffectSourceSamplerMath.Sample(bound, 1, first, second, third, fourth, sampler);
        }
        private int Draw(int bound)
        {
            if (bound < 2) return 0;
            uint before = (state.CampaignAppRandom32 ?? throw Missing("App_random")).State;
            uint next = SourceRandom32Mix.Advance(before);
            state = state with { CampaignAppRandom32 = new(next) };
            return (int)((next & 0x7FFF_FFFFu) % (uint)bound);
        }

        private AreaState Owner(int area) => state.Areas.GetValueOrDefault(area) ?? throw Missing("Area_owner");
        private int Security(int area) => N(Owner(area).Security, "Area_security54");
        private int Population(int area) => N(Owner(area).NativeNormalAftermath?.Quantity52, "Area_population52");
        private byte Timer(int area, int mode) => N(mode == 0 ? Owner(area).MonthlyTimers?.First : Owner(area).MonthlyTimers?.Second, "Area_cooldown");
        private void SetTimer(int area, int mode, byte value)
        {
            var owner = Owner(area); var timers = owner.MonthlyTimers ?? throw Missing("Area_cooldown_owner");
            state = state with { Areas = new(state.Areas) { [area] = owner with { MonthlyTimers = mode == 0
                ? timers with { First = value } : timers with { Second = value } } } };
        }
        private void SetSecurity(int area, int value)
        { state = state with { Areas = new(state.Areas) { [area] = Owner(area) with { Security = value } } }; }
        private byte Flags(int area) => N(current.ReadAreaFlags38(new(area)), "Area38");
        private bool HasEvent(int area) => (Flags(area) & 0x3F) != 0;
        private void SetFlags(int area, byte value) => current.WriteAreaFlags38(new(area), value);
        private static ushort Word(int? value, string name) => value is >= 0 and <= 65535 ? (ushort)value : throw Missing(name);
        private BaseReference City(int area)
        {
            int raw = N(current.ReadAreaCitySlot(new(area)), "Area35");
            return CityAtTable(raw < 52 ? raw : 0);
        }
        private BaseReference CityAtTable(int id) => current.ReadCityTableReference(id) ?? throw Missing("City_table");
        private static BaseReference CityAt(int physical) => new(BaseKind.City, physical);
        private static int Physical(BaseReference city) => N(city.PhysicalSlot, "City_pointer");
        private bool Valid(BaseReference city) => N(current.ReadBaseNativeValidity(city), "City_validity");
        private int ForceId(BaseReference city) => N(current.ReadBaseForceSlot(city), "City_ForceB0");
        private ForceReference Force(BaseReference city)
        { int id = ForceId(city); return current.ReadForceTableReference(id < 52 ? id : 0) ?? throw Missing("City_Force_pointer"); }
        private bool Special(BaseReference city) => N(current.ReadForcePhysicalSlot(Force(city)), "City_Force60") is >= 46 and <= 50;
        private bool ForceFlag(BaseReference city, byte mask)
        {
            var force = Force(city);
            if (!N(current.ReadForceNativeValidity(force), "Force_validity")) return false;
            byte flags = N(state.NativeArmySimulation?.Forces.GetValueOrDefault(N(force.PhysicalSlot, "Force_pointer"))?.Flags12, "Force12");
            return (flags & mask) != 0;
        }
        private bool Player(ForceReference force)
        {
            var selected = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_pointer");
            return N(current.ReadForcePhysicalSlot(selected), "player60") != 0 && selected == force;
        }
        private int Person(int id) => N((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        private IEnumerable<int> BasePersons(BaseReference city)
        {
            int location = N(current.ReadBaseObjectLink(city), "person_Base4E");
            foreach (int person in Registry(RegistryKind.Person))
            {
                if (N(current.ReadPersonFirstLocation(person), "PersonE2") != location) continue;
                if (N(current.ReadPersonStatus(person), "PersonE6") is >= 1 and <= 4) yield return person;
            }
        }
        private bool CityHasImmuneOfficer(BaseReference city)
        {
            foreach (int person in BasePersons(city))
                if (!PersonCurrentBusySource.Read(current, person, Missing) &&
                    N(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x43), "Person_trait67") != 0) return true;
            return false;
        }
        private int BaseTrait(BaseReference city, int effect)
        {
            if (!Valid(city) || ForceId(city) == 0) return 0;
            foreach (int person in BasePersons(city))
                if (!PersonCurrentBusySource.Read(current, person, Missing) &&
                    N(TacticRemakeCurrentTraits.PersonContribution(current, person, effect), "Base_trait") != 0) return 1;
            return 0;
        }
        //232570: the native Area Hex iterator, actual signed occupancy,
        //Army validity, and the original26bit0; no hostile-force substitute.
        private bool AreaHasMarkedArmy(int area)
        {
            for (int index = AreaHexIterator.FindNextIndex(0, area, current); index < 48400;
                index = AreaHexIterator.FindNextIndex(index + 1, area, current))
            {
                var hex = current.ReadHexTableReference(index) ?? throw Missing("harvest_Hex_pointer");
                int slot = unchecked((short)N(current.ReadHexRaw1C(hex), "harvest_Hex1C"));
                if (slot is < 1 or > 500) continue;
                var army = current.ReadArmyUnitTableReference(slot) ?? throw Missing("harvest_Army_pointer");
                if (!N(current.ReadArmyUnitNativeValidity(army), "harvest_Army_validity")) continue;
                byte flags = N(NotificationArmy(state, N(army.PhysicalSlot, "harvest_Army_physical")).NativeTacticRuntime?.Flags26, "harvest_Army26");
                if ((flags & 1) != 0) return true;
            }
            return false;
        }
        private IEnumerable<int> Registry(RegistryKind kind)
        {
            foreach (var entry in current.ReadRegistryReferences(kind) ?? throw Missing("registry_" + kind))
            {
                if (entry is null || entry.Kind != kind) throw Missing("registry_payload_" + kind);
                yield return N(entry.PhysicalSlot, "registry_pointer_" + kind);
            }
        }
        private IEnumerable<BaseReference> Cities() => Registry(RegistryKind.City).Select(CityAt);
        private IEnumerable<int> CityAreas(BaseReference city)
        {
            int physical = Physical(city);
            foreach (int area in Registry(RegistryKind.Area))
                if (N(current.ReadAreaCitySlot(new(area)), "iterator_Area35") == physical) yield return area;
        }
        private void FillNeighbors(int area, HexListHandle list)
        {
            current.ClearTemporaryHexList(list);
            for (int index = 0; index < 8; index++)
            {
                int raw = N(current.ReadAreaNeighborSlot(new(area), index), "neighbor_word");
                if (raw == 0) continue;
                var neighbor = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw Missing("neighbor_pointer");
                if (N(current.ReadAreaNativeValidity(neighbor), "neighbor_validity")) Add(list, RegistryKind.Area, N(neighbor.PhysicalSlot, "neighbor_physical"));
            }
        }
        private HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw Missing("temporary");
        private int Count(HexListHandle list) => N(current.ReadTemporaryHexListCount(list), "temporary_count");
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private void Add(HexListHandle list, RegistryKind kind, int physical)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(kind, physical));
        }
        private List<(HexListNodeReference Node, RegistryKind Kind, int Physical)> Values(HexListHandle list)
        {
            var result = new List<(HexListNodeReference, RegistryKind, int)>();
            for (var node = current.ReadHexListHead(list) ?? throw Missing("temporary_head"); node.Identity.HasValue;
                node = current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next"))
            {
                var value = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload");
                result.Add((node, value.Kind, N(value.PhysicalSlot, "temporary_receiver")));
            }
            return result;
        }
        private bool Contains(HexListHandle list, RegistryKind kind, int physical) => Values(list).Any(value => value.Kind == kind && value.Physical == physical);
        private void RemoveAll(HexListHandle list, RegistryKind kind, int physical)
        {
            foreach (var entry in Values(list))
                if (entry.Kind == kind && entry.Physical == physical) current.RemoveFacilityHexTemporary(list, entry.Node);
        }
        private void Shuffle(HexListHandle list)
        {
            var values = Values(list);
            int size = values.Count, salt = 0;
            for (int remaining = unchecked(size / 2 * size); remaining > 0; remaining--)
            {
                int first = Sample(size, salt++), second = size - Sample(size, salt++) - 1;
                if (first == second) continue;
                var a = values[first]; var b = values[second];
                current.WriteRegistryNodePayload(new(a.Node.Identity), new(b.Kind, b.Physical));
                current.WriteRegistryNodePayload(new(b.Node.Identity), new(a.Kind, a.Physical));
                values[first] = (a.Node, b.Kind, b.Physical); values[second] = (b.Node, a.Kind, a.Physical);
            }
        }
        private void Sort(HexListHandle list, Func<int, int, bool> before)
        {
            var values = Values(list);
            for (int i = 1; i < values.Count; i++)
            {
                var value = values[i]; int at = i;
                while (at > 0 && before(value.Physical, values[at - 1].Physical)) { values[at] = values[at - 1]; at--; }
                values[at] = value;
            }
            current.ReorderCurrentList(list, values.Select(value => value.Node).ToArray());
        }
        private static void Increment(IDictionary<int, int> counts, int id) => counts[id] = counts.TryGetValue(id, out int value) ? unchecked(value + 1) : 1;

        private void ReportEnded(IReadOnlyList<BaseReference> cities, int mode)
        {
            for (int at = 0; at < cities.Count; at += 5)
            {
                var group = cities.Skip(at).Take(5).ToArray();
                History(group, (mode == 0 ? 0x507B : 0x5088) + group.Length - 1);
            }
        }
        private HistoryMessageOperands Operands(IReadOnlyList<BaseReference> cities, bool history)
        {
            ushort? At(int index) => index < cities.Count ? N(current.ReadBaseObjectLink(cities[index]), "report_Base4E") : null;
            int ruler = 5012;
            if (history)
            {
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("report_player_pointer");
                if (N(current.ReadForceNativeValidity(player), "report_player_validity")) ruler = N(current.ReadMoraleWaveRuler22EAC0(player), "report_player_ruler");
            }
            return new(Base0: At(0), Base1: At(1), Base2: At(2), Base3: At(3), Base4: At(4), Person0: history ? 5012 : 0, Person1: history ? ruler : 0);
        }
        private void History(IReadOnlyList<BaseReference> cities, int root)
        {
            if (cities.Count == 0) return;
            int district = N(current.ReadBaseDistrictSlot(cities[0]), "history_City_District");
            int hex = N(CurrentGateMapQueries.FindHex(cities[0], current).PhysicalSlot, "history_City_Hex");
            string text = Format(root, Operands(cities, true));
            int day = (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("history_day450")).Day450;
            var effects = new List<CampaignEffectRequest>();
            state = package.WriteHistory(state, current, new(1, district, hex, day, string.Empty, text, 5012, MarkBit0: 1, MarkBit4: 1), effects);
            Append(effects);
        }
        private string Format(int root, HistoryMessageOperands operands)
        { var result = package.FormatNormalTurnReport(state, current, root, operands); state = result.State; return result.Text; }
        private void Show(IReadOnlyList<BaseReference> cities, int root, int kind, int sound)
        {
            Sound(sound); state = package.AppendNormalTurnDisasterEventCurrent(state, kind);
            Narration(Format(root, Operands(cities, false)));
            state = package.AppendNormalTurnDisasterEventCurrent(state, null);
        }
        private void Sound(int id) => Append([new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, id, null, null, null, null) }]);
        private void Narration(string text) => Append([new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.NarrationText, null, null, text) }]);
        private void Append(IReadOnlyList<CampaignEffectRequest> effects)
        {
            if (effects.Count != 0) state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("pending_owner")).Append(
                CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("revision"), effects) };
        }
        private int P(string name)
        {
            string key = "simulation.normal-turn.disaster." + name;
            var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("parameters");
            if (bank.Float32Values.ContainsKey(key)) throw Missing("integer_parameter_" + name);
            if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
            // Saved integers remain authoritative. Mutable spread inputs have
            // only a fresh producer; neither locale reconstructs missing saves.
            int? knownTc = name switch
            {
                "recovery-security-divisor" or "second-recovery-security-divisor" => 3,
                "spread-population-divisor" => 30000, "spread-maximum" => 10,
                "second-spread-random-bound" => 15, "occurrence-wounded-divisor" => 20000,
                "city-choice-limit" or "second-city-choice-limit" => 4,
                "new-area-limit" => 28, "second-new-area-limit" => 20,
                "spread-city-limit" or "second-spread-city-limit" => 10,
                "second-winter-rate" => 12, "second-spring-rate" => 17,
                "second-summer-rate" => 1, "second-autumn-rate" => 4,
                "harvest-probability" => 40, "harvest-preference-probability" => 50,
                "harvest-low-count" => 2, "harvest-middle-count" => 5, "harvest-high-count" => 7,
                _ => null
            };
            return NormalTurnDisasterParameters.ReadGenerationDefault(package.Seed.Language, name, knownTc)
                ?? throw Missing("current_parameter_" + name);
        }
        private static int Div(int value, int divisor) => divisor == 0 || value == int.MinValue && divisor == -1
            ? throw Missing("division") : value / divisor;
        private static T N<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal-turn.disasters." + field);
    }
}
