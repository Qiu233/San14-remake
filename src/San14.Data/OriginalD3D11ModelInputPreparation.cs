using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalD3D11ModelInputBoundary(int InstanceIndex,uint? DefinitionObjectId,int? DrawIndex,
    string Scope,string Reason);
public sealed record OriginalD3D11ModelViewDraw(int PrimitiveIndex,int InstanceIndex,int DrawIndex,
    ulong DistanceKey,ulong TableOwner,byte[] Vertex,byte[] Pixel);
public sealed record OriginalD3D11ModelViewPlan(IReadOnlyList<int> VisibleInstanceIndices,
    IReadOnlyList<int> VisibleUnsupportedInstanceIndices,IReadOnlyList<OriginalD3D11ModelViewDraw> Draws);
public readonly record struct OriginalD3D11SourceModelMapping(int SceneInstanceIndex,int ModelOrdinal,int PrimitiveCount);
public readonly record struct OriginalD3D11SourceModelShadowMapping(int SceneInstanceIndex,int ModelOrdinal,
    int PrimitiveCount,uint Exclusion78,int Flags10C,float OpacityB0);
public sealed record OriginalD3D11SourceModelShadowTemplate(int TemplateIndex,OriginalNamedModelShader FinalTable,
    OriginalModelMatrixBindingInput Matrix,IReadOnlyList<string> VertexLayout);

/// <summary>First preparation of the finite stock FieldScene static Model bank.
/// Load reads the user's installed resources and validates source profiles once.
/// Scene placements, actual fresh World publications and the accepted initial
/// Camera belong to the caller. WriteNativeInputs only writes this owner's
/// prepared bytes. Generated geometry, shaders and textures are private local
/// cache contents; they are not distributable project assets. This leaf does
/// not select dynamic city/Army/effect owners, alpha55 or external materials.</summary>
public sealed class OriginalD3D11ModelInputPreparation
{
    private readonly IReadOnlyDictionary<string,byte[]> files;
    private readonly Instance[] instances;
    private readonly Primitive[] primitives;
    private readonly OriginalWorldPrimitiveOrderSource order;

    private OriginalD3D11ModelInputPreparation(LoadedSceneResources scene,Dictionary<string,byte[]> preparedFiles,
        Instance[] preparedInstances,Primitive[] preparedPrimitives,OriginalWorldPrimitiveOrderSource sourceOrder,
        int templateCount,int assetCount,OriginalD3D11ModelInputBoundary[] sourceBoundaries,
        IReadOnlyDictionary<int,(uint Exclusion,int Flags,float Opacity)> shadowFields,
        IReadOnlyList<(int Index,OriginalNamedModelShader Final,OriginalModelMatrixBindingInput Matrix,string Directory)> shadowTemplates)
    {
        Scene=scene;files=preparedFiles;instances=preparedInstances;primitives=preparedPrimitives;order=sourceOrder;
        TemplateCount=templateCount;AssetCount=assetCount;Boundaries=Array.AsReadOnly(sourceBoundaries);
        SourceModelMappings=Array.AsReadOnly(instances.Select((item,ordinal)=>
            new OriginalD3D11SourceModelMapping(item.Index,ordinal,item.PrimitiveCount)).ToArray());
        SourceShadowMappings=Array.AsReadOnly(instances.Select((item,ordinal)=>
            new OriginalD3D11SourceModelShadowMapping(item.Index,ordinal,item.PrimitiveCount,
                shadowFields[item.Index].Exclusion,shadowFields[item.Index].Flags,shadowFields[item.Index].Opacity)).ToArray());
        SourceShadowTemplates=Array.AsReadOnly(shadowTemplates.Select(template=>new OriginalD3D11SourceModelShadowTemplate(
            template.Index,template.Final,template.Matrix,Array.AsReadOnly(Encoding.UTF8.GetString(
                files[Normalize(Path.Combine(template.Directory,"layout.txt"))]).Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries)))).ToArray());
    }
    public LoadedSceneResources Scene{get;}
    public int InstanceCount=>instances.Length;
    public int PrimitiveCount=>primitives.Length;
    public int TemplateCount{get;}
    public int AssetCount{get;}
    public IReadOnlyList<OriginalD3D11ModelInputBoundary> Boundaries{get;}
    public int BoundaryCount=>Boundaries.Count;
    public IReadOnlyList<OriginalD3D11SourceModelMapping> SourceModelMappings{get;}
    public IReadOnlyList<OriginalD3D11SourceModelShadowMapping> SourceShadowMappings{get;}
    public IReadOnlyList<OriginalD3D11SourceModelShadowTemplate> SourceShadowTemplates{get;}
    public IReadOnlyList<string> PreparedFilenames=>Array.AsReadOnly(files.Keys.ToArray());

    /// <summary>The fresh-publication callback must describe each actual caller
    /// node. There is no default publication or retained-observation fallback.
    /// Initial Camera matrices/depth come from the same accepted caller packet.
    /// Unsupported profiles remain explicit in boundaries/hasUnsupported.</summary>
    public static OriginalD3D11ModelInputPreparation Load(OriginalGameRepository repo,LoadedSceneResources scene,
        ScenePlacements placements,OriginalMapCameraConstructionPacket initialCamera,
        Func<ScenePlacement,OriginalMapStaticModelPublication?> freshPublication)
    {
        ArgumentNullException.ThrowIfNull(repo);ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(placements);ArgumentNullException.ThrowIfNull(initialCamera);
        ArgumentNullException.ThrowIfNull(freshPublication);
        if(placements.Scene.Scene.Source.ResourceUri!=scene.Scene.Source.ResourceUri||
            placements.Scene.Scene.Source.DecodedSha256!=scene.Scene.Source.DecodedSha256)
            throw new InvalidDataException("Model placements differ from the selected source scene.");
        var assets=new OriginalMapStaticModelAssetCatalog(repo,scene);
        var initial=new{Camera=initialCamera};
        var queue=OriginalModelRendererStateSourceReader.ReadQueue(repo.LoadRenderGraph("system",0x024496EE),54);
        var shaderObjects=repo.LoadObjects("system",0x718CCB8B);
        var order=OriginalWorldPrimitiveOrderSource.LoadDefaultView0(repo);
        var fields=OriginalWorldPrimitiveSortDescriptor.ReadActualFields(order.Descriptor(OriginalWorldPrimitiveSortFamily.Opaque).ToArray());
        if(!fields.SequenceEqual(new[]{14,3}))throw new NotSupportedException("Current Model first-bank sort is outside opaque14->3.");
        var common=OriginalMapModelNativeVisibilitySources.ReadCommonViewTemplate(
            repo.LoadObjects("KIDSSystemResource",OriginalMapFreshCommonResources.SourceFileId),OriginalMapFreshCommonResources.SourceObjectId);
        const string output=".";
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        var profiles=new Dictionary<string,List<Profile>>();
        var profileFailures=new Dictionary<string,bool>();
        var templates=new List<string>();
        var tableOwners=new Dictionary<string,ulong>();
        var boundaries=new List<OriginalD3D11ModelInputBoundary>();
        var instances=new List<Instance>();
        var primitives=new List<Primitive>();
        var imports=new List<object>();
        var writtenStatic=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var shadowWorlds=new List<object>();
        var shadowFields=new Dictionary<int,(uint Exclusion,int Flags,float Opacity)>();
        var shadowTemplates=new List<(int Index,OriginalNamedModelShader Final,OriginalModelMatrixBindingInput Matrix,string Directory)>();
        var shadowDeclarations=new Dictionary<uint,object>();
        foreach(var placement in placements.Instances.Where(p=>p.Model is not null))
        {
            int index=placement.Source.Index;
            try
            {
                var asset=assets.LoadSceneInstance(index);
                var publication=freshPublication(placement)??throw new InvalidDataException("Actual fresh static publication is unavailable.");
                if(publication.FreshConstructorBindPrefix!=true||publication.PrimaryDescriptorPresent!=true||publication.ParentBound!=false||
                    publication.SourceDefaultLocalPoseIsCurrent!=true||publication.CurrentTransform is not null||publication.CurrentPrimitiveLocalRows is not null||
                    string.IsNullOrWhiteSpace(publication.CurrentIdentity))
                    throw new NotSupportedException("This input leaf requires the caller's actual fresh, bound, unparented static publication with source LOCAL pose.");
                string identity=publication.CurrentIdentity;
                var population=OriginalMapStaticModelInstanceFactory.FromNativeScenePlacements(assets,new ScenePlacements(scene,[placement]),
                    _=>publication);
                if(!population.Complete)throw new InvalidDataException(string.Join(";",population.Unresolved.Select(p=>p.Reason)));
                var source=population.Instances.Single();
                var prefix=OriginalMapModelNativeVisibilitySources.PrepareFreshSingleNode(source,new(identity,false,true,true));
                var obb=OriginalMapModelNativeBoundsSources.FromSourceWorld(source,prefix.BoundsPublication);
                var shadowCaller=OriginalDeferredEarlyShadowCasterAdmissionSource.LoadStatic(asset);
                var sourceVisibility=OriginalMapModelNativeCurrentVisibilitySources.ConstructFresh(prefix,identity);
                shadowFields.Add(index,(shadowCaller.Exclusion78,shadowCaller.WorldCasterFlags10C,sourceVisibility.ReadSlot(0)));
                shadowDeclarations.TryAdd(placement.Source.Definition.ObjectId,new{
                    definitionObjectId=placement.Source.Definition.ObjectId,shadowCaller.SourceEvidence,
                    visibilityProducer="OriginalMapModelNativeCurrentVisibilitySources.ConstructFresh(actualPrefix,actualIdentity).ReadSlot(0)"});
                shadowWorlds.Add(new{instanceIndex=index,definitionObjectId=placement.Source.Definition.ObjectId,casterExclusion78=shadowCaller.Exclusion78,
                    casterFlags10C=shadowCaller.WorldCasterFlags10C,opacityB0=sourceVisibility.ReadSlot(0),
                    constructorFlags44=prefix.ConstructorFlags44});
                uint mode=OriginalMapModelNativeVisibilitySources.ResolveFrustumCullingType(prefix.ConstructorFrustumCullingType33C,common.Value);
                if(!profiles.TryGetValue(asset.SourceKey,out var list))
                {
                    list=[];profiles.Add(asset.SourceKey,list);bool failed=false;
                    try
                    {
                        // Reuse the already closed constructor/loader zero branch,
                        // checking each selected source definition once. Model43
                        // keeps CPU158 null; empty World34/51/52 keeps World220 absent.
                        RequireEmpty(asset.TextureResources.ModelObject,43,0x6C3B792D,"Model ResourceSetMaterialsReference");
                        RequireEmpty(placement.Source.Definition,34,0x34CF9E5C,"World material pair bank");
                        RequireEmpty(placement.Source.Definition,51,0xCEE5DDAE,"World material group array");
                        RequireEmpty(placement.Source.Definition,52,0x5EDA237E,"World material object array");
                        var inventory=OriginalModelPrimitiveInventorySource.LoadStatic(asset);
                        var bindings=OriginalModelShaderResources.LoadBindings(repo,asset.TextureResources);
                        foreach(int drawIndex in inventory.Category0.SourceDrawIndices)
                        {
                            try
                            {
                                var draw=asset.Draws[drawIndex];_ = G1ModelTriangleReader.Read(asset.RawTransforms.Geometry,draw);
                                var named=OriginalModelShaderResources.ResolveDrawBaseSource(bindings,drawIndex,shaderObjects);
                                var selected=OriginalModelShaderResources.ResolveType16Interface(named.Binding.Shader,queue.ShaderStateOffset);
                                var provider=selected.Provider??throw new NotSupportedException("Current ordinary18 provider is null.");
                                var shader=OriginalModelShaderResources.LoadDeclaredG1S(repo,provider);
                                var type17=OriginalModelMatrixBindingSourceReader.PrepareUnboundGroup(bindings,drawIndex)
                                    ??throw new NotSupportedException("Actual primitive needs indexed/nonrigid type17.");
                                var matrix=OriginalModelMatrixBindingSourceReader.Select(shader,type17);
                                var selection=OriginalModelShaderSelectionSourceReader.Read(provider,shader);
                                var selectedPrograms=OriginalModelShaderSelectionSourceReader.ResolveSingleOtherDimensionsInitialized(selection,
                                    OriginalModelShaderSelectionSourceReader.ReadInitialCache(selection),matrix,0);
                                int vs=selectedPrograms.Combination.Stages.Single(p=>p.StageCode==1).Entry!.ProgramIndex;
                                int ps=selectedPrograms.Combination.Stages.Single(p=>p.StageCode==0).Entry!.ProgramIndex;
                                var ordinary=OriginalModelOrdinaryQueueSelectionSource.ReadFreshSelected(queue,selected,shader);
                                if(!ordinary.SourceQueueSelectsShader)throw new NotSupportedException("Actual renderer is not selected by opaque54; alpha55 remains outside this catalog.");
                                var material=OriginalModelGbufferMaterialSourceReader.Read(shader,OriginalModelShaderResources.ResolveDrawMaterials(bindings,drawIndex));
                                var spec=OriginalModelGbufferMaterialSourceReader.PrepareSpecular(material,0);
                                if(!spec.WritesValue||spec.Value is null||material.MaterialId is null)
                                    throw new NotSupportedException("Actual material requires an unclosed specular/cache/nMtrID profile.");
                                var vd=shader.ReadResourceDefinitions(vs);var pd=shader.ReadResourceDefinitions(ps);
                                float? alpha=pd.Buffers.Single().Variables.Any(v=>v.Name.Text=="vATest")?
                                    OriginalModelGbufferMaterialSourceReader.PrepareAlphaTest(material.SourceAlphaReference,queue.BackendAlphaEnabled):null;
                                var transform=source.DrawTransforms[drawIndex];
                                byte[] vb=OriginalModelGbufferConstants.Vertex(vd,new byte[160],new(Rows(initial.Camera.SourceWorldToProjection),transform.NormalRows,transform.PositionRows)).Bytes;
                                byte[] pb=OriginalModelGbufferConstants.Pixel(pd,new byte[80],new(spec.Value.Value,
                                    OriginalMapStaticModelTransforms.ToShaderAffineRows(initial.Camera.SourceWorldToView),material.MaterialId.Value,alpha)).Bytes;
                                string tableKey=named.Binding.Shader.Objects.Source.ResourceUri+"/"+named.Binding.Shader.Object.ObjectId.ToString("X8");
                                if(!tableOwners.TryGetValue(tableKey,out ulong table))
                                {
                                    // This loader creates/retains one owned full typed16 table
                                    // per named resource; aliases reuse that same allocation.
                                    table=(ulong)tableOwners.Count+1;tableOwners.Add(tableKey,table);
                                }
                                string directory=Path.Combine(output,"assets",$"{placement.Source.Definition.ObjectId:X8}","draw-"+drawIndex);
                                WritePacket(directory,asset,drawIndex,shader,provider,selectedPrograms.Combination.Index,vs,ps,vb,pb,bindings);
                                int template=templates.Count;templates.Add(Path.GetRelativePath(output,directory).Replace('\\','/'));
                                shadowTemplates.Add((template,named.Binding.Shader,type17,Path.GetRelativePath(output,directory)));
                                list.Add(new(drawIndex,template,table,vd,pd,spec.Value.Value,material.MaterialId.Value,alpha,inventory.DeclaredDistance,vb,pb));
                                imports.Add(new{definitionObjectId=placement.Source.Definition.ObjectId,model=asset.RawTransforms.Source.ResourceUri,
                                    drawIndex,template,tableKey,ownedTableAllocation=table,shader=shader.Source.ResourceUri,vs,ps,
                                    finalTable=named.Binding.Shader.Object.ObjectId,provider=provider.Object.ObjectId,type17,
                                    actualDistance=inventory.DeclaredDistance,ordinary.SourceEvidence,
                                    vertexWrites="mW2P0..63,mL2WT64..111,mL2W112..159",pixelWrites="cSpec0..15,mWV16..63,nMtrID64/68,optionalvATest64"});
                            }
                            catch(Exception error) when(error is InvalidDataException or NotSupportedException)
                            {failed=true;boundaries.Add(new(index,placement.Source.Definition.ObjectId,drawIndex,"asset primitive profile",error.Message));}
                        }
                    }
                    catch(Exception error) when(error is InvalidDataException or NotSupportedException)
                    {failed=true;boundaries.Add(new(index,placement.Source.Definition.ObjectId,null,"asset inventory",error.Message));}
                    profileFailures[asset.SourceKey]=failed;
                }
                int first=primitives.Count;
                foreach(var profile in list)
                {
                    var transform=source.DrawTransforms[profile.DrawIndex];
                    byte[] v=OriginalModelGbufferConstants.Vertex(profile.VertexDefinitions,profile.VertexTemplate,
                        new(Rows(initial.Camera.SourceWorldToProjection),transform.NormalRows,transform.PositionRows)).Bytes;
                    primitives.Add(new(index,profile.DrawIndex,profile.TemplateIndex,profile.TableOwner,v,profile.PixelTemplate.ToArray(),profile));
                }
                var distance=list.Count>0?list[0].Distance:OriginalModelPrimitiveInventorySource.LoadStatic(asset).DeclaredDistance;
                instances.Add(new(index,mode,obb,first,primitives.Count-first,profileFailures[asset.SourceKey],distance));
            }
            catch(Exception error) when(error is InvalidDataException or NotSupportedException)
            {boundaries.Add(new(index,placement.Source.Definition.ObjectId,null,"instance LOCAL/World/OBB",error.Message));}
        }
        AddLines(Path.Combine(output,"models.txt"),templates);
        AddText(Path.Combine(output,"catalog.txt"),FormattableString.Invariant($"{instances.Count} {primitives.Count} {templates.Count} {common.Value}\n"));
        AddText(Path.Combine(output,"sort-fields.txt"),string.Join(' ',fields)+"\n");
        CaptureBinary(Path.Combine(output,"instances.bin"),writer=>
        {
            foreach(var item in instances)
            {
                writer.Write(item.Index);writer.Write(item.Mode);writer.Write(item.FirstPrimitive);writer.Write(item.PrimitiveCount);writer.Write(item.HasUnsupported?1u:0u);
                writer.Write(item.Distance.ForceRegisterNormalZDistance1A0);writer.Write(item.Distance.RegisterNormalZDistanceBias1A4);
                foreach(var vector in new[]{item.Bounds.Center0,item.Bounds.Basis10,item.Bounds.Basis20,item.Bounds.Basis30,item.Bounds.HalfExtents40})
                    foreach(float f in new[]{vector.X,vector.Y,vector.Z,vector.W})writer.Write(f);
            }
        });
        CaptureBinary(Path.Combine(output,"primitives.bin"),writer=>
        {
            foreach(var item in primitives)
            {writer.Write(item.InstanceIndex);writer.Write(item.DrawIndex);writer.Write(item.TemplateIndex);writer.Write(item.TableOwner);writer.Write(item.Vertex);writer.Write(item.Pixel);}
        });

        AddText(Path.Combine(output,"source.json"),JsonSerializer.Serialize(new{
            scene=scene.Scene.Source.ResourceUri,sceneSha256=scene.Scene.Source.DecodedSha256,
            sceneInstanceCount=scene.Instances.Count,declaredModels=placements.Instances.Count(p=>p.Model is not null),
            excludedEffects=placements.Instances.Where(p=>p.Model is null).Select(p=>p.Source.Index),
            loadedAssets=assets.LoadedAssetCount,instanceCount=instances.Count,primitiveCount=primitives.Count,templateCount=templates.Count,
            common,sort=new{order.SourceIdentity,fields,tableOwners},imports,boundaries,
            scope="caller-published fresh static/source LOCAL/unparented; no dynamic owners or external material setter"
        },new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        AddText(Path.Combine(output,"boundaries.json"),JsonSerializer.Serialize(new{boundaries,
            affectedInstances=instances.Where(i=>i.HasUnsupported).Select(i=>i.Index)},new JsonSerializerOptions{WriteIndented=true}));
        AddText(Path.Combine(output,"shadow-source-worlds.json"),JsonSerializer.Serialize(new{
            scope="All source static candidates; caster admission and current per-view/OwnCamera/light gates are separate caller inputs",
            declarations=shadowDeclarations.Values,worlds=shadowWorlds},new JsonSerializerOptions{WriteIndented=true}));
        return new(scene,files,instances.ToArray(),primitives.ToArray(),order,templates.Count,assets.LoadedAssetCount,
            boundaries.ToArray(),shadowFields,shadowTemplates);

        void AddBytes(string path,byte[] bytes)=>files[Normalize(path)]=bytes.ToArray();
        void AddText(string path,string value)=>AddBytes(path,Encoding.UTF8.GetBytes(value));
        void AddLines(string path,IEnumerable<string> lines)
        {
            string[] rows=lines.ToArray();
            AddText(path,rows.Length==0?string.Empty:string.Join(Environment.NewLine,rows)+Environment.NewLine);
        }
        void CaptureBinary(string path,Action<BinaryWriter> encode)
        {
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
            encode(writer);AddBytes(path,stream.ToArray());
        }
        void WritePacket(string directory,OriginalMapStaticModelAsset asset,int drawIndex,LoadedShaderContainer shader,OriginalNamedModelShader provider,
            int combination,int vs,int ps,byte[] vertex,byte[] pixel,LoadedModelShaderBindings bindings)
        {
            string modelDirectory=Path.GetDirectoryName(directory)!;
            var geometry=asset.RawTransforms.Geometry;var draw=asset.Draws[drawIndex];
            foreach(var buffer in geometry.VertexBuffers)if(writtenStatic.Add(Path.Combine(modelDirectory,$"vb-{buffer.Index}.bin")))AddBytes(Path.Combine(modelDirectory,$"vb-{buffer.Index}.bin"),buffer.Data);
            foreach(var buffer in geometry.IndexBuffers)if(writtenStatic.Add(Path.Combine(modelDirectory,$"ib-{buffer.Index}.bin")))AddBytes(Path.Combine(modelDirectory,$"ib-{buffer.Index}.bin"),buffer.Data);
            AddBytes(Path.Combine(directory,"vs.dxbc"),shader.Container.GetProgramBytes(shader.Source.Bytes,vs).ToArray());
            AddBytes(Path.Combine(directory,"ps.dxbc"),shader.Container.GetProgramBytes(shader.Source.Bytes,ps).ToArray());
            AddBytes(Path.Combine(directory,"vs-b0.bin"),vertex);AddBytes(Path.Combine(directory,"ps-b0.bin"),pixel);
            var layout=geometry.Layouts[draw.LayoutIndex];
            AddLines(Path.Combine(directory,"layout.txt"),layout.Attributes.Select(a=>
            {var s=OriginalModelVertexInputs.ReadSemantic(a.SemanticCode);return FormattableString.Invariant($"{s.Name} {s.Index} {Dxgi(a.TypeCode)} {a.BufferReferenceSlot} {a.ByteOffset}");}));
            var signature=shader.ReadSignatures(vs).Parts.Single(s=>s.Kind==DxbcSignatureKind.Input);
            foreach(var input in signature.Parameters.Where(p=>p.RawSystemValue==0))
                if(layout.Attributes.Count(a=>OriginalModelVertexInputs.ReadSemantic(a.SemanticCode).Name==input.SemanticName.Text
                    &&OriginalModelVertexInputs.ReadSemantic(a.SemanticCode).Index==input.SemanticIndex)!=1)throw new NotSupportedException("Source VS input semantic is unclosed.");
            AddLines(Path.Combine(directory,"streams.txt"),layout.BufferReferences.Select((i,slot)=>$"{slot} {geometry.VertexBuffers[i].Stride} ../vb-{i}.bin"));
            var raster=OriginalModelRendererStateSourceReader.ReadGroupRaster(bindings,drawIndex)??throw new NotSupportedException("Source raster override is absent.");
            var depth=OriginalModelRendererStateSourceReader.PrepareQueueDepth(queue,initial.Camera.Lens.ReverseDepth);
            AddText(Path.Combine(directory,"draw.txt"),$"{draw.IndexCount} {draw.IndexStart} 0 4 {geometry.IndexBuffers[draw.IndexBufferIndex].BitsPerIndex}\n../ib-{draw.IndexBufferIndex}.bin\n"+
                $"{raster.NativeFillMode} {raster.NativeCullMode} {(raster.FrontCounterClockwise?1:0)}\n{(depth.DepthTest?1:0)} {(depth.DepthWrite?1:0)} {depth.CompareCode+1} 0\n");
            var plan=OriginalModelStageTextures.Resolve(asset.TextureResources,drawIndex,provider,shader,combination,new(false,false,0));
            var textureRows=new List<string>();var samplerRows=new List<string>();
            foreach(var binding in plan.Bindings)
            {
                var image=OriginalModelStageTextures.LoadImage(repo,asset.TextureResources,plan,binding.TextureRegister);
                var formats=OriginalModelTextureFormatMath.Resolve(image.Image.PixelFormat,(byte)(image.Image.RawHeader[19]&1));
                string texture=Path.Combine(output,"textures",$"{image.Container.Source.FileId:X8}-{image.Image.Index}-{formats.ShaderViewFormat}.tex");
                if(writtenStatic.Add(texture))AddBytes(texture,EncodeTexture(image,formats.ShaderViewFormat));
                textureRows.Add($"{binding.TextureRegister} {formats.ResourceFormat} {formats.ShaderViewFormat} {Path.GetRelativePath(directory,texture).Replace('\\','/')}");
                string sampler=$"ps-s{binding.SamplerRegister}.bin";AddBytes(Path.Combine(directory,sampler),image.Sampler.EncodeD3D11Descriptor());
                samplerRows.Add($"{binding.SamplerRegister} {sampler}");
            }
            AddLines(Path.Combine(directory,"textures-native.txt"),textureRows);AddLines(Path.Combine(directory,"samplers-native.txt"),samplerRows);
        }

    }

    /// <summary>Writes only this bank, with relative paths inside its own root.
    /// This is a cache preparation boundary, not a per-frame method.</summary>
    public void WriteNativeInputs(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if(BoundaryCount!=0)
            throw new NotSupportedException("Static Model input bank has unresolved source/publication profiles: "+
                string.Join("; ",Boundaries.Select(b=>$"instance{b.InstanceIndex} {b.Scope}: {b.Reason}")));
        string root=Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        foreach(var file in files)
        {
            string path=Path.Combine(root,file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path,file.Value);
        }
    }

    /// <summary>Pure CPU selection over the loaded source inventory. Used by
    /// callers that need a source plan; it performs no resource reads/hashes.</summary>
    public OriginalD3D11ModelViewPlan PrepareView(OriginalMapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var planes=TerrainFrustumMath.Create(camera.SourceWorldToProjection,camera.Lens.ReverseDepth);
        var z=OriginalWorldPrimitiveDistanceKey.PrepareViewZPlane(camera.SourceWorldToProjection,camera.Lens.Flags);
        var range=OriginalWorldPrimitiveDistanceKey.PrepareView(camera.Lens.Near,camera.Lens.Far);
        var visible=instances.Where(i=>!OriginalMapModelNativeVisibilitySources.IsCulled(planes,i.Bounds,i.Mode,[])).ToArray();
        var keys=new Dictionary<int,ulong>();var insertion=new List<int>();
        foreach(var item in visible)
        {
            float distance=item.Distance.ForceRegisterNormalZDistance1A0;
            if(distance<0)distance=OriginalWorldPrimitiveDistanceKey.ReadOrientedBoundsDistance(item.Bounds,z);
            ulong key=OriginalWorldPrimitiveDistanceKey.PrepareFirstCounter(range,distance,item.Distance.RegisterNormalZDistanceBias1A4);
            for(int p=item.FirstPrimitive;p<item.FirstPrimitive+item.PrimitiveCount;p++){insertion.Add(p);keys.Add(p,key);}
        }
        var sorted=order.Sort(OriginalWorldPrimitiveSortFamily.Opaque,insertion,(p,field)=>field==14?keys[p]:primitives[p].TableOwner);
        var draws=new List<OriginalD3D11ModelViewDraw>();
        foreach(int p in sorted)
        {
            var item=primitives[p];var profile=item.Profile;
            var v=OriginalModelGbufferConstants.Vertex(profile.VertexDefinitions,item.Vertex,new(Rows(camera.SourceWorldToProjection),
                FloatRows(item.Vertex,64,12),FloatRows(item.Vertex,112,12))).Bytes;
            var b=OriginalModelGbufferConstants.Pixel(profile.PixelDefinitions,item.Pixel,new(profile.Specular,
                OriginalMapStaticModelTransforms.ToShaderAffineRows(camera.SourceWorldToView),profile.MaterialId,profile.Alpha)).Bytes;
            draws.Add(new(p,item.InstanceIndex,item.DrawIndex,keys[p],item.TableOwner,v,b));
        }
        return new(Array.AsReadOnly(visible.Select(i=>i.Index).ToArray()),
            Array.AsReadOnly(visible.Where(i=>i.HasUnsupported).Select(i=>i.Index).ToArray()),draws.AsReadOnly());
    }
    private static string Normalize(string path)=>path.Replace('\\','/').TrimStart('.','/');
    static float[] Rows(Matrix4x4 m)=>[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
    static float[] FloatRows(byte[] bytes,int offset,int count)=>Enumerable.Range(0,count).Select(i=>BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset+i*4,4))).ToArray();
    static void RequireEmpty(DokObject item,int ordinal,uint hash,string role)
    {
        if(item.Properties.Count<=ordinal)throw new InvalidDataException(role+" source ordinal absent.");
        var property=item.Properties[ordinal];
        if(property.Index!=ordinal||property.NameHash!=hash||property.TypeCode!=5||property.ElementCount!=0||property.RawValue.Length!=0)
            throw new NotSupportedException(role+" is not its exact empty original typed array; real material override producer required.");
    }
    static uint Dxgi(ushort type)=>type switch{0=>41,1=>16,2=>6,3=>2,5=>30,7=>14,10=>34,11=>10,13=>28,_=>throw new NotSupportedException($"Source vertex type{type} unclosed.")};
    static byte[] EncodeTexture(OriginalModelStageTextureImage source,uint format)
    {
        var image=source.Image;if(image.LayerCount!=1)throw new NotSupportedException("Source view is not a single layer.");
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);foreach(uint word in new[]{(uint)image.Width,(uint)image.Height,(uint)image.LayerCount,(uint)image.MipCount,format})writer.Write(word);
        for(int mip=0;mip<image.MipCount;mip++)
        {
            var m=image.Mips[mip];byte[] bytes=image.GetEncodedMip(source.Container.Source.Bytes,mip,0).ToArray();
            int pitch=image.PixelFormat switch{0x59 or 0x5C=>Math.Max(1,(m.Width+3)/4)*8,0x5B or 0x5E or 0x5F=>Math.Max(1,(m.Width+3)/4)*16,
                0x00 or 0x01 or 0x02=>m.Width*4,0x03=>m.Width*8,0x04=>m.Width*16,_=>throw new NotSupportedException("Source texture pitch unclosed.")};
            writer.Write((uint)pitch);writer.Write((uint)bytes.Length);writer.Write((uint)bytes.Length);writer.Write(bytes);
        }
        return stream.ToArray();
    }
    record Profile(int DrawIndex,int TemplateIndex,ulong TableOwner,DxbcResourceDefinitions VertexDefinitions,DxbcResourceDefinitions PixelDefinitions,
        Vector4 Specular,int MaterialId,float? Alpha,OriginalWorldPrimitiveModelDistance Distance,byte[] VertexTemplate,byte[] PixelTemplate);
    record Instance(int Index,uint Mode,OriginalMapModelNativeOrientedBounds Bounds,int FirstPrimitive,int PrimitiveCount,bool HasUnsupported,
        OriginalWorldPrimitiveModelDistance Distance);
    record Primitive(int InstanceIndex,int DrawIndex,int TemplateIndex,ulong TableOwner,byte[] Vertex,byte[] Pixel,Profile Profile);
}
