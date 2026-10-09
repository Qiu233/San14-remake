using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

/// <summary>Raw EDX/R8D/R9D scan arguments; their business names are not inferred.</summary>
public readonly record struct S14EventScanParameters(int RawEdx, int RawR8, int RawR9);

public enum S14EventConditionSourceKind
{
    ConditionDirectory,
    NativeEmptyResourceMissing,
    NativeEmptyIdOutsideDirectory
}

public enum S14EventEligibilityStatus
{
    Satisfied = 0,
    Unsatisfied = 1,
    Blocked = 2,
    SkippedWorldFlag = 3
}

/// <summary>
/// Explicit current native inputs and manager mutations. The two world bitmaps
/// are independent. Nullable reads mean unknown evidence, not a cleared bit.
/// ResetResults clears only the result list, not the manager bitmap or +20 flag.
/// This interface deliberately supplies no Core or static-scenario fallback.
/// </summary>
public interface IS14EventEligibilityState
{
    short? ReadCurrentYear();
    int? ReadScenarioMode();
    bool? ReadWorldEventFlag(int eventId);
    bool? ReadWorldResultFlag(int eventId);
    void ResetResults();
    void AppendResult(S14EventEligibilityResult result);
    void WriteManagerEventFlag(int eventId, bool value);
    void WriteAvailableResult(bool value);
}

/// <summary>
/// Runtime-factory input for one candidate. Native empty spans have no original
/// stream index, window or hash; Program is an explicitly synthetic empty view.
/// </summary>
public sealed record S14EventEligibilityContext(S14EventRecord Record, int CandidateOrdinal,
    int? DirectoryIndex, S14EventConditionSourceKind SourceKind,
    S14EventConditionProgram Program, string? ResourceSha256);

/// <summary>
/// Preserves the complete VM result, including full callback categories and root
/// fold. StoredSuccessCount follows the native blocked-count reset. Status3 has
/// no VM result because it does not construct or execute a condition context.
/// </summary>
public sealed record S14EventEligibilityResult(S14EventRecord Record, int CandidateOrdinal,
    int? DirectoryIndex, S14EventConditionSourceKind? SourceKind,
    S14EventEligibilityStatus Status, S14EventConditionResult? ConditionResult)
{
    public int StoredSuccessCount => ConditionResult?.EffectiveSuccessCount ?? 0;
    public int EvaluatedCount => ConditionResult?.EvaluatedCount ?? 0;
}

public sealed record S14EventEligibilityScanResult(IReadOnlyList<S14EventEligibilityResult> Results,
    bool HasAvailableResult);

/// <summary>
/// Verified CE 3E2330 filtering and 3D93A0 condition-result orchestration. This
/// neither executes the separate execution directory nor supplies unresolved
/// business leaves. Candidate order is source ordinal; directory selection uses
/// ID minus the first source row ID, never ordinal or raw field +34.
/// Manager IDs are supported only within the proved 124-byte bitmap (0..991).
/// Mutations follow native order and are not silently rolled back on failure.
/// </summary>
public static class S14EventEligibilityEvaluator
{
    // Same exact 48-byte loader signature as S14EventReader; a self-consistent
    // hash alone cannot establish the profile of a publicly constructed document.
    private static readonly byte[] SourceSignature = Convert.FromHexString(
        "030000000500000009000000040000000E000000000000000000000006000000" +
        "01000000000000000000000000000000");
    /// <summary>Scan source rows in their original ordinal order.</summary>
    public static S14EventEligibilityScanResult Scan(S14EventDocument document,
        IS14EventEligibilityState state, S14EventScanParameters parameters,
        Func<S14EventEligibilityContext, IS14EventConditionRuntime> runtimeFactory)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Events is null) throw new InvalidDataException("Event eligibility resource has no source record table.");
        return Scan(document.Events, document, state, parameters, runtimeFactory);
    }

    /// <summary>
    /// Scan an explicit native candidate-pointer sequence. Order and duplicates
    /// are preserved. Empty candidates with a missing resource still reset the
    /// result list, read the entry year and reset the available-result flag.
    /// </summary>
    public static S14EventEligibilityScanResult Scan(IReadOnlyList<S14EventRecord> candidates,
        S14EventDocument? conditionResource, IS14EventEligibilityState state,
        S14EventScanParameters parameters,
        Func<S14EventEligibilityContext, IS14EventConditionRuntime> runtimeFactory)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(runtimeFactory);
        if (conditionResource is not null) ValidateDocument(conditionResource);
        foreach (S14EventRecord record in candidates)
        {
            if (record is null) throw new InvalidDataException("Event eligibility candidate pointer is unknown.");
            ValidateRecord(record, conditionResource);
        }
        state.ResetResults();
        short year = Known(state.ReadCurrentYear(), "current year");
        var results = new List<S14EventEligibilityResult>();
        var usedRuntimes = new HashSet<IS14EventConditionRuntime>(ReferenceEqualityComparer.Instance);
        for (int ordinal = 0; ordinal < candidates.Count; ordinal++)
        {
            S14EventRecord record = candidates[ordinal];
            if (!PassesFilter(record, year, parameters, state)) continue;
            S14EventEligibilityResult result = EvaluateValidated(record, conditionResource, state, runtimeFactory,
                ordinal, usedRuntimes);
            results.Add(result);
            state.AppendResult(result);
        }

        // All candidates have completed before +20 is reset and postprocessed.
        state.WriteAvailableResult(false);
        bool available = false;
        foreach (S14EventEligibilityResult result in results)
        {
            if (result.Status != S14EventEligibilityStatus.Satisfied) continue;
            bool flag;
            try { flag = Known(state.ReadWorldResultFlag(result.Record.Id), "world+4E0 result bit"); }
            catch (InvalidDataException error) { throw Failure(result.Record, error.Message, error); }
            if (flag) continue;
            state.WriteAvailableResult(true);
            available = true;
            break;
        }
        return new(results.AsReadOnly(), available);
    }

    /// <summary>
    /// Evaluate one explicitly supplied source row without scan filtering or
    /// resetting/postprocessing the manager result list. A null resource uses
    /// the proved native empty-span contract, not a guessed original stream0.
    /// </summary>
    public static S14EventEligibilityResult Evaluate(S14EventRecord record,
        S14EventDocument? conditionResource, IS14EventEligibilityState state,
        Func<S14EventEligibilityContext, IS14EventConditionRuntime> runtimeFactory)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(runtimeFactory);
        if (conditionResource is not null) ValidateDocument(conditionResource);
        ValidateRecord(record, conditionResource);
        return EvaluateValidated(record, conditionResource, state, runtimeFactory, record.Index, null);
    }

    private static bool PassesFilter(S14EventRecord record, short year,
        S14EventScanParameters parameters, IS14EventEligibilityState state)
    {
        ReadOnlySpan<byte> raw = record.RawBytes;
        if (parameters.RawR9 != -1)
        {
            int offset = parameters.RawR9 == 1 ? 0x3C : parameters.RawR9 == 2 ? 0x40 : 0x38;
            if (I32(raw, offset) == 0) return false;
        }
        if (record.Id == 0) return false;
        if (parameters.RawR8 == 0)
        {
            int field = I32(raw, 0x0C);
            if (field != 0 && field < year) return false;
            int mode;
            try { mode = Known(state.ReadScenarioMode(), "scenario mode"); }
            catch (InvalidDataException error) { throw Failure(record, error.Message, error); }
            if (mode == 2 && I32(raw, 4) == 4) return false;
        }
        if (I32(raw, 4) == 0 && parameters.RawR8 != 0 && record.Id != 950 && record.Id < 952)
            return false;
        // Frozen 36F370 is a constant-zero leaf. No unresolved DLC query is inserted.
        return parameters.RawEdx == 0 || I32(raw, 8) == 2;
    }

    private static S14EventEligibilityResult EvaluateValidated(S14EventRecord source,
        S14EventDocument? resource, IS14EventEligibilityState state,
        Func<S14EventEligibilityContext, IS14EventConditionRuntime> runtimeFactory,
        int ordinal, HashSet<IS14EventConditionRuntime>? usedRuntimes)
    {
        S14EventRecord record = source with { RawBytes = source.RawBytes.ToArray() };
        try
        {
            if (record.Id is < 0 or >= S14EventSourceProfile.ManagerFlagCount)
                throw new InvalidDataException("Event ID is outside the supported 124-byte manager bitmap.");
            state.WriteManagerEventFlag(record.Id, false);
            bool worldFlag = Known(state.ReadWorldEventFlag(record.Id), "world+460 event bit");
            if (worldFlag && I32(record.RawBytes, 0x30) == 0)
                return new(record, ordinal, null, null, S14EventEligibilityStatus.SkippedWorldFlag, null);

            int? directoryIndex = null;
            S14EventConditionSourceKind kind = S14EventConditionSourceKind.NativeEmptyResourceMissing;
            S14EventStream stream = new(0, 0, 0, Array.Empty<S14EventOperation>());
            if (resource is not null)
            {
                long index = (long)record.Id - resource.NativeBaseId;
                if (index >= 0 && index < resource.ConditionStreams.Count)
                {
                    directoryIndex = (int)index;
                    kind = S14EventConditionSourceKind.ConditionDirectory;
                    stream = resource.ConditionStreams[(int)index];
                    ValidateStream(stream, resource, (int)index);
                }
                else kind = S14EventConditionSourceKind.NativeEmptyIdOutsideDirectory;
            }
            S14EventConditionProgram program = S14EventConditionProgram.Read(stream);
            var context = new S14EventEligibilityContext(record, ordinal, directoryIndex, kind,
                program, resource?.SourceSha256);
            IS14EventConditionRuntime runtime = runtimeFactory(context)
                ?? throw new InvalidDataException("The current event runtime factory returned no provider.");
            if (runtime.ConditionBlocked || (usedRuntimes is not null && !usedRuntimes.Add(runtime)))
                throw new InvalidDataException("Each candidate requires fresh, unblocked execution state.");
            S14EventConditionResult result = S14EventConditionEvaluator.Evaluate(program, runtime);
            if (result.Status == S14ConditionStatus.Blocked) state.WriteManagerEventFlag(record.Id, true);
            return new(record, ordinal, directoryIndex, kind, (S14EventEligibilityStatus)result.Status, result);
        }
        catch (InvalidDataException error) { throw Failure(record, error.Message, error); }
    }

    private static void ValidateDocument(S14EventDocument document)
    {
        if (document.Events is null || document.Events.Count == 0 || document.RawBytes is null ||
            document.RawBytes.Length < S14EventReader.HeaderBytes || document.Events[0] is null ||
            !document.RawBytes.AsSpan(0, SourceSignature.Length).SequenceEqual(SourceSignature) ||
            document.ConditionStreams is null || document.ConditionStreams.Count != document.Events.Count ||
            (long)document.RecordOffset < S14EventReader.HeaderBytes + (2L * document.Events.Count + 1) * 4 ||
            (long)document.RecordOffset + (long)document.Events.Count * S14EventReader.EventRecordBytes > document.RawBytes.Length ||
            I32(document.RawBytes, 0x30) != document.Events.Count || I32(document.RawBytes, 0x40) != document.RecordOffset ||
            document.NativeBaseId != document.Events[0].Id ||
            Convert.ToHexString(SHA256.HashData(document.RawBytes)) != document.SourceSha256)
            throw new InvalidDataException("Event eligibility resource has invalid source bounds, base ID or source SHA256.");
        for (int ordinal = 0; ordinal < document.Events.Count; ordinal++)
        {
            S14EventRecord? record = document.Events[ordinal];
            if (record is null || record.Index != ordinal)
                throw new InvalidDataException($"Event eligibility record ordinal {ordinal} is invalid.");
            ValidateRecord(record, document);
        }
    }

    private static void ValidateRecord(S14EventRecord record, S14EventDocument? resource)
    {
        if (record.Index < 0 || record.Offset < 0 || (long)record.Offset + S14EventReader.EventRecordBytes > int.MaxValue ||
            record.RawBytes is not { Length: S14EventReader.EventRecordBytes } ||
            I32(record.RawBytes, 0) != record.Id || I32(record.RawBytes, 0x34) != record.RawField34)
            throw Failure(record, "Record window, ID or raw field +34 disagrees with its original 68 bytes.");
        if (resource is not null && (record.Index >= resource.Events.Count ||
            (long)record.Offset != (long)resource.RecordOffset + (long)record.Index * S14EventReader.EventRecordBytes ||
            (long)record.Offset + S14EventReader.EventRecordBytes > resource.RawBytes.Length ||
            !resource.RawBytes.AsSpan(record.Offset, S14EventReader.EventRecordBytes).SequenceEqual(record.RawBytes)))
            throw Failure(record, "Record does not belong to the declared source resource window.");
    }

    private static void ValidateStream(S14EventStream stream, S14EventDocument resource, int index)
    {
        if (stream is null || stream.Index != index || stream.Offset < 0 || stream.Length < 0 ||
            (long)stream.Offset + stream.Length > resource.RawBytes.Length || stream.Operations is null ||
            stream.Length % S14EventReader.OperationBytes != 0 || stream.Operations.Count != stream.Length / S14EventReader.OperationBytes)
            throw new InvalidDataException("Selected condition directory has an invalid source window.");
        int sourceBegin = I32(resource.RawBytes, S14EventReader.HeaderBytes + index * 4);
        int sourceEnd = I32(resource.RawBytes, S14EventReader.HeaderBytes + (index + 1) * 4);
        if (stream.Offset != sourceBegin || (long)stream.Length != (long)sourceEnd - sourceBegin ||
            sourceBegin < (long)resource.RecordOffset + (long)resource.Events.Count * S14EventReader.EventRecordBytes)
            throw new InvalidDataException("Selected condition window disagrees with the original offset directory.");
        for (int ordinal = 0; ordinal < stream.Operations.Count; ordinal++)
        {
            S14EventOperation? operation = stream.Operations[ordinal];
            if (operation is null || operation.RawBytes is not { Length: S14EventReader.OperationBytes } ||
                operation.Index != ordinal || operation.Offset != stream.Offset + ordinal * S14EventReader.OperationBytes ||
                !resource.RawBytes.AsSpan(operation.Offset, S14EventReader.OperationBytes).SequenceEqual(operation.RawBytes))
                throw new InvalidDataException($"Selected condition operation {ordinal} disagrees with its original resource bytes.");
        }
    }

    private static int I32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    private static T Known<T>(T? value, string field) where T : struct =>
        value ?? throw new InvalidDataException($"Current event eligibility input is unknown: {field}.");
    private static InvalidDataException Failure(S14EventRecord record, string message, Exception? inner = null) =>
        new($"Event eligibility row {record.Index}, ID {record.Id}, source offset {record.Offset}: {message}", inner);
}
