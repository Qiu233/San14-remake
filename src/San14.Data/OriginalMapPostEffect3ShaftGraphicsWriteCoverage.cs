using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Exact Shaft PS1 emits RGBA for every invocation, without discard or
/// branch. Actual stock geometry, viewport, raster and successful submission
/// are deliberately a separate owned execution proof.</summary>
public sealed class OriginalMapPostEffect3ShaftGraphicsWriteCoverage
{
    private OriginalMapPostEffect3ShaftGraphicsWriteCoverage(OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3ParameterLightShaftInitialization init,OriginalMapPostEffect3TextureShape shape)
        =>(FrameNumber,ContextOwnerIdentity,SourceSha256,TextureWidth,TextureHeight,RequiredConstantBufferBytesSha256,SourceEvidence)=
            (init.FrameNumber,init.OwnerIdentity,init.SourceSha256,shape.Width,shape.Height,
                Convert.ToHexString(SHA256.HashData(init.ConstantBuffer!.Bytes.Span)),
                "original-Shaft-pass7/PS1;exact98-token-program;no-control/discard;RGB mad output0 thenAlpha1;ret;"+init.SourceEvidence);
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public string SourceSha256{get;}
    public int SourcePassIndex=>7;
    public int ParameterIndex=>11;
    public uint TextureMipLevel=>0;
    public uint TextureWidth{get;}
    public uint TextureHeight{get;}
    public string ProgramSha256=>"C1D33E572A65E710F69284AC31A838E98EE25D17EB27F81B074CCB019A43E62F";
    public string StockVertexProgramSha256=>OriginalMapFinalOutputToneShaderSource.VertexSha256;
    public int RequiredConstantBufferParameterIndex=>1;
    public string RequiredConstantBufferBytesSha256{get;}
    public string SourceEvidence{get;}
    public bool EstablishesGpuCompletion=>false;
    public static OriginalMapPostEffect3ShaftGraphicsWriteCoverage Prepare(OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3ParameterLightShaftInitialization init)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(init);
        _=OriginalMapPostEffect3ParameterLightShaft.PreparePassBindings(allocation,7,init);
        if(!init.BeforeHookAllowsPasses||init.ConstantBuffer is not{Bytes.Length:144} cb||cb.Locale!=init.Locale||
            cb.WriterRva!=checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(init.Locale,"OriginalMapPostEffect3ShaftGraphicsWriteCoverage.cs",0x1153FA0))||
            cb.Declaration.Index!=1||cb.FrameNumber!=init.FrameNumber||cb.OwnerIdentity!=init.OwnerIdentity||
            cb.SourceSha256!=allocation.Source.Sha256)
            throw new InvalidDataException("Shaft7 coverage needs the actual complete enabled source Init.");
        var program=allocation.Source.GetProgram(0,1);
        const string sha="C1D33E572A65E710F69284AC31A838E98EE25D17EB27F81B074CCB019A43E62F";
        if(program.Sha256!=sha||Convert.ToHexString(SHA256.HashData(program.Bytes.Span))!=sha||
            allocation.Passes[7].Pass.ProgramReferences.Where(r=>r.StageIndex is not null).ToArray() is not [{StageCode:0,StageIndex:1}])
            throw new InvalidDataException("Shaft7 coverage selected another pixel shader or custom geometry route.");
        var shape=OriginalMapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(a=>a.Parameter.Index==11));
        if(shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||shape.MipLevels!=1||!shape.HasRenderTargetPath)
            throw new InvalidDataException("Shaft7 complete color target is not its original single2D mip0 declaration11.");
        return new(allocation,init,shape);
    }
}
