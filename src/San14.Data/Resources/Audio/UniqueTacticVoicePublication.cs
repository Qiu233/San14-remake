using San14.Core;

namespace San14.Data;

public sealed record UniqueTacticVoiceSpeaker(int SpeakerId, byte? VoiceType, byte? SpecialVoiceType)
{
    public static UniqueTacticVoiceSpeaker FromOfficer(OfficerState person)
        => new(person.Id, person.VoiceType, person.SpecialVoiceType);
}

/// <summary>A405940 selection for ordinary Voice channel1. Sample storage is
/// independent of this non-spatial playback path.</summary>
public sealed record UniqueTacticSelectedVoice(int SpeakerId, byte VoiceType, byte? SpecialVoiceType,
    int VoiceEvent, int VariantIndex, NarrationVoiceLanguage Language, uint SoundId, bool IsSpecial);

/// <summary>Unadopted application random state. A null selection with no missing
/// owner is source silence, not an audio admission or playback receipt.</summary>
public sealed record UniqueTacticVoicePreparation(CampaignAppRandomState? RandomState,
    UniqueTacticSelectedVoice? SelectedVoice, string? UnresolvedOwner = null)
{
    public bool Available => UnresolvedOwner is null;
    public int? ActualSelectedEvent { get; init; }
}

public sealed record UniqueTacticVoicePlayback(AudioSampleRequest Sample, float Gain,
    float PlaybackRate, AudioMusicOutputGraph Outputs,
    AudioCueStaticPlaybackReference? StaticReference, AudioLegacyMusicReference? LegacyReference)
{
    public int VoiceChannel => 1;
}

/// <summary>36ECA0 ->405940, with its original count/draw/gate order. This does
/// not bind a spatial position, reserve a host player, or adopt application RNG.</summary>
public static class UniqueTacticVoicePublication
{
    /// <summary>403C10 asks whether this actual Person has any Voice32
    /// candidates. It does not test application presence or voice enable.</summary>
    public static AreaAdministrationQuery<bool> QueryHasVoice32(UniqueTacticVoiceSpeaker? speaker,
        NarrationVoiceLanguage language)
    {
        if (speaker is null) return AreaAdministrationQuery<bool>.Known(false);
        try
        {
            byte type = speaker.VoiceType ?? throw Missing("Person_voice_type");
            uint special = Special(language, speaker.SpecialVoiceType ?? throw Missing("Person_special_voice_type"));
            return AreaAdministrationQuery<bool>.Known(Count(Slots(language, type, 32)) + (special != 0 ? 1 : 0) > 0);
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<bool>.Unknown(error.Message); }
    }

    public static UniqueTacticVoicePreparation Prepare(CampaignAppRandomState? sourceRandom,
        UniqueTacticVoiceSpeaker speaker, int voiceEvent, NarrationVoiceSettings settings,
        bool presentationSuppressed)
        => PreparePerson(sourceRandom, speaker, voiceEvent, settings, presentationSuppressed, false);

    internal static UniqueTacticVoicePreparation PreparePerson(CampaignAppRandomState? sourceRandom,
        UniqueTacticVoiceSpeaker speaker, int voiceEvent, NarrationVoiceSettings settings,
        bool presentationSuppressed, bool preferSpecial)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        try
        {
            RequireEvent(voiceEvent);
            byte ordinaryType = speaker.VoiceType ?? throw Missing("Person_voice_type");
            ReadOnlySpan<uint> slots = Slots(settings.Language, ordinaryType, voiceEvent);
            int count = Count(slots);
            uint special = IsSpecialEvent(voiceEvent)
                ? Special(settings.Language, speaker.SpecialVoiceType ?? throw Missing("Person_special_voice_type"), voiceEvent) : 0;
            int total = count + (special != 0 ? 1 : 0);
            int variant = 0;
            var next = sourceRandom;
            bool forcedSpecial = preferSpecial && special != 0;
            if (!forcedSpecial && total >= 2)
            {
                var random = new SharedSourceRandom32((sourceRandom ?? throw Missing("App32")).State);
                variant = random.Next(total);
                next = new(random.State);
            }
            if (forcedSpecial) variant = count;
            bool isSpecial = special != 0 && variant == total - 1;
            uint sound = isSpecial ? special : variant < slots.Length ? slots[variant] : 0;
            //404090 tests presentation suppression;404190 does not. Both reach
            //404680's voice-enable gate only after the selection draw.
            if (!settings.Enabled || !isSpecial && presentationSuppressed || sound == 0)
                return new(next, null);
            return new(next, new(speaker.SpeakerId, ordinaryType,
                IsSpecialEvent(voiceEvent) ? speaker.SpecialVoiceType : null, voiceEvent, variant,
                settings.Language, sound, isSpecial));
        }
        catch (InvalidDataException error) { return new(sourceRandom, null, error.Message); }
    }

    /// <summary>Normal resource resolution only; the caller may retain the bank
    /// and use the overload below for subsequent source requests.</summary>
    public static UniqueTacticVoicePlayback Resolve(GameResourceRepository repository,
        UniqueTacticSelectedVoice selected)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var binding = Bank(selected.Language, selected.VoiceEvent);
        return Resolve(selected, AudioSelectionResources.Load(repository, binding.SelectionBankId),
            repository.LoadRdbAsset("RRPreview", NarrationVoiceSelection.OutputResourceId));
    }

    public static UniqueTacticVoicePlayback Resolve(UniqueTacticSelectedVoice selected,
        AudioSelectionBank bank, LoadedRdbAsset categories)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(categories);
        ValidateSelection(selected);
        var binding = Bank(selected.Language, selected.VoiceEvent);
        if (bank.Source.ResourceUri != GameResourceId.Rdb("RRPreview", binding.SelectionBankId).ToString())
            throw Missing("selection_bank");
        if (categories.ResourceUri != GameResourceId.Rdb("RRPreview", NarrationVoiceSelection.OutputResourceId).ToString())
            throw Missing("output_resource");
        var cue = PersonVoiceResourceBinding.ResolveCue(bank, selected.SoundId);
        AudioSampleStorage storage = PersonVoiceResourceBinding.Storage(bank, cue);
        AudioSampleRequest Sample(uint soundId)
        {
            bool external = storage == AudioSampleStorage.External;
            var samples = bank.Samples.Where(row => row.SampleId == soundId &&
                ((row.Flags & 0x10000) != 0) == external).Take(2).ToArray();
            if (samples.Length != 1 || samples[0].Name is not { Length: > 0 } name || samples[0].Headers.Count != 1)
                throw Missing("unique_named_sample");
            if (samples[0].Headers[0].IsExternal != external) throw Missing("sample_storage");
            return new(binding.SelectionBankId, external ? binding.StreamBankId : null,
                name, storage, AudioLoopPolicy.Disabled);
        }
        //The stream voice families use old51D/6282; memory families use562/A517.
        //Reuse the two existing typed readers rather than reinterpret headers.
        if (storage == AudioSampleStorage.External)
        {
            var reference = AudioLegacyMusicResources.ReadStaticReference(bank, cue.Name!);
            float gain = reference.Group.PrimaryGain * reference.Leaf.LinearGain;
            float rate = MathF.Pow(2, reference.Group.AdditiveRate + reference.Leaf.AdditiveRate);
            return new(Sample(reference.Sound.SoundId), gain, rate,
                AudioMusicOutputResources.Read(categories, reference.Group.ClassificationId), null, reference);
        }
        var current = AudioCueGraphResources.ReadSingleStaticNonSpatialReference(bank, cue.Name!, AudioSampleStorage.Internal);
        return new(Sample(current.Sound.SoundId), current.LinearGain, current.PlaybackRate,
            AudioMusicOutputResources.Read(categories, current.Group.ClassificationId), current, null);
    }

    public static AudioCueBank Bank(NarrationVoiceLanguage language, int voiceEvent)
    {
        RequireEvent(voiceEvent);
        return PersonVoiceResourceBinding.Bank(language, voiceEvent);
    }

    private static void ValidateSelection(UniqueTacticSelectedVoice selected)
    {
        RequireEvent(selected.VoiceEvent);
        var slots = Slots(selected.Language, selected.VoiceType, selected.VoiceEvent);
        int count = Count(slots);
        uint special = IsSpecialEvent(selected.VoiceEvent)
            ? Special(selected.Language, selected.SpecialVoiceType ?? throw Missing("Person_special_voice_type"), selected.VoiceEvent) : 0;
        uint expected = selected.IsSpecial
            ? special != 0 && selected.VariantIndex == count ? special : 0
            : (uint)selected.VariantIndex < (uint)count ? slots[selected.VariantIndex] : 0;
        if (expected == 0 || expected != selected.SoundId) throw Missing("selected_definition");
    }

    private static ReadOnlySpan<uint> Slots(NarrationVoiceLanguage language, byte type, int voiceEvent)
    {
        if (!Enum.IsDefined(language)) throw Missing("voice_language");
        if (type >= 26) return [];
        return voiceEvent switch
        {
            2 => NarrationSpatialArmyVoice2Definitions.Slots(language, type),
            3 or 4 or 5 => NarrationSpatialArmyVoiceDefinitions.Slots(language, type, voiceEvent),
            >= 14 and <= 27 => NarrationTacticSpatialVoiceDefinitions.Slots(language, type, voiceEvent),
            32 => language == NarrationVoiceLanguage.Japanese ? JapaneseOrdinaryVoice32[type] : ChineseOrdinaryVoice32[type],
            33 or 34 or 35 or 36 => NarrationPersonnelVoiceDefinitions.Slots(language, type, voiceEvent),
            38 => NarrationGroupMovementVoiceDefinitions.Slots(language, type),
            40 => NarrationRecruitVoiceDefinitions.Slots(language, type),
            42 => NarrationMerchantVoiceDefinitions.Slots(language, type),
            43 => NarrationStrategicVoiceDefinitions.Slots(language, type),
            45 => NarrationDeathVoiceDefinitions.Slots(language, type),
            _ => PersonPreviewVoiceDefinitions.Slots(language, type, voiceEvent)
        };
    }

    private static int Count(ReadOnlySpan<uint> slots)
    {
        int count = slots.Length;
        while (count > 0 && slots[count - 1] == 0) count--;
        return count;
    }

    private static uint Special(NarrationVoiceLanguage language, byte type)
        => type is >= 27 and <= 97 ? (language == NarrationVoiceLanguage.Japanese
            ? JapaneseSpecialVoice32[type - 27] : ChineseSpecialVoice32[type - 27]) : 0;

    private static uint Special(NarrationVoiceLanguage language, byte type, int voiceEvent)
        => voiceEvent == 32 ? Special(language, type) : PersonPreviewVoiceDefinitions.Special(language, type, voiceEvent);

    private static bool IsSpecialEvent(int voiceEvent) => voiceEvent is 10 or 12 or 32;

    private static void RequireEvent(int voiceEvent)
    {
        if (voiceEvent is < 1 or > 46) throw Missing("voice_event");
    }

    private static InvalidDataException Missing(string owner) => new("unique-tactic.voice." + owner);

    //Finite offline TC table import: ordinary32,26 types x5; special32,
    //71 types27..97. No runtime original-executable access or address lookup.
    private static readonly uint[][] JapaneseOrdinaryVoice32 =
    [
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x2E95DC70u, 0x7F3FBA71u, 0xCFE99872u, 0x00000000u, 0x00000000u],
        [0x1073A60Fu, 0x611D8410u, 0xB1C76211u, 0x00000000u, 0x00000000u],
        [0xF2516FAEu, 0x42FB4DAFu, 0x93A52BB0u, 0x00000000u, 0x00000000u],
        [0xD42F394Du, 0x24D9174Eu, 0x7582F54Fu, 0x00000000u, 0x00000000u],
        [0xB60D02ECu, 0x06B6E0EDu, 0x5760BEEEu, 0x00000000u, 0x00000000u],
        [0x97EACC8Bu, 0xE894AA8Cu, 0x393E888Du, 0x00000000u, 0x00000000u],
        [0x95A1BC75u, 0xE64B9A76u, 0x36F57877u, 0x00000000u, 0x00000000u],
        [0x777F8614u, 0xC8296415u, 0x18D34216u, 0x00000000u, 0x00000000u],
        [0x595D4FB3u, 0xAA072DB4u, 0xFAB10BB5u, 0x00000000u, 0x00000000u],
        [0x3B3B1952u, 0x8BE4F753u, 0xDC8ED554u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
    ];
    private static readonly uint[] JapaneseSpecialVoice32 =
    [
        0x00000000u, 0xA490096Du, 0x866DD30Cu, 0x8424C2F6u, 0x66028C95u, 0x47E05634u, 0x29BE1FD3u, 0x0B9BE972u,
        0xED79B311u, 0xCF577CB0u, 0xB135464Fu, 0x93130FEEu, 0x74F0D98Du, 0x72A7C977u, 0x54859316u, 0x00000000u,
        0x00000000u, 0xFA1EEFF3u, 0xDBFCB992u, 0xBDDA8331u, 0x9FB84CD0u, 0x8196166Fu, 0x6373E00Eu, 0x612ACFF8u,
        0x43089997u, 0x24E66336u, 0x06C42CD5u, 0xE8A1F674u, 0xCA7FC013u, 0xAC5D89B2u, 0x00000000u, 0x00000000u,
        0x00000000u, 0x4FADD679u, 0x318BA018u, 0x136969B7u, 0xF5473356u, 0xD724FCF5u, 0xB902C694u, 0x9AE09033u,
        0x7CBE59D2u, 0x5E9C2371u, 0x4079ED10u, 0x3E30DCFAu, 0x200EA699u, 0x00000000u, 0xE3CA39D7u, 0xC5A80376u,
        0xA785CD15u, 0x896396B4u, 0x6B416053u, 0x4D1F29F2u, 0x2EFCF391u, 0x2CB3E37Bu, 0x00000000u, 0x00000000u,
        0xD24D4058u, 0xB42B09F7u, 0x9608D396u, 0x77E69D35u, 0x59C466D4u, 0x3BA23073u, 0x1D7FFA12u, 0x1B36E9FCu,
        0xFD14B39Bu, 0xDEF27D3Au, 0xC0D046D9u, 0xA2AE1078u, 0x848BDA17u, 0x6669A3B6u, 0x00000000u,
    ];
    private static readonly uint[][] ChineseOrdinaryVoice32 =
    [
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x879FAC66u, 0xD8498A67u, 0x28F36868u, 0x00000000u, 0x00000000u],
        [0x697D7605u, 0xBA275406u, 0x0AD13207u, 0x00000000u, 0x00000000u],
        [0x4B5B3FA4u, 0x9C051DA5u, 0xECAEFBA6u, 0x00000000u, 0x00000000u],
        [0x2D390943u, 0x7DE2E744u, 0xCE8CC545u, 0x00000000u, 0x00000000u],
        [0x0F16D2E2u, 0x5FC0B0E3u, 0xB06A8EE4u, 0x00000000u, 0x00000000u],
        [0xF0F49C81u, 0x419E7A82u, 0x92485883u, 0x00000000u, 0x00000000u],
        [0xEEAB8C6Bu, 0x3F556A6Cu, 0x8FFF486Du, 0x00000000u, 0x00000000u],
        [0xD089560Au, 0x2133340Bu, 0x71DD120Cu, 0x00000000u, 0x00000000u],
        [0xB2671FA9u, 0x0310FDAAu, 0x53BADBABu, 0x00000000u, 0x00000000u],
        [0x9444E948u, 0xE4EEC749u, 0x3598A54Au, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
        [0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u],
    ];
    private static readonly uint[] ChineseSpecialVoice32 =
    [
        0x00000000u, 0xFD99D963u, 0xDF77A302u, 0xDD2E92ECu, 0xBF0C5C8Bu, 0xA0EA262Au, 0x82C7EFC9u, 0x64A5B968u,
        0x46838307u, 0x28614CA6u, 0x0A3F1645u, 0xEC1CDFE4u, 0xCDFAA983u, 0xCBB1996Du, 0xAD8F630Cu, 0x00000000u,
        0x00000000u, 0x5328BFE9u, 0x35068988u, 0x16E45327u, 0xF8C21CC6u, 0xDA9FE665u, 0xBC7DB004u, 0xBA349FEEu,
        0x9C12698Du, 0x7DF0332Cu, 0x5FCDFCCBu, 0x41ABC66Au, 0x23899009u, 0x056759A8u, 0x00000000u, 0x00000000u,
        0x00000000u, 0xA8B7A66Fu, 0x8A95700Eu, 0x6C7339ADu, 0x4E51034Cu, 0x302ECCEBu, 0x120C968Au, 0xF3EA6029u,
        0xD5C829C8u, 0xB7A5F367u, 0x9983BD06u, 0x973AACF0u, 0x7918768Fu, 0x00000000u, 0x3CD409CDu, 0x1EB1D36Cu,
        0x008F9D0Bu, 0xE26D66AAu, 0xC44B3049u, 0xA628F9E8u, 0x8806C387u, 0x85BDB371u, 0x00000000u, 0x00000000u,
        0x2B57104Eu, 0x0D34D9EDu, 0xEF12A38Cu, 0xD0F06D2Bu, 0xB2CE36CAu, 0x94AC0069u, 0x7689CA08u, 0x7440B9F2u,
        0x561E8391u, 0x37FC4D30u, 0x19DA16CFu, 0xFBB7E06Eu, 0xDD95AA0Du, 0xBF7373ACu, 0x00000000u,
    ];
}
