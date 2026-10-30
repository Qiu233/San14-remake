using System.Buffers.Binary;
using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record Tree2ShadowTemplate(byte[] Vertex, int PixelProgram,
    byte[]? Pixel, byte[]? PixelConstants, string[] Layout);

/// <summary>One actual noninstanced Root Tree2 state3. Selection and alpha
/// material preparation reuse the same source consumers as procedural Tree2;
/// current wind remains with the shared graphics owner.</summary>
internal static class Tree2ShadowTemplateSource
{
    internal static Tree2ShadowTemplate Prepare(GameResourceRepository repository,
        MapStaticModelAsset asset, int drawIndex, NamedModelShader actualOwner,
        ModelMatrixBindingInput actualMatrix, IReadOnlyList<string> actualLayout)
    {
        if (actualOwner.Object.ClassId != 0xAB9C4087 || actualOwner.DependencyObjectHashes.Count <= 17)
            throw new InvalidDataException("Root Tree2 shadow requires its complete actual shader dependency table.");
        var provider = ModelShaderResources.ReadDeclaredShader(actualOwner.Objects,
            actualOwner.DependencyObjectHashes[2]);
        if (provider.Object.ClassId != 0x9A44FACE)
            throw new NotSupportedException("Root Tree2 state3 has an unsupported actual creator.");
        var shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
        var tables = shader.ReadG1Bindings();
        if (tables.PassCount != 1 || tables.Variants.RecordWidth != 52 ||
            Enumerable.Range(0, 7).Where(i => i is not 2 and not 6).Any(i => tables.RawDimensionCounts[i] > 1))
            throw new NotSupportedException("Root Tree2 shadow has additional current selection dimensions.");
        if (shader.ReadConfigurations().Records.Any(c => c.Name != "Instancing2" || (c.RawSecondWord & 2) != 0))
            throw new NotSupportedException("Root Tree2 shadow requires another active parameter module.");
        var normalProvider = ModelShaderResources.ReadDeclaredShader(actualOwner.Objects,
            actualOwner.DependencyObjectHashes[17]);
        var normal = ModelShaderResources.LoadDeclaredG1S(repository, normalProvider);
        var normalMatrix = ModelMatrixBindingSourceReader.Select(normal, actualMatrix);
        var normalCombination = normalMatrix.ResolveCombination(new uint[6], 0);
        var textures = ProceduralTree2TextureSource.Resolve(asset.TextureResources, drawIndex,
            normalProvider, normal, normalCombination.Index);
        var primaryRecord = ModelTextureResources.ResolveDrawTextures(asset.TextureResources, drawIndex).Binding.TextureRecord;
        var primary = DeferredEarlyShadowPixelMaterialSource.ReadCurrentPrimary(primaryRecord, asset.TextureResources.Slots);
        var primaryBinding = textures.Bindings.Single(value => value.TextureRegister == 0);
        if (primaryBinding.SourceEntryIndex != primary.FirstPrimary.Index || primaryBinding.SamplerRegister != 0 ||
            primary.Texture is null || primary.EffectivePrimaryUvSet != 0)
            throw new NotSupportedException("Root Tree2 shadow requires its actual normal primary image and UV0.");
        var image = ModelStageTextures.LoadImage(repository, textures, 0);
        if (image.Image.TextureSystem != 0 || image.Image.LayerCount != 1)
            throw new NotSupportedException("Root Tree2 shadow primary requires another texture system.");
        bool alpha = normal.ReadConfigurations().Records.Any(c => c.Name is "AlphaTest" or "Dissolve");
        byte[] first = tables.Variants.Records[0].RawBytes.ToArray();
        byte[] requested = first.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(requested, alpha ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(requested.AsSpan(4), 5u);
        BinaryPrimitives.WriteUInt32LittleEndian(requested.AsSpan(8), image.Image.TextureSystem);
        var selection = LoadedDeferredEarlyShadowRenderer.SelectShadowRequestedVariant(tables.Variants,
            new(0, first, first.ToArray()), requested);
        if (!selection.Success) throw new InvalidDataException("Root Tree2 shadow source variant has no match.");
        var matrix = ModelMatrixBindingSourceReader.Select(shader, actualMatrix);
        uint[] indices = new uint[7];
        indices[2] = checked((uint)matrix.Selection.RowIndex);
        indices[6] = selection.Next.CommittedIndex;
        var combination = tables.ResolveCombination(indices, 0);
        int vs = combination.Stages.Single(s => s.StageCode == 1).Entry!.ProgramIndex;
        int? ps = combination.Stages.Single(s => s.StageCode == 0).Entry?.ProgramIndex;
        if (alpha != ps.HasValue) throw new InvalidDataException("Root Tree2 shadow alpha and actual PS disagree.");
        var vertex = shader.ReadResourceDefinitions(vs);
        Tree2ModelVertexSource.RequireLayout(vertex);
        int normalVs = normalCombination.Stages.Single(s => s.StageCode == 1).Entry!.ProgramIndex;
        Tree2ModelVertexSource.RequireLayout(normal.ReadResourceDefinitions(normalVs));
        var required = shader.ReadSignatures(vs).Parts.Single(part => part.Kind == DxbcSignatureKind.Input)
            .Parameters.Where(parameter => parameter.RawSystemValue == 0)
            .Select(parameter => (parameter.SemanticName.Text, parameter.SemanticIndex)).ToArray();
        string[] layout = actualLayout.Where(line => {
            string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return words.Length >= 5 && required.Contains((words[0], uint.Parse(words[1], CultureInfo.InvariantCulture)));
        }).ToArray();
        if (layout.Length != required.Length) throw new InvalidDataException("Root Tree2 shadow geometry does not supply its selected VS inputs.");
        byte[]? pixel = null;
        byte[]? constants = null;
        if (ps is int selectedPixel)
        {
            var definition = shader.ReadResourceDefinitions(selectedPixel);
            if (definition.Buffers.Count != 1 || definition.Buffers[0].ByteSize != 48 ||
                !definition.Buffers[0].Variables.Select(v => (v.Name.Text, v.StartOffset, v.ByteSize))
                    .SequenceEqual(new[] { ("sbias", 0, 16), ("nStageNum", 16, 4), ("vSAMap", 32, 16) }))
                throw new InvalidDataException("Root Tree2 selected depth-class5 alpha layout differs.");
            constants = new byte[48];
            DeferredEarlyShadowPixelMaterialSource.ApplyOwnedConstructorBiasFragment(constants.AsSpan(0, 16));
            var bindings = ModelShaderResources.LoadBindings(repository, asset.TextureResources);
            var material = ModelGbufferMaterialSourceReader.Read(normal,
                ModelShaderResources.ResolveDrawMaterials(bindings, drawIndex));
            float reference = ModelShadowAlphaSource.PrepareReference(material.SourceAlphaReference,
                ModelShadowAlphaSource.ReadLoaderOverride(asset.TextureResources.Model.Source));
            BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(12), reference);
            BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(16), primary.StageCount);
            float[] map = [primary.StageAlphaMap.X, primary.StageAlphaMap.Y, primary.StageAlphaMap.Z, primary.StageAlphaMap.W];
            for (int i = 0; i < map.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(32 + i * 4), map[i]);
            pixel = shader.Container.GetProgramBytes(shader.Source.Bytes, selectedPixel).ToArray();
        }
        return new(shader.Container.GetProgramBytes(shader.Source.Bytes, vs).ToArray(), ps ?? -1,
            pixel, constants, layout);
    }
}
