using Godot;
using San14.Data;

/// <summary>Actual inherited graphics fields at Shaft7. A producer must retain
/// the current native frontend state and sampler7 selection; constructor/reset
/// samplers from compute are not a pixel-stage selection proof.</summary>
public interface IOriginalMapPostEffect3ShaftPass7DrawCapability
{
    Rect2I ActualViewport{get;}
    float ActualContext1C48{get;}
    OriginalModelRasterInputs ActualRaster{get;}
    IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> ActualPixelSamplers{get;}
    string SourceEvidence{get;}
    void RequireCurrentForPass7(OriginalMapPostEffect3FirstWorldPreparedPrefix actualPrefix);
}

public sealed record OriginalMapPostEffect3ShaftComputeChainResult(
    OriginalMapPostEffect3FirstWorldPreparedPrefix Prefix,bool SourceSkippedAllShaftPasses,
    IReadOnlyList<OriginalMapPostEffect3ComputeCompletedPass> OrderedComputePasses,
    IOriginalMapPostEffect3ComputeTextureLease? OutputColor,
    IOriginalMapPostEffect3ComputeMipTextureLease? CompletedFinalBlur)
{
    public bool CompletesFunctor0=>false;
    public bool CommitsHostHistory=>false;
}

public sealed record OriginalMapPostEffect3ShaftCompletedFamily(
    OriginalMapPostEffect3ShaftComputeChainResult ComputeChain,
    OriginalMapPostEffect3StockGraphicsCompletedPass? ActualStockPass7)
{
    public bool CompletesFunctor0=>false;
    public bool CommitsHostHistory=>false;
    public IOriginalMapPostEffect3ComputeTextureLease? OutputColor{get;init;}=ComputeChain.OutputColor;
    public bool EstablishesCompleteOutputColor=>OutputColor is not null;
}

/// <summary>Consecutive actual Shaft1..7 execution, borrowing the first World
/// owner and its retained source frame. Source-written contents are published
/// before downstream reads. Owns its graphics executor only; it neither accepts
/// a fixture frame nor advances module/Host history.</summary>
public sealed partial class OriginalMapPostEffect3ShaftExecutionOwner:IAsyncDisposable
{
    private readonly OriginalMapPostEffect3FirstWorldExecutionOwner firstWorld;
    private readonly OriginalMapPostEffect3StockGraphicsExecutor graphics;
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();
    private bool accepted,retiring;
    private Task? disposal;
    private IOriginalMapPostEffect3ShaftPass7DrawCapability? pass7Producer;
    private OriginalMapPostEffect3StockGraphicsFrameInputs? pass7Inputs;
    private long pass7WriterRevision,pass7ConstantBufferRevision;
    private OriginalMapFirstWorldNativeSamplerState.ShaftContinuation? actualSamplerContinuation;
    public OriginalMapPostEffect3ResourceBank Bank=>firstWorld.Bank;
    public OriginalMapPostEffect3ShaftComputeChainResult? LastComputeChain{get;private set;}
    public OriginalMapPostEffect3ShaftCompletedFamily? LastCompletedFamily{get;private set;}
    private OriginalMapPostEffect3ShaftExecutionOwner(OriginalMapPostEffect3FirstWorldExecutionOwner actualFirstWorld)
    {firstWorld=actualFirstWorld;graphics=OriginalMapPostEffect3StockGraphicsExecutor.Create(firstWorld.Programs,Bank);}
    public static OriginalMapPostEffect3ShaftExecutionOwner Create(OriginalMapPostEffect3FirstWorldExecutionOwner actualFirstWorld)
    {ArgumentNullException.ThrowIfNull(actualFirstWorld);return new(actualFirstWorld);}

    /// <summary>Join actual pass1, then execute2..6 and publish every exact
    /// source full-store target. A failed attempt remains consumed.</summary>
    public async Task<OriginalMapPostEffect3ShaftComputeChainResult> ContinueAfterActualFirstShaftAsync(
        OriginalMapPostEffect3FirstWorldPreparedPrefix prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);if(accepted)throw new InvalidOperationException("This source Shaft family is already accepted.");accepted=true;}
        await serial.WaitAsync();
        try
        {
            RequirePrefix(prefix);var shaft=prefix.Parameters.RequirePreparedFactories().Shaft;
            if(!ReferenceEquals(shaft,prefix.Result.Shaft))throw new InvalidDataException("First prefix changed its exact private Shaft initialization.");
            if(!shaft.BeforeHookAllowsPasses)
            {
                if(prefix.ActualFirstComputePass1 is not null)throw new InvalidDataException("Disabled Shaft unexpectedly executed its first native pass.");
                var output=prefix.WorldStockScope.LeaseWorkingScene(shaft.OutputTextureAlias.TargetParameterIndex);
                LastComputeChain=new(prefix,true,Array.Empty<OriginalMapPostEffect3ComputeCompletedPass>(),output,null);
                LastCompletedFamily=new(LastComputeChain,null);return LastComputeChain;
            }
            var first=prefix.ActualFirstComputePass1??throw new InvalidDataException("Enabled Shaft has no actual completed pass1.");
            var continuation=firstWorld.NativeSamplers.ContinueCompletedFirstShaft(firstWorld.Compute,first);
            actualSamplerContinuation=continuation;
            var receipts=new List<OriginalMapPostEffect3ComputeCompletedPass>{first};
            _=Bank.PublishShaftTextureCoverage(OriginalMapPostEffect3ShaftWriteCoverage.Prepare(Bank.Source,shaft,1),firstWorld.Compute,first);
            IOriginalMapPostEffect3ComputeMipTextureLease? completedBlur=null;
            for(int pass=2;pass<=6;pass++)
            {
                RequirePrefix(prefix);continuation.RequireCurrent();
                _=OriginalMapPostEffect3ParameterLightShaft.PreparePassBindings(Bank.Source,pass,shaft);
                var compiled=await firstWorld.Programs.GetCompiledPassAsync(pass);
                var declaredSamplers=compiled.Stages.Single().Bindings.Bindings.Where(d=>d.DescriptorKind=="Sampler").Select(d=>d.Source.BindPoint);
                var samplers=continuation.LeaseDefaultsForNextPass(pass,declaredSamplers);
                var reads=ReadActualInputs(prefix,pass);
                var outputs=Bank.Source.Passes[pass].ActiveBindings.Where(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
                    b.Role==OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite).Select(b=>b.ParameterIndex).Distinct()
                    .ToDictionary(i=>i,i=>prefix.Textures.Require(i).Reference.InstanceIndex);
                var cb=shaft.ConstantBuffer??throw new InvalidDataException("Enabled source Shaft has no complete source constants.");
                var complete=await firstWorld.Compute.DispatchAsync(pass,new(prefix.Context.FrameNumber,reads,samplers,
                    new Dictionary<int,int>{{cb.Declaration.Index,prefix.ActualShaftConstantBufferInstance}},outputs){TextureParameters=prefix.Textures});
                continuation.AcceptCompletedNextPass(complete);
                completedBlur=Bank.PublishShaftTextureCoverage(OriginalMapPostEffect3ShaftWriteCoverage.Prepare(Bank.Source,shaft,pass),firstWorld.Compute,complete);
                receipts.Add(complete);RequirePrefix(prefix);
            }
            return LastComputeChain=new(prefix,false,receipts.AsReadOnly(),null,completedBlur);
        }
        finally{serial.Release();}
    }

    /// <summary>Submit actual pass7 only with its real retained graphics state.
    /// This performs the draw; publishing fully readable output11 remains a
    /// separate source-coverage join, rather than assuming allocation readiness.</summary>
    public async Task<OriginalMapPostEffect3ShaftCompletedFamily> CompleteActualStockPass7Async(
        IOriginalMapPostEffect3ShaftPass7DrawCapability actualDraw)
    {
        ArgumentNullException.ThrowIfNull(actualDraw);await serial.WaitAsync();
        try
        {
            lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
            var chain=LastComputeChain??throw new InvalidDataException("Actual Shaft CS chain has not completed.");
            if(LastCompletedFamily is not null)throw new InvalidOperationException("Actual Shaft family is already completed/skipped.");
            var prefix=chain.Prefix;RequirePrefix(prefix);
            if(chain.SourceSkippedAllShaftPasses||chain.OrderedComputePasses.Count!=6||
                !ReferenceEquals(firstWorld.Compute.LastCompletedPass,chain.OrderedComputePasses[5]))
                throw new InvalidDataException("Shaft7 requires this exact uninterrupted successful CS1..6 chain.");
            actualDraw.RequireCurrentForPass7(prefix);ArgumentException.ThrowIfNullOrWhiteSpace(actualDraw.SourceEvidence);
            var shaft=prefix.Parameters.RequirePreparedFactories().Shaft;
            _=OriginalMapPostEffect3ParameterLightShaft.PreparePassBindings(Bank.Source,7,shaft);
            var target=prefix.WorldStockScope.RequirePass7Target();
            var drawInput=new OriginalMapPostEffect3StockGraphicsFrameInputs(prefix.Context.FrameNumber,prefix.WorldStockScope.CurrentStock,
                prefix.Textures,ReadActualInputs(prefix,7),Array.AsReadOnly(actualDraw.ActualPixelSamplers.ToArray()),
                new Dictionary<int,int>{{shaft.ConstantBuffer!.Declaration.Index,prefix.ActualShaftConstantBufferInstance}},
                target,actualDraw.ActualViewport,actualDraw.ActualContext1C48,
                OriginalMapFinalOutputRasterSnapshot.Capture(actualDraw.ActualRaster),actualDraw.SourceEvidence);
            RequireFullStockWriteProfile(drawInput);
            pass7Producer=actualDraw;pass7Inputs=drawInput;
            pass7ConstantBufferRevision=Bank.RequireConstantBufferForPass(1,prefix.ActualShaftConstantBufferInstance,7,prefix.Context.FrameNumber).UploadRevision;
            // Hold the bank throughout the graphics queue and temporary set
            // retirement; no texture-only draw can race resource disposal.
            using var bankScope=await Bank.AcquireBufferExecutionScopeAsync();
            pass7WriterRevision=Bank.InvalidateTextureAfterShaderWrite(11,target.InstanceIndex,0,prefix.Context.FrameNumber,7);
            var complete=await graphics.DrawAsync(7,drawInput);
            actualDraw.RequireCurrentForPass7(prefix);RequirePrefix(prefix);
            LastCompletedFamily=new(chain,complete);
            _=Bank.PublishShaftGraphicsTextureCoverage(OriginalMapPostEffect3ShaftGraphicsWriteCoverage.Prepare(Bank.Source,shaft),
                this,graphics,complete,pass7WriterRevision);
            var output=Bank.CaptureSourceWrittenTextureAliasLease(prefix.Textures,shaft.OutputTextureAlias.TargetParameterIndex,
                prefix.Context.FrameNumber,0,1);
            return LastCompletedFamily=LastCompletedFamily with{OutputColor=output};
        }
        finally{serial.Release();}
    }

    // Only this real executor path has captured the raster and current draw
    // fields; a generic public stock completion record cannot assert them.
    internal void RequireCurrentActualStockCoverage(OriginalMapPostEffect3StockGraphicsExecutor actualExecutor,
        OriginalMapPostEffect3StockGraphicsCompletedPass completed,long writerRevision)
    {
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        var chain=LastComputeChain??throw new InvalidDataException("Actual Shaft chain is unavailable.");
        var input=pass7Inputs??throw new InvalidDataException("Actual source Shaft7 draw inputs were not accepted.");
        var producer=pass7Producer??throw new InvalidDataException("Actual source Shaft7 draw producer is unavailable.");
        RequirePrefix(chain.Prefix);producer.RequireCurrentForPass7(chain.Prefix);RequireFullStockWriteProfile(input);
        var now=producer.ActualRaster;var saved=input.SourceRaster;
        if(!ReferenceEquals(actualExecutor,graphics)||!ReferenceEquals(actualExecutor.LastCompletedPass,completed)||
            !ReferenceEquals(LastCompletedFamily?.ActualStockPass7,completed)||writerRevision!=pass7WriterRevision||
            input.ActualViewport!=producer.ActualViewport||input.ActualContext1C48!=producer.ActualContext1C48||
            input.CurrentDrawContextEvidence!=producer.SourceEvidence||now.Cull!=saved.Cull||now.FrontFace!=saved.FrontFace||
            now.DepthTest!=saved.DepthTest||now.DepthWrite!=saved.DepthWrite||now.DepthCompare!=saved.DepthCompare||
            now.NativeStencil is not null||!now.Attachments.SequenceEqual(saved.Attachments)||
            Bank.RequireConstantBufferForPass(1,chain.Prefix.ActualShaftConstantBufferInstance,7,chain.Prefix.Context.FrameNumber).UploadRevision!=pass7ConstantBufferRevision)
            throw new InvalidDataException("Actual Shaft7 source raster, writer, constants or owned completion changed.");
    }
    private static void RequireFullStockWriteProfile(OriginalMapPostEffect3StockGraphicsFrameInputs input)
    {
        var r=input.SourceRaster;
        if(r.Cull!=RenderingDevice.PolygonCullMode.Disabled||r.DepthTest||r.DepthWrite||r.NativeStencil is not null||
            r.Attachments is not [{EnableBlend:false,WriteR:true,WriteG:true,WriteB:true,WriteA:true}]||
            input.ActualViewport.Position!=Vector2I.Zero||input.ActualViewport.Size!=input.CurrentTarget.Extent||
            !float.IsFinite(input.ActualContext1C48)||input.ActualContext1C48 is <0 or >1||
            input.CurrentStock.CurrentScreenScale29E0!=System.Numerics.Vector2.One)
            throw new NotSupportedException("Source Shaft7 full coverage needs actual unclipped full stock quad, no culling/depth/stencil/blend and complete RGBA writes.");
    }

    private IReadOnlyList<IOriginalMapPostEffect3ComputeTextureLease> ReadActualInputs(
        OriginalMapPostEffect3FirstWorldPreparedPrefix prefix,int pass)
    {
        var result=new List<IOriginalMapPostEffect3ComputeTextureLease>();
        foreach(int index in Bank.Source.Passes[pass].ActiveBindings.Where(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
            b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead).Select(b=>b.ParameterIndex).Distinct())
        {
            var reference=prefix.Textures.Require(index).Reference;
            if(reference.PhysicalAllocationParameterIndex is not null)
                result.Add(Bank.CaptureSourceWrittenTextureAliasLease(prefix.Textures,index,prefix.Context.FrameNumber,0,1));
            else if(reference.ProducerOwnerIdentity==prefix.Context.OutputFrame.Textures[0x064B746B].EffectiveSource.OwnerIdentity)
                result.Add(prefix.WorldStockScope.LeaseWorkingScene(index));
            else if(reference.ProducerOwnerIdentity==prefix.Context.TerrainFrame.Textures[0xE13DDB4F].EffectiveSource.OwnerIdentity)
                result.Add(prefix.WorldStockScope.LeaseInputDepth(prefix.InputDepthSource,index));
            else throw new NotSupportedException("Actual Shaft input has no same-frame source contents producer: "+index);
        }
        return result.AsReadOnly();
    }
    private void RequirePrefix(OriginalMapPostEffect3FirstWorldPreparedPrefix prefix)
    {
        prefix.RequireCurrent();
        var invocation=firstWorld.CurrentRetainedInvocation??throw new InvalidDataException("Shaft continuation needs its current retained source invocation.");
        invocation.RequireModule(Bank.Source);invocation.RequireInitializedFrame(prefix.Parameters);
        if(!ReferenceEquals(firstWorld.CurrentPrefix,prefix)||!ReferenceEquals(prefix.Parameters.Allocation,Bank.Source)||
            !ReferenceEquals(prefix.WorldStockScope.Bank,Bank)||prefix.Context.FrameNumber!=invocation.FrameNumber)
            throw new InvalidDataException("Shaft continuation requires its exact first World owner/private source frame.");
    }
    public ValueTask DisposeAsync()
    {
        lock(lifetime){if(disposal is not null)return new(disposal);retiring=true;return new(disposal=RetireAsync());}
    }
    private async Task RetireAsync()
    {
        await serial.WaitAsync();try{await graphics.DisposeAsync();LastCompletedFamily=null;LastComputeChain=null;pass7Producer=null;pass7Inputs=null;}
        finally{serial.Release();}
    }
}
