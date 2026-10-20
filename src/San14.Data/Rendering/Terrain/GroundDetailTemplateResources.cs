using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record GroundDetailTemplateMetadata(int Index, uint NameHash,
    ProceduralTemplateBounds Bounds, IReadOnlyList<IReadOnlyList<int>> LodDraws,
    IReadOnlyList<uint> ProviderClasses, ProceduralTemplateWorldFields World);

/// <summary>Scene-owned procedural model templates. Population, current camera
/// and wind updates remain independent consumers of these imported resources.</summary>
public sealed partial class GroundDetailTemplateResources
{
    private readonly Dictionary<string, byte[]> files;
    private readonly IReadOnlyList<string> modelDirectories, producerRows;

    private GroundDetailTemplateResources(Dictionary<string, byte[]> media,
        GroundDetailTemplateMetadata[] metadata, List<string> directories, List<string> producers)
    {
        files = media;
        Metadata = Array.AsReadOnly(metadata);
        modelDirectories = directories.AsReadOnly();
        producerRows = producers.AsReadOnly();
    }

    public IReadOnlyList<GroundDetailTemplateMetadata> Metadata { get; }

    public static GroundDetailTemplateResources Load(GameResourceRepository repository,
        LoadedSceneResources scene, ProceduralPlacementDeclaration declaration, bool reversedDepth)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(declaration);
        if (!scene.Dependencies.Any(d => ReferenceEquals(d.Resource, declaration.OwnerSource)) ||
            !scene.Dependencies.Any(d => ReferenceEquals(d.Resource, declaration.Source)))
            throw new InvalidDataException("Ground templates must use this scene's procedural declaration.");
        var templateScene = scene with { Instances = declaration.Templates.Select(t => t.Declaration).ToArray() };
        var assets = new MapStaticModelAssetCatalog(repository, templateScene);
        var shaderObjects = repository.LoadObjects("system", 0x718CCB8B);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var queue54 = ModelRendererStateSourceReader.ReadQueue(graph, 54);
        var queue55 = ModelRendererStateSourceReader.ReadQueue(graph, 55);
        var media = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var writer = new D3D11ModelResourceWriter(repository, media);
        var parts = new Dictionary<uint, PartsRow[]>();
        var metadata = new GroundDetailTemplateMetadata[declaration.Templates.Count];
        var directories = new List<string>();
        var producers = new List<string>();
        foreach (var template in declaration.Templates)
        {
            if (template.Index < 0 || template.Index >= metadata.Length || metadata[template.Index] is not null)
                throw new InvalidDataException("Ground template directory requires unique physical slots.");
            var asset = assets.LoadSceneInstance(template.Index);
            int shadowAlphaOverride = ModelShadowAlphaSource.ReadLoaderOverride(asset.TextureResources.Model.Source);
            var lods = ReadLods(repository, asset, parts);
            var bounds = ProceduralTemplateBounds.FreshScaleOne(template.Index,
                MapModelNativeBoundsSources.PrepareSourceAsset(asset));
            var worldFields = MapProceduralWorldSubmissionSource.ReadTemplate(template);
            var bindings = ModelShaderResources.LoadBindings(repository, asset.TextureResources);
            var locals = MapStaticModelPrimitiveLocalSource.Prepare(asset);
            var world = UnitWorldTransform.ConstructDefinition(asset.TextureResources);
            var providers = new uint[asset.Draws.Count];
            string modelDirectory = $"template-{template.Index}-{template.NameHash:X8}";
            foreach (var draw in asset.Draws)
            {
                if ((uint)draw.Index >= providers.Length)
                    throw new InvalidDataException("Ground model draw physical index differs.");
                var owner = ModelShaderResources.ResolveDrawBaseSource(bindings, draw.Index, shaderObjects).Binding.Shader;
                var selected = owner.Object.ClassId == 0xAB9C4087
                    ? new ModelShaderInterfaceSource(owner, 18, 17,
                        ModelShaderResources.ReadDeclaredShader(shaderObjects, owner.DependencyObjectHashes[17]))
                    : ModelShaderResources.ResolveType16Interface(owner, 18);
                var provider = selected.Provider ?? throw new InvalidDataException("Ground normal renderer is null.");
                bool tree = provider.Object.ClassId == 0xAB9C4087;
                if (!tree && provider.Object.ClassId != 0x18EB1E56)
                    throw new NotSupportedException("Ground template renderer is outside normal PB2/Tree2.");
                providers[draw.Index] = provider.Object.ClassId!.Value;
                var shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
                var matrixInput = ModelMatrixBindingSourceReader.PrepareUnboundGroup(bindings, draw.Index)
                    ?? throw new NotSupportedException("Ground template requires its unbound matrix group.");
                var matrix = ModelMatrixBindingSourceReader.Select(shader, matrixInput);
                G2ShaderCombination combination;
                if (tree)
                    combination = matrix.ResolveCombination(new uint[6], 0);
                else
                {
                    var selection = ModelShaderSelectionSourceReader.Read(provider, shader);
                    combination = ModelShaderSelectionSourceReader.ResolveSingleOtherDimensionsInitialized(selection,
                        ModelShaderSelectionSourceReader.ReadInitialCache(selection), matrix, 0).Combination;
                }
                bool alpha = shader.ReadConfigurations().Records.Any(c => c.Name is "AlphaTest" or "Dissolve");
                var queue = alpha ? queue55 : queue54;
                int vs = combination.Stages.Single(s => s.StageCode == 1).Entry!.ProgramIndex;
                int ps = combination.Stages.Single(s => s.StageCode == 0).Entry!.ProgramIndex;
                var vd = shader.ReadResourceDefinitions(vs);
                var pd = shader.ReadResourceDefinitions(ps);
                var material = ModelGbufferMaterialSourceReader.Read(shader,
                    ModelShaderResources.ResolveDrawMaterials(bindings, draw.Index));
                var specular = ModelGbufferMaterialSourceReader.PrepareSpecular(material, 0);
                if (!specular.WritesValue || specular.Value is null || material.MaterialId is null)
                    throw new NotSupportedException("Ground material requires actual specular and material ID values.");
                var local = locals[draw.Index];
                var transform = local.Rows is { } rows
                    ? world.ShaderTransformWithPrimitiveLocalRows(rows, local.EvidenceUri, local.EvidenceSha256)
                    : world.ShaderTransform();
                // Native update writes W2P, WV and current Tree2 wind before any
                // submitted draw. These lanes are storage seeds, not captured frames.
                byte[] vertex;
                if (tree)
                {
                    if (vd.Buffers.Count != 1 || vd.Buffers[0].ByteSize != 272)
                        throw new InvalidDataException("Ground Tree2 vertex constants require 272 bytes.");
                    vertex = new byte[272];
                    WriteFloats(vertex, 64, transform.NormalRows);
                    WriteFloats(vertex, 112, transform.PositionRows);
                    ProceduralTree2MaterialSource.WriteStatic(vertex, ProceduralTree2MaterialSource.Read(material.Draw));
                }
                else vertex = ModelGbufferConstants.Vertex(vd, new byte[160],
                    new(new float[16], transform.NormalRows, transform.PositionRows)).Bytes;
                float? alphaReference = pd.Buffers.Single().Variables.Any(v => v.Name.Text == "vATest")
                    ? ModelGbufferMaterialSourceReader.PrepareAlphaTest(material.SourceAlphaReference, queue.BackendAlphaEnabled) : null;
                byte[] pixel = ModelGbufferConstants.Pixel(pd, new byte[80],
                    new(specular.Value.Value, new float[12], material.MaterialId.Value, alphaReference)).Bytes;
                var geometry = asset.RawTransforms.Geometry;
                var layout = geometry.Layouts[draw.LayoutIndex];
                bool instanceSlot = layout.BufferReferences.Count == 2 && geometry.VertexBuffers[layout.BufferReferences[1]].Stride == 48 &&
                    geometry.VertexBuffers[layout.BufferReferences[1]].VertexCount == 1;
                if (template.IsInstancing && !instanceSlot)
                    throw new NotSupportedException("Ground template requires its actual 48-byte instance stream.");
                uint group = matrixInput.UnboundGroupSource!.Flags;
                if (((group >> 1) & 15) != 0)
                    throw new NotSupportedException("Ground Tree2 graphics currently supports wind selector zero.");
                string directory = $"{modelDirectory}/draw-{draw.Index}";
                writer.WriteGeometryAndPrograms(directory, modelDirectory, geometry, draw, shader, vs, ps,
                    vertex, pixel, bindings, queue, reversedDepth,
                    template.IsInstancing ? new HashSet<int> { layout.BufferReferences[1] } : null);
                WriteTextures(repository, writer, media, directory, asset, draw.Index, provider, shader, combination.Index, tree);
                WriteShadow(repository, media, directory, asset, draw.Index, owner, shader,
                    combination.Index, matrixInput, template, vertex, transform.PositionRows, alpha,
                    ModelShadowAlphaSource.PrepareReference(material.SourceAlphaReference, shadowAlphaOverride));
                directories.Add(directory);
                producers.Add(FormattableString.Invariant($"{directory} {template.Index} {draw.Index} {queue.Node.Index} {group:X8}"));
            }
            metadata[template.Index] = new(template.Index, template.NameHash, bounds, lods,
                Array.AsReadOnly(providers), worldFields);
        }
        if (metadata.Any(t => t is null)) throw new InvalidDataException("Ground template physical slots are incomplete.");
        return new(media, metadata, directories, producers);
    }

    private static void WriteFloats(byte[] bytes, int offset, float[] values)
    {
        foreach (float value in values)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset), value);
            offset += 4;
        }
    }
}
