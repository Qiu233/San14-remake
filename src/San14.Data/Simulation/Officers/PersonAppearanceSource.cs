using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One reached annual appearance manager on the enclosing Turn's
    /// private candidate, current definitions and shared list pool. No calendar,
    /// host stream, presentation or committed world is published here.</summary>
    internal sealed partial class PersonAppearanceSource
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        private readonly Action<string, GameState>? observe;
        private readonly List<PersonAssignmentPrecallResult> associations = [];
        private readonly List<CampaignEffectRequest> effects = [];
        private GameState latest;

        internal PersonAppearanceSource(ArmyCampaignRuleAdapter package, GameState state,
            SourceWorldContext current, WorldIndexContext pool, Action<string, GameState>? observe = null)
        {
            this.package = package; latest = state; this.current = current; this.pool = pool; this.observe = observe;
        }

        //Shared2EE160 leaf only. No annual iteration, resource binding, list
        //allocation, calendar processing or outer publication is performed.
        internal GameState ApplyInitialLoyaltyCurrent(int person, TransferPersonReference ruler)
        {
            InitialLoyalty(person, ruler);
            return latest;
        }

        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("current_shared_pool");
            // Bind the existing scalar/association owners once, before any
            // writes. Nothing is rebound from a mid-operation role projection.
            package._personRelationInputs.BindCurrent(latest, current);
            package._personAssignmentPrecallInputs.BindCurrent(latest, current);
            HexListHandle? excluded = null, notifications = null;
            try
            {
                excluded = Temporary();
                FillExcluded(excluded);
                notifications = Temporary();
                var date = (latest.NativeMapSeason ?? throw Missing("current_third_date")).ThirdDate;
                for (int slot = 0; slot < 1851; slot++)
                {
                    var pointer = current.ReadTransferPersonTableReference(slot) ?? throw Missing("physical_person_table");
                    int person = Physical(pointer, "physical_person_pointer");
                    if (Status(person) != 7) continue;
                    ushort appearance = AppearanceYear(person);
                    if ((int)appearance > date.Year || Contains(excluded, person)) continue;
                    var related = SelectRelated(person);
                    if (CanJoin(related))
                    {
                        if (!Join(person, related)) continue;
                        Observe("after-join");
                        var force = Force(person);
                        if (Known(current.ReadForceNativeValidity(force), "notification_force_validity"))
                        {
                            var player = current.ReadCurrentPlayerForce() ?? throw Missing("notification_player_force");
                            if (Force(person) == player)
                            {
                                Append(notifications, new(RegistryKind.Person, person));
                                ushort id = Identity(Physical(related, "notification_related_pointer"));
                                Change(person, p => p with { AppearanceNotificationPersonId = id });
                            }
                        }
                    }
                    else
                    {
                        if (!Free(person)) { Observe("after-free-invalid-city"); continue; }
                        Observe("after-free");
                    }
                    // Register the actual current pointer. The next P's Q scan
                    // sees this live list, including successful new nodes.
                    _ = ObjectRegistration.Register(new(RegistryKind.Person, person), current);
                    Observe("after-register");
                    var actualForce = Force(person);
                    if (Known(current.ReadForceNativeValidity(actualForce), "tail_force_validity"))
                    {
                        actualForce = Force(person);
                        var player = current.ReadCurrentPlayerForce() ?? throw Missing("tail_player_force");
                        if (actualForce == player) Transfer().RefreshTransferService(1, 1, 0);
                    }
                }
                if (Known(current.ReadTemporaryHexListCount(notifications), "notification_count") != 0)
                    Sort(notifications);
                var feedback = new PersonAppearanceFeedback(package, current, () => latest,
                    value => latest = value, DrawApp32, effects);
                var node = Head(notifications);
                while (node.Identity.HasValue)
                {
                    var value = Payload(node, RegistryKind.Person);
                    _ = feedback.Run(Known(value.PhysicalSlot, "notification_person_pointer"));
                    Observe("after-notification");
                    node = Next(node); //Read live next only after the consumer.
                }
                ClearRelease(notifications); notifications = null;
                ClearRelease(excluded); excluded = null;
                latest = OwnedArmyLifecycleDetachedInputs.PublishCurrent(latest, current);
                latest = latest with { PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw Missing("personnel_commands") };
                if (effects.Count != 0)
                {
                    var pending = latest.CampaignEffectRequests ?? throw Missing("effect_requests");
                    long revision = latest.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("effect_source_revision");
                    latest = latest with { CampaignEffectRequests = pending.Append(CampaignEffectSource.PersonAppearance, revision, effects) };
                }
                var arena = latest.ReadArmyLifecycle() ?? throw Missing("lifecycle");
                return latest.WithArmyLifecycle(arena with { Indexes = pool.ExportOwnedState() });
            }
            finally
            {
                if (notifications is not null) ClearRelease(notifications);
                if (excluded is not null) ClearRelease(excluded);
            }
        }

        internal void FillExcluded(HexListHandle output)
        {
            var descriptor = pool.AssociatedTaskHandle ?? throw Missing("associated_task_descriptor");
            var node = current.ReadAssociatedChainHead(new(descriptor.Identity)) ?? throw Missing("associated_task_head");
            var queries = new PersonReturnGovernorQueries(latest, package._governorInputs, current);
            while (node.Identity.HasValue)
            {
                var task = current.ReadAssociatedChainPayload(node) ?? throw Missing("associated_task_payload");
                int tag = queries.ReadCurrentAssociatedVirtual98(task) ?? throw Missing("associated_task_type");
                if (tag == 16)
                {
                    // The existing typed task projection owns valid pool value
                    // nodes as canonical IDs. Only its first member is consumed.
                    var members = current.ReadLinkedEntityMemberIds(task) ?? throw Missing("tag16_first_member_list");
                    if (members.Count == 0 || members[0] is not uint raw) throw Missing("tag16_first_member");
                    int signed = unchecked((int)raw);
                    Append(output, new(RegistryKind.Person, Lookup(signed).PhysicalSlot));
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("associated_task_next");
            }
        }

        private TransferPersonReference SelectRelated(int person)
        {
            var best = current.ReadPersonLookupReference(0) ?? throw Missing("related_default_lookup0");
            uint rank = uint.MaxValue;
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person) ?? throw Missing("person_registry");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("person_registry_head");
            while (node.Identity.HasValue)
            {
                var candidate = current.ReadRegistryNodePayload(node) ?? throw Missing("person_registry_payload");
                if (candidate.Kind != RegistryKind.Person) throw Missing("person_registry_kind");
                var other = new TransferPersonReference(candidate.PhysicalSlot);
                if (Relative(person, other, 0xBA)) return other;
                if (rank > 1 && Relative(person, other, 0xB4)) { best = other; rank = 1; }
                else if (rank > 2 && Relative(person, other, 0xB6)) { best = other; rank = 2; }
                else if (rank >= 3 && Status(Physical(other, "related_status_pointer")) == 1 && Sibling(person, other))
                {
                    if (rank != 3 || Birth(best) > Birth(other)) { best = other; rank = 3; }
                }
                else if (rank >= 4 && Status(Physical(other, "related_ruler_pointer")) == 1 &&
                    Relation(person, 0xB2) == Relation(Physical(other, "related_clan_pointer"), 0xB2))
                {
                    if (rank != 4 || Birth(best) > Birth(other)) { best = other; rank = 4; }
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("person_registry_next");
            }
            return best;
        }

        private bool CanJoin(TransferPersonReference related)
        {
            if (!Valid(related)) return false;
            int q = Physical(related, "related_person_pointer");
            if (Serving(q)) return true;
            if (!HasStatus(q, 6)) return false;
            return Known(current.ReadForceNativeValidity(Force(q)), "related_force_validity");
        }

        private bool Join(int person, TransferPersonReference related)
        {
            if (!Valid(related)) return false;
            int q = Physical(related, "join_related_pointer");
            if (!Serving(q) && !HasStatus(q, 6)) return false;
            if (HasStatus(q, 6) && !Known(current.ReadForceNativeValidity(Force(q)), "join_related_force_validity")) return false;
            var ruler = Ruler(Force(q));
            short year = (latest.NativeMapSeason ?? throw Missing("join_current_date")).ThirdDate.Year;
            SetStatus(person, 4);
            District(person, Known(current.ReadPersonDistrictSlot(q), "join_related_district"));
            SetParameter(OwnedArmyGovernorComparatorInputs.PersonWord166Key(person), Math.Clamp((int)year, 0, 350));
            InitialLoyalty(person, ruler);
            // These source byte writes must not erase E8 or unrelated merit channels.
            current.SetMoraleWavePersonPostE7(person, 73);
            Change(person, p => p with { Appointment = OfficerAppointment.Synchronize(73) });
            ChangeMerit(person, stage: 2);
            ChangeMerit(person, reset: 0);
            int input = Credit(2), upper = Credit(6);
            int selector = Known(BattleRemakeCurrentSettings.ReadSelectorE58(latest), "credit_selector");
            int option = Known(BattleRemakeCurrentSettings.ReadInt32(latest, selector == 0 ? 0x4C4 : 0x128), "credit_lower_option");
            int lower = Credit(option == 1 ? 2 : 1);
            int value = lower;
            if (input >= lower) value = input > upper ? upper : input;
            ChangeMerit(person, merit: unchecked((short)value));
            var merit = Owner(person).MeritState;
            Change(person, p => p with { MeritState = new(merit?.Merit, 0, merit?.RankStageId, 0, 0, merit?.ResetCode) });
            ushort destination = Known(current.ReadPersonFirstLocation(q), "join_destination_E2");
            if (Status(q) == 6 && Known(current.ReadForceNativeValidity(Force(q)), "join_late_related_force_validity"))
                destination = Known(current.ReadPersonFirstLocation(Physical(ruler, "saved_ruler_destination")), "saved_ruler_E2");
            var caller = Temporary();
            try
            {
                Append(caller, new(RegistryKind.Person, person));
                var target = BaseLookup.Find(destination, current);
                var moved = ArmyReturnCommanderHomeUpdater.ApplyPersonCurrent(person, target, current, pool,
                    new PersonReturnGovernorQueries(latest, package._governorInputs, current), RequireOwnedAssignmentReports);
                var packet = moved.Packet ?? throw Missing(moved.UnresolvedOwner ?? "person_home_transfer");
                if (packet.AssignmentPrecall is not null)
                {
                    latest = PersonAssignmentPrecallInputs.PublishCurrent(latest, current);
                }
                latest = ArmyReturnCommanderHomeUpdater.PublishPersonCurrent(latest, current, packet);
            }
            finally { ClearRelease(caller); }
            return true;
        }

        private bool Free(int person)
        {
            District(person, 0);
            SetStatus(person, 8);
            SetLoyalty(person, 0);
            bool maximum = Known(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "free_flags13");
            byte raw = Known(ArmyDestructionLoyalty.ReadCurrentRawE8(latest, current, person, package._governorInputs), "free_fresh_E8");
            SetLoyalty(person, maximum ? (byte)255 : raw);
            ushort id = Identity(person);
            if (id is >= 3001 and <= 3150 or >= 2001 and <= 2088 or >= 5101 and <= 5200) return true;
            ushort home = Known(current.ReadPersonFirstLocation(person), "free_old_E2");
            var old = BaseLookup.Find(home, current);
            var area = current.ReadAreaTableReference(0) ?? throw Missing("free_default_Area0");
            if (Known(current.ReadBaseNativeValidity(old), "free_old_Base_validity")) area = PersonFreedomHomeSource.FindBaseArea229FD0(current, old);
            int cityOrdinal = SelectCity(area);
            var city = current.ReadCityTableReference(cityOrdinal <= 51 ? cityOrdinal : 0) ?? throw Missing("free_City_pointer");
            if (!Known(current.ReadBaseNativeValidity(city), "free_City_validity")) { SetStatus(person, 7); return false; }
            ushort first = cityOrdinal is >= 1 and <= 51 ? (ushort)cityOrdinal : (ushort)0;
            current.WritePersonFirstLocation(person, first);
            Change(person, p => p with { LocationId = first == 0 ? null : first });
            int selectedArea = SelectArea(city);
            current.WritePersonSecondLocation(person, selectedArea is >= 1 and <= 500 ? (ushort)(selectedArea + 561) : (ushort)0);
            latest = OfficerLocationSourceAdapter.PublishPosition(latest, Owner(person).Id, current);
            return true;
        }

        private int SelectCity(AreaReference area)
        {
            if (Known(current.ReadAreaNativeValidity(area), "free_input_Area_validity"))
                return Known(current.ReadAreaCitySlot(area), "free_input_Area35");
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.City) ?? throw Missing("free_City_registry");
            int bound = Known(current.ReadTemporaryHexListCount(new(descriptor.Identity)), "free_City_registry_count");
            int selected = DrawApp32(bound);
            // Re-read descriptor/head after the draw. Repeats and invalid City
            // payloads are not filtered before their physical ordinal is used.
            descriptor = current.ReadRegistryDescriptor(RegistryKind.City) ?? throw Missing("free_fresh_City_registry");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("free_City_head");
            int index = 0;
            while (node.Identity.HasValue)
            {
                if (index == selected)
                {
                    var pointer = current.ReadRegistryNodePayload(node) ?? throw Missing("free_selected_City_payload");
                    if (pointer.Kind != RegistryKind.City) throw Missing("free_City_payload_kind");
                    return Known(pointer.PhysicalSlot, "free_City_physical");
                }
                index = unchecked(index + 1);
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("free_City_next");
            }
            return 0;
        }

        private int SelectArea(BaseReference city)
        {
            var list = Temporary();
            try
            {
                var descriptor = current.ReadRegistryDescriptor(RegistryKind.Area) ?? throw Missing("free_Area_registry");
                var node = current.ReadRegistryHead(descriptor) ?? throw Missing("free_Area_head");
                while (node.Identity.HasValue)
                {
                    var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("free_Area_payload");
                    if (payload.Kind != RegistryKind.Area) throw Missing("free_Area_kind");
                    var area = new AreaReference(payload.PhysicalSlot);
                    if (Known(current.ReadAreaNativeValidity(area), "free_Area_validity"))
                    {
                        byte originalCity = Known(current.ReadAreaValidityCode(area), "free_Area34");
                        int physicalCity = Known(city.PhysicalSlot, "free_current_City_ordinal");
                        if (originalCity == physicalCity) Append(list, payload);
                    }
                    node = current.ReadRegistryNodeNext(node) ?? throw Missing("free_Area_next");
                }
                if (!Head(list).Identity.HasValue) return 0;
                int selected = DrawApp32(Known(current.ReadTemporaryHexListCount(list), "free_Area_count"));
                var cursor = Head(list); int index = 0;
                while (cursor.Identity.HasValue)
                {
                    var payload = Payload(cursor, RegistryKind.Area);
                    if (Known(current.ReadAreaNativeValidity(new(payload.PhysicalSlot)), "free_fresh_Area_validity"))
                    {
                        if (index == selected) return Known(payload.PhysicalSlot, "free_Area_physical");
                        index = unchecked(index + 1);
                    }
                    cursor = Next(cursor);
                }
                return 0;
            }
            finally { ClearRelease(list); }
        }

        private void InitialLoyalty(int person, TransferPersonReference ruler)
        {
            if (!Valid(ruler)) return;
            int at = Physical(ruler, "loyalty_ruler");
            if (Status(at) != 1) return;
            if (!Known(current.ReadForceNativeValidity(Force(at)), "loyalty_ruler_force_validity")) return;
            var force = Force(at);
            byte value = CalculateLoyalty(person, force);
            bool maximum = Known(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "loyalty_flags13");
            SetLoyalty(person, maximum ? (byte)255 : value);
        }

        private byte CalculateLoyalty(int person, ForceReference force)
        {
            if (Status(person) == 1) return 250;
            var sovereign = Ruler(force);
            if (Sworn(person, sovereign) || Relative(person, sovereign, 0xBA)) return 150;
            if (RawHit(person, sovereign, like: true)) return 120;
            if (!Known(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(person)), "loyalty_flags14")) return 120;
            int distance = Math.Min(AffinityDistance(person, sovereign), 50);
            int s = Physical(sovereign, "loyalty_sovereign");
            int id = Identity(s);
            var abilities = new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language));
            int charm = Known(abilities.ResolveDimension(latest, id, 4,
                (f, effect) => package._baseCapacityInputs.ReadPanelModifier(latest, f, effect)), "loyalty_current_Charm4");
            int dislike = RawHit(person, sovereign, like: false) ? -15 : 0;
            int doctrine = Doctrine(person);
            int bonus = Relation(person, 0xB2) == Relation(s, 0xB2) ? 5 : 0;
            if (doctrine != 0 && doctrine == Doctrine(s)) bonus += 3;
            short rulerValue = unchecked((short)Known(RulerCaptureReleaseInputs.ReadCurrentWord(latest, s, 0x17A), "loyalty_sovereign17A"));
            short personValue = unchecked((short)Known(RulerCaptureReleaseInputs.ReadCurrentWord(latest, person, 0x17A), "loyalty_person17A"));
            //268D40 mode0: the separate17C penalty/divisor branch is not read.
            //The TC threshold is a proved loader-zero immutable rule. Its
            //formal reader preserves saved overrides, including older saves.
            int threshold = TalentRecruitParameters.Read(latest, "initial-loyalty-signed-threshold");
            if (personValue - rulerValue <= threshold) bonus += 3;
            return (byte)Math.Clamp(unchecked(bonus + (charm / 14 - distance / 2) + 100 + dislike), 0, 100);
        }

        private void Sort(HexListHandle list)
        {
            var cursor = Head(list);
            if (!cursor.Identity.HasValue) return;
            cursor = Next(cursor);
            while (cursor.Identity.HasValue)
            {
                var savedNext = Next(cursor);
                var previous = current.ReadHexListNodePrevious(cursor) ?? throw Missing("sort_previous");
                if (Before(cursor, previous))
                {
                    var insertion = previous;
                    var earlier = current.ReadHexListNodePrevious(previous) ?? throw Missing("sort_previous");
                    while (earlier.Identity.HasValue && Before(cursor, earlier))
                    { insertion = earlier; earlier = current.ReadHexListNodePrevious(earlier) ?? throw Missing("sort_previous"); }
                    var order = Nodes(list);
                    order.RemoveAll(n => n.Identity == cursor.Identity);
                    int index = order.FindIndex(n => n.Identity == insertion.Identity);
                    if (index < 0) throw Missing("sort_insertion_node");
                    order.Insert(index, cursor);
                    current.ReorderCurrentList(list, order);
                }
                cursor = savedNext;
            }
        }

        private bool Before(HexListNodeReference left, HexListNodeReference right)
        {
            int a = Known(Payload(left, RegistryKind.Person).PhysicalSlot, "sort_left_pointer");
            int b = Known(Payload(right, RegistryKind.Person).PhysicalSlot, "sort_right_pointer");
            for (int layer = 0; layer < 4; layer++)
            {
                string bText = NamePart(b, layer), aText = NamePart(a, layer);
                if (PersonNameOrder.Compare(aText, bText) < 0) return true;
                bText = NamePart(b, layer); aText = NamePart(a, layer);
                if (PersonNameOrder.Compare(aText, bText) > 0) return false;
            }
            ushort bId = Identity(b);
            return Identity(a) < bId;
        }

        private string NamePart(int physical, int layer)
        {
            ushort id = Identity(physical);
            var person = Owner(physical);
            bool special = id is >= 5101 and <= 5200 or >= 2062 and <= 2066;
            string? text = special ? layer is 0 or 2 ? person.CurrentNameSortKey : person.Name : layer switch
            { 0 => person.CurrentSurnameSortKey, 1 => person.CurrentSurname, 2 => person.CurrentGivenNameSortKey, _ => person.CurrentGivenName };
            return text ?? throw Missing("sort_current_name_part");
        }

        private bool Relative(int person, TransferPersonReference other, int field)
        {
            if (!Pair(person, other)) return false;
            ushort value = Relation(person, field);
            return value != 0 && value == Identity(Physical(other, "relative_pointer"));
        }
        private bool Sibling(int person, TransferPersonReference other)
        {
            if (!Pair(person, other)) return false;
            int p = Physical(other, "sibling_pointer");
            ushort father = Relation(person, 0xB4);
            if (father != 0 && father == Relation(p, 0xB4)) return true;
            ushort mother = Relation(person, 0xB6);
            return mother != 0 && mother == Relation(p, 0xB6);
        }
        private bool Sworn(int person, TransferPersonReference other)
        {
            if (!Pair(person, other)) return false;
            ushort value = Relation(person, 0xBC);
            return value != 0 && value == Relation(Physical(other, "sworn_pointer"), 0xBC);
        }
        private bool Pair(int person, TransferPersonReference other) =>
            Known(current.ReadPersonNativeValidity(person), "relation_P_validity") && Valid(other) &&
            Identity(person) != Identity(Physical(other, "relation_Q_pointer"));
        private bool RawHit(int person, TransferPersonReference other, bool like)
        {
            if (!Valid(other)) return false;
            ushort target = Identity(Physical(other, "raw_relation_target"));
            for (int index = 0; index < 8; index++)
            {
                ushort value = Known(like ? OwnedPersonRelationInputs.ReadCurrentLikeIdentity(latest, person, index)
                    : OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(latest, person, index), "raw_relation_slot");
                if (value == target) return true;
            }
            return false;
        }
        private int AffinityDistance(int person, TransferPersonReference other)
        {
            if (!Known(current.ReadPersonNativeValidity(person), "affinity_P_validity") || !Valid(other)) return 0;
            byte first = Known(OwnedPersonRelationInputs.ReadCurrentAffinity(latest, person), "affinity_P_BE");
            byte second = Known(OwnedPersonRelationInputs.ReadCurrentAffinity(latest, Physical(other, "affinity_Q_pointer")), "affinity_Q_BE");
            int distance = Math.Abs((int)first - second);
            return Math.Min(Math.Min(distance, 150 - distance), 75);
        }
        private int Doctrine(int person)
        {
            var owner = Owner(person);
            int value = owner.DoctrineId ?? owner.AbilityState?.ObservedDoctrineId ?? throw Missing("raw_doctrine14C");
            if ((uint)value > 255) throw Missing("raw_doctrine14C_width");
            return value;
        }

        private void District(int person, byte destination)
        {
            int before = associations.Count;
            PersonDistrictTransfer.Apply(person, destination, Transfer());
            if (associations.Count != before)
            {
                latest = PersonAssignmentPrecallInputs.PublishCurrent(latest, current);
            }
            // A nonzero join District is mirrored by the shared successful
            // home packet, at its actual later P.Force read. Do not introduce
            // that pointer query into240DB0's district0 return or early writes.
            if (destination == 0)
            {
                var owner = Owner(person);
                var location = current.ReadOfficerLocation(owner.Id) ?? throw Missing("published_membership");
                Change(person, p => p with { DistrictId = null, ForceId = null, LocationState = location });
            }
        }
        private static void RequireOwnedAssignmentReports(PersonAssignmentPrecallResult result)
        {
            // The shared business owner has already produced these actual
            // report operands. No adapter currently converts this family to
            // ordered semantic pending work; never silently discard it.
            if (result.Reports.Count != 0)
                throw Missing("assignment_report_" + result.Reports[0].NativeMessageCode.ToString("X4"));
        }
        private IPersonDistrictTransferState Transfer() => ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, associations, _monthlyAssignmentReports ?? RequireOwnedAssignmentReports);
        private void SetStatus(int person, byte status)
        {
            current.WritePersonStatus(person, status);
            var value = OfficerServiceStatusSourceAdapter.Decode(status);
            Change(person, p => p with { ServiceStatus = value, Role = OfficerServiceRules.PublishedRole(value, p.Role) });
        }
        private void SetLoyalty(int person, byte value)
        {
            current.SetPersonDestructionLoyaltyE8(person, value);
            latest = ArmyDestructionLoyalty.PublishCurrentRawE8(latest, current, person, value);
        }
        private void ChangeMerit(int person, int? stage = null, int? reset = null, int? merit = null)
        {
            var value = Owner(person).MeritState;
            Change(person, p => p with { MeritState = new(merit ?? value?.Merit, value?.PendingMerit,
                stage ?? value?.RankStageId, value?.FirstSelectionCount, value?.SecondSelectionCount, reset ?? value?.ResetCode) });
        }
        private void SetParameter(string key, int value)
        {
            var simulation = latest.NativeArmySimulation ?? throw Missing("saved_parameter_owner");
            latest = latest with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                { Int32Values = new(simulation.Parameters.Int32Values) { [key] = value } } } };
        }
        private int Credit(int slot)
        {
            var ranks = package._scenario.Snapshot.CreditRankParameters?.Ranks;
            if (ranks is null || slot >= ranks.Count) throw Missing("credit_rank_slot");
            return ranks[slot].SignedThreshold;
        }
        private int DrawApp32(int bound)
        {
            if (bound < 2) return 0;
            var state = latest.CampaignAppRandom32 ?? throw Missing("app32_draw_state");
            var random = new SharedSourceRandom32(state.State);
            int selected = random.Next(bound);
            latest = latest with { CampaignAppRandom32 = new(random.State) };
            return selected;
        }
        private TransferPersonReference Ruler(ForceReference force)
        {
            int physical = Known(force.PhysicalSlot, "ruler_force_pointer");
            ushort id = Known(current.ReadForceRecord(physical)?.RawRuler10, "ruler_ID10");
            return Lookup(id);
        }
        private TransferPersonReference Lookup(int id)
        {
            if (id < 0) throw Missing("negative_person_lookup");
            return (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0)) ?? throw Missing("person_lookup");
        }
        private OfficerState Owner(int physical)
        {
            ushort id = Identity(physical);
            if (current.ResolveOfficerSource(id) != physical || !latest.Officers.TryGetValue(id, out var person)) throw Missing("canonical_owned_person");
            return person;
        }
        private void Change(int physical, Func<OfficerState, OfficerState> update)
        {
            var person = Owner(physical);
            latest = latest with { Officers = new(latest.Officers) { [person.Id] = update(person) } };
        }
        private ushort Relation(int person, int field) => Known(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(latest, person, field), "relationship_word");
        private ushort Birth(TransferPersonReference person) => Owner(Physical(person, "birth_pointer")).BirthYear ?? throw Missing("birth_year_AC");
        private ushort AppearanceYear(int physical)
        {
            ushort id = Identity(physical);
            if (id != 0) return Owner(physical).AppearanceYear ?? throw Missing("appearance_year_AA");
            // The existing detached placeholder owner retains its independent
            // AA knowledge; no Officer is fabricated for an unowned record.
            if (latest.NativeArmySimulation?.Parameters.Int32Values is not { } values ||
                values.GetValueOrDefault(OwnedArmyLifecycleDetachedInputs.VersionKey) != 1 ||
                !values.TryGetValue(OwnedArmyLifecycleDetachedInputs.Key("Person", physical, "appearance-year"), out int year) ||
                year is < 0 or > 65535) throw Missing("placeholder_appearance_year_AA");
            return (ushort)year;
        }
        private byte Status(int person) => Known(current.ReadPersonStatus(person), "person_status_E6");
        private bool Serving(int person) => Known(current.ReadPersonNativeValidity(person), "serving_person_validity") && Status(person) is >= 1 and <= 4;
        private bool HasStatus(int person, byte status) => Status(person) == status;
        private bool Valid(TransferPersonReference person) => person.PhysicalSlot is int at && Known(current.ReadPersonNativeValidity(at), "person_validity");
        private ushort Identity(int physical) => Known(current.ReadPersonIdentity(physical), "person_ID10");
        private ForceReference Force(int person) => current.ReadPersonForce(person) ?? throw Missing("person_force");
        private static int Physical(TransferPersonReference reference, string field) => Known(reference.PhysicalSlot, field);

        private HexListHandle Temporary()
        {
            Enter();
            var value = current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
            Exit();
            if (!value.Identity.HasValue) throw Missing("temporary_allocation_profile");
            return value;
        }
        private void Append(HexListHandle list, RegistryObjectReference person)
        {
            Enter();
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, person);
            Exit();
        }
        private void ClearRelease(HexListHandle list)
        { Enter(); current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); Exit(); }
        private void Enter() { if (Known(current.ReadRegistryLockEnabled(), "list_lock_enabled")) current.EnterRegistryLock(); }
        private void Exit() { if (Known(current.ReadRegistryLockEnabled(), "list_unlock_enabled")) current.ExitRegistryLock(); }
        private HexListNodeReference Head(HexListHandle list) => current.ReadHexListHead(list) ?? throw Missing("temporary_head");
        private HexListNodeReference Next(HexListNodeReference node) => current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next");
        private RegistryObjectReference Payload(HexListNodeReference node, RegistryKind kind)
        {
            var value = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload");
            if (value.Kind != kind) throw Missing("temporary_payload_kind");
            return value;
        }
        private List<HexListNodeReference> Nodes(HexListHandle list)
        {
            var result = new List<HexListNodeReference>(); var node = Head(list);
            while (node.Identity.HasValue) { result.Add(node); node = Next(node); }
            return result;
        }
        private bool Contains(HexListHandle list, int person)
        {
            var node = Head(list);
            while (node.Identity.HasValue)
            {
                if (Payload(node, RegistryKind.Person).PhysicalSlot == person) return true;
                node = Next(node);
            }
            return false;
        }
        private void Observe(string point)
        {
            if (observe is null) return;
            var arena = latest.ReadArmyLifecycle() ?? throw Missing("observer_lifecycle");
            var view = latest.WithArmyLifecycle(arena with { Indexes = pool.ProjectOwnedRoles() });
            observe(point, GameStateCopy.Copy(view)!);
        }
        private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("person-appearance." + field);
    }
}
