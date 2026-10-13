using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Named Sky2 constant fields resolved once from an imported program.
/// Current writes retain template padding and validate only current values.</summary>
public sealed class Sky2ConstantLayout
{
    private enum Value
    {
        Fog, CameraToLocal, ProjectionFar, LocalToGlobal, Scattering, ViewPosition,
        SkyInfo0, SkyInfo1, SunRgb, SunDirection, MoonRgb, MoonDirection, MoonFace,
        SunScreen, MoonScreen, Altitude, StableDimensions, Clamp
    }
    private sealed record Field(Value Value, string Name, int Offset, int Words, int Elements, int Stride);
    private readonly Field[] fields;
    private readonly bool pixel, acceptsZeroMoon;
    private readonly IReadOnlyList<Sky2ConstantWrite> writes;
    public int ByteLength { get; }

    private Sky2ConstantLayout(DxbcResourceDefinitions definitions, bool pixel)
    {
        this.pixel = pixel;
        if (definitions.Buffers.Count != 1 || definitions.Buffers[0].Name.Text != "$Globals")
            throw new InvalidDataException("Sky2 constants require one named globals buffer.");
        var buffer = definitions.Buffers[0];
        ByteLength = buffer.ByteSize;
        var prepared = new List<Field>(); var kinds = new HashSet<Value>();
        foreach (var variable in buffer.Variables)
        {
            var kind = variable.Name.Text switch
            {
                "fog" => Value.Fog, "g_mC2L" => Value.CameraToLocal, "g_fProjFarZ" => Value.ProjectionFar,
                "g_mL2G" => Value.LocalToGlobal, "Scat" => Value.Scattering, "g_vViewPos" => Value.ViewPosition,
                "g_vSkyInfo0" => Value.SkyInfo0, "g_vSkyInfo1" => Value.SkyInfo1,
                "g_vSunRGB" => Value.SunRgb, "g_vSunDir" => Value.SunDirection,
                "g_vMoonRGB" => Value.MoonRgb, "g_vMoonDir" => Value.MoonDirection,
                "g_mMoonFace" => Value.MoonFace, "g_vSunScreenPos" => Value.SunScreen,
                "g_vMoonScreenPos" => Value.MoonScreen, "g_vAltitudeInfo" => Value.Altitude,
                "g_vStableDim" => Value.StableDimensions, "g_vClampValue" => Value.Clamp,
                _ => throw new NotSupportedException($"Unknown Sky2 constant {variable.Name.Text}.")
            };
            bool vertexOnly = kind is Value.CameraToLocal or Value.ProjectionFar or Value.LocalToGlobal;
            if (!kinds.Add(kind) || (kind != Value.Fog && pixel == vertexOnly))
                throw new InvalidDataException("Sky2 constant is duplicated or belongs to another stage.");
            ushort typeClass = 1, rows = 1, columns = 4, elements = 0;
            if (kind is Value.CameraToLocal or Value.LocalToGlobal or Value.MoonFace)
            { typeClass = 2; rows = 4; }
            else if (kind == Value.ProjectionFar) { typeClass = 0; columns = 1; }
            else if (kind == Value.Scattering) elements = 4;
            var type = definitions.Types.Single(t => t.Offset == variable.TypeOffset);
            int words = rows * columns * Math.Max(1, (int)elements);
            if (type.Class != typeClass || type.BaseType != 3 || type.Rows != rows || type.Columns != columns ||
                type.Elements != elements || type.Members.Count != 0 || variable.ByteSize != words * 4 ||
                variable.StartOffset < 0 || variable.StartOffset % 4 != 0 || variable.StartOffset > ByteLength - words * 4)
                throw new InvalidDataException($"Unsupported Sky2 constant declaration {variable.Name.Text}.");
            prepared.Add(new(kind, variable.Name.Text, variable.StartOffset, words,
                Math.Max(1, (int)elements), elements == 0 ? words * 4 : 16));
            if (kind == Value.MoonRgb) acceptsZeroMoon = (variable.Flags & 2) == 0;
        }
        var required = pixel ? new[] { Value.ViewPosition, Value.SkyInfo1, Value.Altitude, Value.Clamp }
            : new[] { Value.CameraToLocal, Value.ProjectionFar, Value.LocalToGlobal };
        if (required.Any(kind => !kinds.Contains(kind)))
            throw new InvalidDataException("Sky2 constant buffer is missing a required field.");
        fields = prepared.OrderBy(f => f.Offset).ToArray();
        for (int i = 1; i < fields.Length; i++)
            if (fields[i].Offset < fields[i - 1].Offset + fields[i - 1].Words * 4)
                throw new InvalidDataException("Sky2 constant fields overlap.");
        writes = Array.AsReadOnly(fields.Select(f => new Sky2ConstantWrite(f.Name, f.Offset, f.Elements, f.Stride, f.Stride)).ToArray());
    }
    public static Sky2ConstantLayout ImportVertex(DxbcResourceDefinitions definitions) => new(definitions, false);
    public static Sky2ConstantLayout ImportPixel(DxbcResourceDefinitions definitions) => new(definitions, true);

    public Sky2ConstantPacket WriteVertex(ReadOnlySpan<byte> template, Sky2VertexInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (pixel) throw new InvalidOperationException("Pixel constant layout cannot write vertex inputs.");
        input.SourceMode4Fog?.ValidateValue(input.Fog);
        byte[] bytes = CopyTemplate(template);
        Span<float> values = stackalloc float[16];
        foreach (var field in fields)
        {
            switch (field.Value)
            {
                case Value.Fog: Vector(values, input.Fog); break;
                case Value.CameraToLocal: Matrix(values, input.CameraToLocal); break;
                case Value.ProjectionFar: values[0] = input.ProjectionFarZ; break;
                case Value.LocalToGlobal: Matrix(values, input.LocalToGlobal); break;
            }
            Write(bytes, field, values[..field.Words], input.SourceMode4Fog is not null, false);
        }
        return new(bytes, writes);
    }
    public Sky2ConstantPacket WritePixel(ReadOnlySpan<byte> template, Sky2PixelInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!pixel) throw new InvalidOperationException("Vertex constant layout cannot write pixel inputs.");
        input.SourceMode4Fog?.ValidateValue(input.Fog);
        if (input.SourceZeroMoon is { } zero)
        {
            if (!acceptsZeroMoon) throw new InvalidDataException("Selected shader reads Moon color or has no Moon field.");
            zero.ValidatePreparedRgb(input.MoonRgb);
        }
        byte[] bytes = CopyTemplate(template);
        Span<float> values = stackalloc float[16];
        foreach (var field in fields)
        {
            switch (field.Value)
            {
                case Value.Fog: Vector(values, input.Fog); break;
                case Value.Scattering:
                    if (input.Scattering is null || input.Scattering.Count != 4)
                        throw new InvalidDataException("Sky2 scattering requires four float4 values.");
                    for (int i = 0; i < 4; i++) Vector(values[(i * 4)..], input.Scattering[i]);
                    break;
                case Value.ViewPosition: Vector(values, input.ViewPosition); break;
                case Value.SkyInfo0: Vector(values, input.SkyInfo0); break;
                case Value.SkyInfo1: Vector(values, input.SkyInfo1); break;
                case Value.SunRgb: Vector(values, input.SunRgb); break;
                case Value.SunDirection: Vector(values, input.SunDirection); break;
                case Value.MoonRgb: Vector(values, input.MoonRgb); break;
                case Value.MoonDirection: Vector(values, input.MoonDirection); break;
                case Value.MoonFace: Matrix(values, input.MoonFace ?? throw new InvalidDataException("Sky2 Moon face is required.")); break;
                case Value.SunScreen: Vector(values, input.SunScreenPosition); break;
                case Value.MoonScreen: Vector(values, input.MoonScreenPosition ?? throw new InvalidDataException("Sky2 Moon screen position is required.")); break;
                case Value.Altitude: Vector(values, input.AltitudeInfo); break;
                case Value.StableDimensions: Vector(values, input.StableDimensions); break;
                case Value.Clamp: Vector(values, input.ClampValue); break;
            }
            Write(bytes, field, values[..field.Words], input.SourceMode4Fog is not null, input.SourceZeroMoon is not null);
        }
        return new(bytes, writes);
    }
    private byte[] CopyTemplate(ReadOnlySpan<byte> template)
    {
        if (template.Length != ByteLength) throw new InvalidDataException("Sky2 requires a complete constant template.");
        return template.ToArray();
    }
    private static void Write(byte[] bytes, Field field, ReadOnlySpan<float> values, bool mode4Fog, bool zeroMoon)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!(mode4Fog && field.Value == Value.Fog) && !(zeroMoon && field.Value == Value.MoonRgb && i == 3) &&
                !float.IsFinite(values[i]))
                throw new InvalidDataException($"Nonfinite Sky2 constant {field.Name}.");
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(field.Offset + i * 4, 4), values[i]);
        }
    }
    private static void Vector(Span<float> values, Vector4 v)
    { values[0] = v.X; values[1] = v.Y; values[2] = v.Z; values[3] = v.W; }
    private static void Matrix(Span<float> values, Matrix4x4 m)
    {
        Vector(values, new(m.M11, m.M12, m.M13, m.M14));
        Vector(values[4..], new(m.M21, m.M22, m.M23, m.M24));
        Vector(values[8..], new(m.M31, m.M32, m.M33, m.M34));
        Vector(values[12..], new(m.M41, m.M42, m.M43, m.M44));
    }
}
