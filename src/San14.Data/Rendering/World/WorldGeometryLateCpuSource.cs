using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct WorldGeometryLateCurrentContext(bool CurrentTablePresent,
    uint ContextFlags3E4, uint ContextFlags3F0, uint SelectedViewWord3F8, bool SelectedViewPresent)
{
    public uint SelectedViewIndex => SelectedViewWord3F8 & 0x7FFFFFFFu;
}
public readonly record struct WorldGeometryLateDepth(bool Test, bool Write, uint Compare, bool Stencil);
public readonly record struct WorldGeometryNode57CpuRecipe(bool HasCurrentView,
    WorldGeometryLateDepth? Depth, uint ResultContextFlags3F0, bool RequiresSpecialized1B18Consumer);
public readonly record struct WorldGeometryNode58CpuEntry(bool HasCurrentView,
    bool DispatcherAuxAccepted, bool EntersOrdinaryDispatcher, int? ViewRecordCount, WorldGeometryLateDepth? Depth);

/// <summary>Loaded graph/native facts for the finite ordinary57/58 CPU path.
/// Current population, material final tables and parameter helpers are inputs;
/// none of these source recipes certify that a node returned or pixels exist.</summary>
public sealed class LoadedWorldGeometryLateCpuSource
{
    internal LoadedWorldGeometryLateCpuSource(WorldGeometryLateCpuNativeSource native,
        LoadedRenderGraph graph, ModelQueueStateSource queue)
        => (Native, Graph, Queue58) = (native, graph, queue);
    public WorldGeometryLateCpuNativeSource Native { get; }
    public LoadedRenderGraph Graph { get; }
    public ModelQueueStateSource Queue58 { get; }
    public int ShaderStateIndex => 2;
    public int OrdinaryBankSelector => 0;
    public string SourceEvidence => $"{RenderRuleIdentity.For("WorldGeometryLateCpu",Native.Locale)};" +
        "TC:E9D280-D33F00/EA6470-D43190-D362B0;SC:E9E270-D34C30/EA7460-D43EC0-D36FE0;" +
        "state2-separate-from-bank0;source-only-no-GPU-or-node-return";

    public WorldGeometryNode57CpuRecipe Prepare57(WorldGeometryLateCurrentContext current,
        bool currentCommon240Present, bool currentCommon240Bit2, object? currentView1B18)
    {
        bool view = current.CurrentTablePresent && current.SelectedViewIndex < 32 && current.SelectedViewPresent;
        if (!view) return new(false, null, current.ContextFlags3F0, false);
        uint next = current.ContextFlags3F0 | 0x40u; // D33F69 precedes both later gates.
        bool reachesOwner = !currentCommon240Present || currentCommon240Bit2;
        return new(true, new(true, true, (current.ContextFlags3E4 & 0x20000000) != 0 ? 6u : 3u, false),
            next, reachesOwner && currentView1B18 is not null);
    }

    public WorldGeometryNode58CpuEntry Prepare58Entry(WorldGeometryLateCurrentContext current,
        bool actualCommon240Present, uint actualCommonFlags8,
        int actualView19D4Stored, int actualView19D0Capacity, object? currentView1C18)
    {
        bool view = current.CurrentTablePresent && unchecked((int)current.ContextFlags3F0) >= 0 &&
            current.SelectedViewIndex < 32 && current.SelectedViewPresent;
        if (!view) return new(false, false, false, null, null);
        if (currentView1C18 is not null)
            throw new NotSupportedException("Current node58 shape1C18 requires its actual D48E10 consumer.");
        // D43430..38 tests Aux+8.bit0 before D434EC reads View19D4.
        // A rejected dispatcher does not publish an invented zero row count.
        bool auxAccepted = !actualCommon240Present || (actualCommonFlags8 & 1) != 0;
        if (!auxAccepted) return new(true, false, false, null, null);
        int count = EffectiveCurrentCount(actualView19D4Stored, actualView19D0Capacity);
        return new(true, true, count != 0, count, count == 0 ? null : new(true, false, 2, false));
    }
    public ModelOrdinaryDispatcherStencilPacket Prepare58Record(
        ModelOrdinaryDispatcherRecord current, MapCommonRenderState common, uint contextFlags3E4)
        => ModelOrdinaryDispatcherStencilSource.Prepare(Queue58, current, common, contextFlags3E4);
    public static int EffectiveCurrentCount(int stored, int capacity)
    {
        if (capacity < 0) throw new InvalidDataException("Current counter capacity is invalid.");
        return stored >= 0 ? stored : Math.Min(capacity, -1 - stored);
    }
    public WorldGeometryLateDepth Prepare58ExitDepth(uint actualContextFlags3E4, WorldGeometryLateDepth actualCurrentDepth)
        => actualCurrentDepth with { Write = true, Compare = (actualContextFlags3E4 & 0x20000000) != 0 ? 6u : 3u };
}

public static class WorldGeometryLateCpuSource
{
    public static LoadedWorldGeometryLateCpuSource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var native = RenderSamplerSourceAdapters.LoadWorldGeometryLateCpuNativeSource(repository.SourceDirectory, locale);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        if (graph.Source.OverrideSource is not null || graph.Source.ResourceUri != "san14://rdb/system/024496EE")
            throw new InvalidDataException("Late geometry CPU source requires its original graph at import.");
        graph = graph with { Document = RenderGraphReader.Read(graph.Source.Bytes) };
        var n57 = graph.Document.Nodes[57]; var n58 = graph.Document.Nodes[58];
        if (n57.Call.FactoryClassId != 0xC3A7FC79 || n57.Call.LookupKey != 0x3FF7B2C9 ||
            !n57.Call.ConnectedNodeIndices.SequenceEqual(new uint[] {58}) ||
            n58.Call.FactoryClassId != 0xFC64DCB6 || n58.Call.LookupKey != 0x69E458CB ||
            !n58.Call.ConnectedNodeIndices.SequenceEqual(new uint[] {59}))
            throw new InvalidDataException("Actual geometry57/58 source nodes/edges differ.");
        uint Word(uint name, uint descriptor, int width)
        {
            var field = n57.Call.Arguments.Single(a => a.NameHash == name);
            if (field.DescriptorWord != descriptor || field.RawValue.Length != width)
                throw new InvalidDataException("Geometry57 argument has another source type/width.");
            return width == 1 ? field.RawValue[0] : BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
        if (Word(0x72DB7EBD, 0x05000001, 4) != 0xE1B6A687 || Word(0x9075EDA0, 0x01000001, 1) != 0 ||
            Word(0xA98962A5, 0x01000001, 1) != 2 || Word(0xCEB960BA, 0x01000001, 1) != 1 ||
            Word(0x56AB46F0, 0x05000001, 4) != 0xE13DDB4F || Word(0x31C6BD74, 0x05000001, 4) != 0x7E94C315)
            throw new InvalidDataException("Geometry57 does not select its actual specialized state2/View0/D1/NS profile.");
        var queue = ModelRendererStateSourceReader.ReadQueue(graph, 58);
        if (queue.Flag != 0xE || queue.Flag2 != 2 || queue.RenderType != 0 || queue.ViewId != 0 ||
            queue.ViewObjectHash != 0xE1B6A687 || queue.ShaderStateOffset != 2 || queue.ClockwiseRendering || queue.BackendAlphaEnabled)
            throw new InvalidDataException("Geometry58 requires its actual ordinary bank0/state2 source profile.");
        return new(native, graph, queue);
    }
}
