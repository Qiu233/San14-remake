using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public enum RenderTextureKind { Texture2D, Texture2DArray }

/// <summary>Explicit original texture declaration. Format/color-space codes remain source values;
/// no device format, clear value or attachment lifetime is inferred.</summary>
public sealed record RenderTextureDefinition(LoadedObjects Source, DokObject Object,
    RenderTextureKind Kind, int Width, int Height, int? ArrayDepth,
    float MipLodBias, byte LevelNum, byte MinMipLevel, byte PixelFormat, byte ColorSpace,
    byte MultiSample, byte AddressU, byte AddressV, byte MagFilter, byte MinFilter,
    byte MipFilter, byte AnisoSampleNum, byte LocationHint, byte OutputColorSpace,
    byte NeverInternalCopy, byte? NeedSliceTargets, byte? NeedAllTargets,
    uint? ArrayFlag, uint? NoiseParameterObjectNameHash);

public sealed record RenderTargetReference(int Slot, uint ObjectNameHash, uint SurfaceId,
    RenderTextureDefinition Texture);

public sealed record LoadedRenderGraphTargets(LoadedRenderGraph Graph, CompiledGraphNode Node,
    LoadedObjects TextureObjects, byte Flag, byte ColorRenderTargetNum,
    IReadOnlyList<uint> RawObjectNameHashes, IReadOnlyList<uint> RawSurfaceIds,
    IReadOnlyList<RenderTargetReference> ColorTargets,
    uint DepthObjectNameHash, uint DepthSurfaceId, RenderTextureDefinition? DepthTarget);

public static class RenderTextureResources
{
    public const uint Texture2DClassId = 0x72233E17;
    public const uint Texture2DArrayClassId = 0xD8272294;

    public static RenderTextureDefinition Read(LoadedObjects source, uint objectNameHash)
    {
        ArgumentNullException.ThrowIfNull(source);
        DokObject[] matches = source.Document.Objects.Where(o => o.ObjectId == objectNameHash).ToArray();
        if (objectNameHash == 0 || matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline)
            throw new InvalidDataException($"{source.Source.ResourceUri}: render texture {objectNameHash:X8} is absent, ambiguous or not explicitly inline.");
        DokObject obj = matches[0];
        RenderTextureKind kind = obj.ClassId switch
        {
            Texture2DClassId => RenderTextureKind.Texture2D,
            Texture2DArrayClassId => RenderTextureKind.Texture2DArray,
            _ => throw new InvalidDataException($"{source.Source.ResourceUri}: unsupported render texture class {obj.ClassId:X8}.")
        };
        bool array = kind == RenderTextureKind.Texture2DArray;
        string context = $"{source.Source.ResourceUri} object {objectNameHash:X8}";
        byte B(uint name) => Property(obj, name, 1, 1, context).RawValue[0];
        uint U(uint name) => BinaryPrimitives.ReadUInt32LittleEndian(Property(obj, name, 5, 4, context).RawValue);
        int I(uint name) => BinaryPrimitives.ReadInt32LittleEndian(Property(obj, name, 4, 4, context).RawValue);
        float F(uint name) => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(Property(obj, name, 8, 4, context).RawValue));
        return new(source, obj, kind, I(0xB809B0DA), I(0xAFA78DC7), array ? I(0xB80F13FD) : null,
            F(0x766BCC06), B(0x7744A442), B(0xC936F7DE), B(0x308EB4A3), B(0xC68F94C3),
            B(0x178FF7BD), B(0x5B1B8441), B(0xEF5FF342), B(0x62150DD5), B(0x62185A76),
            B(0x62194334), B(0x4A72D9C4), B(0x6619527C), B(0x8B4A54E4), B(0xDC0732A2),
            array ? B(0xF9275166) : null, array ? B(0x05169EB7) : null,
            array ? U(0x05D92ACC) : null, array ? null : U(0xDE803247));
    }

    private static DokProperty Property(DokObject obj, uint name, uint type, int width, string context)
    {
        DokProperty[] fields = obj.Properties.Where(p => p.NameHash == name).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != 1 || fields[0].RawValue.Length != width)
            throw new InvalidDataException($"{context}: render texture property {name:X8} is not one explicit typed scalar.");
        return fields[0];
    }
}

/// <summary>Explicit E2C0442A attachment declarations. This does not execute the graph,
/// choose a View, resolve SurfaceID to a native slice/mip, or invent clear values.</summary>
public static class RenderGraphTargetResources
{
    public const uint FactoryClassId = 0xE2C0442A;

    public static LoadedRenderGraphTargets Read(LoadedRenderGraph graph, int nodeIndex, LoadedObjects textureObjects)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(textureObjects);
        if (graph.Document.GraphKind != CompiledGraphKind.RenderGraph || (uint)nodeIndex >= graph.Document.Nodes.Count)
            throw new InvalidDataException("Attachment projection requires an identified RenderGraph and a valid explicit node.");
        CompiledGraphNode node = graph.Document.Nodes[nodeIndex];
        if (node.Call.FactoryClassId != FactoryClassId)
            throw new InvalidDataException($"{graph.Source.ResourceUri} node {nodeIndex}: source is not an E2C0442A attachment declaration.");
        string context = $"{graph.Source.ResourceUri} node {nodeIndex}";
        TaskGraphArgument Arg(uint name, uint packed, int bytes)
        {
            TaskGraphArgument? value = node.Call.Arguments.FirstOrDefault(a => a.NameHash == name && a.DescriptorWord == packed);
            if (value is null || value.RawValue.Length != bytes)
                throw new InvalidDataException($"{context}: expected explicit typed attachment parameter {name:X8}.");
            return value;
        }
        byte flag = Arg(0x05D92ACC, 0x01000001, 1).RawValue[0];
        byte count = Arg(0x6A68983C, 0x01000001, 1).RawValue[0];
        // E75060's loader keeps the raw declaration above but only resolves
        // colors when one of the source color-binding bits is present. Node32
        // declares count1 with Flag44 and null color names: its actual target
        // row is depth-only.
        if ((flag & 0x0A) == 0) count = 0;
        if (count > 8) throw new InvalidDataException($"{context}: color target count exceeds the eight original source slots.");
        uint[] names = Words(Arg(0xD208055E, 0x05000008, 32).RawValue);
        uint[] surfaces = Words(Arg(0xBFF6823E, 0x05000008, 32).RawValue);
        uint depthName = BinaryPrimitives.ReadUInt32LittleEndian(Arg(0xC13FB0BE, 0x05000001, 4).RawValue);
        uint depthSurface = BinaryPrimitives.ReadUInt32LittleEndian(Arg(0xAF2E2D9E, 0x05000001, 4).RawValue);
        var cache = new Dictionary<uint, RenderTextureDefinition>();
        RenderTextureDefinition Texture(uint name)
        {
            if (!cache.TryGetValue(name, out RenderTextureDefinition? definition))
            {
                definition = RenderTextureResources.Read(textureObjects, name);
                cache.Add(name, definition);
            }
            return definition;
        }
        var targets = new List<RenderTargetReference>(count);
        for (int slot = 0; slot < count; slot++) targets.Add(new(slot, names[slot], surfaces[slot], Texture(names[slot])));
        return new(graph, node, textureObjects, flag, count, Array.AsReadOnly(names), Array.AsReadOnly(surfaces),
            targets.AsReadOnly(), depthName, depthSurface, depthName == 0 ? null : Texture(depthName));
    }

    private static uint[] Words(ReadOnlySpan<byte> bytes)
    {
        var words = new uint[8];
        for (int i = 0; i < words.Length; i++) words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(i * 4, 4));
        return words;
    }
}
