using System.Buffers.Binary;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

public sealed record MapPostEffect3EyeNativeViewport(float Left,float Top,float Width,float Height,
    float MinimumDepth,float MaximumDepth);

/// <summary>Actual source frontend state after binding the current RT0 view.
/// The game retains its entire raster snapshot; these source fields describe
/// the only Eye Execute writes. Depth range has no inferred default.</summary>
public sealed record MapPostEffect3EyeGraphicsCurrentState(long FrameNumber,
    string ContextOwnerIdentity,string OwnedFrontendIdentity,string ViewIdentity,
    MapPostEffect3EyeNativeViewport ActualViewport,FinalEffectScissor? ActualScissor,
    uint ActualNativeColorStateF4,byte ActualNativeRt0BlendEnable1C0,string SourceEvidence);

/// <summary>Actual bound mip target dimensions from native current RT0+50/54.
/// A source-owned texture pointer is supplied for counted target views; an
/// inherited external caller target carries its actual view identity instead.</summary>
public sealed record MapPostEffect3EyeGraphicsSelectedTarget(
    MapPostEffect3ResolvedTextureParameter? ActualTexture,int ActualInstanceIndex,
    uint NativeMipLevel,int NativeCurrentWidth50,int NativeCurrentHeight54,
    string ActualTargetViewIdentity,string SourceEvidence);

public sealed class MapPostEffect3EyeGraphicsStateRecipe
{
    private readonly MapPostEffect3EyeAdaptation2Frame frame;
    private readonly MapPostEffect3TextureParameterState table;
    internal MapPostEffect3EyeGraphicsStateRecipe(MapPostEffect3EyeAdaptation2Frame frame,
        MapPostEffect3TextureParameterState table,MapPostEffect3EyeBeforeBindings before,
        MapPostEffect3EyeGraphicsCurrentState current,MapPostEffect3EyeGraphicsSelectedTarget target,
        FinalEffectScissor? scissor,string evidence)
        =>(this.frame,this.table,Before,ActualCurrentState,ActualSelectedTarget,DrawScissor,SourceEvidence)=
            (frame,table,before,current,target,scissor,evidence);
    public MapPostEffect3EyeBeforeBindings Before{get;}
    public long FrameNumber=>frame.FrameNumber;
    public string ContextOwnerIdentity=>frame.OwnerIdentity;
    public string SourceSha256=>frame.State.SourceSha256;
    public MapPostEffect3EyeGraphicsCurrentState ActualCurrentState{get;}
    public MapPostEffect3EyeGraphicsSelectedTarget ActualSelectedTarget{get;}
    public MapPostEffect3EyeNativeViewport DrawViewport=>ActualCurrentState.ActualViewport;
    public FinalEffectScissor? DrawScissor{get;}
    public bool ChangesScissor=>Before.RequiresNativeScissorOverride;
    public bool DrawScissorEnabled=>ChangesScissor||ActualCurrentState.ActualScissor is not null;
    public uint DrawNativeColorStateF4=>Before.TemporaryNativeColorStateF4??ActualCurrentState.ActualNativeColorStateF4;
    public byte DrawNativeRt0BlendEnable1C0=>Before.TemporaryNativeRt0BlendEnable1C0??ActualCurrentState.ActualNativeRt0BlendEnable1C0;
    public bool ChangesBlend=>Before.RequiresTemporaryBlendOverride;
    public MapPostEffect3EyeGraphicsCurrentState RestoreExecuteState=>ActualCurrentState;
    public bool PreservesActualRemainingRasterFields=>true;
    public bool ExecuteChangesViewport=>false;
    public uint NativeScissorEnableOpcode=>0x10013;
    public uint NativeScissorRectangleOpcode=>0x10014;
    public IReadOnlyList<int> NativeColorStateAffectedSlots=>Array.AsReadOnly(Enumerable.Range(0,8).ToArray());
    public uint NativeRt0BlendEnableOpcode=>0x100000B;
    public string SourceEvidence{get;}
    public bool ProvesGpuExecution=>false;
    public void RequireCurrent()
    {
        frame.RequireCurrent();
        if(ActualSelectedTarget.ActualTexture is{} texture)table.ValidateCurrent(texture);
    }
}

/// <summary>Typed native target/scissor/blend Execute recipe for actual Eye
/// graphics iterations. The adapter supplies its current full raster state,
/// applies only these fields, then restores that same complete snapshot.</summary>
public sealed class LoadedMapPostEffect3EyeGraphicsStateSource
{
    internal LoadedMapPostEffect3EyeGraphicsStateSource(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}
    public MapPostEffect3EyeGraphicsStateRecipe Prepare(MapPostEffect3EyeAdaptation2Frame frame,
        MapPostEffect3EyeBeforeBindings before,MapPostEffect3TextureParameterState table,
        MapPostEffect3EyeGraphicsCurrentState actualCurrentState,
        MapPostEffect3EyeGraphicsSelectedTarget actualSelectedTarget)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(before);ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(actualCurrentState);ArgumentNullException.ThrowIfNull(actualSelectedTarget);
        frame.RequireCurrent();
        if(frame.Locale!=Locale||!Sse.IsSupported||before.Iteration.IsCompute||!frame.OrderedIterations.Contains(before.Iteration)||
            !ReferenceEquals(table.Allocation,frame.Allocation)||table.FrameNumber!=frame.FrameNumber||
            actualCurrentState.FrameNumber!=frame.FrameNumber||actualCurrentState.ContextOwnerIdentity!=frame.OwnerIdentity||
            string.IsNullOrWhiteSpace(actualCurrentState.OwnedFrontendIdentity)||string.IsNullOrWhiteSpace(actualCurrentState.ViewIdentity)||
            string.IsNullOrWhiteSpace(actualCurrentState.SourceEvidence)||string.IsNullOrWhiteSpace(actualSelectedTarget.SourceEvidence)||
            string.IsNullOrWhiteSpace(actualSelectedTarget.ActualTargetViewIdentity)||actualSelectedTarget.ActualInstanceIndex<0||
            actualSelectedTarget.NativeCurrentWidth50<=0||actualSelectedTarget.NativeCurrentHeight54<=0||actualSelectedTarget.NativeMipLevel>30)
            throw new InvalidDataException("Eye graphics recipe requires its current admitted graphics iteration/frontend/target.");
        var viewport=actualCurrentState.ActualViewport??throw new InvalidDataException("Eye actual postbinding viewport is absent.");
        if(!float.IsFinite(viewport.Left)||!float.IsFinite(viewport.Top)||!float.IsFinite(viewport.Width)||!float.IsFinite(viewport.Height)||
            !float.IsFinite(viewport.MinimumDepth)||!float.IsFinite(viewport.MaximumDepth)||viewport.Width<=0||viewport.Height<=0)
            throw new InvalidDataException("Eye actual native viewport/depth range is not a finite current value.");
        if(actualSelectedTarget.ActualTexture is{} target)
        {
            table.ValidateCurrent(target);
            if(target.Reference.InstanceIndex!=actualSelectedTarget.ActualInstanceIndex)
                throw new InvalidDataException("Eye current RT0 target side differs from its actual pointer.");
            if(target.Reference.PhysicalAllocationParameterIndex is int physical)
            {
                var allocation=frame.Allocation.TextureAllocations.Single(a=>a.Parameter.Index==physical);
                var shape=MapPostEffect3AllocationShapeReader.Read(allocation);int mip=(int)actualSelectedTarget.NativeMipLevel;
                if(mip>=shape.MipLevels||actualSelectedTarget.NativeCurrentWidth50!=Math.Max(1,(int)(allocation.Width>>mip))||
                    actualSelectedTarget.NativeCurrentHeight54!=Math.Max(1,(int)(allocation.Height>>mip)))
                    throw new InvalidDataException("Eye current target dimensions do not belong to its selected source mip.");
            }
        }
        if(before.TargetTextureParameterIndex is int expectedPhysical)
        {
            if(actualSelectedTarget.ActualTexture?.Reference.PhysicalAllocationParameterIndex!=expectedPhysical||
                actualSelectedTarget.NativeMipLevel!=before.TargetMipLevel)
                throw new InvalidDataException("Eye current RT0 differs from the original Before selected physical mip.");
        }
        if(viewport.Width!=actualSelectedTarget.NativeCurrentWidth50||viewport.Height!=actualSelectedTarget.NativeCurrentHeight54)
            throw new InvalidDataException("Eye graphics needs the actual viewport extent after its current target binding.");
        FinalEffectScissor? scissor=actualCurrentState.ActualScissor;
        if(before.RequiresNativeScissorOverride)
        {
            var words=frame.Values["Viewport"];
            float V(int i)=>BinaryPrimitives.ReadSingleLittleEndian(words.AsSpan(i*4,4));
            int X(float extent,float value)=>Sse.ConvertToInt32WithTruncation(Vector128.CreateScalar(
                NativeFloor(NativeFloat.Multiply(extent,value))));
            scissor=new(X(actualSelectedTarget.NativeCurrentWidth50,V(0)),X(actualSelectedTarget.NativeCurrentHeight54,V(1)),
                X(actualSelectedTarget.NativeCurrentWidth50,V(2)),X(actualSelectedTarget.NativeCurrentHeight54,V(3)));
        }
        return new(frame,table,before,actualCurrentState,actualSelectedTarget,scissor,
            SourceEvidence+";actual-after-RT0-binding="+actualSelectedTarget.SourceEvidence+";inherited-full-raster="+actualCurrentState.SourceEvidence);
    }
    // Direct finite101EB80 bit branches followed by the original CVTTSS2SI.
    // Negative nonintegral values subtract1 after truncating mantissa bits.
    private static float NativeFloor(float value)
    {
        uint bits=BitConverter.SingleToUInt32Bits(value),magnitude=bits&0x7FFFFFFF;
        bool negative=(bits&0x80000000)!=0;
        if(magnitude>=0x4B800000)
        {
            if(magnitude>0x7F800000)throw new NotSupportedException("Eye native floor NaN exception route has no current source producer.");
            return value;
        }
        if(magnitude<0x3F800000)return magnitude==0?value:negative?-1:0;
        int shift=(0x96-(int)(bits>>23))&31;
        uint truncated=bits&~((1u<<shift)-1);float result=BitConverter.UInt32BitsToSingle(truncated);
        return negative&&truncated!=bits?NativeFloat.Subtract(result,1):result;
    }
}

public static class MapPostEffect3EyeGraphicsStateSource
{
    public static LoadedMapPostEffect3EyeGraphicsStateSource Load(GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.ReadEyeGraphicsStateSource(RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
    }
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3EyeGraphicsStateSource ReadEyeGraphicsStateSource(LoadedDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Eye graphics native current-state ABI requires a verified SC/TC profile.");

        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;"+"-Execute;-actual-target-viewport;-source-scissor"+
            "6D/82/97-native-floor;-F4/RT0blend1C0-temporary-restore;inherits-actual-cull/depth/stencil",native.Locale);
    }
}
