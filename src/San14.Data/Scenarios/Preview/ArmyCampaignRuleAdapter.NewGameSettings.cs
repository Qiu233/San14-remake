using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Immutable resource captions only; no selected value or permission.</summary>
public sealed record NewGameSettingResourceText(int Group, int Row, string Title, IReadOnlyList<string> Choices);

public sealed partial class ArmyCampaignRuleAdapter
{
    public static IReadOnlyList<NewGameSettingResourceText> ReadNewGameSettingResourceText(LoadedMessages messages)
    {
        var sections = messages.Messages.Sections;
        return Array.AsReadOnly(OrdinaryScenarioSettingsSource.Rows.Select(row =>
            new NewGameSettingResourceText(row.Group, row.Row,
                sections[1].Records[row.Title].Text ?? throw new InvalidDataException("Missing ordinary setting title."),
                Array.AsReadOnly(row.Choices.Select(id => sections[0].Records[id].Text
                    ?? throw new InvalidDataException("Missing ordinary setting choice.")).ToArray()))).ToArray());
    }

    /// <summary>One normal-resource settings publication for a retained draft.
    /// No world capture, source-program read, bank installation or mutation is
    /// performed. Radio events reuse its choices and update only the draft.</summary>
    public NewGameSettingsPreview CaptureNewGameSettings(NewGameOptions draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        draft.ValidateOrdinarySettings();
        if (_scenario.Metadata.FormatMarker != 0x66)
            throw new InvalidDataException("The ordinary settings publication requires the supported CE source profile.");
        var tables = _repository.LoadMessages(_scenario.Snapshot.Locale, MessageKind.Data).Messages.Sections;
        return NewGameOrdinarySettingsSource.Publish(draft,
            _scenario.Snapshot.WorldParameters?.SourceBoundary?.Settings,
            _scenario.Snapshot.WorldParameters?.RawModeValue,
            tables[1].Records, tables[0].Records,
            _scenario.Snapshot.WorldParameters?.RawField48,
            _scenario.Snapshot.WorldParameters?.RawHistoricalEventControl);
    }
}
