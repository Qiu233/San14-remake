using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record SkyCloudPatternTextureBinding(string Name, uint Slot, uint Dimension);
internal sealed record SkyCloudPatternField(int Semantic, int Offset, int Bytes);
internal sealed record SkyCloudPatternStage(uint Slot, int Bytes, IReadOnlyList<SkyCloudPatternField> Fields);

/// <summary>The selected Pattern shaders' used named fields and texture roles.
/// Import is cached; packing and compatibility decoding read no resource definitions.</summary>
public sealed class SkyCloudPatternMaterialLayout
{
    private static readonly ConditionalWeakTable<DxbcResourceDefinitions,
        ConditionalWeakTable<DxbcResourceDefinitions, SkyCloudPatternMaterialLayout>> imported = new();
    private readonly SkyCloudPatternStage vertex, pixel;
    private readonly IReadOnlyDictionary<uint, uint> samplerTextures;
    private readonly bool usesIntegration;
    private static readonly (string Name, int Bytes, int Columns, int Elements)[] fields =
    [
        ("ksgClipPlane",16,4,1), ("g_mtP2W",64,4,0), ("g_vCameraDirVectors",64,4,4), ("g_vProjFarNear",8,2,0),
        ("vEye",16,4,0), ("g_vBaseTexOffset",12,3,0), ("g_vBaseTexScale",12,3,0),
        ("g_vDetailTexOffset",12,3,0), ("g_vDetailTexScale",12,3,0),
        ("g_vDistortTexOffset",8,2,0), ("g_vDistortTexScale",8,2,0),
        ("g_vRaySampleNum",16,4,0), ("g_vEarthRadius",16,4,0),
        ("g_vCloudParams0",16,4,0), ("g_vCloudParams1",16,4,0), ("g_vDistortScale",16,4,0), ("g_vErodeScale",8,2,0),
    ];

    private SkyCloudPatternMaterialLayout(DxbcResourceDefinitions vertex, DxbcResourceDefinitions pixel)
    {
        this.vertex = ImportStage(vertex, true);
        this.pixel = ImportStage(pixel, false);
        usesIntegration = this.pixel.Fields.Any(field => field.Semantic == 11) &&
            this.pixel.Fields.Any(field => field.Semantic == 14);
        if (vertex.Bindings.Any(binding => binding.InputType != 0))
            throw new NotSupportedException("Pattern vertex resources require another producer.");
        var textures = pixel.Bindings.Where(binding => binding.InputType == 2).ToArray();
        var samplers = pixel.Bindings.Where(binding => binding.InputType == 3).ToArray();
        if (pixel.Bindings.Any(binding => binding.InputType is not (0 or 2 or 3)) ||
            textures.Select(binding => binding.BindPoint).Distinct().Count() != textures.Length ||
            samplers.Select(binding => binding.BindPoint).Distinct().Count() != samplers.Length)
            throw new NotSupportedException("Pattern has an unsupported resource binding.");
        var inputs = new List<SkyCloudPatternTextureBinding>();
        var samplerTextures = new Dictionary<uint, uint>();
        foreach (var texture in textures)
        {
            uint dimension = texture.Name.Text switch
            {
                "sBase" or "sDetail" => 8,
                "sDistort" => 4,
                _ => throw new NotSupportedException($"Pattern has no source for {texture.Name.Text}."),
            };
            var sampler = samplers.SingleOrDefault(value => value.Name.Text == "__smp" + texture.Name.Text);
            if (texture.BindCount != 1 || texture.ReturnType != 5 || texture.Dimension != dimension || texture.BindPoint >= 128 ||
                sampler is null || sampler.BindCount != 1 || sampler.Dimension != 0 || sampler.BindPoint >= 16)
                throw new NotSupportedException("Pattern texture or sampler interface is unsupported.");
            inputs.Add(new(texture.Name.Text, texture.BindPoint, dimension));
            samplerTextures.Add(sampler.BindPoint, texture.BindPoint);
        }
        if (samplerTextures.Count != samplers.Length)
            throw new NotSupportedException("Pattern sampler has no actual texture source.");
        TextureBindings = Array.AsReadOnly(inputs.ToArray());
        this.samplerTextures = samplerTextures;
    }
    public uint VertexSlot => vertex.Slot;
    public uint PixelSlot => pixel.Slot;
    public int VertexByteLength => vertex.Bytes;
    public int PixelByteLength => pixel.Bytes;
    public IReadOnlyList<SkyCloudPatternTextureBinding> TextureBindings { get; }
    public uint ResolveSamplerTextureSlot(uint samplerSlot) => samplerTextures.TryGetValue(samplerSlot, out uint slot)
        ? slot : throw new NotSupportedException("Pattern sampler has no imported source role.");

    /// <summary>Export the already imported named layout for the Native Pattern
    /// producer. Semantic IDs are this format's field order, not a memory arena.</summary>
    public byte[] EncodeNativeLayout()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(0x314C4D50u); // PML1
        writer.Write(vertex.Slot); writer.Write((uint)vertex.Bytes); writer.Write((uint)vertex.Fields.Count);
        writer.Write(pixel.Slot); writer.Write((uint)pixel.Bytes); writer.Write((uint)pixel.Fields.Count);
        writer.Write((uint)TextureBindings.Count); writer.Write((uint)samplerTextures.Count);
        writer.Write(usesIntegration ? 1u : 0u);
        foreach (var stage in new[] { vertex, pixel })
            foreach (var field in stage.Fields)
            {
                writer.Write((uint)field.Semantic); writer.Write((uint)field.Offset); writer.Write((uint)field.Bytes);
            }
        foreach (var texture in TextureBindings)
        {
            uint role = texture.Name switch
            {
                "sBase" => 0u, "sDetail" => 1u, "sDistort" => 2u,
                _ => throw new InvalidOperationException("Pattern imported an unknown texture role.")
            };
            writer.Write(role); writer.Write(texture.Slot); writer.Write(texture.Dimension);
        }
        foreach (var sampler in samplerTextures.OrderBy(pair => pair.Key))
        { writer.Write(sampler.Key); writer.Write(sampler.Value); }
        return stream.ToArray();
    }

    public static SkyCloudPatternMaterialLayout Import(DxbcResourceDefinitions vertex, DxbcResourceDefinitions pixel)
        => imported.GetValue(vertex, static _ => new()).GetValue(pixel, value => new(vertex, value));

    private static SkyCloudPatternStage ImportStage(DxbcResourceDefinitions definitions, bool isVertex)
    {
        if (definitions.RawShaderType != (isVertex ? 0xFFFE : 0xFFFF) || definitions.Buffers.Count != 1)
            throw new NotSupportedException("Pattern requires one actual constant buffer per stage.");
        var buffer = definitions.Buffers[0];
        if (buffer.BufferType != 0 || buffer.ByteSize <= 0 || buffer.ByteSize > 65536 || buffer.ByteSize % 16 != 0)
            throw new NotSupportedException("Pattern constant buffer storage is unsupported.");
        var writes = new List<SkyCloudPatternField>();
        foreach (var variable in buffer.Variables)
        {
            if ((variable.Flags & 2) == 0) continue;
            int id = Array.FindIndex(fields, field => field.Name == variable.Name.Text);
            if (id < 0 || (id < 4) != isVertex)
                throw new NotSupportedException($"Pattern has no material producer for {variable.Name.Text}.");
            var field = fields[id];
            var type = definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            bool matrix = id == 1;
            if (variable.StartOffset < 0 || variable.StartOffset % 4 != 0 || variable.ByteSize != field.Bytes ||
                (long)variable.StartOffset + field.Bytes > buffer.ByteSize || type.BaseType != 3 ||
                type.Class != (matrix ? 2 : 1) || type.Rows != (matrix ? 4 : 1) || type.Columns != field.Columns ||
                type.Elements != field.Elements || type.Members.Count != 0)
                throw new NotSupportedException($"Pattern field {field.Name} has an unsupported typed shape.");
            writes.Add(new(id, variable.StartOffset, field.Bytes));
        }
        var ordered = writes.OrderBy(field => field.Offset).ToArray();
        for (int index = 1; index < ordered.Length; index++)
            if (ordered[index - 1].Offset + ordered[index - 1].Bytes > ordered[index].Offset)
                throw new NotSupportedException("Pattern used material fields overlap.");
        var bindings = definitions.Bindings.Where(binding => binding.InputType == 0).ToArray();
        if (bindings.Length != 1 || bindings[0].Name.Text != buffer.Name.Text || bindings[0].BindCount != 1 ||
            bindings[0].Dimension != 0 || bindings[0].BindPoint >= 14)
            throw new NotSupportedException("Pattern constant buffer binding is unsupported.");
        return new(bindings[0].BindPoint, buffer.ByteSize, Array.AsReadOnly(ordered));
    }

    public SkyCloudConstantPacket Pack(SkyCloudPatternMaterial material, uint maximumRaySamples)
    {
        ArgumentNullException.ThrowIfNull(material);
        byte[] vertexBytes = PackStage(vertex, material), pixelBytes = PackStage(pixel, material);
        bool enabled = material.ValidateWork(maximumRaySamples, usesIntegration);
        return new(vertexBytes, pixelBytes, enabled, maximumRaySamples);
    }

    private static byte[] PackStage(SkyCloudPatternStage stage, SkyCloudPatternMaterial material)
    {
        byte[] bytes = new byte[stage.Bytes];
        foreach (var field in stage.Fields)
        {
            float[] values = Values(material, field.Semantic);
            for (int index = 0; index < values.Length; index++)
            {
                if (!float.IsFinite(values[index]))
                    throw new InvalidDataException($"Pattern used field {fields[field.Semantic].Name} is nonfinite.");
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(field.Offset + index * 4), values[index]);
            }
        }
        return bytes;
    }

    /// <summary>Compatibility import consumes only actual used named fields;
    /// padding and unused declarations never become the material authority.</summary>
    public SkyCloudPatternMaterial ReadMaterial(ReadOnlySpan<byte> vertexBytes, ReadOnlySpan<byte> pixelBytes)
    {
        if (vertexBytes.Length != vertex.Bytes || pixelBytes.Length != pixel.Bytes)
            throw new InvalidDataException("Pattern encoded inputs differ from the imported buffer sizes.");
        var values = fields.Select(field => new float[field.Bytes / 4]).ToArray();
        foreach (var field in vertex.Fields)
            ReadField(field, vertexBytes, values[field.Semantic]);
        foreach (var field in pixel.Fields)
            ReadField(field, pixelBytes, values[field.Semantic]);
        Vector2 V2(int id) => new(values[id][0], values[id][1]);
        Vector3 V3(int id) => new(values[id][0], values[id][1], values[id][2]);
        Vector4 V4(int id, int at = 0) => new(values[id][at], values[id][at+1], values[id][at+2], values[id][at+3]);
        var m = values[1];
        return new(new(V4(0), new(m[0],m[1],m[2],m[3],m[4],m[5],m[6],m[7],m[8],m[9],m[10],m[11],m[12],m[13],m[14],m[15]),
            new(V4(2),V4(2,4),V4(2,8),V4(2,12)),V2(3)),
            new(V4(4),V3(5),V3(6),V3(7),V3(8),V2(9),V2(10),V4(11),V4(12),V4(13),V4(14),V4(15),V2(16)));
    }

    private static void ReadField(SkyCloudPatternField field, ReadOnlySpan<byte> source, float[] values)
    {
        for (int index = 0; index < values.Length; index++)
            values[index] = BinaryPrimitives.ReadSingleLittleEndian(source.Slice(field.Offset + index * 4, 4));
    }

    private static float[] Values(SkyCloudPatternMaterial material, int semantic)
    {
        static float[] V2(Vector2 v) => [v.X,v.Y];
        static float[] V3(Vector3 v) => [v.X,v.Y,v.Z];
        static float[] V4(Vector4 v) => [v.X,v.Y,v.Z,v.W];
        var vertex = material.Vertex; var pixel = material.Pixel;
        return semantic switch
        {
            0 => V4(vertex.ClipPlane),
            1 => [vertex.ProjectionToWorld.M11,vertex.ProjectionToWorld.M12,vertex.ProjectionToWorld.M13,vertex.ProjectionToWorld.M14,
                vertex.ProjectionToWorld.M21,vertex.ProjectionToWorld.M22,vertex.ProjectionToWorld.M23,vertex.ProjectionToWorld.M24,
                vertex.ProjectionToWorld.M31,vertex.ProjectionToWorld.M32,vertex.ProjectionToWorld.M33,vertex.ProjectionToWorld.M34,
                vertex.ProjectionToWorld.M41,vertex.ProjectionToWorld.M42,vertex.ProjectionToWorld.M43,vertex.ProjectionToWorld.M44],
            2 => V4(vertex.CameraDirections.Direction0).Concat(V4(vertex.CameraDirections.Direction1))
                .Concat(V4(vertex.CameraDirections.Direction2)).Concat(V4(vertex.CameraDirections.Direction3)).ToArray(),
            3 => V2(vertex.ProjectionFarNear), 4 => V4(pixel.Eye), 5 => V3(pixel.BaseTextureOffset), 6 => V3(pixel.BaseTextureScale),
            7 => V3(pixel.DetailTextureOffset), 8 => V3(pixel.DetailTextureScale), 9 => V2(pixel.DistortTextureOffset),
            10 => V2(pixel.DistortTextureScale), 11 => V4(pixel.RaySampleCount), 12 => V4(pixel.EarthRadius),
            13 => V4(pixel.CloudParameters0), 14 => V4(pixel.CloudParameters1), 15 => V4(pixel.DistortionScale), 16 => V2(pixel.ErosionScale),
            _ => throw new ArgumentOutOfRangeException(nameof(semantic)),
        };
    }
}
