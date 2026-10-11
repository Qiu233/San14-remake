using San14.Data.Formats;

namespace San14.Data;

/// <summary>The selected water material ABI consumed by the Native constant
/// writers, vertex streams and texture bindings. Check it once at resource import;
/// another layout needs its own writer rather than reusing cached material bytes.</summary>
internal static class OceanShaderAbi
{
    private static readonly (string Name, int Offset, int Size)[] vsFields =
    [
        ("vEye", 0, 16),
        ("mW2P", 16, 64),
        ("ksgClipPlane", 80, 96),
        ("vNormalParam", 176, 16),
        ("vNormalNoiseParam", 192, 12),
        ("vHeightMaskUV", 208, 16),
        ("fWaterLevel", 224, 4),
        ("vWaveDirect", 240, 16),
        ("vWaveParameter", 256, 16),
        ("vWaveParameter2", 272, 16),
        ("vWaveFigure", 288, 16),
        ("vWaveNoiseTextureParam", 304, 16),
        ("vShoreBubbleParam", 320, 16),
        ("vBubbleShoalingAjustParam", 336, 16),
        ("vCameraDirXZ", 352, 16),
        ("fShoalingScale", 368, 4),
    ];

    private static readonly (string Name, int Offset, int Size)[] psFields =
    [
        ("vEye", 0, 16),
        ("mWV", 16, 48),
        ("fAmbMapsDORate", 64, 4),
        ("vAmbDifScl", 80, 16),
        ("vNormalNoiseParam", 96, 12),
        ("mRrW2T", 112, 48),
        ("fRrRefractiveIndex", 160, 4),
        ("fRrTransparency", 164, 4),
        ("fDiffuseTransparency", 168, 4),
        ("vRrParam", 176, 16),
        ("vD2Z", 192, 8),
        ("vRrCameraPos", 208, 16),
        ("vRrCameraDir", 224, 16),
        ("vWaveBubbleColorScale", 240, 16),
        ("vWaveSurfaceBubbleColor", 256, 48),
        ("vWaveSurfaceBubbleParam", 304, 16),
        ("vWaveInnerBubbleColor", 320, 48),
        ("vWaveInnerBubbleParam", 368, 16),
        ("vShoreBubbleColor", 384, 48),
        ("vShoreBubbleParam", 432, 16),
        ("vBubbleShoalingAjustParam", 448, 16),
        ("vOceanScatteringColor", 464, 16),
        ("vWaveBubbleSpawnPointArray", 480, 256),
        ("vBubbleDepth", 736, 16),
        ("vBubbleTextureArrayNo", 752, 16),
        ("vWaveSurfaceBubbleScale", 768, 16),
        ("vWaveInnerBubbleScale", 784, 16),
        ("vShoreBubbleScale", 800, 16),
        ("vWaveBubbleOffset", 816, 8),
        ("vAlbedoColor", 832, 16),
        ("vReflectance", 848, 16),
    ];

    private static readonly (string Name, uint Type, uint Dimension, uint Slot)[] vsBindings =
    [
        ("$Globals", 0, 0, 0),
        ("__smpsOceanHeightMask", 3, 0, 0), ("sOceanHeightMask", 2, 4, 0),
        ("__smpsOceanHorizontalDisplacementMap", 3, 0, 1), ("sOceanHorizontalDisplacementMap", 2, 4, 1)
    ];
    private static readonly (string Name, uint Type, uint Dimension, uint Slot)[] psBindings =
    [
        ("$Globals", 0, 0, 0),
        ("__smpsOceanNormalMap", 3, 0, 0), ("sOceanNormalMap", 2, 8, 0),
        ("__smpsOceanDepthMap", 3, 0, 2), ("sOceanDepthMap", 2, 4, 2),
        ("__smpsOceanNormalNoiseMap", 3, 0, 3), ("sOceanNormalNoiseMap", 2, 4, 3),
        ("__smpsOceanBubbleMap", 3, 0, 4), ("sOceanBubbleMap", 2, 5, 4)
    ];

    internal static void RequireSupported(byte[] vertex, byte[] pixel)
    {
        RequireStage(vertex, 0xFFFE, 384, vsFields, vsBindings);
        RequireStage(pixel, 0xFFFF, 864, psFields, psBindings);
        var vertexSignature = DxbcSignatureReader.Read(vertex);
        var pixelSignature = DxbcSignatureReader.Read(pixel);
        var input = vertexSignature.Parts.Single(part => part.Kind == DxbcSignatureKind.Input);
        (string Name, uint Index, byte Mask)[] streams =
            [("POSITION", 0, 7), ("NORMAL", 0, 3), ("TEXCOORD", 0, 7), ("TEXCOORD", 1, 3)];
        if (input.Parameters.Count != streams.Length || streams.Any(stream => !input.Parameters.Any(parameter =>
            parameter.SemanticName.Text.Equals(stream.Name, StringComparison.OrdinalIgnoreCase) &&
            parameter.SemanticIndex == stream.Index && parameter.Mask == stream.Mask &&
            parameter.ComponentType == 3 && parameter.RawSystemValue == 0)))
            throw Unsupported("vertex stream signature");
        var vertexOutput = vertexSignature.Parts.Single(part => part.Kind == DxbcSignatureKind.Output);
        var pixelInput = pixelSignature.Parts.Single(part => part.Kind == DxbcSignatureKind.Input);
        if (pixelInput.Parameters.Any(parameter => !vertexOutput.Parameters.Any(producer =>
            producer.SemanticName.Text.Equals(parameter.SemanticName.Text, StringComparison.OrdinalIgnoreCase) &&
            producer.SemanticIndex == parameter.SemanticIndex && producer.ComponentType == parameter.ComponentType &&
            (producer.Mask & parameter.Mask) == parameter.Mask)))
            throw Unsupported("vertex-to-pixel linkage");
        var output = pixelSignature.Parts.Single(part => part.Kind == DxbcSignatureKind.Output);
        if (output.Parameters.Count != 6 || Enumerable.Range(0, 6).Any(target => !output.Parameters.Any(parameter =>
            parameter.SemanticName.Text.Equals("SV_Target", StringComparison.OrdinalIgnoreCase) &&
            parameter.SemanticIndex == target && parameter.ComponentType == 3 && parameter.Mask == 15)))
            throw Unsupported("six water render targets");
    }

    private static void RequireStage(byte[] bytecode, ushort stage, int size,
        (string Name, int Offset, int Size)[] fields,
        (string Name, uint Type, uint Dimension, uint Slot)[] bindings)
    {
        var definitions = DxbcResourceDefinitionReader.Read(bytecode);
        if (definitions.RawShaderType != stage || definitions.Buffers.Count != 1)
            throw Unsupported("shader stage or constant buffers");
        var globals = definitions.Buffers[0];
        if (globals.Name.Text != "$Globals" || globals.ByteSize != size || globals.Variables.Count != fields.Length ||
            fields.Any(field => !globals.Variables.Any(variable => variable.Name.Text == field.Name &&
                variable.StartOffset == field.Offset && variable.ByteSize == field.Size)))
            throw Unsupported("named constant layout");
        foreach (var field in fields)
        {
            var variable = globals.Variables.Single(value => value.Name.Text == field.Name);
            var type = definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            (int Class, int Rows, int Columns, int Elements) shape = field.Name switch
            {
                "mW2P" => (2, 4, 4, 0),
                "ksgClipPlane" => (1, 1, 4, 6),
                "mWV" or "mRrW2T" or "vWaveSurfaceBubbleColor" or "vWaveInnerBubbleColor" or "vShoreBubbleColor" => (1, 1, 4, 3),
                "vWaveBubbleSpawnPointArray" => (1, 1, 4, 16),
                _ => (field.Size == 4 ? 0 : 1, 1, field.Size / 4, 0)
            };
            bool used = field.Name is not ("fAmbMapsDORate" or "vAmbDifScl");
            if (type.BaseType != 3 || type.Class != shape.Class || type.Rows != shape.Rows ||
                type.Columns != shape.Columns || type.Elements != shape.Elements || type.Members.Count != 0 ||
                ((variable.Flags & 2) != 0) != used)
                throw Unsupported($"typed material field {field.Name}");
        }
        if (definitions.Bindings.Count != bindings.Length || bindings.Any(expected => !definitions.Bindings.Any(binding =>
            binding.Name.Text == expected.Name && binding.InputType == expected.Type && binding.Dimension == expected.Dimension &&
            binding.BindPoint == expected.Slot && binding.BindCount == 1 &&
            (expected.Type != 2 || binding.ReturnType == 5))))
            throw Unsupported("texture, sampler or constant binding");
    }

    private static NotSupportedException Unsupported(string part) =>
        new($"Ocean PB2 wave {part} differs from the supported Native material ABI.");
}
