using San14.Data;

public enum OriginalArmyWorldPrimitiveParameterRoute { Unknown, ActualPriorRetained, ActualCurrentMaterial }

public sealed record OriginalArmyWorldFlagPrimitiveMaterialTable(OriginalRemakeArmyFlagActor ActualActor,
    int SourceRawDrawIndex, uint ActualContainerGroupOrdinal, uint ActualMaterialObjectId,
    OriginalNativeWorldShaderTableState FinalTable, string SourceWriterEvidence)
{
    public OriginalNativeArmyFlagMaterialTextureGroupRecord? ActualCurrentTextures { get; init; }
    public ReadOnlyMemory<byte>? ActualMaterialRecord64 { get; init; }
    public int? ActualMaterialId { get; init; }
    /// <summary>DB8BBB/DBB291 preserve18 also preserves28/40. Texture38 and
    /// context30 are assigned before that gate and keep their own provenance.</summary>
    public OriginalArmyWorldPrimitiveParameterRoute ActualParameterRoute {get;init;}
}

/// <summary>Reached regular texture-group ready writer. The material owner
/// receives the complete real group before controller/ready/final18 selection;
/// this does not imply that E0 currently equals this C0 group.</summary>
public interface IOriginalArmyWorldFlagMaterialResourceReceiver
{
    void ReceiveActualReadyRegularTextureGroup(OriginalNativeMapArmyPreparedFrame actualArmy,
        OriginalRemakeArmyFlagActor actualActor, int actualRawDraw,
        LoadedOriginalArmyFlagMaterialVariation actualVariation,
        OriginalNativeArmyFlagMaterialTextureGroupRecord actualCompleteGroup);
}

/// <summary>The reached container material assignment and primitive builder
/// supply this input. Loading a ColorVariation row is not the assignment. Its
/// container220/228 selection must be consumed into the actual final table.</summary>
public interface IOriginalArmyWorldFlagMaterialTablePublication
{
    OriginalArmyWorldFlagPrimitiveMaterialTable ReadActualFinalTable(OriginalRemakeArmyFlagActor actualActor,
        int sourceDrawIndex, LoadedOriginalArmyFlagMaterialVariation? actualVariation);
    void RequireCurrent(OriginalNativeMapArmyPreparedFrame actualArmy);
    string SourceEvidence { get; }
}

/// <summary>Actual Primitive18 publication after base and any reached
/// material78/80 writer. The owning resource publishes the full table; a
/// selected state18 provider is not a replacement for this value.</summary>
public sealed record OriginalArmyWorldPrimitiveTableRow(string CanonicalPacketKey,
    OriginalNativeWorldShaderTableState FinalTable, string SourceWriterEvidence)
{
    public OriginalArmyWorldFlagPrimitiveMaterialTable? ActualFlagMaterial { get; init; }
}

/// <summary>Complete current table inventory supplied by the actual resource
/// and primitive publishers. Import/allocation occurs before this boundary;
/// reading or sorting candidates never allocates table ranks.</summary>
public sealed class OriginalArmyWorldPrimitiveTablePublication
{
    private readonly OriginalNativeWorldShaderTableRegistry registry;
    private readonly Action requireActualCurrentPublication;
    private readonly IReadOnlyDictionary<string, OriginalArmyWorldPrimitiveTableRow> byPacket;
    private readonly IReadOnlyDictionary<string, OriginalArmyWorldPrimitiveAdmission> primitives;
    private OriginalArmyWorldPrimitiveTablePublication(OriginalNativeWorldShaderTableRegistry actualRegistry,
        OriginalArmyWorldPrimitivePublication actualPopulation, IReadOnlyList<OriginalArmyWorldPrimitiveTableRow> rows,
        Action current)
    {
        registry = actualRegistry; Population = actualPopulation; requireActualCurrentPublication = current;
        if (rows.Any(r => r is null || r.FinalTable is null || string.IsNullOrWhiteSpace(r.SourceWriterEvidence)) ||
            rows.Select(r => r.CanonicalPacketKey).Distinct(StringComparer.Ordinal).Count() != rows.Count ||
            !rows.Select(r => r.CanonicalPacketKey).ToHashSet(StringComparer.Ordinal)
                .SetEquals(actualPopulation.Primitives.Select(p => p.PacketKey)))
            throw new InvalidDataException("Army Primitive18 publication needs every canonical primitive and its actual final table writer.");
        Rows = Array.AsReadOnly(rows.ToArray());
        byPacket = Rows.ToDictionary(r => r.CanonicalPacketKey, StringComparer.Ordinal);
        primitives = Population.Primitives.ToDictionary(p => p.PacketKey, StringComparer.Ordinal);
        RequireCurrent();
    }
    public OriginalArmyWorldPrimitivePublication Population { get; }
    public IReadOnlyList<OriginalArmyWorldPrimitiveTableRow> Rows { get; }
    public static OriginalArmyWorldPrimitiveTablePublication CaptureActual(
        OriginalNativeWorldShaderTableRegistry actualRegistry, OriginalArmyWorldPrimitivePublication actualPopulation,
        IReadOnlyList<OriginalArmyWorldPrimitiveTableRow> completeActualFinalTables, Action requireActualCurrentPublication)
    {
        ArgumentNullException.ThrowIfNull(actualRegistry); ArgumentNullException.ThrowIfNull(actualPopulation);
        ArgumentNullException.ThrowIfNull(completeActualFinalTables); ArgumentNullException.ThrowIfNull(requireActualCurrentPublication);
        return new(actualRegistry, actualPopulation, completeActualFinalTables, requireActualCurrentPublication);
    }
    public static OriginalArmyWorldPrimitiveTablePublication CaptureActualPreparedPackets(
        OriginalNativeWorldShaderTableRegistry actualRegistry, OriginalArmyWorldPrimitivePublication actualPopulation,
        OriginalArmyWorldQueuePreparedPackets actualBody, OriginalArmyWorldQueuePreparedPackets actualFlags,
        Action requireActualCurrentPublication)
    {
        ArgumentNullException.ThrowIfNull(actualBody); ArgumentNullException.ThrowIfNull(actualFlags);
        if (!ReferenceEquals(actualBody.Frame, actualFlags.Frame) || actualBody.Family != "ArmyOrdinary" || actualFlags.Family != "ArmyFlag")
            throw new InvalidDataException("Army final-table inventory requires the same actual complete body/flag preparation.");
        if (actualFlags.FinalTables.Any(row => row.ActualFlagMaterial is not { } material || !ReferenceEquals(material.FinalTable, row.FinalTable)))
            throw new InvalidDataException("Army flag prepared-table publication needs its actual typed current container material receipt.");
        return CaptureActual(actualRegistry, actualPopulation, actualBody.FinalTables.Concat(actualFlags.FinalTables).ToArray(), () =>
        { actualBody.RequireCurrent(); actualFlags.RequireCurrent(); requireActualCurrentPublication(); });
    }
    public void RequireCurrent()
    {
        requireActualCurrentPublication();
        foreach (var state in Rows.Select(r => r.FinalTable).Distinct<OriginalNativeWorldShaderTableState>(ReferenceEqualityComparer.Instance))
            registry.RequireCurrent(state);
    }
    public OriginalArmyWorldPrimitiveTableRow ReadActualFinalTable(OriginalArmyWorldPrimitiveAdmission actualPrimitive)
    {
        if (!primitives.TryGetValue(actualPrimitive.PacketKey, out var expected) || !ReferenceEquals(expected, actualPrimitive) ||
            !byPacket.TryGetValue(actualPrimitive.PacketKey, out var row))
            throw new InvalidDataException("Army table lookup belongs to another current CPU primitive publication.");
        registry.RequireCurrent(row.FinalTable); return row;
    }
    internal OriginalModelShaderInterfaceSource ReadSelectedInterface(OriginalNativeWorldShaderTableState actualTable, int stateIndex)
        => registry.ReadSelectedInterface(actualTable, stateIndex);
}
