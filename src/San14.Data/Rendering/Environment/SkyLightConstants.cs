using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record SkyLightVertexInputs(Vector4 Eye, Vector4 MipInfo);
public sealed record SkyLightPixelInputs(Vector4 LightDirection, Vector4 LightColor,
    Vector4 AmbientLight, Vector4 Eye, Vector4 Fog, Vector4 FogColor,
    IReadOnlyList<Vector4> Scattering, Vector4 Albedo, Vector4 ReflectivityRoughness);
public sealed record SkyLightDrawInputs(SkyLightVertexInputs Vertex,
    SkyLightPixelInputs Pixel);

/// <summary>Exact default-variant native pass12 named packet layout. Source
/// light/camera/material values and all templates are explicit; this does not
/// invent an active light pack or implement its higher-level dirty policy.</summary>
public static class SkyLightConstants
{
    public static byte[] Vertex(DxbcResourceDefinitions definitions, ReadOnlySpan<byte> template,
        SkyLightVertexInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Pack(definitions, template, 32, [("vEye", 0, 0, new[] { input.Eye }),
            ("g_vMipInfo", 16, 0, new[] { input.MipInfo })]);
    }

    public static byte[] Pixel(DxbcResourceDefinitions definitions, ReadOnlySpan<byte> template,
        SkyLightPixelInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Scattering is null || input.Scattering.Count != 4)
            throw new InvalidDataException("Sky light pass12 needs four explicit Scat vectors.");
        // Microsoft text omits the source's one-element light arrays. Keep
        // RDEF Elements=1 for these two fields, rather than treating them as
        // interchangeable scalar float4 definitions.
        return Pack(definitions, template, 192, [("litDir", 0, 1, new[] { input.LightDirection }),
            ("litCol", 16, 1, new[] { input.LightColor }), ("ambLit", 32, 0, new[] { input.AmbientLight }),
            ("vEye", 48, 0, new[] { input.Eye }), ("fog", 64, 0, new[] { input.Fog }),
            ("cFog", 80, 0, new[] { input.FogColor }), ("Scat", 96, 4, input.Scattering.ToArray()),
            ("g_clAlbedo", 160, 0, new[] { input.Albedo }),
            ("g_clRefRough", 176, 0, new[] { input.ReflectivityRoughness })]);
    }

    private static byte[] Pack(DxbcResourceDefinitions definitions, ReadOnlySpan<byte> template,
        int bytes, (string Name, int Offset, int Elements, Vector4[] Vectors)[] fields)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (template.Length != bytes || definitions.Buffers.Count != 1 ||
            definitions.Buffers[0] is not { } buffer || buffer.Name.Text != "$Globals" ||
            buffer.ByteSize != bytes || buffer.Variables.Count != fields.Length)
            throw new InvalidDataException("Sky light pass12 requires the verified complete constant layout.");
        var result = template.ToArray();
        foreach (var field in fields)
        {
            var matches = buffer.Variables.Where(v => v.Name.Text == field.Name).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Sky light source field is missing or duplicated: " + field.Name);
            var variable = matches[0]; var type = definitions.Types.Single(t => t.Offset == variable.TypeOffset);
            if (variable.StartOffset != field.Offset || variable.ByteSize != field.Vectors.Length * 16 ||
                type.Class != 1 || type.BaseType != 3 || type.Rows != 1 || type.Columns != 4 ||
                type.Elements != field.Elements || type.Members.Count != 0)
                throw new InvalidDataException("Sky light source field type/offset differs: " + field.Name);
            for (int index = 0; index < field.Vectors.Length; index++)
            {
                var v = field.Vectors[index]; float[] words = [v.X, v.Y, v.Z, v.W];
                for (int c = 0; c < 4; c++)
                {
                    if (!float.IsFinite(words[c])) throw new InvalidDataException("Sky light field contains a nonfinite value: " + field.Name);
                    BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(field.Offset + index * 16 + c * 4, 4), words[c]);
                }
            }
        }
        return result;
    }
}
