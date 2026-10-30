using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record CampaignEventExecutionSlice(CampaignEventExecutionPlan Plan, CampaignStoryExecutionCursor Cursor);

public static partial class CampaignEventExecution
{
    private static StagingRuntime CreateRuntime(CampaignEventExecutionProgram program,
        IS14PlayerEventConditionQueries persons, Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        IS14AudioEventExecutionState? audioInputs, IReadOnlyList<int>? initialPersonParameters,
        CampaignEventExecutionInputs? storyInputs)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(persons);
        ArgumentNullException.ThrowIfNull(dispatchInputs);
        if (storyInputs is not null && !ReferenceEquals(storyInputs.Persons, persons))
            throw new InvalidDataException("Story queries must be the same detached current person snapshot.");
        initialPersonParameters ??= storyInputs?.InitialPersonParameters;
        if (storyInputs?.InitialNameParameters is { Count: not 5 })
            throw new InvalidDataException("Story name parameters require five current strings.");
        if (initialPersonParameters is not null &&
            (initialPersonParameters.Count != 5 || initialPersonParameters.Any(id => id is < 0 or > ushort.MaxValue)))
            throw new InvalidDataException("Original message person parameters require five known word IDs.");
        // All source instructions and dependencies are checked before visiting the first action.
        if (program.DispatchProgram.Operations.Any(operation => (operation.Code == 92 || operation.Code == 91 && !program.IsEditedProgram)) && audioInputs is null)
            throw new InvalidDataException("Complete original event execution requires current audio inputs.");
        var messagePeople = storyInputs?.InitialMessagePeople ?? initialPersonParameters?.Select(id => (int?)id).ToArray();
        if (messagePeople is not null && (messagePeople.Count != 5 || messagePeople.Any(id => id is < 0 or > ushort.MaxValue)))
            throw new InvalidDataException("Original message person bank requires five nullable word IDs.");
        if (storyInputs?.InitialCityParameters is { } cities &&
            (cities.Count != 5 || cities.Any(id => id is < 0 or > ushort.MaxValue)))
            throw new InvalidDataException("Original message city bank requires five word IDs.");
        foreach (var bank in new[] { storyInputs?.InitialForceParameters, storyInputs?.InitialAreaParameters })
            if (bank is not null && (bank.Count != 5 || bank.Any(id => id is < 0 or > ushort.MaxValue)))
                throw new InvalidDataException("Original message force/area banks require five word IDs.");
        if (storyInputs?.InitialItemParameters is { } items &&
            (items.Count != 5 || items.Any(id => id is < 0 or > 201)))
            throw new InvalidDataException("Story Item parameters require five native item slots.");
        if (storyInputs?.InitialProvinceParameters is { } provinces &&
            (provinces.Count != 10 || provinces.Any(province => province is < 0 or > 22)))
            throw new InvalidDataException("Story Province parameters require ten nullable current State ordinals.");
        if (storyInputs?.InitialNumberParameters is { Count: not 5 })
            throw new InvalidDataException("Story Number parameters require five nullable signed integers.");
        var runtime = new StagingRuntime(persons, dispatchInputs, audioInputs, messagePeople, storyInputs, program.IsEditedProgram);
        if (storyInputs is not null)
        {
            runtime.BindStoryCityTargets();
            runtime.BindStoryActorSelections();
        }
        return runtime;
    }

    private static CampaignEventExecutionPlan CreatePlan(CampaignEventExecutionProgram program,
        StagingRuntime runtime, S14EventConditionResult result)
    {
        return new(program, Array.AsReadOnly(runtime.Commands.ToArray()), result,
            runtime.Parameters is null ? null : Array.AsReadOnly(runtime.Parameters.ToArray()),
            runtime.Names, runtime.SelectedSpeakers)
            { FinalCityParameters = runtime.CityParameters, FinalForceParameters = runtime.ForceParameters,
                FinalAreaParameters = runtime.AreaParameters, FinalItemParameters = runtime.ItemParameters,
                FinalNumberParameters = runtime.NumberParameters, FinalProvinceParameters = runtime.ProvinceParameters, FinalDialogueCast = runtime.DialogueCast,
                SelectedTargetForcePhysicalSlots = Array.AsReadOnly(Enumerable.Range(0, 4)
                    .Select(index => runtime.TargetForcePhysicalSlot(-2 - index)).ToArray()) };
    }

    internal static CampaignEventExecutionSlice StageUntilRelationship(CampaignEventExecutionProgram program,
        CampaignEventExecutionInputs inputs, Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        IS14AudioEventExecutionState audio, CampaignStoryExecutionCursor? cursor,
        Func<int, int, int, int, int> relationship,
        Func<S14ConditionInstruction, CampaignEventExecutionContext, Func<int, S14EventConditionForceReference?>, int?>? business = null,
        Action<int>? recordAggregateResult = null)
    {
        if (cursor?.PersonListContinuation is not null && (cursor.Frames.IsDefaultOrEmpty ||
            cursor.Frames[^1].Cursor >= program.DispatchProgram.Operations.Count ||
            program.DispatchProgram.Operations[cursor.Frames[^1].Cursor].Code != 237))
            throw new InvalidDataException("story.person_list.resume_instruction");
        if (cursor?.ForceRankContinuation is { } rank && (cursor.Frames.IsDefaultOrEmpty ||
            cursor.Frames[^1].Cursor >= program.DispatchProgram.Operations.Count ||
            program.DispatchProgram.Operations[cursor.Frames[^1].Cursor].Code != 120 ||
            rank.OperationIndex != cursor.Frames[^1].Cursor))
            throw new InvalidDataException("story.force_rank.resume_instruction");
        if (cursor?.SinglePersonDeathContinuation is not null && (cursor.Frames.IsDefaultOrEmpty ||
            cursor.Frames[^1].Cursor >= program.DispatchProgram.Operations.Count ||
            program.DispatchProgram.Operations[cursor.Frames[^1].Cursor].Code != 162))
            throw new InvalidDataException("story.single_person_death.resume_instruction");
        var runtime = CreateRuntime(program, inputs.Persons, dispatchInputs, audio, null, inputs);
        runtime.RelationshipOperation = relationship;
        runtime.BusinessOperation = business;
        runtime.RecordAggregateResult = recordAggregateResult;
        if (cursor is not null) runtime.Restore(cursor);
        var checkpoint = cursor is null ? null : new S14EventExecutionCheckpoint(cursor.Frames.Select(frame =>
            new S14EventExecutionFrame(frame.Begin, frame.End, frame.All, frame.Depth, frame.OrDepth, frame.Cursor,
                frame.Fold, frame.PendingGroup, frame.IfPhase, frame.ThenMarker, frame.ElseMarker, frame.SelectedThen))
                .ToImmutableArray(), cursor.SuccessCount, cursor.EvaluatedCount, cursor.RootFold);
        var slice = S14EventConditionEvaluator.EvaluateUntilYield(program.DispatchProgram, runtime, checkpoint,
            () => false, context => runtime.Commands.Count != 0 &&
                (context.Operation.Code is 419 or 420 || IsStoryWorldMutation(context.Operation.Code)),
            () => runtime.IsLeafIncomplete);
        return new(CreatePlan(program, runtime, slice.Result), runtime.Capture(slice.Checkpoint));
    }

    private sealed partial class StagingRuntime
    {
        internal Func<int, int, int, int, int>? RelationshipOperation { get; set; }
        internal Func<S14ConditionInstruction, CampaignEventExecutionContext,
            Func<int, S14EventConditionForceReference?>, int?>? BusinessOperation { get; set; }
        internal Action<int>? RecordAggregateResult { get; set; }
        internal bool IsLeafIncomplete { get; private set; }

        internal void BindStoryActorSelections() => conditionRuntime.BindStoryActorResolver(
            selector => Story.ResolvePerson(selector, conditionRuntime.ReadMessageTargetForce));

        private int ExecuteWorldMutation(S14ConditionInstruction operation)
        {
            var invoke = BusinessOperation ?? throw new InvalidDataException("story.person_list_business_owner");
            int? result = invoke(operation, Story, conditionRuntime.ReadMessageTargetForce);
            IsLeafIncomplete = result is null;
            return result.GetValueOrDefault(); //Ignored by the evaluator while incomplete.
        }

        internal void Restore(CampaignStoryExecutionCursor cursor)
        {
            Parameters = cursor.MessageContext.People.ToArray();
            Names = cursor.MessageContext.Names?.ToArray();
            cityParameters = cursor.MessageContext.Cities?.ToArray();
            forceParameters = cursor.MessageContext.Forces?.ToArray();
            areaParameters = cursor.MessageContext.Areas?.ToArray();
            itemParameters = cursor.MessageContext.Items?.ToArray();
            numberParameters = cursor.MessageContext.Numbers?.ToArray();
            provinceParameters = cursor.MessageContext.Provinces?.ToArray();
            Story.RestoreExecutionContext(cursor);
            Story.PersonListContinuation = cursor.PersonListContinuation;
            Story.ForceRankContinuation = cursor.ForceRankContinuation;
            Story.SinglePersonDeathContinuation = cursor.SinglePersonDeathContinuation;
            Story.RestoreStoryAreaSelections(cursor.ConditionAreaPhysicalSlots);
            Story.RestoreStoryForceSelections(cursor.ConditionForceListPhysicalSlots);
            Story.RestoreStoryMoveCities(cursor.MoveCityPhysicalSlots is { } moveCities
                ? moveCities.Select(city => city is null ? null : new S14EventConditionCityPointer(city.PhysicalSlot)).ToArray() : null);
            conditionRuntime.RestoreStoryTargetCities(cursor.SelectedCityPhysicalSlots is { } targetCities
                ? targetCities.Select(city => city is null ? null : new S14EventConditionCityPointer(city.PhysicalSlot)).ToArray()
                : null);
            conditionRuntime.RestoreStorySelections(cursor.ConditionPersonPhysicalSlots, cursor.ConditionCityPhysicalSlots,
                cursor.ConditionForcePhysicalSlots, cursor.ConditionBlocked);
        }

        internal CampaignStoryExecutionCursor Capture(S14EventExecutionCheckpoint checkpoint)
        {
            var selection = conditionRuntime.CaptureStorySelections();
            return new(checkpoint.Frames.Select(frame => new CampaignStoryDispatchFrame(frame.Begin, frame.End,
                frame.All, frame.Depth, frame.OrDepth, frame.Cursor, frame.Fold, frame.PendingGroup, frame.IfPhase,
                frame.ThenMarker, frame.ElseMarker, frame.SelectedThen)).ToImmutableArray(),
                checkpoint.SuccessCount, checkpoint.EvaluatedCount, checkpoint.RootFold,
                new(Parameters?.ToArray() ?? new int?[5], Names?.ToArray(), cityParameters?.ToArray())
                { Forces = forceParameters?.ToArray(), Areas = areaParameters?.ToArray(),
                    Items = itemParameters?.ToArray(), Numbers = numberParameters?.ToArray(), Provinces = provinceParameters?.ToArray() },
                Story.Candidates.Select(person => person.PhysicalSlot).ToImmutableArray(), Story.CaptureStoryActors(),
                Story.CaptureStoryDialogue(), Story.PersonListBlocked, selection.People.ToImmutableArray(),
                selection.Cities.ToImmutableArray(), selection.Forces.ToImmutableArray(), selection.Blocked) { PersonListContinuation = Story.PersonListContinuation, ForceRankContinuation = Story.ForceRankContinuation, SinglePersonDeathContinuation = Story.SinglePersonDeathContinuation, ConditionAreaPhysicalSlots = Story.CaptureStoryAreaSelections(),
                ConditionForceListPhysicalSlots = Story.CaptureStoryForceSelections(),
                MoveCityPhysicalSlots = Story.CaptureStoryMoveCities()
                    .Select(city => city is null ? null : new CampaignStoryCitySelection(city.PhysicalSlot)).ToImmutableArray(),
                SelectedCityPhysicalSlots = conditionRuntime.CaptureStoryTargetCities()
                    .Select(city => city is null ? null : new CampaignStoryCitySelection(city.PhysicalSlot)).ToImmutableArray() };
        }
    }
}
