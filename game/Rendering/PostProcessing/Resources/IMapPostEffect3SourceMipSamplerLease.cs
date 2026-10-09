/// <summary>Actual native sampler producer, capable of proving the filter
/// and LOD settings required by a source mip-read footprint.</summary>
public interface IMapPostEffect3SourceMipSamplerLease:IMapPostEffect3ComputeSamplerLease
{
    void RequireSourceMipSampling(uint filter,uint mipLodBiasBits,uint minimumLodBits,uint maximumLodBits);
}
