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
public sealed record NarrationSpatialVoicePosition(float X, float Y, float Z);
public sealed record NarrationSpatialVoiceRequest(NarrationVoiceRequest Voice,
    float SourceX, float SourceY, float SourceZ);

/// <summary>One application-owned narration preparation. Requests are source
/// sound selections awaiting an audio consumer, not playback receipts.</summary>
public sealed class NarrationVoiceRequests
{
    private readonly NarrationVoiceSettings settings;
    private readonly Func<int, int> random;
    private readonly bool suppressed;
    private readonly List<NarrationVoiceRequest> pending = [];
    private readonly Func<int, NarrationSpatialVoicePosition?>? spatialPosition;
    private readonly Func<NarrationSpatialVoiceRequest, bool>? spatialAdmission;
    private readonly List<NarrationSpatialVoiceRequest> spatial = [];
    public IReadOnlyList<NarrationVoiceRequest> Pending { get; }
    public IReadOnlyList<NarrationSpatialVoiceRequest> PendingSpatial { get; }

    /// <param name="suppressRequests">The actual application presentation-mode
    /// gate. Ordinary campaign narration supplies false.</param>
    public NarrationVoiceRequests(NarrationVoiceSettings settings, Func<int, int> appPresentationRandom,
        bool suppressRequests = false, Func<int, NarrationSpatialVoicePosition?>? spatialPosition = null,
        Func<NarrationSpatialVoiceRequest, bool>? spatialAdmission = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(appPresentationRandom);
        settings.Validate();
        this.settings = settings;
        random = appPresentationRandom;
        suppressed = suppressRequests;
        this.spatialPosition = spatialPosition;
        this.spatialAdmission = spatialAdmission;
        Pending = pending.AsReadOnly();
        PendingSpatial = spatial.AsReadOnly();
    }

    internal bool RequestSpatial(OfficerState person, int voiceId)
    {
        if (voiceId is not (3 or 4 or 5)) throw Missing("spatial_voice_family");
        //405AC0 resolves the actual current Person position before405B90's
        //variant draw. A known invalid position returns0 without drawing.
        var position = (spatialPosition ?? throw Missing("spatial_position_owner"))(person.Id);
        if (position is null) return false;
        byte type = person.VoiceType ?? throw Missing("current_Person_voice_type");
        ReadOnlySpan<uint> slots = type < 26
            ? NarrationSpatialArmyVoiceDefinitions.Slots(settings.Language, type, voiceId) : [];
        int count = slots.Length;
        while (count > 0 && slots[count - 1] == 0) count--;
        int variant = count < 2 ? 0 : random(count);
        if (count >= 2 && (uint)variant >= (uint)count) throw Missing("presentation_random_range");
        //404300 has the Voice enable gate, but does not use405CF0's event
        //presentation suppression. It then performs actual spatial admission.
        if (!settings.Enabled || type >= 26) return false;
        uint sound = slots[variant < 5 ? variant : 0];
        if (sound == 0) return false;
        var request = new NarrationSpatialVoiceRequest(new(person.Id, type, voiceId,
            settings.Language, variant, sound), position.X, position.Y, position.Z);
        if (!(spatialAdmission ?? throw Missing("spatial_admission_owner"))(request)) return false;
        spatial.Add(request);
        return true;
    }

    internal bool Request(OfficerState person, int voiceId)
    {
        //These imported families follow the ordinary405E80 path. Special
        //voice10/12/32 must not borrow these five-slot definitions.
        if (voiceId is not (33 or 34 or 35 or 36 or 38 or 40 or 42 or 43)) throw Missing("voice_family");
        byte voiceType = person.VoiceType ?? throw Missing("current_Person_voice_type");
        ReadOnlySpan<uint> slots = voiceType < 26
            ? voiceId switch
            {
                33 or 34 or 35 or 36 => NarrationPersonnelVoiceDefinitions.Slots(settings.Language, voiceType, voiceId),
                38 => NarrationGroupMovementVoiceDefinitions.Slots(settings.Language, voiceType),
                40 => NarrationRecruitVoiceDefinitions.Slots(settings.Language, voiceType),
                43 => NarrationStrategicVoiceDefinitions.Slots(settings.Language, voiceType),
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
