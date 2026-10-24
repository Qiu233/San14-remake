using System.Globalization;
using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OfficerGrowthNotificationResult(GameState? State,
    int? KnownClearedNoticeCount, int RequestCount, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached30 growth-report manager. Requests retain source-time
    /// text; only the final successful tail clears the global notice knowledge.</summary>
    public OfficerGrowthNotificationResult TryAdvanceOfficerGrowthNotifications(
        GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.OfficerGrowthNotifications ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Growth notifications require actual stage30 entry.");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            //32EF60 precedes even an empty notice tree.
            var primary = ReadGrowthPlayerPrimaryDistrict(state, current);
            var clear = new List<OfficerState>();
            var entries = new List<(OfficerState Person, int Physical, OfficerAbilityGrowthNotice[]? Notices)>();
            long knownCount = 0;
            bool countKnown = true;
            foreach (var person in state.Officers.Values)
            {
                var notices = person.Growth?.Notifications;
                //An absent native tree entry has no Person/District/name read.
                if (notices is { Length: 0 }) continue;
                int physical = current.ResolveOfficerSource(person.Id)
                    ?? throw GrowthMissing("growth_person_receiver");
                if ((uint)physical >= 1851) throw GrowthMissing("growth_person_allocation");
                entries.Add((person, physical, notices));
            }
            //258E40 keys the tree by unsigned Person pointer;2D2650 walks it
            //in order. The ordinary1851-Person allocation is one contiguous
            //array, and its physical table is filled with successive elements
            //(TC320000.cpp4740..4776, stride0x1F0). Physical order is therefore
            //the pointer-key order, independently of IDs or registry dispatch.
            entries.Sort((first, second) => first.Physical.CompareTo(second.Physical));

            var requests = new List<CampaignEffectRequest>();
            var working = state;
            var formatter = new GrowthNoticeFormatter(this, state, current);
            foreach (var (person, physical, notices) in entries)
            {
                byte rawDistrict = GrowthKnown(current.ReadPersonDistrictSlot(physical), "growth_person_district");
                var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
                    ?? throw GrowthMissing("growth_person_district_pointer");
                if (district == primary) //Includes two observed null pointers.
                {
                    if (notices is null)
                        throw GrowthMissing(person.Growth is null ? "growth_owner_membership" : "growth_notices_membership");
                    foreach (var notice in notices)
                    {
                        //Keep the common caller prefix even for2E69/2E6A,
                        //whose bodies do not themselves display S0 or integers.
                        int sender = GrowthIdentity(state, current, physical, person.Id);
                        string label = formatter.ReadAbilityLabel(notice.AbilityIndex);
                        int after = notice.AfterAbility, before = notice.BeforeAbility;
                        if ((uint)after > byte.MaxValue || (uint)before > byte.MaxValue)
                            throw GrowthMissing("growth_notice_byte_domain");
                        int[] integers = [after, after - before, 0, 0, 0, 0];
                        int popupId = notice.UninterpretedCode is 17 or 18
                            ? notice.UninterpretedState == 0 ? 0x2E6A : 0x2E69 : 0x2E68;
                        string popup = formatter.Format(popupId, sender, label, integers);
                        string history = formatter.Format(0x2E6B, sender, label, integers);
                        if (notice.ReportContext == 0)
                        {
                            requests.Add(new(null, null) { OfficerGrowthFeedback = new(
                                OfficerGrowthFeedbackOperation.SoundCue, null, null) });
                            requests.Add(new(null, null) { OfficerGrowthFeedback = new(
                                OfficerGrowthFeedbackOperation.Narration, new(sender, popup), null) });
                        }
                        //All context values continue through fresh history inputs.
                        int officer = GrowthIdentity(state, current, physical, person.Id);
                        int day = StatusCampaignDay(state, moment);
                        ushort first = GrowthKnown(current.ReadPersonFirstLocation(physical), "growth_report_first_location");
                        var target = BaseLookup.Find(first, current);
                        byte raw = GrowthKnown(current.ReadPersonDistrictSlot(physical), "growth_report_raw_district");
                        int hex = ReadGrowthReportHex(current, target);
                        working = WriteHistory(working, current, new(0, raw, hex, day, history,
                            CampaignHistoryWriter.Supplemental(working), officer, MarkBit4: 1), requests);
                    }
                }
                //Unknown nonprimary notices are explicitly cleared at265E10,
                //without asking for their count, XP, byte values or names.
                clear.Add(person);
                if (notices is null) countKnown = false;
                else knownCount += notices.Length;
            }

            //265E10 is a global tail, including skipped nonprimary owners. No
            //domain object or pending prefix is replaced before every input succeeds.
            var pending = working.CampaignEffectRequests;
            if (requests.Count != 0)
                pending = (pending ?? throw GrowthMissing("growth_pending_owner"))
                    .Append(CampaignEffectSource.OfficerGrowthNotifications, moment.CalendarCycleOrdinal, requests);
            var officers = new Dictionary<int, OfficerState>(state.Officers);
            foreach (var person in clear)
                officers[person.Id] = person with { Growth = person.Growth is { } growth
                    ? growth.ClearNotifications() : new OfficerGrowthState(null, []) };
            return new(working with { Officers = officers, CampaignEffectRequests = pending },
                countKnown && knownCount <= int.MaxValue ? (int)knownCount : null, requests.Count, null);
        }
        catch (InvalidDataException error) { return new(null, null, 0, error.Message); }
        catch (DecoderFallbackException) { return new(null, null, 0, "growth_text_invalid_unicode"); }
    }

    private static DistrictReference ReadGrowthPlayerPrimaryDistrict(GameState state, SourceWorldContext current)
    {
        var force = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw GrowthMissing("growth_player_force_lookup");
        int identity = force.PhysicalSlot ?? throw GrowthMissing("growth_player_force_pointer");
        if (identity is >= 1 and <= 51)
        {
            ushort ruler = current.ReadForceRecord(identity)?.RawRuler10
                ?? throw GrowthMissing("growth_player_ruler");
            foreach (var entry in current.ReadArmyReturnRegisteredDistricts()
                ?? throw GrowthMissing("growth_primary_district_registry"))
            {
                int at = entry?.PhysicalSlot ?? throw GrowthMissing("growth_primary_district_pointer");
                if (GrowthKnown(current.ReadDistrictForceId(at), "growth_primary_district_force") != identity) continue;
                if (GrowthKnown(current.ReadDistrictLeader(at), "growth_primary_district_leader") == ruler ||
                    GrowthKnown(current.ReadDistrictNumber(at), "growth_primary_district_number") == 1)
                    return entry!;
            }
        }
        return current.ReadDistrictTableReference(0) ?? throw GrowthMissing("growth_primary_district_zero_lookup");
    }

    private static int ReadGrowthReportHex(SourceWorldContext current, BaseReference target)
    {
        var hex = CurrentGateMapQueries.FindHex(target, current);
        int ordinal = hex.PhysicalSlot ?? throw GrowthMissing("growth_report_null_hex_ordinal");
        if ((uint)ordinal >= 48400) throw GrowthMissing("growth_report_hex_ordinal");
        return ordinal;
    }

    private static int GrowthIdentity(GameState state, SourceWorldContext current, int physical, int ownerId)
    {
        int id = GrowthKnown(current.ReadPersonIdentity(physical), "growth_person_identity");
        if (id is < 1 or > 6000 || id != ownerId || current.ResolveOfficerSource(id) != physical ||
            !state.Officers.ContainsKey(id)) throw GrowthMissing("growth_person_canonical_owner");
        return id;
    }

    private static T GrowthKnown<T>(T? value, string field) where T : struct => value ?? throw GrowthMissing(field);
    private static InvalidDataException GrowthMissing(string field) => new("army.growth-notifications." + field);

    /// <summary>The four actual growth templates: literal, F3, S0, I0/I1 and
    /// return. Neither Deployment family execution nor application RNG is used.</summary>
    private sealed class GrowthNoticeFormatter(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current)
    {
        private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);
        private MessageDocument? data, game;

        internal string ReadAbilityLabel(int ability)
        {
            if ((uint)ability >= 5) return string.Empty;
            data ??= package._repository.LoadMessages(package.Seed.Language, MessageKind.Data).Messages;
            int index = 0x183 + ability;
            if (data.Sections.Count == 0 || (uint)index >= (uint)data.Sections[0].Records.Count)
                throw GrowthMissing("growth_text_label_lookup");
            string label = data.Sections[0].Records[index].Text ?? throw GrowthMissing("growth_text_label_unknown");
            ValidateCharacters(label);
            if (label.Length >= 126 || Encoding.UTF8.GetByteCount(label) >= 126)
                throw GrowthMissing("growth_text_label_capacity");
            return label;
        }

        internal string Format(int messageId, int sender, string label, int[] integers)
        {
            if (messageId is < 0x2E68 or > 0x2E6B || integers.Length != 6)
                throw GrowthMissing("growth_text_resource_profile");
            game ??= package._repository.LoadMessages(package.Seed.Language, MessageKind.Game).Messages;
            int section = messageId / 10000, index = messageId % 10000;
            if ((uint)section >= (uint)game.Sections.Count || (uint)index >= (uint)game.Sections[section].Records.Count)
                throw GrowthMissing("growth_text_message_lookup");
            byte[] bytes = game.Sections[section].Records[index].RawBytes;
            var result = new StringBuilder();
            int at = 0;
            while (at < bytes.Length)
            {
                if (Has(5, 5, 5))
                {
                    at += 3;
                    if (at != bytes.Length) throw GrowthMissing("growth_text_resource_profile");
                    string complete = result.ToString();
                    ValidateCharacters(complete);
                    //0x800 destination bytes, plus the actual fixed%s common
                    //workspace demand6*N+18. These are refusal bounds, not truncation.
                    if (complete.Length >= 1024) throw GrowthMissing("growth_text_output_capacity");
                    if (6 * (long)complete.Length + 18 > 0x800)
                        throw GrowthMissing("growth_text_format_workspace");
                    return complete;
                }
                if (Has(7, 7, 1))
                {
                    int start = at + 3, end = start;
                    while (end <= bytes.Length - 3 && !At(end, 7, 7, 2)) end += 2;
                    if (end > bytes.Length - 3) throw GrowthMissing("growth_text_literal_end");
                    string literal = StrictUtf16.GetString(bytes, start, end - start);
                    ValidateCharacters(literal);
                    result.Append(literal); at = end + 3; continue;
                }
                if (Has(2, 0x46, 0x33))
                {
                    //PersonArg0 was bound by the caller; F3 is queried only
                    //here, after its preceding label/byte/int preparation.
                    var abilities = new TacticRemakeCurrentAbilities(current, package._scenario,
                        package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
                    string name = new DeploymentNarrationCurrentPeople(state, current, abilities, sender, 0)
                        .PersonText(0, '3');
                    ValidateCharacters(name); result.Append(name); at += 3; continue;
                }
                if (at <= bytes.Length - 2 && bytes[at] == 2)
                {
                    string value = bytes[at + 1] switch
                    {
                        0x3C => label,
                        0x32 => integers[0].ToString(CultureInfo.InvariantCulture),
                        0x33 => integers[1].ToString(CultureInfo.InvariantCulture),
                        _ => throw GrowthMissing("growth_text_resource_profile")
                    };
                    result.Append(value); at += 2; continue;
                }
                throw GrowthMissing("growth_text_resource_profile");
            }
            throw GrowthMissing("growth_text_return");

            bool At(int offset, byte a, byte b, byte c) => offset <= bytes.Length - 3 &&
                bytes[offset] == a && bytes[offset + 1] == b && bytes[offset + 2] == c;
            bool Has(byte a, byte b, byte c) => At(at, a, b, c);
        }

        private static void ValidateCharacters(string text)
        {
            for (int index = 0; index < text.Length; index++)
            {
                char value = text[index];
                if (value == '\0') throw GrowthMissing("growth_text_embedded_nul");
                if (char.IsHighSurrogate(value))
                    throw GrowthMissing(index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
                        ? "growth_text_non_bmp" : "growth_text_invalid_unicode");
                if (char.IsLowSurrogate(value)) throw GrowthMissing("growth_text_invalid_unicode");
                if (char.IsControl(value)) throw GrowthMissing("growth_text_control_profile");
            }
        }
    }
}
