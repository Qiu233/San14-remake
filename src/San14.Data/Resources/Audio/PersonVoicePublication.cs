using San14.Core;

namespace San14.Data;

/// <summary>Person gallery preview and Direction share ordinary, non-spatial
/// Voice channel1. Random selection belongs to the application, not a World.</summary>
public static class PersonVoicePublication
{
    /// <summary>The gallery draws its event1..46 before405940. Its prefer-special
    /// argument selects an existing Voice10/12/32 special without a variant draw.
    /// Enable and presentation-suppression gates still follow source selection.</summary>
    public static UniqueTacticVoicePreparation PreparePreview(CampaignAppRandomState? sourceRandom,
        UniqueTacticVoiceSpeaker speaker, NarrationVoiceSettings settings, bool presentationSuppressed = false,
        int previousVoiceEvent = 0)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        if (sourceRandom is null) return new(null, null, "person-preview.voice.App32");
        var random = new SharedSourceRandom32(sourceRandom.State);
        int voiceEvent = random.Next(46) + 1;
        //The stock repeat branch draws0..45 without adding1. Preserve its
        //possible event0 silence instead of rerolling to a playable sample.
        if (voiceEvent == previousVoiceEvent)
            do { voiceEvent = random.Next(46); } while (voiceEvent == previousVoiceEvent);
        if (voiceEvent == 0) return new(new(random.State), null) { ActualSelectedEvent = 0 };
        return UniqueTacticVoicePublication.PreparePerson(new(random.State), speaker, voiceEvent,
            settings, presentationSuppressed, true) with { ActualSelectedEvent = voiceEvent };
    }
}
