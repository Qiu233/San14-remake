using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual application entry, supplied by the new/load lifecycle. It
/// is not inferred from the world's age, absent owners or a saved completion bit.</summary>
public enum CampaignEntryKind { NewCampaign, RestoreCampaign }
public sealed record FirstStrategyEntry(CampaignEntryKind Kind, TransferModeState? ExternalMode,
    bool? ApplicationPresent, int? SelectedApplicationUserIndex);
public sealed record FirstStrategyInitialization(GameState? State, string Branch,
    S14EventEligibilityScanResult? Eligibility, int[] RegisteredDisappearances, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One transaction for all three first-strategy writers. It does
    /// not play events, refresh combat, acknowledge stories or run stage2.</summary>
    public FirstStrategyInitialization TryInitializeFirstStrategy(GameState state, FirstStrategyEntry entry)
    {
        var registered = new List<int>();
        try
        {
            if (!Enum.IsDefined(entry.Kind)) throw OccupationMissing("first_strategy_entry_kind");
            if (entry.Kind == CampaignEntryKind.RestoreCampaign) return new(state, "source.load_gate", null, [], null);
            var mode = entry.ExternalMode ?? throw OccupationMissing("first_strategy_external_mode_pointer");
            if (mode.Present && OccupationKnown(mode.Value, "first_strategy_external_mode_value") != 0)
                return new(state, "source.external_mode_gate", null, [], null);
            bool applicationPresent = OccupationKnown(entry.ApplicationPresent, "first_strategy_application_pointer");
            int user = applicationPresent
                ? OccupationKnown(entry.SelectedApplicationUserIndex, "first_strategy_selected_application_user_index") : -1;
            state = AppendInitialStrategyChronicleCurrent(state);
            var scan = ScanCampaignEventConditions(state, user, applicationPresent: applicationPresent);
            var history = scan.State.CampaignEvents ?? throw OccupationMissing("first_strategy_history");
            var occurred = history.Occurred.ToArray(); var dates = history.OccurrenceDates.ToArray();
            foreach (var row in scan.Eligibility.Results)
                if (row.Status == S14EventEligibilityStatus.Satisfied && row.Record.Id <= 949 &&
                    BinaryPrimitives.ReadInt32LittleEndian(row.Record.RawBytes.AsSpan(4)) == 3 &&
                    (uint)BinaryPrimitives.ReadInt32LittleEndian(row.Record.RawBytes.AsSpan(8)) < 3)
                    occurred[row.Record.Id] = true;
            int selector = OccupationKnown(BattleRemakeCurrentSettings.ReadSelectorE58(scan.State), "first_strategy_settings_selector");
            int enabled = OccupationKnown(BattleRemakeCurrentSettings.ReadInt32(scan.State, selector == 0 ? 0x14C : 0x3C), "first_strategy_historical_setting");
            if (enabled == 1)
            {
                int scenario = (scan.State.CampaignEventWorld ?? throw OccupationMissing("first_strategy_world")).ScenarioId;
                (int Id, CampaignEventOccurrenceDate? Date) seed = scenario switch
                { 1 => (1, new(184, 2, 1)), 2 => (3, new(190, 1, 1)), 3 => (17, new(194, 6, 1)), _ => (0, null) };
                if (seed.Id != 0) { occurred[seed.Id] = true; dates[seed.Id] = seed.Date; }
            }
            var working = scan.State with { CampaignEvents = history with { Occurred = occurred, OccurrenceDates = dates } };
            var current = CaptureCurrentCampaignEventQueries(working);
            var date = working.NativeMapSeason?.ThirdDate ?? throw OccupationMissing("first_strategy_date");
            if (working.PersonnelCommands?.Disappearances is null) throw OccupationMissing("first_strategy_disappearance_owner");
            var candidates = current.CreateTemporaryHexList() ?? throw OccupationMissing("first_strategy_person_temporary");
            try
            {
                int setting = selector == 0
                    ? OccupationKnown(BattleRemakeCurrentSettings.ReadInt32(working, 0x1DC), "first_strategy_special_person_setting")
                    : OccupationKnown(BattleRemakeCurrentSettings.ReadByte(working, 0x78), "first_strategy_special_person_setting");
                if (selector == 0) setting = setting == 1 ? 1 : 0;
                else setting = unchecked((sbyte)setting);
                var people = new List<int>();
                for (int physical = 0; physical < 1851; physical++)
                {
                    int at = current.ReadTransferPersonTableReference(physical)?.PhysicalSlot ?? throw OccupationMissing("first_strategy_physical_person");
                    ushort id = OccupationKnown(current.ReadPersonIdentity(at), "first_strategy_person_identity");
                    if (!(id == 249 && setting == 0 || id is 433 or 440 or 842 or 980)) continue;
                    if (OccupationKnown(current.ReadPersonStatus(at), "first_strategy_person_status") is not (7 or 8)) continue;
                    if (current.AppendPersonReturnTemporary(candidates, at)) people.Add(at);
                }
                foreach (int physical in people)
                {
                    var associated = current.ReadPersonLinkedEntity(physical) ?? throw OccupationMissing("first_strategy_person_association");
                    if (associated.Identity.HasValue) continue;
                    ushort year = OccupationKnown(current.ConditionAppearanceYearReader?.Invoke(physical), "first_strategy_appearance_year");
                    ushort id = OccupationKnown(current.ReadPersonIdentity(physical), "first_strategy_person_identity");
                    int offset = id == 842 ? 1440 : id is 249 or 433 or 440 or 980 ? 360 : 0;
                    // Appearance is a source u16, but the temporary date stores
                    // that word as a signed year before ordinal conversion.
                    int appearanceDay = PersonReturnCommands.CalendarOrdinal(new(unchecked((short)year), 1, 1, 0));
                    int delay = unchecked(appearanceDay + offset - PersonReturnCommands.CalendarOrdinal(date));
                    if (delay <= 0) continue;
                    var result = PersonReturnCommands.RegisterDisappearance(working, current, physical, delay, true, date);
                    working = result.State ?? throw OccupationMissing(result.UnresolvedOwner ?? "first_strategy_disappearance_registration");
                    if (result.TaskId is int taskId) registered.Add(taskId);
                }
            }
            finally { current.ClearTemporaryHexList(candidates); current.ReleaseTemporaryHexList(candidates); }
            working = PersonReturnCommands.PublishCurrent(working, current);
            return new(working with { CampaignEvents = working.CampaignEvents! with { InitialStrategyProcessed = true } },
                "source.complete_three_writers", scan.Eligibility, registered.ToArray(), null);
        }
        catch (InvalidDataException error) { return new(null, "unresolved", null, [], error.Message); }
    }
}
