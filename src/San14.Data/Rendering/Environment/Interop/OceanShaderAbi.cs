using San14.Data.Formats;

namespace San14.Data;

/// <summary>The selected water material ABI consumed by the Native constant
/// producers, vertex streams and texture bindings. Import named destinations once
/// from the selected program; domain producers never depend on buffer offsets.</summary>
internal static class OceanShaderAbi
{
    private static readonly (string Name, int Id, int Size)[] vsFields =
    [
        ("vEye", 0, 16),
        ("mW2P", 1, 64),
        ("ksgClipPlane", 2, 96),
        ("vNormalParam", 3, 16),
        ("vNormalNoiseParam", 4, 12),
        ("vHeightMaskUV", 5, 16),
        ("fWaterLevel", 6, 4),
        ("vWaveDirect", 7, 16),
        ("vWaveParameter", 8, 16),
        ("vWaveParameter2", 9, 16),
        ("vWaveFigure", 10, 16),
        ("vWaveNoiseTextureParam", 11, 16),
        ("vShoreBubbleParam", 12, 16),
        ("vBubbleShoalingAjustParam", 13, 16),
        ("vCameraDirXZ", 14, 16),
        ("fShoalingScale", 15, 4),
    ];

    private static readonly (string Name, int Id, int Size)[] psFields =
    [
        ("vEye", 0, 16),
        ("mWV", 16, 48),
        ("vNormalNoiseParam", 4, 12),
        ("mRrW2T", 17, 48),
        ("fRrRefractiveIndex", 18, 4),
        ("fRrTransparency", 19, 4),
        ("fDiffuseTransparency", 20, 4),
        ("vRrParam", 21, 16),
        ("vD2Z", 22, 8),
        ("vRrCameraPos", 23, 16),
        ("vRrCameraDir", 24, 16),
        ("vWaveBubbleColorScale", 25, 16),
        ("vWaveSurfaceBubbleColor", 26, 48),
        ("vWaveSurfaceBubbleParam", 27, 16),
        ("vWaveInnerBubbleColor", 28, 48),
        ("vWaveInnerBubbleParam", 29, 16),
        ("vShoreBubbleColor", 30, 48),
        ("vShoreBubbleParam", 12, 16),
        ("vBubbleShoalingAjustParam", 13, 16),
        ("vOceanScatteringColor", 31, 16),
        ("vWaveBubbleSpawnPointArray", 32, 256),
        ("vBubbleDepth", 33, 16),
        ("vBubbleTextureArrayNo", 34, 16),
        ("vWaveSurfaceBubbleScale", 35, 16),
        ("vWaveInnerBubbleScale", 36, 16),
        ("vShoreBubbleScale", 37, 16),
        ("vWaveBubbleOffset", 38, 8),
        ("vAlbedoColor", 39, 16),
        ("vReflectance", 40, 16),
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

    internal static OceanMaterialLayout ImportLayout(byte[] vertex, byte[] pixel)
    {
        var vertexLayout = ImportStage(vertex, 0xFFFE, 1, vsFields, vsBindings);
        var pixelLayout = ImportStage(pixel, 0xFFFF, 0, psFields, psBindings);
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
        return new(vertexLayout, pixelLayout);
    }

    private static OceanMaterialStageLayout ImportStage(byte[] bytecode, ushort stage, uint stageCode,
        (string Name, int Id, int Size)[] fields,
        (string Name, uint Type, uint Dimension, uint Slot)[] bindings)
    {
        var definitions = DxbcResourceDefinitionReader.Read(bytecode);
        if (definitions.RawShaderType != stage || definitions.Buffers.Count != 1)
            throw Unsupported("shader stage or constant buffers");
        var globals = definitions.Buffers[0];
        if (globals.Name.Text != "$Globals" || globals.BufferType != 0 ||
            globals.ByteSize <= 0 || globals.ByteSize > 65536 || globals.ByteSize % 16 != 0)
            throw Unsupported("constant buffer storage");
        if (globals.Variables.Select(value => value.Name.Text).Distinct().Count() != globals.Variables.Count)
            throw Unsupported("duplicate constant names");
        var writes = new List<OceanMaterialFieldWrite>();
        foreach (var variable in globals.Variables)
        {
            // Unused declarations and padding receive explicit zero storage.
            // No ambient provider is inferred from an unused declaration.
            if ((variable.Flags & 2) == 0) continue;
            var field = fields.SingleOrDefault(value => value.Name == variable.Name.Text);
            if (field.Name is null) throw Unsupported($"live material field {variable.Name.Text}");
            var type = definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            (int Class, int Rows, int Columns, int Elements) shape = field.Name switch
            {
                "mW2P" => (2, 4, 4, 0),
                "ksgClipPlane" => (1, 1, 4, 6),
                "mWV" or "mRrW2T" or "vWaveSurfaceBubbleColor" or "vWaveInnerBubbleColor" or "vShoreBubbleColor" => (1, 1, 4, 3),
                "vWaveBubbleSpawnPointArray" => (1, 1, 4, 16),
                _ => (field.Size == 4 ? 0 : 1, 1, field.Size / 4, 0)
            };
            if (variable.ByteSize != field.Size || variable.StartOffset % 4 != 0 ||
                type.BaseType != 3 || type.Class != shape.Class || type.Rows != shape.Rows ||
                type.Columns != shape.Columns || type.Elements != shape.Elements || type.Members.Count != 0)
                throw Unsupported($"typed material field {field.Name}");
            writes.Add(new((uint)field.Id, (uint)variable.StartOffset, 0, (uint)field.Size));
        }
        var ordered = writes.OrderBy(value => value.Destination).ToArray();
        for (int index = 1; index < ordered.Length; index++)
            if (ordered[index - 1].Destination + ordered[index - 1].ByteCount > ordered[index].Destination)
                throw Unsupported("overlapping live constants");
        var constant = definitions.Bindings.SingleOrDefault(value => value.Name.Text == "$Globals");
        if (constant is null || constant.InputType != 0 || constant.Dimension != 0 ||
            constant.BindCount != 1 || constant.BindPoint >= 14)
            throw Unsupported("constant buffer binding");
        if (definitions.Bindings.Count != bindings.Length || bindings.Any(expected => !definitions.Bindings.Any(binding =>
            binding.Name.Text == expected.Name && binding.InputType == expected.Type && binding.Dimension == expected.Dimension &&
            (expected.Type == 0 || binding.BindPoint == expected.Slot) && binding.BindCount == 1 &&
            (expected.Type != 2 || binding.ReturnType == 5))))
            throw Unsupported("texture, sampler or constant binding");
        return new(stageCode, constant.BindPoint, (uint)globals.ByteSize, Array.AsReadOnly(ordered));
    }

    private static NotSupportedException Unsupported(string part) =>
        new($"Ocean PB2 wave {part} differs from the supported Native material ABI.");
}
