using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record TerritoryTextureReference(string Role, uint Register,
    TaskGraphArgument Argument, RenderTextureDefinition Texture);
public sealed record LoadedTerritoryResources(LoadedRenderGraph Graph, CompiledGraphNode Node,
    LoadedObjects ShaderObjects, DokObject ShaderObject, LoadedObjects ScreenMapObjects,
    DokObject ScreenMapObject, LoadedShaderContainer Shader, byte ViewId, uint LayerViewMask,
    IReadOnlyList<TerritoryTextureReference> Gbuffers);

/// <summary>The explicit original GBufferEdit PB2 ScreenMap source. Object IDs
/// resolve within their supplied documents; no runtime packet is synthesized.</summary>
public static class TerritoryResources
{
    public const uint PassClass = 0xA82D6D40;
    public const uint ShaderObjectClass = 0xC78FE503;
    public const uint ScreenMapClass = 0x12EA4048;
    public const uint ShaderFileId = 0x3DC29F92;
    public const string ShaderSha256 = "8CCB65D9A08EBD9D84608B858D5C0370E7BDDBA2974521CEED514161D9BB3DC8";
    public const string VertexSha256 = "F333B6EB2FB3E1ED504F374661F94F98EBB8A17E062D59F976BBD5CACE83F878";
    public const string PixelSha256 = "4E4E07D8CFA5EA4FF2882D908FDAD0CBD4642F8C5D108D348DF202281B742874";

    public static LoadedTerritoryResources Read(GameResourceRepository repository,
        LoadedRenderGraph graph, int nodeIndex, LoadedObjects shaderObjects,
        LoadedObjects screenMapObjects, LoadedObjects textureObjects)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(shaderObjects); ArgumentNullException.ThrowIfNull(screenMapObjects);
        ArgumentNullException.ThrowIfNull(textureObjects);
        if (graph.Document.GraphKind != CompiledGraphKind.RenderGraph ||
            (uint)nodeIndex >= graph.Document.Nodes.Count)
            throw new InvalidDataException("Territory resources require an explicit RenderGraph node.");
        var node = graph.Document.Nodes[nodeIndex];
        if (node.Call.FactoryClassId != PassClass)
            throw new InvalidDataException("Territory source is not a GBufferEditPB2 ScreenMap pass.");
        TaskGraphArgument Field(uint hash, uint descriptor, int length)
        {
            var values = node.Call.Arguments.Where(a => a.NameHash == hash && a.DescriptorWord == descriptor).ToArray();
            return values.Length == 1 && values[0].RawValue.Length == length ? values[0] :
                throw new InvalidDataException($"Territory node {nodeIndex} lacks explicit field {hash:X8}.");
        }
        uint Word(uint hash) => BinaryPrimitives.ReadUInt32LittleEndian(Field(hash, 0x05000001, 4).RawValue);
        DokObject Object(LoadedObjects source, uint id, uint type)
        {
            var values = source.Document.Objects.Where(o => o.ObjectId == id).ToArray();
            return values.Length == 1 && values[0].Kind == DokObjectKind.Inline && values[0].ClassId == type ? values[0] :
                throw new InvalidDataException($"Territory object {id:X8} has no exact class in {source.Source.ResourceUri}.");
        }
        var provider = Object(shaderObjects, Word(0x51B1BFC2), ShaderObjectClass);
        var screen = Object(screenMapObjects, Word(0xAF260DD8), ScreenMapClass);
        var references = provider.Properties.Where(p => p.NameHash == 0x193025DF).ToArray();
        if (references.Length != 1 || references[0].TypeCode != 5 || references[0].ElementCount != 1 ||
            references[0].RawValue.Length != 4 || BinaryPrimitives.ReadUInt32LittleEndian(references[0].RawValue) != ShaderFileId)
            throw new InvalidDataException("Territory ShaderObject does not reference the verified PB2SM G1S.");
        var shader = repository.LoadShaderContainer("system", ShaderFileId);
        ValidateShader(shader);
        if (Word(0xCA398DA4) != 0)
            throw new InvalidDataException("Territory composite GBuffers source requires a separate adapter.");
        (string Role, uint Register, uint Hash)[] fields =
            [("D1", 1, 0xB7CCDD1F), ("AB", 2, 0x33E0132B), ("RS", 3, 0x454BF54B),
             ("NS", 4, 0xF43A3947), ("RR", 5, 0x0419AB84)];
        var textures = fields.Select(f => new TerritoryTextureReference(f.Role, f.Register,
            Field(f.Hash, 0x05000001, 4), RenderTextureResources.Read(textureObjects, Word(f.Hash)))).ToArray();
        return new(graph, node, shaderObjects, provider, screenMapObjects, screen, shader,
            Field(0x9075EDA0, 0x01000001, 1).RawValue[0], Word(0xD3299DF0), Array.AsReadOnly(textures));
    }

    public static void ValidateShader(LoadedShaderContainer shader)
    {
        ArgumentNullException.ThrowIfNull(shader);
        if (shader.Source.FileId != ShaderFileId || shader.Source.DecodedSha256 != ShaderSha256 ||
            Convert.ToHexString(SHA256.HashData(shader.Source.Bytes)) != ShaderSha256 ||
            shader.Container.Programs.Count != 2 ||
            Convert.ToHexString(SHA256.HashData(shader.Container.GetProgramBytes(shader.Source.Bytes, 0))) != VertexSha256 ||
            Convert.ToHexString(SHA256.HashData(shader.Container.GetProgramBytes(shader.Source.Bytes, 1))) != PixelSha256)
            throw new InvalidDataException("Territory source is outside the verified PB2SM vertex/pixel contract.");
    }
}
