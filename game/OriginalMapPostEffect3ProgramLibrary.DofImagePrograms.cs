using San14.Data;
using San14.Data.Formats;

public sealed partial class OriginalMapPostEffect3ProgramLibrary
{
    /// <summary>This exact shader creation profile does not enable a dispatch
    /// owner. ComputeExecutor retains its pass58/CS46-only typed buffer gate,
    /// and its exact CS46/51 workgroup initialization gate. Original CS47..49
    /// append through explicit raw atomics; none declares append/counter UAVs.</summary>
    private bool IsOriginalDofImageStorageProgram(OriginalMapPostEffect3Program program,DxbcResourceDefinitions reflected)
    {
        if(SourceSha256!=OriginalMapFinalOutputPostEffect3Source.PayloadSha256||program.StageCode!=5||
            program.StageIndex is not(47 or 48 or 49))return false;
        var raw=reflected.Bindings.Where(b=>b.InputType is 6 or 8).ToArray();
        var typed=reflected.Bindings.Where(b=>b.InputType==4).OrderBy(b=>b.BindPoint).ToArray();
        if(program.Sha256!=OriginalMapPostEffect3DofImageStorageAdapter.SourceSha256(program.StageIndex)||
            reflected.Bindings.Count!=6||reflected.Bindings.Any(b=>b.InputType is 9 or 10 or 11)||
            raw is not[{InputType:8,Dimension:1,ReturnType:6,BindPoint:0,BindCount:1}]||typed is not[
                {Dimension:1,ReturnType:5,BindPoint:1,BindCount:1},{Dimension:1,ReturnType:5,BindPoint:2,BindCount:1}])
            throw new InvalidDataException("Original DOF image shader creation profile differs from exact CS47..49/raw u0/full float4 u1/u2; append/counter or another typed profile remains unsupported.");
        return true;
    }
}
