/// <summary>Actual native sampler producer, capable of proving the filter
/// and LOD settings required by a source mip-read footprint.</summary>
public interface IOriginalMapPostEffect3SourceMipSamplerLease:IOriginalMapPostEffect3ComputeSamplerLease
{
    void RequireSourceMipSampling(uint filter,uint mipLodBiasBits,uint minimumLodBits,uint maximumLodBits);
}
