using System.Security.Cryptography;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A complete, immutable original presentation stream; it executes no native code.</summary>
public sealed class CampaignEventExecutionProgram
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
        S14EventConditionProgram dispatch = S14EventConditionProgram.Read(canonical.ExecutionStreams[(int)slot]);
        Preflight(dispatch);
        return new(source, dispatch);
    }

    public static bool IsSupported(int code) => code is 0 or 5 or 6 or 7 or 8 or
        85 or 87 or 90 or 91 or 92 or 94 or 96 or 97 or 101 or 106 or 107 or
        110 or 111 or 215 or 217 or 226 or 239 or 424 or 425;

    private static void Preflight(S14EventConditionProgram dispatch)
    {
        foreach (S14ConditionInstruction operation in dispatch.Operations)
        {
            if (!IsSupported(operation.Code))
                throw Failure(operation, "No complete original presentation binding exists.");
            if (UnsupportedArguments(operation.Code, operation.RawArguments) is string reason)
                throw Failure(operation, reason);
        }
    }

    internal static string? UnsupportedArguments(int code, IReadOnlyList<int> arguments)
    {
        int[] persons = code switch
        {
            96 => [0, 3, 4, 5], 97 => [0, 4, 5, 6],
            101 => [0, 1, 2, 3, 4], 106 => [0, 1], 107 => [0, 1, 2], _ => []
        };
        if (persons.Any(index => arguments[index] < 0 && arguments[index] != -4 &&
            arguments[index] is not (>= -14 and <= -10))) return "Person selector has no closed execution-context producer.";
        if (code == 217 && arguments.Any(value => value < -1)) return "Force selector has no closed execution-context producer.";
        if ((code == 424 && arguments[0] < 0) || (code == 425 && arguments.Take(5).Any(value => value < 0)))
            return "Negative scenery index has no safe resource-table profile.";
        if (code == 94 && arguments[1] == 0x87F3)
            return "Special doctrine-adjusted narration requires its independent numeric message context.";
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
    IReadOnlyList<int?> PersonParameters, IReadOnlyList<CampaignEventPersonParameterWrite> ParameterWrites)
    : CampaignEventCommand(OperationIndex, SourceOffset, 96)
{
    public IReadOnlyList<string>? NameParameters { get; init; }
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
public sealed record CampaignEventNamedPersonMessage(int OperationIndex, int SourceOffset,
    int SpeakerCharacterId, int MessageId, int Style, int NameMessageId,
    IReadOnlyList<int?> PersonParameters, IReadOnlyList<string>? NameParameters,
    IReadOnlyList<CampaignEventPersonParameterWrite> ParameterWrites)
    : CampaignEventCommand(OperationIndex, SourceOffset, 97);
public sealed record CampaignEventNarration(int OperationIndex, int SourceOffset,
    int Mode, int MessageId, IReadOnlyList<int?> PersonParameters, IReadOnlyList<string>? NameParameters)
    : CampaignEventCommand(OperationIndex, SourceOffset, 94);

public enum CampaignEventAudioKind { Bgm, Se, StopSlot, StopAllSe, StopActiveSlots }
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
    public IReadOnlyDictionary<int, int> SelectedSpeakerIds { get; }

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
            87 or 94 or 97 or 106 or 110 or 111 or 215 or 217 or 226 or 239 or 424 or 425))
            throw new InvalidDataException("A complete story requires its saved semantic consumer; the legacy message batch cannot commit or acknowledge it.");
    }
}

public static class CampaignEventExecution
{
    public static CampaignEventExecutionPlan Stage(CampaignEventExecutionProgram program,
        IS14PlayerEventConditionQueries persons,
        Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        IS14AudioEventExecutionState? audioInputs = null, IReadOnlyList<int>? initialPersonParameters = null,
        CampaignEventExecutionInputs? storyInputs = null)
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
        if (program.DispatchProgram.Operations.Any(operation => operation.Code is 91 or 92) && audioInputs is null)
            throw new InvalidDataException("Complete original event execution requires current audio inputs.");
        var runtime = new StagingRuntime(persons, dispatchInputs, audioInputs, initialPersonParameters, storyInputs);
        S14EventConditionResult result = S14EventConditionEvaluator.Evaluate(program.DispatchProgram, runtime);
        return new(program, Array.AsReadOnly(runtime.Commands.ToArray()), result,
            runtime.Parameters is null ? null : Array.AsReadOnly(runtime.Parameters.ToArray()),
            runtime.Names, runtime.SelectedSpeakers);
    }

    private sealed class StagingRuntime(IS14PlayerEventConditionQueries persons,
        Func<S14ConditionContext, S14ConditionDispatchInputs> inputs,
        IS14AudioEventExecutionState? audioInputs, IReadOnlyList<int>? initialParameters,
        CampaignEventExecutionInputs? storyInputs)
        : IS14EventConditionRuntime
    {
        public List<CampaignEventCommand> Commands { get; } = [];
        public int?[]? Parameters { get; private set; } = initialParameters?.Select(id => (int?)id).ToArray();
        private readonly CampaignEventExecutionContext? _story = storyInputs is null ? null : new(storyInputs);
        public IReadOnlyList<string>? Names { get; private set; } = storyInputs?.InitialNameParameters is { } names
            ? Array.AsReadOnly(names.ToArray()) : null;
        public IReadOnlyDictionary<int, int> SelectedSpeakers => new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(
            _story?.Actors.ToDictionary(pair => pair.Key, pair => (int)(persons.ReadConditionPersonCharacterId(pair.Value)
                ?? throw new InvalidDataException("Selected actor identity is unknown."))) ?? []);
        private CampaignEventExecutionContext Story => _story ?? throw new InvalidDataException("Current story execution context is unknown.");
        public bool ConditionBlocked => false;
        public bool TryGetBinding(int code, out S14ConditionBinding binding)
        {
            if (S14EventConditionEvaluator.BuiltInBindings.TryGetValue(code, out binding)) return true;
            if (CampaignEventExecutionProgram.IsSupported(code)) { binding = new(code is 215 or 217 or 226 or 239 ? 0 : 1, 1, true); return true; }
            binding = default; return false;
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
                    case 217: Story.AddForcePersons(arguments); break;
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
                    case 94:
                        Commands.Add(new CampaignEventNarration(operation.Index, operation.Offset, arguments[0], arguments[1],
                            MessagePersons(), MessageNames()));
                        break;
                    case 97:
                        //No outer validity gate; the message manager always reads this speaker ID.
                        var namedPointer = ResolvePerson(arguments[0]);
                        int namedId = persons.ReadConditionPersonCharacterId(namedPointer)
                            ?? throw new InvalidDataException("Named-message speaker identity is unknown.");
                        Parameters ??= new int?[5];
                        var namedWrites = WriteMessagePersons(arguments, 4);
                        Parameters[0] = namedId; namedWrites.Add(new(0, namedId));
                        Commands.Add(new CampaignEventNamedPersonMessage(operation.Index, operation.Offset,
                            namedId, arguments[1], arguments[2], arguments[3], MessagePersons(), MessageNames(), namedWrites.AsReadOnly()));
                        break;
                    case 85:
                        // +10 is retained but not consumed by this source slot0.
                        Commands.Add(new CampaignEventBackgroundStill(operation.Index, operation.Offset, arguments[0]));
                        break;
                    case 90: break; // Verified HideStill slot0 returns1 without presentation writes.
                    case 91:
                    case 92:
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
                    case 96:
                        CampaignEventPerson speaker = ReadPerson(arguments[0]);
                        // Invalid speaker skips the helper and manager, yet the leaf returns1.
                        if (!speaker.NativeValid) break;
                        Parameters ??= new int?[5];
                        var writes = WriteMessagePersons(arguments, 3);
                        // 844110 always writes speaker ID into person parameter0 after the helper.
                        Parameters[0] = speaker.CharacterId!.Value;
                        writes.Add(new(0, speaker.CharacterId.Value));
                        Commands.Add(new CampaignEventPersonMessage(operation.Index, operation.Offset,
                            speaker, arguments[1], arguments[2], MessagePersons(),
                            writes.AsReadOnly()) { NameParameters = Names });
                        break;
                    default: throw new InvalidDataException("No staged original presentation leaf is bound.");
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

        private CampaignEventPerson ReadPerson(int selector)
            => ReadPerson(ResolvePerson(selector));

        private S14EventConditionPersonReference ResolvePerson(int selector)
        {
            if (_story is not null) return _story.ResolvePerson(selector);
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
