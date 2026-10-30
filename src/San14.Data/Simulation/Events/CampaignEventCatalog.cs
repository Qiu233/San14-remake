using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A source instruction rejected by the current complete-stream profile.</summary>
public sealed record CampaignEventRejection(int OperationIndex, int Offset,
    int Code, string Reason);

/// <summary>Immutable source instruction; no condition or action is evaluated here.</summary>
public sealed record CampaignEventOperation(int Index, int Offset, int Code,
    short RawField04, short RawField06, IReadOnlyList<int> RawArguments,
    IReadOnlyList<byte> RawBytes);

public sealed record CampaignEventStream(int DirectoryIndex, int Offset, int Length,
    string Sha256, IReadOnlyList<CampaignEventOperation> Operations,
    IReadOnlyList<CampaignEventRejection> Rejections)
{
    public bool ProfileSupported => Rejections.Count == 0;
    public CampaignEventRejection? FirstRejection => Rejections.FirstOrDefault();
    public bool HasMeaningfulAction => Operations.Any(operation =>
        operation.Code is 85 or 87 or 90 or 91 or 92 or 94 or 96 or 97 or 106 or 107 or 424 or >= 188 and <= 200);
}

/// <summary>
/// Original row and ID-minus-base directory references. Missing directories are
/// retained explicitly; neither row ordinal nor raw +34 replaces the native ID.
/// </summary>
public sealed record CampaignEventEntry(int RecordOrdinal, int EventId,
    int RawField34, int RecordOffset, IReadOnlyList<byte> RawRecordBytes,
    int? DirectoryIndex, CampaignEventStream? Condition,
    CampaignEventStream? Execution)
{
    public bool CompleteProfileSupported => Condition?.ProfileSupported == true &&
        Execution?.ProfileSupported == true;
    public bool HasMeaningfulSupportedExecution => EventId != 0 &&
        CompleteProfileSupported && Execution!.HasMeaningfulAction;
}

/// <summary>
/// Source/override-aware campaign event inventory. Support means the entire
/// original stream fits the existing condition and execution profiles; current
/// eligibility, dispatch inputs and complete writer effects remain required.
/// A code-zero placeholder is never advertised as a playable campaign event.
/// </summary>
public sealed class CampaignEventCatalog
{
    public const string SourcePath = "0004/EventPKCE001.s14";
    private readonly byte[] _sourceBytes;
    public SourceProvenance Source { get; }
    public int NativeBaseId { get; }
    public IReadOnlyList<CampaignEventEntry> Events { get; }
    public IReadOnlyList<CampaignEventEntry> SupportedEvents { get; }

    // Return an independent source document for callers that construct a runtime.
    public S14EventDocument Document => S14EventReader.Read(_sourceBytes);

    private CampaignEventCatalog(S14EventDocument document, SourceProvenance source)
    {
        _sourceBytes = document.RawBytes.ToArray();
        Source = source;
        NativeBaseId = document.NativeBaseId;
        var conditions = document.ConditionStreams.Select(stream => Describe(stream, false)).ToArray();
        var executions = document.ExecutionStreams.Select(stream => Describe(stream, true)).ToArray();
        var entries = new List<CampaignEventEntry>(document.Events.Count);
        foreach (S14EventRecord row in document.Events)
        {
            long index = (long)row.Id - NativeBaseId;
            int? directory = index >= 0 && index < conditions.Length ? (int)index : null;
            entries.Add(new(row.Index, row.Id, row.RawField34, row.Offset,
                Array.AsReadOnly(row.RawBytes.ToArray()), directory,
                directory is int slot ? conditions[slot] : null,
                directory is int executionSlot ? executions[executionSlot] : null));
        }
        Events = entries.AsReadOnly();
        SupportedEvents = Array.AsReadOnly(entries.Where(entry =>
            entry.HasMeaningfulSupportedExecution).ToArray());
    }

    public static CampaignEventCatalog Load(GameResourceRepository repository,
        string relativePath = SourcePath)
    {
        ArgumentNullException.ThrowIfNull(repository);
        LoadedEvents loaded = repository.LoadEvents(relativePath);
        return Read(loaded.Document, loaded.Source);
    }

    public static CampaignEventCatalog Read(S14EventDocument document, SourceProvenance source)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(source);
        S14EventDocument canonical = S14EventReader.Read(document.RawBytes);
        if (canonical.SourceSha256 != document.SourceSha256 ||
            !canonical.SourceSha256.Equals(source.Sha256, StringComparison.OrdinalIgnoreCase) ||
            canonical.RawBytes.LongLength != source.Bytes ||
            string.IsNullOrWhiteSpace(source.ResourceUri))
            throw new InvalidDataException("Campaign event source bytes, hash or provenance differ.");
        return new(canonical, source);
    }

    private static CampaignEventStream Describe(S14EventStream stream, bool execution)
    {
        var operations = new List<CampaignEventOperation>(stream.Operations.Count);
        var rejections = new List<CampaignEventRejection>();
        foreach (S14EventOperation source in stream.Operations)
        {
            byte[] bytes = source.RawBytes.ToArray();
            int[] arguments = Enumerable.Range(0, 8).Select(index =>
                BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12 + index * 4))).ToArray();
            var operation = new CampaignEventOperation(source.Index, source.Offset, source.Code,
                source.RawField04, source.RawField06, Array.AsReadOnly(arguments), Array.AsReadOnly(bytes));
            operations.Add(operation);
            bool supported = execution ? S14AreaEventExecutionRuntime.IsSupported(source.Code) || CampaignEventExecutionProgram.IsSupported(source.Code) :
                S14EventConditionEvaluator.BuiltInBindings.ContainsKey(source.Code) ||
                S14BasicEventConditionRuntime.IsSupported(source.Code) || S14PlayerEventConditionRuntime.IsSupported(source.Code);
            if (!supported)
                rejections.Add(new(source.Index, source.Offset, source.Code, execution ?
                    "No complete execution binding for this original opcode." :
                    "No current basic condition binding for this original opcode."));
            else if (!execution && source.Code is 9 or 10 && arguments[0] is < 0 or >= S14EventSourceProfile.WorldFlagCount)
                rejections.Add(new(source.Index, source.Offset, source.Code,
                    "Event flag selector is outside the supported native world bitmap."));
            else if (!execution && source.Code is 17 or 19 && arguments[1] < 0)
                rejections.Add(new(source.Index, source.Offset, source.Code,
                    "Dynamic person or city selectors are unsupported by the basic condition profile."));
            else if (!execution && ((source.Code is 18 or 48 && UnknownForce(arguments[1])) ||
                (source.Code == 36 && UnknownForce(arguments[1])) ||
                (source.Code is 25 or 27 or 37 && (UnknownForce(arguments[0]) || UnknownForce(arguments[2]))) ||
                (source.Code == 26 && (UnknownForce(arguments[0]) || UnknownForce(arguments[1]))) ||
                (source.Code is 34 or 243 && UnknownPerson(arguments[0])) ||
                (source.Code == 33 && (UnknownPerson(arguments[0]) || UnknownForce(arguments[1]))) ||
                (source.Code == 35 && (UnknownForce(arguments[0]) || UnknownPerson(arguments[1]))) ||
                (source.Code == 68 && (arguments[0] < 0 || UnknownPerson(arguments[1]))) ||
                (source.Code is 49 or 50 && (arguments[0] < 0 || UnknownForce(arguments[2]))) ||
                (source.Code is 52 or 55 or 246 && arguments[0] < -1) ||
                (source.Code == 264 && (arguments[0] < 0 || UnknownForce(arguments[2]))) ||
                (source.Code == 265 && (UnknownForce(arguments[0]) || UnknownForce(arguments[2]))) ||
                (source.Code == 38 && (UnknownForce(arguments[0]) || arguments[3] < 0)) ||
                (source.Code is 21 or 23 or 28 or 29 or 30 or 39 or 47 or 255 && UnknownForce(arguments[0])) ||
                (source.Code == 56 && (UnknownPerson(arguments[0]) || UnknownForce(arguments[2]))) ||
                (source.Code is 57 or 74 && (UnknownPerson(arguments[0]) || arguments[2] < 0)) ||
                (source.Code is 58 or 60 or 62 or 67 or 78 && (UnknownPerson(arguments[0]) || UnknownPerson(arguments[2]))) ||
                (source.Code is 64 or 69 or 244 && arguments.Take(5).Any(UnknownPerson)) ||
                (source.Code == 216 && arguments.Take(5).Any(value => value < 0)) ||
                (source.Code == 367 && UnknownForce(arguments[0])) ||
                (source.Code == 46 && UnknownForce(arguments[0])) ||
                (source.Code == 444 && arguments[0] < -1) ||
                (source.Code == 452 && (UnknownForce(arguments[0]) || UnknownForce(arguments[2]))) ||
                (source.Code == 436 && arguments[0] < 0) ||
                (source.Code == 437 && arguments.Skip(1).Take(5).Any(value => value < 0)) ||
                (source.Code == 431 && UnknownForce(arguments[0])) ||
                (source.Code == 417 && (UnknownForce(arguments[0]) || arguments[2] < -1)) ||
                (source.Code == 422 && (arguments[0] < 0 || UnknownForce(arguments[2]))) ||
                (source.Code is 423 or 451 && arguments[0] < 0) ||
                (source.Code == 372 && arguments.Any(value => value < -1)) ||
                (source.Code == 374 && UnknownForce(arguments[2])) ||
                (source.Code is 20 or 22 or 24 or 42 or 225 && UnknownForce(arguments[0])) ||
                (source.Code is 59 or 61 or 66 or 73 or 75 or 79 or 298 && UnknownPerson(arguments[0]))))
                rejections.Add(new(source.Index, source.Offset, source.Code,
                    "Dynamic person selectors are unsupported by the current person condition profile."));
        }

        if (execution && operations.Count != 0 &&
            !operations.All(operation => S14AreaEventExecutionRuntime.IsSupported(operation.Code)) &&
            !operations.All(operation => CampaignEventExecutionProgram.IsSupported(operation.Code) &&
                CampaignEventExecutionProgram.UnsupportedArguments(operation.Code, operation.RawArguments) is null))
        {
            var first = operations.First(operation => !S14AreaEventExecutionRuntime.IsSupported(operation.Code));
            if (!rejections.Any(rejection => rejection.OperationIndex == first.Index))
                rejections.Add(new(first.Index, first.Offset, first.Code,
                    "No single complete area or story execution profile supports this stream and its selectors."));
        }

        // Always validate the complete structure, including groups whose leaves
        // are currently unknown. This prevents a prefix from becoming a program.
        try { _ = execution ? S14EventConditionProgram.ReadExecution(stream) : S14EventConditionProgram.Read(stream); }
        catch (InvalidDataException error)
        {
            CampaignEventOperation? rejected = FindStructuralFailure(operations, execution);
            if (rejected is null) throw;
            if (!rejections.Any(rejection => rejection.OperationIndex == rejected.Index))
                rejections.Add(new(rejected.Index, rejected.Offset, rejected.Code, error.Message));
        }
        rejections.Sort((left, right) => left.OperationIndex.CompareTo(right.OperationIndex));
        byte[] allBytes = operations.SelectMany(operation => operation.RawBytes).ToArray();
        return new(stream.Index, stream.Offset, stream.Length,
            Convert.ToHexString(SHA256.HashData(allBytes)), operations.AsReadOnly(), rejections.AsReadOnly());

        static bool UnknownPerson(int selector) => selector < 0 && selector is not (>= -8 and <= -4);
        static bool UnknownForce(int selector) => selector < -5;
    }

    private static CampaignEventOperation? FindStructuralFailure(
        IReadOnlyList<CampaignEventOperation> operations, bool execution)
    {
        int failure = -1;
        try
        {
            _ = S14EventConditionProgram.ReadGroupEnds(operations.Select(operation => operation.Code).ToArray(), out failure,
                personListConditionBlocks: !execution);
        }
        catch (InvalidDataException) { return failure >= 0 ? operations[failure] : null; }
        return null;
    }
}
