using San14.Data;

/// <summary>Application voice switches, independent of gain and text locale.
/// Fresh values come from the original environment constructor.</summary>
internal sealed record VoicePreferences(bool Enabled = true,
    NarrationVoiceLanguage Language = NarrationVoiceLanguage.Japanese)
{
    internal NarrationVoiceSettings Capture()
    {
        var settings = new NarrationVoiceSettings(Enabled, Language);
        settings.Validate();
        return settings;
    }
}
