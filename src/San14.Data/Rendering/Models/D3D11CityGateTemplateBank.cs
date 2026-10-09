using System.Collections.ObjectModel;
using System.Text.Json;

namespace San14.Data;

public sealed record D3D11CityGatePreparedAsset(int Index,int Category,string AssetSourceKey,
    IReadOnlyList<int> DrawIndices,IReadOnlyList<int> TemplateIndices);

/// <summary>Import-time managed binding for the same CSP1/models.txt bank used
/// by NativeCityPopulation. It does not create GPU objects. The native owner
/// must load Directory before a publication using these profile indices.</summary>
public sealed class D3D11CityGateTemplateBank
{
    readonly IReadOnlyDictionary<string,D3D11CityGatePreparedAsset> assets;
    readonly IReadOnlyList<D3D11CityGatePreparedAsset> profiles;
    readonly Dictionary<string,D3D11CityGatePreparedAsset> bound=[];
    public string Directory{get;}
    public int AssetCount{get;}
    public int TemplateCount{get;}
    public int MaxDraws{get;}
    public IReadOnlyList<D3D11CityGatePreparedAsset> Profiles=>profiles;
    D3D11CityGateTemplateBank(string directory,int assetCount,int templateCount,int maxDraws,
        IReadOnlyDictionary<string,D3D11CityGatePreparedAsset> sourceProfiles)
    {
        (Directory,AssetCount,TemplateCount,MaxDraws,assets)=(directory,assetCount,templateCount,maxDraws,sourceProfiles);
        profiles=Array.AsReadOnly(sourceProfiles.Values.OrderBy(p=>p.Index).ToArray());
    }

    public static D3D11CityGateTemplateBank LoadImported(string directory)
    {
        directory=Path.GetFullPath(directory);string[] models=File.ReadAllLines(Path.Combine(directory,"models.txt"));
        using var stream=File.OpenRead(Path.Combine(directory,"asset-profiles.bin"));using var read=new BinaryReader(stream);
        if(read.ReadUInt32()!=0x31505343u)throw new InvalidDataException("Current City/Gate bank needs original CSP1 source profiles.");
        int count=read.ReadInt32(),max=read.ReadInt32();if(count<=0||max<=0)throw new InvalidDataException("Current primary source bank is empty.");
        var profiles=new Dictionary<int,(int Category,int[] Draws,int[] Templates)>();
        for(int index=0;index<count;index++){
            if(read.ReadInt32()!=index)throw new InvalidDataException("Current primary bank profile order differs.");
            int category=read.ReadInt32(),drawCount=read.ReadInt32();uint unsupported=read.ReadUInt32();
            _=read.ReadUInt32();_=read.ReadSingle();_=read.ReadSingle();
            if(unsupported!=0||drawCount<0||drawCount>max)throw new InvalidDataException("Current primary bank contains an unsupported source profile.");
            var draws=new int[drawCount];var templates=new int[drawCount];
            for(int draw=0;draw<drawCount;draw++){
                draws[draw]=read.ReadInt32();templates[draw]=read.ReadInt32();int queue=read.ReadInt32();
                _=read.ReadUInt64();_=read.ReadUInt32();uint records=read.ReadUInt32();
                _=read.ReadUInt32();_=read.ReadUInt32();_=read.ReadUInt32();
                if(templates[draw]<0||templates[draw]>=models.Length||queue is not(54 or 55)||records==0)
                    throw new InvalidDataException("Current primary source template/ordinary route is unclosed.");
                if(read.ReadBytes(240).Length!=240)throw new InvalidDataException("Current primary full160/80CB template truncated.");
            }
            profiles.Add(index,(category,draws,templates));
        }
        if(stream.Position!=stream.Length)throw new InvalidDataException("Current primary bank has trailing source fields.");
        var keys=new Dictionary<int,string>();
        string bindings=Path.Combine(directory,"asset-bindings.json");
        bool migrate=!File.Exists(bindings);
        if(!migrate){
            using var json=JsonDocument.Parse(File.ReadAllBytes(bindings));var root=json.RootElement;
            if(root.GetProperty("Version").GetInt32()!=1)throw new InvalidDataException("Unsupported primary asset binding version.");
            foreach(var asset in root.GetProperty("Assets").EnumerateArray()){
                int index=asset.GetProperty("Index").GetInt32();
                if(asset.GetProperty("Category").GetInt32()!=profiles[index].Category||
                    !asset.GetProperty("DrawIndices").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual(profiles[index].Draws))
                    throw new InvalidDataException("Primary asset binding differs from its CSP1 category/draw order.");
                keys.Add(index,asset.GetProperty("AssetSourceKey").GetString()!);
            }
        }else{
            // Read an old research cache only at this loading boundary, then
            // retain its complete bindings locally. Future loads do not need
            // the developer's donor directory or its observation source.json.
            string source=Path.Combine(directory,"source.json");
            if(File.Exists(source))ReadLegacy(source);
            string addition=Path.Combine(directory,"template-imports.json");
            if(File.Exists(addition)){
                using var json=JsonDocument.Parse(File.ReadAllBytes(addition));var root=json.RootElement;
                ReadLegacy(Path.Combine(root.GetProperty("donor").GetString()!,"source.json"));
                foreach(var asset in root.GetProperty("prepared").EnumerateArray()){
                    int index=asset.GetProperty("Index").GetInt32();keys[index]=asset.GetProperty("asset").GetString()!;
                    if(!asset.GetProperty("DrawIndices").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual(profiles[index].Draws))
                        throw new InvalidDataException("Current prepared asset declaration/draw source differs from CSP1.");
                }
            }
        }
        if(keys.Count!=count)throw new InvalidDataException("Complete source asset/profile binding unavailable.");
        var result=keys.ToDictionary(p=>p.Value,p=>new D3D11CityGatePreparedAsset(p.Key,profiles[p.Key].Category,p.Value,
            Array.AsReadOnly(profiles[p.Key].Draws),Array.AsReadOnly(profiles[p.Key].Templates)),StringComparer.Ordinal);
        if(migrate){
            var document=new{Version=1,Assets=result.Values.OrderBy(asset=>asset.Index).Select(asset=>new{
                asset.Index,asset.Category,asset.AssetSourceKey,asset.DrawIndices}).ToArray()};
            string temporary=bindings+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temporary,JsonSerializer.Serialize(document,new JsonSerializerOptions{WriteIndented=true}));
            File.Move(temporary,bindings,overwrite:true);
        }
        return new(directory,count,models.Length,max,new ReadOnlyDictionary<string,D3D11CityGatePreparedAsset>(result));
        void ReadLegacy(string path){
            using var json=JsonDocument.Parse(File.ReadAllBytes(path));var root=json.RootElement;
            var categoryProfiles=root.GetProperty("owners").EnumerateArray().GroupBy(o=>o.GetProperty("Category").GetInt32())
                .ToDictionary(g=>g.Key,g=>g.Select(o=>o.GetProperty("assetProfile").GetInt32()).Distinct().Single());
            foreach(var group in root.GetProperty("imports").EnumerateArray().GroupBy(i=>i.GetProperty("asset").GetString()!)){
                string key=group.Key;int category;
                if(key.StartsWith("city-category:",StringComparison.Ordinal))category=int.Parse(key.AsSpan(14,key.IndexOf('|')-14));
                else if(key.StartsWith("catalog-reference:gate-category:",StringComparison.Ordinal)){
                    const int start=32;category=int.Parse(key.AsSpan(start,key.IndexOf(':',start)-start));}
                else throw new InvalidDataException("Imported legacy primary asset source role is unclosed.");
                int index=categoryProfiles[category];
                if(profiles[index].Category!=category||!group.Select(i=>i.GetProperty("drawIndex").GetInt32()).SequenceEqual(profiles[index].Draws))
                    throw new InvalidDataException("Imported primary legacy asset/CSP1 draw profile differs.");
                keys[index]=key;
            }
        }
    }
    /// <summary>One original asset import/cache miss validates its source
    /// identity and complete Category0 order. Normal packet captures reuse it.</summary>
    public D3D11CityGatePreparedAsset? Bind(MapStaticModelAsset asset,int category)
    {
        if(bound.TryGetValue(asset.SourceKey,out var cached))return cached;
        if(!assets.TryGetValue(asset.SourceKey,out var profile))return null;
        if(profile.Category!=category||!ModelPrimitiveInventorySource.LoadStatic(asset).Category0.SourceDrawIndices.SequenceEqual(profile.DrawIndices))
            throw new InvalidDataException("Current original asset does not match its prepared category/Category0 order.");
        bound.Add(asset.SourceKey,profile);return profile;
    }
    public byte[] Encode(D3D11CityGatePrimaryFrame frame,uint commonFrustumMode)
    {
        if(frame.Unavailable.Count!=0||frame.Owners.Count!=61)throw new InvalidDataException("Current City/Gate complete source61 publication unavailable.");
        using var output=new MemoryStream();using var w=new BinaryWriter(output);w.Write(0x31555043u);w.Write(frame.Owners.Count);
        foreach(var owner in frame.Owners){var profile=Bind(owner.Instance.Asset,owner.Category)??throw new InvalidDataException("Actual current primary source asset import missing.");
            w.Write(owner.Index);w.Write(profile.Index);w.Write(1u);w.Write(owner.Caller.Mask328);w.Write(owner.Caller.Mask32C);w.Write(owner.Caller.Mask330);
            w.Write(owner.Caller.ActualEligibilityV408?1u:0u);w.Write(owner.Caller.Exclusion78);w.Write(owner.Caller.WorldCasterFlags10C);w.Write(owner.Opacity);
            w.Write(MapModelNativeVisibilitySources.ResolveFrustumCullingType(owner.Prefix.ConstructorFrustumCullingType33C,commonFrustumMode));
            foreach(var v in new[]{owner.Bounds.Center0,owner.Bounds.Basis10,owner.Bounds.Basis20,owner.Bounds.Basis30,owner.Bounds.HalfExtents40}){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);w.Write(v.W);}
            w.Write(profile.DrawIndices.Count);foreach(int draw in profile.DrawIndices){w.Write(draw);var t=owner.Instance.DrawTransforms[draw];foreach(float f in t.NormalRows)w.Write(f);foreach(float f in t.PositionRows)w.Write(f);}
        }
        return output.ToArray();
    }
}
