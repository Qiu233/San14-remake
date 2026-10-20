using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GroundDetailTemplateResources
{
    // The current atlas is the declared depth4E/class5 family, as in
    // D3D11ShadowInputPreparation. This import does not choose cascade membership.
    private static void WriteShadow(GameResourceRepository repository, Dictionary<string, byte[]> media,
        string directory, MapStaticModelAsset asset, int drawIndex, NamedModelShader owner,
        LoadedShaderContainer normalShader, int normalCombination, ModelMatrixBindingInput matrixInput,
        ProceduralTemplate template, byte[] normalVertex, float[] positionRows, bool alpha,
        float shadowAlphaReference)
    {
        if (owner.Object.ClassId is not (0xAB9C4087 or 0x18EB1E56) || owner.DependencyObjectHashes.Count <= 17)
            throw new NotSupportedException("Ground shadow requires the verified full Tree2/PB2 type16 table.");
        // FB4C60 publishes own + dependencies; state3 therefore uses dependency2.
        var provider = ModelShaderResources.ReadDeclaredShader(owner.Objects, owner.DependencyObjectHashes[2]);
        bool tree = provider.Object.ClassId == 0x9A44FACE;
        if ((!tree && provider.Object.ClassId != 0x39E93192) ||
            (tree ? provider.ShaderFileHash != 0x04C6DACE : provider.ShaderFileHash is not (0x193FCD66 or 0x9DE72C62)))
            throw new NotSupportedException("Ground state3 shader has no imported creator/profile.");
        var shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
        var bindings = shader.ReadG1Bindings();
        if (bindings.PassCount != 1 || bindings.Variants.RecordWidth != (tree ? 52 : 12) ||
            Enumerable.Range(0, 7).Where(i => i is not 2 and not 6).Any(i => bindings.RawDimensionCounts[i] > 1))
            throw new NotSupportedException("Ground state3 has additional current selection dimensions.");
        // The audited fresh registry has no Instancing2 named factory. Optional
        // configurations are skipped, while full source variant tails survive.
        if (shader.ReadConfigurations().Records.Any(c => c.Name != "Instancing2" || (c.RawSecondWord & 2) != 0))
            throw new NotSupportedException("Ground shadow requires an additional parameter-module owner.");
        var normalProvider = ModelShaderResources.ReadDeclaredShader(owner.Objects, owner.DependencyObjectHashes[17]);
        var texturePlan = tree
            ? ProceduralTree2TextureSource.Resolve(asset.TextureResources, drawIndex, normalProvider, normalShader, normalCombination)
            : ModelStageTextures.Resolve(asset.TextureResources, drawIndex, normalProvider, normalShader, normalCombination, new(false, false, 0));
        var primaryRecord = ModelTextureResources.ResolveDrawTextures(asset.TextureResources, drawIndex).Binding.TextureRecord;
        var primary = DeferredEarlyShadowPixelMaterialSource.ReadCurrentPrimary(primaryRecord, asset.TextureResources.Slots);
        var primaryBinding = texturePlan.Bindings.Single(b => b.TextureRegister == 0);
        if (primaryBinding.SourceEntryIndex != primary.FirstPrimary.Index || primaryBinding.SamplerRegister != 0 || primary.Texture is null)
            throw new NotSupportedException("Ground shadow primary differs from the retained normal t0/s0 resource.");
        var image = ModelStageTextures.LoadImage(repository, texturePlan, 0);
        if (image.Image.TextureSystem != 0 || image.Image.LayerCount != 1 || primary.EffectivePrimaryUvSet != 0)
            throw new NotSupportedException("Ground shadow requires another primary texture/UV shader path.");
        // 950990 writes header low3 TextureSystem to descriptor36 bits7..9;
        // 1054140 reads that same field into variant word2. It is not RawValue6.
        byte[] first = bindings.Variants.Records[0].RawBytes.ToArray();
        byte[] requested = first.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(requested, alpha ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(requested.AsSpan(4), 5);
        BinaryPrimitives.WriteUInt32LittleEndian(requested.AsSpan(8), image.Image.TextureSystem);
        var selection = LoadedDeferredEarlyShadowRenderer.SelectShadowRequestedVariant(bindings.Variants,
            new(0, first, first.ToArray()), requested);
        if (!selection.Success) throw new InvalidDataException("Ground state3 source variant has no match.");
        var matrix = ModelMatrixBindingSourceReader.Select(shader, matrixInput);
        uint[] indices = new uint[7]; // 982518 constructor; only matrix/variant writers reached.
        indices[2] = checked((uint)matrix.Selection.RowIndex); indices[6] = selection.Next.CommittedIndex;
        var combination = bindings.ResolveCombination(indices, 0);
        int vs = combination.Stages.Single(s => s.StageCode == 1).Entry!.ProgramIndex;
        int? ps = combination.Stages.Single(s => s.StageCode == 0).Entry?.ProgramIndex;
        if (alpha != ps.HasValue) throw new InvalidDataException("Ground shadow alpha queue and actual PS selection disagree.");
        var vertexDefinition = shader.ReadResourceDefinitions(vs).Buffers.Single();
        int vertexSize = checked((int)vertexDefinition.ByteSize);
        bool localToProjection = vertexDefinition.Variables.Any(v => v.Name.Text == "mL2P");
        byte[] vertex;
        if (tree)
        {
            var normalDefinition = normalShader.ReadResourceDefinitions(0).Buffers.Single();
            if (vertexSize != 272 || normalVertex.Length != vertexSize || !vertexDefinition.Variables
                .Select(v => (v.Name.Text, v.StartOffset, v.ByteSize)).SequenceEqual(normalDefinition.Variables.Select(v => (v.Name.Text, v.StartOffset, v.ByteSize))))
                throw new InvalidDataException("Tree shadow and normal wind/local layouts differ.");
            vertex = normalVertex.ToArray();
        }
        else
        {
            if (vertexSize != (localToProjection ? 64 : 112) || template.IsInstancing == localToProjection)
                throw new InvalidDataException("Ground generic shadow matrix family differs from its instance declaration.");
            vertex = new byte[vertexSize];
            if (!localToProjection) WriteFloats(vertex, 64, positionRows);
        }
        string shadow = directory + "/shadow";
        media[shadow + "/vs.dxbc"] = shader.Container.GetProgramBytes(shader.Source.Bytes, vs).ToArray();
        media[shadow + "/vs-b0.bin"] = vertex;
        // Keep the existing importer format: name/index/format/slot/offset/class/step.
        var required = shader.ReadSignatures(vs).Parts.Single(p => p.Kind == DxbcSignatureKind.Input)
            .Parameters.Where(p => p.RawSystemValue == 0).Select(p => (p.SemanticName.Text, p.SemanticIndex)).ToArray();
        var layout = Encoding.UTF8.GetString(media[directory + "/layout.txt"]).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => { var words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                return required.Contains((words[0], uint.Parse(words[1], System.Globalization.CultureInfo.InvariantCulture))); });
        media[shadow + "/layout.txt"] = Encoding.UTF8.GetBytes(string.Join('\n', layout) + "\n");
        int pixelSize = 0;
        if (ps is int pixelProgram)
        {
            var pd = shader.ReadResourceDefinitions(pixelProgram);
            if (pd.Buffers.Count != 1 || pd.Buffers[0].ByteSize != 48 || !pd.Buffers[0].Variables
                .Select(v => (v.Name.Text, v.StartOffset, v.ByteSize)).SequenceEqual(new[] { ("sbias", 0, 16), ("nStageNum", 16, 4), ("vSAMap", 32, 16) }))
                throw new InvalidDataException("Ground shadow alpha constant layout differs.");
            byte[] pixel = new byte[48];
            DeferredEarlyShadowPixelMaterialSource.ApplyOwnedConstructorBiasFragment(pixel.AsSpan(0, 16));
            BinaryPrimitives.WriteSingleLittleEndian(pixel.AsSpan(12), shadowAlphaReference);
            WriteFloats(pixel, 16, [primary.StageCount]);
            WriteFloats(pixel, 32, [primary.StageAlphaMap.X, primary.StageAlphaMap.Y, primary.StageAlphaMap.Z, primary.StageAlphaMap.W]);
            media[shadow + "/ps.dxbc"] = shader.Container.GetProgramBytes(shader.Source.Bytes, pixelProgram).ToArray();
            media[shadow + "/ps-b0.bin"] = pixel; pixelSize = pixel.Length;
        }
        string row = FormattableString.Invariant($"{directory} {template.Index} {drawIndex} {(tree ? 1 : 0)} {(alpha ? 1 : 0)} {vertexSize} {pixelSize} {(localToProjection ? 1 : 0)} {shadow}\n");
        media.TryGetValue("shadow-draws.txt", out var previous);
        media["shadow-draws.txt"] = Encoding.UTF8.GetBytes((previous is null ? "" : Encoding.UTF8.GetString(previous)) + row);
    }
}
