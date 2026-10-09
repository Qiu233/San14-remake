using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record DeferredTextureInput(string Role, short EncodedParameter,
    TaskGraphArgument SourceArgument, G1ShaderResourceBinding ResourceMapping,
    DxbcRdefBinding PixelBinding, RenderTextureDefinition Texture);

public sealed record LoadedDeferredTexturePlan(LoadedRenderGraph Graph, CompiledGraphNode Node,
    ClusteredDeferredShaderReference ShaderReference, LoadedShaderContainer Shader,
    G1ShaderResourceBindings ResourceBindings, DxbcResourceDefinitions PixelDefinitions,
    LoadedObjects TextureObjects, byte ViewId, uint LightGroupId,
    IReadOnlyList<DeferredTextureInput> Inputs);

/// <summary>Finite F103 individual D1/AB/RS/NS/RR texture plan, using an explicit
/// source combination and exact PS RDEF declarations. Does not execute plugins,
/// resolve omitted defaults, select a View or claim current GPU bindings.</summary>
public static class DeferredTexturePlan
{
    public static LoadedDeferredTexturePlan Read(LoadedRenderGraph graph, int nodeIndex,
        LoadedObjects shaderObjects, LoadedObjects textureObjects, LoadedShaderContainer shader,
        int explicitCombinationIndex)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(shaderObjects);
        ArgumentNullException.ThrowIfNull(textureObjects);
        ArgumentNullException.ThrowIfNull(shader);
        if (graph.Document.GraphKind != CompiledGraphKind.RenderGraph || (uint)nodeIndex >= graph.Document.Nodes.Count)
            throw new InvalidDataException("Deferred texture plan requires an explicit RenderGraph node.");
        CompiledGraphNode node = graph.Document.Nodes[nodeIndex];
        if (node.Call.FactoryClassId != RenderGraphShaderReferences.ClusteredDeferredFactoryClassId)
            throw new InvalidDataException("Deferred texture plan source is not an F103 node.");
        ClusteredDeferredShaderReference reference = RenderGraphShaderReferences.Read(graph, shaderObjects)
            .References.Single(r => r.Node.Index == nodeIndex);
        if (reference.ShaderFileHash != shader.Source.FileId)
            throw new InvalidDataException("Deferred shader file does not match this node's explicit ShaderObject reference.");
        TaskGraphArgument Arg(uint hash, uint descriptor, int bytes)
        {
            TaskGraphArgument? arg = node.Call.Arguments.FirstOrDefault(a => a.NameHash == hash && a.DescriptorWord == descriptor);
            if (arg is null || arg.RawValue.Length != bytes)
                throw new InvalidDataException($"Deferred node {nodeIndex} requires explicit typed field {hash:X8}.");
            return arg;
        }
        uint Word(uint hash) => BinaryPrimitives.ReadUInt32LittleEndian(Arg(hash, 0x05000001, 4).RawValue);
        if (Word(0xCA398DA4) != 0)
            throw new InvalidDataException("Composite GBuffers owner is outside the verified individual-target plan.");
        if (Word(0x5102842C) == 0)
            throw new InvalidDataException("Deferred RR source is absent; the original RR/IR fallback needs a separate plan.");
        G1ShaderResourceBindings resources = shader.ReadG1ResourceBindings(explicitCombinationIndex);
        G2ShaderCombinationStage[] pixels = resources.Combination.Stages.Where(s => s.StageCode == 0 && s.Entry is not null).ToArray();
        if (pixels.Length != 1)
            throw new InvalidDataException("Deferred combination requires one explicit pixel program.");
        DxbcResourceDefinitions pixel = shader.ReadResourceDefinitions(pixels[0].Entry!.ProgramIndex);
        (string Role, short Encoded, uint Hash)[] fields =
        [ ("D1", 8, 0xB7CCDD1F), ("AB", 128, 0x33E0132B), ("RS", 129, 0x454BF54B),
          ("NS", 130, 0xF43A3947), ("RR", 131, 0x5102842C) ];
        var inputs = new List<DeferredTextureInput>(fields.Length);
        var textures = new Dictionary<uint, RenderTextureDefinition>();
        foreach (var field in fields)
        {
            TaskGraphArgument argument = Arg(field.Hash, 0x05000001, 4);
            uint key = BinaryPrimitives.ReadUInt32LittleEndian(argument.RawValue);
            if (!textures.TryGetValue(key, out RenderTextureDefinition? texture))
            {
                texture = RenderTextureResources.Read(textureObjects, key);
                textures.Add(key, texture);
            }
            G1ShaderResourceBinding[] mappings = resources.Entries.Where(e => e.PrimaryEncoded == field.Encoded).ToArray();
            if (mappings.Length != 1 || mappings[0].SlotIndex < 0)
                throw new InvalidDataException($"Deferred {field.Role} resource has no unique explicit source mapping.");
            G1ShaderResourceBinding mapping = mappings[0];
            DxbcRdefBinding[] declarations = pixel.Bindings.Where(b => b.Name.Text == mapping.Name && b.InputType == 2).ToArray();
            uint dimension = texture.Kind == RenderTextureKind.Texture2D ? 4u : 5u;
            if (declarations.Length != 1 || declarations[0].BindPoint != (uint)mapping.SlotIndex ||
                declarations[0].BindCount != 1 || declarations[0].Dimension != dimension)
                throw new InvalidDataException($"Deferred {field.Role} texture kind/name/slot differs from its exact pixel declaration.");
            inputs.Add(new(field.Role, field.Encoded, argument, mapping, declarations[0], texture));
        }
        return new(graph, node, reference, shader, resources, pixel, textureObjects,
            Arg(0x9075EDA0, 0x01000001, 1).RawValue[0], Word(0x59BD6FC4), inputs.AsReadOnly());
    }
}
