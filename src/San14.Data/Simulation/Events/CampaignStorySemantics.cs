using San14.Core;
namespace San14.Data;

public static class CampaignStorySemantics
{
    public static CampaignStoryRequest Bind(CampaignEventExecutionPlan plan, long sequence, bool allowEmpty = false)
    {
        var steps = plan.Commands.Select<CampaignEventCommand, CampaignStoryStep>(command => command switch
        {
            CampaignEventCameraToScenery c => new StoryCamera(c.HexId, c.CameraParameter, c.BlackOutParameter, c.WaitMilliseconds),
            CampaignEventSuppressedCamera => new StoryTransition(false, -1),
            CampaignEventCameraToTarget c => new StoryCamera(c.HexId, c.CameraParameter, -1, 1000),
            CampaignEventBackgroundStill b => new StoryBackground(b.StillId, b.NativeTransition)
                { ResourceKind = b.NativeType, FadeIn = b.NativeType == 15 },
            CampaignEventStandingPosition2 s => new StoryStanding(s.Persons.Select(Character).ToArray()),
            CampaignEventStandingPosition3 s => new StoryStanding(s.Persons.Select(Character).ToArray()),
            CampaignEventPersonParameters p => new StoryPeople(p.CharacterIds.ToArray()),
            CampaignEventDoctrineNameParameters n => new StoryNames(n.Names.ToArray()),
            CampaignEventNameParameters n => new StoryNames(n.Names.ToArray()),
            CampaignEventMessageStringParameters n => new StoryNames(n.Names.ToArray()),
            CampaignEventPoemEvent poem => new StoryPoemEvent(poem.RawId),
            CampaignEventAudioControl audio => audio.Step,
            CampaignEventItemIllustration i => new StoryItemIllustration(i.IllustrationIndex, i.Setting),
            CampaignEventResetStandingRegistration => new StoryResetStandingRegistration(),
            CampaignEventInputWait => new CampaignStoryInputWait(),
            CampaignEventHideMessage => new StoryHideMessage(),
            CampaignEventWait wait => new StoryWait(wait.Milliseconds),
            CampaignEventOwnedEffect e => e.Effect,
            CampaignEventFormatOnlyMessage m => new StoryFormatOnlyMessage(m.Speaker, m.MessageId,
                m.PersonParameters.ToArray(), m.NameParameters?.ToArray())
                { Cities = m.CityParameters?.ToArray(), Forces = m.ForceParameters?.ToArray(),
                    Areas = m.AreaParameters?.ToArray(), Items = m.ItemParameters?.ToArray(), Numbers = m.NumberParameters?.ToArray(), Provinces = m.ProvinceParameters?.ToArray() },
            CampaignEventPersonMessage m => new StoryMessage(m.Speaker.CharacterId!.Value, m.MessageId, m.NativeStyle, null, m.PersonParameters.ToArray(), m.NameParameters?.ToArray())
                { Cities = m.CityParameters?.ToArray(), Forces = m.ForceParameters?.ToArray(),
                    Areas = m.AreaParameters?.ToArray(), Items = m.ItemParameters?.ToArray(),
                    Numbers = m.NumberParameters?.ToArray(), Provinces = m.ProvinceParameters?.ToArray(), SpeechOnly = m.SpeechOnly, WithoutPortrait = m.WithoutPortrait },
            CampaignEventNamedPersonMessage m => new StoryMessage(checked((ushort)m.SpeakerCharacterId), m.MessageId, m.Style, m.NameMessageId, m.PersonParameters.ToArray(), m.NameParameters?.ToArray())
                { Cities = m.CityParameters?.ToArray(), Forces = m.ForceParameters?.ToArray(), Areas = m.AreaParameters?.ToArray(),
                    Items = m.ItemParameters?.ToArray(), Numbers = m.NumberParameters?.ToArray(), Provinces = m.ProvinceParameters?.ToArray(), WithoutPortrait = m.WithoutPortrait },
            CampaignEventNarration n => new StoryNarration(n.Mode, n.MessageId, n.PersonParameters.ToArray(), n.NameParameters?.ToArray())
                { Cities = n.CityParameters?.ToArray(), Forces = n.ForceParameters?.ToArray(), Areas = n.AreaParameters?.ToArray(),
                    Items = n.ItemParameters?.ToArray(), Numbers = n.NumberParameters?.ToArray(), Provinces = n.ProvinceParameters?.ToArray() },
            CampaignEventBlackTransition b => new StoryTransition(b.FadeIn, b.Parameter),
            CampaignEventStill s => new StoryIllustration(s.StillId, s.Option),
            CampaignEventSmallStill s => new StorySmallIllustration(s.StillId, s.Option),
            CampaignEventTutorialStill s => new StoryTutorialIllustration(s.StillId, s.Option),
            CampaignEventHideCharacters h => new StoryHideCharacters(h.Setting, h.CharacterIds.ToArray()),
            CampaignEventAudio a => new StoryAudio(a.Kind switch
                { CampaignEventAudioKind.Bgm => StoryAudioKind.Music,
                  CampaignEventAudioKind.DirectBgm => StoryAudioKind.DirectMusic,
                  CampaignEventAudioKind.FadeOutBgm => StoryAudioKind.FadeOutMusic, CampaignEventAudioKind.Se => StoryAudioKind.Sound,
                  CampaignEventAudioKind.StopSlot => StoryAudioKind.StopSoundSlot, CampaignEventAudioKind.StopAllSe => StoryAudioKind.StopAllSounds,
                  CampaignEventAudioKind.StopActiveSlots => StoryAudioKind.StopActiveSoundSlots,
                  CampaignEventAudioKind.SeIfNotSkipped => StoryAudioKind.SoundIfNotSkipped,
                  _ => throw new InvalidDataException("Unknown story audio semantic.") }, a.Id, a.Flags, a.ScalarA, a.ScalarB),
            _ => throw new InvalidDataException("Story command has no complete semantic consumer.")
        }).ToArray();
        // The fresh execution-context ctor3D2E50 zeros its three owned-effect
        // slots. This is new-invocation knowledge, never an old-save fallback.
        var request = new CampaignStoryRequest(sequence, plan.Source, plan.Program.Revision, steps, 0)
            { Scene = CampaignStoryScene.Empty with { OwnedEffects = [] } };
        request.Validate(allowEmpty); return request;
    }
    private static StoryCharacter Character(CampaignEventPerson person) => new(person.NativeValid, person.CharacterId);
}
