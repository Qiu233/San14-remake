using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The immutable primary model closure of one resource generation.
/// Loading requires neither a World nor a camera or current record routing.</summary>
public sealed class D3D11CityGateStaticResources
{
    internal IReadOnlyList<D3D11CityGateStaticAsset> Assets { get; }
    internal uint CommonFrustumMode { get; }
    internal IReadOnlyList<int> SortFields { get; }
    public string Directory { get; }
    public D3D11PrimaryForwardStaticResources Forward { get; }
    readonly IReadOnlyDictionary<string,D3D11PrimaryShadowStaticResources> shadows;

    private D3D11CityGateStaticResources(string directory,List<D3D11CityGateStaticAsset> assets,
        uint commonMode,IReadOnlyList<int> sortFields,D3D11PrimaryForwardStaticResources forward,
        IReadOnlyDictionary<string,D3D11PrimaryShadowStaticResources> shadows)
    {
        (Directory,Assets,CommonFrustumMode,SortFields,Forward)=
            (Path.GetFullPath(directory),assets.AsReadOnly(),commonMode,sortFields,forward);
        this.shadows=shadows;
    }

    public D3D11PrimaryShadowStaticResources RequireShadow(string locale)
        =>shadows.TryGetValue(locale,out var resources)?resources:
            throw new InvalidDataException("Primary shadow resources were not prepared for locale "+locale+".");

    /// <summary>Publishes static media before Title admission. The caller owns
    /// a unique directory for this generation and retains it while borrowed.</summary>
    public static D3D11CityGateStaticResources Prepare(GameResourceRepository repository,
        LoadedSceneResources scene,string directory,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
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
        var profiles=new List<D3D11CityGateStaticAsset>();var modelDirectories=new List<string>();
        var forwardSources=new List<D3D11PrimaryForwardTemplateSource>();
        var shadowSources=new List<D3D11PrimaryShadowTemplateSource>();
        for(int category=11;category<=88;category++)
        {
            token.ThrowIfCancellationRequested();
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
                var caller=DeferredEarlyShadowCasterAdmissionSource.LoadStatic(asset);
                var bindings=ModelShaderResources.LoadBindings(repository,asset.TextureResources);
                var root=UnitWorldTransform.ConstructDefinition(asset.TextureResources);
                var locals=MapStaticModelPrimitiveLocalSource.Prepare(asset);
                var draws=new List<D3D11CityGateStaticDraw>();
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
                    int template=modelDirectories.Count;
                    string target=$"assets/category-{category}/draw-{drawIndex}";
                    string metadata=writer.WriteStaticPacket(target,$"meshes/{asset.RawTransforms.Source.FileId:X8}",asset,drawIndex,shader,provider,
                        program.Combination.Index,vs,ps,bindings);
                    modelDirectories.Add(target);
                    forwardSources.Add(new(template,named.Binding.Shader,type17,target+"/layout.txt"));
                    shadowSources.Add(new(template,named.Binding.Shader,type17,target+"/layout.txt"));
                    string tableKey=named.Binding.Shader.Objects.Source.ResourceUri+"/"+named.Binding.Shader.Object.ObjectId.ToString("X8");
                    draws.Add(new(drawIndex,template,target,queue,tableKey,named.Binding.Shader.Object.ObjectId,
                        shader.Source.FileId,vs,ps,vd,pd,transform.NormalRows,transform.PositionRows,
                        specular.Value.Value,material.MaterialId.Value,alpha,metadata));
                }
                profiles.Add(new(profiles.Count,category,asset.SourceKey,caller.Exclusion78,inventory.DeclaredDistance,draws.AsReadOnly()));
            }
            catch(Exception error)when(error is InvalidDataException or NotSupportedException)
            {throw new NotSupportedException($"Primary category{category} resource preparation failed: {error.Message}",error);}
        }
        var forward=D3D11PrimaryForwardStaticResources.Prepare(repository,forwardSources,files,token);
        string outputRoot=Path.GetFullPath(directory);
        var shadows=new Dictionary<string,D3D11PrimaryShadowStaticResources>(StringComparer.Ordinal);
        foreach(string locale in new[]{"TC","SC"})
            shadows.Add(locale,D3D11PrimaryShadowStaticResources.Prepare(repository,shadowSources,files,
                locale,Path.Combine(outputRoot,"shadow",locale),token));
        System.IO.Directory.CreateDirectory(outputRoot);
        foreach(var file in files)
        {
            token.ThrowIfCancellationRequested();
            string path=Path.Combine(outputRoot,file.Key);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path,file.Value);
        }
        return new(outputRoot,profiles,commonMode,sortFields,forward,shadows);
    }

    static void RequireEmpty(DokObject item,int ordinal,uint hash,string role)
    {
        var property=item.Properties.Single(p=>p.Index==ordinal&&p.NameHash==hash);
        if(property.TypeCode!=5||property.ElementCount!=0||property.RawValue.Length!=0)
            throw new NotSupportedException(role+" needs its actual nonempty source producer.");
    }
}

internal sealed record D3D11CityGateStaticAsset(int Index,int Category,string SourceKey,uint Exclusion,
    WorldPrimitiveModelDistance Distance,IReadOnlyList<D3D11CityGateStaticDraw> Draws);
internal sealed record D3D11CityGateStaticDraw(int Draw,int Template,string RelativeDirectory,ModelQueueStateSource Queue,
    string TableKey,uint FinalTable,uint ShaderFile,int VertexIndex,int PixelIndex,
    DxbcResourceDefinitions VertexDefinitions,DxbcResourceDefinitions PixelDefinitions,
    float[] NormalRows,float[] PositionRows,Vector4 Specular,int MaterialId,float? Alpha,string DrawMetadata);
