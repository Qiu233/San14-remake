using System.Text.Json.Serialization;

namespace San14.Core;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(StoryCamera), "camera")]
[JsonDerivedType(typeof(StoryBackground), "background")]
[JsonDerivedType(typeof(StoryStanding), "standing")]
[JsonDerivedType(typeof(StoryPeople), "people")]
[JsonDerivedType(typeof(StoryNames), "names")]
[JsonDerivedType(typeof(StoryMessage), "message")]
[JsonDerivedType(typeof(StoryNarration), "narration")]
[JsonDerivedType(typeof(StoryTransition), "transition")]
[JsonDerivedType(typeof(StoryIllustration), "illustration")]
[JsonDerivedType(typeof(StoryAudio), "audio")]
[JsonDerivedType(typeof(StoryLandmarkRewardNotice), "landmark-reward")]
[JsonDerivedType(typeof(StoryRestoreCampaignCamera), "restore-campaign-camera")]
public abstract record CampaignStoryStep;
public sealed record StoryCharacter(bool Available, ushort? CharacterId);
public sealed record StoryCamera(int? HexId, int CameraSetting, int BlackoutSetting, int WaitMilliseconds) : CampaignStoryStep;
public sealed record StoryBackground(int IllustrationId, int TransitionSetting) : CampaignStoryStep;
public sealed record StoryStanding(StoryCharacter[] Characters) : CampaignStoryStep;
public sealed record StoryPeople(int[] Characters) : CampaignStoryStep;
public sealed record StoryNames(string[] Names) : CampaignStoryStep;
public sealed record StoryMessage(ushort Speaker, int MessageId, int Style, int? NameMessageId,
    int[] People, string[]? Names) : CampaignStoryStep;
public sealed record StoryNarration(int Mode, int MessageId, int[] People, string[] Names) : CampaignStoryStep;
public sealed record StoryTransition(bool FadeIn, int Setting) : CampaignStoryStep;
public sealed record StoryIllustration(int IllustrationId, int Setting) : CampaignStoryStep;
public enum StoryAudioKind { Music, Sound, StopSoundSlot, StopAllSounds }
public sealed record StoryAudio(StoryAudioKind AudioKind, int Id, int Flags, float ScalarA, float ScalarB) : CampaignStoryStep;
public sealed record StoryLandmarkRewardNotice(int ForceId, byte EffectKind, int Amount, int MessageId) : CampaignStoryStep;
public sealed record CampaignCameraSnapshot(float FocusX, float FocusY, float FocusZ, float FocusW,
    double Distance, double Yaw, double Pitch);
/// <summary>The presentation owner compares focus and submits the original one-millisecond request only when changed.</summary>
public sealed record StoryRestoreCampaignCamera(CampaignCameraSnapshot Snapshot) : CampaignStoryStep;
public sealed record CampaignStoryScene(StoryBackground? Background, StoryStanding? Standing,
    StoryIllustration? Illustration, StoryTransition? Blackout, StoryCamera? Camera,
    CampaignStoryStep? Text, StoryAudio? Music)
{
    public CampaignCameraSnapshot? RestoredCamera { get; init; }
    public static CampaignStoryScene Empty => new(null, null, null, null, null, null, null);
    public CampaignStoryScene Apply(CampaignStoryStep step) => step switch
    {
        StoryBackground b => this with { Background = b },
        StoryStanding s => this with { Standing = s },
        StoryIllustration i => this with { Illustration = i },
        StoryTransition t => this with { Blackout = t },
        StoryCamera c => this with { Camera = c, RestoredCamera = null },
        StoryRestoreCampaignCamera r => this with { RestoredCamera = r.Snapshot },
        StoryMessage or StoryNarration or StoryLandmarkRewardNotice => this with { Text = step },
        StoryAudio a when a.AudioKind == StoryAudioKind.Music => this with { Music = a },
        _ => this
    };
}

/// <summary>Resource identities and bound presentation semantics. No instruction stream or execution receipt.</summary>
public sealed record CampaignStoryRequest(long Sequence, EventSourceReference Source, string ContentRevision,
    CampaignStoryStep[] Steps, int NextStep)
{
    [JsonRequired] public CampaignStoryScene Scene { get; init; } = CampaignStoryScene.Empty;
    public CampaignStoryRequest CompleteStep()
    {
        if (NextStep >= Steps.Length) throw new InvalidDataException("Story has no waiting step.");
        return this with { Scene = Scene.Apply(Steps[NextStep]), NextStep = NextStep + 1 };
    }
    public CampaignStoryRequest Copy() => this with { Steps = Steps.Select(CloneStep).ToArray(), Scene = Scene with
        { Standing = Scene.Standing is { } s ? s with { Characters = s.Characters.ToArray() } : null,
          Text = Scene.Text is null ? null : CloneStep(Scene.Text) } };
    private static CampaignStoryStep CloneStep(CampaignStoryStep step) => step switch
    {
        StoryStanding standing => standing with { Characters = standing.Characters.ToArray() },
        StoryPeople people => people with { Characters = people.Characters.ToArray() },
        StoryNames names => names with { Names = names.Names.ToArray() },
        StoryMessage message => message with { People = message.People.ToArray(), Names = message.Names?.ToArray() },
        StoryNarration narration => narration with { People = narration.People.ToArray(), Names = narration.Names.ToArray() },
        _ => step
    };
    public void Validate()
    {
        PlayableEventStateValidation.ValidateSource(Source);
        if (Scene is null || Sequence < 1 || string.IsNullOrWhiteSpace(ContentRevision) || Steps is not { Length: > 0 } ||
            NextStep < 0 || NextStep > Steps.Length || Steps.Any(s => s is null))
            throw new InvalidDataException("Saved story request is incomplete.");
        foreach (var step in Steps)
            switch (step)
            {
                case StoryStanding s when s.Characters is not { Length: 2 or 3 } ||
                    s.Characters.Any(p => p is null || p.Available != p.CharacterId.HasValue):
                case StoryPeople p when p.Characters is not { Length: 5 } || p.Characters.Any(i => i is < 0 or > ushort.MaxValue):
                case StoryNames n when n.Names is not { Length: 5 } || n.Names.Any(n => n is null):
                case StoryMessage m when m.People is not { Length: 5 } || m.People.Any(i => i is < 0 or > ushort.MaxValue) ||
                    m.Names is not null && (m.Names.Length != 5 || m.Names.Any(n => n is null)):
                case StoryNarration narration when narration.People is not { Length: 5 } || narration.People.Any(i => i is < 0 or > ushort.MaxValue) ||
                    narration.Names is not { Length: 5 } || narration.Names.Any(v => v is null):
                case StoryAudio a when !Enum.IsDefined(a.AudioKind) || !float.IsFinite(a.ScalarA) || !float.IsFinite(a.ScalarB):
                case StoryCamera c when c.WaitMilliseconds < 0:
                case StoryRestoreCampaignCamera r when r.Snapshot is null:
                    throw new InvalidDataException("Saved story semantics are incomplete.");
            }
    }
}
public sealed record CampaignStoryStepReceipt(long RequestSequence, int Step);
