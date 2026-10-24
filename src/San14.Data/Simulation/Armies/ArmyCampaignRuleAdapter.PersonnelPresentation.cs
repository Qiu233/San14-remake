using San14.Core;

namespace San14.Data;

public sealed record PersonnelPresentationSpeaker(int LookupId, int? OfficerId, string Name,
    OfficerPortraitRoute? Face2Portrait, DeploymentNarrationPortraitRoute? StandingPortrait,
    string? PortraitUnresolvedInput);
public sealed record RecruitConversationSpeakers(PersonnelPresentationSpeaker Executor,
    PersonnelPresentationSpeaker Target);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Resolves resources for the saved source-time actors. The name
    /// and selected appearance do not come from their post-recruitment state.</summary>
    public AreaAdministrationQuery<RecruitConversationSpeakers> QueryRecruitConversationSpeakers(
        GameState state, TalentRecruitConversationContext conversation)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(conversation);
        try
        {
            var current = CapturePersonPresentationCurrent(state);
            return AreaAdministrationQuery<RecruitConversationSpeakers>.Known(new(
                Speaker(conversation.ExecutorOfficerId, conversation.ExecutorNameAtSource, conversation.ExecutorPortraitAppearanceIndex),
                Speaker(conversation.TargetOfficerId, conversation.TargetNameAtSource, conversation.TargetPortraitAppearanceIndex)));

            PersonnelPresentationSpeaker Speaker(int id, string name, ushort? appearance)
            {
                if (id is < 1 or > 6000 || name is null)
                    throw new InvalidDataException("personnel.presentation.recruit_source_actor");
                DeploymentNarrationPortraitRoute? standing = null;
                string? missing = null;
                try
                {
                    standing = ReadNarrationPortrait(state, current, _definitions, id,
                        appearance ?? throw new InvalidDataException("personnel.presentation.recruit_source_appearance"),
                        requireCurrentAppearance: false);
                }
                catch (InvalidDataException error) { missing = error.Message; }
                return new(id, id, name, null, standing, missing);
            }
        }
        catch (InvalidDataException error)
        {
            return AreaAdministrationQuery<RecruitConversationSpeakers>.Unknown(error.Message);
        }
    }

    /// <summary>Projects the actual current speaker. The special serif dialog
    /// uses bank10; ordinary event narration uses the existing standing route.</summary>
    public AreaAdministrationQuery<PersonnelPresentationSpeaker> QueryPersonnelPresentationSpeaker(
        GameState state, int officerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            if (officerId is < 0 or > 6000)
                throw new InvalidDataException("personnel.presentation.person_lookup");
            var current = CapturePersonPresentationCurrent(state);
            int physical = current.ReadPersonLookupReference(officerId)?.PhysicalSlot ??
                throw new InvalidDataException("personnel.presentation.person_pointer");
            int identity = current.ReadPersonIdentity(physical) ??
                throw new InvalidDataException("personnel.presentation.person_identity");
            if (_definitions.ResolveOfficerSource(identity) != physical ||
                !state.Officers.TryGetValue(identity, out var person))
                throw new InvalidDataException("personnel.presentation.person_owner");
            //22F020/22F060 concatenate the current surname and given name,
            //including for5012. Resource message labels are not substitutes.
            if (person.CurrentSurname is not string surname || person.CurrentGivenName is not string given)
                throw new InvalidDataException("personnel.presentation.current_name");
            if (surname.Contains('\uFFFD') || given.Contains('\uFFFD'))
                throw new InvalidDataException("personnel.presentation.current_name_encoding");
            string name = surname + given;
            OfficerPortraitRoute? face = null;
            DeploymentNarrationPortraitRoute? standing = null;
            string? missing = null;
            try
            {
                ushort appearance = person.PortraitAppearanceIndex ??
                    throw new InvalidDataException("personnel.presentation.current_appearance");
                if (current.ReadPersonPortraitAppearance(physical) != appearance)
                    throw new InvalidDataException("personnel.presentation.appearance_binding");
                if (officerId == 5012)
                {
                    face = ReadOfficerPortraitAppearance(state, current, _definitions,
                        identity, appearance, ArmyMapUiSources.Create());
                    if (face.Kind != OfficerPortraitRouteKind.StockNamed)
                        missing = face.UnresolvedInput ?? "personnel.presentation.face2_route";
                }
                else standing = ReadNarrationPortrait(state, current, _definitions, identity, appearance);
            }
            catch (InvalidDataException error) { missing = error.Message; }
            return AreaAdministrationQuery<PersonnelPresentationSpeaker>.Known(
                new(officerId, identity, name, face, standing, missing));
        }
        catch (InvalidDataException error)
        {
            return AreaAdministrationQuery<PersonnelPresentationSpeaker>.Unknown(error.Message);
        }
    }
}

/// <summary>One immutable, already selected native sound request. These keys
/// are offline extracted data; runtime resolution only reads installed assets.</summary>
public sealed record PersonnelPresentationAudioSource(AudioCueBank Bank, uint SoundId,
    AudioSampleStorage Storage, bool IsVoice)
{
    public static PersonnelPresentationAudioSource Select(TalentCommandPresentationRequest? talent,
        DiplomacyArrivalAudioRequest? arrival, NarrationVoiceLanguage language)
    {
        if ((talent is null) == (arrival is null))
            throw new InvalidDataException("Personnel audio needs exactly one request.");
        if (talent is not null)
        {
            if (talent.Operation != TalentCommandPresentationOperation.SoundCue)
                throw new InvalidDataException("Personnel presentation is not a sound request.");
            uint sound = talent.SoundId switch
            {
                4 => 0x60E1C344, //SE_000_HAPPY
                5 => 0x7E332452, //SE_000_HAPPY2
                6 => 0xA583960B, //SE_000_UNHAPPY
                8 => 0x9038AFA2, //SE_000_HAPPEN
                _ => throw new InvalidDataException("personnel.presentation.sound_index")
            };
            return new(new(0x17047C02, null), sound, AudioSampleStorage.Internal, false);
        }
        int variant = arrival!.Variant;
        if ((uint)variant >= 3) throw new InvalidDataException("Invalid arrival voice variant.");
        //405A40's App choice already belongs to the saved request: no redraw.
        uint hash = language switch
        {
            NarrationVoiceLanguage.Japanese => new uint[] { 0xD61E61CE, 0x26C83FCF, 0x77721DD0 }[variant],
            NarrationVoiceLanguage.Chinese => new uint[] { 0x2F2831C4, 0x7FD20FC5, 0xD07BEDC6 }[variant],
            _ => throw new ArgumentOutOfRangeException(nameof(language))
        };
        return new(NarrationVoiceSelection.Bank(language), hash, AudioSampleStorage.External, true);
    }

    public AudioSampleRequest Resolve(AudioSelectionBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (bank.Source.ResourceUri != GameResourceId.Rdb("RRPreview", Bank.SelectionBankId).ToString())
            throw new InvalidDataException("Personnel audio bank differs from its source binding.");
        bool external = Storage == AudioSampleStorage.External;
        var matches = bank.Samples.Where(sample => sample.SampleId == SoundId &&
            ((sample.Flags & 0x10000) != 0) == external).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Name is not { Length: > 0 } name)
            throw new InvalidDataException($"Personnel sound {SoundId:X8} has no unique named sample.");
        return new(Bank.SelectionBankId, Bank.StreamBankId, name, Storage, AudioLoopPolicy.Disabled);
    }
}
