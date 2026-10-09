using San14.Data;

public enum ArmyWorldPrimitiveParameterRoute { Unknown, ActualPriorRetained, ActualCurrentMaterial }

public sealed record ArmyWorldFlagPrimitiveMaterialTable(RemakeArmyFlagActor ActualActor,
    int SourceRawDrawIndex, uint ActualContainerGroupOrdinal, uint ActualMaterialObjectId,
    NativeWorldShaderTableState FinalTable, string SourceWriterEvidence)
{
    public NativeArmyFlagMaterialTextureGroupRecord? ActualCurrentTextures { get; init; }
    public ReadOnlyMemory<byte>? ActualMaterialRecord64 { get; init; }
    public int? ActualMaterialId { get; init; }
    /// <summary>DB8BBB/DBB291 preserve18 also preserves28/40. Texture38 and
    /// context30 are assigned before that gate and keep their own provenance.</summary>
    public ArmyWorldPrimitiveParameterRoute ActualParameterRoute {get;init;}
}

/// <summary>Reached regular texture-group ready writer. The material owner
/// receives the complete real group before controller/ready/final18 selection;
/// this does not imply that E0 currently equals this C0 group.</summary>
public interface IArmyWorldFlagMaterialResourceReceiver
{
    void ReceiveActualReadyRegularTextureGroup(NativeMapArmyPreparedFrame actualArmy,
        RemakeArmyFlagActor actualActor, int actualRawDraw,
        LoadedArmyFlagMaterialVariation actualVariation,
        NativeArmyFlagMaterialTextureGroupRecord actualCompleteGroup);
}

/// <summary>The reached container material assignment and primitive builder
/// supply this input. Loading a ColorVariation row is not the assignment. Its
/// container220/228 selection must be consumed into the actual final table.</summary>
public interface IArmyWorldFlagMaterialTablePublication
{
    ArmyWorldFlagPrimitiveMaterialTable ReadActualFinalTable(RemakeArmyFlagActor actualActor,
        int sourceDrawIndex, LoadedArmyFlagMaterialVariation? actualVariation);
    void RequireCurrent(NativeMapArmyPreparedFrame actualArmy);
    string SourceEvidence { get; }
}

/// <summary>Actual Primitive18 publication after base and any reached
/// material78/80 writer. The owning resource publishes the full table; a
/// selected state18 provider is not a replacement for this value.</summary>
public sealed record ArmyWorldPrimitiveTableRow(string CanonicalPacketKey,
    NativeWorldShaderTableState FinalTable, string SourceWriterEvidence)
{
    public ArmyWorldFlagPrimitiveMaterialTable? ActualFlagMaterial { get; init; }
}

/// <summary>Complete current table inventory supplied by the actual resource
/// and primitive publishers. Import/allocation occurs before this boundary;
/// reading or sorting candidates never allocates table ranks.</summary>
public sealed class ArmyWorldPrimitiveTablePublication
{
    private readonly NativeWorldShaderTableRegistry registry;
    private readonly Action requireActualCurrentPublication;
    private readonly IReadOnlyDictionary<string, ArmyWorldPrimitiveTableRow> byPacket;
    private readonly IReadOnlyDictionary<string, ArmyWorldPrimitiveAdmission> primitives;
    private ArmyWorldPrimitiveTablePublication(NativeWorldShaderTableRegistry actualRegistry,
        ArmyWorldPrimitivePublication actualPopulation, IReadOnlyList<ArmyWorldPrimitiveTableRow> rows,
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
    public ArmyWorldPrimitivePublication Population { get; }
    public IReadOnlyList<ArmyWorldPrimitiveTableRow> Rows { get; }
    public static ArmyWorldPrimitiveTablePublication CaptureActual(
        NativeWorldShaderTableRegistry actualRegistry, ArmyWorldPrimitivePublication actualPopulation,
        IReadOnlyList<ArmyWorldPrimitiveTableRow> completeActualFinalTables, Action requireActualCurrentPublication)
    {
        ArgumentNullException.ThrowIfNull(actualRegistry); ArgumentNullException.ThrowIfNull(actualPopulation);
        ArgumentNullException.ThrowIfNull(completeActualFinalTables); ArgumentNullException.ThrowIfNull(requireActualCurrentPublication);
        return new(actualRegistry, actualPopulation, completeActualFinalTables, requireActualCurrentPublication);
    }
    public static ArmyWorldPrimitiveTablePublication CaptureActualPreparedPackets(
        NativeWorldShaderTableRegistry actualRegistry, ArmyWorldPrimitivePublication actualPopulation,
        ArmyWorldQueuePreparedPackets actualBody, ArmyWorldQueuePreparedPackets actualFlags,
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
        foreach (var state in Rows.Select(r => r.FinalTable).Distinct<NativeWorldShaderTableState>(ReferenceEqualityComparer.Instance))
            registry.RequireCurrent(state);
    }
    public ArmyWorldPrimitiveTableRow ReadActualFinalTable(ArmyWorldPrimitiveAdmission actualPrimitive)
    {
        if (!primitives.TryGetValue(actualPrimitive.PacketKey, out var expected) || !ReferenceEquals(expected, actualPrimitive) ||
            !byPacket.TryGetValue(actualPrimitive.PacketKey, out var row))
            throw new InvalidDataException("Army table lookup belongs to another current CPU primitive publication.");
        registry.RequireCurrent(row.FinalTable); return row;
    }
    internal ModelShaderInterfaceSource ReadSelectedInterface(NativeWorldShaderTableState actualTable, int stateIndex)
        => registry.ReadSelectedInterface(actualTable, stateIndex);
}
