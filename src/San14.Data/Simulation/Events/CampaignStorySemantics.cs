using San14.Core;
namespace San14.Data;

public static class CampaignStorySemantics
{
    public static CampaignStoryRequest Bind(CampaignEventExecutionPlan plan, long sequence)
    {
        var steps = plan.Commands.Select<CampaignEventCommand, CampaignStoryStep>(command => command switch
        {
            CampaignEventCameraToScenery c => new StoryCamera(c.HexId, c.CameraParameter, c.BlackOutParameter, c.WaitMilliseconds),
            CampaignEventBackgroundStill b => new StoryBackground(b.StillId, b.NativeTransition),
            CampaignEventStandingPosition2 s => new StoryStanding(s.Persons.Select(Character).ToArray()),
            CampaignEventStandingPosition3 s => new StoryStanding(s.Persons.Select(Character).ToArray()),
            CampaignEventPersonParameters p => new StoryPeople(p.CharacterIds.ToArray()),
            CampaignEventNameParameters n => new StoryNames(n.Names.ToArray()),
            CampaignEventPersonMessage m => new StoryMessage(m.Speaker.CharacterId!.Value, m.MessageId, m.NativeStyle, null, m.PersonParameters.ToArray(), m.NameParameters?.ToArray()),
            CampaignEventNamedPersonMessage m => new StoryMessage(checked((ushort)m.SpeakerCharacterId), m.MessageId, m.Style, m.NameMessageId, m.PersonParameters.ToArray(), m.NameParameters.ToArray()),
            CampaignEventNarration n => new StoryNarration(n.Mode, n.MessageId, n.PersonParameters.ToArray(), n.NameParameters.ToArray()),
            CampaignEventBlackTransition b => new StoryTransition(b.FadeIn, b.Parameter),
            CampaignEventStill s => new StoryIllustration(s.StillId, s.Option),
            CampaignEventAudio a => new StoryAudio(a.Kind switch
                { CampaignEventAudioKind.Bgm => StoryAudioKind.Music, CampaignEventAudioKind.Se => StoryAudioKind.Sound,
                  CampaignEventAudioKind.StopSlot => StoryAudioKind.StopSoundSlot, CampaignEventAudioKind.StopAllSe => StoryAudioKind.StopAllSounds,
                  _ => throw new InvalidDataException("Unknown story audio semantic.") }, a.Id, a.Flags, a.ScalarA, a.ScalarB),
            _ => throw new InvalidDataException("Story command has no complete semantic consumer.")
        }).ToArray();
        var request = new CampaignStoryRequest(sequence, plan.Source, plan.Program.Revision, steps, 0);
        request.Validate(); return request;
    }
    private static StoryCharacter Character(CampaignEventPerson person) => new(person.NativeValid, person.CharacterId);
}
