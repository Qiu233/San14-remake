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
        var rows = new List<CurrentScenarioSettingRow>(CurrentScenarioRows.Length);
        foreach (var descriptor in CurrentScenarioRows)
        {
            int? raw = descriptor.Width == 1
                ? BattleRemakeCurrentSettings.ReadByte(state, descriptor.Member)
                : BattleRemakeCurrentSettings.ReadInt32(state, descriptor.Member);
            int? selected = raw;
            string? unresolved = raw is null ? $"current-settings.member-{descriptor.Member:X3}" : null;
            if (descriptor.Member == 0x3C)
            {
                //609420's final flag overwrite can determine this row even
                //without World50. Otherwise that saved current owner is still
                //unmodeled; the raw bank value cannot prove the local choice.
                if (flags is uint actual && (actual & 0x40000) != 0) { selected = 1; unresolved = null; }
                else { selected = null; unresolved = "current-scenario.World50"; }
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

    //TC643B70's complete ordinary display binding at the resource boundary.
    //Member offsets identify the existing owned bank, not executable addresses.
    private sealed record CurrentScenarioRow(int Group, int Row, int Member, int Width, int Title, int[] Choices);
    private static readonly CurrentScenarioRow[] CurrentScenarioRows =
    [
        new(0, 0, 0x28, 4, 0x4C2, [0xD, 0xE, 0xF, 0x10, 0x11]),
        new(0, 1, 0x3C, 4, 0x4C3, [0x19, 0x1A]),
        new(0, 2, 0x80, 4, 0x4C4, [0x21, 0x22, 0x23]),
        new(0, 3, 0x88, 1, 0x1B, [0x24, 0x25]),
        new(0, 4, 0x84, 4, 0x3FD, [0x26, 0x27, 0x28, 0x29]),
        new(0, 5, 0x30, 4, 0x4C5, [0x15, 0x16, 0x17, 0x18]),
        new(0, 6, 0x2C, 4, 0x4C6, [0x12, 0x13, 0x14]),
        new(0, 7, 0x78, 1, 0x4C7, [0x1D, 0x1E]),
        new(0, 8, 0x79, 1, 0x4C8, [0x1F, 0x20]),
        new(1, 0, 0xB8, 4, 0x6C9, [0x2A, 0x2B]),
        new(1, 1, 0xBC, 4, 0x6CA, [0x2C, 0x2D]),
        new(1, 2, 0xC0, 4, 0x6CB, [0x2E, 0x2F]),
        new(1, 3, 0xC4, 4, 0x6CC, [0x30, 0x31, 0x32]),
        new(1, 4, 0xC8, 4, 0x6CD, [0x33, 0x34, 0x35]),
        new(1, 5, 0xCC, 4, 0x6CE, [0x36, 0x37]),
        new(1, 6, 0xD0, 4, 0x6CF, [0x38, 0x39]),
        new(2, 0, 0xD4, 4, 0x6D8, [0x3A, 0x3B]),
        new(2, 1, 0xD8, 4, 0x6D9, [0x3C, 0x3D]),
        new(2, 2, 0xDC, 4, 0x6DA, [0x3E, 0x3F]),
        new(2, 3, 0xE0, 4, 0x6DB, [0x40, 0x41, 0x42]),
        new(2, 4, 0xE4, 4, 0x6DC, [0x43, 0x44]),
        new(2, 5, 0xE8, 4, 0x6DD, [0x45, 0x46]),
        new(2, 6, 0xEC, 4, 0x6DE, [0x47, 0x48]),
        new(3, 0, 0xF8, 4, 0x7EE, [0x49, 0x4A]),
        new(3, 1, 0xFC, 4, 0x7EF, [0x4B, 0x4C]),
        new(3, 2, 0x100, 4, 0x7F0, [0x4D, 0x4E]),
        new(3, 3, 0x104, 4, 0x7F1, [0x4F, 0x50]),
        new(3, 4, 0x108, 4, 0x7F2, [0x51, 0x52, 0x53]),
        new(3, 5, 0x10C, 4, 0x7F3, [0x54, 0x55]),
        new(3, 6, 0x110, 4, 0x7F4, [0x56, 0x57]),
        new(4, 0, 0x114, 4, 0x893, [0x17A7, 0x17A8, 0x17A9]),
        new(4, 1, 0x118, 4, 0x87E, [0x17AA, 0x17AB, 0x17AC]),
        new(4, 2, 0x11C, 4, 0x890, [0x17AD, 0x17AE]),
        new(4, 3, 0x120, 4, 0x880, [0x17AF, 0x17B0]),
        new(4, 4, 0x124, 4, 0x87B, [0x17B2, 0x17B3]),
        new(4, 5, 0x128, 4, 0x87F, [0x17B4, 0x17B5]),
        new(4, 6, 0x12C, 4, 0x892, [0x17B6, 0x17B7, 0x17B8]),
        new(4, 7, 0x130, 4, 0x861, [0x17A4, 0x17A5]),
    ];
}
