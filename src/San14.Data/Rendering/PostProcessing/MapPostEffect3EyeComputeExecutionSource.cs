using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Current source Eye ordinals5/6/7. EnableAutoExposure does not gate
/// these native iterations. The original custom Execute returns true for
/// them; the wrapper subsequently issues the automatic compute dispatch.</summary>
public sealed class MapPostEffect3EyeComputeExecutionRecipe
{
    private readonly MapPostEffect3EyeAdaptation2Frame frame;
    private readonly MapPostEffect3ConstantBufferWriter exposureWriter;
    internal MapPostEffect3EyeComputeExecutionRecipe(MapPostEffect3EyeAdaptation2Frame frame,
        MapPostEffect3EyeBeforeBindings before,MapPostEffect3ParameterConstantBuffer? exposure,
        string programSha,MapPostEffect3DispatchExtent dispatch,MapPostEffect3ConstantBufferWriter exposureWriter,string evidence)
    {
        this.frame=frame;this.exposureWriter=exposureWriter;Before=before;ExposureConstants=exposure;ProgramSha256=programSha;ActualDispatch=dispatch;
        OrderedEffectiveBindings=Array.AsReadOnly(frame.Allocation.Passes[before.Iteration.SourcePassIndex].ActiveBindings.ToArray());
        SourceEvidence=evidence+";"+before.SourceEvidence;
    }
    public long FrameNumber=>frame.FrameNumber;
    public string ContextOwnerIdentity=>frame.OwnerIdentity;
    public string SourceSha256=>frame.State.SourceSha256;
    public string Locale=>frame.Locale;
    public int SourcePassIndex=>Before.Iteration.SourcePassIndex;
    public int NativeCachedPassOrdinal=>Before.Iteration.NativeCachedPassOrdinal;
    public MapPostEffect3EyeBeforeBindings Before{get;}
    public MapPostEffect3ParameterConstantBuffer? ExposureConstants{get;}
    public string ProgramSha256{get;}
    public MapPostEffect3DispatchExtent ActualDispatch{get;}
    public IReadOnlyList<MapPostEffect3Binding> OrderedEffectiveBindings{get;}
    public string SourceEvidence{get;}
    public bool ExecutesPass=>true;
    public bool RequiresGenericComputeDispatch=>true;
    public bool NativeBeforeReadsAutoExposureFlag=>false;
    public bool NativeExecuteEmitsOwnDispatch=>false;
    public byte NativeExecuteReturnAl=>1;
    public bool EstablishesGpuCompletion=>false;
    public int EnableAutoExposureValue=>BinaryPrimitives.ReadInt32LittleEndian(frame.Values["EnableAutoExposure"]);
    /// <summary>CS52's exact original old-u0 load branch. This is independent
    /// of EnableAutoExposure. Positive adaptation rates require prior texel0.</summary>
    public bool PriorExposureTexelLoadExecutes=>SourcePassIndex==104&&
        (BinaryPrimitives.ReadSingleLittleEndian(ExposureConstants!.Bytes.Span)>0||
         BinaryPrimitives.ReadSingleLittleEndian(ExposureConstants.Bytes.Span[4..])>0);
    public void RequireCurrent()
    {
        frame.RequireCurrent();
        if(!frame.OrderedIterations.Contains(Before.Iteration)||!Before.RequiresGenericComputeDispatch||
            Before.Iteration.NativeLoopIndex!=0||Before.Iteration.NativeLoopCount!=1)
            throw new InvalidDataException("Eye compute recipe is not its current selected single source iteration.");
        if(SourcePassIndex!=102&&(ExposureConstants is not{Bytes.Length:64}||ExposureConstants.Writer!=exposureWriter))
            throw new InvalidDataException("Eye compute has no complete actual source Exposure CB24.");
    }
    internal void RequirePreparedFrame(MapPostEffect3EyeAdaptation2Frame actual)
    {if(!ReferenceEquals(frame,actual))throw new InvalidDataException("Eye execution belongs to another exact prepared source frame.");RequireCurrent();}
}

public sealed class LoadedMapPostEffect3EyeComputeExecutionSource
{
    internal LoadedMapPostEffect3EyeComputeExecutionSource(LoadedDeferredSamplerNativeSource native,string evidence)=>(Native,SourceEvidence)=(native,evidence);
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string Locale=>Native.Locale;
    public MapPostEffect3ConstantBufferWriter ExposureWriter=>MapPostEffect3ConstantBufferWriter.Exposure;
    public string SourceEvidence{get;}
    public MapPostEffect3EyeComputeExecutionRecipe Prepare(
        MapPostEffect3AllocationPlan allocation,MapPostEffect3EyeAdaptation2Frame frame,
        MapPostEffect3EyeBeforeBindings before,MapPostEffect3ParameterConstantBuffer? actualExposureConstants)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(before);
        frame.RequireCurrent();MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(frame.Locale!=Locale)throw new InvalidDataException("Eye compute needs the same original executable locale as its current Eye controller.");
        int ordinal=before.Iteration.NativeCachedPassOrdinal,pass=before.Iteration.SourcePassIndex;
        if(!ReferenceEquals(frame.Allocation,allocation)||!frame.OrderedIterations.Contains(before.Iteration)||
            ordinal is not(5 or 6 or 7)||pass!=ordinal+97||!before.RequiresGenericComputeDispatch||
            before.NativeDrawCommand.Length!=0||before.BloomLoopConstants is not null||
            before.SourceTextureParameterIndex is not null||before.TargetTextureParameterIndex is not null)
            throw new InvalidDataException("Eye compute needs its actual selected source Before5/6/7, without an invented raster/mip override.");
        if((frame.NativeMask1E8&(1u<<ordinal))==0)
            throw new InvalidDataException("Eye compute current mask does not select its actual ordinal.");
        if(pass!=102)
        {
            if(actualExposureConstants is not{Bytes.Length:64} cb||cb.Writer!=ExposureWriter||cb.Declaration.Index!=24||
                cb.FrameNumber!=frame.FrameNumber||cb.OwnerIdentity!=frame.OwnerIdentity||cb.SourceSha256!=frame.State.SourceSha256)
                throw new InvalidDataException("Eye automatic compute needs current original complete Exposure CB24/64B.");
            foreach(var pair in new[]{(Name:"AdaptedScaleLight",Offset:0),(Name:"AdaptedScaleDark",Offset:4),
                (Name:"EvRangeMinMax",Offset:8),(Name:"DeltaTime",Offset:20)})
                if(!cb.Bytes.Span.Slice(pair.Offset,4).SequenceEqual(frame.Values[pair.Name].AsSpan(0,4)))
                    throw new InvalidDataException("Eye compute CB24 was produced for different current scalar values.");
        }
        var program=MapPostEffect3Functor1FootprintSource.RequireProgram(allocation,pass,5,pass-52);
        string sha=program.Sha256;
        if(allocation.Passes[pass].DispatchExtent is not{X:>0,Y:>0,Z:1} dispatch)
            throw new InvalidDataException("Eye selected generic dispatch lacks its actual positive source context extent.");
        if((pass is 102 or 104)&&dispatch!=new MapPostEffect3DispatchExtent(1,1,1))
            throw new NotSupportedException("Original Eye clear/exposure use exactly one source dispatch group.");
        var result=new MapPostEffect3EyeComputeExecutionRecipe(frame,before,pass==102?null:actualExposureConstants,sha,dispatch,ExposureWriter,SourceEvidence);
        result.RequireCurrent();return result;
    }
}

public static class MapPostEffect3EyeComputeExecutionSource
{
    public static LoadedMapPostEffect3EyeComputeExecutionSource Load(GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.ReadEyeComputeExecutionSource(RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
    }
}
public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3EyeComputeExecutionSource ReadEyeComputeExecutionSource(LoadedDeferredSamplerNativeSource native)
    {

        return new(native,native.ResourceUri+"-mask-unconditional-E0;-Before-mask-and-4Klookup-only"+
            "-compute5/6/7→-AL1;→-generic-CS-after-ExecuteAL1"+
            "counted-pass102/103/104-native-dispatch-context;no-AutoExposure-skip/no-GPU-claim");
    }
}
