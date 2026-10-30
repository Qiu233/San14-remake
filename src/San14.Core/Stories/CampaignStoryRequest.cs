using System.Text.Json.Serialization;
using System.Collections.Immutable;

namespace San14.Core;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(StoryCamera), "camera")]
[JsonDerivedType(typeof(StoryBackground), "background")]
[JsonDerivedType(typeof(StoryStanding), "standing")]
[JsonDerivedType(typeof(StoryPeople), "people")]
[JsonDerivedType(typeof(StoryNames), "names")]
[JsonDerivedType(typeof(StoryMessage), "message")]
[JsonDerivedType(typeof(StoryHideMessage), "hide-message")]
[JsonDerivedType(typeof(StoryWait), "wait")]
[JsonDerivedType(typeof(CampaignStoryInputWait), "input-wait")]
[JsonDerivedType(typeof(StoryPoemEvent), "poem-event")]
[JsonDerivedType(typeof(StoryNarration), "narration")]
[JsonDerivedType(typeof(StoryTransition), "transition")]
[JsonDerivedType(typeof(StoryIllustration), "illustration")]
[JsonDerivedType(typeof(StorySmallIllustration), "small-illustration")]
[JsonDerivedType(typeof(StoryTutorialIllustration), "tutorial-illustration")]
[JsonDerivedType(typeof(StoryItemIllustration), "item-illustration")]
[JsonDerivedType(typeof(StoryResetStandingRegistration), "reset-standing-registration")]
[JsonDerivedType(typeof(StoryFormatOnlyMessage), "format-only-message")]
[JsonDerivedType(typeof(StoryOwnedEffect), "owned-effect")]
[JsonDerivedType(typeof(StoryHideCharacters), "hide-characters")]
[JsonDerivedType(typeof(StoryAudio), "audio")]
[JsonDerivedType(typeof(StoryLandmarkRewardNotice), "landmark-reward")]
[JsonDerivedType(typeof(StoryRestoreCampaignCamera), "restore-campaign-camera")]
public abstract record CampaignStoryStep;
public sealed record StoryCharacter(bool Available, ushort? CharacterId);
public sealed record StoryCamera(int? HexId, int CameraSetting, int BlackoutSetting, int WaitMilliseconds) : CampaignStoryStep;
public sealed record StoryBackground(int IllustrationId, int TransitionSetting) : CampaignStoryStep
{
    public int ResourceKind { get; init; } = 15;
    public bool FadeIn { get; init; } = true;
}
public sealed record StoryStanding(StoryCharacter[] Characters) : CampaignStoryStep;
public sealed record StoryPeople(int[] Characters) : CampaignStoryStep;
public sealed record StoryNames(string[] Names) : CampaignStoryStep;
public sealed record StoryMessage(ushort Speaker, int MessageId, int Style, int? NameMessageId,
    int?[] People, string[]? Names) : CampaignStoryStep
{
    public bool SpeechOnly { get; init; }
    [JsonRequired] public bool WithoutPortrait { get; init; } = false;
    public int[]? Cities { get; init; }
    public int[]? Forces { get; init; }
    public int[]? Areas { get; init; }
    public int[]? Items { get; init; }
    public int?[]? Numbers { get; init; }
    public int?[]? Provinces { get; init; }
}
public sealed record StoryHideMessage : CampaignStoryStep;
public sealed record StoryWait(int Milliseconds) : CampaignStoryStep;
/// <summary>Waits for the actual EventView input/automatic/skip lifecycle.</summary>
public sealed record CampaignStoryInputWait : CampaignStoryStep;
/// <summary>The ordinary child poem presentation; completion follows its real exit.</summary>
public sealed record StoryPoemEvent(int RawId) : CampaignStoryStep;
public sealed record StoryNarration(int Mode, int MessageId, int?[] People, string[]? Names) : CampaignStoryStep
{
    public int[]? Cities { get; init; }
    public int[]? Forces { get; init; }
    public int[]? Areas { get; init; }
    public int[]? Items { get; init; }
    public int?[]? Numbers { get; init; }
    public int?[]? Provinces { get; init; }
}
/// <summary>Executes the ordinary message formatter after hiding the message,
/// without displaying a page or adding an entry to the displayed text log.</summary>
public sealed record StoryFormatOnlyMessage(ushort Speaker, int MessageId, int?[] People, string[]? Names) : CampaignStoryStep
{
    public int[]? Cities { get; init; }
    public int[]? Forces { get; init; }
    public int[]? Areas { get; init; }
    public int[]? Items { get; init; }
    public int?[]? Numbers { get; init; }
    public int?[]? Provinces { get; init; }
}
public sealed record StoryTransition(bool FadeIn, int Setting) : CampaignStoryStep;
public sealed record StoryIllustration(int IllustrationId, int Setting) : CampaignStoryStep;
public sealed record StorySmallIllustration(int IllustrationId, int Setting) : CampaignStoryStep;
public sealed record StoryTutorialIllustration(int IllustrationId, int Setting) : CampaignStoryStep;
/// <summary>The independent Item illustration layer. Null hides it; zero is a real illustration.</summary>
public sealed record StoryItemIllustration(int? IllustrationId, int Setting) : CampaignStoryStep;
public sealed record StoryResetStandingRegistration : CampaignStoryStep;
public enum StoryOwnedEffectOperation { Create, StopAll }
/// <summary>An invocation-owned presentation operation, not an allocated runtime effect handle.</summary>
public sealed record StoryOwnedEffect(StoryOwnedEffectOperation Operation, int EffectId, int SelectorMode) : CampaignStoryStep;
/// <summary>A context-owned slot known to the actual presentation host. Runtime GPU handles are not saved.</summary>
public sealed record StoryOwnedEffectSlot(int Slot, int EffectId, int SelectorMode);
/// <summary>The actual host's complete slot snapshot after an effect operation.</summary>
public sealed record StoryOwnedEffectReceipt(ImmutableArray<StoryOwnedEffectSlot> Slots);
public sealed record StoryHideCharacters(int Setting, int[] Characters) : CampaignStoryStep;
public enum StoryAudioKind { Music, Sound, StopSoundSlot, StopAllSounds, StopActiveSoundSlots, SoundIfNotSkipped, FadeOutMusic, DirectMusic, FadeInSoundIfNotSkipped, ChangeSelectedSoundVolumes, FadeOutMatchingSounds, StopMatchingSounds }
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
    // A hidden message pane retains its actual last written name. Null is unknown legacy state.
    [JsonRequired] public string? RetainedSpeakerName { get; init; }
    public CampaignCameraSnapshot? RestoredCamera { get; init; }
    public StorySmallIllustration? SmallIllustration { get; init; }
    public StoryTutorialIllustration? TutorialIllustration { get; init; }
    public StoryItemIllustration? ItemIllustration { get; init; }
    public ImmutableArray<StoryOwnedEffectSlot>? OwnedEffects { get; init; }
    public CampaignStoryPreparedText? PreparedText { get; init; }
    public ImmutableDictionary<int, TalentOfficerPresentationContext> VisibleActors { get; init; } = ImmutableDictionary<int, TalentOfficerPresentationContext>.Empty;
    public int? HighlightedSlot { get; init; }
    //These are independent retained layout states, including hidden slots.
    //The source layout constructors start bright; old saves migrate to null.
    public ImmutableArray<bool?>? PortraitDarkFlags { get; init; } = [false, false, false, false, false];
    public static CampaignStoryScene Empty => new(null, null, null, null, null, null, null);
    public CampaignStoryScene Apply(CampaignStoryStep step) => step switch
    {
        StoryBackground b => this with { Background = b,
            Blackout = b.FadeIn ? new StoryTransition(true, -1) : Blackout },
        StoryStanding s => this with { Standing = s, PortraitDarkFlags = [true, true, true, true, true] },
        StoryResetStandingRegistration => this with { Standing = null },
        StoryIllustration i => this with { Illustration = i,
            VisibleActors = i.IllustrationId != 0 ? ImmutableDictionary<int, TalentOfficerPresentationContext>.Empty : VisibleActors,
            HighlightedSlot = i.IllustrationId != 0 ? null : HighlightedSlot },
        StorySmallIllustration i => this with { SmallIllustration = i,
            VisibleActors = i.IllustrationId != 0 ? ImmutableDictionary<int, TalentOfficerPresentationContext>.Empty : VisibleActors,
            HighlightedSlot = i.IllustrationId != 0 ? null : HighlightedSlot },
        StoryTutorialIllustration i => this with { TutorialIllustration = i,
            VisibleActors = i.IllustrationId != 0 ? ImmutableDictionary<int, TalentOfficerPresentationContext>.Empty : VisibleActors,
            HighlightedSlot = i.IllustrationId != 0 ? null : HighlightedSlot },
        StoryItemIllustration i => this with { ItemIllustration = i,
            VisibleActors = i.IllustrationId.HasValue ? ImmutableDictionary<int, TalentOfficerPresentationContext>.Empty : VisibleActors,
            HighlightedSlot = i.IllustrationId.HasValue ? null : HighlightedSlot },
        StoryFormatOnlyMessage or StoryHideMessage => this with { Text = null, PreparedText = null },
        StoryHideCharacters hide => HideCharacters(hide.Characters),
        StoryTransition t => this with { Blackout = t,
            Standing = !t.FadeIn ? null : Standing,
            Text = !t.FadeIn ? null : Text,
            PreparedText = !t.FadeIn ? null : PreparedText,
            VisibleActors = !t.FadeIn ? ImmutableDictionary<int, TalentOfficerPresentationContext>.Empty : VisibleActors,
            HighlightedSlot = !t.FadeIn ? null : HighlightedSlot },
        StoryCamera c => this with { Camera = c, RestoredCamera = null,
            Blackout = new StoryTransition(false, -1), Standing = null, Text = null, PreparedText = null,
            VisibleActors = ImmutableDictionary<int, TalentOfficerPresentationContext>.Empty, HighlightedSlot = null },
        StoryRestoreCampaignCamera r => this with { RestoredCamera = r.Snapshot },
        StoryMessage or StoryNarration or StoryLandmarkRewardNotice => this with { Text = step },
        StoryAudio a when a.AudioKind is StoryAudioKind.Music or StoryAudioKind.DirectMusic or StoryAudioKind.FadeOutMusic => this with { Music = a },
        _ => this
    };
    private CampaignStoryScene HideCharacters(int[] characters)
    {
        var visible = characters.Length == 0 ? ImmutableDictionary<int, TalentOfficerPresentationContext>.Empty :
            VisibleActors.Where(pair => !characters.Contains(pair.Value.OfficerId)).ToImmutableDictionary();
        return this with { VisibleActors = visible,
            HighlightedSlot = HighlightedSlot is int slot && visible.ContainsKey(slot) ? slot : null };
    }
}

/// <summary>Resource identities and bound presentation semantics. No instruction stream or execution receipt.</summary>
public sealed record CampaignStoryRequest(long Sequence, EventSourceReference Source, string ContentRevision,
    CampaignStoryStep[] Steps, int NextStep)
{
    [JsonRequired] public CampaignStoryScene Scene { get; init; } = CampaignStoryScene.Empty;
    [JsonRequired] public CampaignStorySelectionContext? SelectionContext { get; init; }
    public CampaignStoryPreparedStep? PreparedStep { get; init; }
    public ImmutableArray<CampaignStoryPreparedText> TextLog { get; init; } = ImmutableArray<CampaignStoryPreparedText>.Empty;
    public CampaignStoryRequest CompleteStep(CampaignStoryStepReceipt? receipt = null)
    {
        if (NextStep >= Steps.Length) throw new InvalidDataException("Story has no waiting step.");
        if (receipt is not null && (receipt.RequestSequence != Sequence || receipt.Step != NextStep))
            throw new InvalidDataException("Story completion does not identify the waiting step.");
        var step = Steps[NextStep];
        if (step is StoryFormatOnlyMessage &&
            (PreparedStep is not { Text: { FormatOnly: true } } || PreparedStep.Step != NextStep))
            throw new InvalidDataException("A format-only story message requires its actual prepared formatter result.");
        var scene = Scene.Apply(step);
        if (step is StoryOwnedEffect effect)
        {
            var result = receipt?.OwnedEffectResult
                ?? throw new InvalidDataException("Owned story effects require the actual presentation host's completed slot snapshot.");
            ValidateOwnedEffects(result.Slots);
            if (effect.Operation == StoryOwnedEffectOperation.StopAll && !result.Slots.IsEmpty)
                throw new InvalidDataException("Stopping all owned story effects requires an empty completed slot snapshot.");
            scene = scene with { OwnedEffects = result.Slots };
        }
        else if (receipt?.OwnedEffectResult is not null)
            throw new InvalidDataException("This story step does not execute an owned effect operation.");
        var textLog = TextLog;
        if (step is StoryMessage or StoryNarration)
        {
            var text = PreparedStep?.Text;
            if (text is not null) textLog = textLog.Add(text);
            scene = scene with { PreparedText = text, HighlightedSlot = null };
            if (step is StoryMessage && text is not null)
                scene = scene with { RetainedSpeakerName = text.SpeakerName };
            if (step is StoryMessage { SpeechOnly: false } message)
            {
                int slot = 0;
                if (scene.Standing is { } standing)
                {
                    int index = Array.FindIndex(standing.Characters, character => character.Available && character.CharacterId == message.Speaker);
                    if (index >= 0) slot = standing.Characters.Length == 2 ? index + 3 : index switch { 0 => 1, 1 => 0, _ => 2 };
                }
                if (message.WithoutPortrait)
                    scene = scene with { VisibleActors = scene.VisibleActors.Remove(slot), HighlightedSlot = null };
                else if (text?.Speaker is { } speaker)
                    scene = scene with { VisibleActors = scene.VisibleActors.SetItem(slot, speaker), HighlightedSlot = slot,
                        PortraitDarkFlags = Enumerable.Range(0, 5).Select(index => (bool?)(index != slot)).ToImmutableArray() };
            }
        }
        return this with { Scene = scene, NextStep = NextStep + 1, PreparedStep = null, TextLog = textLog };
    }
    public CampaignStoryRequest Copy() => this with { Steps = Steps.Select(CloneStep).ToArray(), Scene = Scene with
        { Standing = Scene.Standing is { } s ? s with { Characters = s.Characters.ToArray() } : null,
          Text = Scene.Text is null ? null : CloneStep(Scene.Text) } };
    private static CampaignStoryStep CloneStep(CampaignStoryStep step) => step switch
    {
        StoryStanding standing => standing with { Characters = standing.Characters.ToArray() },
        StoryPeople people => people with { Characters = people.Characters.ToArray() },
        StoryNames names => names with { Names = names.Names.ToArray() },
        StoryMessage message => message with { People = message.People.ToArray(), Names = message.Names?.ToArray(),
            Cities = message.Cities?.ToArray(), Forces = message.Forces?.ToArray(), Areas = message.Areas?.ToArray(),
            Items = message.Items?.ToArray(), Numbers = message.Numbers?.ToArray(), Provinces = message.Provinces?.ToArray() },
        StoryNarration narration => narration with { People = narration.People.ToArray(), Names = narration.Names?.ToArray(),
            Cities = narration.Cities?.ToArray(), Forces = narration.Forces?.ToArray(), Areas = narration.Areas?.ToArray(),
            Items = narration.Items?.ToArray(), Numbers = narration.Numbers?.ToArray(), Provinces = narration.Provinces?.ToArray() },
        StoryFormatOnlyMessage message => message with { People = message.People.ToArray(), Names = message.Names?.ToArray(),
            Cities = message.Cities?.ToArray(), Forces = message.Forces?.ToArray(), Areas = message.Areas?.ToArray(),
            Items = message.Items?.ToArray(), Numbers = message.Numbers?.ToArray(), Provinces = message.Provinces?.ToArray() },
        StoryHideCharacters hide => hide with { Characters = hide.Characters.ToArray() },
        _ => step
    };
    public void Validate(bool allowEmpty = false)
    {
        PlayableEventStateValidation.ValidateSource(Source);
        if (Scene is null || Sequence < 1 || string.IsNullOrWhiteSpace(ContentRevision) || Steps is null || (!allowEmpty && Steps.Length == 0) ||
            NextStep < 0 || NextStep > Steps.Length || Steps.Any(s => s is null) || TextLog.IsDefault)
            throw new InvalidDataException("Saved story request is incomplete.");
        if (PreparedStep is { } prepared && (prepared.Step != NextStep || NextStep >= Steps.Length ||
            (Steps[NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage) != (prepared.Text is not null) ||
            prepared.Text is { } currentText && (currentText.SpeechOnly != (Steps[NextStep] is StoryMessage { SpeechOnly: true }) ||
                currentText.FormatOnly != (Steps[NextStep] is StoryFormatOnlyMessage))) ||
            Scene.VisibleActors is null || Scene.VisibleActors.Any(pair => pair.Key is < 0 or > 4 || !CampaignStoryPreparationValidation.Speaker(pair.Value)) ||
            Scene.HighlightedSlot is int slot && !Scene.VisibleActors.ContainsKey(slot))
            throw new InvalidDataException("Saved prepared story step or visible actors are inconsistent.");
        CampaignStoryPreparationValidation.ValidateText(PreparedStep?.Text);
        CampaignStoryPreparationValidation.ValidateText(Scene.PreparedText);
        if (Scene.PortraitDarkFlags is { } dark && (dark.IsDefault || dark.Length != 5))
            throw new InvalidDataException("Saved Story portrait layout states are invalid.");
        if (Scene.OwnedEffects is { } effects) ValidateOwnedEffects(effects);
        if (Scene.PreparedText?.FormatOnly == true || Scene.Text is StoryFormatOnlyMessage)
            throw new InvalidDataException("A format-only message cannot be displayed in the saved story scene.");
        foreach (var text in TextLog)
        {
            if (text is null || text.FormatOnly) throw new InvalidDataException("Saved story text log contains an undisplayed entry.");
            CampaignStoryPreparationValidation.ValidateText(text);
        }
        foreach (var step in Steps) ValidateStep(step);
        if (Scene.Background is { } background) ValidateStep(background);
        if (Scene.SmallIllustration is { } small) ValidateStep(small);
        if (Scene.TutorialIllustration is { } tutorial) ValidateStep(tutorial);
        if (Scene.ItemIllustration is { } item) ValidateStep(item);
        if (Scene.Text is { } previousText) ValidateStep(previousText);
    }
    private static void ValidateStep(CampaignStoryStep step)
    {
        switch (step)
            {
                case StoryStanding s when s.Characters is not { Length: 2 or 3 } ||
                    s.Characters.Any(p => p is null || p.Available != p.CharacterId.HasValue):
                case StoryPeople p when p.Characters is not { Length: 5 } || p.Characters.Any(i => i is < 0 or > ushort.MaxValue):
                case StoryNames n when n.Names is not { Length: 5 } || n.Names.Any(n => n is null):
                case StoryMessage m when m.People is not { Length: 5 } || m.People.Any(i => i is < 0 or > ushort.MaxValue) ||
                    m.Names is not null && m.Names.Length != 5 ||
                    !CitiesValid(m.Cities) || !CitiesValid(m.Forces) || !CitiesValid(m.Areas) ||
                    !CampaignStoryMessageBanks.IsValid(m.Items, m.Numbers, m.Provinces):
                case StoryNarration narration when narration.People is not { Length: 5 } || narration.People.Any(i => i is < 0 or > ushort.MaxValue) ||
                    narration.Names is not null && narration.Names.Length != 5 ||
                    !CitiesValid(narration.Cities) || !CitiesValid(narration.Forces) || !CitiesValid(narration.Areas) ||
                    !CampaignStoryMessageBanks.IsValid(narration.Items, narration.Numbers, narration.Provinces):
                case StoryFormatOnlyMessage message when message.People is not { Length: 5 } || message.People.Any(i => i is < 0 or > ushort.MaxValue) ||
                    message.Names is not null && message.Names.Length != 5 ||
                    !CitiesValid(message.Cities) || !CitiesValid(message.Forces) || !CitiesValid(message.Areas) ||
                    !CampaignStoryMessageBanks.IsValid(message.Items, message.Numbers, message.Provinces):
                case StoryPoemEvent poem when poem.RawId is < 1 or > 20:
                case StoryItemIllustration item when item.IllustrationId is < 0 or > 456:
                case StoryOwnedEffect effect when !Enum.IsDefined(effect.Operation) ||
                    (effect.Operation == StoryOwnedEffectOperation.Create
                        ? effect.EffectId is < 118 or > 129 || effect.SelectorMode is not (1 or 2)
                        : effect.EffectId != 0 || effect.SelectorMode != 0):
                case StoryBackground background when background.ResourceKind is not (14 or 15):
                case StoryHideCharacters hide when hide.Characters is null || hide.Characters.Length > 7 ||
                    hide.Characters.Any(id => id is <= 0 or > ushort.MaxValue):
                case StoryAudio a when !Enum.IsDefined(a.AudioKind) || !float.IsFinite(a.ScalarA) || !float.IsFinite(a.ScalarB):
                case StoryCamera c when c.WaitMilliseconds < 0:
                case StoryRestoreCampaignCamera r when r.Snapshot is null:
                    throw new InvalidDataException("Saved story semantics are incomplete.");
            }
    }
    private static bool CitiesValid(int[]? cities) => cities is null ||
        cities.Length == 5 && cities.All(id => id is >= 0 and <= ushort.MaxValue);
    private static void ValidateOwnedEffects(ImmutableArray<StoryOwnedEffectSlot> slots)
    {
        if (slots.IsDefault || slots.Length > 3 || slots.Any(slot => slot is null || slot.Slot is < 0 or > 2 ||
                slot.EffectId is < 118 or > 129 || slot.SelectorMode is not (1 or 2)) ||
            slots.Select(slot => slot.Slot).Distinct().Count() != slots.Length)
            throw new InvalidDataException("Saved owned story effect slots are incomplete or invalid.");
    }
}
public sealed record CampaignStoryStepReceipt(long RequestSequence, int Step)
{
    public StoryOwnedEffectReceipt? OwnedEffectResult { get; init; }
}
