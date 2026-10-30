using System.Security.Cryptography;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A complete, immutable original presentation stream; it executes no native code.</summary>
public sealed partial class CampaignEventExecutionProgram
{
    public EventSourceReference Source { get; }
    public S14EventConditionProgram DispatchProgram { get; }
    public string StreamSha256 { get; }
    public string Revision => Source.ResourceSha256 + ":" + StreamSha256;

    private CampaignEventExecutionProgram(EventSourceReference source,
        S14EventConditionProgram dispatch)
    {
        Source = source;
        DispatchProgram = dispatch;
        StreamSha256 = Convert.ToHexString(SHA256.HashData(
            dispatch.Operations.SelectMany(operation => operation.RawBytes).ToArray()));
    }

    public static CampaignEventExecutionProgram ReadEvent(LoadedEvents loaded, int eventId)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        S14EventDocument canonical = S14EventReader.Read(loaded.Document.RawBytes);
        if (canonical.SourceSha256 != loaded.Document.SourceSha256 ||
            !canonical.SourceSha256.Equals(loaded.Source.Sha256, StringComparison.OrdinalIgnoreCase) ||
            canonical.RawBytes.LongLength != loaded.Source.Bytes)
            throw new InvalidDataException("Original event execution source bytes or provenance differ.");
        if (canonical.Events.Count(row => row.Id == eventId) != 1)
            throw new InvalidDataException("Original execution requires one actual event row.");
        long slot = (long)eventId - canonical.NativeBaseId;
        if (slot < 0 || slot >= canonical.ExecutionStreams.Count)
            throw new InvalidDataException("Original event execution ID is outside its native directory.");
        var source = new EventSourceReference(loaded.Source.ResourceUri,
            canonical.SourceSha256, (int)slot, eventId);
        PlayableEventStateValidation.ValidateSource(source);
        S14EventConditionProgram dispatch = S14EventConditionProgram.ReadExecution(canonical.ExecutionStreams[(int)slot]);
        Preflight(dispatch);
        return new(source, dispatch);
    }

    /// <summary>Uses an already loaded resource directory. Format/provenance
    /// validation belongs to the resource reader, not each selected playback.</summary>
    internal static CampaignEventExecutionProgram ReadLoadedEvent(LoadedEvents loaded, int eventId)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        long slot = (long)eventId - loaded.Document.NativeBaseId;
        if (slot < 0 || slot >= loaded.Document.ExecutionStreams.Count)
            throw new InvalidDataException("event.story.execution_directory");
        var source = new EventSourceReference(loaded.Source.ResourceUri, loaded.Source.Sha256, (int)slot, eventId);
        var dispatch = S14EventConditionProgram.ReadExecution(loaded.Document.ExecutionStreams[(int)slot]);
        // Unsupported reached handlers fail at dispatch. In playback mode the
        // original permission table suppresses business-only operations, so a
        // whole-stream support proof would incorrectly reject unvisited work.
        return new(source, dispatch);
    }

    public static bool IsSupported(int code) => code is 366 or 271 or 272 or 284 or 291 or 268 or 269 or 352 or 349 or 384 or 113 or 150 or 251 or 114 or 438 or 439 or 112 or 254 or 303 or 302 or 330 or 382 or 151 or 138 or 137 or 170 or 262 or 141 or 292 or 304 or 301 or 320 or 318 or 368 or 356 or 350 or 380 or 443 or 445 or 293 or 313 or 314 or 434 or 379 or 364 or 70 or 71 or 76 or 283 or 297 or 376 or 440 or 258 or 261 or 281 or 370 or 435 or 327 or 132 or 441 or 135 or 167 or 169 or 418 or 419 or 420 or 0 or 5 or 6 or 7 or 8 or
        81 or 82 or 83 or 84 or 85 or 86 or 87 or 88 or 89 or 90 or 91 or 92 or 93 or 94 or 95 or 96 or 97 or 98 or 99 or 100 or 101 or 102 or 103 or 104 or 105 or 106 or 107 or 108 or 109 or
        110 or 111 or 249 or 288 or 326 or 331 or 332 or 385 or 410 or 411 or 215 or 216 or 217 or 223 or 225 or 226 or 227 or 229 or 231 or 239 or 253 or 257 or 424 or 425 or 218 or 219 or 220 or 221 or 222 or 224 or 228 or 230 or 240 or 232 or 233 or 234 or 235 or 236 or 237 or 238 or 142 or 143 or 144 or 145 or 146 or 147 or 148 or 273 or 274 or 275 or 276 or 277 or 278 or 279 or 346 or 122 or 123 or 124 or 125 or 126 or 428 or 430 or 241 or 242 or 243 or 244 or 166 or 377 or 426 or 447 or 448 or 152 or 247 or 365 or 168 or 259 or 260 or 449 or 450 or 209 or 210 or 305 or 306 or 307 or 308 or 309 or 310 or 312 or 383 or 372 or 121 or 115 or 446 or 120 or 263 or 267 or 289 or 158 or 159 or 160 or 153 or 154 or 155 or 156 or 162 or 245 or 252 or 285 or 294 or 295 or 149 or 299 or 188 or 189 or 191 or 192 or 296 or 316 or 321 or 322 or 203 or 204 or 205 or 206 or 207 or 208 or 214 or 172 or 360 or 381 or 290 or >= 173 and <= 187 or 190 or >= 193 and <= 202 or 116 or 117 or 118 or 119 or >= 127 and <= 131 or 163 or 270 or 256 or 287 or 133 or 134 or 375 or 348 or 171 or 359 or 351 or 323 or 286 or 211 or 212 or 213 or 164 or 165 or 319 or 315 or 31 or 432 or 433 or 43 or 80 or 248 or 427 or 324 or 325 or 328 or 329;

    private static void Preflight(S14EventConditionProgram dispatch)
    {
        foreach (S14ConditionInstruction operation in dispatch.Operations)
        {
            if (!IsSupported(operation.Code) && operation.Code is not (1 or 2 or 3 or 4) &&
                !S14PlayerEventConditionRuntime.IsSupported(operation.Code) && !S14BasicEventConditionRuntime.IsSupported(operation.Code))
                throw Failure(operation, "No complete original presentation binding exists.");
            if (UnsupportedArguments(operation.Code, operation.RawArguments) is string reason)
                throw Failure(operation, reason);
        }
    }

    internal static string? UnsupportedArguments(int code, IReadOnlyList<int> arguments)
    {
        if (((code is 82 or 227) && arguments[0] < -9) || (code == 99 && arguments.Take(5).Any(value => value < -9)))
            return "Negative Base selector is outside the current source profile.";
        if ((code == 424 && arguments[0] < 0) || (code == 425 && arguments.Take(5).Any(value => value < 0)))
            return "Negative scenery index has no safe resource-table profile.";
        return null;
    }

    private static InvalidDataException Failure(S14ConditionInstruction operation, string reason) =>
        new($"Original execution operation {operation.Index}, code {operation.Code}, offset {operation.Offset}: {reason}");
}

public abstract record CampaignEventCommand(int OperationIndex, int SourceOffset, int Code);
public sealed record CampaignEventBackgroundStill(int OperationIndex, int SourceOffset,
    int StillId, int NativeType = 15, int NativeOption = 0, int NativeTransition = -1)
    : CampaignEventCommand(OperationIndex, SourceOffset, 85);
public sealed record CampaignEventPerson(S14EventConditionPersonReference Pointer,
    bool NativeValid, ushort? CharacterId);

/// <summary>Native order: clear slots3/4, clear standing context, arrange1/0/2, assign1/0/2.</summary>
public sealed record CampaignEventStandingPosition3(int OperationIndex, int SourceOffset,
    IReadOnlyList<CampaignEventPerson> Persons)
    : CampaignEventCommand(OperationIndex, SourceOffset, 107);
public sealed record CampaignEventPersonParameters(int OperationIndex, int SourceOffset,
    IReadOnlyList<int> CharacterIds)
    : CampaignEventCommand(OperationIndex, SourceOffset, 101);
public sealed record CampaignEventPersonMessage(int OperationIndex, int SourceOffset,
    CampaignEventPerson Speaker, int MessageId, int NativeStyle,
    IReadOnlyList<int?> PersonParameters, IReadOnlyList<CampaignEventPersonParameterWrite> ParameterWrites,
    int SourceCode = 96)
    : CampaignEventCommand(OperationIndex, SourceOffset, SourceCode)
{
    public IReadOnlyList<string>? NameParameters { get; init; }
    public IReadOnlyList<int>? CityParameters { get; init; }
    public IReadOnlyList<int>? ForceParameters { get; init; }
    public IReadOnlyList<int>? AreaParameters { get; init; }
    public IReadOnlyList<int>? ItemParameters { get; init; }
    public IReadOnlyList<int?>? NumberParameters { get; init; }
    public IReadOnlyList<int?>? ProvinceParameters { get; init; }
    public bool SpeechOnly { get; init; }
    public bool WithoutPortrait { get; init; }
}
public sealed record CampaignEventPersonParameterWrite(int ParameterIndex, int CharacterId);
public sealed record CampaignEventStandingPosition2(int OperationIndex, int SourceOffset,
    IReadOnlyList<CampaignEventPerson> Persons) : CampaignEventCommand(OperationIndex, SourceOffset, 106);
public sealed record CampaignEventCameraToScenery(int OperationIndex, int SourceOffset,
    int? HexId, int CameraParameter, int BlackOutParameter, int WaitMilliseconds)
    : CampaignEventCommand(OperationIndex, SourceOffset, 424);
public sealed record CampaignEventNameParameters(int OperationIndex, int SourceOffset,
    IReadOnlyList<string> Names) : CampaignEventCommand(OperationIndex, SourceOffset, 425);
public sealed record CampaignEventBlackTransition(int OperationIndex, int SourceOffset,
    bool FadeIn, int Parameter) : CampaignEventCommand(OperationIndex, SourceOffset, FadeIn ? 111 : 110);
public sealed record CampaignEventStill(int OperationIndex, int SourceOffset,
    int StillId, int Option) : CampaignEventCommand(OperationIndex, SourceOffset, 87);
public sealed record CampaignEventSmallStill(int OperationIndex, int SourceOffset,
    int StillId, int Option) : CampaignEventCommand(OperationIndex, SourceOffset, 88);
public sealed record CampaignEventTutorialStill(int OperationIndex, int SourceOffset,
    int StillId, int Option) : CampaignEventCommand(OperationIndex, SourceOffset, 253);
public sealed record CampaignEventHideCharacters(int OperationIndex, int SourceOffset,
    int Setting, IReadOnlyList<int> CharacterIds) : CampaignEventCommand(OperationIndex, SourceOffset, 108);
public sealed record CampaignEventNamedPersonMessage(int OperationIndex, int SourceOffset,
    int SpeakerCharacterId, int MessageId, int Style, int NameMessageId,
    IReadOnlyList<int?> PersonParameters, IReadOnlyList<string>? NameParameters,
    IReadOnlyList<CampaignEventPersonParameterWrite> ParameterWrites, int SourceCode = 97)
    : CampaignEventCommand(OperationIndex, SourceOffset, SourceCode)
{
    public bool WithoutPortrait { get; init; }
    public IReadOnlyList<int>? CityParameters { get; init; }
    public IReadOnlyList<int>? ForceParameters { get; init; }
    public IReadOnlyList<int>? AreaParameters { get; init; }
    public IReadOnlyList<int>? ItemParameters { get; init; }
    public IReadOnlyList<int?>? NumberParameters { get; init; }
    public IReadOnlyList<int?>? ProvinceParameters { get; init; }
}
public sealed record CampaignEventNarration(int OperationIndex, int SourceOffset,
    int Mode, int MessageId, IReadOnlyList<int?> PersonParameters, IReadOnlyList<string>? NameParameters)
    : CampaignEventCommand(OperationIndex, SourceOffset, 94)
{
    public IReadOnlyList<int>? CityParameters { get; init; }
    public IReadOnlyList<int>? ForceParameters { get; init; }
    public IReadOnlyList<int>? AreaParameters { get; init; }
    public IReadOnlyList<int>? ItemParameters { get; init; }
    public IReadOnlyList<int?>? NumberParameters { get; init; }
    public IReadOnlyList<int?>? ProvinceParameters { get; init; }
}

public enum CampaignEventAudioKind { Bgm, Se, StopSlot, StopAllSe, StopActiveSlots, SeIfNotSkipped, FadeOutBgm, DirectBgm }
public sealed record CampaignEventAudio(int OperationIndex, int SourceOffset, int Code,
    CampaignEventAudioKind Kind, int Id, int Flags, float ScalarA, float ScalarB)
    : CampaignEventCommand(OperationIndex, SourceOffset, Code);

/// <summary>
/// A transaction-local result. Commands are read-only output and remain staged
/// until the caller commits the matching Core candidate. No world fields,
/// event flags, audio manager or execution stamps are written here.
/// </summary>
public sealed class CampaignEventExecutionPlan
{
    public CampaignEventExecutionProgram Program { get; }
    public EventSourceReference Source => Program.Source;
    public IReadOnlyList<CampaignEventCommand> Commands { get; }
    public S14EventConditionResult DispatchResult { get; }
    public IReadOnlyList<int?>? FinalPersonParameters { get; }
    public IReadOnlyList<string>? FinalNameParameters { get; }
    public IReadOnlyList<int>? FinalCityParameters { get; init; }
    public IReadOnlyList<int>? FinalForceParameters { get; init; }
    public IReadOnlyList<int>? FinalAreaParameters { get; init; }
    public IReadOnlyList<int>? FinalItemParameters { get; init; }
    public IReadOnlyList<int?>? FinalNumberParameters { get; init; }
    public IReadOnlyList<int?>? FinalProvinceParameters { get; init; }
    public CampaignEventDialogueCast? FinalDialogueCast { get; init; }
    public IReadOnlyDictionary<int, int> SelectedSpeakerIds { get; }
    public IReadOnlyList<int?> SelectedTargetForcePhysicalSlots { get; init; } = [];

    internal CampaignEventExecutionPlan(CampaignEventExecutionProgram program,
        IReadOnlyList<CampaignEventCommand> commands, S14EventConditionResult result,
        IReadOnlyList<int?>? parameters, IReadOnlyList<string>? names,
        IReadOnlyDictionary<int, int> selected)
        => (Program, Commands, DispatchResult, FinalPersonParameters, FinalNameParameters, SelectedSpeakerIds) =
            (program, commands, result, parameters, names, selected);

    /// <summary>Existing Core reports carry provenance, while the typed plan carries presentation.</summary>
    public PlayableEventEffectBatch CreateCoreBatch(ulong randomState)
    {
        RequireLegacyMessageBatch();
        return new(Commands.OfType<CampaignEventPersonMessage>().Select(message => new PlayableEventEffect(
            PlayableEventEffectKind.Message, MessageKey: "original.event.person_message",
            SourceOperationIndex: message.OperationIndex)).ToArray(), randomState);
    }

    public void RequireCommitted(GameState state, string eventKey)
    {
        RequireLegacyMessageBatch();
        ArgumentNullException.ThrowIfNull(state);
        PlayableEventStateValidation.Validate(state);
        if (state.EventRuntime is not { } runtime ||
            !runtime.Executions.TryGetValue(eventKey, out PlayableEventExecutionStamp? stamp) ||
            stamp.Revision != Program.Revision || stamp.LastPeriod != state.PeriodNumber ||
            !runtime.SourceRevisions.TryGetValue(eventKey, out string? revision) || revision != Program.Revision)
            throw new InvalidDataException("Original event presentation has no matching committed execution.");
        PlayableEventReportEntry[] reports = (state.EventReports ?? []).Where(report =>
            report.EventKey == eventKey && report.Revision == Program.Revision &&
            report.Period == state.PeriodNumber && report.Source == Source).ToArray();
        if (reports.Count(report => report.Kind == "triggered") != 1)
            throw new InvalidDataException("Original event presentation has no matching source trigger report.");
        int[] expected = Commands.OfType<CampaignEventPersonMessage>()
            .Select(message => message.OperationIndex).ToArray();
        int?[] actual = reports.Where(report => report.Kind == nameof(PlayableEventEffectKind.Message))
            .Select(report => report.SourceOperationIndex).ToArray();
        if (!expected.Select(index => (int?)index).SequenceEqual(actual))
            throw new InvalidDataException("Original event presentation messages differ from the committed source sequence.");
    }

    private void RequireLegacyMessageBatch()
    {
        if (Program.DispatchProgram.Operations.Any(operation => operation.Code is
            81 or 86 or 87 or 88 or 89 or 93 or 94 or 95 or 97 or 98 or 99 or 100 or 102 or 103 or 104 or 105 or 106 or 108 or 109 or 110 or 111 or 215 or 216 or 217 or 223 or 225 or 226 or 227 or 229 or 231 or 239 or 249 or 253 or 326 or 331 or 332 or 424 or 425))
            throw new InvalidDataException("A complete story requires its saved semantic consumer; the legacy message batch cannot commit or acknowledge it.");
    }
}

public static partial class CampaignEventExecution
{
    public static CampaignEventExecutionPlan Stage(CampaignEventExecutionProgram program,
        IS14PlayerEventConditionQueries persons,
        Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        IS14AudioEventExecutionState? audioInputs = null, IReadOnlyList<int>? initialPersonParameters = null,
        CampaignEventExecutionInputs? storyInputs = null)
    {
        var runtime = CreateRuntime(program, persons, dispatchInputs, audioInputs, initialPersonParameters, storyInputs);
        S14EventConditionResult result = S14EventConditionEvaluator.Evaluate(program.DispatchProgram, runtime);
        return CreatePlan(program, runtime, result);
    }

    private sealed partial class StagingRuntime(IS14PlayerEventConditionQueries persons,
        Func<S14ConditionContext, S14ConditionDispatchInputs> inputs,
        IS14AudioEventExecutionState? audioInputs, IReadOnlyList<int?>? initialParameters,
        CampaignEventExecutionInputs? storyInputs, bool editedProgram)
        : IS14EventConditionRuntime
    {
        public List<CampaignEventCommand> Commands { get; } = [];
        public int?[]? Parameters { get; private set; } = initialParameters?.ToArray();
        private int[]? cityParameters = storyInputs?.InitialCityParameters?.ToArray();
        private int[]? forceParameters = storyInputs?.InitialForceParameters?.ToArray();
        private int[]? areaParameters = storyInputs?.InitialAreaParameters?.ToArray();
        private int[]? itemParameters = storyInputs?.InitialItemParameters?.ToArray();
        private int?[]? numberParameters = storyInputs?.InitialNumberParameters?.ToArray();
        private int?[]? provinceParameters = storyInputs?.InitialProvinceParameters?.ToArray();
        public IReadOnlyList<int>? ItemParameters => MessageItems();
        public IReadOnlyList<int?>? NumberParameters => MessageNumbers();
        public IReadOnlyList<int?>? ProvinceParameters => MessageProvinces();
        public IReadOnlyList<int>? CityParameters => cityParameters is null ? null : Array.AsReadOnly(cityParameters.ToArray());
        public IReadOnlyList<int>? ForceParameters => MessageForces();
        public IReadOnlyList<int>? AreaParameters => MessageAreas();
        private readonly CampaignEventExecutionContext? _story = storyInputs is null ? null : new(storyInputs);
        private readonly S14PlayerEventConditionRuntime conditionRuntime = new(persons, inputs,
            persons as IS14BasicEventConditionQueries);
        public bool? ForcedIfResult => storyInputs?.ForcedIfResult;
        public CampaignEventDialogueCast? DialogueCast => _story?.ExportDialogueCast();
        public IReadOnlyList<string>? Names { get; private set; } = storyInputs?.InitialNameParameters is { } names
            ? Array.AsReadOnly(names.ToArray()) : null;
        public IReadOnlyDictionary<int, int> SelectedSpeakers => new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(
            _story?.Actors.ToDictionary(pair => pair.Key, pair => (int)(persons.ReadConditionPersonCharacterId(pair.Value)
                ?? throw new InvalidDataException("Selected actor identity is unknown."))) ?? []);
        public int? TargetForcePhysicalSlot(int selector) =>
            conditionRuntime.ReadMessageTargetForce(selector)?.PhysicalSlot;
        private CampaignEventExecutionContext Story => _story ?? throw new InvalidDataException("Current story execution context is unknown.");
        public bool ConditionBlocked => conditionRuntime.ConditionBlocked || (_story?.PersonListBlocked ?? false);
        public bool TryGetBinding(int code, out S14ConditionBinding binding)
        {
            // Source metadata is separate from rewritten handler availability.
            // In title playback second-control0 is a genuine skipped business
            // operation; it must not demand or execute an invented handler.
            return EventExecutionPermissionsData.TryGet(code, out binding);
        }
        public S14ConditionDispatchInputs ReadInputs(S14ConditionContext context) => inputs(context);
        public int InvokeLeaf(S14ConditionContext context)
        {
            S14ConditionInstruction operation = context.Operation;
            IReadOnlyList<int> arguments = operation.RawArguments;
            try
            {
                switch (operation.Code)
                {
                    case 262:
                    case 271:
                    case 272:
                        return Story.ExecuteStoryPersonFamilyList(operation.Code, arguments,
                            conditionRuntime.ReadMessageTargetForce, Story.ReadCurrentStoryPersonRelationWord,
                            Story.ReadCurrentStoryPersonKinship);
                    case 291:
                    case 292:
                        return Story.ExecuteStoryAreaRegionFilter(operation.Code, arguments);
                    case 268:
                    case 269:
                    case 318:
                        return conditionRuntime.ExecuteStoryCityRegionFilter(operation.Code, arguments,
                            storyInputs?.AreaListCurrent ?? throw new InvalidDataException("story.city-region.current_owner"),
                            () => new S14EventConditionCityPointer(Story.ResolveStoryCitySelector(-4).PhysicalSlot));
                    case 438: return Story.AddStoryCoalitionForces();
                    case 366:
                        return ExecuteWorldMutation(operation);
                    case 151:
                    case 320:
                    case 443:
                    case 352:
                    case 349:
                    case 350:
                    case 356:
                    case 302:
                    case 303:
                    case 304:
                    case 301:
                    case 137:
                    case 138:
                    case 284:
                    case 439:
                    case 368:
                    case 384:
                    case 112:
                    case 113:
                    case 114:
                    case 382:
                    case 141:
                    case 150:
                    case 251:
                    case 170:
                    case 254:
                    case 330:
                    case 380:
                    case 445:
                        return ExecuteWorldMutation(operation);
                    case 293:
                    case 313:
                    case 314:
                        return Story.ExecuteStoryAreaFilter(operation.Code, arguments);
                    case 434:
                    case 379:
                        return Story.ExecuteStoryPersonFilter(operation.Code, arguments, Story.ReadStoryPersonDoctrineValue);
                    case 364: return conditionRuntime.KeepStoryCityWithFewestTroops();
                    case 70:
                    case 71:
                    case 76:
                    case 283:
                    case 297:
                    case 376:
                        return Story.ExecuteStoryPresenceCondition(operation.Code, arguments, conditionRuntime.ReadMessageTargetForce);
                    case 440: return conditionRuntime.CompareStoryCitySelectionCount(arguments) ? 1 : 0;
                    case 258:
                    case 370:
                        return StageMessageMonthAndProvince(operation);
                    case 327:
                    case 435:
                        return StageAuxMessageParameters(operation);
                    case 261:
                    case 281:
                        return conditionRuntime.InvokeMessageParameterCondition(context);
                    case 132:
                    case 441:
                    case 135:
                    case 167:
                    case 169:
                        return Story.ExecuteStoryInvocationSources(operation.Code, arguments,
                            conditionRuntime.ReadMessageTargetForce, conditionRuntime.StoreStoryTargetForce,
                            Story.SampleStoryInvocationSourceSelection);
                    case 31:
                    case 43:
                        return conditionRuntime.EvaluateStoryAggregateCondition(context).Result ? 1 : 0;
                    case 80:
                        return Story.CompareCandidateCount(arguments) ? 1 : 0;
                    case 432:
                    case 433:
                        var aggregate = conditionRuntime.EvaluateStoryAggregateCondition(context);
                        if (aggregate.CurrentPlayerCityIncreaseKind is int increaseKind)
                            RecordAggregateResult?.Invoke(increaseKind);
                        return aggregate.Result ? 1 : 0;
                    case 248: return StageMessageStringParameters(operation);
                    case 324:
                    case 325:
                    case 328:
                    case 329:
                        StageAudioControl(operation);
                        return 1;
                    case 427:
                        bool bypassPoem = storyInputs?.ReadReturnToTitlePending?.Invoke()
                            ?? throw new InvalidDataException("story.poem_event.application_return_title_owner");
                        if (bypassPoem || arguments[0] >= 21) return 1;
                        if (arguments[0] <= 0) throw new InvalidDataException("story.poem_event.descriptor_identity");
                        Commands.Add(new CampaignEventPoemEvent(operation.Index, operation.Offset, arguments[0]));
                        return 1;
                    case 133:
                    case 134:
                    case 375:
                    case 348:
                        return Story.ExecuteStoryCitySearch(operation.Code, arguments, conditionRuntime.ReadMessageTargetForce);
                    case >= 127 and <= 131:
                    case 163:
                    case 256:
                    case 287:
                    case 171:
                    case 359:
                    case 351:
                    case 323:
                    case 286:
                    case 164:
                    case 165:
                    case 319:
                    case 315:
                        return ExecuteWorldMutation(operation);
                    case 270:
                    case 211:
                    case 212:
                    case 213:
                        return ExecuteStoryCityListResourceMutation(operation);
                    case 418:
                    case 419:
                    case 420:
                        return (RelationshipOperation ?? throw new InvalidDataException("story.relationship_execution_owner"))(
                            operation.Code,
                            Story.ResolvePerson(arguments[0], conditionRuntime.ReadMessageTargetForce).PhysicalSlot,
                            Story.ResolvePerson(arguments[1], conditionRuntime.ReadMessageTargetForce).PhysicalSlot,
                            Story.ResolvePerson(arguments[2], conditionRuntime.ReadMessageTargetForce).PhysicalSlot);
                    case 81: return Story.ComparePersonInList(arguments, conditionRuntime.ReadMessageTargetForce) ? 1 : 0;
                    case 93: return Story.ChangeDialogueForce(arguments[0], conditionRuntime.ReadMessageTargetForce) ? 1 : 0;
                    case 102:
                        itemParameters = arguments.Take(5).Select(Story.ReadItemMessageParameter).ToArray();
                        break;
                    case 103:
                        numberParameters = arguments.Take(5).Select(value => (int?)value).ToArray();
                        break;
                    case 104:
                        //Zero is a no-write operand. Missing prior knowledge remains unknown.
                        numberParameters ??= new int?[5];
                        for (int slot = 0; slot < 5; slot++)
                            if (arguments[slot] != 0) numberParameters[slot] = Story.ReadGameNumberMessageParameter(arguments[slot]);
                        break;
                    case 82:
                    case 83:
                    case 84:
                        // Collection's source gate returns before City,
                        // Force or Person selector lookup, camera movement
                        // and the extra ordinary-path one-second wait.
                        if (Story.SkipCamera)
                            Commands.Add(new CampaignEventSuppressedCamera(operation.Index, operation.Offset, operation.Code));
                        else
                            Commands.Add(new CampaignEventCameraToTarget(operation.Index, operation.Offset, operation.Code,
                                Story.CameraTargetHex(operation.Code, arguments[0], conditionRuntime.ReadMessageTargetForce), arguments[1]));
                        break;
                    case 209:
                    case 210:
                    case 305:
                    case 306:
                    case 307:
                    case 308:
                    case 309:
                    case 310:
                    case 312:
                    case 383:
                    case 289:
                        return ExecuteStoryCityListResourceMutation(operation);
                    case >= 173 and <= 183:
                        return Story.ExecuteStoryForceList(operation, conditionRuntime.ReadMessageTargetForce);
                    case 190:
                        return Story.AddStoryAreasByForce(arguments[0], conditionRuntime.ReadMessageTargetForce);
                    case >= 184 and <= 187:
                    case >= 193 and <= 202:
                    case 116:
                    case 117:
                    case 118:
                    case 119:
                        return ExecuteWorldMutation(operation);
                    case 203:
                    case 204:
                    case 205:
                    case 206:
                    case 207:
                    case 208:
                    case 214:
                        return ExecuteStoryCityListOperation(operation);
                    case 172:
                        Story.SetSpeaker(arguments[0], arguments[1], conditionRuntime.ReadMessageTargetForce);
                        return 1;
                    case 360:
                        Story.AddAllForcePersons(arguments[0], conditionRuntime.ReadMessageTargetForce);
                        return 1;
                    case 381:
                        Story.RemoveForcePersons(arguments[0], conditionRuntime.ReadMessageTargetForce);
                        return 1;
                    case 290:
                        Story.AddFootholdPersons(arguments[0]);
                        return 1;
                    case 188:
                    case 189:
                    case 191:
                    case 192:
                    case 296:
                    case 316:
                    case 321:
                    case 322:
                        return Story.ExecuteStoryAreaList(operation, conditionRuntime.ReadMessageTargetForce);
                    case 149:
                    case 299:
                    case 153:
                    case 154:
                    case 155:
                    case 156:
                    case 158:
                    case 159:
                    case 160:
                    case 162:
                    case 245:
                    case 252:
                    case 285:
                    case 294:
                    case 295:
                    case 120:
                    case 263:
                    case 267:
                    case 115:
                    case 446:
                    case 121:
                    case 168:
                    case 259:
                    case 260:
                    case 449:
                    case 450:
                    case 166:
                    case 377:
                    case 426:
                    case 447:
                    case 448:
                    case 152:
                    case 247:
                    case 365:
                    case 428:
                    case 430:
                    case 122:
                    case 123:
                    case 124:
                    case 125:
                    case 126:
                    case 142:
                    case 143:
                    case 144:
                    case 145:
                    case 146:
                    case 147:
                    case 148:
                    case 273:
                    case 274:
                    case 275:
                    case 276:
                    case 277:
                    case 278:
                    case 279:
                    case 346:
                    case 232:
                    case 233:
                    case 234:
                    case 235:
                    case 236:
                    case 237:
                    case 238:
                        return ExecuteWorldMutation(operation);
                    case 241:
                        Commands.Add(new CampaignEventInputWait(operation.Index, operation.Offset));
                        break;
                    case 242:
                    case 243:
                    case 244:
                        return Story.ExecuteInvocationControl(operation.Code, arguments,
                            conditionRuntime.ReadMessageTargetForce, conditionRuntime.StoreStoryTargetForce,
                            conditionRuntime.BlockStoryExecution);
                    case 218:
                    case 219:
                    case 220:
                    case 221:
                    case 222:
                    case 224:
                    case 228:
                    case 230:
                    case 240:
                        return Story.ExecutePersonRelationList(operation.Code, arguments, conditionRuntime.ReadMessageTargetForce);
                    case 216: Story.AddPersons(arguments, conditionRuntime.ReadMessageTargetForce); break;
                    case 217: Story.AddForcePersons(arguments, conditionRuntime.ReadMessageTargetForce); break;
                    case 223: Story.RemovePersons(arguments, conditionRuntime.ReadMessageTargetForce); break;
                    case 225: return Story.NarrowPersonsByForce(arguments[0], conditionRuntime.ReadMessageTargetForce) ? 1 : 0;
                    case 227: return Story.NarrowPersonsByHome(arguments[0]) ? 1 : 0;
                    case 229: return Story.NarrowPersonsByHumble(arguments[0] != 0) ? 1 : 0;
                    case 231: return Story.NarrowPersonsByCanJoinEvent(arguments[0] != 0) ? 1 : 0;
                    case 226: return Story.NarrowPersons(arguments) ? 1 : 0;
                    case 239: Story.SelectSpeakers(arguments); break;
                    case 215: Story.ClearCandidates(); break;
                    case 424:
                        bool skipCamera = Story.SkipCamera;
                        int? hex = skipCamera ? null : Story.SceneryHex(Story.ResolveScenery(arguments[0]));
                        Commands.Add(new CampaignEventCameraToScenery(operation.Index, operation.Offset, hex,
                            arguments[1], -1, skipCamera ? 0 : 1000));
                        break;
                    case 425:
                        Names = Array.AsReadOnly(arguments.Take(5).Select(raw => Story.SceneryName(Story.ResolveScenery(raw))).ToArray());
                        Commands.Add(new CampaignEventNameParameters(operation.Index, operation.Offset, Names));
                        break;
                    case 106:
                        var twoPointers = arguments.Take(2).Select(ResolvePerson).ToArray();
                        Commands.Add(new CampaignEventStandingPosition2(operation.Index, operation.Offset,
                            Array.AsReadOnly(twoPointers.Select(ReadPerson).ToArray())));
                        break;
                    case 110:
                    case 111:
                        Commands.Add(new CampaignEventBlackTransition(operation.Index, operation.Offset, operation.Code == 111, arguments[0]));
                        break;
                    case 87:
                        Commands.Add(new CampaignEventStill(operation.Index, operation.Offset, arguments[0], arguments[1]));
                        break;
                    case 88:
                        Commands.Add(new CampaignEventSmallStill(operation.Index, operation.Offset, arguments[0], arguments[1]));
                        break;
                    case 410:
                        //3C1360 uses the authored illustration kind, hides the
                        //standing cast on every non-large branch, and clears
                        //the small layer except when actually showing small.
                        if (arguments[1] == 13 && arguments[0] != 0)
                            Commands.Add(new CampaignEventStill(operation.Index, operation.Offset, arguments[0], 1));
                        else
                        {
                            Commands.Add(new CampaignEventHideCharacters(operation.Index, operation.Offset, 0, []));
                            if (arguments[1] == 14 && arguments[0] != 0)
                            {
                                Commands.Add(new CampaignEventBackgroundStill(operation.Index, operation.Offset,
                                    arguments[0], NativeType: 14));
                                Commands.Add(new CampaignEventBlackTransition(operation.Index, operation.Offset, true, -1));
                            }
                            else if (arguments[1] == 16 && arguments[0] != 0)
                            {
                                Commands.Add(new CampaignEventSmallStill(operation.Index, operation.Offset, arguments[0], 1));
                                break;
                            }
                        }
                        Commands.Add(new CampaignEventSmallStill(operation.Index, operation.Offset, 0, 0));
                        break;
                    case 253:
                        Commands.Add(new CampaignEventTutorialStill(operation.Index, operation.Offset, arguments[0], arguments[1]));
                        break;
                    case 108:
                        var hiddenIds = new List<int>();
                        for (int index = 1; index < 8; index++)
                        {
                            if (arguments[index] == 0) continue;
                            var hidden = ResolvePerson(arguments[index]);
                            hiddenIds.Add(persons.ReadConditionPersonCharacterId(hidden)
                                ?? throw new InvalidDataException("Hidden story character identity is unknown."));
                        }
                        Commands.Add(new CampaignEventHideCharacters(operation.Index, operation.Offset,
                            arguments[0], hiddenIds.AsReadOnly()));
                        break;
                    case 99:
                        cityParameters = arguments.Take(5).Select(Story.CityMessageParameter).ToArray();
                        break;
                    case 98:
                        forceParameters = arguments.Take(5).Select(selector =>
                            Story.ForceMessageParameter(selector, conditionRuntime.ReadMessageTargetForce)).ToArray();
                        break;
                    case 100:
                        areaParameters = arguments.Take(5).Select(Story.AreaMessageParameter).ToArray();
                        break;
                    case 94:
                        int narrationRoot = arguments[1] == 0x87F3
                            ? Story.PrepareNarration(arguments[1], ref numberParameters) : arguments[1];
                        Commands.Add(new CampaignEventNarration(operation.Index, operation.Offset, arguments[0], narrationRoot,
                            MessagePersons(), MessageNames()) { CityParameters = MessageCities(),
                                ForceParameters = MessageForces(), AreaParameters = MessageAreas(), ItemParameters = MessageItems(), NumberParameters = MessageNumbers(), ProvinceParameters = MessageProvinces() });
                        break;
                    case 97:
                    case 332:
                        //No outer validity gate; the message manager always reads this speaker ID.
                        var namedPointer = ResolvePerson(arguments[0]);
                        int namedId = (operation.Code == 332 ? persons.ReadConditionPersonRawCharacterId(namedPointer)
                            : persons.ReadConditionPersonCharacterId(namedPointer))
                            ?? throw new InvalidDataException("Named-message speaker identity is unknown.");
                        Parameters ??= new int?[5];
                        var namedWrites = WriteMessagePersons(arguments, 4);
                        Parameters[0] = namedId; namedWrites.Add(new(0, namedId));
                        Commands.Add(new CampaignEventNamedPersonMessage(operation.Index, operation.Offset,
                            namedId, arguments[1], arguments[2], arguments[3], MessagePersons(), MessageNames(), namedWrites.AsReadOnly(), operation.Code)
                            { WithoutPortrait = operation.Code == 332, CityParameters = MessageCities(), ForceParameters = MessageForces(), AreaParameters = MessageAreas(), ItemParameters = MessageItems(), NumberParameters = MessageNumbers(), ProvinceParameters = MessageProvinces() });
                        break;
                    case 86:
                        Commands.Add(new CampaignEventBackgroundStill(operation.Index, operation.Offset, arguments[0], NativeType: 14));
                        break;
                    case 85:
                        // +10 is retained but not consumed by this source slot0.
                        Commands.Add(new CampaignEventBackgroundStill(operation.Index, operation.Offset, arguments[0]));
                        break;
                    case 90: break; // Verified HideStill slot0 returns1 without presentation writes.
                    case 288:
                        Commands.Add(new CampaignEventAudio(operation.Index, operation.Offset, operation.Code,
                            CampaignEventAudioKind.FadeOutBgm, 0, 0, arguments[0], 0));
                        break;
                    case 411:
                        //404570 checks its unsigned music-index domain before
                        //any manager mutation. In particular, -2 retains BGM.
                        if ((uint)arguments[0] < AudioMapMusicSelector.MusicIndexCount)
                            Commands.Add(new CampaignEventAudio(operation.Index, operation.Offset, operation.Code,
                                CampaignEventAudioKind.DirectBgm, arguments[0], 0, 1000, 1000));
                        break;
                    case 91:
                    case 92:
                        if (operation.Code == 91 && editedProgram)
                        {
                            //3D6C30 redirects edited BGM to the direct411 handler,
                            //including its unchanged/invalid-index manager gate.
                            if ((uint)arguments[0] < AudioMapMusicSelector.MusicIndexCount)
                                Commands.Add(new CampaignEventAudio(operation.Index, operation.Offset, 411,
                                    CampaignEventAudioKind.DirectBgm, arguments[0], 0, 1000, 1000));
                            break;
                        }
                        if (operation.Code == 92 && arguments[0] > 0)
                        {
                            int mapIndex = arguments[0] - 1;
                            int mapped = mapIndex < 96 ? audioInputs!.ReadSeMapEntry(mapIndex)
                                ?? throw new InvalidDataException("Current audio mapping entry is unknown.") : -1;
                            //The first manager call depends on the live1AC
                            //skip state when this step arrives. The second is
                            //unconditional even when that first call is skipped.
                            Commands.Add(new CampaignEventAudio(operation.Index, operation.Offset, operation.Code,
                                CampaignEventAudioKind.SeIfNotSkipped, mapped, 0, 0, 0));
                            Commands.Add(new CampaignEventAudio(operation.Index, operation.Offset, operation.Code,
                                CampaignEventAudioKind.Se, mapped, 0, 0, 0));
                            break;
                        }
                        //The live62-slot manager is queried when this step
                        //arrives, after preceding sounds have really played.
                        //A staging-time activity snapshot is not that owner.
                        if (operation.Code == 92 && arguments[0] == -3)
                        {
                            Commands.Add(new CampaignEventAudio(operation.Index, operation.Offset, operation.Code,
                                CampaignEventAudioKind.StopActiveSlots, 0, 0, 0, 0));
                            break;
                        }
                        S14AudioEventExecution.Execute(operation.Code, arguments[0],
                            new StagingAudio(audioInputs!, operation, Commands));
                        break;
                    case 89:
                        if (Story.ReadItemPresentation(arguments[0], arguments[1]) is { } item)
                            Commands.Add(new CampaignEventItemIllustration(operation.Index, operation.Offset,
                                item.IllustrationIndex, item.Setting));
                        break;
                    case 105:
                        Commands.Add(new CampaignEventResetStandingRegistration(operation.Index, operation.Offset));
                        break;
                    case 109:
                        if (Story.PrepareOwnedEffect(arguments[0]) is { } effect)
                            Commands.Add(new CampaignEventOwnedEffect(operation.Index, operation.Offset, effect));
                        break;
                    case 107:
                        var pointers = new S14EventConditionPersonReference[3];
                        for (int index = 0; index < 3; index++) pointers[index] = ResolvePerson(arguments[index]);
                        var standing = new CampaignEventPerson[3];
                        for (int index = 0; index < 3; index++) standing[index] = ReadPerson(pointers[index]);
                        Commands.Add(new CampaignEventStandingPosition3(operation.Index, operation.Offset,
                            Array.AsReadOnly(standing)));
                        break;
                    case 101:
                        var parameterIds = new int[5];
                        for (int index = 0; index < 5; index++)
                        {
                            CampaignEventPerson person = ReadPerson(arguments[index]);
                            parameterIds[index] = person.NativeValid ? person.CharacterId!.Value : 0;
                        }
                        Parameters = parameterIds.Select(id => (int?)id).ToArray();
                        Commands.Add(new CampaignEventPersonParameters(operation.Index, operation.Offset,
                            Array.AsReadOnly(parameterIds)));
                        break;
                    case 385:
                        var editPointer = ResolvePerson(arguments[0]);
                        int editId = persons.ReadConditionPersonCharacterId(editPointer)
                            ?? throw new InvalidDataException("Edited-message speaker identity is unknown.");
                        Parameters ??= new int?[5];
                        var editWrites = WriteMessagePersons(arguments, 4);
                        if (editId == 0)
                            Commands.Add(new CampaignEventNarration(operation.Index, operation.Offset, 1,
                                arguments[1], MessagePersons(), MessageNames()));
                        else
                        {
                            bool editValid = persons.ReadConditionPersonValidity(editPointer)
                                ?? throw new InvalidDataException("Edited-message speaker validity is unknown.");
                            Parameters[0] = editId; editWrites.Add(new(0, editId));
                            Commands.Add(new CampaignEventPersonMessage(operation.Index, operation.Offset,
                                new(editPointer, editValid, checked((ushort)editId)), arguments[1], arguments[2],
                                MessagePersons(), editWrites.AsReadOnly(), 385));
                        }
                        break;
                    case 249:
                        Commands.Add(new CampaignEventHideMessage(operation.Index, operation.Offset));
                        break;
                    case 326:
                        //The actual UI manager's fast/skip state is sampled
                        //when this wait begins, never while staging the stream.
                        Commands.Add(new CampaignEventWait(operation.Index, operation.Offset, arguments[0]));
                        break;
                    case 95:
                    case 96:
                    case 331:
                        CampaignEventPerson speaker = ReadPerson(arguments[0]);
                        //96 gates its entire helper/format operation. 95
                        //first formats with the actual receiver's raw ID10;
                        //its later validity gate suppresses display only.
                        if ((operation.Code is 96 or 331) && !speaker.NativeValid) break;
                        Parameters ??= new int?[5];
                        var writes = WriteMessagePersons(arguments, 3);
                        if (!speaker.NativeValid)
                        {
                            ushort rawSpeaker = persons.ReadConditionPersonRawCharacterId(speaker.Pointer)
                                ?? throw new InvalidDataException("Story format-only speaker raw identity is unknown.");
                            Parameters[0] = rawSpeaker;
                            Commands.Add(new CampaignEventFormatOnlyMessage(operation.Index, operation.Offset,
                                rawSpeaker, arguments[1], MessagePersons(), MessageNames())
                            { CityParameters = MessageCities(), ForceParameters = MessageForces(),
                                AreaParameters = MessageAreas(), ItemParameters = MessageItems(), NumberParameters = MessageNumbers(), ProvinceParameters = MessageProvinces() });
                            break;
                        }
                        // 844110 always writes speaker ID into person parameter0 after the helper.
                        Parameters[0] = speaker.CharacterId!.Value;
                        writes.Add(new(0, speaker.CharacterId.Value));
                        Commands.Add(new CampaignEventPersonMessage(operation.Index, operation.Offset,
                            speaker, arguments[1], arguments[2], MessagePersons(),
                            writes.AsReadOnly(), operation.Code) { NameParameters = Names, CityParameters = MessageCities(),
                                ForceParameters = MessageForces(), AreaParameters = MessageAreas(), ItemParameters = MessageItems(), NumberParameters = MessageNumbers(), ProvinceParameters = MessageProvinces(),
                                SpeechOnly = operation.Code == 95, WithoutPortrait = operation.Code == 331 });
                        break;
                    default: return conditionRuntime.InvokeLeaf(context);
                }
                return 1;
            }
            catch (InvalidDataException error)
            {
                throw new InvalidDataException($"Original event operation {operation.Index}, code {operation.Code}, " +
                    $"offset {operation.Offset}: {error.Message}", error);
            }
        }

        private List<CampaignEventPersonParameterWrite> WriteMessagePersons(IReadOnlyList<int> arguments, int argumentStart)
        {
            var writes = new List<CampaignEventPersonParameterWrite>();
            for (int parameter = 1; parameter <= 3; parameter++)
            {
                var person = ReadPerson(arguments[argumentStart + parameter - 1]);
                if (!person.NativeValid) continue;
                Parameters![parameter] = person.CharacterId!.Value;
                writes.Add(new(parameter, person.CharacterId.Value));
            }
            return writes;
        }
        private IReadOnlyList<int?> MessagePersons() => Array.AsReadOnly(
            Parameters?.ToArray() ?? new int?[5]);
        private IReadOnlyList<string>? MessageNames() => Names;
        private IReadOnlyList<int>? MessageCities() => cityParameters is null ? null : Array.AsReadOnly(cityParameters.ToArray());
        private IReadOnlyList<int>? MessageForces() => forceParameters is null ? null : Array.AsReadOnly(forceParameters.ToArray());
        private IReadOnlyList<int>? MessageAreas() => areaParameters is null ? null : Array.AsReadOnly(areaParameters.ToArray());
        private IReadOnlyList<int>? MessageItems() => itemParameters is null ? null : Array.AsReadOnly(itemParameters.ToArray());
        private IReadOnlyList<int?>? MessageProvinces() => provinceParameters is null ? null : Array.AsReadOnly(provinceParameters.ToArray());
        private IReadOnlyList<int?>? MessageNumbers() => numberParameters is null ? null : Array.AsReadOnly(numberParameters.ToArray());

        private CampaignEventPerson ReadPerson(int selector)
            => ReadPerson(ResolvePerson(selector));

        private S14EventConditionPersonReference ResolvePerson(int selector)
        {
            if (_story is not null) return _story.ResolvePerson(selector, conditionRuntime.ReadMessageTargetForce);
            if (selector < 0) throw new InvalidDataException("Dynamic person selector requires current story context.");
            S14EventConditionPersonPointer pointer = selector <= 6000
                ? persons.ReadConditionPersonTableSlot(selector)
                    ?? throw new InvalidDataException("Current person table pointer is unknown.")
                : persons.ReadConditionBasePerson()
                    ?? throw new InvalidDataException("Current base person pointer is unknown.");
            S14EventConditionPersonReference? person = pointer.Value;
            if (person is null)
                person = (persons.ReadConditionPersonTableSlot(0)
                    ?? throw new InvalidDataException("Current person-zero fallback pointer is unknown.")).Value;
            if (person is null) throw new InvalidDataException("Native person-zero fallback pointer is null.");
            return person;
        }

        private CampaignEventPerson ReadPerson(S14EventConditionPersonReference person)
        {
            bool valid = persons.ReadConditionPersonValidity(person)
                ?? throw new InvalidDataException("Current native person validity is unknown.");
            ushort? id = valid ? persons.ReadConditionPersonCharacterId(person)
                ?? throw new InvalidDataException("Current native person character ID is unknown.") : null;
            return new(person, valid, id);
        }
    }

    private sealed class StagingAudio(IS14AudioEventExecutionState inputs,
        S14ConditionInstruction operation, List<CampaignEventCommand> commands) : IS14AudioEventExecutionState
    {
        public float? ReadBgmScalar() => inputs.ReadBgmScalar();
        public int? ReadSeMapEntry(int index) => inputs.ReadSeMapEntry(index);
        public bool? ReadWorldPointerPresent() => inputs.ReadWorldPointerPresent();
        public int? ReadWorldField1AC() => inputs.ReadWorldField1AC();
        public bool? ReadSeSlotActive(int slot) => inputs.ReadSeSlotActive(slot);
        public void InvokeBgmManager(int rawId, int flags, float scalar3, float scalar5) =>
            Add(CampaignEventAudioKind.Bgm, rawId, flags, scalar3, scalar5);
        public void InvokeSeManager(int mappedId, float scalar2, float scalar3) =>
            Add(CampaignEventAudioKind.Se, mappedId, 0, scalar2, scalar3);
        public void StopSeSlot(int slot) => Add(CampaignEventAudioKind.StopSlot, slot, 0, 0, 0);
        public void StopAllSe() => Add(CampaignEventAudioKind.StopAllSe, 0, 0, 0, 0);
        private void Add(CampaignEventAudioKind kind, int id, int flags, float a, float b) =>
            commands.Add(new CampaignEventAudio(operation.Index, operation.Offset, operation.Code, kind, id, flags, a, b));
    }
}
