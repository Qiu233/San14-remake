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
        =>Prepare(D3D11CityGateStaticResources.Prepare(repository,scene,
            Path.Combine(directory,"static-media")),initialCamera,common,contextFlags3E4,orderedRecords,fieldShaderTableOwners,directory);

    public static D3D11CityGateTemplateBank Prepare(D3D11CityGateStaticResources sources,
        MapCameraConstructionPacket initialCamera,MapCommonRenderState common,uint contextFlags3E4,
        IReadOnlyList<MapWorldKind1SourceMetadata> orderedRecords,IReadOnlyDictionary<string,ulong> fieldShaderTableOwners,
        string directory)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(initialCamera);ArgumentNullException.ThrowIfNull(common);
        ArgumentNullException.ThrowIfNull(orderedRecords);ArgumentNullException.ThrowIfNull(fieldShaderTableOwners);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if(orderedRecords.Count is <1 or >32||common.ModelGroupSelector is not byte modelSelector||
            common.ModelExclusionMask is not uint commonExclusion)
            throw new NotSupportedException("Primary resource routing requires complete declared records and current Common model settings.");
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        var tables=fieldShaderTableOwners.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        ulong nextTable=tables.Values.DefaultIfEmpty(0UL).Max();
        var profiles=new List<AssetProfile>();var modelDirectories=new List<string>();
        foreach(var asset in sources.Assets)
        {
            var draws=new List<DrawProfile>();
            foreach(var source in asset.Draws)
            {
                byte[] vertex=ModelGbufferConstants.Vertex(source.VertexDefinitions,new byte[160],
                    new(Rows(initialCamera.SourceWorldToProjection),source.NormalRows,source.PositionRows)).Bytes;
                byte[] pixel=ModelGbufferConstants.Pixel(source.PixelDefinitions,new byte[80],new(source.Specular,
                    MapStaticModelTransforms.ToShaderAffineRows(initialCamera.SourceWorldToView),source.MaterialId,source.Alpha)).Bytes;
                uint membership=0;ModelOrdinaryStaticStencilRule? route=null;
                for(int record=0;record<orderedRecords.Count;record++)
                {
                    uint mask=orderedRecords[record].DeclaredGroupMask;
                    if((mask&(asset.Exclusion|commonExclusion))!=0)continue;
                    var candidate=ModelOrdinaryDispatcherStencilSource.PrepareResourceRoute(source.Queue,mask,modelSelector,contextFlags3E4);
                    if(!candidate.RecordSelected)continue;
                    if(route is not null&&route!=candidate)
                        throw new NotSupportedException("Primary template needs per-record stencil state for distinct admitted rules.");
                    route=candidate;membership|=1u<<record;
                }
                route??=ModelOrdinaryDispatcherStencilSource.PrepareResourceRoute(source.Queue,
                    orderedRecords[0].DeclaredGroupMask,modelSelector,contextFlags3E4);
                if(!tables.TryGetValue(source.TableKey,out ulong table)){table=checked(++nextTable);tables.Add(source.TableKey,table);}
                string target=source.RelativeDirectory;
                // Initial CB bytes travel once in asset-profiles.bin to the
                // primary population consumer with its actual routing metadata.
                Text(target+"/static-media-root.txt",Path.Combine(sources.Directory,target)+"\n");
                var depth=ModelRendererStateSourceReader.PrepareQueueDepth(source.Queue,initialCamera.Lens.ReverseDepth);
                Text(target+"/draw.txt",source.DrawMetadata+D3D11ModelResourceWriter.EncodeDepth(depth,route.StencilEnabled,route.Reference));
                modelDirectories.Add(target);
                draws.Add(new(source.Draw,source.Template,source.Queue.Node.Index,table,source.FinalTable,membership,
                    source.ShaderFile,source.VertexIndex,source.PixelIndex,vertex,pixel));
            }
            profiles.Add(new(asset.Index,asset.Category,asset.SourceKey,asset.Exclusion,asset.Distance,draws));
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
        Text("template-catalog.txt",$"{modelDirectories.Count} {sources.CommonFrustumMode}\n");
        Text("sort-fields.txt",string.Join(' ',sources.SortFields)+"\n");
        Text("record-count.txt",orderedRecords.Count+"\n");
        Text("asset-bindings.json",JsonSerializer.Serialize(new{Version=1,Assets=prepared},new JsonSerializerOptions{WriteIndented=true}));
        return new(directory,modelDirectories.Count,maxDraws,prepared,files,tables,sources);
        void Text(string path,string value)=>files.Add(path,Encoding.UTF8.GetBytes(value));
    }

    static float[] Rows(Matrix4x4 m)=>[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
    sealed record AssetProfile(int Index,int Category,string SourceKey,uint Exclusion,WorldPrimitiveModelDistance Distance,List<DrawProfile> Draws);
    sealed record DrawProfile(int Draw,int Template,int Queue,ulong Table,uint FinalTable,uint RecordMembership,
        uint ShaderFile,int VertexIndex,int PixelIndex,byte[] Vertex,byte[] Pixel);
}
