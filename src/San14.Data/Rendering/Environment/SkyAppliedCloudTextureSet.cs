using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record SkyAppliedCloudTextureInput(uint SourceBindPoint,string TextureName,SkyTextureSource Texture);

/// <summary>Imported physical inputs for Cloud and its complete atmosphere
/// Plane. Device generations borrow these immutable image and sampler owners.</summary>
public sealed class SkyAppliedCloudTextureSet
{
    private static readonly ConditionalWeakTable<SkyAppliedSeasonCloudTextureSources,SkyAppliedCloudTextureSet> Imported=new();
    public IReadOnlyList<SkyAppliedCloudTextureInput> CloudInputs { get; }
    public IReadOnlyList<SkyTextureSource> StaInputs { get; }

    private SkyAppliedCloudTextureSet(SkyAppliedSeasonCloudTextureSources sources)
    {
        var plane=sources.RenderingPlane;
        if(sources.Noise.Entries.Count!=3||sources.Cirrus.Entries.Count!=1||plane is not null&&plane.Entries.Count<3)
            throw new InvalidDataException("Cloud requires three noise images, one cirrus image and the selected atmosphere's first three Plane entries.");
        var noise=Read(sources.Noise);
        var cirrus=Read(sources.Cirrus);
        var sta=plane is null ? Array.Empty<SkyTextureSource>() : Read(plane);
        var inputs=new List<SkyAppliedCloudTextureInput>{new(0,"sBase",noise[0]),new(1,"sDetail",noise[1]),
            new(2,"sDistort",noise[2]),new(3,"sCirrus",cirrus[0])};
        if(plane is not null)
        {
            if(sta.Length>3)inputs.Add(new(4,"sSkyAmbientAltitude2dTable",sta[3]));
            inputs.Add(new(5,"sTransmit2dTable",sta[1]));inputs.Add(new(6,"sSctr3dTable",sta[0]));
        }
        foreach(var input in inputs)
            if(input.Texture.SourceDimension!=(input.SourceBindPoint is 0 or 1 or 6 ? 8u : 4u))
                throw new InvalidDataException("Cloud image dimension does not satisfy its declared texture consumer.");
        if(plane is not null&&sta[2].SourceDimension!=4)
            throw new InvalidDataException("The atmosphere Plane entry not sampled by Cloud must still be a two-dimensional texture.");
        CloudInputs=inputs.AsReadOnly();
        StaInputs=Array.AsReadOnly(sta);
    }

    public static SkyAppliedCloudTextureSet Import(SkyAppliedSeasonCloudTextureSources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return Imported.GetValue(sources,static value=>new(value));
    }

    public bool SamePhysical(SkyAppliedCloudTextureSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CloudInputs.Count==other.CloudInputs.Count&&CloudInputs.Zip(other.CloudInputs).All(pair=>
            pair.First.SourceBindPoint==pair.Second.SourceBindPoint&&Same(pair.First.Texture,pair.Second.Texture))&&SameSta(other);
    }

    public bool SameSta(SkyAppliedCloudTextureSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return StaInputs.Count==other.StaInputs.Count&&StaInputs.Zip(other.StaInputs).All(pair=>Same(pair.First,pair.Second));
    }

    private static bool Same(SkyTextureSource left,SkyTextureSource right)
        => left.Identity==right.Identity&&left.SamplerDescriptorSha256==right.SamplerDescriptorSha256;

    private static SkyTextureSource[] Read(SkyCloudSourceTextureGroup group)
        => group.Entries.Select((entry,index)=>SkyTextureSource.Import(entry.Resource,entry.ImageIndex,
            group.ReadSourceShaderViewFormat(index),group.ReadSourceSamplerDescriptor(index))).ToArray();
}
