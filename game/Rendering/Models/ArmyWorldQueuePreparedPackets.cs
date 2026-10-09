using Godot;
using San14.Data;

/// <summary>Complete canonical source packet inventory for a current CPU
/// Army prefix. Preparation, visibility/counts and render submission are
/// separate; cloth producer images must outlive the consuming queue owner.</summary>
public sealed class ArmyWorldQueuePreparedPackets
{
    private readonly Action requireCurrent;
    internal ArmyWorldQueuePreparedPackets(NativeMapArmyPreparedFrame frame, string family,
        IReadOnlyList<ModelGbufferInstancePacket> packets,
        IReadOnlyList<ArmyWorldQueuePrimitiveSource> sources,
        IReadOnlyList<ArmyWorldPrimitiveTableRow> actualFinalTables, Action current)
        => (Frame, Family, Packets, Sources, FinalTables, requireCurrent) = (frame, family,
            Array.AsReadOnly(packets.ToArray()), Array.AsReadOnly(sources.ToArray()), Array.AsReadOnly(actualFinalTables.ToArray()), current);
    public NativeMapArmyPreparedFrame Frame { get; }
    public string Family { get; }
    public IReadOnlyList<ModelGbufferInstancePacket> Packets { get; }
    public IReadOnlyList<ArmyWorldQueuePrimitiveSource> Sources { get; }
    public IReadOnlyList<ArmyWorldPrimitiveTableRow> FinalTables { get; }
    public void RequireCurrent() => requireCurrent();

    internal static ModelRasterInputs PreparationRaster(IReadOnlyList<ModelBlendAttachment> actualColor)
        => new(RenderingDevice.PolygonCullMode.Back, RenderingDevice.PolygonFrontFace.CounterClockwise,
            true, true, RenderingDevice.CompareOperator.GreaterOrEqual,
            Army48GraphicsContract.PrepareAttachments(actualColor));
    internal static void RequireColorInputScope(NativeMapArmyPreparedFrame frame,
        IReadOnlyList<ModelBlendAttachment> actualColor)
    {
        ArgumentNullException.ThrowIfNull(actualColor);
        if (actualColor.Count == 0 && (frame.Ordinary.Count != 0 || frame.Flags.Count != 0 ||
            frame.OrdinaryActors != 0 || frame.FlagActors != 0 ||
            frame.CpuPrimitivePopulation is not { Actors.Count: 0, CompleteSourcePrimitiveCount: 0 }))
            throw new NotSupportedException("Absent packet raster input requires the actual complete zero-actor/primitive Army inventory.");
    }
    internal static void RequireFrame(NativeMapArmyPreparedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame); frame.Animation.Validate();
        if (frame.Unresolved.Count != 0 || frame.CpuPrimitivePopulation is not { } population ||
            !ReferenceEquals(population.Context, frame.Context) || frame.Context.PreparedCamera is null)
            throw new InvalidDataException("Army World queue preparation needs the actual complete current BeforeFrame CPU owner and Camera.");
    }
}

/// <summary>One source-load cache per renderer lifetime; frame lookups never
/// rehash a graph/shader or replay its constructors.</summary>
internal sealed class ArmyWorldQueueSourceCache
{
    private readonly Dictionary<(NamedModelShader FullTable, int StateIndex, LoadedShaderContainer Shader, LoadedModelShaderBindings Model, int Draw),
        ArmyWorldQueuePrimitiveSource> cached = [];
    private ModelQueueStateSource? opaque, alpha;
    public ArmyWorldQueuePrimitiveSource Read(string packet, LoadedRenderGraph graph,
        LoadedModelShaderBindings actualModel, int draw, ModelShaderInterfaceSource actualSelected,
        LoadedShaderContainer actualShader)
    {
        var key = (actualSelected.Owner, actualSelected.StateIndex, actualShader, actualModel, draw);
        if (!cached.TryGetValue(key, out var source))
        {
            opaque ??= ModelRendererStateSourceReader.ReadQueue(graph, 49);
            alpha ??= ModelRendererStateSourceReader.ReadQueue(graph, 50);
            cached.Add(key, source = new(packet,
                ModelOrdinaryQueueSelectionSource.ReadFreshSelected(opaque, actualSelected, actualShader),
                ModelOrdinaryQueueSelectionSource.ReadFreshSelected(alpha, actualSelected, actualShader),
                ModelRendererStateSourceReader.ReadGroupRaster(actualModel, draw)));
        }
        return source with { PacketKey = packet };
    }
}
