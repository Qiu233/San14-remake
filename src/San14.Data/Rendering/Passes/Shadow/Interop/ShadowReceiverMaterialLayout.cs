using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public enum ShadowReceiverMaterialField : uint
{
    LightDirection, AmbientLight, WorldToPattern, PatternScale, WorldToShadows,
    ShadowMapOffsets, ShadowDepthProjection, ShadowMapDepthFade, ShadowMapEnabled,
    ShadowMapScale, SecondaryShadowMapScale, ShadowDepthPlane, SubShadowCount,
    WorldToSubShadows, SubShadowMapScale, ViewToWorld, ProjectionToWorld, SlopeBias,
    DepthFade, ShadowLoopCount, ShadowLightPlanes, SubShadowLightPlanes,
}

public enum ShadowReceiverTextureRole : uint { Shadow, Lighting, SubShadow, Depth, Normals }
public enum ShadowReceiverSamplerRole : uint { Shadow, Lighting, SubShadow, Depth, Normals, ShadowPoint }

public sealed record ShadowReceiverFieldWrite(ShadowReceiverMaterialField Field,
    int DestinationOffset, int SourceOffset, int ByteCount);
public sealed record ShadowReceiverTextureBinding(ShadowReceiverTextureRole Role, uint Slot, uint Dimension);
public sealed record ShadowReceiverSamplerBinding(ShadowReceiverSamplerRole Role, uint Slot, uint TextureSlot);
public sealed record ShadowReceiverPackedMaterial(byte[] VertexBytes, byte[] PixelBytes);

/// <summary>Named receiver values. A missing value is permitted only when the
/// selected program does not use it; it is never replaced by retained buffer bytes.</summary>
public sealed record ShadowReceiverMaterial
{
    public Vector4? LightDirection { get; init; }
    public Vector4? AmbientLight { get; init; }
    public Matrix4x4? WorldToPattern { get; init; }
    public float? PatternScale { get; init; }
    public IReadOnlyList<Matrix4x4>? WorldToShadows { get; init; }
    public IReadOnlyList<Vector4>? ShadowMapOffsets { get; init; }
    public Vector4? ShadowDepthProjection { get; init; }
    public Vector4? ShadowMapDepthFade { get; init; }
    public IReadOnlyList<bool>? ShadowMapEnabled { get; init; }
    public Vector4? ShadowMapScale { get; init; }
    public Vector4? SecondaryShadowMapScale { get; init; }
    public Vector4? ShadowDepthPlane { get; init; }
    public int? SubShadowCount { get; init; }
    public IReadOnlyList<Matrix4x4>? WorldToSubShadows { get; init; }
    public Vector4? SubShadowMapScale { get; init; }
    public Matrix4x4? ViewToWorld { get; init; }
    public Matrix4x4? ProjectionToWorld { get; init; }
    public Vector2? SlopeBias { get; init; }
    public float? DepthFade { get; init; }
    public int? ShadowLoopCount { get; init; }
    public IReadOnlyList<Vector4>? ShadowLightPlanes { get; init; }
    public IReadOnlyList<Vector4>? SubShadowLightPlanes { get; init; }
}

/// <summary>Imports the actual used receiver ABI once. Material packing follows
/// named semantics and imported offsets, without a source-container fingerprint.</summary>
public sealed class ShadowReceiverMaterialLayout
{
    private sealed record FieldShape(string Name, int Bytes, int Class, int BaseType,
        int Rows, int Columns, int Elements);
    private sealed record Stage(int Slot, int Bytes, IReadOnlyList<ShadowReceiverFieldWrite> Writes);
    private static readonly ConditionalWeakTable<DxbcResourceDefinitions,
        ConditionalWeakTable<DxbcResourceDefinitions, ShadowReceiverMaterialLayout>> imported = new();
    private static readonly FieldShape[] shapes =
    [
        new("litDir",16,1,3,1,4,1), new("ambLit",16,1,3,1,4,0),
        new("mW2Pt",64,2,3,4,4,0), new("fPtn",4,0,3,1,1,0),
        new("mW2S",256,2,3,4,4,4), new("vSMOf",64,1,3,1,4,4),
        new("vS2VZ",16,1,3,1,4,0), new("vSMDF",16,1,3,1,4,0),
        new("bSM",36,0,1,1,1,3), new("scSM",16,1,3,1,4,0),
        new("scSM2",16,1,3,1,4,0), new("sdplane",16,1,3,1,4,0),
        new("nSubSdw",4,0,2,1,1,0), new("mSubW2S",256,2,3,4,4,4),
        new("scSubSM",16,1,3,1,4,0), new("mV2W",64,2,3,4,4,0),
        new("mP2W",64,2,3,4,4,0), new("srbSlp",8,1,3,1,2,0),
        new("fDOFade",4,0,3,1,1,0), new("nSSLoop",4,0,2,1,1,0),
        new("vLPlane",192,1,3,1,4,12), new("vSubLPlane",192,1,3,1,4,12),
    ];
    private readonly Stage vertex, pixel;

    private ShadowReceiverMaterialLayout(DxbcResourceDefinitions vertex, DxbcResourceDefinitions pixel)
    {
        this.vertex = ImportStage(vertex, true);
        this.pixel = ImportStage(pixel, false);
        if (pixel.Bindings.Any(binding => binding.InputType is not (0 or 2 or 3)))
            throw new NotSupportedException("Receiver resource kind has no producer.");
        var textures = new List<ShadowReceiverTextureBinding>();
        var textureNames = new Dictionary<string, ShadowReceiverTextureBinding>(StringComparer.Ordinal);
        foreach (var binding in pixel.Bindings.Where(binding => binding.InputType == 2))
        {
            var role = TextureRole(binding.Name.Text);
            uint dimension = role == ShadowReceiverTextureRole.SubShadow ? 5u : 4u;
            if (binding.BindCount != 1 || binding.BindPoint >= 128 || binding.Dimension != dimension || binding.ReturnType != 5 ||
                textures.Any(value => value.Role == role || value.Slot == binding.BindPoint))
                throw new NotSupportedException("Receiver texture has an unsupported actual interface.");
            var texture = new ShadowReceiverTextureBinding(role, binding.BindPoint, dimension);
            textures.Add(texture); textureNames.Add(binding.Name.Text, texture);
        }
        var samplers = new List<ShadowReceiverSamplerBinding>();
        foreach (var binding in pixel.Bindings.Where(binding => binding.InputType == 3))
        {
            if (binding.BindCount != 1 || binding.BindPoint >= 16 || binding.Dimension != 0 ||
                samplers.Any(value => value.Slot == binding.BindPoint))
                throw new NotSupportedException("Receiver sampler has an unsupported actual interface.");
            if (binding.Name.Text == "__smpsShadowPointSampler")
            {
                samplers.Add(new(ShadowReceiverSamplerRole.ShadowPoint, binding.BindPoint, uint.MaxValue));
                continue;
            }
            if (!binding.Name.Text.StartsWith("__smp", StringComparison.Ordinal) ||
                !textureNames.TryGetValue(binding.Name.Text[5..], out var texture))
                throw new NotSupportedException($"Receiver sampler {binding.Name.Text} has no actual texture association.");
            samplers.Add(new((ShadowReceiverSamplerRole)texture.Role, binding.BindPoint, texture.Slot));
        }
        TextureBindings = Array.AsReadOnly(textures.ToArray());
        SamplerBindings = Array.AsReadOnly(samplers.ToArray());
    }

    public int VertexSlot => vertex.Slot;
    public int VertexByteLength => vertex.Bytes;
    public int PixelSlot => pixel.Slot;
    public int PixelByteLength => pixel.Bytes;
    public IReadOnlyList<ShadowReceiverFieldWrite> VertexWrites => vertex.Writes;
    public IReadOnlyList<ShadowReceiverFieldWrite> PixelWrites => pixel.Writes;
    public IReadOnlyList<ShadowReceiverTextureBinding> TextureBindings { get; }
    public IReadOnlyList<ShadowReceiverSamplerBinding> SamplerBindings { get; }

    public static ShadowReceiverMaterialLayout Import(DxbcResourceDefinitions vertex, DxbcResourceDefinitions pixel)
    {
        ArgumentNullException.ThrowIfNull(vertex); ArgumentNullException.ThrowIfNull(pixel);
        return imported.GetValue(vertex, static _ => new()).GetValue(pixel, value => new(vertex, value));
    }

    private static Stage ImportStage(DxbcResourceDefinitions definitions, bool isVertex)
    {
        if (definitions.RawShaderType != (isVertex ? 0xFFFE : 0xFFFF))
            throw new NotSupportedException("Receiver stage type differs from its actual graphics interface.");
        if (isVertex)
        {
            if (definitions.Buffers.Count != 0 || definitions.Bindings.Count != 0)
                throw new NotSupportedException("Receiver vertex resources require an additional producer.");
            return new(-1, 0, Array.Empty<ShadowReceiverFieldWrite>());
        }
        if (definitions.Buffers.Count != 1)
            throw new NotSupportedException("Receiver pixel stage requires one actual constant buffer.");
        var buffer = definitions.Buffers[0];
        if (buffer.BufferType != 0 || buffer.ByteSize <= 0 || buffer.ByteSize > 65536 || buffer.ByteSize % 16 != 0)
            throw new NotSupportedException("Receiver constant-buffer storage is unsupported.");
        var writes = new List<ShadowReceiverFieldWrite>();
        var usedRanges = new List<(int Offset, int Bytes)>();
        var usedFields = new HashSet<int>();
        foreach (var variable in buffer.Variables)
        {
            if ((variable.Flags & 2) == 0) continue;
            int id = Array.FindIndex(shapes, value => value.Name == variable.Name.Text);
            if (id < 0)
                throw new NotSupportedException($"Receiver has no named material producer for {variable.Name.Text}.");
            var shape = shapes[id];
            var types = definitions.Types.Where(value => value.Offset == variable.TypeOffset).ToArray();
            if (types.Length != 1) throw new NotSupportedException("Receiver used field has no unique actual type.");
            var type = types[0];
            if (!usedFields.Add(id) || variable.StartOffset < 0 || variable.StartOffset % 4 != 0 ||
                variable.ByteSize != shape.Bytes || (long)variable.StartOffset + shape.Bytes > buffer.ByteSize ||
                type.Class != shape.Class || type.BaseType != shape.BaseType || type.Rows != shape.Rows ||
                type.Columns != shape.Columns || type.Elements != shape.Elements || type.Members.Count != 0)
                throw new NotSupportedException($"Receiver field {shape.Name} has an unsupported typed shape.");
            usedRanges.Add((variable.StartOffset, shape.Bytes));
            if ((ShadowReceiverMaterialField)id == ShadowReceiverMaterialField.ShadowMapEnabled)
            {
                for (int element = 0; element < 3; element++)
                    writes.Add(new((ShadowReceiverMaterialField)id, variable.StartOffset + element * 16, element * 4, 4));
            }
            else writes.Add(new((ShadowReceiverMaterialField)id, variable.StartOffset, 0, shape.Bytes));
        }
        var ordered = usedRanges.OrderBy(value => value.Offset).ToArray();
        for (int index = 1; index < ordered.Length; index++)
            if (ordered[index - 1].Offset + ordered[index - 1].Bytes > ordered[index].Offset)
                throw new NotSupportedException("Receiver used material fields overlap.");
        var bindings = definitions.Bindings.Where(value => value.InputType == 0).ToArray();
        if (bindings.Length != 1 || bindings[0].Name.Text != buffer.Name.Text || bindings[0].BindCount != 1 ||
            bindings[0].Dimension != 0 || bindings[0].BindPoint >= 14)
            throw new NotSupportedException("Receiver constant-buffer binding is unsupported.");
        return new(checked((int)bindings[0].BindPoint), buffer.ByteSize,
            Array.AsReadOnly(writes.OrderBy(value => value.DestinationOffset).ToArray()));
    }

    private static ShadowReceiverTextureRole TextureRole(string name) => name switch
    {
        "sShadow" => ShadowReceiverTextureRole.Shadow, "sLit" => ShadowReceiverTextureRole.Lighting,
        "sSubShadow" => ShadowReceiverTextureRole.SubShadow, "sDepth" => ShadowReceiverTextureRole.Depth,
        "sGBuf0" => ShadowReceiverTextureRole.Normals,
        _ => throw new NotSupportedException($"Receiver texture {name} has no source role."),
    };

    public byte[] EncodeNativeLayout()
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(0x314C4D52u); // RML1
        foreach (var stage in new[] { vertex, pixel })
        { writer.Write(stage.Slot); writer.Write((uint)stage.Bytes); writer.Write((uint)stage.Writes.Count); }
        writer.Write((uint)TextureBindings.Count); writer.Write((uint)SamplerBindings.Count);
        foreach (var stage in new[] { vertex, pixel })
            foreach (var write in stage.Writes)
            {
                writer.Write((uint)write.Field); writer.Write((uint)write.DestinationOffset);
                writer.Write((uint)write.SourceOffset); writer.Write((uint)write.ByteCount);
            }
        foreach (var texture in TextureBindings)
        { writer.Write((uint)texture.Role); writer.Write(texture.Slot); writer.Write(texture.Dimension); }
        foreach (var sampler in SamplerBindings)
        { writer.Write((uint)sampler.Role); writer.Write(sampler.Slot); writer.Write(sampler.TextureSlot); }
        return stream.ToArray();
    }

    public ShadowReceiverPackedMaterial Pack(ShadowReceiverMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        var values = new Dictionary<ShadowReceiverMaterialField, byte[]>();
        byte[] StageBytes(Stage stage)
        {
            byte[] storage = new byte[stage.Bytes];
            foreach (var write in stage.Writes)
            {
                if (!values.TryGetValue(write.Field, out var source))
                { source = EncodeField(material, write.Field); values.Add(write.Field, source); }
                source.AsSpan(write.SourceOffset, write.ByteCount).CopyTo(storage.AsSpan(write.DestinationOffset, write.ByteCount));
            }
            return storage;
        }
        return new(StageBytes(vertex), StageBytes(pixel));
    }

    private static byte[] EncodeField(ShadowReceiverMaterial material, ShadowReceiverMaterialField field)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        T Required<T>(T? value) where T : struct => value ?? throw new InvalidDataException($"Receiver used field {field} has no current value.");
        void Float(float value)
        {
            if (!float.IsFinite(value)) throw new InvalidDataException($"Receiver used field {field} is nonfinite.");
            writer.Write(value);
        }
        void V4(Vector4 value) { Float(value.X); Float(value.Y); Float(value.Z); Float(value.W); }
        void Matrix(Matrix4x4 value)
        {
            Float(value.M11); Float(value.M12); Float(value.M13); Float(value.M14);
            Float(value.M21); Float(value.M22); Float(value.M23); Float(value.M24);
            Float(value.M31); Float(value.M32); Float(value.M33); Float(value.M34);
            Float(value.M41); Float(value.M42); Float(value.M43); Float(value.M44);
        }
        IReadOnlyList<T> Array<T>(IReadOnlyList<T>? value, int count)
            => value is { } array && array.Count == count ? array :
                throw new InvalidDataException($"Receiver used field {field} requires {count} current elements.");
        switch (field)
        {
            case ShadowReceiverMaterialField.LightDirection: V4(Required(material.LightDirection)); break;
            case ShadowReceiverMaterialField.AmbientLight: V4(Required(material.AmbientLight)); break;
            case ShadowReceiverMaterialField.WorldToPattern: Matrix(Required(material.WorldToPattern)); break;
            case ShadowReceiverMaterialField.PatternScale: Float(Required(material.PatternScale)); break;
            case ShadowReceiverMaterialField.WorldToShadows: foreach (var value in Array(material.WorldToShadows,4)) Matrix(value); break;
            case ShadowReceiverMaterialField.ShadowMapOffsets: foreach (var value in Array(material.ShadowMapOffsets,4)) V4(value); break;
            case ShadowReceiverMaterialField.ShadowDepthProjection: V4(Required(material.ShadowDepthProjection)); break;
            case ShadowReceiverMaterialField.ShadowMapDepthFade: V4(Required(material.ShadowMapDepthFade)); break;
            case ShadowReceiverMaterialField.ShadowMapEnabled: foreach (bool value in Array(material.ShadowMapEnabled,3)) writer.Write(value ? 1u : 0u); break;
            case ShadowReceiverMaterialField.ShadowMapScale: V4(Required(material.ShadowMapScale)); break;
            case ShadowReceiverMaterialField.SecondaryShadowMapScale: V4(Required(material.SecondaryShadowMapScale)); break;
            case ShadowReceiverMaterialField.ShadowDepthPlane: V4(Required(material.ShadowDepthPlane)); break;
            case ShadowReceiverMaterialField.SubShadowCount: writer.Write(Required(material.SubShadowCount)); break;
            case ShadowReceiverMaterialField.WorldToSubShadows: foreach (var value in Array(material.WorldToSubShadows,4)) Matrix(value); break;
            case ShadowReceiverMaterialField.SubShadowMapScale: V4(Required(material.SubShadowMapScale)); break;
            case ShadowReceiverMaterialField.ViewToWorld: Matrix(Required(material.ViewToWorld)); break;
            case ShadowReceiverMaterialField.ProjectionToWorld: Matrix(Required(material.ProjectionToWorld)); break;
            case ShadowReceiverMaterialField.SlopeBias: var bias = Required(material.SlopeBias); Float(bias.X); Float(bias.Y); break;
            case ShadowReceiverMaterialField.DepthFade: Float(Required(material.DepthFade)); break;
            case ShadowReceiverMaterialField.ShadowLoopCount: writer.Write(Required(material.ShadowLoopCount)); break;
            case ShadowReceiverMaterialField.ShadowLightPlanes: foreach (var value in Array(material.ShadowLightPlanes,12)) V4(value); break;
            case ShadowReceiverMaterialField.SubShadowLightPlanes: foreach (var value in Array(material.SubShadowLightPlanes,12)) V4(value); break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
        return stream.ToArray();
    }
}
