using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A source instruction rejected by the current complete-stream profile.</summary>
public sealed record OriginalCampaignEventRejection(int OperationIndex, int Offset,
    int Code, string Reason);

/// <summary>Immutable source instruction; no condition or action is evaluated here.</summary>
public sealed record OriginalCampaignEventOperation(int Index, int Offset, int Code,
    short RawField04, short RawField06, IReadOnlyList<int> RawArguments,
    IReadOnlyList<byte> RawBytes);

public sealed record OriginalCampaignEventStream(int DirectoryIndex, int Offset, int Length,
    string Sha256, IReadOnlyList<OriginalCampaignEventOperation> Operations,
    IReadOnlyList<OriginalCampaignEventRejection> Rejections)
{
    public bool ProfileSupported => Rejections.Count == 0;
    public OriginalCampaignEventRejection? FirstRejection => Rejections.FirstOrDefault();
    public bool HasMeaningfulAction => Operations.Any(operation =>
        operation.Code is 90 or 91 or 92 or >= 188 and <= 200);
}

/// <summary>
/// Original row and ID-minus-base directory references. Missing directories are
/// retained explicitly; neither row ordinal nor raw +34 replaces the native ID.
/// </summary>
public sealed record OriginalCampaignEventEntry(int RecordOrdinal, int EventId,
    int RawField34, int RecordOffset, IReadOnlyList<byte> RawRecordBytes,
    int? DirectoryIndex, OriginalCampaignEventStream? Condition,
    OriginalCampaignEventStream? Execution)
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
public sealed class OriginalCampaignEventCatalog
{
    public const string SourcePath = "0004/EventPKCE001.s14";
    private readonly byte[] _sourceBytes;
    public SourceProvenance Source { get; }
    public int NativeBaseId { get; }
    public IReadOnlyList<OriginalCampaignEventEntry> Events { get; }
    public IReadOnlyList<OriginalCampaignEventEntry> SupportedEvents { get; }

    // Return an independent source document for callers that construct a runtime.
    public S14EventDocument Document => S14EventReader.Read(_sourceBytes);

    private OriginalCampaignEventCatalog(S14EventDocument document, SourceProvenance source)
    {
        _sourceBytes = document.RawBytes.ToArray();
        Source = source;
        NativeBaseId = document.NativeBaseId;
        var conditions = document.ConditionStreams.Select(stream => Describe(stream, false)).ToArray();
        var executions = document.ExecutionStreams.Select(stream => Describe(stream, true)).ToArray();
        var entries = new List<OriginalCampaignEventEntry>(document.Events.Count);
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

    public static OriginalCampaignEventCatalog Load(OriginalGameRepository repository,
        string relativePath = SourcePath)
    {
        ArgumentNullException.ThrowIfNull(repository);
        LoadedEvents loaded = repository.LoadEvents(relativePath);
        return Read(loaded.Document, loaded.Source);
    }

    public static OriginalCampaignEventCatalog Read(S14EventDocument document, SourceProvenance source)
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

    private static OriginalCampaignEventStream Describe(S14EventStream stream, bool execution)
    {
        var operations = new List<OriginalCampaignEventOperation>(stream.Operations.Count);
        var rejections = new List<OriginalCampaignEventRejection>();
        foreach (S14EventOperation source in stream.Operations)
        {
            byte[] bytes = source.RawBytes.ToArray();
            int[] arguments = Enumerable.Range(0, 8).Select(index =>
                BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12 + index * 4))).ToArray();
            var operation = new OriginalCampaignEventOperation(source.Index, source.Offset, source.Code,
                source.RawField04, source.RawField06, Array.AsReadOnly(arguments), Array.AsReadOnly(bytes));
            operations.Add(operation);
            bool supported = execution ? S14AreaEventExecutionRuntime.IsSupported(source.Code) :
                S14EventConditionEvaluator.BuiltInBindings.ContainsKey(source.Code) ||
                source.Code is 9 or 15 or 17 or 19 || S14PlayerEventConditionRuntime.IsSupported(source.Code);
            if (!supported)
                rejections.Add(new(source.Index, source.Offset, source.Code, execution ?
                    "No complete execution binding for this original opcode." :
                    "No current basic condition binding for this original opcode."));
            else if (!execution && source.Code == 9 && arguments[0] is < 0 or >= 1024)
                rejections.Add(new(source.Index, source.Offset, source.Code,
                    "Event flag selector is outside the supported native world bitmap."));
            else if (!execution && source.Code is 17 or 19 && arguments[1] < 0)
                rejections.Add(new(source.Index, source.Offset, source.Code,
                    "Dynamic person or city selectors are unsupported by the basic condition profile."));
            else if (!execution && ((source.Code == 36 && arguments[1] < 0) ||
                (source.Code is 59 or 298 && arguments[0] < 0)))
                rejections.Add(new(source.Index, source.Offset, source.Code,
                    "Dynamic person selectors are unsupported by the current person condition profile."));
        }

        // Always validate the complete structure, including groups whose leaves
        // are currently unknown. This prevents a prefix from becoming a program.
        try { _ = S14EventConditionProgram.Read(stream); }
        catch (InvalidDataException error)
        {
            OriginalCampaignEventOperation? rejected = FindStructuralFailure(operations);
            if (rejected is null) throw;
            if (!rejections.Any(rejection => rejection.OperationIndex == rejected.Index))
                rejections.Add(new(rejected.Index, rejected.Offset, rejected.Code, error.Message));
        }
        rejections.Sort((left, right) => left.OperationIndex.CompareTo(right.OperationIndex));
        byte[] allBytes = operations.SelectMany(operation => operation.RawBytes).ToArray();
        return new(stream.Index, stream.Offset, stream.Length,
            Convert.ToHexString(SHA256.HashData(allBytes)), operations.AsReadOnly(), rejections.AsReadOnly());
    }

    private static OriginalCampaignEventOperation? FindStructuralFailure(
        IReadOnlyList<OriginalCampaignEventOperation> operations)
    {
        var groups = new Stack<OriginalCampaignEventOperation>();
        foreach (OriginalCampaignEventOperation operation in operations)
        {
            if (operation.Code is < 0 or >= 453 or 1 or 2 or 3 or 4 or 216 or 257 or 372 or 379)
                return operation;
            if (operation.Code is 5 or 7) groups.Push(operation);
            else if (operation.Code is 6 or 8)
            {
                if (groups.Count == 0 || groups.Peek().Code + 1 != operation.Code) return operation;
                groups.Pop();
            }
        }
        return groups.Count == 0 ? null : groups.Peek();
    }
}
