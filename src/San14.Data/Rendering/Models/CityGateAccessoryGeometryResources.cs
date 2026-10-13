using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CityGateAccessoryDrawResources(int DrawIndex, int PaletteIndex, int MaterialManager,
    NamedModelShader BaseTable, NamedModelShader Provider, LoadedShaderContainer Shader,
    int Combination, int VertexProgram, int PixelProgram, ModelGbufferMaterialSource Material);

/// <summary>Shared category-2 geometry, base materials and animation resources.
/// This owner contains no Army actors, current accessory population, generated
/// flag texture or assertion that a requested group has completed creation.</summary>
public sealed class CityGateAccessoryGeometryResources
{
    readonly Dictionary<string, byte[]> files;
    public string Directory { get; }
    public LoadedArmyCatalog Catalog { get; }
    public LoadedModelMaterials Model { get; }
    public LoadedModelTransforms Transforms => Animation.Model;
    public LoadedModelShaderBindings ShaderBindings { get; }
    public ArmyContainerMaterialGroupSource MaterialGroups { get; }
    public LoadedCityGateAccessoryAnimation Animation { get; }
    public IReadOnlyList<CityGateAccessoryDrawResources> Draws { get; }

    CityGateAccessoryGeometryResources(string directory, LoadedArmyCatalog catalog, LoadedModelMaterials model,
        LoadedModelShaderBindings bindings, ArmyContainerMaterialGroupSource materialGroups, LoadedCityGateAccessoryAnimation animation,
        List<CityGateAccessoryDrawResources> draws, Dictionary<string, byte[]> files)
        => (Directory, Catalog, Model, ShaderBindings, MaterialGroups, Animation, Draws, this.files) =
            (Path.GetFullPath(directory), catalog, model, bindings, materialGroups, animation, draws.AsReadOnly(), files);

    public static CityGateAccessoryGeometryResources Prepare(GameResourceRepository repository,
        CityGateAccessoryResources accessories, MapCameraConstructionPacket initialCamera,
        MapCommonRenderState common, uint contextFlags, IReadOnlyList<MapWorldKind1SourceMetadata> orderedRecords,
        string locale, string directory)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(accessories);
        ArgumentNullException.ThrowIfNull(initialCamera); ArgumentNullException.ThrowIfNull(common);
        ArgumentNullException.ThrowIfNull(orderedRecords); ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (orderedRecords.Count is < 1 or > 32 || common.ModelGroupSelector is not byte selector ||
            common.ModelExclusionMask is not uint commonExclusion)
            throw new NotSupportedException("Accessory resource routing requires declared records and Common model settings.");
        var catalog = CeArmyModels.LoadCatalog(repository);
        var category = CeArmyModels.KnownSourceCategories[2];
        var definition = catalog.Definitions.Single(d => d.Definition.ObjectId == category.DefinitionId);
        if (definition.ModelObject.ObjectId != category.ModelObjectId || definition.ModelFileId != category.ModelFileId)
            throw new InvalidDataException("Accessory definition selects a different category-2 model.");
        var animation = accessories.Animation;
        var model = repository.LoadModelMaterials("CharacterEditor", definition.ModelFileId);
        if (model.Source.ResourceUri != animation.Model.Source.ResourceUri ||
            model.Source.DecodedSha256 != animation.Model.Source.DecodedSha256)
            throw new InvalidDataException("Accessory geometry and animation belong to different resource generations.");
        var geometry = animation.Model.Geometry; var skeleton = animation.Model.Transforms;
        var bindings = ModelShaderResources.LoadBindings(repository, catalog.Dictionary, category.ModelObjectId, model);
        _ = ArmyModelDynamicShaderSource.ImportFresh(catalog, 2);
        var grouping = ArmyContainerMaterialGroupSource.Load(repository, catalog, 2, geometry);
        var sourceDraws = G1ModelDrawReader.Read(geometry);
        if (sourceDraws.Draws.Count != 3)
            throw new NotSupportedException("Accessory native geometry requires the three supported category-2 draws.");
        var root = animation.DefinitionRoot;
        var local = new ArmyAnimationLocalPose(0, 0, skeleton.Joints.SelectMany(j => j.RawBytes).ToArray(), []);
        var hierarchy = ArmyAnimationHierarchy.Build(skeleton, local,
            new(new(root.Scale34, 0), root.Quaternion24, new(root.Position18, 1)));
        var matrices = hierarchy.BuildMatrices();
        uint exclusion = UInt(definition.Definition, MapSourceCreatedViewFields.ModelExclusion78PropertyHash);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var shaderObjects = repository.LoadObjects("system", 0x718CCB8B);
        var queue54 = ModelRendererStateSourceReader.ReadQueue(graph, 54);
        var queue55 = ModelRendererStateSourceReader.ReadQueue(graph, 55);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var writer = new D3D11ModelResourceWriter(repository, files);
        var draws = new List<CityGateAccessoryDrawResources>(); var paths = new List<string>();
        foreach (var draw in sourceDraws.Draws)
        {
            var group = grouping.ReadDeclaredDraw(draw.Index);
            if (group.NativeGroupMode6 == 1)
                throw new NotSupportedException("Accessory named matrix wrappers require their actual resource owner.");
            int paletteIndex = checked((int)draw.UnknownValue8);
            var palette = skeleton.JointPalettes[paletteIndex];
            var matrixInput = ModelMatrixBindingSourceReader.Prepare(draw, 0, (uint)palette.Entries.Count)
                ?? throw new NotSupportedException("Accessory draw has no skin matrix input.");
            var skin = CityGateAccessorySkinConstants.SkinControls(geometry, draw, matrixInput);
            var baseTable = ModelShaderResources.ResolveDrawBaseSource(bindings, draw.Index, shaderObjects).Binding.Shader;
            var shaderInterface = ModelShaderResources.ResolveType16Interface(baseTable, queue54.ShaderStateOffset);
            var provider = shaderInterface.Provider ?? throw new NotSupportedException("Accessory ordinary shader provider is absent.");
            var shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
            var queue = ModelOrdinaryQueueSelectionSource.ReadFreshSelected(queue54, shaderInterface, shader).SourceQueueSelectsShader ? queue54 : queue55;
            if (!ModelOrdinaryQueueSelectionSource.ReadFreshSelected(queue, shaderInterface, shader).SourceQueueSelectsShader)
                throw new NotSupportedException("Accessory shader has no supported ordinary queue.");
            var selectedMatrix = ModelMatrixBindingSourceReader.Select(shader, matrixInput);
            var selection = ModelShaderSelectionSourceReader.Read(provider, shader);
            var program = ModelShaderSelectionSourceReader.ResolveSingleOtherDimensionsInitialized(selection,
                ModelShaderSelectionSourceReader.ReadInitialCache(selection), selectedMatrix, 0);
            int vs = program.Combination.Stages.Single(s => s.StageCode == 1).Entry!.ProgramIndex;
            int ps = program.Combination.Stages.Single(s => s.StageCode == 0).Entry!.ProgramIndex;
            var active = ArmyNativePaletteWriter.Build(skeleton, paletteIndex, matrices, matrices.Count,
                new Vector4[384], ArmyNativePoseBufferKind.Current);
            if (active.WrittenEntryCount != palette.Entries.Count)
                throw new NotSupportedException("Accessory default pose leaves a referenced palette entry unresolved.");
            byte[] vertex = CityGateAccessorySkinConstants.Vertex(shader.ReadResourceDefinitions(vs),
                initialCamera.SourceWorldToProjection, active.Rows.Take(palette.Entries.Count * 3).ToArray(), skin);
            var material = ModelGbufferMaterialSourceReader.Read(shader, ModelShaderResources.ResolveDrawMaterials(bindings, draw.Index));
            var specular = ModelGbufferMaterialSourceReader.PrepareSpecular(material, 0);
            if (!specular.WritesValue || specular.Value is null || material.MaterialId is null)
                throw new NotSupportedException("Accessory base material needs an independently produced shader value.");
            var pixelDefinitions = shader.ReadResourceDefinitions(ps);
            float? alpha = pixelDefinitions.Buffers.Single().Variables.Any(v => v.Name.Text == "vATest") ?
                ModelGbufferMaterialSourceReader.PrepareAlphaTest(material.SourceAlphaReference, queue.BackendAlphaEnabled) : null;
            byte[] pixel = ModelGbufferConstants.Pixel(pixelDefinitions, new byte[80], new(specular.Value.Value,
                MapStaticModelTransforms.ToShaderAffineRows(initialCamera.SourceWorldToView), material.MaterialId.Value, alpha)).Bytes;
            ModelOrdinaryStaticStencilRule? route = null;
            foreach (var record in orderedRecords)
            {
                if ((record.DeclaredGroupMask & (exclusion | commonExclusion)) != 0) continue;
                var current = ModelOrdinaryDispatcherStencilSource.PrepareResourceRoute(queue, record.DeclaredGroupMask, selector, contextFlags);
                if (!current.RecordSelected) continue;
                if (route is not null && route != current)
                    throw new NotSupportedException("Accessory static geometry needs distinct per-record stencil rules.");
                route = current;
            }
            route ??= ModelOrdinaryDispatcherStencilSource.PrepareResourceRoute(queue, orderedRecords[0].DeclaredGroupMask, selector, contextFlags);
            string path = $"geometry/draw-{draw.Index}";
            writer.WriteGeometryAndPrograms(path, $"geometry/meshes/{definition.ModelFileId:X8}", geometry, draw,
                shader, vs, ps, vertex, pixel, bindings, queue, initialCamera.Lens.ReverseDepth,
                ArmySkinVertexAdapter.SingleInstanceSourceStreams(geometry, draw.Index), route.StencilEnabled, route.Reference);
            // Material textures are independently created/installed by the flag
            // owner. An empty static list is not a completed material receipt.
            Text(path + "/textures-native.txt", ""); Text(path + "/samplers-native.txt", "");
            files.Add(path + "/skin-controls.bin", Words(skin));
            if (group.DeclaredContainerGroupOrdinal is not uint ordinal || ordinal > 2 ||
                group.DeclaredDefaultMaterialObjectId is null)
                throw new NotSupportedException("Accessory draw has no supported declared material group.");
            int manager = ordinal < 2 ? ArmyFlagMaterialVariations.ReadManagerForDeclaredGroup(2, ordinal) : -1;
            draws.Add(new(draw.Index, paletteIndex, manager, baseTable, provider, shader,
                program.Combination.Index, vs, ps, material));
            paths.Add($"draw-{draw.Index}");
        }
        Text("geometry/models.txt", string.Join('\n', paths) + "\n");
        using (var stream = new MemoryStream())
        {
            using (var output = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                output.Write(0x31474341u); output.Write(draws.Count);
                foreach (var draw in draws) { output.Write(draw.DrawIndex); output.Write(draw.PaletteIndex); output.Write(draw.MaterialManager); }
            }
            files.Add("geometry/bindings.bin", stream.ToArray());
        }
        var pool = ArmyGraphicsPoolSource.Create(locale).Banks.Single(p => p.Category == 2);
        files.Add("geometry/pool.bin", Words([pool.Capacity, pool.Kind, pool.Flags]));
        using (var stream = new MemoryStream())
        {
            using (var output = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                output.Write(2u); output.Write(animation.Profile.RawActionId); output.Write(animation.Action.ActionIndex);
                output.Write(animation.Profile.AnimationFileId); output.Write(0u);
                output.Write(animation.Profile.ControllerRate28); output.Write(animation.Profile.Duration8);
                output.Write(animation.Profile.LoopStartDeclaration);
                if (animation.Profile.FpsModeTable.Count != 2) throw new NotSupportedException("Accessory FPS table has an unsupported extent.");
                foreach (uint fps in animation.Profile.FpsModeTable) output.Write(fps);
            }
            files.Add("geometry/default-action.bin", stream.ToArray());
        }
        var header = geometry.RawGeometryHeader;
        var bounds = MapModelNativeBoundsSources.BuildSingleNode(MapModelNativeBoundsSources.PrepareHeader(
            new(F(header, 20), F(header, 24), F(header, 28)), new(F(header, 32), F(header, 36), F(header, 40)),
            animation.Model.Source.ResourceUri, animation.Model.Source.DecodedSha256), root, false, false);
        using (var stream = new MemoryStream())
        {
            using (var output = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                Vector(output, new(root.Scale34, 1)); var q = root.Quaternion24;
                Vector(output, new(q.X, q.Y, q.Z, q.W)); Vector(output, new(root.Position18, 1));
                foreach (var value in new[] { bounds.Center0, bounds.Basis10, bounds.Basis20, bounds.Basis30, bounds.HalfExtents40 }) Vector(output, value);
            }
            files.Add("geometry/constructor-world.bin", stream.ToArray());
        }
        return new(directory, catalog, model, bindings, grouping, animation, draws, files);

        void Text(string name, string text) => files.Add(name, Encoding.UTF8.GetBytes(text));
    }

    public void WriteNativeInputs()
    {
        foreach (var file in files)
        {
            string path = Path.Combine(Directory, file.Key);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.Value);
        }
        ArmyGraphicsAnimationNativeInputs.WritePose(Transforms.Transforms, Animation.Action, Path.Combine(Directory, "pose-native"));
    }

    static uint UInt(DokObject value, uint name)
    {
        var property = value.Properties.Single(p => p.NameHash == name);
        if (property.TypeCode != 5 || property.ElementCount != 1 || property.ElementSize != 4 || property.RawValue.Length != 4)
            throw new InvalidDataException("Accessory source reference is not a scalar DWORD.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }
    static float F(byte[] bytes, int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at, 4));
    static byte[] Words(IReadOnlyList<uint> words)
    {
        byte[] bytes = new byte[words.Count * 4];
        for (int i = 0; i < words.Count; ++i) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
        return bytes;
    }
    static void Vector(BinaryWriter output, Vector4 value)
    { output.Write(value.X); output.Write(value.Y); output.Write(value.Z); output.Write(value.W); }
}
