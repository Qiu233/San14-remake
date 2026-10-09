/// <summary>Readable pixels for a positively closed source shader footprint.
/// Keeps the original full texture view. This capability does not establish
/// all texels, carve a new native view, or substitute zero for unread texels.
/// The consumer supplies the exact selected shader and actual uploaded CB
/// receipts so a conditional read cannot outlive its original predicate.</summary>
public interface IMapPostEffect3SourceTextureReadFootprintLease:
    IMapPostEffect3ComputeTextureLease
{
    void RequireReadableSourceFootprint(int sourcePassIndex,uint sourceRegister,
        string actualProgramSha256,
        IReadOnlyList<MapPostEffect3ComputeConstantBufferReadReceipt> actualConstantBufferReads);
}
