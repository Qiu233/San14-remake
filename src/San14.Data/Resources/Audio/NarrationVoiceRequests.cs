using San14.Core;

namespace San14.Data;

/// <summary>The application's voice language is independent of message locale.</summary>
public enum NarrationVoiceLanguage { Japanese, Chinese }

public sealed record NarrationVoiceSettings(bool Enabled = true,
    NarrationVoiceLanguage Language = NarrationVoiceLanguage.Japanese)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Language)) throw new InvalidDataException("Invalid narration voice language.");
    }
}

public sealed record NarrationVoiceRequest(int SpeakerId, byte VoiceType, int VoiceId,
    NarrationVoiceLanguage Language, int VariantIndex, uint SoundId);

/// <summary>One application-owned narration preparation. Requests are source
/// sound selections awaiting an audio consumer, not playback receipts.</summary>
public sealed class NarrationVoiceRequests
{
    private readonly NarrationVoiceSettings settings;
    private readonly Func<int, int> random;
    private readonly bool suppressed;
    private readonly List<NarrationVoiceRequest> pending = [];
    public IReadOnlyList<NarrationVoiceRequest> Pending { get; }

    /// <param name="suppressRequests">The actual application presentation-mode
    /// gate. Ordinary campaign narration supplies false.</param>
    public NarrationVoiceRequests(NarrationVoiceSettings settings, Func<int, int> appPresentationRandom,
        bool suppressRequests = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(appPresentationRandom);
        settings.Validate();
        this.settings = settings;
        random = appPresentationRandom;
        suppressed = suppressRequests;
        Pending = pending.AsReadOnly();
    }

    internal bool Request(OfficerState person, int voiceId)
    {
        //These imported families follow the ordinary405E80 path. Special
        //voice10/12/32 must not borrow these five-slot definitions.
        if (voiceId is not (34 or 35 or 36 or 40 or 42)) throw Missing("voice_family");
        byte voiceType = person.VoiceType ?? throw Missing("current_Person_voice_type");
        ReadOnlySpan<uint> slots = voiceType < 26
            ? voiceId switch
            {
                34 or 35 or 36 => NarrationPersonnelVoiceDefinitions.Slots(settings.Language, voiceType, voiceId),
                40 => NarrationRecruitVoiceDefinitions.Slots(settings.Language, voiceType),
                _ => NarrationMerchantVoiceDefinitions.Slots(settings.Language, voiceType)
            }
            : [];
        int count = slots.Length;
        while (count > 0 && slots[count - 1] == 0) count--;
        // The source selects a variant before checking its voice-enable gate.
        int variant = count < 2 ? 0 : random(count);
        if (count >= 2 && (uint)variant >= (uint)count) throw Missing("presentation_random_range");
        if (!settings.Enabled || suppressed) return false;
        if (voiceType > 97) throw Missing("extended_voice_eligibility");
        if (voiceType >= 26) return false;
        uint sound = slots[variant < 5 ? variant : 0];
        if (sound == 0) return false;
        pending.Add(new(person.Id, voiceType, voiceId, settings.Language, variant, sound));
        return true;
    }

    private static InvalidDataException Missing(string field) => new("narration.voice." + field);
}
