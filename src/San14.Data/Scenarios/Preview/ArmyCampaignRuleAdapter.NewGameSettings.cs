using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
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
            tables[1].Records, tables[0].Records);
    }
}
