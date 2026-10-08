using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalClusteredDeferredShaderReference(CompiledGraphNode Node,
    TaskGraphArgument ShaderObjectArgument, DokObject ShaderObject,
    DokProperty G1SReference, uint ShaderFileHash);

public sealed record LoadedRenderGraphShaderReferences(LoadedRenderGraph Graph,
    LoadedObjects ShaderObjects, IReadOnlyList<OriginalClusteredDeferredShaderReference> References);

/// <summary>Follow explicit compiled clustered-deferred keys into a supplied original ShaderObject document.
/// Source documents remain separate; this does not select or execute a render branch.</summary>
public static class OriginalRenderGraphShaderReferences
{
    public const uint ClusteredDeferredFactoryClassId = 0xF103DB00;
    public const uint ShaderObjectNameHashProperty = 0x702A1052;
    public const uint ShaderObjectClassId = 0x8687085E;
    public const uint G1SFileResourceHashProperty = 0x193025DF;

    public static LoadedRenderGraphShaderReferences Read(LoadedRenderGraph graph, LoadedObjects shaderObjects)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(shaderObjects);
        if (graph.Document.GraphKind != OriginalCompiledGraphKind.RenderGraph)
            throw new InvalidDataException("Shader references require an explicitly identified original RenderGraph.");
        var references = new List<OriginalClusteredDeferredShaderReference>();
        foreach (CompiledGraphNode node in graph.Document.Nodes.Where(n => n.Call.FactoryClassId == ClusteredDeferredFactoryClassId))
        {
            string context = $"{graph.Source.ResourceUri} node {node.Index}";
            TaskGraphArgument? argument = node.Call.Arguments.FirstOrDefault(a =>
                a.DescriptorWord == 0x05000001 && a.NameHash == ShaderObjectNameHashProperty);
            // This projection needs an explicit source reference. A native
            // omitted-field default or another document is not inferred here.
            if (argument is null || argument.RawValue.Length != 4)
                throw new InvalidDataException($"{context}: expected an explicit typed DeferredShadingPassShaderObjectHash scalar.");
            uint key = BinaryPrimitives.ReadUInt32LittleEndian(argument.RawValue);
            DokObject[] matches = shaderObjects.Document.Objects.Where(o => o.ObjectId == key).ToArray();
            if (key == 0 || matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].ClassId != ShaderObjectClassId)
                throw new InvalidDataException($"{context}: ShaderObject {key:X8} is absent, ambiguous or unsupported in {shaderObjects.Source.ResourceUri}.");
            DokObject provider = matches[0];
            DokProperty[] fields = provider.Properties.Where(p => p.NameHash == G1SFileResourceHashProperty).ToArray();
            if (fields.Length != 1 || fields[0].TypeCode != 5 || fields[0].ElementCount != 1 || fields[0].RawValue.Length != 4)
                throw new InvalidDataException($"{context}: ShaderObject {key:X8} requires one explicit G1SFileResourceHash scalar.");
            uint file = BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
            if (file == 0)
                throw new InvalidDataException($"{context}: ShaderObject {key:X8} has a zero G1S source reference.");
            references.Add(new(node, argument, provider, fields[0], file));
        }
        return new(graph, shaderObjects, references.AsReadOnly());
    }
}
