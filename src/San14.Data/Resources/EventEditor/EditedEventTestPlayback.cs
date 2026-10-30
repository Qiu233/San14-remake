using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The contents editor's test1 invocation. Only detached working contents
/// and the user article bank are executed; no conditions, results or occurrence writer exist.</summary>
public sealed class EditedEventTestPlayback
{
    private readonly EditedEventTestContext context;
    private readonly EditedEventLibrary articles;
    public CampaignStoryRequest Story { get; private set; }
    public bool Completed => Story.NextStep == Story.Steps.Length;
    public string? UnresolvedOwner { get; private set; }

    private EditedEventTestPlayback(EditedEventTestContext context, EditedEventLibrary articles,
        CampaignStoryRequest story) => (this.context, this.articles, Story) = (context, articles, story);

    public static EditedEventTestPlayback Create(int slot, IReadOnlyList<EditedEventContent> workingContents,
        EditedEventLibrary articles, string libraryUri, string revision, EditedEventTestContext context,
        int settingPredicateValue)
    {
        ArgumentNullException.ThrowIfNull(workingContents); ArgumentNullException.ThrowIfNull(articles);
        ArgumentNullException.ThrowIfNull(context);
        if (workingContents.Count > EditedEventLibrary.ContentCapacity)
            throw new InvalidDataException("event_editor.test.content_capacity");
        var statements = ImmutableArray.CreateBuilder<EditedEventInstruction>();
        foreach (var content in workingContents)
        {
            // This also checks the actual six-statement language, without requiring
            // unrelated draft name, conditions or results to be commit-ready.
            _ = EditedEventContentEditing.Read(content);
            statements.AddRange(content.Program.Instructions.Select(instruction => instruction with { Suppression = 0 }));
        }
        //399970 clears imported suppression, then suppresses the preceding85
        //background when a later410 selects a nonzero kind14 background.
        int lastBackground = -1;
        for (int index = 0; index < statements.Count; index++)
        {
            var instruction = statements[index];
            if (instruction.Code == 85) lastBackground = index;
            if (instruction.Code == 410 && instruction.Arguments[0] > 0 && instruction.Arguments[1] == 14 && lastBackground >= 0)
                statements[lastBackground] = statements[lastBackground] with { Suppression = 1 };
        }
        var source = new EventSourceReference(libraryUri, revision, slot, slot);
        var compiled = EditedEventProgramCompiler.Compile(new(statements.ToImmutable()), slot);
        var program = CampaignEventExecutionProgram.ReadEditedProgram(source, compiled);
        // Fresh83C100 has its fast/skip latch clear and the edited context1D8
        //clear. The user setting predicate remains an actual application input.
        var plan = CampaignEventExecution.Stage(program, context,
            _ => new(false, settingPredicateValue, false), new CampaignNormalEventAudioInputs(context.Locale));
        return new(context, articles, CampaignStorySemantics.Bind(plan, 1, allowEmpty: true));
    }

    public CampaignStoryPreparationResult PrepareCurrentStep()
    {
        if (Completed) return new(null, null, "event_editor.test.current_step");
        if (Story.PreparedStep is { } cached) return new(cached, null, null);
        try
        {
            var step = Story.Steps[Story.NextStep];
            CampaignStoryPreparedText? text = null;
            if (step is StoryMessage or StoryNarration)
            {
                int slot = step is StoryMessage message ? message.MessageId : ((StoryNarration)step).MessageId;
                string article = EditedEventArticleText.Format(articles.FindMessage(slot)?.Text ?? string.Empty);
                var speaker = step is StoryMessage person ? context.ReadSpeaker(person.Speaker) : null;
                text = new(speaker?.NameAtSource ?? string.Empty, article, ImmutableArray.Create(article), speaker, []);
            }
            var prepared = new CampaignStoryPreparedStep(Story.NextStep, text);
            Story = Story with { PreparedStep = prepared }; UnresolvedOwner = null;
            return new(prepared, null, null);
        }
        catch (InvalidDataException error)
        { UnresolvedOwner = error.Message; return new(null, null, error.Message); }
    }

    public bool CompleteStep(CampaignStoryRequest expectedOwner, CampaignStoryStepReceipt receipt)
    {
        if (!ReferenceEquals(Story, expectedOwner) || Completed || receipt.RequestSequence != Story.Sequence ||
            receipt.Step != Story.NextStep) return false;
        if (Story.Steps[Story.NextStep] is StoryMessage or StoryNarration && Story.PreparedStep?.Text is null) return false;
        Story = Story.CompleteStep(receipt); return true;
    }

    public AreaAdministrationQuery<StoryPresentationPublication> QueryPresentation()
    {
        var step = Completed ? null : Story.Steps[Story.NextStep];
        var standing = step as StoryStanding ?? Story.Scene.Standing;
        int[] slots = standing?.Characters.Length == 2 ? [3, 4] : [1, 0, 2];
        var registered = standing is null ? Array.Empty<StoryStandingRegistration>() :
            standing.Characters.Select((person, index) => new StoryStandingRegistration(slots[index],
                person.Available ? person.CharacterId : null)).ToArray();
        var actors = new SortedDictionary<int, TalentOfficerPresentationContext>(Story.Scene.VisibleActors);
        var prepared = Story.PreparedStep?.Text;
        bool currentText = prepared is not null && step is StoryMessage or StoryNarration;
        int? highlighted = Story.Scene.HighlightedSlot;
        if (currentText && step is StoryMessage message && prepared?.Speaker is { } speaker)
        {
            int slot = registered.FirstOrDefault(person => person.OfficerId == message.Speaker)?.Slot ?? 0;
            actors[slot] = speaker; highlighted = slot;
        }
        else if (currentText) highlighted = null;
        var publication = actors.Select(pair => context.PublishActor(pair.Key, pair.Value)).ToArray();
        return AreaAdministrationQuery<StoryPresentationPublication>.Known(new(Story.Sequence, Story.NextStep,
            prepared ?? Story.Scene.PreparedText, registered, publication, highlighted) { HasCurrentPreparedText = currentText });
    }
}
