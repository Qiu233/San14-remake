using Godot;
using San14.Data;

/// <summary>Complete canonical source packet inventory for a current CPU
/// Army prefix. Preparation, visibility/counts and render submission are
/// separate; cloth producer images must outlive the consuming queue owner.</summary>
public sealed class OriginalArmyWorldQueuePreparedPackets
{
    private readonly Action requireCurrent;
    internal OriginalArmyWorldQueuePreparedPackets(OriginalNativeMapArmyPreparedFrame frame, string family,
        IReadOnlyList<OriginalModelGbufferInstancePacket> packets,
        IReadOnlyList<OriginalArmyWorldQueuePrimitiveSource> sources,
        IReadOnlyList<OriginalArmyWorldPrimitiveTableRow> actualFinalTables, Action current)
        => (Frame, Family, Packets, Sources, FinalTables, requireCurrent) = (frame, family,
            Array.AsReadOnly(packets.ToArray()), Array.AsReadOnly(sources.ToArray()), Array.AsReadOnly(actualFinalTables.ToArray()), current);
    public OriginalNativeMapArmyPreparedFrame Frame { get; }
    public string Family { get; }
    public IReadOnlyList<OriginalModelGbufferInstancePacket> Packets { get; }
    public IReadOnlyList<OriginalArmyWorldQueuePrimitiveSource> Sources { get; }
    public IReadOnlyList<OriginalArmyWorldPrimitiveTableRow> FinalTables { get; }
    public void RequireCurrent() => requireCurrent();

    internal static OriginalModelRasterInputs PreparationRaster(IReadOnlyList<OriginalModelBlendAttachment> actualColor)
        => new(RenderingDevice.PolygonCullMode.Back, RenderingDevice.PolygonFrontFace.CounterClockwise,
            true, true, RenderingDevice.CompareOperator.GreaterOrEqual,
            OriginalArmy48GraphicsContract.PrepareAttachments(actualColor));
    internal static void RequireColorInputScope(OriginalNativeMapArmyPreparedFrame frame,
        IReadOnlyList<OriginalModelBlendAttachment> actualColor)
    {
        ArgumentNullException.ThrowIfNull(actualColor);
        if (actualColor.Count == 0 && (frame.Ordinary.Count != 0 || frame.Flags.Count != 0 ||
            frame.OrdinaryActors != 0 || frame.FlagActors != 0 ||
            frame.CpuPrimitivePopulation is not { Actors.Count: 0, CompleteSourcePrimitiveCount: 0 }))
            throw new NotSupportedException("Absent packet raster input requires the actual complete zero-actor/primitive Army inventory.");
    }
    internal static void RequireFrame(OriginalNativeMapArmyPreparedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame); frame.Animation.Validate();
        if (frame.Unresolved.Count != 0 || frame.CpuPrimitivePopulation is not { } population ||
            !ReferenceEquals(population.Context, frame.Context) || frame.Context.PreparedCamera is null)
            throw new InvalidDataException("Army World queue preparation needs the actual complete current BeforeFrame CPU owner and Camera.");
    }
}

/// <summary>One source-load cache per renderer lifetime; frame lookups never
/// rehash a graph/shader or replay its constructors.</summary>
internal sealed class OriginalArmyWorldQueueSourceCache
{
    private readonly Dictionary<(OriginalNamedModelShader FullTable, int StateIndex, LoadedShaderContainer Shader, LoadedModelShaderBindings Model, int Draw),
        OriginalArmyWorldQueuePrimitiveSource> cached = [];
    private OriginalModelQueueStateSource? opaque, alpha;
    public OriginalArmyWorldQueuePrimitiveSource Read(string packet, LoadedRenderGraph graph,
        LoadedModelShaderBindings actualModel, int draw, OriginalModelShaderInterfaceSource actualSelected,
        LoadedShaderContainer actualShader)
    {
        var key = (actualSelected.Owner, actualSelected.StateIndex, actualShader, actualModel, draw);
        if (!cached.TryGetValue(key, out var source))
        {
            opaque ??= OriginalModelRendererStateSourceReader.ReadQueue(graph, 49);
            alpha ??= OriginalModelRendererStateSourceReader.ReadQueue(graph, 50);
            cached.Add(key, source = new(packet,
                OriginalModelOrdinaryQueueSelectionSource.ReadFreshSelected(opaque, actualSelected, actualShader),
                OriginalModelOrdinaryQueueSelectionSource.ReadFreshSelected(alpha, actualSelected, actualShader),
                OriginalModelRendererStateSourceReader.ReadGroupRaster(actualModel, draw)));
        }
        return source with { PacketKey = packet };
    }
}
