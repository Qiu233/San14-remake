using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalOrdinaryRsLayerStage(OriginalWorldGeometryBindNode Targets,
    CompiledGraphNode IndirectNode, CompiledGraphNode SdfNode, OriginalWorldGeometryPopNode Pop,
    uint IndirectProcess, uint SdfPackedProcess30, uint DefaultViewObjectNameHash,
    uint SharedSdfObjectNameHash)
{
    public uint IndirectColorWriteMask => 8;
    public uint SdfColorWriteMask => 3;
    public bool ClearsRsOnBind => false;
}

/// <summary>Actual selected operation0 bank. The DWORD addition wraps just as
/// AB00A0 does; zero is tested after addition, rather than testing each count.</summary>
public readonly record struct OriginalOrdinarySdfOperation0Counts(uint Selector6C,
    uint SelectedCount48, uint SelectedCount58)
{
    public uint SelectedTotal => unchecked(SelectedCount48 + SelectedCount58);
}

/// <summary>Current manager8 and its two operation pointers. A null nullable
/// operation means the actual current pointer is null, not a guessed count0.</summary>
public sealed record OriginalOrdinarySdfProviderCounts(bool Manager8Present,
    uint? Operation1Count38, OriginalOrdinarySdfOperation0Counts? Operation0)
{
    public static OriginalOrdinarySdfProviderCounts AbsentManager => new(false, null, null);
}

public readonly record struct OriginalOrdinarySdfCurrentCall(bool CurrentTablePresent,
    int HostContextFlags3F0, uint HostViewIndex3F8, bool SelectedViewPresent,
    bool RequiredOcDepthNsResolved, uint CurrentView158C, bool CurrentTargetHelper959250);

/// <summary>Only the numeric and nullable fields explicitly initialized by
/// D220B0. This does not resolve its four shader bindings or complete a loader.</summary>
public readonly record struct OriginalOrdinarySdfFreshNumericState(bool Manager8Present,
    bool PreviousImage30Present, bool PreviousImage38Present, uint State40);

/// <summary>The actual graph2 prefix prepares the same SDF CPU before all
/// three RS3 consumers. Its manager/operation attachment and world population
/// must run even when the helper returns false after a new attachment.</summary>
public sealed record OriginalOrdinarySdfPreparationSource(CompiledGraphNode Node,
    uint SharedSdfObjectNameHash, uint OcTargetObjectNameHash, uint PackedProcess30);

/// <summary>One load of the three actual RS3 writer instances. Current selected
/// View, provider population, textures and state40 remain per-call owner inputs.
/// No returned recipe is a completed draw or an initialized texture.</summary>
public sealed class LoadedOriginalOrdinaryRsLayerSource
{
    internal LoadedOriginalOrdinaryRsLayerSource(string locale, string evidence,
        LoadedRenderGraph graph, LoadedObjects objects, OriginalOrdinarySdfPreparationSource preparation,
        IReadOnlyList<OriginalOrdinaryRsLayerStage> stages)
        => (Locale, SourceEvidence, Graph, Objects, PreparationNode2, Stages) = (locale, evidence, graph, objects, preparation, stages);
    public string Locale { get; }
    public string SourceEvidence { get; }
    public LoadedRenderGraph Graph { get; }
    public LoadedObjects Objects { get; }
    public OriginalOrdinarySdfPreparationSource PreparationNode2 { get; }
    public IReadOnlyList<OriginalOrdinaryRsLayerStage> Stages { get; }

    public OriginalOrdinaryRsLayerStage StageAtBind(int node) => Stages.Single(s => s.Targets.NodeIndex == node);
    public OriginalOrdinaryRsLayerStage StageAtSdf(int node) => Stages.Single(s => s.SdfNode.Index == node);

    public OriginalOrdinarySdfFreshNumericState ConstructFreshSdfNumericState() => new(false, false, false, 0);

    /// <summary>D61120 first reads operation1 count38. Only if that is absent
    /// or zero does it test operation0's current selected DWORD count sum.</summary>
    public bool HasCurrentSdfPopulation(OriginalOrdinarySdfProviderCounts current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!current.Manager8Present) return false;
        if (current.Operation1Count38 is uint count && count != 0) return true;
        return current.Operation0 is { } operation && operation.SelectedTotal != 0;
    }

    /// <summary>EA8D50 current table/index/View/input gates, followed by the
    /// shared provider and state40 gates. The caller commits NextState40 only
    /// after the selected actual fallback/final method returns.</summary>
    public OriginalDeferredSdfDecision PrepareSdf(int sdfNode,
        OriginalOrdinarySdfCurrentCall current, OriginalOrdinarySdfProviderCounts provider, uint currentState40)
    {
        var stage = StageAtSdf(sdfNode);
        uint index = unchecked((current.HostViewIndex3F8 & 0x7FFFFFFFu) + ((stage.SdfPackedProcess30 >> 24) & 0x7Fu));
        return OriginalDeferredExtraLayerPlan.Sdf(new(
            current.CurrentTablePresent && current.HostContextFlags3F0 >= 0,
            index, current.SelectedViewPresent, current.RequiredOcDepthNsResolved,
            stage.SdfPackedProcess30, HasCurrentSdfPopulation(provider), current.CurrentView158C,
            currentState40, current.CurrentTargetHelper959250));
    }

    public OriginalWorldGeometryCurrentPop PreparePop(int bindNode,
        int currentContextFlags3F0, bool currentTablePresent, OriginalWorldGeometryHostFields currentHost)
    {
        var pop = StageAtBind(bindNode).Pop;
        if (currentContextFlags3F0 < 0 || !currentTablePresent)
            return new(false, pop.EffectiveFlags, false, false);
        bool optional = pop.AllowsOptionalE86E40 && currentHost.Mode != 0;
        if (optional) throw new NotSupportedException("This RS3 pop requires the actual current E86E40 nonzero-mode helper.");
        return new(true, pop.EffectiveFlags, false, pop.CopiesDepthBeforePop);
    }

    /// <summary>Both paths must receive the genuinely retained A024EF37 CPU40;
    /// stages69/120/164 do not own independent once flags.</summary>
    public uint AfterCompletedActiveFinalMethod(uint currentState40) => currentState40 & 0xBFFFFFFFu;
}

public static class OriginalOrdinaryRsLayerSource
{
    public const uint SharedSdfObjectNameHash = 0xA024EF37;

    public static LoadedOriginalOrdinaryRsLayerSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string evidence = OriginalDeferredSamplerSources.LoadOrdinaryRsLayerNativeSource(repository.SourceDirectory, locale);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var objects = repository.LoadObjects("system", 0x93FB3280);
        Require(graph.Source, GameResourceId.Rdb("system", 0x024496EE).ToString(), OriginalTerrainGbufferTargetSources.GraphSha256);
        Require(objects.Source, OriginalDeferredAuxiliaryTargetSources.ActualObjectsResourceUri,
            OriginalDeferredAuxiliaryTargetSources.ActualObjectsSha256);
        graph = graph with { Document = RenderGraphReader.Read(graph.Source.Bytes) };
        objects = objects with { Document = DokReader.Read(objects.Source.Bytes) };
        var sdf = objects.Document.Objects.Single(o => o.ObjectId == SharedSdfObjectNameHash);
        if (sdf.ClassId != 0x71B26C2F || sdf.Kind != DokObjectKind.Inline)
            throw new InvalidDataException("The three ordinary SDF wrappers require their shared original A024EF37 declaration.");
        var prefix = graph.Document.Nodes[2];
        if (prefix.Call.FactoryClassId != 0xEBFE38E8 || prefix.Call.LookupKey != 0xE318257F ||
            B(prefix, 0x9075EDA0) != 0 || B(prefix, 0x69E3D5B7) != 0 ||
            U(prefix, 0x3D905A18) != SharedSdfObjectNameHash || U(prefix, 0x43E79658) != 0x9ACA259A ||
            U(prefix, 0xB7CCDD1F) != 0 || U(prefix, 0xF43A3947) != 0 ||
            U(prefix, 0xFA5A06A8) != 3 || B(prefix, 0xE98D44D5) != 0 ||
            !prefix.Call.ConnectedNodeIndices.SequenceEqual(new[] { 3u }))
            throw new InvalidDataException("The shared SDF source requires its actual graph2 preparation declaration.");
        var preparation = new OriginalOrdinarySdfPreparationSource(prefix, SharedSdfObjectNameHash, 0x9ACA259A, 0xC00);
        var rows = new[] {
            ReadStage(graph, objects, 67, 0x325ADB59, 0x000130CB, 0x421970A7, 0, 0),
            ReadStage(graph, objects, 118, 0xD176D713, 0xC14D4551, 0xD19512E1, 0x50, 1),
            ReadStage(graph, objects, 162, 0x6D97B179, 0x066CEEAB, 0x3CD54EC7, 0x60, 2) };
        return new(locale, evidence + ";canonical024496EE/93FB3280;67-70/118-121/162-165;shared-A024EF37-state40",
            graph, objects, preparation, Array.AsReadOnly(rows));
    }

    private static OriginalOrdinaryRsLayerStage ReadStage(LoadedRenderGraph graph, LoadedObjects objects,
        int bindIndex, uint bindKey, uint indirectKey, uint popKey, uint process, byte refraction)
    {
        var targets = OriginalRenderGraphTargetResources.Read(graph, bindIndex, objects);
        if (targets.Node.Call.LookupKey != bindKey || targets.Flag != 0x12 || targets.ColorRenderTargetNum != 1 ||
            targets.ColorTargets.Single().ObjectNameHash != 0x521C7DED || targets.ColorTargets[0].SurfaceId != 3 ||
            targets.DepthObjectNameHash != 0xE13DDB4F || targets.DepthSurfaceId != 0 || targets.DepthTarget is null)
            throw new InvalidDataException("Ordinary RS3 target does not match its own no-clear color/depth source row.");
        var rs = targets.ColorTargets[0].Texture;
        if (rs.Kind != OriginalRenderTextureKind.Texture2DArray || rs.ArrayDepth != 5 || rs.NeedSliceTargets != 1 ||
            rs.LevelNum != 1 || rs.MinMipLevel != 0 || rs.MultiSample != 0)
            throw new InvalidDataException("Ordinary RS3 requires the actual per-slice mip0 surface table.");
        var depth = targets.DepthTarget;
        var color = new OriginalWorldGeometryAttachment(0, 0x521C7DED, 3, 0, 3, rs, OriginalRenderTextureFormatMath.Resolve(rs));
        var depthAttachment = new OriginalWorldGeometryAttachment(-1, 0xE13DDB4F, 0, 0, 0, depth,
            OriginalRenderTextureFormatMath.Resolve(depth));
        var bind = new OriginalWorldGeometryBindNode(bindIndex, bindKey, 0xE2C0442A,
            Array.AsReadOnly(targets.Node.Call.ConnectedNodeIndices.ToArray()), 0x12, 0x40000001,
            Array.AsReadOnly(new[] { color }), depthAttachment, Arguments(targets.Node));
        var indirect = graph.Document.Nodes[bindIndex + 1];
        var sdf = graph.Document.Nodes[bindIndex + 2];
        var pop = graph.Document.Nodes[bindIndex + 3];
        if (indirect.Call.FactoryClassId != 0x93FFBE0F || indirect.Call.LookupKey != indirectKey ||
            U(indirect, 0xFA5A06A8) != 8 || U(indirect, 0x59BD6FC4) != 8 ||
            U(indirect, 0xB7CCDD1F) != 0xE13DDB4F || B(indirect, 0xCF60AA7C) != process ||
            U(indirect, 0x7046CA70) != 0x3E4CCFB5 || U(indirect, 0x3C2655F2) != 0xCDC8F8D5 ||
            U(indirect, 0x72DB7EBD) != 0xE1B6A687 || B(indirect, 0x9075EDA0) != 0)
            throw new InvalidDataException("Ordinary Indirect instance differs from its own source process/provider/View.");
        if (sdf.Call.FactoryClassId != 0xEBFE38E8 || B(sdf, 0x69E3D5B7) != 1 || B(sdf, 0xE98D44D5) != refraction ||
            U(sdf, 0xFA5A06A8) != 3 || U(sdf, 0x43E79658) != 0x521C7DED ||
            U(sdf, 0x3D905A18) != SharedSdfObjectNameHash || B(sdf, 0x9075EDA0) != 0)
            throw new InvalidDataException("Ordinary ViewSDF instance differs from its own source process/shared data.");
        if (pop.Call.FactoryClassId != 0x1F7C5CF8 || pop.Call.LookupKey != popKey || U(pop, 0x636CA9AE) != 1)
            throw new InvalidDataException("Ordinary RS3 pop flags differ from their no-depthcopy source.");
        foreach (var node in new[] { targets.Node, indirect, sdf, pop })
            if (!node.Call.ConnectedNodeIndices.SequenceEqual(new[] { (uint)node.Index + 1 }))
                throw new InvalidDataException("Ordinary RS3 source sequence is not the original four consecutive nodes.");
        var popRow = new OriginalWorldGeometryPopNode(pop.Index, popKey, pop.Call.FactoryClassId,
            Array.AsReadOnly(pop.Call.ConnectedNodeIndices.ToArray()), 1, Arguments(pop));
        uint packed = (3u << 10) | ((uint)refraction << 8) | 1u;
        return new(bind, indirect, sdf, popRow, process, packed, 0xE1B6A687, SharedSdfObjectNameHash);
    }

    private static uint U(CompiledGraphNode n, uint hash) => BinaryPrimitives.ReadUInt32LittleEndian(Arg(n, hash, 0x05000001, 4));
    private static byte B(CompiledGraphNode n, uint hash) => Arg(n, hash, 0x01000001, 1)[0];
    private static byte[] Arg(CompiledGraphNode n, uint hash, uint descriptor, int length)
    {
        var value = n.Call.Arguments.Single(a => a.NameHash == hash && a.DescriptorWord == descriptor).RawValue;
        if (value.Length != length) throw new InvalidDataException("Ordinary RS3 source field width differs.");
        return value;
    }
    private static IReadOnlyList<OriginalWorldGeometrySourceArgument> Arguments(CompiledGraphNode n) =>
        Array.AsReadOnly(n.Call.Arguments.Select(a => new OriginalWorldGeometrySourceArgument(a.Index, a.NameHash,
            a.DescriptorWord, Convert.ToHexString(a.RawValue))).ToArray());
    private static void Require(LoadedRdbAsset source, string uri, string sha)
    {
        if (source.ResourceUri != uri || source.DecodedSha256 != sha || source.OverrideSource is not null ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != sha)
            throw new InvalidDataException("Ordinary RS3 source load requires the accepted graph/object generation.");
    }
}
