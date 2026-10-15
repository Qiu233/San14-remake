using System.Buffers.Binary;
using San14.Data;

internal enum DeferredAuxiliaryFilterOperation
{
    GaussianSpecular,
    GgxSpecular2,
    GgxSpecular3,
    GaussianDepth
}

/// <summary>One current CPU operation, prepared before pixel writes and consumed
/// by the producer. It owns no device resources and is never cached across frames.</summary>
internal sealed class DeferredAuxiliaryFilterPreparation
{
    public LoadedDeferredAuxiliaryPixelSourceRecipes Source { get; }
    public DeferredF103SelectedSource Selection { get; }
    public string ProducerProvenance { get; }
    public DeferredAuxiliaryFilterOperation Operation { get; }
    public DeferredTransparencyImageSelection Transparency { get; }
    public IReadOnlyList<DeferredAuxiliaryFilterWrite> Writes { get; }
    public uint GgxFilterType => Operation switch
    {
        DeferredAuxiliaryFilterOperation.GgxSpecular2 => 2,
        DeferredAuxiliaryFilterOperation.GgxSpecular3 => 3,
        _ => throw new InvalidOperationException("This filter operation is not GGX.")
    };

    private DeferredAuxiliaryFilterPreparation(LoadedDeferredAuxiliaryPixelSourceRecipes source,
        DeferredF103SelectedSource selection,string producer,DeferredAuxiliaryFilterOperation operation,
        DeferredTransparencyImageSelection transparency,IReadOnlyList<DeferredAuxiliaryFilterWrite> writes)
        => (Source,Selection,ProducerProvenance,Operation,Transparency,Writes)=
            (source,selection,producer,operation,transparency,writes);

    public static DeferredAuxiliaryFilterPreparation Gaussian(
        LoadedDeferredAuxiliaryPixelSourceRecipes source,DeferredAuxiliaryGaussianFrame current)
        => new(source,current.AcceptedSelection,current.CurrentCommonViewProvenance,
            DeferredAuxiliaryFilterOperation.GaussianSpecular,current.Transparency,
            DeferredAuxiliaryGaussianSchedule.ReadSpecular(source,current.Transparency,current.CurrentContextFlags3F0,
                current.CurrentGaussianSize94,current.CurrentDispersion9C,current.Exponential));

    public static DeferredAuxiliaryFilterPreparation Ggx(
        LoadedDeferredAuxiliaryPixelSourceRecipes source,DeferredAuxiliaryGgxFrame current)
    {
        // Keep the accepted Camera's exact values, including the sign of zero.
        ReadOnlySpan<byte> camera=current.AcceptedSelection.Camera.Prefix.Span;
        float fov=BinaryPrimitives.ReadSingleLittleEndian(camera.Slice(0x148,4));
        float aspect=BinaryPrimitives.ReadSingleLittleEndian(camera.Slice(0x14C,4));
        var writes=DeferredAuxiliaryGgxSchedule.Read(source,current.Transparency,current.CurrentContextFlags3F0,
            current.CurrentFilterType90,current.CurrentSampleCount94,fov,aspect,current.TangentDispatch);
        return new(source,current.AcceptedSelection,current.CurrentCommonViewProvenance,
            current.CurrentFilterType90==2?DeferredAuxiliaryFilterOperation.GgxSpecular2:
                DeferredAuxiliaryFilterOperation.GgxSpecular3,current.Transparency,writes);
    }

    public static DeferredAuxiliaryFilterPreparation Depth(LoadedDeferredAuxiliaryPixelSourceRecipes source,
        DeferredF103SelectedSource selection,uint flags,uint depthFilterType,string producer,
        IDeferredAuxiliaryExponential? exponential=null)
        => new(source,selection,producer,DeferredAuxiliaryFilterOperation.GaussianDepth,default,
            DeferredAuxiliaryGaussianSchedule.ReadDepth(source,flags,depthFilterType,exponential));
}
