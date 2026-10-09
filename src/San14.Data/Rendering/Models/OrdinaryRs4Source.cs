using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum OrdinaryRs4InterfaceBoundary { ShortTable, NullInterface, RequiresRenderer }
public sealed record OrdinaryRs4FinalInterface(int QueueNode, int StateIndex,
    NamedModelShader FinalTable, int StateCount, ModelShaderInterfaceSource? Interface,
    OrdinaryRs4InterfaceBoundary Boundary);
public sealed record OrdinaryRs4CurrentEntry(bool HasSelectedView, bool DispatcherAccepted,
    int? CurrentRecordCount, bool EntersRecordDispatcher);
public sealed record OrdinaryRs4PixelProgram(int ProgramIndex, string ProgramSha256,
    IReadOnlyList<DxbcSignatureParameter> Outputs)
{
    public bool DeclaresRs4Target6 => Outputs.Any(p => p.SemanticName.Text.Equals("SV_Target", StringComparison.OrdinalIgnoreCase)
        && p.SemanticIndex == 6 && p.Register == 6);
}

/// <summary>Loaded program declarations for a nonnull, final type16 slot.
/// Program selection reads the retained matrix; it does not rehash/reparse G1S.
/// Output declarations are not coverage, blend, or initialized texture content.</summary>
public sealed class LoadedOrdinaryRs4Renderer
{
    internal LoadedOrdinaryRs4Renderer(OrdinaryRs4FinalInterface selected,
        LoadedShaderContainer shader, G1ShaderBindingTables bindings,
        IReadOnlyDictionary<int, OrdinaryRs4PixelProgram> programs)
        => (Selected, Shader, Bindings, PixelPrograms) = (selected, shader, bindings, programs);
    public OrdinaryRs4FinalInterface Selected { get; }
    public LoadedShaderContainer Shader { get; }
    public G1ShaderBindingTables Bindings { get; }
    public IReadOnlyDictionary<int, OrdinaryRs4PixelProgram> PixelPrograms { get; }
    public OrdinaryRs4PixelProgram? SelectPixelProgram(ReadOnlySpan<uint> actualDimensions, uint actualPass)
    {
        var combination = Bindings.ResolveCombination(actualDimensions, actualPass);
        var pixel = combination.Stages.Single(s => s.StageCode == 0);
        return pixel.Entry is null ? null : PixelPrograms[pixel.Entry.ProgramIndex];
    }
}

/// <summary>Actual earlier node43/44/45/46/47 source; node150/151/152 are
/// retained separately as the later state23 comparison. No node48/53 alias,
/// constructor clear, or shader-state18 population authorizes this stage.</summary>
public sealed class LoadedOrdinaryRs4Source
{
    private readonly Dictionary<(NamedModelShader Table, int State), OrdinaryRs4FinalInterface> interfaces = [];
    internal LoadedOrdinaryRs4Source(WorldGeometryLateCpuNativeSource native,
        LoadedRenderGraphTargets targets, LoadedRenderGraphTargets lateTargets,
        IReadOnlyDictionary<int, ModelQueueStateSource> queues, WorldGeometryPopNode pop)
        => (Native, Targets43, ComparisonTargets150, Queues, Pop47) = (native, targets, lateTargets, queues, pop);
    public WorldGeometryLateCpuNativeSource Native { get; }
    public LoadedRenderGraphTargets Targets43 { get; }
    public LoadedRenderGraphTargets ComparisonTargets150 { get; }
    public IReadOnlyDictionary<int, ModelQueueStateSource> Queues { get; }
    public WorldGeometryPopNode Pop47 { get; }
    public string SourceEvidence => $"{RenderRuleIdentity.For("OrdinaryRs4",Native.Locale)};" +
        "graph43-seven-MRT/44-45-state22/46-state13/47;EA6470-D43190-D362B0;" +
        "actual-View0/current-first-bank/final-type16;150-state23-separate;no-pixel-ready-claim";

    /// <summary>The full retained table belongs to the actual final primitive
    /// publication, after material override. Caller retains that table's owner
    /// lifetime. Null and short slots skip shaders; they do not erase tuples.</summary>
    public OrdinaryRs4FinalInterface ReadFinalInterface(int queueNode, NamedModelShader actualFinalTable)
    {
        ArgumentNullException.ThrowIfNull(actualFinalTable);
        var queue = Queues[queueNode];
        var key = (actualFinalTable, (int)queue.ShaderStateOffset);
        if (interfaces.TryGetValue(key, out var selected)) return selected with { QueueNode = queueNode };
        if (!actualFinalTable.HasVerifiedType16Creator)
            throw new NotSupportedException("RS4 needs the actual source-created final type16 table.");
        int count = checked(actualFinalTable.DependencyObjectHashes.Count + 1);
        var slot = count > queue.ShaderStateOffset
            ? ModelShaderResources.ResolveType16Interface(actualFinalTable, queue.ShaderStateOffset) : null;
        selected = new(queueNode, queue.ShaderStateOffset, actualFinalTable, count, slot,
            slot is null ? OrdinaryRs4InterfaceBoundary.ShortTable : slot.IsNull
                ? OrdinaryRs4InterfaceBoundary.NullInterface : OrdinaryRs4InterfaceBoundary.RequiresRenderer);
        interfaces.Add(key, selected); return selected;
    }

    /// <summary>Current EA6470 selection followed by D43190's Aux8.bit0 and
    /// actual View19D4/19D0 count. Record count is independent of primitive
    /// counts. Nonnull alternate shape1C18 requires its separate dispatcher.</summary>
    public OrdinaryRs4CurrentEntry PrepareCurrentEntry(int queueNode,
        WorldGeometryLateCurrentContext current, bool actualCommon240Present,
        uint actualCommonFlags8, int actualView19D4Stored, int actualView19D0Capacity,
        object? actualView1C18)
    {
        var queue = Queues[queueNode];
        if (queueNode is not (44 or 45 or 46))
            throw new NotSupportedException("Later151/152 requires its actual CheckSkip/frame-flag entry; comparison metadata does not authorize it.");
        uint view = unchecked(current.SelectedViewIndex + queue.ViewId);
        bool selected = current.CurrentTablePresent && unchecked((int)current.ContextFlags3F0) >= 0 &&
            view < 32 && current.SelectedViewPresent;
        if (!selected) return new(false, false, null, false);
        if (actualView1C18 is not null)
            throw new NotSupportedException("RS4 alternate shape1C18 needs its actual D48E10 consumer.");
        bool accepted = !actualCommon240Present || (actualCommonFlags8 & 1) != 0;
        if (!accepted) return new(true, false, null, false);
        int count = LoadedWorldGeometryLateCpuSource.EffectiveCurrentCount(actualView19D4Stored, actualView19D0Capacity);
        return new(true, true, count, count != 0);
    }

    public WorldGeometryCurrentPop PrepareCurrentPop(int currentQ3F0, bool actualTablePresent,
        WorldGeometryHostFields actualHost)
    {
        if (currentQ3F0 < 0 || !actualTablePresent) return new(false, Pop47.EffectiveFlags, false, false);
        if (actualHost.Mode != 0)
            throw new NotSupportedException("RS4 pop47 nonzero Host mode requires its actual E86E40 consumer.");
        return new(true, Pop47.EffectiveFlags, false, Pop47.CopiesDepthBeforePop);
    }

    /// <summary>Resource load/reload boundary for a newly encountered nonnull
    /// final interface. Seven-dimension/pass values are supplied later by the
    /// real renderer owner; zero indices are not inferred from initialization.</summary>
    public LoadedOrdinaryRs4Renderer LoadRenderer(GameResourceRepository repository,
        OrdinaryRs4FinalInterface selected)
    {
        var provider = selected.Interface?.Provider ?? throw new InvalidDataException("A short/null RS4 interface has no renderer.");
        var shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
        if (Convert.ToHexString(SHA256.HashData(shader.Source.Bytes)) != shader.Source.DecodedSha256)
            throw new InvalidDataException("RS4 renderer bytes differ at resource load.");
        var bindings = shader.ReadG1Bindings();
        var programs = bindings.Combinations.Rows.SelectMany(c => c.Stages.Where(s => s.StageCode == 0 && s.Entry is not null))
            .Select(s => s.Entry!.ProgramIndex).Distinct().ToDictionary(index => index, index =>
                new OrdinaryRs4PixelProgram(index,
                    Convert.ToHexString(SHA256.HashData(shader.Container.GetProgramBytes(shader.Source.Bytes, index))),
                    Array.AsReadOnly(shader.ReadSignatures(index).Parts.Where(p => p.Kind == DxbcSignatureKind.Output)
                        .SelectMany(p => p.Parameters).ToArray())));
        return new(selected, shader, bindings, programs);
    }
}

public static class OrdinaryRs4Source
{
    public static LoadedOrdinaryRs4Source Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Read(repository.LoadRenderGraph("system", 0x024496EE), repository.LoadObjects("system", 0x93FB3280),
            RenderSamplerSourceAdapters.LoadWorldGeometryLateCpuNativeSource(repository.SourceDirectory, locale));
    }

    /// <summary>Import boundary; allows the normal owner to reuse its already
    /// loaded native source windows and original graph/texture catalogue.</summary>
    public static LoadedOrdinaryRs4Source Read(LoadedRenderGraph graph, LoadedObjects textures,
        WorldGeometryLateCpuNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(graph); ArgumentNullException.ThrowIfNull(textures); ArgumentNullException.ThrowIfNull(native);
        if (graph.Source.ResourceUri != "san14://rdb/system/024496EE" ||
            graph.Source.OverrideSource is not null)
            throw new InvalidDataException("RS4 import requires the exact original graph.");
        graph = graph with { Document = RenderGraphReader.Read(graph.Source.Bytes) };
        var targets = RenderGraphTargetResources.Read(graph, 43, textures);
        var late = RenderGraphTargetResources.Read(graph, 150, textures);
        var expected = new[] { (0x8B18CE44u, 0u), (0x7E94C315u, 0u), (0x521C7DEDu, 0u), (0x521C7DEDu, 1u),
            (0x521C7DEDu, 2u), (0x85C125A6u, 1u), (0x521C7DEDu, 4u) };
        var lateExpected = new[] { (0x8B18CE44u,0u),(0x7E94C315u,0u),(0x521C7DEDu,0u),(0x85C125A6u,0u),
            (0x521C7DEDu,2u),(0x85C125A6u,1u),(0x521C7DEDu,4u),(0x521C7DEDu,1u) };
        foreach (var target in new[] { targets, late })
            if (target.Flag != (target.Node.Index == 43 ? 0x46 : 0x06) || target.ColorTargets.Count != (target.Node.Index==43?7:8) ||
                !target.ColorTargets.Select(t => (t.ObjectNameHash, t.SurfaceId)).SequenceEqual(target.Node.Index==43?expected:lateExpected) ||
                target.DepthObjectNameHash != 0xE13DDB4F || target.DepthSurfaceId != 0 ||
                target.Node.Call.LookupKey != (target.Node.Index == 43 ? 0xFACB46E2 : 0x80E99DA2) ||
                !target.Node.Call.ConnectedNodeIndices.SequenceEqual(new[] { (uint)target.Node.Index + 1 }))
                throw new InvalidDataException("RS4 target bind does not retain its actual attachments and depth.");
        var queues = new Dictionary<int, ModelQueueStateSource>();
        foreach (var profile in new[] { (44, 0x45D6C82Cu, 0xEu, 0u, 22), (45, 0x392D671Du, 6u, 0u, 22),
            (46, 0xA48F4DC8u, 0xEu, 2u, 13), (151, 0xCB4B296Cu, 0x4Eu, 0u, 23), (152, 0xC7D1E677u, 0x46u, 0u, 23) })
        {
            var queue = ModelRendererStateSourceReader.ReadQueue(graph, profile.Item1);
            if (queue.Node.Call.LookupKey != profile.Item2 || queue.Flag != profile.Item3 || queue.Flag2 != profile.Item4 ||
                queue.ShaderStateOffset != profile.Item5 || queue.RenderType != 0 || queue.ViewObjectHash != 0xE1B6A687 ||
                queue.ViewId != 0 || queue.ClockwiseRendering ||
                !queue.Node.Call.ConnectedNodeIndices.SequenceEqual(new[] { (uint)queue.Node.Index + 1 }))
                throw new InvalidDataException("RS4 primitive queue differs from its actual source profile.");
            queues.Add(queue.Node.Index, queue);
        }
        var node = graph.Document.Nodes[47]; var arg = node.Call.Arguments.Single();
        if (node.Call.FactoryClassId != 0x1F7C5CF8 || node.Call.LookupKey != 0x09912A70 ||
            !node.Call.ConnectedNodeIndices.SequenceEqual(new uint[] {48}) || arg.NameHash != 0x636CA9AE ||
            arg.DescriptorWord != 0x05000001 || arg.RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(arg.RawValue) != 0x0100007F)
            throw new InvalidDataException("RS4 pop47 needs its actual DWORD flags and edge48.");
        var pop = new WorldGeometryPopNode(47, node.Call.LookupKey, node.Call.FactoryClassId,
            Array.AsReadOnly(node.Call.ConnectedNodeIndices.ToArray()), 0x0100007F,
            Array.AsReadOnly(new[] {new WorldGeometrySourceArgument(arg.Index,arg.NameHash,arg.DescriptorWord,Convert.ToHexString(arg.RawValue))}));
        return new(native, targets, late, queues, pop);
    }
}
