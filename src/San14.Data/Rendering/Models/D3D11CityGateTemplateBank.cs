using System.Collections.ObjectModel;

namespace San14.Data;

public sealed record D3D11CityGatePreparedAsset(int Index,int Category,string AssetSourceKey,
    IReadOnlyList<int> DrawIndices,IReadOnlyList<int> TemplateIndices);

/// <summary>Resource-prepared City/Gate/Area template bank. Immutable geometry,
/// programs and materials are separate from current population publications.</summary>
public sealed class D3D11CityGateTemplateBank
{
    readonly IReadOnlyDictionary<string,D3D11CityGatePreparedAsset> assets;
    readonly IReadOnlyList<D3D11CityGatePreparedAsset> profiles;
    readonly IReadOnlyDictionary<string,byte[]> files;
    readonly Dictionary<string,D3D11CityGatePreparedAsset> bound=[];
    public string Directory{get;}
    public int AssetCount=>profiles.Count;
    public int TemplateCount{get;}
    public int MaxDraws{get;}
    public IReadOnlyList<D3D11CityGatePreparedAsset> Profiles=>profiles;
    public IReadOnlyDictionary<string,ulong> ShaderTableOwners{get;}

    internal D3D11CityGateTemplateBank(string directory,int templateCount,int maxDraws,
        IEnumerable<D3D11CityGatePreparedAsset> sourceProfiles,Dictionary<string,byte[]> media,
        Dictionary<string,ulong> shaderTableOwners)
    {
        Directory=Path.GetFullPath(directory);TemplateCount=templateCount;MaxDraws=maxDraws;
        profiles=Array.AsReadOnly(sourceProfiles.OrderBy(p=>p.Index).ToArray());
        assets=new ReadOnlyDictionary<string,D3D11CityGatePreparedAsset>(profiles.ToDictionary(p=>p.AssetSourceKey,StringComparer.Ordinal));
        files=new ReadOnlyDictionary<string,byte[]>(media);
        ShaderTableOwners=new ReadOnlyDictionary<string,ulong>(shaderTableOwners);
    }

    public static D3D11CityGateTemplateBank Prepare(GameResourceRepository repository,LoadedSceneResources scene,
        MapCameraConstructionPacket initialCamera,MapCommonRenderState common,uint contextFlags3E4,
        IReadOnlyList<MapWorldKind1SourceMetadata> orderedRecords,IReadOnlyDictionary<string,ulong> fieldShaderTableOwners,
        string directory)
        =>D3D11CityGateInputPreparation.Prepare(repository,scene,initialCamera,common,contextFlags3E4,
            orderedRecords,fieldShaderTableOwners,directory);

    /// <summary>Write this generation's imported resources. No old cache or
    /// current population, registered caller, census or donor file is read.</summary>
    public void WriteNativeInputs()
    {
        System.IO.Directory.CreateDirectory(Directory);
        foreach(var file in files)
        {
            string path=Path.Combine(Directory,file.Key);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path,file.Value);
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
