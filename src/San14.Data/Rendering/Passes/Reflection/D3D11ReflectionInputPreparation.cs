using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Original native RefProbe programs and initial fresh static state12
/// material packets for one actual Model bank. Current capture membership,
/// order, pose, clipping, scope Eye and probe geometry remain native inputs.</summary>
public sealed class D3D11ReflectionInputPreparation
{
    private sealed record PreparedPacket(int Status, string Directory);
    public const string NativeDirectory = "reflection-input";
    private readonly IReadOnlyDictionary<string, byte[]> files;

    private D3D11ReflectionInputPreparation(LoadedReflectionProducerPlan source,
        Dictionary<string, byte[]> preparedFiles, int packetCount, int nullCount, int unsupportedCount)
        => (Source, files, PacketCount, NullPacketCount, UnsupportedPacketCount) =
            (source, preparedFiles, packetCount, nullCount, unsupportedCount);

    public LoadedReflectionProducerPlan Source { get; }
    public int PacketCount { get; }
    public int NullPacketCount { get; }
    public int UnsupportedPacketCount { get; }

    public static D3D11ReflectionInputPreparation Prepare(GameResourceRepository repository,
        D3D11ModelInputPreparation actualFieldModels, ScenePlacements actualRootPlacements,
        MapCameraConstructionPacket actualMainCamera)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualFieldModels);
        ArgumentNullException.ThrowIfNull(actualRootPlacements); ArgumentNullException.ThrowIfNull(actualMainCamera);
        if (!ReferenceEquals(actualFieldModels.Scene, actualRootPlacements.Scene) ||
            !ReferenceEquals(actualFieldModels.SourceAssets.Scene, actualFieldModels.Scene))
            throw new InvalidDataException("Capture preparation requires the actual same-generation root placements and Model bank.");
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var source = ReflectionProducerPlan.Read(graph, repository.LoadObjects("system", 0x718CCB8B),
            repository.LoadObjects("system", 0x93FB3280),
            repository.LoadShaderContainer("system", ReflectionProducerPlan.ShaderFileId));
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var pass in source.Passes)
            foreach (var stage in pass.Stages)
            {
                string name = stage.StageCode switch { 0 => "ps", 1 => "vs", 5 => "cs",
                    _ => throw new NotSupportedException("RefProbe contains an unsupported native shader stage.") };
                Add($"programs/pass-{pass.PassIndex}/{name}.dxbc",
                    source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes, stage.ProgramIndex).ToArray());
            }
        var samplers = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, actualMainCamera.ExecutableLocale);
        Sampler("cube", RenderSamplerSourceAdapters.CubeTextureKey(samplers, 1));
        Sampler("octmap", RenderSamplerSourceAdapters.RenderTextureKey(samplers, 1));
        Sampler("radiance-single", RenderSamplerSourceAdapters.RenderTextureKey(samplers, 1));
        Sampler("radiance-mipped", RenderSamplerSourceAdapters.RenderTextureKey(samplers, 2));
        Sampler("shadow", RenderSamplerSourceAdapters.RenderTextureKey(samplers, 1));

        float ambientRate = ReadRetainedMainAmbientRate(actualMainCamera);
        var placements = actualRootPlacements.Instances.ToDictionary(value => value.Source.Index);
        var models = actualFieldModels.SourceModelMappings.ToDictionary(value => value.SceneInstanceIndex);
        var templates = actualFieldModels.SourceShadowTemplates.ToDictionary(value => value.TemplateIndex);
        var prepared = new Dictionary<int, PreparedPacket>();
        var templateSources = new Dictionary<int, (string Asset, int Draw)>();
        var packetRows = new List<string>(); var imports = new List<object>();
        var writer = new D3D11ModelResourceWriter(repository, files);
        int presentCount = 0, nullCount = 0, unsupportedCount = 0;
        foreach (var primitive in actualFieldModels.SourcePrimitiveMappings)
        {
            if (!models.TryGetValue(primitive.SceneInstanceIndex, out var model) ||
                !placements.TryGetValue(primitive.SceneInstanceIndex, out var placement) || placement.Model is null ||
                !templates.TryGetValue(primitive.TemplateIndex, out var template))
                throw new InvalidDataException("Capture primitive has no actual same-bank model, placement or shader template.");
            var asset = actualFieldModels.SourceAssets.LoadSceneInstance(placement.Source);
            var assetDraw = (asset.SourceKey, primitive.DrawIndex);
            if (templateSources.TryGetValue(primitive.TemplateIndex, out var existing) && existing != assetDraw)
                throw new InvalidDataException("Capture template aliases distinct source geometry/draw identities.");
            templateSources[primitive.TemplateIndex] = assetDraw;
            if (!prepared.TryGetValue(primitive.TemplateIndex, out var packet))
            {
                bool tree=template.FinalTable.Object.ClassId==0xAB9C4087;
                var selected = tree ? ReflectionTree2CaptureSource.ReadInterface(template.FinalTable)
                    : ModelShaderResources.ResolveType16Interface(template.FinalTable, 12);
                if (selected.Provider is not { } provider)
                {
                    packet = new(0, "-"); // Actual final type16 state12 null skip.
                }
                else
                {
                    try
                    {
                        if (provider.Object.ClassId != (tree?0xAB9C4087u:0x18EB1E56u))
                            throw new NotSupportedException("Capture static material requires its actual PB2 provider owner.");
                        var shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
                        var matrix = ModelMatrixBindingSourceReader.Select(shader, template.Matrix);
                        var selection = tree ? ReflectionTree2CaptureSource.ReadSelection(provider,shader)
                            : ModelShaderSelectionSourceReader.Read(provider, shader);
                        if (selection.Dimensions.Where(value => value.DimensionIndex is not (0 or 2)).Any(value => value.RawCount > 1))
                            throw new NotSupportedException("Capture renderer requires a separate current shader-dimension owner.");
                        var initial = ModelShaderSelectionSourceReader.ReadInitialCache(selection);
                        // EC7100's actual zeroed48-byte request reaches the feature
                        // matcher. It is not an assumed row0 or exact-match gate.
                        var feature = ModelShaderSelectionSourceReader.SelectFeature(selection, initial.Indices[0], new uint[12]);
                        uint[] indices = initial.Indices.ToArray(); indices[0] = feature.Write.NextIndex;
                        if (matrix.Selection.RowIndex < 0) throw new InvalidDataException("Capture source type17 row is absent.");
                        indices[2] = checked((uint)matrix.Selection.RowIndex);
                        var combination = shader.ReadG1Bindings().ResolveCombination(indices, 0);
                        if (combination.Stages.Any(value => value.Entry is not null && value.StageCode is not (0 or 1)))
                            throw new NotSupportedException("Capture static packet requires its actual rigid VS/PS pair.");
                        int vs = combination.Stages.Single(value => value.StageCode == 1 && value.Entry is not null).Entry!.ProgramIndex;
                        int ps = combination.Stages.Single(value => value.StageCode == 0 && value.Entry is not null).Entry!.ProgramIndex;
                        // This backend binds the actual native null SRV. The
                        // PS2-only replacement-Cube gate belongs to Godot.
                        if(tree)RequireFourOutputs(shader,ps);else RequireRigidAbi(shader, vs, ps);
                        var bindings = ModelShaderResources.LoadBindings(repository, asset.TextureResources);
                        var material = ModelGbufferMaterialSourceReader.Read(shader,
                            ModelShaderResources.ResolveDrawMaterials(bindings, primitive.DrawIndex));
                        var specular = ModelGbufferMaterialSourceReader.PrepareSpecular(material, 0);
                        if (!specular.WritesValue || specular.Value is not { } specularValue)
                            throw new NotSupportedException("Capture material requires its actual retained specular endpoint.");
                        var pixel = RetainedPixel(shader.ReadResourceDefinitions(ps), specularValue,
                            material.SourceAlphaReference, ambientRate);
                        string directory = $"packets/template-{primitive.TemplateIndex}";
                        // Rigid256 and Tree2368 share matrices/clip0..255.
                        // Tree2 additionally retains its full112-byte material/
                        // wind tail for the actual shared current wind producer.
                        // This unsubmitted seed supplies no fabricated View.
                        var treePacket=tree?ReflectionTree2CaptureSource.Prepare(repository,asset,primitive.DrawIndex,
                            template.FinalTable,shader,vs,ps,combination.Index,template.Matrix,material):null;
                        byte[] vertex = treePacket?.Vertex ?? new byte[256];
                        var draw = asset.Draws[primitive.DrawIndex];
                        var layout = asset.RawTransforms.Geometry.Layouts[draw.LayoutIndex];
                        foreach (var input in shader.ReadSignatures(vs).Parts.Single(value => value.Kind == DxbcSignatureKind.Input)
                            .Parameters.Where(value => value.RawSystemValue == 0))
                            if (layout.Attributes.Count(value => ModelVertexInputs.ReadSemantic(value.SemanticCode).Name == input.SemanticName.Text &&
                                ModelVertexInputs.ReadSemantic(value.SemanticCode).Index == input.SemanticIndex) != 1)
                                throw new NotSupportedException("Capture source geometry does not supply the selected VS input.");
                        // EC7100 enables depth test/write and compare3 in its own
                        // scope. The native backend adds1 to source compare3.
                        var depth = new ModelQueueDepthState(0x1B, true, true, 3, false);
                        writer.WriteGeometryAndPrograms(directory, $"geometry/template-{primitive.TemplateIndex}",
                            asset.RawTransforms.Geometry, draw, shader, vs, ps, vertex, pixel, bindings, depth);
                        var texturePlan = treePacket?.Textures ?? ModelStageTextures.ResolveNativeReflectionCapture(asset.TextureResources,
                            primitive.DrawIndex, provider, shader, indices, 0, new(false, false, 0));
                        if(treePacket is not null)AddText(directory+"/capture-profile.txt",
                            FormattableString.Invariant($"1 1 {treePacket.GroupFlags}\n"));
                        WriteTextures(directory, texturePlan);
                        imports.Add(new { primitive.TemplateIndex, primitive.DrawIndex, source = asset.SourceKey,
                            finalTableObjectId = template.FinalTable.Object.ObjectId, providerObjectId = provider.Object.ObjectId,
                            shader = shader.Source.ResourceUri, shaderVersion = shader.Source.DecodedSha256,
                            combination = combination.Index, vs, ps, feature = feature.Write.NextIndex,
                            type17Row = matrix.Selection.RowIndex, tree, vertexBytes=vertex.Length, specularFlags = specular.NextCachedFlags,
                            ambientRate, retainedAmbient = "fresh-shared-GBuffer-J30-diffuse1-specular1;fresh-P26-native-null" });
                        packet = new(1, directory);
                    }
                    catch (NotSupportedException error)
                    {
                        string boundary = $"boundaries/template-{primitive.TemplateIndex}.txt";
                        AddText(boundary, "reflection.capture.static.nonnull_unsupported: " + error.Message + "\n");
                        packet = new(2, boundary);
                        imports.Add(new { primitive.TemplateIndex, primitive.DrawIndex, source = asset.SourceKey,
                            finalTableObjectId = template.FinalTable.Object.ObjectId, providerObjectId = provider.Object.ObjectId,
                            unsupported = error.Message });
                    }
                }
                prepared.Add(primitive.TemplateIndex, packet);
            }
            if (packet.Status == 0) nullCount++; else if (packet.Status == 1) presentCount++; else unsupportedCount++;
            packetRows.Add(FormattableString.Invariant($"0 {model.ModelOrdinal} {primitive.DrawIndex} {primitive.TemplateIndex} {primitive.TableOwner} {packet.Status} {packet.Directory}"));
        }
        AddText("packets.txt", packetRows.Count == 0 ? string.Empty : string.Join('\n', packetRows) + "\n");
        AddText("source.json", JsonSerializer.Serialize(new {
            scope = "selected normal RefProbe programs and actual accepted fresh static Model bank; current membership/order/face fields belong to native Scene",
            source = source.Shader.Source.ResourceUri, sourceVersion = source.Shader.Source.DecodedSha256,
            presentCount, nullCount, unsupportedCount, imports,
            runtimeWrites = "VS matrices0..159+clip160..255;Tree2 shared-wind tail256..367;PS litDir0,litCol16,ambLit32,vEye48,vProbePosition96,vDistanceConv112 before every actual draw",
            nativeNullP26 = "genuine null SRV; no substitute environment Cube or inferred sampler",
            samplerSource = "EC9960 oneMip cube;F37980 depth oneMip;FD95A0 atlas oneMip/radiance actual mip count;BD4820 binds retained wrapper key without sampler modifier"
        }, new JsonSerializerOptions { WriteIndented = true }));
        return new(source, files, presentCount, nullCount, unsupportedCount);

        void Add(string path, byte[] data) => files[path.Replace('\\', '/')] = data.ToArray();
        void AddText(string path, string text) => Add(path, Encoding.UTF8.GetBytes(text));
        void Sampler(string role, byte[] key) => Add($"programs/samplers/{role}.bin",
            RenderSamplerSourceAdapters.DecodeKey(samplers, key).EncodeD3D11Descriptor());
        void WriteTextures(string directory, ModelStageTexturePlan plan)
        {
            var textures = new List<string>(); var samplerRows = new List<string>();
            foreach (var binding in plan.Bindings)
            {
                var image = ModelStageTextures.LoadImage(repository, plan, binding.TextureRegister);
                uint view = ModelStageTextures.ReadNativeViewFormat(image.Image);
                var formats = ModelTextureFormatMath.Resolve(image.Image.PixelFormat,
                    view == ModelTextureFormatMath.Resolve(image.Image.PixelFormat, 0).ShaderViewFormat ? (byte)0 : (byte)1);
                string texture = $"textures/{image.Container.Source.FileId:X8}-{image.Image.Index}-{view}.tex";
                if (!files.ContainsKey(texture)) Add(texture, D3D11ModelResourceWriter.EncodeTexture(image.Container, image.Image, view));
                textures.Add(FormattableString.Invariant($"{binding.TextureRegister} {formats.ResourceFormat} {view} {Path.GetRelativePath(directory, texture).Replace('\\', '/')}"));
                string sampler = $"ps-s{binding.SamplerRegister}.bin";
                Add(directory + "/" + sampler, image.Sampler.EncodeD3D11Descriptor());
                samplerRows.Add(FormattableString.Invariant($"{binding.SamplerRegister} {sampler}"));
            }
            AddText(directory + "/textures-native.txt", string.Join('\n', textures) + "\n");
            AddText(directory + "/samplers-native.txt", string.Join('\n', samplerRows) + "\n");
        }
    }

    public void WriteNativeInputs(string packageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        string root = Path.Combine(packageRoot, NativeDirectory);
        foreach (var file in files)
        {
            string path = Path.Combine(root, file.Key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, file.Value);
        }
    }

    private static float ReadRetainedMainAmbientRate(MapCameraConstructionPacket camera)
    {
        var canonical = MapCameraConstructionSource.ReadPerspective(camera.Source.Resource, MainMapCameraAdapter.MainCameraObjectId);
        if (camera.Source.Camera.ObjectId != canonical.Camera.ObjectId || camera.ExecutableLocale is not ("SC" or "TC"))
            throw new InvalidDataException("Capture startup requires its actual selected source Main Camera.");
        var property = canonical.Camera.Properties[977];
        if (property.Index != 977 || property.NameHash != 0x08367252 || property.TypeCode != 8 ||
            property.ElementCount != 1 || property.ElementSize != 4 || property.RawValue.Length != 4)
            throw new InvalidDataException("Capture Main ambient DO rate differs from its actual typed loader binding.");
        float rate = BinaryPrimitives.ReadSingleLittleEndian(property.RawValue);
        if (!float.IsFinite(rate)) throw new InvalidDataException("Capture Main ambient DO rate is nonfinite.");
        return rate;
    }

    private static void RequireRigidAbi(LoadedShaderContainer shader, int vs, int ps)
    {
        var vertex = shader.ReadResourceDefinitions(vs);
        var buffer = vertex.Buffers.Single(value => value.Name.Text == "$Globals");
        (string Name, int Offset, int Bytes)[] required = [("mW2P",0,64),("mL2WT",64,48),("mL2W",112,48),("ksgClipPlane",160,96)];
        if (buffer.ByteSize != 256 || buffer.Variables.Count != required.Length ||
            vertex.Bindings is not [{ InputType: 0, BindPoint: 0, BindCount: 1 }] ||
            required.Any(expected => buffer.Variables.Count(value => value.Name.Text == expected.Name &&
                value.StartOffset == expected.Offset && value.ByteSize == expected.Bytes) != 1))
            throw new NotSupportedException("Capture static VS requires the exact rigid256 source ABI.");
        RequireFourOutputs(shader,ps);
    }

    private static void RequireFourOutputs(LoadedShaderContainer shader,int ps)
    {
        var outputs = shader.ReadSignatures(ps).Parts.Single(value => value.Kind == DxbcSignatureKind.Output).Parameters;
        if (!outputs.Select(value => value.Register).Order().SequenceEqual(new uint[] { 0, 1, 2, 3 }) ||
            outputs.Any(value => value.SemanticName.Text != "SV_Target" || value.ComponentType != 3 || value.Mask != 15))
            throw new NotSupportedException("Capture PS requires its exact four RGBA source outputs.");
    }

    private static byte[] RetainedPixel(DxbcResourceDefinitions definitions, Vector4 specular,
        byte alphaReference, float ambientRate)
    {
        var buffer = definitions.Buffers.Single(value => value.Name.Text == "$Globals");
        (string Name, int Offset, int Bytes)[] fields = [("litDir",0,16),("litCol",16,16),("ambLit",32,16),("vEye",48,16),
            ("cSpec",64,16),("vProbePosition",96,16),("vDistanceConv",112,16),("fAmbMapsDORate",128,4),
            ("vAmbDifScl",144,16),("vAmbSpcScl",160,16)];
        bool alpha = buffer.Variables.Any(value => value.Name.Text == "vATest");
        if (buffer.ByteSize != 176 || buffer.Variables.Count != fields.Length + (alpha ? 1 : 0) ||
            fields.Any(expected => buffer.Variables.Count(value => value.Name.Text == expected.Name &&
                value.StartOffset == expected.Offset && value.ByteSize == expected.Bytes) != 1) ||
            alpha && buffer.Variables.Count(value => value.Name.Text == "vATest" && value.StartOffset == 80 && value.ByteSize == 4) != 1)
            throw new NotSupportedException("Capture PS requires its exact native176 retained/current module ABI.");
        var bytes = new byte[176];
        Vector(64, specular);
        if (alpha) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(80),
            ModelGbufferMaterialSourceReader.PrepareAlphaTest(alphaReference, true));
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(128), ambientRate);
        // Same fresh class18 J30 owner as the existing capture packet source.
        var ambient = ReflectionCaptureRendererAmbientState.CreateFreshClass18().CaptureRetainedScales();
        Vector(144, ambient.Diffuse); Vector(160, ambient.Specular);
        return bytes;

        void Vector(int offset, Vector4 value)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset), value.X);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4), value.Y);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8), value.Z);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 12), value.W);
        }
    }
}
