using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CurrentScenarioSettingChoice(int RadioIndex, int MessageId, string Text);
public sealed record CurrentScenarioSettingRow(int Group, int Row, int MemberOffset, int Width,
    int TitleMessageId, string Title, int? RawValue, int? SelectedRadioIndex,
    IReadOnlyList<CurrentScenarioSettingChoice> Choices, string? UnresolvedInput);
public sealed record CurrentScenarioSettingsPreview(IReadOnlyList<CurrentScenarioSettingRow> Rows);

public sealed partial class ArmyCampaignRuleAdapter
{
    public bool CanViewCurrentScenarioSettings(GameState state) =>
        _scenario.Metadata.FormatMarker == 0x66 &&
        BattleRemakeCurrentSettings.ReadSelectorE58(state) is int selector && selector != 0;

    /// <summary>One read-only publication of the saved ordinary environment.
    /// Page-local normalizations do not install or edit settings. Tab changes
    /// reuse this publication; they do not capture or re-query the world.</summary>
    public CurrentScenarioSettingsPreview CaptureCurrentScenarioSettings(GameState state, string? locale = null)
    {
        if (!CanViewCurrentScenarioSettings(state))
            throw new NotSupportedException("Current scenario settings require the supported ordinary CE owner.");
        locale ??= _scenario.Snapshot.Locale;
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        var tables = _repository.LoadMessages(locale, MessageKind.Data).Messages.Sections;
        uint? flags = OwnedArmyReturnServiceInputs.ReadCurrent(state);
        int? mode = state.CampaignEventWorld?.ScenarioMode;
        int? scenario = state.CampaignEventWorld?.ScenarioId;
        var rows = new List<CurrentScenarioSettingRow>(OrdinaryScenarioSettingsSource.Rows.Length);
        foreach (var descriptor in OrdinaryScenarioSettingsSource.Rows)
        {
            int? raw = descriptor.Width == 1
                ? BattleRemakeCurrentSettings.ReadByte(state, descriptor.Member)
                : BattleRemakeCurrentSettings.ReadInt32(state, descriptor.Member);
            int? selected = raw;
            string? unresolved = raw is null ? $"current-settings.member-{descriptor.Member:X3}" : null;
            if (descriptor.Member == 0x3C)
            {
                //The saved event world already owns World50. Preserve unknown
                //for old saves; neither field is refilled from the scenario.
                int? control = state.CampaignEventWorld?.HistoricalEventControl;
                if (flags is uint actual && (actual & 0x40000) != 0 || control == 0)
                { selected = 1; unresolved = null; }
                else if (control is null || flags is null)
                { selected = null; unresolved = control is null ? "current-scenario.World50" : "current-scenario.World16A8"; }
            }
            else if (descriptor.Member == 0x2C)
            {
                if (mode is null) { selected = null; unresolved = "current-scenario.World44"; }
                else if (mode == 2) { selected = 2; unresolved = null; }
            }
            else if (descriptor.Member == 0x79)
            {
                if (scenario is null) { selected = null; unresolved = "current-scenario.World48"; }
                else if (scenario == 0x3F) { selected = 1; unresolved = null; }
            }
            if (selected is int value && (value < 0 || value >= descriptor.Choices.Length))
            { selected = null; unresolved = $"current-settings.member-{descriptor.Member:X3}.radio-range"; }
            var choices = descriptor.Choices.Select((id, index) => new CurrentScenarioSettingChoice(index, id,
                tables[0].Records[id].Text ?? throw new InvalidDataException("Missing current scenario setting choice."))).ToArray();
            rows.Add(new(descriptor.Group, descriptor.Row, descriptor.Member, descriptor.Width, descriptor.Title,
                tables[1].Records[descriptor.Title].Text ?? throw new InvalidDataException("Missing current scenario setting title."),
                raw, selected, Array.AsReadOnly(choices), unresolved));
        }
        return new(rows.AsReadOnly());
    }

}
