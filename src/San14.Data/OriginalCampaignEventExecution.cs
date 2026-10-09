using System.Security.Cryptography;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A complete, immutable original presentation stream; it executes no native code.</summary>
public sealed class OriginalCampaignEventExecutionProgram
{
    public EventSourceReference Source { get; }
    public S14EventConditionProgram DispatchProgram { get; }
    public string StreamSha256 { get; }
    public string Revision => Source.ResourceSha256 + ":" + StreamSha256;

    private OriginalCampaignEventExecutionProgram(EventSourceReference source,
        S14EventConditionProgram dispatch)
    {
        Source = source;
        DispatchProgram = dispatch;
        StreamSha256 = Convert.ToHexString(SHA256.HashData(
            dispatch.Operations.SelectMany(operation => operation.RawBytes).ToArray()));
    }

    public static OriginalCampaignEventExecutionProgram ReadEvent(LoadedEvents loaded, int eventId)
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
        85 or 90 or 91 or 92 or 96 or 101 or 107;

    private static void Preflight(S14EventConditionProgram dispatch)
    {
        foreach (S14ConditionInstruction operation in dispatch.Operations)
        {
            if (!IsSupported(operation.Code))
                throw Failure(operation, "No complete original presentation binding exists.");
            // The literal branch is closed in the native person resolvers. Dynamic
            // execution-object selectors require their own current context profile.
            int[] consumed = operation.Code switch
            {
                96 => [0, 3, 4, 5], 101 => [0, 1, 2, 3, 4], 107 => [0, 1, 2], _ => []
            };
            if (consumed.Any(index => operation.RawArguments[index] < 0))
                throw Failure(operation, "Dynamic person selectors are outside this execution profile.");
        }
    }

    private static InvalidDataException Failure(S14ConditionInstruction operation, string reason) =>
        new($"Original execution operation {operation.Index}, code {operation.Code}, offset {operation.Offset}: {reason}");
}

public abstract record OriginalCampaignEventCommand(int OperationIndex, int SourceOffset, int Code);
public sealed record OriginalCampaignEventBackgroundStill(int OperationIndex, int SourceOffset,
    int StillId, int NativeType = 15, int NativeOption = 0, int NativeTransition = -1)
    : OriginalCampaignEventCommand(OperationIndex, SourceOffset, 85);
public sealed record OriginalCampaignEventPerson(S14EventConditionPersonReference Pointer,
    bool NativeValid, ushort? CharacterId);

/// <summary>Native order: clear slots3/4, clear standing context, arrange1/0/2, assign1/0/2.</summary>
public sealed record OriginalCampaignEventStandingPosition3(int OperationIndex, int SourceOffset,
    IReadOnlyList<OriginalCampaignEventPerson> Persons)
    : OriginalCampaignEventCommand(OperationIndex, SourceOffset, 107);
public sealed record OriginalCampaignEventPersonParameters(int OperationIndex, int SourceOffset,
    IReadOnlyList<int> CharacterIds)
    : OriginalCampaignEventCommand(OperationIndex, SourceOffset, 101);
public sealed record OriginalCampaignEventPersonMessage(int OperationIndex, int SourceOffset,
    OriginalCampaignEventPerson Speaker, int MessageId, int NativeStyle,
    IReadOnlyList<int> PersonParameters, IReadOnlyList<OriginalCampaignEventPersonParameterWrite> ParameterWrites)
    : OriginalCampaignEventCommand(OperationIndex, SourceOffset, 96);
public sealed record OriginalCampaignEventPersonParameterWrite(int ParameterIndex, int CharacterId);
public enum OriginalCampaignEventAudioKind { Bgm, Se, StopSlot, StopAllSe }
public sealed record OriginalCampaignEventAudio(int OperationIndex, int SourceOffset, int Code,
    OriginalCampaignEventAudioKind Kind, int Id, int Flags, float ScalarA, float ScalarB)
    : OriginalCampaignEventCommand(OperationIndex, SourceOffset, Code);

/// <summary>
/// A transaction-local result. Commands are read-only output and remain staged
/// until the caller commits the matching Core candidate. No world fields,
/// event flags, audio manager or execution stamps are written here.
/// </summary>
public sealed class OriginalCampaignEventExecutionPlan
{
    public OriginalCampaignEventExecutionProgram Program { get; }
    public EventSourceReference Source => Program.Source;
    public IReadOnlyList<OriginalCampaignEventCommand> Commands { get; }
    public S14EventConditionResult DispatchResult { get; }
    public IReadOnlyList<int?>? FinalPersonParameters { get; }

    internal OriginalCampaignEventExecutionPlan(OriginalCampaignEventExecutionProgram program,
        IReadOnlyList<OriginalCampaignEventCommand> commands, S14EventConditionResult result,
        IReadOnlyList<int?>? parameters)
        => (Program, Commands, DispatchResult, FinalPersonParameters) = (program, commands, result, parameters);

    /// <summary>Existing Core reports carry provenance, while the typed plan carries presentation.</summary>
    public PlayableEventEffectBatch CreateCoreBatch(ulong randomState) => new(
        Commands.OfType<OriginalCampaignEventPersonMessage>().Select(message => new PlayableEventEffect(
            PlayableEventEffectKind.Message, MessageKey: "original.event.person_message",
            SourceOperationIndex: message.OperationIndex)).ToArray(), randomState);

    public void RequireCommitted(GameState state, string eventKey)
    {
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
        int[] expected = Commands.OfType<OriginalCampaignEventPersonMessage>()
            .Select(message => message.OperationIndex).ToArray();
        int?[] actual = reports.Where(report => report.Kind == nameof(PlayableEventEffectKind.Message))
            .Select(report => report.SourceOperationIndex).ToArray();
        if (!expected.Select(index => (int?)index).SequenceEqual(actual))
            throw new InvalidDataException("Original event presentation messages differ from the committed source sequence.");
    }
}

public static class OriginalCampaignEventExecution
{
    public static OriginalCampaignEventExecutionPlan Stage(OriginalCampaignEventExecutionProgram program,
        IS14PlayerEventConditionQueries persons,
        Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        IS14AudioEventExecutionState? audioInputs = null, IReadOnlyList<int>? initialPersonParameters = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(persons);
        ArgumentNullException.ThrowIfNull(dispatchInputs);
        if (initialPersonParameters is not null &&
            (initialPersonParameters.Count != 5 || initialPersonParameters.Any(id => id is < 0 or > ushort.MaxValue)))
            throw new InvalidDataException("Original message person parameters require five known word IDs.");
        // All source instructions and dependencies are checked before visiting the first action.
        if (program.DispatchProgram.Operations.Any(operation => operation.Code is 91 or 92) && audioInputs is null)
            throw new InvalidDataException("Complete original event execution requires current audio inputs.");
        var runtime = new StagingRuntime(persons, dispatchInputs, audioInputs, initialPersonParameters);
        S14EventConditionResult result = S14EventConditionEvaluator.Evaluate(program.DispatchProgram, runtime);
        return new(program, Array.AsReadOnly(runtime.Commands.ToArray()), result,
            runtime.Parameters is null ? null : Array.AsReadOnly(runtime.Parameters.ToArray()));
    }

    private sealed class StagingRuntime(IS14PlayerEventConditionQueries persons,
        Func<S14ConditionContext, S14ConditionDispatchInputs> inputs,
        IS14AudioEventExecutionState? audioInputs, IReadOnlyList<int>? initialParameters)
        : IS14EventConditionRuntime
    {
        public List<OriginalCampaignEventCommand> Commands { get; } = [];
        public int?[]? Parameters { get; private set; } = initialParameters?.Select(id => (int?)id).ToArray();
        public bool ConditionBlocked => false;
        public bool TryGetBinding(int code, out S14ConditionBinding binding)
        {
            if (S14EventConditionEvaluator.BuiltInBindings.TryGetValue(code, out binding)) return true;
            if (OriginalCampaignEventExecutionProgram.IsSupported(code)) { binding = new(1, 1, true); return true; }
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
                    case 85:
                        // +10 is retained but not consumed by this source slot0.
                        Commands.Add(new OriginalCampaignEventBackgroundStill(operation.Index, operation.Offset, arguments[0]));
                        break;
                    case 90: break; // Verified HideStill slot0 returns1 without presentation writes.
                    case 91:
                    case 92:
                        S14AudioEventExecution.Execute(operation.Code, arguments[0],
                            new StagingAudio(audioInputs!, operation, Commands));
                        break;
                    case 107:
                        var pointers = new S14EventConditionPersonReference[3];
                        for (int index = 0; index < 3; index++) pointers[index] = ResolvePerson(arguments[index]);
                        var standing = new OriginalCampaignEventPerson[3];
                        for (int index = 0; index < 3; index++) standing[index] = ReadPerson(pointers[index]);
                        Commands.Add(new OriginalCampaignEventStandingPosition3(operation.Index, operation.Offset,
                            Array.AsReadOnly(standing)));
                        break;
                    case 101:
                        var parameterIds = new int[5];
                        for (int index = 0; index < 5; index++)
                        {
                            OriginalCampaignEventPerson person = ReadPerson(arguments[index]);
                            parameterIds[index] = person.NativeValid ? person.CharacterId!.Value : 0;
                        }
                        Parameters = parameterIds.Select(id => (int?)id).ToArray();
                        Commands.Add(new OriginalCampaignEventPersonParameters(operation.Index, operation.Offset,
                            Array.AsReadOnly(parameterIds)));
                        break;
                    case 96:
                        OriginalCampaignEventPerson speaker = ReadPerson(arguments[0]);
                        // Invalid speaker skips the helper and manager, yet the leaf returns1.
                        if (!speaker.NativeValid) break;
                        Parameters ??= new int?[5];
                        var writes = new List<OriginalCampaignEventPersonParameterWrite>();
                        // 3D85B0 receives indexes(-1,3,4,5,-1); skipped slots keep their old value.
                        for (int parameter = 1; parameter <= 3; parameter++)
                        {
                            OriginalCampaignEventPerson person = ReadPerson(arguments[parameter + 2]);
                            if (!person.NativeValid) continue;
                            Parameters[parameter] = person.CharacterId!.Value;
                            writes.Add(new(parameter, person.CharacterId.Value));
                        }
                        // 844110 always writes speaker ID into person parameter0 after the helper.
                        Parameters[0] = speaker.CharacterId!.Value;
                        writes.Add(new(0, speaker.CharacterId.Value));
                        if (Parameters.Any(id => id is null))
                            throw new InvalidDataException("Consumed message person parameters are unknown.");
                        Commands.Add(new OriginalCampaignEventPersonMessage(operation.Index, operation.Offset,
                            speaker, arguments[1], arguments[2], Array.AsReadOnly(Parameters.Select(id => id!.Value).ToArray()),
                            writes.AsReadOnly()));
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

        private OriginalCampaignEventPerson ReadPerson(int selector)
            => ReadPerson(ResolvePerson(selector));

        private S14EventConditionPersonReference ResolvePerson(int selector)
        {
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

        private OriginalCampaignEventPerson ReadPerson(S14EventConditionPersonReference person)
        {
            bool valid = persons.ReadConditionPersonValidity(person)
                ?? throw new InvalidDataException("Current native person validity is unknown.");
            ushort? id = valid ? persons.ReadConditionPersonCharacterId(person)
                ?? throw new InvalidDataException("Current native person character ID is unknown.") : null;
            return new(person, valid, id);
        }
    }

    private sealed class StagingAudio(IS14AudioEventExecutionState inputs,
        S14ConditionInstruction operation, List<OriginalCampaignEventCommand> commands) : IS14AudioEventExecutionState
    {
        public float? ReadBgmScalar() => inputs.ReadBgmScalar();
        public int? ReadSeMapEntry(int index) => inputs.ReadSeMapEntry(index);
        public bool? ReadWorldPointerPresent() => inputs.ReadWorldPointerPresent();
        public int? ReadWorldField1AC() => inputs.ReadWorldField1AC();
        public bool? ReadSeSlotActive(int slot) => inputs.ReadSeSlotActive(slot);
        public void InvokeBgmManager(int rawId, int flags, float scalar3, float scalar5) =>
            Add(OriginalCampaignEventAudioKind.Bgm, rawId, flags, scalar3, scalar5);
        public void InvokeSeManager(int mappedId, float scalar2, float scalar3) =>
            Add(OriginalCampaignEventAudioKind.Se, mappedId, 0, scalar2, scalar3);
        public void StopSeSlot(int slot) => Add(OriginalCampaignEventAudioKind.StopSlot, slot, 0, 0, 0);
        public void StopAllSe() => Add(OriginalCampaignEventAudioKind.StopAllSe, 0, 0, 0, 0);
        private void Add(OriginalCampaignEventAudioKind kind, int id, int flags, float a, float b) =>
            commands.Add(new OriginalCampaignEventAudio(operation.Index, operation.Offset, operation.Code, kind, id, flags, a, b));
    }
}
