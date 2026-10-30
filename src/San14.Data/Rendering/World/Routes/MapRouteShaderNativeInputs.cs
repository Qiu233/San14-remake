using System.Globalization;
using System.Text;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Import-boundary export of the actual route shader media. This does
/// not select a current combination, prepare numeric constants or certify a draw.</summary>
public static class MapRouteShaderNativeInputs
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    public static void WriteNativeInputs(MapRouteDrawResources resources, string inputRoot)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputRoot);
        var shader = resources.Shader;
        string directory = Path.Combine(Path.GetFullPath(inputRoot), "route-program-input");
        Directory.CreateDirectory(directory);
        var programs = new List<string>();
        var constants = new List<string>();
        var textures = new List<string>();
        var inputs = new List<string>();
        var options = new JsonSerializerOptions { WriteIndented = true };
        foreach (var program in shader.Container.Programs)
        {
            var definitions = shader.ReadResourceDefinitions(program.Index);
            int stage = definitions.RawShaderType switch
            {
                0xFFFE => 1, // G1/DXBC vertex stage, not native command opcode.
                0xFFFF => 0,
                _ => throw new NotSupportedException("route.shader.stage")
            };
            string stem = "program-" + program.Index.ToString("D2", CultureInfo.InvariantCulture);
            File.WriteAllBytes(Path.Combine(directory, stem + ".dxbc"),
                shader.Container.GetProgramBytes(shader.Source.Bytes, program.Index).ToArray());
            programs.Add(FormattableString.Invariant($"{program.Index} {stage} {stem}.dxbc"));
            File.WriteAllText(Path.Combine(directory, stem + "-rdef.json"),
                JsonSerializer.Serialize(definitions, options), Utf8);
            var signatures = shader.ReadSignatures(program.Index);
            File.WriteAllText(Path.Combine(directory, stem + "-signature.json"),
                JsonSerializer.Serialize(signatures, options), Utf8);
            foreach (var binding in definitions.Bindings)
            {
                if (binding.InputType == 0)
                {
                    var buffer = definitions.Buffers.Single(b => b.Name.Text == binding.Name.Text);
                    if (binding.BindCount != 1 || buffer.ByteSize <= 0 || buffer.ByteSize % 16 != 0)
                        throw new NotSupportedException("route.shader.constant_buffer_extent");
                    foreach (var variable in buffer.Variables)
                    {
                        var type = definitions.Types.Single(t => t.Offset == variable.TypeOffset);
                        // Preserve scalar-array packing (including its holes),
                        // matrix class and USED flag. No zero CB is exported.
                        constants.Add(FormattableString.Invariant(
                            $"{program.Index} {binding.BindPoint} {buffer.ByteSize} {variable.Name.Text} {variable.StartOffset} {variable.ByteSize} {variable.Flags} {type.Class} {type.BaseType} {type.Rows} {type.Columns} {type.Elements}"));
                    }
                }
                else if (binding.InputType is 2 or 3)
                    textures.Add(FormattableString.Invariant(
                        $"{program.Index} {binding.InputType} {binding.BindPoint} {binding.BindCount} {binding.Dimension} {binding.Name.Text}"));
                else throw new NotSupportedException("route.shader.resource_kind");
            }
            if (stage == 1)
                foreach (var input in signatures.Parts.Where(p => p.Kind == DxbcSignatureKind.Input)
                    .SelectMany(p => p.Parameters))
                    inputs.Add(FormattableString.Invariant(
                        $"{program.Index} {input.SemanticName.Text} {input.SemanticIndex} {input.Register} {input.ComponentType} {input.Mask} {input.ReadWriteMask} {input.RawSystemValue}"));
        }
        var combinations = new List<string>();
        foreach (var combination in shader.ReadCombinations().Rows)
        {
            if (combination.Stages.Any(s => s.Entry is not null && s.StageCode is not (0 or 1)))
                throw new NotSupportedException("route.shader.combination_stages");
            int vertex = combination.Stages.Single(s => s.StageCode == 1 && s.Entry is not null).Entry!.ProgramIndex;
            int pixel = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null).Entry!.ProgramIndex;
            combinations.Add(FormattableString.Invariant($"{combination.Index} {vertex} {pixel}"));
        }
        File.WriteAllLines(Path.Combine(directory, "programs.txt"), programs, Utf8);
        File.WriteAllLines(Path.Combine(directory, "combinations.txt"), combinations, Utf8);
        File.WriteAllLines(Path.Combine(directory, "constant-layout.txt"), constants, Utf8);
        File.WriteAllLines(Path.Combine(directory, "resource-slots.txt"), textures, Utf8);
        File.WriteAllLines(Path.Combine(directory, "vertex-signatures.txt"), inputs, Utf8);
        // Source route records are five floats, not a generic model layout.
        // Native loadNativeModelLayout supplies absent signature semantics by
        // copying the first matched descriptor, as the original loader does.
        File.WriteAllLines(Path.Combine(directory, "layout.txt"),
            ["POSITION 0 6 0 0", "TEXCOORD 0 16 0 12"], Utf8);
        File.WriteAllText(Path.Combine(directory, "vertex-stride.txt"), "20\n", Utf8);

        // The actual loaded program name drives the constant-color branch.
        // CNS draws replace the caller's twelve feature words before matching.
        File.WriteAllText(Path.Combine(directory, "route-program-kind.txt"),
            shader.Container.NamePrefix + "\n", Utf8);
        // The route creator supplies an unskinned type17 record of all zeros
        // and three explicit affine rows. These are primitive source inputs,
        // separate from dimension2's declared [8,0,0,0] and [9,7,128,0] rows.
        var matrixMatch = G1ShaderMatrixBindingMatcher.Select(
            [0u, 0u, 0u, 0u], shader.ReadMatrixBindings());
        // The guarded source writer retains its constructor index zero on a
        // matcher miss; a nonexact in-range nearest row still commits.
        int matrixIndex = matrixMatch.RowIndex >= 0 &&
            (uint)matrixMatch.RowIndex < resources.Bindings.RawDimensionCounts[2]
            ? matrixMatch.RowIndex : 0;
        File.WriteAllLines(Path.Combine(directory, "primitive-matrix.txt"),
            ["0 0 0 0", "1 0 0 0", "0 1 0 0", "0 0 1 0",
                matrixIndex.ToString(CultureInfo.InvariantCulture)], Utf8);

        var bindings = resources.Bindings;
        File.WriteAllText(Path.Combine(directory, "dimensions.txt"),
            bindings.PassCount.ToString(CultureInfo.InvariantCulture) + " " +
            string.Join(" ", bindings.RawDimensionCounts.Select(Decimal)) + "\n", Utf8);
        File.WriteAllLines(Path.Combine(directory, "features.txt"), bindings.Features.Select(feature =>
            feature.Index.ToString(CultureInfo.InvariantCulture) + " " +
            string.Join(" ", feature.Feature.Words.Select(Decimal))), Utf8);
        foreach (var feature in bindings.Features)
            File.WriteAllBytes(Path.Combine(directory, $"feature-{feature.Index:D2}.bin"), feature.Feature.RawBytes.ToArray());
        File.WriteAllLines(Path.Combine(directory, "variants.txt"), bindings.Variants.Records.Select(variant =>
            FormattableString.Invariant($"{variant.Index} {variant.SourceOffset} {Convert.ToHexString(variant.RawBytes.Span)}")), Utf8);
        // Keep all seven source axes. Counts alone do not preserve the
        // declaration masks used by the native feature/context matcher.
        var selection = shader.ReadSelectionDimensions();
        File.WriteAllLines(Path.Combine(directory, "selection-dimensions.txt"), selection.Dimensions.Select(dimension =>
            FormattableString.Invariant($"{dimension.DimensionIndex} {dimension.RawCount} {dimension.RecordWidth} {dimension.SourceRange.Offset} dimension-{dimension.DimensionIndex}.bin")), Utf8);
        foreach (var dimension in selection.Dimensions)
        {
            using var stream = new MemoryStream();
            foreach (var record in dimension.Records) stream.Write(record.RawBytes.Span);
            File.WriteAllBytes(Path.Combine(directory, $"dimension-{dimension.DimensionIndex}.bin"), stream.ToArray());
        }
        var configurations = shader.ReadConfigurations();
        File.WriteAllLines(Path.Combine(directory, "configurations.txt"), configurations.Records.Select(configuration =>
            FormattableString.Invariant($"{configuration.Index} {configuration.NameHash} {configuration.RawSecondWord} {configuration.Parameters.Count} {configuration.Name}")), Utf8);
        File.WriteAllLines(Path.Combine(directory, "configuration-parameters.txt"), configurations.Records.SelectMany(configuration =>
            configuration.Parameters.Select(parameter => FormattableString.Invariant(
                $"{configuration.Index} {parameter.Index} {parameter.NameHash} {parameter.RawValue}"))), Utf8);
        WriteTextures(resources, directory, options);
        File.WriteAllText(Path.Combine(directory, "source.json"), JsonSerializer.Serialize(new
        {
            resources.ContextRevision,
            ProgramName = resources.ProgramReference.Object.ObjectId,
            DeclarationName = resources.ProgramDeclaration.Object.ObjectId,
            shader.Source.ResourceUri,
            shader.Source.DecodedSha256,
            Bindings = resources.Bindings,
            SelectionDimensions = selection.Dimensions,
            Configurations = configurations,
            Matrices = shader.ReadMatrixBindings(),
            VertexStride = 20,
            TextureGroupSlotMeaning = "source image ordinal; shader register and current four-image selector are selected by the draw consumer"
        }, options), Utf8);
    }

    private static void WriteTextures(MapRouteDrawResources resources, string directory, JsonSerializerOptions options)
    {
        var groups = new List<string>();
        foreach (var (name, group) in resources.TextureGroups)
        {
            string relative = "texture-groups/" + name.ToString("X8", CultureInfo.InvariantCulture);
            string destination = Path.Combine(directory, relative);
            Directory.CreateDirectory(destination);
            var images = new List<string>();
            var identities = new List<object>();
            for (int slot = 0; slot < group.Images.Count; slot++)
            {
                var source = group.Images[slot];
                var image = source.Image;
                uint view = ModelStageTextures.ReadNativeViewFormat(image);
                uint resource = ModelTextureFormatMath.Resolve(image.PixelFormat, 0).ResourceFormat;
                // This is the existing NativeModelTexture framing: original
                // encoded mip bytes, with resource and SRV formats kept apart.
                string stem = "image-" + slot.ToString("D2", CultureInfo.InvariantCulture);
                File.WriteAllBytes(Path.Combine(destination, stem + ".tex"),
                    D3D11ModelResourceWriter.EncodeTexture(source.Resource, image, view));
                var sampler = G1TextureSamplerReader.ReadRetainedSource(image);
                File.WriteAllBytes(Path.Combine(destination, stem + "-sampler.bin"), sampler.EncodeD3D11Descriptor());
                images.Add(FormattableString.Invariant($"{slot} {resource} {view} {stem}.tex {stem}-sampler.bin"));
                identities.Add(new
                {
                    SourceSlot = slot, StaticObjectNameHash = source.Definition.ObjectId,
                    source.Resource.Source.ResourceUri, source.Resource.Source.DecodedSha256,
                    source.ImageIndex, image.PixelFormat, image.Width, image.Height, image.Depth, image.MipCount,
                    ResourceFormat = resource, ViewFormat = view, Sampler = sampler
                });
            }
            File.WriteAllLines(Path.Combine(destination, "images.txt"), images, Utf8);
            File.WriteAllText(Path.Combine(destination, "source.json"), JsonSerializer.Serialize(new
            {
                GroupNameHash = name, group.TextureObjectNameHash,
                TableResourceUri = group.Table.ResourceUri, TableDecodedSha256 = group.Table.DecodedSha256,
                Images = identities
            }, options), Utf8);
            groups.Add(FormattableString.Invariant($"{name} {group.TextureObjectNameHash} {group.Images.Count} {relative}"));
        }
        File.WriteAllLines(Path.Combine(directory, "texture-groups.txt"), groups, Utf8);
    }

    private static string Decimal(uint value) => value.ToString(CultureInfo.InvariantCulture);
}
