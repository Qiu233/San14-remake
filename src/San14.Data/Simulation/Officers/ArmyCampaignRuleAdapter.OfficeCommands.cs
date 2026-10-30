using San14.Core;

namespace San14.Data;

internal sealed record OfficeCommandPerson(int Id, int Physical, int CurrentOfficeId,
    bool CanAssign, bool CanRemove, string? DisabledReason)
{
    public OfficerAbilities? BaseAbilities { get; init; }
}
internal sealed record OfficeCommandSlot(int Id, int Grade, string Name, int CommandLimit,
    OfficerAbilities AbilityBonus, bool Enabled, string? DisabledReason)
{
    public int? CurrentHolderId { get; init; }
    public byte RawFlags3D { get; init; }
    public bool NativeValid { get; init; }
}
internal sealed record OfficeCommandCurrentRules(SourceWorldContext Current, int ForceId,
    IReadOnlyList<OfficeCommandSlot> Slots, IReadOnlyList<OfficeCommandPerson> People)
{
    public IReadOnlyDictionary<int, int> OfficeGrades { get; init; } = new Dictionary<int, int>();
    public IReadOnlyList<int> ForcePersonIds { get; init; } = [];
    public IReadOnlyList<OfficeCommandSlot> AllSlots { get; init; } = [];
    public byte ForceTitle48 { get; init; }
    public byte RankAC { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal const int OfficeNone = 0x49;

    internal OfficeCommandCurrentRules CaptureOfficeCommandCurrentRules(GameState state, int forceId)
    {
        if (forceId is < 1 or > 51) throw OfficeMissing("Force_identity");
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        _moraleWaveInputs.BindCurrent(state, current);
        var force = current.ReadForceTableReference(forceId) ?? throw OfficeMissing("Force_pointer");
        if (!OfficeNeed(current.ReadForceNativeValidity(force), "Force_validity")) throw OfficeMissing("Force_inactive");
        byte title = OfficeNeed(current.ReadForceKingdomNameIndex(forceId), "Force48");
        byte rank = OfficeNeed(current.ReadMoraleWaveForceRank47(forceId), "Force47");
        byte rankLimit = OfficeNeed(OwnedArmyMoraleWaveInputs.ReadRankAC(state, rank), "RankAC");
        var forcePeople = new List<int>();
        var people = new List<OfficeCommandPerson>();
        var holders = new Dictionary<int, int>();
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw OfficeMissing("Person_registry"))
        {
            int physical = entry ?? throw OfficeMissing("Person_registry_pointer");
            //2349D0(filter0): registered chain, raw Force and status1..4.
            if (OfficeNeed(current.ReadPersonOldForceSlot(physical), "Person_Force") != forceId) continue;
            byte status = OfficeNeed(current.ReadPersonStatus(physical), "PersonE6");
            if (status is < 1 or > 4) continue;
            int id = OfficeNeed(current.ReadPersonIdentity(physical), "Person10");
            if (!state.Officers.ContainsKey(id) || current.ResolveOfficerSource(id) != physical)
                throw OfficeMissing("Person_domain_binding");
            forcePeople.Add(id);
            int office = OfficeNeed(current.ReadMoraleWavePersonPostE7(physical), "PersonE7");
            if (office != OfficeNone) holders.TryAdd(office, id);
            if (status is 1 or 6 || DuelAftermathSpecialPerson(current, physical) ||
                OfficeSpecial(current, physical)) continue;
            bool disabled = OfficeBusy(current, physical) ||
                (OfficeNeed(current.ReadPersonTraitMask1BC(physical), "Person1BC") & 255) == 1;
            people.Add(new(id, physical, OfficeCurrentDraftPostCurrent(state, current, physical), !disabled, !disabled,
                disabled ? "officer-office.person_unavailable" : null)
                { BaseAbilities = state.Officers[id].BaseAbilities });
        }
        var slots = new List<OfficeCommandSlot>();
        var all = new List<OfficeCommandSlot>();
        var grades = new Dictionary<int, int>();
        //None is still a real grade operand of the change counters even if
        //its Post native-validity predicate rejects it as an assignable slot.
        grades.Add(OfficeNone, OfficeNeed(OwnedArmyMoraleWaveInputs.ReadPost37(state, OfficeNone), "None_Post37"));
        for (int post = 0; post < 256; post++)
        {
            bool nativeValid = OfficeNeed(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, post), "Post_validity");
            if (!nativeValid && post != OfficeNone) continue;
            int grade = OfficeNeed(OwnedArmyMoraleWaveInputs.ReadPost37(state, post), "Post37");
            grades[post] = grade;
            byte flags = OfficeByte(state, OwnedArmyGovernorComparatorInputs.OfficeFlags3DKey(post));
            if (!state.OfficerOffices.TryGetValue(post, out var row)) throw OfficeMissing("Post_definition_" + post);
            var bonus = row.AbilityBonus;
            int? holder = holders.TryGetValue(post, out int held) ? held : null;
            bool enabled = holder is not int person || !OfficeBusy(current,
                current.ResolveOfficerSource(person) ?? throw OfficeMissing("Post_holder_pointer"));
            var slot = new OfficeCommandSlot(post, grade, row.Name,
                OfficeNeed(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, post), "Post_capacity"),
                new(OfficeNeed(OwnedArmyMoraleWaveInputs.ReadPost38(state, current, post), "Post_leadership"),
                    bonus.Strength, bonus.Intelligence, bonus.Politics, 0), enabled,
                enabled ? null : "officer-office.holder_unavailable")
                { CurrentHolderId = holder, RawFlags3D = flags, NativeValid = nativeValid };
            all.Add(slot);
            bool allowed = title switch
            {
                6 => (flags & 1) != 0, 44 => (flags & 2) != 0, 45 => (flags & 4) != 0,
                _ => (flags & 7) == 0 && rankLimit - 1 < grade
            };
            if (nativeValid && post != OfficeNone && allowed) slots.Add(slot);
        }
        return new(current, forceId, slots, people)
        {
            OfficeGrades = grades, ForcePersonIds = forcePeople, AllSlots = all,
            ForceTitle48 = title, RankAC = rankLimit
        };
    }

    internal CommandResult ApplyOfficerOffices(GameState state, ForceState force,
        IReadOnlyList<OfficerOfficeAssignment> assignments, CommandContext context)
    {
        try
        {
            var rules = CaptureOfficeCommandCurrentRules(state, force.Id);
            var current = rules.Current;
            var original = new Dictionary<int, int>();
            foreach (int id in rules.ForcePersonIds)
            {
                int person = current.ResolveOfficerSource(id) ?? throw OfficeMissing("draft_Person_pointer");
                original.TryAdd(id, OfficeNeed(current.ReadMoraleWavePersonPostE7(person), "draft_PersonE7"));
            }
            var draft = new Dictionary<int, int>(original);
            var people = rules.People.ToDictionary(p => p.Id);
            var slots = rules.Slots.ToDictionary(p => p.Id);
            foreach (var assignment in assignments)
            {
                int post = assignment.OfficeId;
                int? incoming = assignment.OfficerId;
                int? holder = post == OfficeNone ? null : rules.ForcePersonIds.Cast<int?>()
                    .FirstOrDefault(id => draft[id!.Value] == post);
                if (holder == incoming && post != OfficeNone) continue;
                if (incoming is int selected && draft.TryGetValue(selected, out int selectedPost) && selectedPost == post) continue;
                //A former office outside the force's current appointment menu
                //can still be dismissed by the native per-person clear action.
                //It cannot receive a new appointee through this exception.
                if (post != OfficeNone && !slots.ContainsKey(post) && (incoming is not null || holder is null))
                    return CommandResult.Reject("The current force cannot assign this office.", "officer-office.post_ineligible");
                if (incoming is int id && (!people.ContainsKey(id) || !draft.ContainsKey(id)))
                    return CommandResult.Reject("The officer is not in the current appointment list.", "officer-office.person_ineligible");
                if (post != OfficeNone && slots.TryGetValue(post, out var slot) && !slot.Enabled)
                    return CommandResult.Reject("The current office holder cannot be changed.", "officer-office.holder_unavailable");
                if (incoming is int personId && !people[personId].CanAssign)
                    return CommandResult.Reject("The officer cannot be reassigned now.", "officer-office.person_unavailable");
                if (holder is int displaced)
                {
                    if (!people.TryGetValue(displaced, out var old) || !old.CanRemove)
                        return CommandResult.Reject("The current holder cannot be removed.", "officer-office.holder_unavailable");
                    draft[displaced] = OfficeNone;
                }
                if (incoming is int appointee) draft[appointee] = post;
            }
            //1F44E0 retains only changed Person entries.1EE720/1F18E0 walk
            //that pointer-key map in order; the ordinary Person allocation is
            //one contiguous array, independently of input and registry order.
            var changes = draft.Where(pair => pair.Value != original[pair.Key])
                .Select(pair => (Id: pair.Key, Physical: current.ResolveOfficerSource(pair.Key)
                    ?? throw OfficeMissing("changed_Person_pointer"), Post: pair.Value))
                .OrderBy(pair => pair.Physical).ToArray();
            if (changes.Length == 0) return CommandResult.Accept(state);
            var before = new Dictionary<int, byte>();
            foreach (var row in changes)
                before.Add(row.Id, OfficeNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, row.Physical,
                    _governorInputs), "changed_before_E8"));
            var working = state;
            foreach (var row in changes)
            {
                int oldPost = OfficeNeed(current.ReadMoraleWavePersonPostE7(row.Physical), "apply_old_PostE7");
                if (!OfficeNeed(current.ReadPersonNativeValidity(row.Physical), "apply_Person_validity")) continue;
                if (!OfficeNeed(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(working, row.Post), "apply_new_Post_validity")) continue;
                if (!OfficeNeed(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(working, oldPost), "apply_old_Post_validity")) continue;
                var officer = working.Officers[row.Id];
                current.SetMoraleWavePersonPostE7(row.Physical, checked((byte)row.Post));
                working = working with { Officers = new(working.Officers)
                    { [row.Id] = officer with { Appointment = OfficerAppointment.Synchronize(row.Post) } } };
                int delta = OfficeLoyaltyDeltaCurrent(working, current, row.Physical, row.Post, oldPost);
                if (delta == 0) continue;
                byte raw = OfficeNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(working, current, row.Physical, _governorInputs), "apply_E8");
                int after;
                if (delta < 0)
                {
                    int sign = delta >> 31;
                    int magnitude = unchecked((delta ^ sign) - sign);
                    after = Math.Clamp(unchecked(raw - (byte)magnitude), 0, 255);
                }
                else
                {
                    int amount = Math.Min(delta, Math.Max(0, 100 - raw));
                    after = Math.Min(255, unchecked(raw + (byte)amount));
                }
                if (OfficeNeed(current.ReadPersonBehavior(row.Physical)?.PreservesMaximumLoyalty, "Person15E_bit13")) after = 255;
                current.SetPersonDestructionLoyaltyE8(row.Physical, (byte)after);
                working = ArmyDestructionLoyalty.PublishCurrentRawE8(working, current, row.Physical, (byte)after);
            }
            bool decreased = false;
            foreach (var row in changes)
            {
                byte after = OfficeNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(working, current, row.Physical, _governorInputs), "changed_after_E8");
                byte prior = before[row.Id];
                if (after < prior && (after < 100 || prior < 100)) { decreased = true; break; }
            }
            var requests = new List<CampaignEffectRequest>();
            var normal = FormatOfficeCommandReportCurrent(working, current, 0x597C, force.Id);
            working = normal.State;
            OfficeSound(requests, 8);
            OfficeAnnouncement(requests, normal.Text);
            if (decreased)
            {
                var warning = FormatOfficeCommandReportCurrent(working, current, 0x597D, force.Id);
                working = warning.State;
                OfficeSound(requests, 6);
                OfficeAnnouncement(requests, warning.Text);
            }
            var pending = working.CampaignEffectRequests ?? throw OfficeMissing("pending_owner");
            long cycle = working.NativeMapSeason?.AdvancedCalendarSteps ?? throw OfficeMissing("calendar_owner");
            return CommandResult.Accept(working with { CampaignEffectRequests =
                pending.Append(CampaignEffectSource.OfficerOfficeCommands, cycle, requests) });
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    internal int OfficeLoyaltyDeltaCurrent(GameState state, SourceWorldContext current, int person, int next, int old)
    {
        //1EDB50, after the actual PostE7 write, still receives both Post pointers.
        if (next == old) return 0;
        var force = current.ReadPersonForce(person) ?? throw OfficeMissing("loyalty_Force_pointer");
        int rulerId = TalentRulerCurrent(current, force);
        int ruler = current.ReadPersonLookupReference(rulerId)?.PhysicalSlot ?? throw OfficeMissing("loyalty_ruler_pointer");
        if (OfficeRelatedCurrent(state, current, ruler, person) && !OfficeRelationList(state, current, ruler, person, 0xD0)) return 0;
        if (OfficeRelationList(state, current, person, ruler, 0xC0)) return 0;
        int first = OfficeNeed(OwnedArmyMoraleWaveInputs.ReadPost37(state, old), "loyalty_old_Post37");
        int second = OfficeNeed(OwnedArmyMoraleWaveInputs.ReadPost37(state, next), "loyalty_new_Post37");
        var bank = state.NativeArmySimulation?.Parameters ?? throw OfficeMissing("loyalty_parameter_bank");
        const string key = PersonnelCommandParameters.OfficeLoyaltyPerGrade;
        if (bank.Float32Values.ContainsKey(key)) throw OfficeMissing("loyalty_parameter_type");
        int multiplier;
        if (!bank.Int32Values.TryGetValue(key, out multiplier))
        {
            //TC1A495C0 is a file-backed immutable i32 with one read at1EDC04.
            if (bank.Language != "TC" || bank.RuleVersion != SimulationRuleIdentity.ForLocale("TC"))
                throw OfficeMissing("loyalty_multiplier");
            multiplier = 2;
        }
        return unchecked((first - second) * multiplier);
    }

    internal static int OfficeCurrentDraftPostCurrent(GameState state, SourceWorldContext current, int physical)
    {
        //1EE270 returns the current/draft Post if native-valid, otherwise the
        //actual None object at table slot73. The formal writer retains rawE7.
        int post = OfficeNeed(current.ReadMoraleWavePersonPostE7(physical), "draft_PostE7");
        return OfficeNeed(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, post), "draft_Post_validity")
            ? post : OfficeNone;
    }

    private static void OfficeSound(List<CampaignEffectRequest> requests, int sound) => requests.Add(new(null, null)
        { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, sound, null, null, null, null) });
    private static void OfficeAnnouncement(List<CampaignEffectRequest> requests, string text) => requests.Add(new(null, null)
        { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text) });

    private static ushort OfficeWord(GameState state, int person, int field)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw OfficeMissing("relation_bank");
        string key = OwnedPersonRelationInputs.PersonFieldKey(person, field);
        if (bank.Int32Values.GetValueOrDefault(OwnedPersonRelationInputs.VersionKey) != 1 ||
            bank.Float32Values.ContainsKey(key) || !bank.Int32Values.TryGetValue(key, out int value) || (uint)value > 65535)
            throw OfficeMissing("relation_" + field.ToString("X"));
        return (ushort)value;
    }
    private static bool OfficeRelationList(GameState state, SourceWorldContext current, int person, int other, int field)
    {
        if (!OfficeNeed(current.ReadPersonNativeValidity(other), "relation_target_validity")) return false;
        int id = OfficeNeed(current.ReadPersonIdentity(other), "relation_target_identity");
        for (int i = 0; i < 8; i++) if (OfficeWord(state, person, field + i * 2) == id) return true;
        return false;
    }
    private static bool OfficeRelatedCurrent(GameState state, SourceWorldContext current, int a, int b)
    {
        if (!OfficeNeed(current.ReadPersonNativeValidity(a), "relation_first_validity") ||
            !OfficeNeed(current.ReadPersonNativeValidity(b), "relation_second_validity")) return false;
        bool Serves(int p) => OfficeNeed(current.ReadPersonStatus(p), "relation_status") is >= 1 and <= 4;
        if (Serves(a) && Serves(b) && OfficeNeed(current.ReadPersonOldForceSlot(a), "relation_first_Force") !=
            OfficeNeed(current.ReadPersonOldForceSlot(b), "relation_second_Force")) return false;
        int Id(int p) => OfficeNeed(current.ReadPersonIdentity(p), "relation_identity");
        bool Kin(int first, int second, int field)
        {
            if (Id(first) == Id(second)) return false;
            int value = OfficeWord(state, first, field);
            return value != 0 && value == (field == 0xBC ? OfficeWord(state, second, field) : Id(second));
        }
        if (Kin(a, b, 0xB4) || Kin(a, b, 0xB6) || Kin(b, a, 0xB4) || Kin(b, a, 0xB6) || Kin(a, b, 0xBA)) return true;
        if (Id(a) != Id(b) && (OfficeWord(state, a, 0xB4) != 0 && OfficeWord(state, a, 0xB4) == OfficeWord(state, b, 0xB4) ||
            OfficeWord(state, a, 0xB6) != 0 && OfficeWord(state, a, 0xB6) == OfficeWord(state, b, 0xB6))) return true;
        if (Kin(a, b, 0xBC)) return true;
        return OfficeWord(state, a, 0xB2) != 0 && OfficeWord(state, a, 0xB2) == OfficeWord(state, b, 0xB2);
    }

    private static byte OfficeByte(GameState state, string key)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw OfficeMissing("parameter_bank");
        if (bank.Float32Values.ContainsKey(key) || !bank.Int32Values.TryGetValue(key, out int value) || (uint)value > 255)
            throw OfficeMissing("parameter_" + key);
        return (byte)value;
    }
    private static bool OfficeSpecial(SourceWorldContext current, int person) =>
        OfficeNeed(current.ReadPersonBehavior(person)?.UsesSpecialPersonRules, "Person15E_bit14");
    private static bool OfficeBusy(SourceWorldContext current, int person) => PersonCurrentBusySource.Read(current, person, OfficeMissing);
    private static T OfficeNeed<T>(T? value, string field) where T : struct => value ?? throw OfficeMissing(field);
    private static InvalidDataException OfficeMissing(string field) => new("officer-office." + field);
}
