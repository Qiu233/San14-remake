using San14.Core;

namespace San14.Data;

/// <summary>Actual ordinary2704F0 two-way Relations13 mutation. Directional
/// hostilityEA is a separate owner. Each call retains its own level notice.</summary>
internal static partial class ForceRelationChangeCurrent
{
    /// <summary>Original notify/fifth0 contract used by monthly expiry.
    /// Every numerical store publishes to the caller's SAME candidate before
    /// subsequent report/policy/special-tail reads, including a late failure.</summary>
    internal static int ApplyMonthly(Func<GameState> latest, Action<GameState> publish,
        SourceWorldContext current, ForceReference first, ForceReference second, int adjustment, int notify,
        Func<string, int> parameter, Action<int> queue,
        Action<int, ForceReference, ForceReference, bool> narration,
        Func<ForceReference, (bool Found, int Value)> policy,
        Action<int, ForceReference> addedPerson, ApplicationGameplayContext? application = null,
        Action<HexListHandle>? removedPeople = null)
    {
        bool Valid(ForceReference f) => Known(current.ReadForceNativeValidity(f), "monthly_force_validity");
        int Id(ForceReference f) => Known(current.ReadForcePhysicalSlot(f), "monthly_force_identity");
        bool Player(ForceReference f)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(latest(), current) ?? throw Missing("monthly_player");
            int physical = Id(player);
            return physical != 0 && physical == Id(f);
        }
        byte Read(ForceReference f, int other)
        {
            if (other >= 52) return 0;
            var values = latest().NativeArmySimulation?.Forces.GetValueOrDefault(
                Known(f.PhysicalSlot, "monthly_force_pointer"))?.NormalAftermath?.Relations13;
            return values is { Length: 52 } ? values[other] : throw Missing("monthly_relation_values");
        }
        int Level()
        {
            int value = Read(first, Id(second));
            for (int i = 1; i <= 5; i++)
                if (value < parameter(SimulationParameterKeys.ForceRelations.LevelThreshold(i))) return i - 1;
            return 5;
        }
        void Store(ForceReference own, ForceReference other)
        {
            int index = Id(other);
            if (index >= 52) return;
            var state = latest();
            var simulation = state.NativeArmySimulation ?? throw Missing("monthly_relation_state");
            int physical = Known(own.PhysicalSlot, "monthly_relation_pointer");
            var force = simulation.Forces.GetValueOrDefault(physical) ?? throw Missing("monthly_relation_force");
            var fields = force.NormalAftermath ?? throw Missing("monthly_relation_fields");
            var values = fields.Relations13 is { Length: 52 } old ? old.ToArray() : throw Missing("monthly_relation_values");
            values[index] = (byte)Math.Clamp(unchecked(values[index] + adjustment), 0, 200);
            current.SetRulerReleaseRelation13(physical, index, values[index]);
            publish(state with { NativeArmySimulation = simulation with { Forces = new(simulation.Forces)
                { [physical] = force with { NormalAftermath = fields with { Relations13 = values } } } } });
        }
        if (!Valid(first) || !Valid(second)) return 0;
        if (Id(first) == Id(second)) return 0;
        var mode = current.TransferModeStateReader is { } read ? read() : current.ArmyReturnTransferMode;
        if (mode is null) throw Missing("monthly_transfer_mode_owner");
        if (mode.Present && Known(mode.Value, "monthly_mode_value") != 0) notify = 0;
        int before = Level();
        Store(first, second); Store(second, first);
        int after = Level();
        if (before == after)
        {
            if ((Player(first) || Player(second)) && notify != 0)
            {
                queue(8);
                if (adjustment == 0) return 255;
                int root = 0x305B;
                if (adjustment > 0)
                {
                    var a = policy(first); var b = policy(second);
                    root = a.Found || b.Found ? 0x305D : 0x305C;
                }
                narration(root, first, second, false);
            }
            return 255;
        }
        if ((Player(first) || Player(second)) && notify != 0)
        {
            //Changed level formats BEFORE its queue8, unlike equal level.
            narration(0x3055 + after, first, second, true);
        }
        int secondId = Id(second);
        if (secondId is >= 46 and <= 50)
            ApplyMonthlySpecial(latest, current, first, second, before, after, parameter, addedPerson, application, removedPeople: removedPeople);
        else if (Id(first) is >= 46 and <= 50)
            ApplyMonthlySpecial(latest, current, second, first, before, after, parameter, addedPerson, application, removedPeople: removedPeople);
        return after;
    }

    //Actual27B0F0 caller ABI: ordinary receiver, special receiver, old/new
    //levels and fifth0. All relation stores/reports already happened above.
    internal static void ApplyInitialSpecialPersonnel(Func<GameState> latest, SourceWorldContext current,
        ForceReference ordinary, ForceReference special, int level, Func<string, int> parameter,
        Action<int, ForceReference> addedPerson, ApplicationGameplayContext? application,
        Action<int> addedNotice, Action<HexListHandle>? removedPeople = null)
        => ApplyMonthlySpecial(latest, current, ordinary, special, 3, level, parameter,
            addedPerson, application, addedNotice, removedPeople);

    private static void ApplyMonthlySpecial(Func<GameState> latest, SourceWorldContext current,
        ForceReference ordinary, ForceReference special, int before, int after, Func<string, int> parameter,
        Action<int, ForceReference> addedPerson, ApplicationGameplayContext? application,
        Action<int>? addedNotice = null, Action<HexListHandle>? removedPeople = null)
    {
        if (!Known(current.ReadForceNativeValidity(ordinary), "monthly_special_first_validity") ||
            !Known(current.ReadForceNativeValidity(special), "monthly_special_second_validity")) return;
        int identity = Known(current.ReadForcePhysicalSlot(special), "monthly_special_identity");
        if (identity is < 46 or > 50 || before == after) return;
        var state = latest();
        var world = state.CampaignEventWorld ?? throw Missing("monthly_special_current_World454");
        var season = state.NativeMapSeason ?? throw Missing("monthly_special_current_calendar");
        int time = unchecked(world.ElapsedPeriodOffset + (int)season.AdvancedPeriods);
        if (time < parameter(SimulationParameterKeys.ForceRelations.SpecialPersonnelStartPeriod)) return;

        int row = Known(current.ReadForcePhysicalSlot(special), "monthly_special_group_identity") - 46;
        int selected = Known(current.ReadForcePhysicalSlot(special), "monthly_special_eligibility_identity");
        var eligibility = PersonReturnPathSource.ReadSpecialPersonnelEligibility(latest(), current, ordinary, selected, application);
        if (eligibility.Value is not bool eligible) throw Missing(eligibility.UnresolvedOwner ?? "monthly_special_2C6550");
        if (!eligible) return;
        if ((uint)row >= 5) throw Missing("monthly_special_group_pointer");

        var additions = current.CreateTemporaryHexList() ?? throw Missing("monthly_special_add_descriptor");
        try
        {
            var removals = current.CreateTemporaryHexList() ?? throw Missing("monthly_special_remove_descriptor");
            try
            {
                int[] pair = row switch
                {
                    0 => [4111, 4115], 1 => [4131, 4135], 2 => [4151, 4155],
                    3 => [4171, 4175], _ => [4191, 4195]
                };
                int[] triple = row switch
                {
                    0 => [4113, 4117, 4118], 1 => [4133, 4137, 4138], 2 => [4153, 4157, 4158],
                    3 => [4173, 4177, 4178], _ => [4193, 4197, 4198]
                };
                if (after == 5)
                {
                    if (PermitPlayerEvent(280 + row * 2)) Collect(triple, additions, adding: true);
                    if (before < 4 && PermitPlayerEvent(279 + row * 2)) Collect(pair, additions, adding: true);
                }
                else if (after == 4)
                {
                    if (before < 5)
                    {
                        if (before < 4 && PermitPlayerEvent(279 + row * 2)) Collect(pair, additions, adding: true);
                    }
                    else Collect(triple, removals, adding: false);
                }
                else if (before >= 4)
                {
                    if (before == 5) Collect(triple, removals, adding: false);
                    Collect(pair, removals, adding: false);
                }

                if (Known(current.ReadTemporaryHexListCount(additions), "monthly_special_add_count") != 0)
                {
                    var node = current.ReadRegistryHead(new(additions.Identity)) ?? throw Missing("monthly_special_add_head");
                    while (node.Identity.HasValue)
                    {
                        var person = current.ReadRegistryNodePayload(node) ?? throw Missing("monthly_special_add_payload");
                        if (person.Kind != RegistryKind.Person) throw new InvalidDataException("Special personnel list contains another payload kind.");
                        if (Known(current.ReadRegistryObjectNativeValidity(person), "monthly_special_added_person_validity"))
                            addedPerson(Known(person.PhysicalSlot, "monthly_special_added_person"), ordinary);
                        node = current.ReadRegistryNodeNext(node) ?? throw Missing("monthly_special_add_next");
                    }
                    if (Player())
                    {
                        if (addedNotice is null) throw Missing("monthly_special_personnel_report_324C");
                        addedNotice(Known(current.ReadTemporaryHexListCount(additions), "monthly_special_added_count"));
                    }
                }
                if (Known(current.ReadTemporaryHexListCount(removals), "monthly_special_remove_count") != 0)
                {
                    if (removedPeople is null) throw Missing("monthly_special_personnel_2CD1C0_flags1_1_0_consumer");
                    removedPeople(removals);
                }
            }
            finally { current.ClearTemporaryHexList(removals); current.ReleaseTemporaryHexList(removals); }
        }
        finally { current.ClearTemporaryHexList(additions); current.ReleaseTemporaryHexList(additions); }

        bool Player()
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(latest(), current) ?? throw Missing("monthly_special_player_pointer");
            int physical = Known(current.ReadForcePhysicalSlot(player), "monthly_special_player_identity");
            return physical != 0 && Known(current.ReadForcePhysicalSlot(ordinary), "monthly_special_first_identity") == physical;
        }
        bool PermitPlayerEvent(int key)
        {
            if (!Player()) return true;
            if (!Player()) return false; //The source repeats this current predicate.
            var history = latest().CampaignEvents ?? throw Missing("monthly_special_current_event_history");
            return (uint)key < history.Occurred.Length ? history.Occurred[key]
                : throw Missing("monthly_special_event_index");
        }
        void Collect(int[] ids, HexListHandle list, bool adding)
        {
            foreach (int id in ids)
            {
                var person = current.ReadPersonLookupReference(id) ?? throw Missing("monthly_special_person_lookup");
                if (person.PhysicalSlot is not int physical) continue; //Known null lookup is natively invalid.
                if (!Known(current.ReadPersonNativeValidity(physical), "monthly_special_person_validity")) continue;
                var force = current.ReadPersonForce(physical) ?? throw Missing("monthly_special_person_Force");
                bool valid = Known(current.ReadForceNativeValidity(force), "monthly_special_person_Force_validity");
                if (adding)
                {
                    if (valid || Known(current.ReadPersonStatus(physical), "monthly_special_person_status") != 7) continue;
                }
                else
                {
                    if (!valid) continue;
                    var freshForce = current.ReadPersonForce(physical) ?? throw Missing("monthly_special_fresh_person_Force");
                    if (freshForce != ordinary) continue; //Pointer identity, not a Force-ID lookup.
                }
                var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("monthly_special_person_node");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, physical));
            }
        }
    }
}
