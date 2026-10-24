using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ActionReportStrategistPage(int MessageId, string Text);
public sealed record ActionReportStrategistSnapshot(ActionReportStrategistPerson? Speaker,
    bool? PortraitVisible, ushort? PortraitAppearance, ImmutableArray<ActionReportStrategistPage> Pages,
    bool Completed, int? ReachedMessageId);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One explicit selected-category entry. Calling this consumes
    /// presentation random draws; querying Snapshot or painting a View does not.
    /// The list must be the result of the real completed list preparation,
    /// including its actual empty-fallback bit.</summary>
    public ActionReportStrategistPagesOperation BeginActionReportStrategistPages(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ActionReportPreparedList list)
        => new(this, state, current, pool, list);

    public sealed class ActionReportStrategistPagesOperation
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        private readonly ActionReportPreparedList list;
        private readonly List<ActionReportStrategistPage> pages = [];
        private bool entered, completed;
        private ActionReportStrategistPerson? speaker;
        private bool? portraitVisible;
        private ushort? portrait;
        private int? reachedMessage;
        public GameState State { get; private set; }
        public ActionReportStrategistSnapshot Snapshot => new(speaker, portraitVisible, portrait,
            pages.ToImmutableArray(), completed, reachedMessage);

        internal ActionReportStrategistPagesOperation(ArmyCampaignRuleAdapter package, GameState state,
            SourceWorldContext current, WorldIndexContext pool, ActionReportPreparedList list)
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("same_current_pool");
            this.package = package; State = state; this.current = current; this.pool = pool;
            this.list = list ?? throw new ArgumentNullException(nameof(list));
        }

        public ActionReportStrategistSnapshot Produce()
        {
            if (entered) throw Missing("operation_already_entered");
            entered = true;
            var selection = new PersonDeathHistorySelection(State, current, pool,
                new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language)), package._governorInputs,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(State, force, effect));
            var worldForce = OwnedCurrentPlayerForce.ReadCurrentReference(State, current) ?? throw Missing("World3A");
            var primary = selection.PrimaryDistrict(worldForce);
            byte raw = K(current.ReadDistrictForceSlot(primary), "primary_District10");
            var actor = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("actor_Force");
            var person = selection.Select(primary);
            int physical = K(person.PhysicalSlot, "speaker_pointer");
            // The first native validity call has no branch, but still precedes identity.
            _ = K(current.ReadPersonNativeValidity(physical), "speaker_validity");
            ushort identity = K(current.ReadPersonIdentity(physical), "speaker10");
            speaker = new(physical, identity);
            var lookup = (identity < 6001 ? current.ReadPersonLookupReference(identity)
                : current.ReadTransferPersonTableReference(0)) ?? throw Missing("portrait_lookup");
            bool visible = lookup.PhysicalSlot is int at && K(current.ReadPersonNativeValidity(at), "portrait_validity");
            portraitVisible = visible;
            if (visible) portrait = K(current.ReadPersonPortraitAppearance(lookup.PhysicalSlot!.Value), "portrait_A6");

            switch (list.Category)
            {
                case 0: Add(list.UsesEmptyFallback ? 0x5197 : 0x518f, actor); break;
                case 1: Add(list.UsesEmptyFallback ? 0x5106 : 0x50ff, actor); break;
                case 2:
                    CommandSummary(actor, [2, 3], 0x5122, 0x511b, 0x510d, 0x5114);
                    break;
                case 3: RelationSummary(actor); break;
                case 4:
                    CommandSummary(actor, [10, 11, 23, 12, 24, 13, 14], 0x5142, 0x513b, 0x512d, 0x5134);
                    break;
                case 5: SupplySummary(actor); break;
                case 6: AssociationSummary(actor); break;
                default: throw Missing("category");
            }
            completed = true;
            return Snapshot;
        }

        private int MarkedRecords()
        {
            if (list.Records.IsDefault) throw Missing("complete_list_records");
            int count = 0;
            foreach (var entry in list.Records)
                if (((entry ?? throw Missing("record_pointer")).Flags & 0x20) != 0) count++;
            return count;
        }

        private void CommandSummary(ForceReference actor, int[] kinds, int empty,
            int commandsOnly, int recordsOnly, int both)
        {
            int records = MarkedRecords(), commands = 0;
            foreach (int person in ForcePersons(actor))
            {
                var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("person_associated22AC00");
                if (K(current.ReadLinkedEntityNativeValidity(linked), "associated_validity") &&
                    kinds.Contains(CommandKind(linked))) commands++;
            }
            if (records < 1) Add(commands < 1 ? empty : commandsOnly, actor,
                commands < 1 ? [] : [commands]);
            else Add(commands < 1 ? recordsOnly : both, actor,
                commands < 1 ? [records] : [records, commands]);
        }

        private void RelationSummary(ForceReference actor)
        {
            int friendly = 0, unfriendly = 0, alliances = 0;
            foreach (var value in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("Force_C0"))
            {
                if (value is not { Kind: RegistryKind.Force }) throw Missing("Force_C0_payload");
                var other = new ForceReference(value.PhysicalSlot);
                if (Ordinal(other) == Ordinal(actor)) continue;
                // The indexed timer belongs to OTHER, with actor as column.
                if ((ReadReportForceRelationByte(State, other, Ordinal(actor), Missing) & 0x7f) != 0) alliances++;
                int level = FriendshipLevel(other, actor);
                if (level >= 4) friendly++;
                else if (FriendshipLevel(other, actor) < 3) unfriendly++;
            }
            Add(0x5129, actor, [friendly, unfriendly, alliances]);
        }

        // 2ADA50 is the raw-byte threshold leaf. It has no validity guard;
        // some event callers have their own guards, which do not belong here.
        private int FriendshipLevel(ForceReference first, ForceReference second)
        {
            int column = Ordinal(second), value = 0;
            if (column < 52)
            {
                int receiver = Ordinal(first);
                var relations = receiver == 0 ? State.NativeForceUnregisterCurrent?.PlaceholderRelations13 :
                    State.NativeArmySimulation?.Forces.GetValueOrDefault(receiver)?.NormalAftermath?.Relations13;
                if (relations is not { Length: 52 }) throw Missing("Force13_relations");
                value = relations[column];
            }
            var simulation = State.NativeArmySimulation ?? throw Missing("relation_parameters");
            for (int level = 1; level <= 5; level++)
            {
                if (!simulation.Parameters.Int32Values.TryGetValue(
                    SimulationParameterKeys.ForceRelations.LevelThreshold(level), out int threshold))
                    throw Missing("relation_threshold_" + level);
                if (value < threshold) return level - 1;
            }
            return 5;
        }

        private void SupplySummary(ForceReference actor)
        {
            int flaggedArmies = 0, total = 0, bases = 0;
            foreach (int physical in ForceArmies(actor))
            {
                var slot = State.ReadArmyLifecycle()?.ReceiverSlots()[physical] ?? throw Missing("Army_current_slot");
                var image = slot.ActiveUnitId is int id ? State.Units.GetValueOrDefault(id) : slot.PendingImage;
                byte flags = K(image?.NativeTacticRuntime?.Flags26, "Army26");
                if ((flags & 1) != 0) flaggedArmies++;
                total++;
            }
            foreach (var entry in current.ReadRegisteredBases() ?? throw Missing("Foothold80"))
            {
                var target = entry ?? throw Missing("Foothold80_payload");
                int key = Ordinal(actor);
                if (key is < 1 or > 51 || K(current.ReadBaseForceSlot(target), "Foothold_virtualB0") != key) continue;
                if (ReadNearbyEnemy(target)) bases++;
            }
            if (flaggedArmies < 1)
            {
                if (bases > 0) Add(0x5157, actor, [bases]);
                else if (total > 0) Add(0x515e, actor, [total]);
                else Add(0x5165, actor);
            }
            else Add(bases < 1 ? 0x5150 : 0x5149, actor,
                bases < 1 ? [flaggedArmies] : [flaggedArmies, bases]);
        }

        private bool ReadNearbyEnemy(BaseReference receiver)
        {
            // Foothold v68 consumes saved Base67 bit0, independently from
            // native validity. City and Gate zero each have their own owner.
            int physical = K(receiver.PhysicalSlot, "nearby_Base_pointer");
            bool? value = receiver.Kind switch
            {
                BaseKind.City when (uint)physical < 52 => physical == 0 ? State.UnassignedCityHasNearbyEnemyArmy :
                    State.Cities.GetValueOrDefault(physical)?.HasNearbyEnemyArmy,
                BaseKind.Gate when (uint)physical < 11 => physical == 0 ? State.UnassignedGateHasNearbyEnemyArmy :
                    State.Gates.GetValueOrDefault(physical + 51)?.HasNearbyEnemyArmy,
                _ => null
            };
            return K(value, "Base67_nearby_enemy");
        }

        private void AssociationSummary(ForceReference actor)
        {
            int marked = MarkedRecords(), missions = 0, associated = 0;
            foreach (int person in ForcePersons(actor))
            {
                var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("mission_associated22AC00");
                if (K(current.ReadLinkedEntityNativeValidity(linked), "mission_associated_validity") && CommandKind(linked) == 21)
                    missions++;
            }
            foreach (int person in ForcePersons(actor))
            {
                ushort id = K(current.ReadPersonIdentity(person), "association_Person10");
                var force = current.ReadPersonForce(person) ?? throw Missing("association_Person_Force");
                // 229930(mode0) searches only the three Person words. A target
                // byte of zero does not make a matching word disappear.
                if (id == 0) continue;
                for (int index = 0; index < 3; index++)
                    if (K(current.ReadForceAssociationPersonId(force, index), "Force196_" + index) == id)
                    { associated++; break; }
            }
            // The following native Force-neighborhood loop computes a local
            // boolean which is never read by this formatter. It neither writes
            // world state nor draws randoms; no compatibility scan is needed.
            if (marked < 1)
            {
                if (missions > 0) Add(0x517a, actor, [missions]);
                else Add(associated < 1 ? 0x5181 : 0x5197, actor);
            }
            else Add(missions < 1 ? 0x516c : 0x5173, actor,
                missions < 1 ? [marked] : [marked, missions]);
        }

        private IEnumerable<int> ForcePersons(ForceReference actor)
        {
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person) ?? throw Missing("Person100_descriptor");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("Person100_head");
            int key = Ordinal(actor);
            while (true)
            {
                // Each end comparison reads the real Force again, including
                // an initially empty chain; the filter retains begin's key.
                _ = Ordinal(actor);
                if (!node.Identity.HasValue) yield break;
                var payload = current.ReadRegistryNodePayload(node);
                if (payload is not { Kind: RegistryKind.Person, PhysicalSlot: int person }) throw Missing("Person100_payload");
                if (key is >= 1 and <= 51)
                {
                    // 22CF60 returns the raw District10 byte, not a looked-up
                    // Force pointer; do not add a Force0 lookup here.
                    if (K(current.ReadPersonOldForceSlot(person), "Person_raw_Force") == key &&
                        K(current.ReadPersonStatus(person), "Person_iterator_E6") is >= 1 and <= 4)
                        yield return person;
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("Person100_live_next");
            }
        }

        private IEnumerable<int> ForceArmies(ForceReference actor)
        {
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw Missing("Army60_descriptor");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("Army60_head");
            int key = Ordinal(actor);
            while (true)
            {
                _ = Ordinal(actor);
                if (!node.Identity.HasValue) yield break;
                var payload = current.ReadRegistryNodePayload(node);
                if (payload is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw Missing("Army60_payload");
                if (key is >= 1 and <= 51 && current.ReadArmyUnitRawForceSlot(new(physical)) == key &&
                    !K(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(State, current, physical), "Army233560"))
                    yield return physical;
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("Army60_live_next");
            }
        }

        private int CommandKind(PersonLinkedEntityReference linked)
        {
            var owner = current.ReadOwnedPersonCommandManager() ?? throw Missing("personnel_manager");
            int id = K(linked.Identity, "associated_pointer");
            if (owner.Returns.ContainsKey(id)) return 15;
            if (owner.Disappearances?.ContainsKey(id) == true) return 16;
            throw Missing("associated_virtual98");
        }

        private void Add(int message, ForceReference actor, int[]? integers = null)
        {
            reachedMessage = message;
            ushort person0 = K(current.ReadPersonIdentity(speaker!.PhysicalSlot), "message_speaker10");
            int physical = Ordinal(actor);
            ushort person1 = physical == 0 ? (ushort)5014 :
                K(current.ReadForceRecord(physical)?.RawRuler10, "message_ruler10");
            var result = package.FormatActionStrategistReport(State, current, message,
                new(Person0: person0, Person1: person1, Integers: integers), value => State = value);
            State = result.State;
            // 680C30 appends the complete returned string as one page. Newline
            // characters do not manufacture additional source page entries.
            pages.Add(new(message, result.Text));
        }
        private int Ordinal(ForceReference force) => K(current.ReadForcePhysicalSlot(force), "Force_virtual60");
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("action-report.strategist." + field);
    }

    private HistoryTextResult FormatActionStrategistReport(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands, Action<GameState> publishReached)
    {
        var latest = state;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var people = new DeploymentNarrationCurrentPeople(state, current, abilities,
            operands.Person0, operands.Person1,
            panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(latest, force, effect),
            appearanceRepository: _repository, appearanceScenario: _scenario, appearanceLanguage: Seed.Language);
        var context = new ReportCurrent(this, () => latest, current, people, operands);
        int Draw(int bound)
        {
            if (bound < 2) return 0;
            var saved = latest.CampaignAppRandom32 ?? throw HistoryMissing("App32_draw_state");
            var random = new SharedSourceRandom32(saved.State);
            int value = random.Next(bound);
            latest = latest with { CampaignAppRandom32 = new(random.State) };
            publishReached(latest);
            return value;
        }
        var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
        string text = vm.ResolveActionStrategistReport(context, Draw, root);
        return new(latest, text);
    }
}
