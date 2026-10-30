using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ConfiguredFieldActorListRow(
    uint AnchorObjectId, uint GateObjectId, uint SourceListObjectId);

/// <summary>Actual current named Data resolution. A missing dictionary entry is
/// unknown; Present=false is an explicitly resolved absent Data. ListMembers
/// borrows the current ordered members, not an object's declaration array.</summary>
public sealed record ConfiguredFieldActorListCurrentData(
    bool Present,
    bool? ListCpuPresent = null,
    int? SignedGateValue = null,
    IReadOnlyList<OwnedWorldData?>? ListMembers = null);

public sealed record ConfiguredFieldActorListAppendResult(long Attempts, long Appended);

/// <summary>F43DE0's configured source-list copy to the real 175E Actor List.
/// This consumes actual Data membership; it neither creates Fields nor derives
/// active membership from resource declarations, stage names or adjacency.</summary>
public sealed class ConfiguredFieldActorListPublication
{
    public const uint ConfigurationObjectId = 0x7376A8BE;
    public const uint ModelListObjectId = 0xE65D70AE;
    public const uint DestinationListObjectId = 0x175E5FDB;
    public const uint SignedGatePropertyHash = 0x66FEA95A;

    private readonly ConfiguredFieldActorListRow[] rows;

    private ConfiguredFieldActorListPublication(LoadedObjects source,
        ConfiguredFieldActorListRow[] rows, uint[] initialEnabledWords)
    {
        SourceResourceUri = source.Source.ResourceUri;
        SourceDecodedSha256 = source.Source.DecodedSha256;
        this.rows = rows;
        Rows = Array.AsReadOnly(rows);
        InitialEnabledWords = Array.AsReadOnly(initialEnabledWords);
    }

    public string SourceResourceUri { get; }
    public string SourceDecodedSha256 { get; }
    public IReadOnlyList<ConfiguredFieldActorListRow> Rows { get; }
    /// <summary>Constructor values only. Actual 149E70/156640 map-load writes
    /// replace the current enabled words; publication takes those explicitly.</summary>
    public IReadOnlyList<uint> InitialEnabledWords { get; }

    public static ConfiguredFieldActorListPublication Read(RenderResourceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return Read(descriptor.Source, descriptor.Object);
    }

    public static ConfiguredFieldActorListPublication Read(LoadedObjects source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var matches = source.Document.Objects.Where(o => o.ObjectId == ConfigurationObjectId).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("Field Actor List configuration must resolve one actual named declaration.");
        return Read(source, matches[0]);
    }

    private static ConfiguredFieldActorListPublication Read(LoadedObjects source, DokObject configuration)
    {
        if (configuration.ObjectId != ConfigurationObjectId ||
            configuration.Kind != DokObjectKind.Inline || configuration.ClassId != 0xF1FBFFBA)
            throw new InvalidDataException("Field Actor List configuration requires its actual generic Data declaration.");
        uint count = Words(configuration, 0x91A41AD2, 1)[0];
        if (count > int.MaxValue)
            throw new InvalidDataException("Field Actor List row count exceeds the managed resource range.");
        int length = (int)count;
        if (length == 0) return new(source, [], []);
        uint[] anchors = Words(configuration, 0x507FD909, length);
        uint[] gates = Words(configuration, 0x103338F0, length);
        uint[] lists = Words(configuration, 0x8F48B83D, length);
        uint[] enabled = Words(configuration, 0x1416EE72, length);
        var rows = new ConfiguredFieldActorListRow[length];
        for (int index = 0; index < length; index++)
            rows[index] = new(anchors[index], gates[index], lists[index]);
        return new(source, rows, enabled.AsSpan(0, length).ToArray());
    }

    /// <summary>Append each actual source member with the native single-item
    /// v8 semantics. An allocation failure leaves earlier appends retained and
    /// does not suppress later attempts. Owner/frame validation stays in the
    /// existing list owner. This is not a rendering or readiness receipt.</summary>
    public ConfiguredFieldActorListAppendResult Append(OwnedWorldLists owner, long currentFrame,
        IReadOnlyList<uint> actualEnabledWords,
        IReadOnlyDictionary<uint, ConfiguredFieldActorListCurrentData> actualNamedData)
        => AppendRows(owner, currentFrame, actualEnabledWords, actualNamedData, 0, rows.Length);

    /// <summary>The normal F43DE0 caller uses this at each row's tail, after
    /// that row's preceding World callbacks and before the next row begins.</summary>
    public ConfiguredFieldActorListAppendResult AppendRow(int rowIndex, OwnedWorldLists owner,
        long currentFrame, IReadOnlyList<uint> actualEnabledWords,
        IReadOnlyDictionary<uint, ConfiguredFieldActorListCurrentData> actualNamedData)
    {
        if ((uint)rowIndex >= (uint)rows.Length) throw new ArgumentOutOfRangeException(nameof(rowIndex));
        return AppendRows(owner, currentFrame, actualEnabledWords, actualNamedData, rowIndex, rowIndex + 1);
    }

    private ConfiguredFieldActorListAppendResult AppendRows(OwnedWorldLists owner, long currentFrame,
        IReadOnlyList<uint> actualEnabledWords,
        IReadOnlyDictionary<uint, ConfiguredFieldActorListCurrentData> actualNamedData,
        int firstRow, int rowEnd)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(actualEnabledWords);
        ArgumentNullException.ThrowIfNull(actualNamedData);
        if (actualEnabledWords.Count != rows.Length)
            throw new ArgumentException("Current enabled words must belong to this configuration's actual rows.", nameof(actualEnabledWords));
        long attempts = 0, appended = 0;
        for (int index = firstRow; index < rowEnd; index++)
        {
            if (actualEnabledWords[index] == 0) continue;
            if (!Require(ModelListObjectId).Present || !Require(DestinationListObjectId).Present) continue;
            var row = rows[index];
            if (!Require(row.AnchorObjectId).Present) continue;
            var gate = Require(row.GateObjectId);
            if (!gate.Present) continue;
            int value = gate.SignedGateValue ?? throw new InvalidOperationException(
                $"Current Field Actor gate {row.GateObjectId:X8} has no actual signed {SignedGatePropertyHash:X8} value.");
            if (value <= 0) continue;
            var source = Require(row.SourceListObjectId);
            if (!source.Present) continue;
            bool sourceCpu = source.ListCpuPresent ?? throw new InvalidOperationException(
                $"Current Field Actor source {row.SourceListObjectId:X8} has unknown CPU presence.");
            bool destinationCpu = Require(DestinationListObjectId).ListCpuPresent ?? throw new InvalidOperationException(
                "Current 175E Actor List has unknown CPU presence.");
            if (!sourceCpu || !destinationCpu) continue;
            var members = source.ListMembers ?? throw new InvalidOperationException(
                $"Current Field Actor source {row.SourceListObjectId:X8} has no actual member span.");
            int count = members.Count;
            for (int memberIndex = 0; memberIndex < count; memberIndex++)
            {
                attempts++;
                if (owner.Append(currentFrame, DestinationListObjectId, [members[memberIndex]])) appended++;
            }
        }
        return new(attempts, appended);

        ConfiguredFieldActorListCurrentData Require(uint objectId)
        {
            if (objectId == 0) return new(false); // B3B6F0's exact null-name lookup.
            if (actualNamedData.TryGetValue(objectId, out var value) && value is not null) return value;
            throw new InvalidOperationException($"Current Field Actor named Data {objectId:X8} has not been resolved.");
        }
    }

    private static uint[] Words(DokObject source, uint hash, int minimumCount)
    {
        var matches = source.Properties.Where(p => p.NameHash == hash).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException($"Field Actor configuration property {hash:X8} is missing or repeated.");
        var property = matches[0];
        if (property.TypeCode != 5 || property.ElementSize != 4 || property.ElementCount < minimumCount ||
            property.RawValue.Length != checked(property.ElementCount * 4))
            throw new InvalidDataException($"Field Actor configuration property {hash:X8} has an invalid word-array shape.");
        if (hash == 0x91A41AD2 && property.ElementCount != 1)
            throw new InvalidDataException("Field Actor row count must be a scalar.");
        return Enumerable.Range(0, property.ElementCount).Select(index =>
            BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue.AsSpan(index * 4, 4))).ToArray();
    }
}
