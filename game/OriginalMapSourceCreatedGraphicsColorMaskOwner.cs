using San14.Data;

/// <summary>Published only by the owned complete final wrapper after its
/// actual output and source histories commit. This is device
/// submission acknowledgement, not a GPU fence or readback.</summary>
public sealed class OriginalMapSourceCreatedGraphicsCompletedFrame
{
    private readonly OriginalMapSourceCreatedGraphicsColorMaskOwner owner;
    private readonly OriginalMapFinalOutputProductionPrepared? legacyFinal;
    private readonly long legacyOrdinal;
    internal OriginalMapSourceCreatedGraphicsCompletedFrame(OriginalMapSourceCreatedGraphicsColorMaskOwner owner,
        OriginalNativeMapFrameContext context,OriginalMapFinalOutputProductionPrepared final,long ordinal,
        OriginalMapCopy97CurrentStockPacket? stock,OriginalSkyCloudSourceClipPlanePacket? clip)
    { this.owner=owner;Context=context;legacyFinal=final;legacyOrdinal=ordinal;Stock=stock;Clip=clip; }
    internal OriginalMapSourceCreatedGraphicsCompletedFrame(OriginalMapSourceCreatedGraphicsColorMaskOwner owner,
        OriginalNativeMapFrameContext context,OriginalMapPostEffect3ControlledHostCompleted controlled,
        OriginalMapCopy97CurrentStockPacket stock,OriginalSkyCloudSourceClipPlanePacket? clip)
    { this.owner=owner;Context=context;ControlledCompletion=controlled;Stock=stock;Clip=clip; }
    public OriginalNativeMapFrameContext Context { get; }
    public OriginalMapFinalOutputProductionPrepared ActualFinalConstants => legacyFinal??
        throw new InvalidOperationException("Controlled output has its own completion; no legacy final constants were executed.");
    public OriginalMapPostEffect3ControlledHostCompleted? ControlledCompletion { get; }
    public long ActualFinalChainOrdinal => ControlledCompletion is null?legacyOrdinal:
        throw new InvalidOperationException("Controlled output does not advance the legacy final chain.");
    public OriginalMapCopy97CurrentStockPacket? Stock { get; }
    public OriginalSkyCloudSourceClipPlanePacket? Clip { get; }
    public void RequireCurrent() => owner.RequireCurrentCompletion(this);
}

/// <summary>Borrowed source final owner plus one retained owned frontend mask
/// history. It allocates no GPU resources. The successful actual complete final
/// call is the acknowledgement; preparing packets never advances history.</summary>
public sealed class OriginalMapSourceCreatedGraphicsColorMaskOwner : IAsyncDisposable
{
    private readonly OriginalMapFinalOutputProductionFactory final;
    private readonly OriginalMapOwnedGraphicsColorMaskHistorySource source;
    private readonly object lifetime=new();
    private OriginalNativeMapFrameContext? pendingContext;
    private OriginalMapOwnedGraphicsMaskPrefix? pending;
    private bool started,retiring,finalRunning;
    private OriginalMapCopy97CurrentStockSource? stockSource;
    private Func<OriginalNativeMapFrameContext,OriginalMapCopy97CurrentStockPacket>? currentStock;
    private OriginalSkyCloudSourceClipPlane? clipSource;
    private Func<OriginalNativeMapFrameContext,OriginalSkyCloudSourceClipPlanePacket>? currentClip;
    private TaskCompletionSource? drained;
    private Task? disposal;
    private OriginalMapSourceCreatedGraphicsColorMaskOwner(OriginalMapFinalOutputProductionFactory final,
        OriginalMapOwnedGraphicsColorMaskHistorySource source)
    { this.final=final;this.source=source; }
    public long CompletedSourceFrameNumber => source.CompletedSourceFrameNumber;
    /// <summary>The exact privately created frontend history, borrowed by
    /// finite current stock-field producers. It cannot be replaced.</summary>
    public OriginalMapOwnedGraphicsColorMaskHistorySource OwnedSource => source;
    public OriginalNativeMapFrameContext? LastCompletedContext { get; private set; }
    public OriginalMapSourceCreatedGraphicsCompletedFrame? LastCompletedFrame { get; private set; }
    public static OriginalMapSourceCreatedGraphicsColorMaskOwner ReadFresh(OriginalGameRepository repository,
        string locale,string ownedFrontendIdentity,string ownedViewIdentity,
        OriginalMapFinalOutputProductionFactory actualSourceFinal)
    {
        ArgumentNullException.ThrowIfNull(actualSourceFinal);
        if(!actualSourceFinal.Chain.HasCompleteSourceEffects ||
            actualSourceFinal.Chain.Final.TargetRoute!=OriginalMapFinalOutputTargetRoute.SourceHostParentOutput ||
            actualSourceFinal.Chain.Final.Program.DeadColorCorrection is null)
            throw new InvalidDataException("Owned mask history requires the actual complete source Host direct-parent final owner.");
        return new(actualSourceFinal,OriginalMapOwnedGraphicsColorMaskHistorySource.ReadFresh(repository,locale,
            ownedFrontendIdentity,ownedViewIdentity,actualSourceFinal.Chain.Source));
    }
    /// <summary>Attach the actual finite stock owner before WORK starts. The
    /// getter must return its privately checked current stage packet. It is
    /// read again after final completion to reject a replaced attempt.</summary>
    public void BindCurrentStockHistory(OriginalMapCopy97CurrentStockSource actualStock,
        Func<OriginalNativeMapFrameContext,OriginalMapCopy97CurrentStockPacket> actualCurrentStock)
    {
        ArgumentNullException.ThrowIfNull(actualStock);ArgumentNullException.ThrowIfNull(actualCurrentStock);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(stockSource is not null || started || finalRunning || source.CompletedSourceFrameNumber!=0 ||
                !ReferenceEquals(actualStock.OwnedFrontendSource,source))
                throw new InvalidDataException("Stock history must bind this exact fresh owned frontend once, before actual WORK.");
            stockSource=actualStock;currentStock=actualCurrentStock;
        }
    }
    /// <summary>The actual source factory bound the private stock backend's
    /// getter before WORK. Borrow it for this pending current attempt without
    /// waiting for final-output history acknowledgement.</summary>
    public OriginalMapCopy97CurrentStockPacket RequireCurrentStockPacket(OriginalNativeMapFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(pending is null||stockSource is null||currentStock is null)
                throw new InvalidDataException("Actual current stock source/pending frontend prefix is unavailable.");
            RequireCurrent(context,pending);
            var packet=currentStock(context);
            if(!ReferenceEquals(packet.Source,stockSource)||!ReferenceEquals(stockSource.OwnedFrontendSource,source)||
                packet.FrameNumber!=context.FrameNumber||packet.ViewIdentity!=source.OwnedViewIdentity)
                throw new InvalidDataException("Private factory stock getter returned another frontend/source attempt.");
            packet.ValidateCurrent();return packet;
        }
    }
    /// <summary>Borrow the actual Cloud special-cache history once, after the
    /// finite current stock owner is bound and before WORK. The getter returns
    /// only the private same-attempt Cloud candidate; bootstrap plans reject.</summary>
    public void BindCurrentClipHistory(OriginalSkyCloudSourceClipPlane actualClip,
        Func<OriginalNativeMapFrameContext,OriginalSkyCloudSourceClipPlanePacket> actualCurrentClip)
    {
        ArgumentNullException.ThrowIfNull(actualClip);ArgumentNullException.ThrowIfNull(actualCurrentClip);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(clipSource is not null || stockSource is null || started || finalRunning || pending is not null ||
                source.CompletedSourceFrameNumber!=0 || actualClip.CompletedSourceFrameNumber!=0)
                throw new InvalidDataException("Clip history must bind this fresh finite stock/frontend owner once, before actual WORK.");
            actualClip.BindOwnedFrontend(source);
            clipSource=actualClip;currentClip=actualCurrentClip;
        }
    }
    public OriginalMapOwnedGraphicsMaskPrefix PrepareCurrentSkyPrefix(OriginalNativeMapFrameContext context,
        string actualViewIdentity,string currentProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(context.FrameNumber<1 || context.PreparedCamera is null || started || finalRunning ||
                (pendingContext is not null&&!ReferenceEquals(pendingContext,context)))
                throw new InvalidDataException("Owned mask prefix requires this exact accepted attempt and a completed previous attempt.");
            pending=source.PrepareCurrentSkyPrefix(new(context.FrameNumber,actualViewIdentity,currentProducerProvenance));
            pendingContext=context;return pending;
        }
    }
    /// <summary>Invoke immediately before actual WORK starts. An attempt that
    /// later fails remains consumed; target resize does not create fresh state.</summary>
    public void BeginWorkSubmission(OriginalNativeMapFrameContext context,OriginalMapOwnedGraphicsMaskPrefix prefix)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);RequireCurrent(context,prefix);
            if(started)throw new InvalidDataException("WORK already started for this mask publication.");
            source.BeginWorkSubmission(prefix);started=true;
        }
    }
    /// <summary>Use in the owned factory's actual AfterDeferred stage in place
    /// of calling the borrowed final factory separately. No duplicate draw.
    /// Only this successful complete source call makes the next Sky prefix
    /// eligible. GPU uploads and other completed frames cannot acknowledge it.</summary>
    public async Task<OriginalRenderOutputPresentation> RenderSourceFinalAndCompleteAsync(
        OriginalNativeMapFrameContext context,OriginalMapFinalOutputProductionFrameInputs actualFinalInputs)
    {
        ArgumentNullException.ThrowIfNull(actualFinalInputs);
        OriginalMapOwnedGraphicsMaskPrefix accepted;long completedBefore;
        OriginalMapCopy97CurrentStockPacket? acceptedStock;
        OriginalSkyCloudSourceClipPlanePacket? acceptedClip;
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            accepted=pending??throw new InvalidDataException("Source final mask history has no current WORK prefix.");
            RequireCurrent(context,accepted);
            if(!started || finalRunning || actualFinalInputs.Lighting.Binding.ViewIdentity!=source.OwnedViewIdentity)
                throw new InvalidDataException("Source final mask acknowledgement is out of order or belongs to another View.");
            completedBefore=final.Chain.CompletedFrameCount;finalRunning=true;
            acceptedStock=null;
            acceptedClip=null;
        }
        try
        {
            lock(lifetime)
            {
                if(currentStock is not null)
                {
                    acceptedStock=currentStock(context);
                    if(!ReferenceEquals(acceptedStock.Source,stockSource) || acceptedStock.FrameNumber!=context.FrameNumber)
                        throw new InvalidDataException("Current stock packet does not identify this complete final attempt.");
                    acceptedStock.ValidateCurrent();
                }
                if(currentClip is not null)
                {
                    acceptedClip=currentClip(context);
                    if(!ReferenceEquals(acceptedClip.Source,clipSource) || acceptedClip.IsConstructorPlan ||
                        acceptedClip.FrameNumber!=context.FrameNumber || acceptedClip.ViewIdentity!=source.OwnedViewIdentity ||
                        acceptedStock is null)
                        throw new InvalidDataException("Current Cloud clip does not identify the exact submitted finite source attempt.");
                    clipSource!.ValidateSubmittedCurrentFamily(acceptedClip,acceptedStock);
                }
            }
            var output=await final.RenderAsync(context,actualFinalInputs);
            lock(lifetime)
            {
                ObjectDisposedException.ThrowIf(retiring,this);RequireCurrent(context,accepted);
                var completed=final.LastAcceptedConstants??throw new InvalidDataException("Source final did not publish successful constants.");
                if(final.Chain.CompletedFrameCount!=completedBefore+1 || completed.FrameNumber!=context.FrameNumber ||
                    final.Constants.CommittedFrameNumber!=context.FrameNumber)
                    throw new InvalidDataException("Another source final call cannot acknowledge this accepted mask attempt.");
                if(acceptedStock is not null && !ReferenceEquals(acceptedStock,currentStock!(context)))
                    throw new InvalidDataException("Current stock stage packet was replaced during the actual final call.");
                if(acceptedClip is not null && !ReferenceEquals(acceptedClip,currentClip!(context)))
                    throw new InvalidDataException("Current Cloud clip packet was replaced during the actual final call.");
                // Validate all before any commit. Clip then Stock replay run
                // while the mask prefix and actual stock packet are active.
                // Any failure invalidates all paired histories.
                source.ValidateCompletedSourceHostOutput(accepted,completed);
                if(acceptedStock is not null)
                    stockSource!.ValidateCompletedSourceHostOutput(acceptedStock,completed);
                if(acceptedClip is not null)
                    clipSource!.ValidateCompletedSourceHostOutput(acceptedClip,acceptedStock!,completed);
                if(acceptedClip is not null)
                    clipSource!.ReplayCompletedSourceHostOutput(acceptedClip,acceptedStock!,completed);
                if(acceptedStock is not null)
                {
                    stockSource!.ReplayCompletedSourceHostOutput(acceptedStock,completed);
                }
                source.ReplayCompletedSourceHostOutput(accepted,completed);
                LastCompletedContext=context;pendingContext=null;pending=null;started=false;
                LastCompletedFrame=new(this,context,completed,final.Chain.CompletedFrameCount,acceptedStock,acceptedClip);
            }
            return output;
        }
        catch
        {
            lock(lifetime)
            {
                source.MarkFailedOrUnknownWriter("failed/interrupted-source-Host-attempt");
                stockSource?.MarkFailedImportedOrUnknownWriter("failed/interrupted-source-Host-attempt");
                clipSource?.MarkImportedOrUnknownPlaneOrMaskWriter("failed/interrupted-source-Host-attempt");
                LastCompletedFrame=null;
            }
            throw;
        }
        finally
        { lock(lifetime){finalRunning=false;if(retiring)drained?.TrySetResult();} }
    }
    /// <summary>Commit the normal controlled renderer only after its actual
    /// module output and selected caller tail returned. Preparing a tail or
    /// presenting an image does not acknowledge the source histories.</summary>
    public OriginalMapSourceCreatedGraphicsCompletedFrame CompleteControlledSourceHostOutput(
        OriginalNativeMapFrameContext context,OriginalMapPostEffect3ControlledHostCompleted completed)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(completed);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var accepted=pending??throw new InvalidDataException("Controlled output has no submitted source prefix.");
            RequireCurrent(context,accepted);
            if(!started||finalRunning||currentStock is null||stockSource is null||
                completed.FrameNumber!=context.FrameNumber)
                throw new InvalidDataException("Controlled source acknowledgement is out of order.");
            finalRunning=true;
            try
            {
                var stock=currentStock(context);
                if(!ReferenceEquals(stock.Source,stockSource)||!ReferenceEquals(completed.CurrentStock,stock))
                    throw new InvalidDataException("Controlled completion belongs to another actual stock packet.");
                stock.ValidateCurrent();
                var clip=currentClip?.Invoke(context);
                if(clip is not null)
                {
                    if(!ReferenceEquals(clip.Source,clipSource)||clip.IsConstructorPlan||
                        clip.FrameNumber!=context.FrameNumber||clip.ViewIdentity!=source.OwnedViewIdentity)
                        throw new InvalidDataException("Controlled completion belongs to another actual Cloud clip packet.");
                    clipSource!.ValidateSubmittedCurrentFamily(clip,stock);
                }
                completed.RequireCompletedSourceHostWriterScope();
                source.ValidateCompletedSourceHostOutput(accepted,completed);
                stockSource.ValidateCompletedSourceHostOutput(stock,completed);
                if(clip is not null)clipSource!.ValidateCompletedSourceHostOutput(clip,stock,completed);
                if(!ReferenceEquals(stock,currentStock(context))||
                    clip is not null&&!ReferenceEquals(clip,currentClip!(context)))
                    throw new InvalidDataException("Source packets changed during controlled completion.");
                if(clip is not null)clipSource!.ReplayCompletedSourceHostOutput(clip,stock,completed);
                stockSource.ReplayCompletedSourceHostOutput(stock,completed);
                source.ReplayCompletedSourceHostOutput(accepted,completed);
                LastCompletedContext=context;pendingContext=null;pending=null;started=false;
                return LastCompletedFrame=new(this,context,completed,stock,clip);
            }
            catch
            {
                source.MarkFailedOrUnknownWriter("failed-controlled-source-Host-completion");
                stockSource.MarkFailedImportedOrUnknownWriter("failed-controlled-source-Host-completion");
                clipSource?.MarkImportedOrUnknownPlaneOrMaskWriter("failed-controlled-source-Host-completion");
                LastCompletedFrame=null;throw;
            }
            finally{finalRunning=false;if(retiring)drained?.TrySetResult();}
        }
    }
    public void MarkFailedOrUnknownMaskWriter(string actualWriter)
    { lock(lifetime){source.MarkFailedOrUnknownWriter(actualWriter);stockSource?.MarkFailedImportedOrUnknownWriter(actualWriter);clipSource?.MarkImportedOrUnknownPlaneOrMaskWriter(actualWriter);LastCompletedFrame=null;} }
    internal void RequireCurrentCompletion(OriginalMapSourceCreatedGraphicsCompletedFrame completed)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(LastCompletedFrame,completed) || !ReferenceEquals(LastCompletedContext,completed.Context) ||
                source.CompletedSourceFrameNumber!=completed.Context.FrameNumber ||
                (completed.ControlledCompletion is null&&(
                    !ReferenceEquals(final.LastAcceptedConstants,completed.ActualFinalConstants) ||
                    final.Chain.CompletedFrameCount!=completed.ActualFinalChainOrdinal ||
                    final.Constants.CommittedFrameNumber!=completed.Context.FrameNumber)) ||
                (stockSource is not null&&stockSource.CompletedSourceFrameNumber!=completed.Context.FrameNumber) ||
                (clipSource is not null&&(clipSource.CompletedSourceFrameNumber!=completed.Context.FrameNumber ||
                    completed.Clip is null || !ReferenceEquals(completed.Clip.Source,clipSource))))
                throw new InvalidDataException("Complete graphics submission was superseded, failed or belongs to another owner.");
            if(completed.Clip is not null)clipSource!.RequireCompletedPacket(completed.Clip);
        }
    }
    private void RequireCurrent(OriginalNativeMapFrameContext context,OriginalMapOwnedGraphicsMaskPrefix prefix)
    {
        if(!ReferenceEquals(context,pendingContext) || !ReferenceEquals(prefix,pending) || prefix.FrameNumber!=context.FrameNumber)
            throw new InvalidDataException("Mask publication belongs to another context/attempt/source owner.");
    }
    public ValueTask DisposeAsync()
    {
        lock(lifetime)
        {
            retiring=true;source.MarkFailedOrUnknownWriter("owned-frontend-retired");
            stockSource?.MarkFailedImportedOrUnknownWriter("owned-frontend-retired");LastCompletedFrame=null;
            clipSource?.MarkImportedOrUnknownPlaneOrMaskWriter("owned-frontend-retired");
            return new(disposal??=RetireAsync());
        }
    }
    private async Task RetireAsync()
    {
        Task wait;lock(lifetime){wait=finalRunning?(drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task:Task.CompletedTask;}
        await wait;
        lock(lifetime){pending=null;pendingContext=null;LastCompletedContext=null;LastCompletedFrame=null;}
        // The source final factory is borrowed and retired by its actual owner.
    }
}
