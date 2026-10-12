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
        var plane=sources.RequireAtmospherePlane();
        if(sources.Noise.Entries.Count!=3||sources.Cirrus.Entries.Count!=1||plane.Entries.Count!=4)
            throw new InvalidDataException("Cloud requires three noise images, one cirrus image and four atmosphere Plane entries.");
        var noise=Read(sources.Noise);
        var cirrus=Read(sources.Cirrus);
        var sta=Read(plane);
        SkyAppliedCloudTextureInput[] inputs=[new(0,"sBase",noise[0]),new(1,"sDetail",noise[1]),
            new(2,"sDistort",noise[2]),new(3,"sCirrus",cirrus[0]),
            new(4,"sSkyAmbientAltitude2dTable",sta[3]),new(5,"sTransmit2dTable",sta[1]),new(6,"sSctr3dTable",sta[0])];
        uint[] dimensions=[8,8,4,4,4,4,8];
        for(int index=0;index<inputs.Length;index++)
            if(inputs[index].Texture.SourceDimension!=dimensions[index])
                throw new InvalidDataException("Cloud image dimension does not satisfy its declared texture consumer.");
        if(sta[2].SourceDimension!=4)
            throw new InvalidDataException("The atmosphere Plane entry not sampled by Cloud must still be a two-dimensional texture.");
        CloudInputs=Array.AsReadOnly(inputs);
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
        return CloudInputs.Zip(other.CloudInputs).All(pair=>Same(pair.First.Texture,pair.Second.Texture))&&SameSta(other);
    }

    public bool SameSta(SkyAppliedCloudTextureSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return StaInputs.Zip(other.StaInputs).All(pair=>Same(pair.First,pair.Second));
    }

    private static bool Same(SkyTextureSource left,SkyTextureSource right)
        => left.Identity==right.Identity&&left.SamplerDescriptorSha256==right.SamplerDescriptorSha256;

    private static SkyTextureSource[] Read(SkyCloudSourceTextureGroup group)
        => group.Entries.Select((entry,index)=>SkyTextureSource.Import(entry.Resource,entry.ImageIndex,
            group.ReadSourceShaderViewFormat(index),group.ReadSourceSamplerDescriptor(index))).ToArray();
}
