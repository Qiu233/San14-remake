namespace San14.Data;

/// <summary>Offline extracted normal SE manager cue indices. Runtime follows
/// the installed cue graph; an index is never a mixer slot or a sample ID.</summary>
public static class NormalSoundSource
{
    public const uint SelectionBankId = 0x17047C02;
    //TC .rdata RVA13C55F0, 220 u32 entries. Data only, extracted offline.
    private static readonly uint[] CueHashes =
    [
        0xB419E4AD, 0x027AB6FE, 0x5958260A, 0x90B3D3DB, 0x60E1C344, 0x7E332452, 0xA583960B, 0xB400EB99,
        0x9038AFA2, 0xCEE219DC, 0x83D7FE8E, 0x8EE05E74, 0x68FDC418, 0xB6A880B6, 0xDA80596A, 0x5F0927A0,
        0x66F6BF1B, 0x83FCAD3D, 0xAA512353, 0x2A94EEAF, 0x211889FF, 0x93B9E02E, 0xF94508E5, 0x6657FC76,
        0x6541CD1D, 0x7B2F05D5, 0x3FC0E7F4, 0x0452CA13, 0xC8E4AC32, 0x8D768E51, 0x52087070, 0x6D9B17D0,
        0xEC32C7B0, 0xE348545C, 0x345CE58B, 0x8E384FCC, 0xE813BA0D, 0x3EDD3BF7, 0x8F8719F8, 0x4CFCAF3F,
        0x48FE3E1E, 0x44FFCCFD, 0x41015BDC, 0x3D02EABB, 0xD6281BA6, 0x5CADD645, 0xE33390E4, 0x8F1DD775,
        0xD779A60F, 0x53C831CC, 0x99EEC736, 0x8EDDE509, 0x367FBDC3, 0x8CA0AFD2, 0xD9C29A43, 0x29C72910,
        0xC468BC51, 0xCA163AA6, 0xEFEB8F44, 0xD944AC95, 0x2D992883, 0x0349C1AB, 0xF31507A4, 0x25FA0A85,
        0x3BD7CF2C, 0x445222B8, 0x09CE01D9, 0x1D36BB34, 0x6CB60E00, 0x68B79CDF, 0x71F35476, 0x4A3203A0,
        0x2C0FCD3F, 0x6CA14D2F, 0x3EBC4BAB, 0xB20A74B4, 0x835CEAA6, 0xAC4E0DD7, 0xDDC08357, 0x3EB427D8,
        0x52486883, 0x0A31A1EE, 0x36D63AC5, 0xB2861FA5, 0x279AABA0, 0x897200BE, 0xED4054D2, 0x7DF25517,
        0x93580410, 0x5800588D, 0x1FD05461, 0x83056141, 0x135A169A, 0x81E6DAD5, 0xAC068776, 0x8F1CFDF6,
        0x008B9183, 0x818DB93C, 0x19E96AC7, 0x6A8C9E32, 0xBE706BB3, 0x7EED9D1E, 0x262583EB, 0x2553C86B,
        0xAB4466FB, 0x41434101, 0xCD7D5E7F, 0x98E503AB, 0xD94BC10B, 0xC476D3D2, 0x4501341B, 0xAB84599F,
        0xC4A8F7B3, 0xC9F74081, 0x85D38F9D, 0x6E864EB1, 0x3580DD6B, 0xC17A1874, 0xE8019C43, 0x64A8E520,
        0x1F6D5AD4, 0xBBE60F5B, 0xA5B231C3, 0x8AED6706, 0xEB703DA4, 0x2DCFDB4B, 0x44A23549, 0x600DBFFE,
        0x11321BB9, 0xC55EAEDB, 0xE9BB1735, 0x1F4EC8AC, 0xFD420550, 0x1C1A5F40, 0x49E28C65, 0x1E950674,
        0xF3B5784C, 0x97187A4C, 0x60E78D66, 0xBAF6199F, 0xA297EFFE, 0x2408FB68, 0xB7CFBCEA, 0x4E511520,
        0xE4D04C97, 0x091EE964, 0xAC5629C7, 0xC113B1E9, 0xF2A3932E, 0x912CCDB0, 0x1A0672D3, 0x2DDB2FC7,
        0xA7974F3D, 0x23573541, 0x255ED13E, 0x3699F2A8, 0x7585D64B, 0x9DD30C4C, 0x7D938115, 0xF5CD78EC,
        0x95AC67E1, 0xBA7E6C51, 0x410426F0, 0xC07258E3, 0x04D788D0, 0x56E403B9, 0x28C785E4, 0x06C0E546,
        0x58FF8E46, 0xBEDA4E9C, 0xABD73C9E, 0x15FA7F7B, 0x1DC28C43, 0x4F8E873B, 0x5D781D48, 0x4D502FBC,
        0xAE7CB567, 0xBBA87E56, 0xE78FD817, 0x5458C32D, 0x6E4AF369, 0xF67B3348, 0xBE94D850, 0xDE155C30,
        0x7D987022, 0x36715BF7, 0xD3475856, 0xEFFE6C34, 0x5D5D3981, 0x64C26EA0, 0x12B9B366, 0xB84ED082,
        0x7FA2C5E2, 0xC713EF3B, 0xDF1344BA, 0xFBE9058F, 0x01E1E87B, 0x33E652A1, 0x0F5C3B6E, 0x32839B23,
        0x5BD37FA4, 0x9DC81685, 0x9D14C40D, 0x1F018C0E, 0xF1DF298B, 0x273F6FFD, 0xC8C9AB2C, 0xC72C03CC,
        0x0278CA3F, 0x3BB1ADF6, 0x5C23FE37, 0x321D4F7B, 0x6DFFB214, 0xF5164848, 0x07544AA9, 0x1A3380BE,
        0x7AC43E65, 0x3E64AEFD, 0x625404F3, 0x4B470394
    ];

    public static uint? CueHash(int managerId) => (uint)managerId < (uint)CueHashes.Length ? CueHashes[managerId] : null;

    public static int ManagerId(uint cueHash)
    {
        int index = Array.IndexOf(CueHashes, cueHash);
        return index >= 0 ? index : throw new InvalidDataException("audio.normal_sound.cue_manager_identity");
    }

    public static NormalSoundPreparation Prepare(AudioSelectionBank bank, LoadedRdbAsset categories, int managerId)
    {
        uint hash = CueHash(managerId) ?? throw new InvalidDataException("audio.normal_sound.manager_index");
        if (bank.Source.ResourceUri != GameResourceId.Rdb("RRPreview", SelectionBankId).ToString())
            throw new InvalidDataException("audio.normal_sound.selection_bank");
        var matches = bank.Cues.Where(cue => cue.CueId == hash).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Name is not { Length: > 0 } name)
            throw new InvalidDataException("audio.normal_sound.unique_cue");
        var playback = AudioCueGraphResources.ReadSingleStaticNonSpatialReference(bank, name);
        var gain = AudioCueGraphResources.ReadStaticGainCategoryIds(categories, playback.Group.ClassificationId);
        if (gain.Count != 1 || gain[0] is not (0xF4E09144 or 0x2A9C5558))
            throw new NotSupportedException("audio.normal_sound.dynamic_gain_categories");
        var samples = bank.Samples.Where(sample => sample.SampleId == playback.Sound.SoundId).Take(2).ToArray();
        if (samples.Length != 1 || samples[0].Name is not { Length: > 0 } sampleName)
            throw new InvalidDataException("audio.normal_sound.unique_sample");
        if ((samples[0].Flags & 0x10000) != 0)
            throw new NotSupportedException("audio.normal_sound.external_stream_bank");
        return new(AudioSelectionResources.SelectInternalSample(bank, sampleName).AsDecodeInput(),
            playback, gain[0], sampleName, bank.Source.ResourceUri, bank.Source.DecodedSha256,
            samples[0].SectionOffset, samples[0].SectionBytes);
    }
}

public sealed record NormalSoundPreparation(AudioDecodeInput Input, AudioCueStaticPlaybackReference Playback,
    uint GainCategoryId, string SampleName, string BankUri, string BankHash, int SampleOffset, int SampleBytes);
