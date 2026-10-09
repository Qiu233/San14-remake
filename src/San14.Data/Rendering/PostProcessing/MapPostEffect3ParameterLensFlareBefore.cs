namespace San14.Data;

/// <summary>Source1163970 read or UAV submission. NativeStageCode is
/// the original submission selector (0 compute/2 pixel), not DXBC stage code.</summary>
public sealed record MapPostEffect3LensFlareBeforeTextureSubmission(MapPostEffect3ResourceSetter Setter,int NativeStageCode,int RegisterIndex,
    MapPostEffect3BindingRole Role,int? ParameterIndex,int? DescriptorTextureOffset,int MipSelector,string Locale="TC");

public sealed record MapPostEffect3LensFlareBeforeInputs(MapPostEffect3DispatchExtent ActualIncomingDispatch,
    string SourceEvidence);
public sealed record MapPostEffect3LensFlareBeforePacket(int PassIndex,bool BeforeHookAllowsPass,
    MapPostEffect3DispatchExtent Dispatch,
    IReadOnlyList<MapPostEffect3LensFlareBeforeTextureSubmission> OrderedTextureSubmissions,
    long FrameNumber,string OwnerIdentity,string SourceSha256,string SourceEvidence,string Locale="TC");

/// <summary>'s per-pass hook. The incoming RenderContext1BB0..1BB8
/// belongs to the caller's actual setup; this writer does not replace missing
/// context state with a guessed source dispatch. Descriptor30/38 remain explicit
/// resource dependencies to be resolved by their own original producer.</summary>
public static class MapPostEffect3ParameterLensFlareBefore
{


    public static MapPostEffect3LensFlareBeforePacket Prepare(
        MapPostEffect3AllocationPlan allocation,
        MapPostEffect3ParameterLensFlareInitialization init,int passIndex,
        MapPostEffect3LensFlareBeforeInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(init);ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.ActualIncomingDispatch);ArgumentException.ThrowIfNullOrWhiteSpace(inputs.SourceEvidence);
        if(passIndex is <20 or >43||allocation.ContextOwnerIdentity!=init.OwnerIdentity||allocation.Source.Sha256!=init.SourceSha256||
            MapPostEffect3PassStateReader.Read(allocation.Source,init.Locale)[passIndex].Factory.Index!=4||
            allocation.ContextWidth==0||allocation.ContextHeight==0||allocation.ContextWidth>int.MaxValue||allocation.ContextHeight>int.MaxValue)
            throw new InvalidDataException("LensFlare Before current pass/allocation/init owner differs.");
        var pass=allocation.Source.Passes[passIndex];uint hash=pass.NameHash;
        bool Is(string name)=>hash==MapPostEffect3ParameterState.PrimaryNameHash(name);
        int category=passIndex<25?0:passIndex<30?1:passIndex<35?2:3;
        var submissions=new List<MapPostEffect3LensFlareBeforeTextureSubmission>();
        var dispatch=inputs.ActualIncomingDispatch;string evidence=";category24name-table"+inputs.SourceEvidence;
        // Native returns before all resource mutations for retained aperture
        // and its five expensive starburst passes.
        if(!init.FStopChanged14&&category==1)return Result(false);
        bool allowed=true;
        if(init.AnamorphicEnabled16)
        {
            int tex0=Index("AnamorphicBloom0"),tex1=Index("AnamorphicBloom1");
            uint w=(uint)(int)((float)(allocation.ContextWidth>>1)*BitConverter.UInt32BitsToSingle(0x3ED9DF52))&0xFFFFFFFC,
                h=(allocation.ContextHeight>>1)&0xFFFFFFFC;
            bool overrideDispatch=true;
            if(Is("UpScaleAdd2")){Write(tex0,2);w>>=2;h>>=2;}
            else if(Is("PostGaussBlurCS2")){Read(0,tex0);Write(tex1,2);w>>=2;h>>=2;}
            else if(Is("UpScaleAdd1")){Read(1,tex1);Write(tex0,1);w>>=1;h>>=1;}
            else if(Is("PostGaussBlurCS1")){Read(0,tex0);Write(tex1,1);w>>=1;h>>=1;}
            else if(Is("UpScaleAdd0")){Read(1,tex1);Write(tex0,0);}
            else if(Is("PostGaussBlurCS0")){Read(0,tex0);Write(tex1,0);}
            else if(Is("AnamorphicBlur"))w>>=1;
            else if(!Is("AddAnamorphic"))overrideDispatch=false;
            // only overwrites dispatch when computed X is nonzero.
            if(overrideDispatch)
            {uint x=Ceil16(w);if(x!=0)dispatch=new(x,Ceil16(h),1);}
        }
        else if(Is("AnamorphicBlur")||Is("AddAnamorphic"))allowed=false;
        if(!init.LensEnabled15&&category==2&&!Is("ClearGhostImage"))allowed=false;
        int stage=dispatch.X>=1?0:2;
        submissions.Add(new(MapPostEffect3ResourceSetter.ShaderResource,stage,7,MapPostEffect3BindingRole.ShaderResourceRead,null,0x30,0,init.Locale));
        submissions.Add(new(MapPostEffect3ResourceSetter.ShaderResource,stage,8,MapPostEffect3BindingRole.ShaderResourceRead,null,0x38,0,init.Locale));
        return Result(allowed);
        MapPostEffect3LensFlareBeforePacket Result(bool value)=>new(passIndex,value,dispatch,submissions.AsReadOnly(),
            init.FrameNumber,init.OwnerIdentity,init.SourceSha256,evidence,init.Locale);
        int Index(string name)
        {
            var rows=allocation.TextureParameters.Where(t=>t.NameHash==MapPostEffect3ParameterState.PrimaryNameHash(name)).ToArray();
            if(rows.Length!=1)throw new InvalidDataException("Lens Before named texture absent: "+name);return rows[0].Index;
        }
        void Read(int register,int index)=>submissions.Add(new(MapPostEffect3ResourceSetter.ShaderResource,0,register,MapPostEffect3BindingRole.ShaderResourceRead,index,null,0,init.Locale));
        void Write(int index,int mip)=>submissions.Add(new(MapPostEffect3ResourceSetter.UnorderedAccess,0,0,MapPostEffect3BindingRole.UnorderedAccessReadWrite,index,null,mip,init.Locale));
    }
    private static uint Ceil16(uint v)=>(v>>4)+((v&15)!=0?1u:0u);
}
