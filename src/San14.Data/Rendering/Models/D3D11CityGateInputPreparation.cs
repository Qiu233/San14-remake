using System.Numerics;
using System.Text;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Import the complete declared City/Gate/Area/facility primary resource roster.
/// Current population, registration, visibility and counters belong to their
/// normal runtime owners and are not supplied by this static bank.</summary>
public static class D3D11CityGateInputPreparation
{
    public static D3D11CityGateTemplateBank Prepare(GameResourceRepository repository,LoadedSceneResources scene,
        MapCameraConstructionPacket initialCamera,MapCommonRenderState common,uint contextFlags3E4,
        IReadOnlyList<MapWorldKind1SourceMetadata> orderedRecords,IReadOnlyDictionary<string,ulong> fieldShaderTableOwners,
        string directory)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(initialCamera);ArgumentNullException.ThrowIfNull(common);
        ArgumentNullException.ThrowIfNull(orderedRecords);ArgumentNullException.ThrowIfNull(fieldShaderTableOwners);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if(orderedRecords.Count is <1 or >32||common.ModelGroupSelector is not byte modelSelector||
            common.ModelExclusionMask is not uint commonExclusion)
            throw new NotSupportedException("Primary resource routing requires complete declared records and current Common model settings.");
        var assets=new MapStaticModelAssetCatalog(repository,scene);
        var graph=repository.LoadRenderGraph("system",0x024496EE);
        var shaderObjects=repository.LoadObjects("system",0x718CCB8B);
        var queue54=ModelRendererStateSourceReader.ReadQueue(graph,54);
        var queue55=ModelRendererStateSourceReader.ReadQueue(graph,55);
        var order=WorldPrimitiveOrderSource.LoadDefaultView0(repository);
        var sortFields=WorldPrimitiveSortDescriptor.ReadActualFields(order.Descriptor(WorldPrimitiveSortFamily.Opaque).ToArray());
        var commonMode=MapModelNativeVisibilitySources.ReadCommonViewTemplate(
            repository.LoadObjects("KIDSSystemResource",0x6D011726),0x899F101B).Value;
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        var writer=new D3D11ModelResourceWriter(repository,files);
        var tables=fieldShaderTableOwners.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        ulong nextTable=tables.Values.DefaultIfEmpty(0UL).Max();
        var profiles=new List<AssetProfile>();var modelDirectories=new List<string>();
        for(int category=11;category<=88;category++)
        {
            try
            {
                var asset=category<=54?assets.LoadCityCategory(category):category<=58?assets.LoadGate(category):
                    category<=60?assets.LoadCapitalCategory(category):assets.LoadFacilityCategory(category);
                var definition=asset.TextureResources.CatalogDefinition??throw new InvalidDataException("Primary WorldPQ definition is absent.");
                RequireEmpty(asset.TextureResources.ModelObject,43,0x6C3B792D,"Model material overrides");
                RequireEmpty(definition,34,0x34CF9E5C,"World material pair bank");
                RequireEmpty(definition,51,0xCEE5DDAE,"World material groups");
                RequireEmpty(definition,52,0x5EDA237E,"World material objects");
                RequireEmpty(definition,36,0xE4706CC5,"World child names");
                RequireEmpty(definition,37,0x55460C2F,"World child object names");
                var inventory=ModelPrimitiveInventorySource.LoadStatic(asset);
                if(inventory.Category1.Count!=0)throw new NotSupportedException("Primary Category1 needs its independent publication producer.");
                var caller=DeferredEarlyShadowCasterAdmissionSource.LoadStatic(asset).CaptureRetainedFresh();
                var bindings=ModelShaderResources.LoadBindings(repository,asset.TextureResources);
                var root=UnitWorldTransform.ConstructDefinition(asset.TextureResources);
                var locals=MapStaticModelPrimitiveLocalSource.Prepare(asset);
                var draws=new List<DrawProfile>();
                foreach(int drawIndex in inventory.Category0.SourceDrawIndices)
                {
                    var draw=asset.Draws[drawIndex];_=G1ModelTriangleReader.Read(asset.RawTransforms.Geometry,draw);
                    var named=ModelShaderResources.ResolveDrawBaseSource(bindings,drawIndex,shaderObjects);
                    var selected=ModelShaderResources.ResolveType16Interface(named.Binding.Shader,queue54.ShaderStateOffset);
                    var provider=selected.Provider??throw new NotSupportedException("Primary class18 provider is absent.");
                    var shader=ModelShaderResources.LoadDeclaredG1S(repository,provider);
                    var queue=ModelOrdinaryQueueSelectionSource.ReadFreshSelected(queue54,selected,shader).SourceQueueSelectsShader?queue54:queue55;
                    if(!ModelOrdinaryQueueSelectionSource.ReadFreshSelected(queue,selected,shader).SourceQueueSelectsShader)
                        throw new NotSupportedException("Primary renderer does not select ordinary54 or55.");
                    var type17=ModelMatrixBindingSourceReader.PrepareUnboundGroup(bindings,drawIndex)
                        ??throw new NotSupportedException("Primary needs its indexed/nonrigid matrix producer.");
                    var matrix=ModelMatrixBindingSourceReader.Select(shader,type17);
                    var selection=ModelShaderSelectionSourceReader.Read(provider,shader);
                    var program=ModelShaderSelectionSourceReader.ResolveSingleOtherDimensionsInitialized(selection,
                        ModelShaderSelectionSourceReader.ReadInitialCache(selection),matrix,0);
                    int vs=program.Combination.Stages.Single(p=>p.StageCode==1).Entry!.ProgramIndex;
                    int ps=program.Combination.Stages.Single(p=>p.StageCode==0).Entry!.ProgramIndex;
                    var vd=shader.ReadResourceDefinitions(vs);var pd=shader.ReadResourceDefinitions(ps);
                    var material=ModelGbufferMaterialSourceReader.Read(shader,ModelShaderResources.ResolveDrawMaterials(bindings,drawIndex));
                    var specular=ModelGbufferMaterialSourceReader.PrepareSpecular(material,0);
                    if(!specular.WritesValue||specular.Value is null||material.MaterialId is null)
                        throw new NotSupportedException("Primary material values require an independent producer.");
                    float? alpha=pd.Buffers.Single().Variables.Any(v=>v.Name.Text=="vATest")?
                        ModelGbufferMaterialSourceReader.PrepareAlphaTest(material.SourceAlphaReference,queue.BackendAlphaEnabled):null;
                    var local=locals[drawIndex];
                    var transform=local.Rows is {} localRows?
                        root.ShaderTransformWithPrimitiveLocalRows(localRows,local.EvidenceUri,local.EvidenceSha256):root.ShaderTransform();
                    byte[] vertex=ModelGbufferConstants.Vertex(vd,new byte[160],new(Rows(initialCamera.SourceWorldToProjection),
                        transform.NormalRows,transform.PositionRows)).Bytes;
                    byte[] pixel=ModelGbufferConstants.Pixel(pd,new byte[80],new(specular.Value.Value,
                        MapStaticModelTransforms.ToShaderAffineRows(initialCamera.SourceWorldToView),material.MaterialId.Value,alpha)).Bytes;
                    uint membership=0;ModelOrdinaryStaticStencilRule? route=null;
                    for(int record=0;record<orderedRecords.Count;record++)
                    {
                        uint mask=orderedRecords[record].DeclaredGroupMask;
                        if((mask&(caller.Exclusion78|commonExclusion))!=0)continue;
                        var candidate=ModelOrdinaryDispatcherStencilSource.PrepareResourceRoute(queue,mask,modelSelector,contextFlags3E4);
                        if(!candidate.RecordSelected)continue;
                        if(route is not null&&route!=candidate)
                            throw new NotSupportedException("Primary template needs per-record stencil state for distinct admitted rules.");
                        route=candidate;membership|=1u<<record;
                    }
                    // A declared asset may currently have no ordinary record
                    // membership. Keep its resources and the real queue rule;
                    // only an actual runtime population can submit a draw.
                    route??=ModelOrdinaryDispatcherStencilSource.PrepareResourceRoute(queue,
                        orderedRecords[0].DeclaredGroupMask,modelSelector,contextFlags3E4);
                    string tableKey=named.Binding.Shader.Objects.Source.ResourceUri+"/"+named.Binding.Shader.Object.ObjectId.ToString("X8");
                    if(!tables.TryGetValue(tableKey,out ulong table)){table=checked(++nextTable);tables.Add(tableKey,table);}
                    int template=modelDirectories.Count;
                    string target=$"assets/category-{category}/draw-{drawIndex}";
                    writer.WritePacket(target,$"meshes/{asset.RawTransforms.Source.FileId:X8}",asset,drawIndex,shader,provider,
                        program.Combination.Index,vs,ps,vertex,pixel,bindings,queue,initialCamera.Lens.ReverseDepth,
                        route.StencilEnabled,route.Reference);
                    modelDirectories.Add(target);
                    draws.Add(new(drawIndex,template,queue.Node.Index,table,named.Binding.Shader.Object.ObjectId,membership,
                        shader.Source.FileId,vs,ps,vertex,pixel));
                }
                profiles.Add(new(profiles.Count,category,asset.SourceKey,caller.Exclusion78,inventory.DeclaredDistance,draws));
            }
            catch(Exception error)when(error is InvalidDataException or NotSupportedException)
            {throw new NotSupportedException($"Primary category{category} resource preparation failed: {error.Message}",error);}
        }
        int maxDraws=profiles.Max(p=>p.Draws.Count);
        using(var stream=new MemoryStream())
        {
            using(var output=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                output.Write(0x31505343u);output.Write(profiles.Count);output.Write(maxDraws);
                foreach(var profile in profiles)
                {
                    output.Write(profile.Index);output.Write(profile.Category);output.Write(profile.Draws.Count);output.Write(0u);
                    output.Write(profile.Exclusion);output.Write(profile.Distance.ForceRegisterNormalZDistance1A0);
                    output.Write(profile.Distance.RegisterNormalZDistanceBias1A4);
                    foreach(var draw in profile.Draws)
                    {
                        output.Write(draw.Draw);output.Write(draw.Template);output.Write(draw.Queue);output.Write(draw.Table);
                        output.Write(draw.FinalTable);output.Write(draw.RecordMembership);output.Write(draw.ShaderFile);
                        output.Write(draw.VertexIndex);output.Write(draw.PixelIndex);output.Write(draw.Vertex);output.Write(draw.Pixel);
                    }
                }
            }
            files.Add("asset-profiles.bin",stream.ToArray());
        }
        var prepared=profiles.Select(p=>new D3D11CityGatePreparedAsset(p.Index,p.Category,p.SourceKey,
            Array.AsReadOnly(p.Draws.Select(d=>d.Draw).ToArray()),Array.AsReadOnly(p.Draws.Select(d=>d.Template).ToArray()))).ToArray();
        Text("models.txt",string.Join('\n',modelDirectories)+"\n");
        Text("template-catalog.txt",$"{modelDirectories.Count} {commonMode}\n");
        Text("sort-fields.txt",string.Join(' ',sortFields)+"\n");
        Text("record-count.txt",orderedRecords.Count+"\n");
        Text("asset-bindings.json",JsonSerializer.Serialize(new{Version=1,Assets=prepared},new JsonSerializerOptions{WriteIndented=true}));
        return new(directory,modelDirectories.Count,maxDraws,prepared,files,tables);
        void Text(string path,string value)=>files.Add(path,Encoding.UTF8.GetBytes(value));
    }

    static float[] Rows(Matrix4x4 m)=>[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
    static void RequireEmpty(DokObject item,int ordinal,uint hash,string role)
    {
        var property=item.Properties.Single(p=>p.Index==ordinal&&p.NameHash==hash);
        if(property.TypeCode!=5||property.ElementCount!=0||property.RawValue.Length!=0)
            throw new NotSupportedException(role+" needs its actual nonempty source producer.");
    }
    sealed record AssetProfile(int Index,int Category,string SourceKey,uint Exclusion,WorldPrimitiveModelDistance Distance,List<DrawProfile> Draws);
    sealed record DrawProfile(int Draw,int Template,int Queue,ulong Table,uint FinalTable,uint RecordMembership,
        uint ShaderFile,int VertexIndex,int PixelIndex,byte[] Vertex,byte[] Pixel);
}
