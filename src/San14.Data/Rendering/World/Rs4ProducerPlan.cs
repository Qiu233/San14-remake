using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record Rs4QueueSource(LoadedRenderGraphTargets Attachment,
    CompiledGraphNode Queue, TaskGraphArgument StateOffsetArgument, int StateIndex,
    uint RequiredFrameFlag);
public sealed record LoadedRs4ProducerPlan(LoadedRenderGraph Graph,
    LoadedObjects ShaderObjects, LoadedObjects TextureObjects,
    IReadOnlyList<Rs4QueueSource> Queues);

public enum Rs4StateBoundary { StateArrayTooShort, NullInterface, RequiresCombination }
public enum Rs4PixelBoundary { NoPixelStage, NoTarget6Output, RequiresCoverageBlendAndWriteMask }

/// <summary>A declaration can describe the base owner without proving the later
/// material/swap result. A final owner must be supplied explicitly before selecting programs.</summary>
public sealed record Rs4StateSource(LoadedRs4ProducerPlan Plan,
    Rs4QueueSource Queue, NamedModelShader Owner,
    ResolvedModelDrawShaderSource? BaseDraw, bool FinalPrimitiveOwnerSupplied,
    int StateCount, ModelShaderInterfaceSource? Interface,
    Rs4StateBoundary Boundary)
{
    public bool SkipsFinalPrimitive => FinalPrimitiveOwnerSupplied &&
        Boundary is Rs4StateBoundary.StateArrayTooShort or Rs4StateBoundary.NullInterface;
}

/// <summary>Selected original bytecode and its output declaration. Even a target6
/// signature requires the original coverage, blend and write-mask state; it is
/// neither a pixel evaluator nor proof of texture initialization.</summary>
public sealed record Rs4PixelSource(Rs4StateSource State,
    LoadedShaderContainer Shader, IReadOnlyList<uint> ExplicitDimensions, uint ExplicitPass,
    G2ShaderCombination Combination, G2ShaderCombinationStage PixelStage,
    G2ShaderProgram? PixelProgram, string? PixelProgramSha256,
    DxbcProgramSignatures? Signatures, DxbcSignatureParameter? Target6Output,
    Rs4PixelBoundary Boundary);

/// <summary>Finite RS surface4 source pipeline. Follows the model's exact type16
/// owner dependency, then seven explicit dimensions and a pass to a physical PS.
/// Never treats state22/23 as a program index or a skipped draw as a zero clear.</summary>
public static class Rs4ProducerPlan
{
    public const uint RsObjectId = 0x521C7DED;
    public const uint LateQueueFrameFlag = 0x40000000;
    private const string GraphSha = "B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";

    public static LoadedRs4ProducerPlan Read(LoadedRenderGraph graph,
        LoadedObjects shaderObjects, LoadedObjects textureObjects)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(shaderObjects);
        ArgumentNullException.ThrowIfNull(textureObjects);
        if (graph.Source.ResourceUri != "san14://rdb/system/024496EE" ||
            graph.Source.DecodedSha256 != GraphSha ||
            Convert.ToHexString(SHA256.HashData(graph.Source.Bytes)) != GraphSha ||
            graph.Document.GraphKind != CompiledGraphKind.RenderGraph)
            throw Error("requires the exact original RenderGraph source contract");
        // Raw selected source bytes are authoritative; cached parsed records cannot
        // change a declaration while borrowing a different source's hash.
        graph = new(CompiledGraphReader.Read(graph.Source.Bytes, CompiledGraphKind.RenderGraph), graph.Source);
        shaderObjects = ReadObjects(shaderObjects);
        textureObjects = ReadObjects(textureObjects);
        var queues = new List<Rs4QueueSource>();
        foreach ((int attachmentIndex, int queueIndex, byte stateIndex, uint frameFlag) in new[]
        {
            (43, 44, (byte)22, 0u), (43, 45, (byte)22, 0u),
            (150, 151, (byte)23, LateQueueFrameFlag), (150, 152, (byte)23, LateQueueFrameFlag)
        })
        {
            LoadedRenderGraphTargets attachment = RenderGraphTargetResources.Read(graph, attachmentIndex, textureObjects);
            RenderTargetReference[] targets = attachment.ColorTargets.Where(t => t.Slot == 6).ToArray();
            if (targets.Length != 1 || targets[0].ObjectNameHash != RsObjectId || targets[0].SurfaceId != 4 ||
                targets[0].Texture.Kind != RenderTextureKind.Texture2DArray || targets[0].Texture.ArrayDepth != 5 ||
                attachment.Flag != (attachmentIndex == 43 ? 0x46 : 0x06))
                throw Error($"attachment {attachmentIndex} does not bind RS surface4 to MRT6 with its original flags");
            CompiledGraphNode queue = Node(graph, queueIndex);
            TaskGraphArgument offset = ByteArgument(queue, ModelShaderResources.StateOffsetPropertyHash);
            if (queue.Call.FactoryClassId != ModelShaderResources.QueueFactoryClass || offset.RawValue[0] != stateIndex)
                throw Error($"queue {queueIndex} differs from its exact FC64 state-array offset");
            queues.Add(new(attachment, queue, offset, stateIndex, frameFlag));
        }
        CompiledGraphNode checkSkip = Node(graph, 149);
        if (checkSkip.Call.FactoryClassId != 0x5C8B904B || ByteArgument(checkSkip, 0xF2044AFF).RawValue[0] != 5)
            throw Error("late queue CheckSkipType5 source is missing");
        return new(graph, shaderObjects, textureObjects, queues.AsReadOnly());
    }

    /// <summary>Concrete installed model/draw base declaration. Later material
    /// record publication and current prepared collection membership remain unresolved.</summary>
    public static Rs4StateSource ReadDrawBase(LoadedRs4ProducerPlan plan,
        int queueIndex, LoadedModelShaderBindings model, int drawIndex)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ResolvedModelDrawShaderSource draw = ModelShaderResources.ResolveDrawBaseSource(model, drawIndex, plan.ShaderObjects);
        return SelectOwner(plan, queueIndex, draw.Binding.Shader, draw, false);
    }

    /// <summary>The caller supplies the proven final primitive owner after
    /// material/swap publication. This function validates the source declaration;
    /// it does not observe the current native queue or select a material branch.</summary>
    public static Rs4StateSource SelectFinalOwner(LoadedRs4ProducerPlan plan,
        int queueIndex, NamedModelShader finalOwner)
        => SelectOwner(plan, queueIndex, finalOwner, null, true);

    private static Rs4StateSource SelectOwner(LoadedRs4ProducerPlan plan,
        int queueIndex, NamedModelShader owner, ResolvedModelDrawShaderSource? draw, bool final)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(owner);
        Rs4QueueSource[] queues = plan.Queues.Where(q => q.Queue.Index == queueIndex).ToArray();
        if (queues.Length != 1) throw Error($"node {queueIndex} is not one of the four validated RS4 queues");
        Rs4QueueSource queue = queues[0];
        int expectedState = queueIndex switch { 44 or 45 => 22, 151 or 152 => 23, _ => -1 };
        int expectedAttachment = expectedState == 22 ? 43 : 150;
        uint expectedFrameFlag = expectedState == 23 ? LateQueueFrameFlag : 0;
        if (expectedState < 0 || queue.StateIndex != expectedState || queue.Attachment.Node.Index != expectedAttachment ||
            queue.RequiredFrameFlag != expectedFrameFlag || queue.Queue.Call.FactoryClassId != ModelShaderResources.QueueFactoryClass ||
            queue.StateOffsetArgument.NameHash != ModelShaderResources.StateOffsetPropertyHash ||
            queue.StateOffsetArgument.DescriptorWord != 0x01000001 || queue.StateOffsetArgument.RawValue.Length != 1 ||
            queue.StateOffsetArgument.RawValue[0] != expectedState)
            throw Error($"node {queueIndex} differs from its validated RS4 queue source");
        // Re-read the declaration so a caller cannot change the cached file/dependency
        // fields while retaining a genuine object identity from this source document.
        NamedModelShader canonical = ModelShaderResources.ReadDeclaredShader(plan.ShaderObjects, owner.Object.ObjectId);
        if (owner.Objects.Source.ResourceUri != plan.ShaderObjects.Source.ResourceUri ||
            owner.Objects.Source.DecodedSha256 != plan.ShaderObjects.Source.DecodedSha256 ||
            owner.Object.ClassId != canonical.Object.ClassId || owner.ShaderFileHash != canonical.ShaderFileHash ||
            !owner.DependencyObjectHashes.SequenceEqual(canonical.DependencyObjectHashes) || !canonical.HasVerifiedType16Creator)
            throw Error($"owner {owner.Object.ObjectId:X8} differs from its exact verified type16 source");
        int count = checked(canonical.DependencyObjectHashes.Count + 1);
        if (count > 0xFFFFFF) throw Error("type16 interface count exceeds the original typeWord count field");
        if (count <= queue.StateIndex)
            return new(plan, queue, canonical, draw, final, count, null, Rs4StateBoundary.StateArrayTooShort);
        ModelShaderInterfaceSource selected = ModelShaderResources.ResolveType16Interface(canonical, queue.StateIndex);
        return new(plan, queue, canonical, draw, final, count, selected, selected.IsNull ?
            Rs4StateBoundary.NullInterface : Rs4StateBoundary.RequiresCombination);
    }

    public static LoadedShaderContainer LoadSelectedG1S(GameResourceRepository repository, Rs4StateSource state)
    {
        ArgumentNullException.ThrowIfNull(repository);
        NamedModelShader provider = RequireProvider(state);
        return ModelShaderResources.LoadDeclaredG1S(repository, provider);
    }

    public static Rs4PixelSource SelectPixelProgram(Rs4StateSource state,
        LoadedShaderContainer shader, ReadOnlySpan<uint> explicitDimensions, uint explicitPass)
    {
        ArgumentNullException.ThrowIfNull(shader);
        NamedModelShader provider = RequireProvider(state);
        if (!state.FinalPrimitiveOwnerSupplied)
            throw Error("base draw declaration cannot bypass the unresolved final material/swap primitive owner");
        if (shader.Source.ResourceUri != GameResourceId.Rdb("system", provider.ShaderFileHash).ToString() ||
            shader.Source.FileId != provider.ShaderFileHash ||
            Convert.ToHexString(SHA256.HashData(shader.Source.Bytes)) != shader.Source.DecodedSha256)
            throw Error("selected G1S differs from the selected dependency renderer's own source reference");
        shader = new(G2ShaderContainerReader.Read(shader.Source.Bytes), shader.Source);
        G1ShaderBindingTables binding = shader.ReadG1Bindings();
        G2ShaderCombination combination = binding.ResolveCombination(explicitDimensions, explicitPass);
        G2ShaderCombinationStage[] stages = combination.Stages.Where(s => s.StageCode == 0).ToArray();
        if (stages.Length != 1) throw Error("combination has no unique original pixel-stage field");
        G2ShaderCombinationStage stage = stages[0];
        G2ShaderProgram? program = stage.Entry is null ? null : shader.Container.Programs[stage.Entry.ProgramIndex];
        DxbcProgramSignatures? signatures = program is null ? null : shader.ReadSignatures(program.Index);
        DxbcSignatureParameter[] targets = signatures?.Parts.Where(p => p.Kind == DxbcSignatureKind.Output)
            .SelectMany(p => p.Parameters).Where(p => p.SemanticName.Text.Equals("SV_Target", StringComparison.OrdinalIgnoreCase) &&
                p.SemanticIndex == 6 && p.Register == 6).ToArray() ?? [];
        if (targets.Length > 1) throw Error("pixel shader has ambiguous SV_Target6 output signatures");
        DxbcSignatureParameter? target = targets.SingleOrDefault();
        return new(state, shader, Array.AsReadOnly(explicitDimensions.ToArray()), explicitPass, combination, stage, program,
            program is null ? null : Convert.ToHexString(SHA256.HashData(shader.Container.GetProgramBytes(shader.Source.Bytes, program.Index))),
            signatures, target, program is null ? Rs4PixelBoundary.NoPixelStage : target is null ?
                Rs4PixelBoundary.NoTarget6Output : Rs4PixelBoundary.RequiresCoverageBlendAndWriteMask);
    }

    private static NamedModelShader RequireProvider(Rs4StateSource state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Rs4StateSource canonical = SelectOwner(state.Plan, state.Queue.Queue.Index, state.Owner,
            state.BaseDraw, state.FinalPrimitiveOwnerSupplied);
        if (state.StateCount != canonical.StateCount || state.Boundary != canonical.Boundary ||
            state.Interface?.Provider?.Object.ObjectId != canonical.Interface?.Provider?.Object.ObjectId)
            throw Error("selected state no longer matches the exact owner dependency slot");
        return canonical.Interface?.Provider ?? throw Error("null/short type16 state has no renderer source; substituting a shader or zero clear is unsupported");
    }

    private static CompiledGraphNode Node(LoadedRenderGraph graph, int index)
    {
        CompiledGraphNode[] nodes = graph.Document.Nodes.Where(n => n.Index == index).ToArray();
        return nodes.Length == 1 ? nodes[0] : throw Error($"node {index} is absent or ambiguous");
    }
    private static LoadedObjects ReadObjects(LoadedObjects objects)
    {
        if (Convert.ToHexString(SHA256.HashData(objects.Source.Bytes)) != objects.Source.DecodedSha256)
            throw Error($"source hash differs from selected object bytes: {objects.Source.ResourceUri}");
        return new(DokReader.Read(objects.Source.Bytes), objects.Source);
    }
    private static TaskGraphArgument ByteArgument(CompiledGraphNode node, uint hash)
    {
        TaskGraphArgument[] values = node.Call.Arguments.Where(a => a.NameHash == hash).ToArray();
        return values.Length == 1 && values[0].DescriptorWord == 0x01000001 && values[0].RawValue.Length == 1 ?
            values[0] : throw Error($"node {node.Index} field {hash:X8} requires one explicit typed byte");
    }
    private static InvalidDataException Error(string message) => new($"Original RS4 producer: {message}.");
}
