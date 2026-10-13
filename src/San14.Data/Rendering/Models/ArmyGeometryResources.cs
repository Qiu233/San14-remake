using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ArmyGeometryDrawResources(int DrawIndex, int PaletteIndex, int MaterialManager,
    NamedModelShader BaseTable, NamedModelShader Provider, LoadedShaderContainer Shader,
    int Combination, int VertexProgram, int PixelProgram, ModelGbufferMaterialSource Material,
    ModelQueueStateSource Queue, uint AllowedRecords, bool StencilEnabled, byte StencilReference,
    ModelMatrixBindingInput MatrixInput, ReadOnlyMemory<byte> InitialVertex, ReadOnlyMemory<byte> InitialPixel);

/// <summary>One declared category's resource inputs, independent of placed actors.
/// Material reselection uses the same camera, matrix constructor and static route.</summary>
public sealed class ArmyCategoryGeometryResources
{
    readonly GameResourceRepository repository;
    readonly MapCommonRenderState common;
    readonly IReadOnlyList<MapWorldKind1SourceMetadata> records;
    readonly LoadedRenderGraph graph;
    readonly ArmyGeometryConstants constants;
    readonly IReadOnlyDictionary<int, G1DrawRecord> sourceDraws;
    public int Category { get; }
    public LoadedArmyCatalog Catalog { get; }
    public LoadedModelMaterials Model { get; }
    public LoadedModelTransforms Transforms { get; }
    public LoadedModelShaderBindings ShaderBindings { get; }
    public ArmyContainerMaterialGroupSource? MaterialGroups { get; }
    public MapCameraConstructionPacket InitialCamera { get; }
    public uint ContextFlags { get; }
    public bool ReverseDepth => InitialCamera.Lens.ReverseDepth;
    public ArmyPrimitivePublicationSource PrimitiveSource { get; }
    public ArmyModelWorldVisibilitySource Visibility { get; }
    public UnitWorldTransformState Constructor { get; }
    public IReadOnlyList<ArmyGeometryDrawResources> Draws { get; private set; } = [];

    internal ArmyCategoryGeometryResources(GameResourceRepository repository, int category,
        LoadedArmyCatalog catalog, LoadedModelMaterials model, LoadedModelTransforms transforms,
        LoadedModelShaderBindings bindings, ArmyContainerMaterialGroupSource? materialGroups,
        UnitWorldTransformState constructor, MapCameraConstructionPacket camera, MapCommonRenderState common,
        uint contextFlags, IReadOnlyList<MapWorldKind1SourceMetadata> orderedRecords, LoadedRenderGraph graph)
    {
        this.repository = repository; Category = category; Catalog = catalog; Model = model; Transforms = transforms;
        ShaderBindings = bindings; MaterialGroups = materialGroups; Constructor = constructor; InitialCamera = camera;
        this.common = common; ContextFlags = contextFlags; records = Array.AsReadOnly(orderedRecords.ToArray()); this.graph = graph;
        PrimitiveSource = ArmyPrimitivePublicationSource.ReadKnownCategory(catalog, category, new(model.Geometry, model.Source));
        Visibility = ArmyModelWorldVisibilitySource.Read(PrimitiveSource);
        sourceDraws = G1ModelDrawReader.Read(model.Geometry).Draws.ToDictionary(d => d.Index);
        constants = new(transforms, constructor, camera);
        _ = ArmyModelDynamicShaderSource.ImportFresh(catalog, category);
        var objects = repository.LoadObjects("system", 0x718CCB8B);
        Draws = Array.AsReadOnly(PrimitiveSource.OrdinarySourceDrawIndices.Select(draw =>
            PrepareMaterialDraw(draw, ModelShaderResources.ResolveDrawBaseSource(bindings, draw, objects).Binding.Shader, null, null)).ToArray());
    }

    public byte[] PrepareVertexConstants(int drawIndex, DxbcResourceDefinitions definitions)
    {
        var draw = sourceDraws[drawIndex];
        return constants.Vertex(draw, Matrix(draw), definitions);
    }

    public byte[] PreparePixelConstants(int drawIndex, DxbcResourceDefinitions definitions,
        ModelGbufferMaterialSource material, ModelQueueStateSource? queue = null)
        => constants.Pixel(definitions, material, queue ?? Draws.Single(d => d.DrawIndex == drawIndex).Queue);

    public ArmyGeometryDrawResources PrepareMaterialDraw(int drawIndex, NamedModelShader actualTable,
        ArmyMaterialParameterRecord12? record, int? materialId)
    {
        var draw = sourceDraws[drawIndex]; var matrix = Matrix(draw);
        var iface = ModelShaderResources.ResolveType16Interface(actualTable, 18);
        var provider = iface.Provider ?? throw new NotSupportedException("Army class18 shader provider is absent.");
        var shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
        var queue = PrepareQueue(iface, shader);
        var selected = ModelMatrixBindingSourceReader.Select(shader, matrix);
        var selection = ModelShaderSelectionSourceReader.Read(provider, shader);
        var program = ModelShaderSelectionSourceReader.ResolveSingleOtherDimensionsInitialized(selection,
            ModelShaderSelectionSourceReader.ReadInitialCache(selection), selected, 0);
        int vs = program.Combination.Stages.Single(s => s.StageCode == 1).Entry!.ProgramIndex;
        int ps = program.Combination.Stages.Single(s => s.StageCode == 0).Entry!.ProgramIndex;
        var material = ModelGbufferMaterialSourceReader.Read(shader, ModelShaderResources.ResolveDrawMaterials(ShaderBindings, drawIndex));
        if (record is not null)
        {
            ReadOnlySpan<byte> bytes = record.MaterialRecord64.Span;
            if (bytes.Length != 64 || materialId is null)
                throw new InvalidDataException("Army selected material requires its complete type12 record and material ID.");
            material = material with { RawMaterialFlags = record.Flags1C, SourceSpecular = new(F(bytes,32),F(bytes,36),F(bytes,40),F(bytes,44)),
                SourceAlphaReference = record.AlphaReference3E, MaterialIdParameter = null, MaterialId = materialId };
        }
        uint membership = 0; ModelOrdinaryStaticStencilRule? route = null;
        for (int i = 0; i < records.Count; ++i)
        {
            uint mask = records[i].DeclaredGroupMask;
            if ((mask & (PrimitiveSource.Exclusion78 | common.ModelExclusionMask!.Value)) != 0) continue;
            var candidate = ModelOrdinaryDispatcherStencilSource.PrepareResourceRoute(queue, mask, common.ModelGroupSelector!.Value, ContextFlags);
            if (!candidate.RecordSelected) continue;
            if (route is not null && route != candidate)
                throw new NotSupportedException("Army geometry requires distinct admitted per-record stencil state.");
            route = candidate; membership |= 1u << i;
        }
        route ??= ModelOrdinaryDispatcherStencilSource.PrepareResourceRoute(queue, records[0].DeclaredGroupMask,
            common.ModelGroupSelector!.Value, ContextFlags);
        int manager = -1;
        if (Category is 2 or 3)
        {
            var group = MaterialGroups!.ReadDeclaredDraw(drawIndex);
            if (group.NativeGroupMode6 == 1)
                throw new NotSupportedException("Army named matrix group requires its separate resource owner.");
            if (group.DeclaredContainerGroupOrdinal is not uint ordinal || group.DeclaredDefaultMaterialObjectId is null)
                throw new NotSupportedException("Army flag draw has no actual declared material group.");
            if (Category == 2 ? ordinal < 2 : ordinal == 0)
                manager = ArmyFlagMaterialVariations.ReadManagerForDeclaredGroup(Category, ordinal);
        }
        return new(drawIndex, checked((int)draw.UnknownValue8), manager, actualTable, provider, shader,
            program.Combination.Index, vs, ps, material, queue, membership, route.StencilEnabled, route.Reference,
            matrix, constants.Vertex(draw, matrix, shader.ReadResourceDefinitions(vs)), constants.Pixel(shader.ReadResourceDefinitions(ps), material, queue));
    }

    ModelMatrixBindingInput Matrix(G1DrawRecord draw)
    {
        var group = G1ModelDrawReader.Read(Model.Geometry).Groups.Single(g => g.DrawIndices.Contains(draw.Index));
        if ((ushort)group.Flags == 1) throw new NotSupportedException("Army named matrix group requires its actual wrapper owner.");
        var palette = Transforms.Transforms.JointPalettes[checked((int)draw.UnknownValue8)];
        return ModelMatrixBindingSourceReader.Prepare(draw, 0, checked((uint)palette.Entries.Count)) ??
            throw new NotSupportedException("Army draw has no supported type17 source input.");
    }

    ModelQueueStateSource PrepareQueue(ModelShaderInterfaceSource selected, LoadedShaderContainer shader)
    {
        var provider = selected.Provider!;
        if (selected.StateIndex != 18 || provider.Object.ClassId != 0x18EB1E56 ||
            provider.ShaderFileHash != shader.Source.FileId ||
            shader.Source.ResourceUri != GameResourceId.Rdb("system",provider.ShaderFileHash).ToString())
            throw new NotSupportedException("Army resource routing requires its actual class18 provider association.");
        var configurations = shader.ReadConfigurations().Records;
        if (configurations.Count(c=>c.Name=="GBuffer" && c.RawSecondWord==2)!=1)
            throw new NotSupportedException("Army renderer requires its implemented GBuffer module.");
        // The fresh class18 constructor clears its modifier. GBuffer preserves
        // the dispatcher's stencil. Other created plugins need their own actual
        // callback producer; optional zero-word records create no such callback.
        if (configurations.Any(c=>c.Name!="GBuffer" && c.RawSecondWord!=0))
            throw new NotSupportedException("Army renderer has an unimplemented created shader plugin.");
        // C12600's name query includes optional zero-word records. It is not a
        // texture-presence test or a request to resolve a different G1 variant.
        bool lightMap=configurations.Any(c=>c.Name=="LightMap");
        bool alpha=configurations.Any(c=>c.Name is "AlphaTest" or "Dissolve");
        var candidates=new[]{49,50,54,55}.Select(i=>ModelRendererStateSourceReader.ReadQueue(graph,i))
            .Where(q=>((q.Flag2&4)!=0)==lightMap && q.BackendAlphaEnabled==alpha).ToArray();
        if(candidates.Length!=1)throw new NotSupportedException("Army renderer has no unique initialized ordinary queue.");
        var queue=candidates[0];
        if(queue.RenderType!=0 || queue.ViewId!=0 || queue.ShaderStateOffset!=18 || queue.ClockwiseRendering ||
            queue.Flag!=(queue.Node.Index is 49 or 54?0x0E:0x06) || queue.Flag2!=(lightMap?4u:0u))
            throw new NotSupportedException("Army resource routing requires its ordinary View0 queue state.");
        return queue;
    }
    static float F(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at,4));
}

/// <summary>Complete category0..9 static resource library. Actor allocation,
/// current roots and material-ready publications are separate runtime owners.</summary>
public sealed class ArmyGeometryResources
{
    readonly Dictionary<string, byte[]> files;
    public string Directory { get; }
    public LoadedArmyCatalog Catalog { get; }
    public ArmyGraphicsActions GraphicsActions { get; }
    public CityGateAccessoryGeometryResources SharedCategory2 { get; }
    public IReadOnlyDictionary<int, ArmyCategoryGeometryResources> Categories { get; }
    ArmyGeometryResources(string directory, LoadedArmyCatalog catalog, ArmyGraphicsActions actions,
        CityGateAccessoryGeometryResources shared, Dictionary<int,ArmyCategoryGeometryResources> categories,
        Dictionary<string,byte[]> files)
        => (Directory,Catalog,GraphicsActions,SharedCategory2,Categories,this.files) =
            (Path.GetFullPath(directory),catalog,actions,shared,new ReadOnlyDictionary<int,ArmyCategoryGeometryResources>(categories),files);

    public static ArmyGeometryResources Prepare(GameResourceRepository repository, MapCameraConstructionPacket initialCamera,
        MapCommonRenderState common, uint contextFlags, IReadOnlyList<MapWorldKind1SourceMetadata> orderedRecords,
        CityGateAccessoryGeometryResources sharedCategory2, string locale, string directory)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(initialCamera);
        ArgumentNullException.ThrowIfNull(common); ArgumentNullException.ThrowIfNull(orderedRecords);
        ArgumentNullException.ThrowIfNull(sharedCategory2); ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (orderedRecords.Count is < 1 or > 32 || common.ModelGroupSelector is null || common.ModelExclusionMask is null ||
            (contextFlags & 3) != 1 || orderedRecords.Any(r => (r.SourceLightDataF20 & 1) != 0))
            throw new NotSupportedException("Army static routing requires actual ordinary records, Common settings and context.");
        var catalog = sharedCategory2.Catalog;
        var actions = new ArmyGraphicsActions(repository, catalog, Enumerable.Range(0,10));
        var pool = ArmyGraphicsPoolSource.Create(locale,10);
        var graph = repository.LoadRenderGraph("system",0x024496EE);
        uint commonType = MapModelNativeVisibilitySources.ReadCommonViewTemplate(
            repository.LoadObjects("KIDSSystemResource",CommonRenderResources.SourceFileId),CommonRenderResources.SourceObjectId).Value;
        var files = new Dictionary<string,byte[]>(StringComparer.Ordinal);
        var writer = new D3D11ModelResourceWriter(repository,files);
        var categories = new Dictionary<int,ArmyCategoryGeometryResources>();
        for (int category = 0; category < 10; ++category)
        {
            try
            {
                var known = CeArmyModels.KnownSourceCategories[category];
                var definition = catalog.Definitions.Single(d => d.Definition.ObjectId == known.DefinitionId);
                if (definition.ModelObject.ObjectId != known.ModelObjectId || definition.ModelFileId != known.ModelFileId)
                    throw new InvalidDataException("Army category model reference differs from its registered declaration.");
                LoadedModelMaterials model; LoadedModelTransforms transforms; LoadedModelShaderBindings bindings;
                LoadedModelTextureResources? textures = null; ArmyContainerMaterialGroupSource? groups = null;
                UnitWorldTransformState root;
                if (category == 2)
                {
                    model = sharedCategory2.Model; transforms = sharedCategory2.Transforms; bindings = sharedCategory2.ShaderBindings;
                    groups = sharedCategory2.MaterialGroups; root = sharedCategory2.Animation.DefinitionRoot;
                }
                else if (category == 3)
                {
                    model = repository.LoadModelMaterials("CharacterEditor",definition.ModelFileId);
                    transforms = repository.LoadModelTransforms("CharacterEditor",definition.ModelFileId);
                    bindings = ModelShaderResources.LoadBindings(repository,catalog.Dictionary,known.ModelObjectId,model);
                    groups = ArmyContainerMaterialGroupSource.Load(repository,catalog,category,model.Geometry);
                    root = UnitWorldTransform.ConstructDefinition(catalog.Catalog,definition.Definition);
                }
                else
                {
                    var loaded = CeArmyModels.LoadKnownCategory(repository,catalog,category);
                    model = loaded.Resources.Model; transforms = loaded.Transforms; textures = loaded.Resources;
                    bindings = ModelShaderResources.LoadBindings(repository,textures);
                    root = UnitWorldTransform.ConstructDefinition(catalog.Catalog,definition.Definition);
                }
                var prepared = new ArmyCategoryGeometryResources(repository,category,catalog,model,transforms,bindings,groups,
                    root,initialCamera,common,contextFlags,orderedRecords,graph);
                categories.Add(category,prepared);
                string prefix = $"category-{category}";
                var sourceDraws = G1ModelDrawReader.Read(model.Geometry).Draws.ToDictionary(d => d.Index);
                for (int index = 0; index < prepared.Draws.Count; ++index)
                {
                    var draw = prepared.Draws[index]; string path = $"{prefix}/draw-{draw.DrawIndex}";
                    if (category != 2)
                    {
                        writer.WriteGeometryAndPrograms(path,$"meshes/{model.Source.FileId:X8}",model.Geometry,sourceDraws[draw.DrawIndex],
                            draw.Shader,draw.VertexProgram,draw.PixelProgram,draw.InitialVertex.ToArray(),draw.InitialPixel.ToArray(),bindings,
                            draw.Queue,initialCamera.Lens.ReverseDepth,ArmySkinVertexAdapter.SingleInstanceSourceStreams(model.Geometry,draw.DrawIndex),
                            draw.StencilEnabled,draw.StencilReference);
                        if (textures is not null) writer.WriteStaticTextures(path,textures,draw.DrawIndex,draw.Provider,draw.Shader,draw.Combination);
                        else { Text(path+"/textures-native.txt",""); Text(path+"/samplers-native.txt",""); }
                        files.Add(path+"/skin-controls.bin",Words(ArmyGeometryConstants.SkinControls(draw.MatrixInput)));
                    }
                }
                Text(prefix+"/models.txt",string.Join('\n',prepared.Draws.Select(d=>$"draw-{d.DrawIndex}"))+"\n");
                Text(prefix+"/flag-draws.txt",string.Join('\n',prepared.Draws.Where(d=>d.MaterialManager>=0).Select(d=>$"{d.DrawIndex} {d.MaterialManager}"))+
                    (prepared.Draws.Any(d=>d.MaterialManager>=0)?"\n":""));
                var tableIds = prepared.Draws.Select(d=>d.BaseTable).DistinctBy(t=>t.Objects.Source.ResourceUri+"/"+t.Object.ObjectId.ToString("X8")).ToArray();
                Text(prefix+"/base-table-owners.txt",string.Join('\n',tableIds.Select((t,i)=>$"{t.Object.ObjectId} {i+1}"))+"\n");
                Text(prefix+"/table-identities.txt",string.Join('\n',tableIds.Select((t,i)=>$"{i+1} {t.Objects.Source.ResourceUri} {t.Object.ObjectId}"))+"\n");
                var visibility=prepared.Visibility;
                uint culler=MapModelNativeVisibilitySources.ResolveFrustumCullingType(visibility.DeclaredFrustumType.Value,commonType);
                if(culler is not (0 or 3))throw new NotSupportedException("Army detailed custom box culler has no current consumer.");
                var bounds=MapModelNativeBoundsSources.BuildSingleNode(visibility.PreparedModelBounds,root,false,false);
                var distance=ModelPrimitiveInventorySource.LoadArmy(prepared.PrimitiveSource).DeclaredDistance;
                Binary(prefix+"/mode-resource.bin",output=>
                {
                    output.Write("ARMYMOD1"u8);output.Write(category);output.Write(prepared.Draws.Count);output.Write(orderedRecords.Count);
                    output.Write(common.ModelExclusionMask.Value);output.Write(visibility.ConstructOwnedVisibility($"Army-resource-category{category}").ReadSlot(0));
                    output.Write(prepared.PrimitiveSource.Exclusion78);output.Write(culler);
                    output.Write(distance.ForceRegisterNormalZDistance1A0);output.Write(distance.RegisterNormalZDistanceBias1A4);
                    Vector(output,new(root.Scale34,1));var q=root.Quaternion24;Vector(output,new(q.X,q.Y,q.Z,q.W));Vector(output,new(root.Position18,1));
                    foreach(var value in new[]{bounds.Center0,bounds.Basis10,bounds.Basis20,bounds.Basis30,bounds.HalfExtents40})Vector(output,value);
                    foreach(var record in orderedRecords){output.Write(record.DeclaredGroupMask);output.Write(record.MaxPrimitiveNum);}
                    for(int index=0;index<prepared.Draws.Count;++index)
                    {
                        var draw=prepared.Draws[index];output.Write(draw.DrawIndex);output.Write(category==2?sharedCategory2.Draws.ToList().FindIndex(d=>d.DrawIndex==draw.DrawIndex):index);
                        output.Write(draw.PaletteIndex);output.Write(draw.Queue.Node.Index);output.Write(draw.AllowedRecords);output.Write(draw.BaseTable.Object.ObjectId);
                        output.Write((ulong)(Array.FindIndex(tableIds,t=>t.Objects.Source.ResourceUri==draw.BaseTable.Objects.Source.ResourceUri&&t.Object.ObjectId==draw.BaseTable.Object.ObjectId)+1));
                    }
                });
                Binary(prefix+"/local-bounds.bin",output=>{foreach(var v in new[]{visibility.PreparedModelBounds.GetterMinimum,visibility.PreparedModelBounds.GetterMaximum})
                    {output.Write(v.X);output.Write(v.Y);output.Write(v.Z);}});
                var bank=pool.Banks.Single(b=>b.Category==category);
                Binary(prefix+"/pool-banks.bin",output=>{output.Write("ARMYPOL1"u8);output.Write(1u);output.Write(bank.Category);output.Write(bank.Capacity);output.Write(bank.Kind);output.Write(bank.Flags);});
                var action=actions.Actions[(category,0)];
                Binary(prefix+"/army-default-actions.bin",output=>{output.Write("ARMYACT1"u8);output.Write(1u);
                    foreach(uint fps in ArmyGraphicsRules.FpsModeRates)output.Write(fps);WriteAction(output,action,false);});
            }
            catch(Exception error)when(error is InvalidDataException or NotSupportedException)
            {throw new NotSupportedException($"Army category{category} resource preparation failed: {error.Message}",error);}
        }
        Binary("resource-bank.bin",output=>{output.Write(0x31425241u);output.Write(10u);});
        Text("categories.txt",string.Join('\n',Enumerable.Range(0,10).Select(i=>$"{i} category-{i} {(i==2?1:0)}"))+"\n");
        return new(directory,catalog,actions,sharedCategory2,categories,files);
        void Text(string path,string text)=>files.Add(path,Encoding.UTF8.GetBytes(text));
        void Binary(string path,Action<BinaryWriter> write){using var stream=new MemoryStream();using(var output=new BinaryWriter(stream,Encoding.UTF8,true))write(output);files.Add(path,stream.ToArray());}
    }

    public void WriteNativeInputs()
    {
        foreach(var file in files){string path=Path.Combine(Directory,file.Key);System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,file.Value);}
        foreach(var category in Categories.Values)
            ArmyGraphicsAnimationNativeInputs.WritePose(category.Transforms.Transforms,GraphicsActions.Actions[(category.Category,0)].Animation,
                Path.Combine(Directory,$"category-{category.Category}","poses",$"category-{category.Category}"));
    }

    public void WriteActionInputs(string actionDirectory)
    {
        System.IO.Directory.CreateDirectory(actionDirectory);
        using var output=new BinaryWriter(File.Create(Path.Combine(actionDirectory,"actions.bin")));
        output.Write("ARMYANM1"u8);output.Write(GraphicsActions.Actions.Count);
        foreach(var action in GraphicsActions.Actions.Values.OrderBy(a=>a.Category).ThenBy(a=>a.Animation.ActionIndex))
        {
            WriteAction(output,action,true);
            ArmyGraphicsAnimationNativeInputs.WritePose(Categories[action.Category].Transforms.Transforms,action.Animation,
                Path.Combine(actionDirectory,$"category-{action.Category}",$"action-{action.RawActionId}"));
        }
    }
    static void WriteAction(BinaryWriter output,ArmyGraphicsAction action,bool complete)
    {
        output.Write(action.Category);output.Write(action.RawActionId);output.Write(action.Animation.ActionIndex);
        output.Write(action.Animation.AnimationData.Single().Source.FileId);output.Write(action.Category<2?1u:0u);
        output.Write(60f);output.Write(action.Duration);output.Write(action.LoopDeclaration);
        if(!complete)return;
        output.Write(action.StartFrame);output.Write(action.DeclaredLength);output.Write(action.EndOffset);output.Write(action.Interpolation);
        output.Write(action.ContinuationAction);output.Write(action.ContinuationObject);output.Write(action.PlayerFlags);output.Write(action.AdditionalAnimationCount);
    }
    static byte[] Words(IReadOnlyList<uint> words){byte[] bytes=new byte[words.Count*4];for(int i=0;i<words.Count;++i)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),words[i]);return bytes;}
    static void Vector(BinaryWriter output,Vector4 value){output.Write(value.X);output.Write(value.Y);output.Write(value.Z);output.Write(value.W);}
}
