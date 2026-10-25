using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnMonthlyCaptiveEscapesCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new MonthlyCaptiveEscapeSource(this, state, current, pool).Run();

    /// <summary>29FA40, reached by the normal Turn's month-start action14.
    /// All three city groups and their Person lists are operation-local.</summary>
    private sealed class MonthlyCaptiveEscapeSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        private sealed record EscapeGroup(BaseReference City, HexListHandle People);
        private readonly SortedDictionary<int, EscapeGroup> all = [], held = [], owned = [];
        private readonly List<CampaignEffectRequest> effects = [];
        private TacticRemakeCurrentAbilities? abilities;

        internal GameState Run()
        {
            if (package.Seed.Language != "TC") throw Missing("SC_29FA40_owner");
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("shared_pool");
            var candidates = Temporary();
            try
            {
                var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");
                foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
                {
                    int person = K(entry, "Person_pointer");
                    if (K(current.ReadPersonStatus(person), "PersonE6") != 6 || Progress(person) <= 1 ||
                        !AtBase(person) || EscapePrevented(person)) continue;
                    int probability = Probability(person);
                    int sample = TacticEffectSourceSamplerMath.Sample(100, 1,
                        unchecked(date.Year + date.Month + date.Day), Identity(person), 0, 0, Sampler());
                    if (sample < probability) Append(candidates, person);
                }
                Sort(candidates, date.Year);
                var node = Head(candidates);
                while (node.Identity.HasValue)
                {
                    int person = Payload(node);
                    var city = HoldingCity(person); //retained before2E7890
                    var group = Group(all, city);
                    if (K(current.ReadTemporaryHexListCount(group.People), "city_escape_count") < 3)
                    {
                        Append(group.People, person);
                        var force = BaseForce(city);
                        if (IsPlayer(force)) Append(Group(held, city).People, person);
                        else if (IsPlayer(current.ReadPersonForce(person) ?? throw Missing("Person_Force22CB40")))
                            Append(Group(owned, city).People, person);
                        //The very same2E7890 writer is used with notification0;
                        //there is no duplicate release or synthetic Return task.
                        state = package.ApplyCaptiveReleaseCurrent(state, current, person, date,
                            () => Sampler().Day450, effects, null, reportRelease: false);
                        var aftermath = current.ReadOfficerAftermath(person) ?? throw Missing("released_resentment_owner");
                        int target = aftermath.Resentment?.TargetOfficerId ?? aftermath.SourceKnowledge?.UnmappedResentmentTarget
                            ?? throw Missing("released_resentment162");
                        if (target == 0)
                        {
                            var captor = BaseForce(city);
                            ushort raw = K(current.ReadForceRecord(K(captor.PhysicalSlot, "captor_Force_pointer"))?.RawRuler10, "captor_ruler10");
                            var ruler = Person(raw);
                            if (ruler.PhysicalSlot is int physical && K(current.ReadPersonNativeValidity(physical), "captor_ruler_validity"))
                            {
                                ushort id = (ushort)Math.Min((int)Identity(physical), 6000);
                                byte intensity = (byte)Math.Clamp(Parameter("resentment-intensity", 2), 0, 255);
                                bool mapped = state.Officers.ContainsKey(id);
                                current.SetOfficerAftermath(person, aftermath with
                                {
                                    Resentment = new(mapped ? id : null, intensity),
                                    SourceKnowledge = (aftermath.SourceKnowledge ?? new(Identity(person), null, null, null)) with
                                    { UnmappedResentmentTarget = mapped ? null : id }
                                });
                                state = current.PublishOfficerAftermath(state, Identity(person));
                            }
                        }
                    }
                    node = Next(node);
                }
                //All keys are actual City receivers from2A9DC0. Their native
                //contiguous allocation order is exactly physical City order;
                //there is no invented ordering between City and Gate objects.
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("report_player32EFA0");
                foreach (var group in owned.Values) Report(group, player, playerHeld: false);
                foreach (var group in held.Values) Report(group, player, playerHeld: true);
                if (effects.Count != 0)
                    state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("effect_requests"))
                        .Append(CampaignEffectSource.MonthlyMaintenance,
                            state.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("effect_revision"), effects) };
                return state;
            }
            finally
            {
                //3076F0(full range)->305690 visits right subtree, receiver,
                //then left: descending keys, independently of tree rotations.
                foreach (var group in owned.Reverse()) Release(group.Value.People);
                foreach (var group in held.Reverse()) Release(group.Value.People);
                foreach (var group in all.Reverse()) Release(group.Value.People);
                Release(candidates);
            }
        }

        private int Progress(int person)
        {
            int value = current.ReadOfficerAftermath(person)?.SourceKnowledge?.CaptureResetCode
                ?? throw Missing("Person168_captivity_progress");
            return value is >= 0 and <= 255 ? value : throw Missing("Person168_byte");
        }
        private bool AtBase(int person) => PersonPositionRepair.DecodeSecondLocation(
            K(current.ReadPersonSecondLocation(person), "PersonE4")).Kind is 5 or 6;
        private bool EscapePrevented(int person)
        {
            if (!AtBase(person)) return false;
            ushort raw = K(current.ReadPersonSecondLocation(person), "protection_PersonE4");
            var actual = BaseLookup.Find(raw < 62 ? raw : 0, current);
            var governor = Governor(actual);
            return governor.PhysicalSlot is int physical && K(current.ReadPersonNativeValidity(physical), "protection_governor_validity") &&
                AtBase(physical) && K(TacticRemakeCurrentTraits.CommanderHasEffect(current, physical, 107), "protection_trait107");
        }
        private BaseReference HoldingCity(int person)
        {
            ushort raw = K(current.ReadPersonSecondLocation(person), "holding_PersonE4");
            if (raw < 52) return current.ReadCityTableReference(raw) ?? throw Missing("holding_City_table");
            if (raw is >= 52 and <= 61)
            {
                var gate = current.ReadGateTableReference(raw - 51) ?? throw Missing("holding_Gate_table");
                return CurrentGateMapQueries.FindCity(gate, current);
            }
            throw Missing("holding_not_Base_after_2346D0");
        }
        private TransferPersonReference Governor(BaseReference location) =>
            Person(K(current.ReadArmyReturnBaseGovernor2E(location), "Base_governor2E"));
        private TransferPersonReference Person(ushort raw) =>
            (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("Person_lookup");
        private ForceReference BaseForce(BaseReference location)
        {
            int raw = K(current.ReadBaseForceSlot(location), "Base_Force_B0");
            return current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("Base_Force_pointer");
        }
        private bool IsPlayer(ForceReference force)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_Force");
            int own = K(current.ReadForcePhysicalSlot(player), "player_Force60");
            return own != 0 && own == K(current.ReadForcePhysicalSlot(force), "compared_Force60");
        }
        private int Ability(int person, int dimension)
        {
            abilities ??= new(current, package._scenario, package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
            return K(abilities.ResolveDimension(state, Identity(person), dimension,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect)), "current_ability_" + dimension);
        }
        private int Probability(int person)
        {
            if (!K(current.ReadPersonNativeValidity(person), "probability_Person_validity")) return 0;
            var city = HoldingCity(person);
            TransferPersonReference governor = K(current.ReadBaseNativeValidity(city), "probability_City_validity")
                ? Governor(city) : new(null);
            int progress = Progress(person);
            int intelligence = Ability(person, 2), strength = Ability(person, 1);
            int strongest = Math.Max(50, Math.Max(strength, intelligence));
            int guard = 0;
            if (governor.PhysicalSlot is int physical && K(current.ReadPersonNativeValidity(physical), "probability_governor_validity"))
            {
                int guardIntelligence = Ability(physical, 2), guardStrength = Ability(physical, 1);
                guard = Math.Max(guardStrength, guardIntelligence);
            }
            int minimum = Parameter("minimum-probability", 5);
            int divisor = Parameter("governor-ability-divisor", 5);
            if (divisor == 0 || unchecked(100 - guard) == int.MinValue && divisor == -1) throw Missing("probability_division");
            int rate = unchecked(unchecked(strongest * progress) / 12 + unchecked(100 - guard) / divisor);
            return rate < minimum ? minimum : rate > Parameter("maximum-probability", 50)
                ? Parameter("maximum-probability", 50) : rate;
        }
        private void Sort(HexListHandle list, int year)
        {
            var node = Head(list);
            if (!node.Identity.HasValue) return;
            node = Next(node);
            while (node.Identity.HasValue)
            {
                var next = Next(node);
                var previous = current.ReadHexListNodePrevious(node) ?? throw Missing("sort_previous");
                if (Before(Payload(node), Payload(previous)))
                {
                    var insertion = previous;
                    var earlier = current.ReadHexListNodePrevious(previous) ?? throw Missing("sort_previous");
                    while (earlier.Identity.HasValue && Before(Payload(node), Payload(earlier)))
                    { insertion = earlier; earlier = current.ReadHexListNodePrevious(earlier) ?? throw Missing("sort_previous"); }
                    var order = new List<HexListNodeReference>();
                    for (var cursor = Head(list); cursor.Identity.HasValue; cursor = Next(cursor)) order.Add(cursor);
                    order.RemoveAll(value => value.Identity == node.Identity);
                    int at = order.FindIndex(value => value.Identity == insertion.Identity);
                    if (at < 0) throw Missing("sort_insertion");
                    order.Insert(at, node); current.ReorderCurrentList(list, order);
                }
                node = next;
            }
            bool Before(int first, int second)
            {
                byte a = K(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, first, package._governorInputs), "sort_E8");
                byte b = K(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, second, package._governorInputs), "sort_E8");
                if (a != b) return a > b;
                int p = Probability(first), q = Probability(second);
                if (p != q) return p > q;
                int firstYears = unchecked(year - K(package._governorInputs.ReadCurrentPhysicalWord166(state, first), "sort_166"));
                int secondYears = unchecked(year - K(package._governorInputs.ReadCurrentPhysicalWord166(state, second), "sort_166"));
                return firstYears != secondYears ? firstYears > secondYears : Identity(first) < Identity(second);
            }
        }

        private EscapeGroup Group(SortedDictionary<int, EscapeGroup> groups, BaseReference city)
        {
            int physical = K(city.PhysicalSlot, "group_City_pointer");
            if (!groups.TryGetValue(physical, out var group))
            { group = new(city, Temporary()); groups.Add(physical, group); }
            return group;
        }
        private void Report(EscapeGroup group, ForceReference player, bool playerHeld)
        {
            var first = Head(group.People);
            if (!first.Identity.HasValue) throw Missing("report_group_empty_receiver");
            int person = Payload(first);
            int count = K(current.ReadTemporaryHexListCount(group.People), "report_group_count");
            var force = playerHeld ? BaseForce(group.City)
                : current.ReadPersonForce(person) ?? throw Missing("report_Person_Force");
            ushort advisor = K(current.ReadAssignmentForceAdvisor52(K(force.PhysicalSlot, "report_Force_pointer")), "report_advisor52");
            int speaker = advisor is >= 1 and <= 6000 ? advisor : 5012;
            string statement = Format(0x5D81, new(Person0: Identity(person)));
            effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.ControllerQueueEvent,
                playerHeld ? 6 : 8, null, null) });
            effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.ConditionalSpeakerText,
                null, Identity(person), statement) });
            ushort location = K(current.ReadBaseObjectLink(group.City), "report_Base4E");
            string main = Format(count == 1 ? 0x5D96 : 0x5D97, new(Person0: Identity(person), Base0: location));
            ushort ruler = K(current.ReadMoraleWaveRuler22EAC0(player), "report_player_ruler");
            string supplemental = Format(count == 1 ? 0x51EB : 0x5204,
                new(Person0: speaker, Person1: ruler, Person2: Identity(person),
                    Base0: K(current.ReadBaseObjectLink(group.City), "report_supplemental_Base4E"),
                    Integers: count == 1 ? null : [K(current.ReadTemporaryHexListCount(group.People), "report_supplemental_count")]));
            int district = playerHeld ? K(current.ReadBaseDistrictSlot(group.City), "report_Base_v78")
                : K(current.ReadPersonDistrictSlot(person), "report_PersonE0");
            var position = PersonReturnCommands.ResolvePersonPosition(state, current, person,
                () => state.NativeMapSeason?.ThirdDate ?? throw Missing("report_position_date"));
            if (current.ReadOwnedPersonCommandManager() is { } tasks) state = state with { PersonnelCommands = tasks };
            int hex = K(position.PhysicalSlot, "report_Hex22DEF0");
            int day = Sampler().Day450;
            state = package.WriteHistory(state, current, new(5, district, hex, day, main, supplemental,
                speaker, MarkBit0: 0, MarkBit4: playerHeld ? 1 : 0), effects);
        }
        private string Format(int root, HistoryMessageOperands operands)
        {
            var result = package.FormatNormalTurnReport(state, current, root, operands);
            state = result.State; return result.Text;
        }
        private int Parameter(string name, int knownTc)
        {
            //TC immutable data:1A4A4B4 divisor5,1A4A4B8 minimum5,
            //1A4A4BC maximum50,1A4A4C0 resentment2 (typed/data.tsv254874..7).
            string key = "simulation.captive-escape." + name;
            var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("parameters");
            if (parameters.Float32Values.ContainsKey(key)) throw Missing("integer_parameter_" + name);
            return parameters.Int32Values.TryGetValue(key, out int value) ? value : knownTc;
        }
        private TacticSourceSamplerWorld Sampler() => package.ReadArmyDestructionSampler(state, current)
            ?? throw Missing("World450_45C");
        private ushort Identity(int person) => K(current.ReadPersonIdentity(person), "Person10");
        private HexListHandle Temporary()
        {
            Lock(true); try { return current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor"); }
            finally { Lock(false); }
        }
        private void Append(HexListHandle handle, int person)
        {
            Lock(true);
            try
            {
                var node = current.AllocateRegistryNode(new(handle.Identity)) ?? throw Missing("temporary_node");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, person));
            }
            finally { Lock(false); }
        }
        private HexListNodeReference Head(HexListHandle handle) => current.ReadHexListHead(handle) ?? throw Missing("temporary_head");
        private HexListNodeReference Next(HexListNodeReference node) => current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next");
        private int Payload(HexListNodeReference node)
        {
            var value = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload");
            if (value.Kind != RegistryKind.Person) throw Missing("temporary_Person_kind");
            return K(value.PhysicalSlot, "temporary_Person_pointer");
        }
        private void Release(HexListHandle handle)
        { Lock(true); try { current.ClearTemporaryHexList(handle); current.ReleaseTemporaryHexList(handle); } finally { Lock(false); } }
        private void Lock(bool enter)
        {
            if (!K(current.ReadRegistryLockEnabled(), "list_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal.turn.monthly-captive-escape." + field);
    }
}
