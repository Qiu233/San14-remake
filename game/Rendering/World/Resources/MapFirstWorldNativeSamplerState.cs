using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>Fresh independently owned native sampler state. Its baseline is
/// the verified BF93D0 initialization reset. Only the exact first World native
/// graphics prefix and the first Shaft CS0 dispatch are admitted. Original
/// process state, original height-initializer ordering, later frames and later
/// compute writers are deliberately outside this capability. Godot depth and
/// stencil transport compute adapters do not become original native CS writes.</summary>
public sealed partial class MapFirstWorldNativeSamplerState : IAsyncDisposable
{
    public WorldNativeSamplerResetSource Source { get; }
    public PostProcessResourceBank Bank { get; }
    private readonly MapSourceCreatedWorldPlayableHost host;
    private readonly SceneViewNativeMapWorldSkySourceOwner sky;
    private readonly Rid defaultSampler;
    private readonly object lifetime=new();
    private Prefix? current;
    private MapPostEffect3FirstWorldExecutionOwner? moduleOwner;
    private IReadOnlyDictionary<uint,IMapPostEffect3ComputeSamplerLease>? retainedSlots;
    private bool retiring,dispatchAccepted,dispatchFinished;
    private Task? disposal;
    private TaskCompletionSource? drained;

    private MapFirstWorldNativeSamplerState(WorldNativeSamplerResetSource source,
        MapSourceCreatedWorldPlayableHost host,SceneViewNativeMapWorldSkySourceOwner sky,
        PostProcessResourceBank bank,Rid sampler)
        =>(Source,this.host,this.sky,Bank,defaultSampler)=(source,host,sky,bank,sampler);

    /// <summary>Call before the first render attempt, for example in the
    /// before-first-frame host configuration seam. This synchronous acceptance
    /// validates freshness before posting the actual sampler creation.</summary>
    public static Task<MapFirstWorldNativeSamplerState> BuildAsync(
        LoadedDeferredSamplerNativeSource native,
        MapSourceCreatedWorldPlayableHost freshHost,
        SceneViewNativeMapWorldSkySourceOwner actualSky,
        PostProcessResourceBank bank)
    {
        ArgumentNullException.ThrowIfNull(native);ArgumentNullException.ThrowIfNull(freshHost);
        ArgumentNullException.ThrowIfNull(actualSky);ArgumentNullException.ThrowIfNull(bank);
        if(freshHost.Scene.RenderAttemptNumber!=0 || freshHost.Scene.FrameNumber!=0 ||
            freshHost.HasAcceptedOutput || freshHost.Session.CurrentFrame is not null ||
            !ReferenceEquals(freshHost.BorrowedSky,actualSky.Device) || bank.Source.ResizeOnly)
            throw new InvalidDataException("Native sampler reset must initialize a new owned World context before its first attempt.");
        _=bank.RequireTexture(11,0); // Lifetime/owned allocation, never pixel readiness.
        var source=RenderSamplerSourceAdapters.ReadWorldNativeSamplerReset(native);
        byte[] descriptor=source.DefaultDescriptor;
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            new MapFirstWorldNativeSamplerState(source,freshHost,actualSky,bank,
                CreateDefault(rd,descriptor)));
    }

    /// <summary>Capture only after actual late World draws and source node1
    /// Exposure complete, before final completion. No caller absence Boolean
    /// or textual assertion supplies the prefix. The private host revalidates
    /// Sky, ordinary geometry, Early, Camera/Aux and the complete late roster.</summary>
    public void CaptureFirstWorldPrefix(NativeMapWorldDeferredSubmittedFrame submitted,
        MapExposurePacket actualExposure)
    {
        ArgumentNullException.ThrowIfNull(submitted);ArgumentNullException.ThrowIfNull(actualExposure);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(current is not null || dispatchAccepted)
                throw new InvalidOperationException("The fresh native sampler prefix can be accepted only once.");
            long count=host.Final.Chain.Exposure.DrawCount;
            var skyPrefix=RequireGraphicsPrefix(submitted,sky,actualExposure,count);
            var frame=host.Session.CurrentFrame!;
            if(frame.MainCamera.ExecutableLocale!=Source.Native.Locale ||
                skyPrefix.FrameNumber!=submitted.Context.FrameNumber || string.IsNullOrWhiteSpace(Bank.Source.ContextOwnerIdentity))
                throw new InvalidDataException("Native sampler source has another Camera revision or module owner.");
            current=new(submitted,actualExposure,count,skyPrefix);
        }
    }

    /// <summary>Actual reset-derived CS0 slots7/8. They are not the Lens
    /// manager5415/4015 pair. The first Shaft setup binds current depth at2;
    /// the graphics-only World/stock prefix cannot rewrite native CS7/8.</summary>
    public IReadOnlyList<IMapPostEffect3ComputeSamplerLease> LeaseFirstShaftDefaults()
    {
        lock(lifetime)
        {
            RequireCurrentLocked();
            return Array.AsReadOnly<IMapPostEffect3ComputeSamplerLease>(
                [new Selected(this,current!,7),new Selected(this,current!,8)]);
        }
    }

    /// <summary>Finite execution boundary for these reset-derived leases.
    /// Once the first native CS dispatch ends or fails they are invalid; no
    /// later source compute family can silently inherit this fresh proof.</summary>
    public async Task<MapPostEffect3ComputeCompletedPass> DispatchFirstShaftAsync(
        MapPostEffect3ComputeExecutor executor,MapPostEffect3ComputeFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(executor);ArgumentNullException.ThrowIfNull(input);
        lock(lifetime)
        {
            RequireCurrentLocked();
            if(dispatchAccepted || middleContinuationAccepted || !ReferenceEquals(executor.Bank,Bank) || input.FrameNumber!=current!.Submitted.Context.FrameNumber)
                throw new InvalidDataException("Reset-derived sampler leases admit only this bank's first same-frame Shaft dispatch.");
            foreach(uint slot in new uint[]{7,8})
                if(input.CurrentSamplers.Count(s=>s.SourceRegister==slot)!=1 ||
                    input.CurrentSamplers.Single(s=>s.SourceRegister==slot) is not Selected selected ||
                    !ReferenceEquals(selected.Owner,this) || !ReferenceEquals(selected.AcceptedPrefix,current))
                    throw new InvalidDataException("First Shaft dispatch does not retain the exact accepted native default sampler selections.");
            dispatchAccepted=true;
        }
        try
        {
            // The executor validates the actual pass1 program, source dispatch
            // dimensions, ready CBs, textures and the remaining current s2.
            return await executor.DispatchAsync(1,input);
        }
        finally
        {
            lock(lifetime){dispatchFinished=true;drained?.TrySetResult();}
        }
    }

    private void RequireCurrentLocked()
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(current is not {} prefix || dispatchFinished)
            throw new InvalidDataException("Fresh native sampler selection is missing, consumed or failed.");
        _=Bank.RequireTexture(11,0);
        var actual=RequireGraphicsPrefix(prefix.Submitted,sky,prefix.Exposure,prefix.ExposureDrawCount);
        if(!ReferenceEquals(actual,prefix.SkyPrefix) || !defaultSampler.IsValid)
            throw new InvalidDataException("Native default sampler prefix has another exact Sky/reset generation.");
    }

    internal void BindModuleOwner(MapPostEffect3FirstWorldExecutionOwner owner)
    {
        lock(lifetime)
        {
            if(moduleOwner is not null||current is not null||!ReferenceEquals(owner.Bank,Bank))
                throw new InvalidDataException("Native state must bind its actual fresh module once.");
            moduleOwner=owner;
        }
    }
    internal void PrepareRetainedOwnedFrame(MapPostEffect3FirstWorldExecutionOwner owner,
        NativeMapFrameContext context,MapPostEffect3LensExecutionOwner.LensRetainedSamplerCapability actualWriter)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(moduleOwner,owner)||context.FrameNumber!=2||current?.Submitted.Context.FrameNumber!=1||
                !middleContinuationAccepted||(dispatchAccepted&&!dispatchFinished)||actualWriter.NativeWriterFrameNumber!=1||
                actualWriter.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity)
                throw new InvalidDataException("Retained native CS slots need this module's completed real frame1 Lens writer.");
            actualWriter.RequireCurrent();
            retainedSlots=new Dictionary<uint,IMapPostEffect3ComputeSamplerLease>
                {{7,actualWriter.LeaseForOwnedFrame(2,7)},{8,actualWriter.LeaseForOwnedFrame(2,8)}};
            current=null;dispatchAccepted=false;dispatchFinished=false;
            shaftContinuationAccepted=false;middleContinuationAccepted=false;drained=null;
        }
    }
    private MapOwnedGraphicsMaskPrefix RequireGraphicsPrefix(NativeMapWorldDeferredSubmittedFrame submitted,
        SceneViewNativeMapWorldSkySourceOwner actualSky,MapExposurePacket exposure,long drawCount)
        =>host.RequireFirstWorldNativeGraphicsPrefix(submitted,actualSky,exposure,drawCount,
            submitted.Context.FrameNumber==2?moduleOwner:null);
    private DeferredDeviceBinding RequirePixelSampler7(NativeMapWorldDeferredSubmittedFrame submitted,
        MapExposurePacket exposure)
        =>host.RequireInheritedPixelSampler7(submitted,exposure,submitted.Context.FrameNumber==2?moduleOwner:null);
    private Rid RequireSelectedSamplerRid(uint register)
    {
        if(register is not(7 or 8))throw new InvalidDataException("Native retained slot is not CS7/8.");
        if(retainedSlots is null)return defaultSampler;
        var lease=retainedSlots[register];lease.RequireCurrent();return lease.SamplerRid;
    }

    private sealed record Prefix(NativeMapWorldDeferredSubmittedFrame Submitted,
        MapExposurePacket Exposure,long ExposureDrawCount,MapOwnedGraphicsMaskPrefix SkyPrefix);

    private sealed record Selected(MapFirstWorldNativeSamplerState Owner,Prefix AcceptedPrefix,
        uint SourceRegister):IMapPostEffect3ComputeSamplerLease
    {
        public long FrameNumber=>AcceptedPrefix.Submitted.Context.FrameNumber;
        public string ContextOwnerIdentity=>Owner.Bank.Source.ContextOwnerIdentity;
        public int NativeStageOrdinal=>0;
        public Rid SamplerRid=>Owner.RequireSelectedSamplerRid(SourceRegister);
        public string SourceEvidence=>(Owner.retainedSlots?.GetValueOrDefault(SourceRegister)?.SourceEvidence??Owner.Source.SourceEvidence)+";"+AcceptedPrefix.Submitted.SourceEvidence+
            ";first-owned-World-native-VSPS-prefix;CS0/reset-default-slot="+SourceRegister;
        public void RequireCurrent()
        {
            lock(Owner.lifetime)
            {
                Owner.RequireCurrentLocked();
                if(!ReferenceEquals(Owner.current,AcceptedPrefix) || SourceRegister is not (7 or 8))
                    throw new InvalidDataException("Native default sampler lease is another prefix or native stage slot.");
            }
        }
    }

    private static Rid CreateDefault(RenderingDevice rd,ReadOnlySpan<byte> descriptor)
    {
        // This finite profile differs from keyed manager samplers: native
        // comparison is Never but disabled, border is opaque white, MinLOD is
        // -FLT_MAX. No generic sampler adapter rule is relaxed to admit it.
        if(descriptor.Length!=52)throw new InvalidDataException("Native reset default must have its complete52B descriptor.");
        uint[] words=new uint[13];
        for(int i=0;i<13;i++)words[i]=BinaryPrimitives.ReadUInt32LittleEndian(descriptor.Slice(i*4,4));
        uint[] expected=[0x15,3,3,3,0,1,1,0x3F800000,0x3F800000,0x3F800000,0x3F800000,0xFF7FFFFF,0x7F7FFFFF];
        if(!words.SequenceEqual(expected))throw new InvalidDataException("Native fresh backend default descriptor changed.");
        using var state=new RDSamplerState
        {
            MinFilter=RenderingDevice.SamplerFilter.Linear,MagFilter=RenderingDevice.SamplerFilter.Linear,
            MipFilter=RenderingDevice.SamplerFilter.Linear,
            RepeatU=RenderingDevice.SamplerRepeatMode.ClampToEdge,
            RepeatV=RenderingDevice.SamplerRepeatMode.ClampToEdge,
            RepeatW=RenderingDevice.SamplerRepeatMode.ClampToEdge,
            LodBias=BitConverter.UInt32BitsToSingle(words[4]),MinLod=BitConverter.UInt32BitsToSingle(words[11]),
            MaxLod=BitConverter.UInt32BitsToSingle(words[12]),UseAnisotropy=false,AnisotropyMax=1,
            EnableCompare=false,CompareOp=RenderingDevice.CompareOperator.Never,
            BorderColor=RenderingDevice.SamplerBorderColor.FloatOpaqueWhite,UnnormalizedUvw=false
        };
        Rid result=rd.SamplerCreate(state);
        if(!result.IsValid)throw new InvalidDataException("Device rejected the verified native reset default sampler.");
        return result;
    }

    public ValueTask DisposeAsync()
    {
        lock(lifetime)
        {
            if(disposal is not null)return new(disposal);
            retiring=true;
            Task pending=dispatchAccepted&&!dispatchFinished?
                (drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task:Task.CompletedTask;
            return new(disposal=RetireAsync(pending));
        }
    }
    private async Task RetireAsync(Task pending)
    {
        await pending;
        await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{if(defaultSampler.IsValid)rd.FreeRid(defaultSampler);return true;});
    }
}
