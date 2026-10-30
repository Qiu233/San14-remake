using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A real event environment with a private playback cursor. It owns
/// no campaign operation, authority, occurrence writer or simulated receipt.</summary>
public sealed class EventStorySession
{
    private readonly LoadedEvents events;
    private readonly int[] eventIds;
    private readonly IS14AudioEventExecutionState audio;
    private int eventIndex = -1;
    private CampaignEventExecutionProgram? program;
    private CampaignStoryExecutionCursor? executionCursor;
    public GameState World { get; private set; }
    public ArmyCampaignRuleAdapter Rules { get; }
    public CampaignStoryRequest? Story { get; private set; }
    public string SceneKey { get; } = "event-story:" + Guid.NewGuid().ToString("N");
    public int ExecutionMode { get; }
    public int VariantFlag { get; }
    public int? CurrentEventId => eventIndex >= 0 && eventIndex < eventIds.Length ? eventIds[eventIndex] : null;
    public bool Completed => eventIndex >= eventIds.Length;
    public string? UnresolvedOwner { get; private set; }

    private EventStorySession(LoadedEvents events, GameState world, ArmyCampaignRuleAdapter rules,
        IReadOnlyList<int> eventIds, int executionMode, int variantFlag, IS14AudioEventExecutionState audio)
    {
        if (eventIds.Count == 0 || eventIds.Any(id => id is < 0 or >= 1024))
            throw new ArgumentOutOfRangeException(nameof(eventIds));
        if (variantFlag is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(variantFlag));
        if (executionMode != 3)
            throw new InvalidDataException("collection.event.playback_mode_requires_collection_context");
        this.events = events; World = world; Rules = rules;
        this.eventIds = eventIds.ToArray(); ExecutionMode = executionMode; VariantFlag = variantFlag;
        this.audio = audio;
    }

    /// <summary>Creates the Collection playback context (packet mode3,
    /// forced If variant). Ordinary and Ending playback retain their own
    /// domain owners and must not use this title-only execution context.</summary>
    public static EventStorySession Create(GameResourceRepository repository, GameState world,
        ArmyCampaignRuleAdapter rules, IReadOnlyList<int> eventIds, int executionMode,
        int variantFlag, IS14AudioEventExecutionState audio)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules); ArgumentNullException.ThrowIfNull(audio);
        return CreateLoaded(repository.LoadEvents(CampaignEventCatalog.SourcePath), world, rules,
            eventIds, executionMode, variantFlag, audio);
    }

    internal static EventStorySession CreateLoaded(LoadedEvents events, GameState world,
        ArmyCampaignRuleAdapter rules, IReadOnlyList<int> eventIds, int executionMode,
        int variantFlag, IS14AudioEventExecutionState audio)
        => new(events, world, rules, eventIds, executionMode, variantFlag, audio);

    /// <summary>Starts the next original script only after the prior script's
    /// actual last receipt. An unresolved script keeps its place for retry.</summary>
    public bool MoveNextEvent()
    {
        if (Completed || Story is { } previous && previous.NextStep < previous.Steps.Length) return false;
        bool resume=executionCursor is {IsCompleted:false};
        int next=resume?eventIndex:eventIndex+1;
        if(next>=eventIds.Length)
        {eventIndex=next;Story=null;program=null;executionCursor=null;UnresolvedOwner=null;return true;}
        try
        {
            var selected=resume?program??throw new InvalidDataException("event.story.resume_program")
                :CampaignEventExecutionProgram.ReadLoadedEvent(events,eventIds[next]);
            var slice=Rules.PrepareCollectionEventStorySliceCurrent(World,selected,checked(next+1L),
                ExecutionMode,VariantFlag,audio,resume?Story:null,resume?executionCursor:null);
            //A failed formatter never commits a partial Name bank, RNG, index
            //or cursor. Only the successfully prepared source slice is adopted.
            eventIndex=next;program=selected;executionCursor=slice.Cursor;
            World=slice.State;Story=slice.Story;UnresolvedOwner=null;return true;
        }
        catch(InvalidDataException error){UnresolvedOwner=error.Message;return false;}
    }

    /// <summary>Formats once on this exact story and publishes only its App
    /// state and small prepared text owner. Reopening a page uses this cache.</summary>
    public CampaignStoryPreparationResult PrepareCurrentStep()
    {
        if (Story is not { } story || story.NextStep >= story.Steps.Length)
            return new(null, null, "event.story.current_step");
        if (story.PreparedStep is { } cached) return new(cached, World.CampaignAppRandom32, null);
        var result = Rules.PrepareNormalTurnStoryStep(World, story);
        if (result.UnresolvedOwner is null && result.Prepared is { } prepared && prepared.Step == story.NextStep)
        {
            if (story.Steps[story.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage && prepared.Text is null)
                return result with { UnresolvedOwner = "event.story.prepared_text" };
            World = World with { CampaignAppRandom32 = result.AppRandomState ?? World.CampaignAppRandom32 };
            Story = story with { PreparedStep = prepared }; UnresolvedOwner = null;
        }
        else UnresolvedOwner = result.UnresolvedOwner ?? "event.story.prepared_step";
        return result;
    }

    public bool CompleteStep(CampaignStoryRequest expectedOwner, CampaignStoryStepReceipt receipt)
    {
        if (!ReferenceEquals(Story, expectedOwner) || receipt.RequestSequence != expectedOwner.Sequence ||
            receipt.Step != expectedOwner.NextStep || expectedOwner.NextStep >= expectedOwner.Steps.Length)
            return false;
        if (expectedOwner.Steps[expectedOwner.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage &&
            expectedOwner.PreparedStep?.Text is null) return false;
        Story = expectedOwner.CompleteStep(receipt);
        return true;
    }
}

