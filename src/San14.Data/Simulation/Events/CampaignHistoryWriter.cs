using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal sealed record CampaignHistoryWriteResult(GameState State,
    CampaignHistoryNotification? Written, bool NeedsNotification);

/// <summary>The source-time persistent writer. The same ReportPool core serves
/// its raw interoperability caller; this adapter owns named immutable fields.</summary>
internal sealed class CampaignHistoryWriter(GameResourceRepository repository, LoadedScenario scenario, string language)
{
    private LoadedArmyFlagPaneNames? names;

    internal CampaignHistoryWriteResult Write(GameState state, SourceWorldContext current, ReportCommand command)
    {
        var adapter = new Current(state, current, force => ShortName(state, current, force));
        var written = ReportPool.WriteRecord(command, adapter);
        CampaignHistoryNotification? notification = written?.PhysicalSlot is int slot
            ? new(checked((ushort)slot), adapter.History.Records[slot]) : null;
        bool nonempty = command.Text is { Length: > 0 } text && text[0] != '\0';
        return new(written is null ? state : state with { CampaignHistory = adapter.History },
            notification, notification is not null && nonempty);
    }

    //A pointer operand is prepared by the caller, but its text is dereferenced
    //only after the writer recording gate and slot. Preserve unknown until then.
    internal static string? Supplemental(GameState state) => state.CampaignHistory?.DefaultSupplementalText;

    internal string ShortName(GameState state, SourceWorldContext current, ForceReference force)
    {
        if (!K(current.ReadForceNativeValidity(force), "short_name_Force_validity")) return "";
        int physical = K(force.PhysicalSlot, "short_name_Force_pointer");
        byte raw = K(current.ReadForceKingdomNameIndex(physical), "short_name_Force48");
        int selected = raw <= 100 ? raw : 0;
        names ??= ArmyFlagPaneNames.Load(scenario);
        var definition = names.CustomNames.Definitions[selected];
        string text;
        if (definition.NativeValidity) text = definition.ShortName;
        else text = FallbackName(state, current, force, (owner, full) =>
            full ? owner.Name ?? throw Missing("fallback_current_Name") :
                owner.CurrentSurname ?? throw Missing("fallback_current_Surname"));
        text = Head(text);
        //2AD190 zeroes two units, then30DED0 copies one. Return only that
        //nonzero unit; the shared2-unit record copy preserves its real tail.
        return text.Length == 0 ? "" : text[..1];
    }

    //The full fallback and the custom-first command operand are distinct
    //source functions. Share Force selection while letting each actual
    //caller supply its existing current Person-name knowledge projection.
    internal string CustomFirstName(GameState state, SourceWorldContext current, ForceReference force,
        Func<OfficerState, bool, string> personName)
    {
        int physical = K(force.PhysicalSlot, "command_Force_pointer");
        byte raw = K(current.ReadForceKingdomNameIndex(physical), "command_Force48");
        names ??= ArmyFlagPaneNames.Load(scenario);
        var definition = names.CustomNames.Definitions[raw <= 100 ? raw : 0];
        return definition.NativeValidity ? Head(definition.ShortName) : FallbackName(state, current, force, personName);
    }

    internal string FallbackName(GameState state, SourceWorldContext current, ForceReference force,
        Func<OfficerState, bool, string> personName, Formats.MessageDocument? admittedData = null)
    {
            //22EE60 repeats its own validity and physical reads after the
            //custom lookup. Its default Force0 uses actual lookup5014.
            if (!K(current.ReadForceNativeValidity(force), "fallback_Force_validity")) return "";
            int at = K(current.ReadForcePhysicalSlot(force), "fallback_Force_physical");
            ushort rawPerson = at == 0 ? (ushort)5014 :
                K(current.ReadForceRecord(at)?.RawRuler10, "fallback_Force_ruler");
            var person = (rawPerson <= 6000 ? current.ReadPersonLookupReference(rawPerson) :
                current.ReadTransferPersonTableReference(0)) ?? throw Missing("fallback_Person_lookup");
            int p = K(person.PhysicalSlot, "fallback_Person_pointer");
            if (!K(current.ReadPersonNativeValidity(p), "fallback_Person_validity")) return "";
            int id = K(current.ReadPersonIdentity(p), "fallback_Person_identity");
            var owner = state.Officers.GetValueOrDefault(id) ?? throw Missing("fallback_current_Person");
            if (current.ResolveOfficerSource(id) != p) throw Missing("fallback_current_Person_binding");
            at = K(current.ReadForcePhysicalSlot(force), "fallback_fresh_Force_physical");
            bool special = at is >= 46 and <= 50;
            bool full = !special || id is >= 5101 and <= 5200 or >= 2062 and <= 2066;
            string text = personName(owner, full);
            if (!special)
            {
                //The actual formatting appends Data1/034F. Normally the first
                //unit already comes from the name; an empty current name must
                //still follow the real suffix instead of inventing an empty.
                var data = admittedData ?? repository.LoadMessages(language, Formats.MessageKind.Data).Messages;
                if (data.Sections.Count <= 1 || data.Sections[1].Records.Count <= 0x34F)
                    throw Missing("fallback_army_suffix");
                text = Head(text) + Head(data.Sections[1].Records[0x34F].Text ?? throw Missing("fallback_army_suffix"));
            }
            return text;
    }

    private static string Head(string text) => text.IndexOf('\0') is int n && n >= 0 ? text[..n] : text;
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("campaign-history." + field);

    private sealed class Current(GameState state, SourceWorldContext current,
        Func<ForceReference, string> shortName) : IReportPoolState
    {
        //History itself is acquired only when cursor/records are reached. A
        //known-suppressed call must not require an otherwise missing ring.
        private CampaignHistoryState? history = state.CampaignHistory;
        private int? pendingSlot;
        private CampaignHistoryRecord? pendingRecord;
        internal CampaignHistoryState History => history ?? throw Missing("saved_ring");
        public ReportBlockerState? ReadReportBlocker() =>
            state.CampaignEventEligibility?.HistoryRecordingSuppressed is bool suppressed
                ? new(true, suppressed ? 1 : 0) : null;
        public ushort? ReadReportCursor() => History.Cursor;
        public void WriteReportCursor(ushort value)
        {
            var completed = History;
            history = completed with
            {
                Cursor = value,
                Records = pendingSlot is int slot && pendingRecord is { } record
                    ? completed.Records.SetItem(slot, record) : completed.Records
            };
        }
        public ReportReference? ReadReportTableReference(int slot) => new(slot);
        private CampaignHistoryRecord Record(ReportReference reference)
        {
            int slot = Slot(reference);
            return pendingSlot == slot && pendingRecord is { } record ? record : History.Records[slot];
        }
        private static int Slot(ReportReference reference) => reference.PhysicalSlot is int slot &&
            (uint)slot < CampaignHistoryState.Capacity ? slot : throw Missing("record_reference");
        private void Replace(ReportReference reference, CampaignHistoryRecord value)
        {
            //One invocation writes one selected record. Keep intermediate stores
            //local; flags reads see them, and the final cursor store publishes
            //the single immutable ring replacement.
            pendingSlot = Slot(reference);
            pendingRecord = value;
        }
        public byte? ReadReportByte(ReportReference reference, int offset) => offset == 0x82
            ? Record(reference).Flags : throw Missing("unexpected_byte_read");
        public void WriteReportByte(ReportReference reference, int offset, byte value)
        {
            var record = Record(reference);
            Replace(reference, offset switch
            {
                0x12 => record with { DistrictId = value },
                0x82 => record with { Flags = value },
                0x86 => record with { Category = value },
                0x134 => record with { ColorIndex = value },
                _ => throw Missing("unexpected_byte_write")
            });
        }
        public void WriteReportWord(ReportReference reference, int offset, ushort value)
        {
            var r = Record(reference);
            if (offset == 0x10) r = r with { CampaignDay = value };
            else if (offset == 0x84) r = r with { HexId = value };
            else if (offset == 0x88) r = r with { PersonId = value };
            else if (offset >= 0x14 && offset < 0x82 && (offset & 1) == 0)
                r = r with { Text = r.Text.SetItem((offset - 0x14) / 2, value) };
            else if (offset >= 0x8A && offset < 0x134 && (offset & 1) == 0)
                r = r with { SupplementalText = r.SupplementalText.SetItem((offset - 0x8A) / 2, value) };
            else if (offset is 0x136 or 0x138)
                r = r with { ForceShortName = r.ForceShortName.SetItem((offset - 0x136) / 2, value) };
            else throw Missing("unexpected_word_write");
            Replace(reference, r);
        }
        public void ClearReportBytes(ReportReference reference, int start, int length) =>
            throw Missing("unexpected_reset");
        public DistrictReference? ReadDistrictTableReference(int slot) => current.ReadDistrictTableReference(slot);
        public bool? ReadDistrictNativeValidity(DistrictReference reference) => current.ReadDistrictNativeValidity(reference);
        public byte? ReadDistrictForceSlot(DistrictReference reference) => current.ReadDistrictForceSlot(reference);
        public ForceReference? ReadForceTableReference(int slot) => current.ReadForceTableReference(slot);
        public byte? ReadForceReportColor(ForceReference reference) => current.ReadForceReportColor(reference);
        public string? ReadForceReportShortName(ForceReference reference) => shortName(reference);
        public bool? ReadReportUiPresent() => throw Missing("unexpected_UI_query");
        public void PresentReport(ReportReference reference) => throw Missing("unexpected_UI_callback");
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private CampaignHistoryWriter? historyWriter;
    internal CampaignHistoryWriter HistoryWriter => historyWriter ??= new(_repository, _scenario, Seed.Language);
    internal GameState WriteHistory(GameState state, SourceWorldContext current, ReportCommand command,
        List<CampaignEffectRequest> effects)
    {
        var result = HistoryWriter.Write(state, current, command);
        if (result.NeedsNotification && result.Written is { } notification)
            effects.Add(new(null, null) { HistoryNotification = notification });
        return result.State;
    }
}
