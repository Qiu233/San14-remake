using San14.Data;

public sealed partial class OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner
{
    public object CaptureSourceIngressEvidence()
    {
        RequireCurrent();
        return new{frameNumber=World.Context.FrameNumber,sourceKind=retainedIngress is null?"first-owned-constructor":"retained-actual-port-return",
            stockPreviousCompletedSourceFrame=World.CurrentStock.PreviousCompletedSourceFrameNumber,
            previousAcceptedPortFrame=retainedIngress?.Continuation.FrameNumber,
            previousAcceptedGraphicsRelease=retainedIngress?.Continuation.LastActualGraphicsPass,
            currentColorStateF4,currentScissor,currentMinimumDepth,currentMaximumDepth,
            currentTargetRectangleCapturedAtBind=true,firstConstructorReapplied=retainedIngress is null,
            originalRawScopeEquivalence=false,grantsPixels=false,SourceEvidence};
    }
    /// <summary>A new current World loan of the exact completed module and
    /// frontend. Fresh constructor state is not reapplied to this generation.</summary>
    internal static OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner CaptureRetainedSource(
        OriginalGameRepository repository,OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame actualPreparation,
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner actualWorld,
        OriginalMapPostEffect3WorldControlledModuleOwner actualModule,
        OriginalMapPostEffect3Functor1ModuleInvocation actualInvocation)
    {
        actualPreparation.RequireCurrent();actualWorld.RequireCurrent();
        var previous=actualModule.RequirePreviousFrontendReturn(actualInvocation,actualWorld);
        var continuation=previous.Scope.DrawContinuation??
            throw new InvalidDataException("Retained frontend lacks its actual last graphics Release/End scope continuation.");
        var ingress=new OriginalMapPostEffect3RetainedFrontendIngress(actualModule,actualInvocation,continuation);
        ingress.RequireCurrent(actualPreparation,actualWorld);
        var sources=actualModule.StaticSources;sources.RequireRepository(repository);
        return new(actualPreparation,actualWorld,sources.NativeFrontend,sources.EyeGraphics,ingress);
    }
    private OriginalMapPostEffect3ManagedDrawContinuation CaptureReleasedDrawContinuation()
    {
        var state=LastRestoredNativeState??throw new InvalidDataException("Actual End needs its accepted graphics restoration snapshot.");
        var scope=moduleScope??throw new InvalidDataException("Actual End has no source invocation.");
        if(lastReleasedGraphicsPass is not int pass||submissionLedger is null||
            !submissionLedger.HistoricalReceipts.Any(r=>r.SourcePassIndex==pass)||
            state.FrameNumber!=World.Context.FrameNumber||state.OwnedFrontendIdentity!=World.CurrentStock.OwnedFrontendIdentity||
            state.ViewIdentity!=World.CurrentStock.ViewIdentity||state.ActualNativeRt0BlendEnable1C0!=0||
            !float.IsFinite(state.ActualViewport.MinimumDepth)||!float.IsFinite(state.ActualViewport.MaximumDepth))
            throw new InvalidDataException("Frontend continuation has no exact actual accepted Release and restored endpoints/scissor.");
        submissionLedger.RequireHistoricalAuthority();
        return new(scope.Invocation.Begin.Counter,World.CurrentStock.Source,scope.Parent.Owner,state,pass);
    }
}

/// <summary>Frozen value continuation of an actual accepted graphics Release.
/// Contains no prior frame/World/invocation graph and proves no native raw scope.</summary>
internal sealed class OriginalMapPostEffect3ManagedDrawContinuation
{
    internal OriginalMapPostEffect3ManagedDrawContinuation(uint counter,OriginalMapCopy97CurrentStockSource stock,
        OriginalMapPostEffect3WorldCompositeSourceOwner parent,OriginalMapPostEffect3EyeGraphicsCurrentState actual,int pass)
    {
        Counter=counter;Stock=stock;Parent=parent;FrameNumber=actual.FrameNumber;OwnedFrontendIdentity=actual.OwnedFrontendIdentity;
        ViewIdentity=actual.ViewIdentity;ContextOwnerIdentity=actual.ContextOwnerIdentity;ColorStateF4=actual.ActualNativeColorStateF4;
        Scissor=actual.ActualScissor;MinimumDepth=actual.ActualViewport.MinimumDepth;MaximumDepth=actual.ActualViewport.MaximumDepth;
        LastActualGraphicsPass=pass;
    }
    internal readonly uint Counter,ColorStateF4;
    internal readonly long FrameNumber;
    internal readonly OriginalMapCopy97CurrentStockSource Stock;
    internal readonly OriginalMapPostEffect3WorldCompositeSourceOwner Parent;
    internal readonly string OwnedFrontendIdentity,ViewIdentity,ContextOwnerIdentity;
    internal readonly OriginalFinalEffectScissor? Scissor;
    internal readonly float MinimumDepth,MaximumDepth;
    internal readonly int LastActualGraphicsPass;
    internal void RequireNext(long frameNumber,uint counter,long priorCompletedFrame,
        OriginalMapCopy97CurrentStockSource stock,OriginalMapPostEffect3WorldCompositeSourceOwner parent,
        string frontend,string view,string context,bool usesConstructor)
    {
        if(usesConstructor||FrameNumber+1!=frameNumber||Counter==uint.MaxValue||counter!=Counter+1||
            priorCompletedFrame!=FrameNumber||!ReferenceEquals(stock,Stock)||!ReferenceEquals(parent,Parent)||
            frontend!=OwnedFrontendIdentity||view!=ViewIdentity||context!=ContextOwnerIdentity)
            throw new InvalidDataException("Retained frontend value continuation is stale, skipped, constructor-reset or owned by another source/parent.");
    }
}

/// <summary>Closed current source admission. Previous authority is rejoined
/// through the module's bounded window, without strongly retaining old returns.</summary>
internal sealed class OriginalMapPostEffect3RetainedFrontendIngress
{
    private readonly OriginalMapPostEffect3WorldControlledModuleOwner module;
    private readonly OriginalMapPostEffect3Functor1ModuleInvocation invocation;
    internal OriginalMapPostEffect3RetainedFrontendIngress(OriginalMapPostEffect3WorldControlledModuleOwner module,
        OriginalMapPostEffect3Functor1ModuleInvocation invocation,OriginalMapPostEffect3ManagedDrawContinuation continuation)
        =>(this.module,this.invocation,Continuation)=(module,invocation,continuation);
    internal OriginalMapPostEffect3ManagedDrawContinuation Continuation{get;}
    internal string SourceEvidence=>$"actual-owned-retained-frontend;previous-port-frame={Continuation.FrameNumber};"+
        $"previous-actual-graphics-Release={Continuation.LastActualGraphicsPass};same-module/current-World-parent-stock-table-counter;"+
        "retained-F4/scissor/depth-endpoints-from-accepted-Release;current-target-viewport-from-actual-mip-wrapper;no-native-raw-equivalence";
    internal void RequireCurrent(OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame preparation,
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner world)
    {
        var prior=module.RequirePreviousFrontendReturn(invocation,world);
        preparation.ActualFrontendParent.RequireCurrent();
        if(!ReferenceEquals(prior.Scope.DrawContinuation,Continuation)||!prior.Scope.ManagedEndExecuted||!prior.Scope.ManagedOuterRestored||
            !ReferenceEquals(preparation.Context,world.Context)||!ReferenceEquals(preparation.Allocation,world.Bank.Source)||
            preparation.MainSelector.Selector!=0||invocation.FrameNumber!=preparation.FrameNumber||
            module.RetainedDepthCompare1C44!=7||module.RetainedMatchingDepthTargetCount1C40!=0||module.RetainedContextWord1C48!=0)
            throw new InvalidDataException("Retained frontend requires its closed immediately preceding actual return and this current World/table/parent/source generation.");
        Continuation.RequireNext(preparation.FrameNumber,invocation.Begin.Counter,world.CurrentStock.PreviousCompletedSourceFrameNumber,
            world.CurrentStock.Source,preparation.ActualFrontendParent.Owner,world.CurrentStock.OwnedFrontendIdentity,
            world.CurrentStock.ViewIdentity,world.TextureParameters.ContextOwnerIdentity,world.CurrentStock.UsesConstructedFrontendState);
    }
}
