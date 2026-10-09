using San14.Data;

public sealed partial class MapPostEffect3DepthOfField2ExecutionOwner
{
    public int? CurrentSourcePassIndex{get;private set;}
    public string CurrentSourceOperation{get;private set;}="accepted-source-invocation";
    public string? LastFailureEvidence{get;private set;}
    private string currentGraphicsReadInterfaces="none-captured";

    private void CaptureCurrentGraphicsReadInterfaces(MapPostEffect3CompiledPass compiled,
        IReadOnlyList<IMapPostEffect3ComputeTextureLease> reads)
    {
        currentGraphicsReadInterfaces=string.Join(";",compiled.Stages.SelectMany(stage=>
            stage.Bindings.Bindings.Where(binding=>binding.DescriptorKind=="Texture").Select(binding=>
            {
                var row=Bank.Source.Passes[compiled.Source.Index].ActiveBindings.Last(r=>
                    r.Table==MapPostEffect3BindingTable.TextureParameters&&
                    r.Role==MapPostEffect3BindingRole.ShaderResourceRead&&
                    (r.StageMask&(stage.Source.StageCode==1?2:4))!=0&&r.RegisterIndex==binding.Source.BindPoint);
                // The effective Before setter can replace a counted static row.
                var read=reads.OfType<ReadCapability>().Single(r=>r.Rows.Any(r=>r.RegisterIndex==binding.Source.BindPoint)&&
                    stage.Source.StageCode==0);
                var pointer=read.Pointer.Reference;
                return $"stage={stage.Source.StageCode}/{stage.Source.StageIndex},sha={stage.Source.Sha256},"+
                    $"t={binding.Source.BindPoint},declDimension={binding.Source.Dimension},declReturn={binding.Source.ReturnType},"+
                    $"staticParam={row.ParameterIndex},actualParam={read.ParameterIndex},side={read.InstanceIndex},"+
                    $"nativeView={read.NativeViewFormat},device={read.DeviceFormat},extent={read.Extent},"+
                    $"storage={read.StorageRid},view={read.ShaderViewRid},selectedFirst={pointer.NativeFirstMipLevel?.ToString()??"none"},"+
                    $"selectedCount={pointer.NativeMipLevelCount?.ToString()??"none"},"+
                    $"producerType={read.Actual.GetType().FullName},allocatedHistory={read.AllocatedOnlyUnusedHistory},"+
                    $"independentExposure={read.IndependentExposure}";
            })));
        if(currentGraphicsReadInterfaces.Length==0)currentGraphicsReadInterfaces="no-translated-texture-declarations";
    }
}
