namespace San14.Data;

/// <summary>Implemented by a closed Game adapter joining its actual module
/// PortReturn, current completed presentation target and required caller-tail
/// command. This source interface creates no device authority.</summary>
public interface IMapPostEffect3ControlledHostOutputAuthority
{
    MapPostEffect3OwnedControlledCameraFrame ControlledCamera{get;}
    MainMapContextCurrentPacket CurrentContext{get;}
    MapPostEffect3ApplyViewSlotSource ApplyView{get;}
    MapCopy97CurrentStockPacket CurrentStock{get;}
    MapPostEffect3TextureParameterState TextureParameters{get;}
    MapPostEffect3Functor1SourceReceipt CpuDirectoryReturned{get;}
    MapPostEffect3Functor1SourceReceipt SourceControllerReturned{get;}
    MapPostEffect3Functor1SelectedDeviceJoin ActualDeviceJoin{get;}
    int FinalOutputSourcePassIndex{get;}
    object ActualPresentationIdentity{get;}
    long ActualPresentationRevision{get;}
    MapPostEffect3ControlledHostTailRecipe PostApplyTail{get;}
    object? ActualPostApplyExposureCopyIdentity{get;}
    string SourceEvidence{get;}
    void RequireActualReturnedOutput();
}

/// <summary>Source branch and wrapper identities for 's post-Functor
/// exposure publication. A different global50 target requires a real stock
/// command even after every selected Functor1 shader returned.</summary>
public sealed class MapPostEffect3ControlledHostTailRecipe
{
    private readonly MapPostEffect3TextureParameterState table;
    private readonly MapPostEffect3OwnedControlledCameraFrame camera;
    private readonly MapPostEffect3ControlledAuxFields aux;
    private readonly long? nullRevision;
    internal MapPostEffect3ControlledHostTailRecipe(LoadedMapPostEffect3ControlledHostSource source,
        MapPostEffect3OwnedControlledCameraFrame camera,MapPostEffect3TextureParameterState table,
        MapPostEffect3ControlledAuxFields aux,object? inputWrapper,object? globalWrapper,string evidence)
    {
        this.camera=camera;this.table=table;this.aux=aux;
        Camera1510Word=camera.CaptureAcceptedCamera().SourceSpecialRectangleFlag1510;
        LookupRequested=source.RequestsExposureLookupCandidate(Camera1510Word,aux.Flags8);
        if(LookupRequested)
        {
            ActualModuleExposurePointer=table.ReadKnownNullablePointer(61) is null?null:table.Require(61);
            if(ActualModuleExposurePointer is null)nullRevision=table.CaptureKnownNullRevision(61);
        }
        if((ActualModuleExposurePointer is null)!=(inputWrapper is null))
            throw new InvalidDataException("Post-Apply lookup must retain its real native-null or actual module61 wrapper identity.");
        ActualModuleWrapperIdentity=inputWrapper;ActualGlobalExposureWrapperIdentity=globalWrapper;
        RequiresExposureCopy=source.RequiresExposureCopyCandidate(inputWrapper,globalWrapper);
        SourceEvidence=source.SourceEvidence+";"+evidence+"-input-null-skip/-global50-null-skip/-same-wrapper-skip/otherwise-source-caller-copy";
        RequireCurrent();
    }
    public long FrameNumber=>camera.FrameNumber;
    public uint Camera1510Word{get;}
    public bool LookupRequested{get;}
    public bool RequiresExposureCopy{get;}
    public MapPostEffect3ResolvedTextureParameter? ActualModuleExposurePointer{get;}
    public object? ActualModuleWrapperIdentity{get;}
    public object? ActualGlobalExposureWrapperIdentity{get;}
    public string SourceEvidence{get;}
    public bool ProvesExposureCopyCompleted=>false;
    public void RequireCurrent()
    {
        if(table.FrameNumber!=FrameNumber||!ReferenceEquals(table.Allocation,camera.Allocation)||aux.FrameNumber!=FrameNumber||
            camera.Route.SelectedModuleFunctorIndex!=1||!camera.Route.UsesOwnedController)
            throw new InvalidDataException("Controlled Host caller tail belongs to another current source Camera/table.");
        if(LookupRequested)
        {if(ActualModuleExposurePointer is{} pointer)table.ValidateCurrent(pointer);else table.ValidateKnownNullRevision(61,nullRevision!.Value);}
    }
    internal void RequireSame(MapPostEffect3OwnedControlledCameraFrame actualCamera,
        MapPostEffect3TextureParameterState actualTable)
    {RequireCurrent();if(!ReferenceEquals(actualCamera,camera)||!ReferenceEquals(actualTable,table))
        throw new InvalidDataException("Post-Apply tail recipe was replaced by another prepared module/table.");}
}

public sealed class LoadedMapPostEffect3ControlledHostSource
{
    internal LoadedMapPostEffect3ControlledHostSource(LoadedDeferredSamplerNativeSource native,string evidence)
        =>(Native,SourceEvidence)=(native,evidence);
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public bool RequestsExposureLookupCandidate(uint actualCamera1510Word,uint actualAuxFlags8)
        =>(actualAuxFlags8&0x2000000)!=0||(actualCamera1510Word&1)!=0;
    /// <summary>Pure source branch. Wrapper identity is required: equal
    /// dimensions, pixels or descriptors do not make the pointers equal.</summary>
    public bool RequiresExposureCopyCandidate(object? actualInputWrapper,object? actualGlobalWrapper)
        =>actualInputWrapper is not null&&actualGlobalWrapper is not null&&!ReferenceEquals(actualInputWrapper,actualGlobalWrapper);
    public static LoadedMapPostEffect3ControlledHostSource Load(GameResourceRepository repository,string locale="TC")
        =>RenderSamplerSourceAdapters.ReadControlledHostSource(RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
    public MapPostEffect3ControlledHostTailRecipe PrepareTail(
        MapPostEffect3OwnedControlledCameraFrame actualCamera,MapPostEffect3TextureParameterState actualTable,
        MapPostEffect3ControlledAuxFields actualAux,object? actualLookedUpModuleWrapper,
        object? actualGlobalExposureWrapper,string actualWrapperProducerEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWrapperProducerEvidence);
        if(actualCamera.CaptureAcceptedCamera().ExecutableLocale!=Native.Locale)
            throw new InvalidDataException("Controlled Host tail and current Camera require the same native locale.");
        return new(this,actualCamera,actualTable,actualAux,actualLookedUpModuleWrapper,actualGlobalExposureWrapper,actualWrapperProducerEvidence);
    }
    public MapPostEffect3ControlledHostWriterScope ConstructOwnedWriterScope(MainMapContextCurrentSource actualQSource)
    {
        if(actualQSource.Host.Locale!=Native.Locale)
            throw new InvalidDataException("Controlled Host writer scope and Q require the same native source generation.");
        return new(this,new MapPostEffect3OwnedMainHostCallbackScope(actualQSource));
    }
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3ControlledHostSource ReadControlledHostSource(LoadedDeferredSamplerNativeSource native)
    {

        return new(native,native.RenderRuleIdentity+
            $"{native.Locale}:paired-Host-current-lookup/stock-tail-profile;/FB4-EA2OutputExposureScale-current-lookup;-null-input-skip"+
            "-null-global50-skip/-identical-wrapper-skip;8604;9A51-stock-exposure-copy"+
            "-source-clip-Begin-End/-B0-bind-domain;finite-owned-Q-tail;no-legacy-final-or-device-completion");
    }
}

/// <summary>Same owned Q's finite post-Apply callback/overlay family. Reuses
/// the Q constructor/leaf proof, while keeping F1 and fallback outputs distinct.</summary>
public sealed class MapPostEffect3ControlledHostWriterScope
{
    private readonly LoadedMapPostEffect3ControlledHostSource source;
    private readonly MapPostEffect3OwnedMainHostCallbackScope q;
    internal MapPostEffect3ControlledHostWriterScope(LoadedMapPostEffect3ControlledHostSource source,
        MapPostEffect3OwnedMainHostCallbackScope q)=>(this.source,this.q)=(source,q);
    public void MarkImportedOrUnknownCallbackOrOverlayWriter(string actualWriter)=>q.MarkImportedOrUnknownCallbackOrOverlayWriter(actualWriter);
    internal void Require(MapPostEffect3OwnedControlledCameraFrame camera,MainMapContextCurrentPacket context,
        MapPostEffect3ApplyViewSlotSource view)
    {
        RequireCurrentContext(context);
        if(camera.FrameNumber!=context.FrameNumber||view.FrameNumber!=context.FrameNumber||view.Index!=0||
            view.ContextOwnerIdentity!=context.Source.OwnedHostIdentity||camera.Route.SelectedModuleFunctorIndex!=1||
            !camera.Route.UsesOwnedController||camera.Route.ExecutesToneFallback||!camera.Route.BypassesLegacyA1Branch||
            camera.CaptureAcceptedCamera().ExecutableLocale!=context.Source.Host.Locale)
            throw new NotSupportedException("Controlled Host completion needs the exact current Main slot0/owned Q/Functor1 writer family.");
    }
    internal void RequireCurrentContext(MainMapContextCurrentPacket context)
    {
        q.RequireCurrentSource(context);
        bool reset=context.ContextFlags3F0==0&&context.Phase==MainMapContextCurrentPhase.DeferredPreparationReset;
        bool thin=context.ContextFlags3F0 is 0 or 0x400&&
            context.Phase==MainMapContextCurrentPhase.NativeThinSurfaceFlagWriter;
        bool specialized=context.ContextFlags3F0==0x40&&
            context.Phase==MainMapContextCurrentPhase.NativeSpecializedGeometryFlagWriter;
        if(context.ContextFlags3F4!=0||!reset&&!thin&&!specialized)
            throw new NotSupportedException("Functor1 Host Apply-tail scope requires its actual reset/Q0, thin-surface/Q0-or400 or specialized57-OR40/Q40 writer; other bits/imported Q are outside this source profile.");
    }
    public string SourceEvidence=>source.SourceEvidence+";"+q.Source.SourceProvenance+
        ";same-owned-Q-v170-ret-leaf/16-source-null-overlay-slots;Functor1-reset/source-thin-or-actual-specialized57-OR40;"+
        "controlled-module-return;shared-tail-viewport-restoration;Apply-tail-before-outer-OR20;no-output-claim";
}

/// <summary>Closed source Host acknowledgement with a live actual Game
/// authority. Does not create a GPU fence, raw native cache observation or
/// any next-frame pixel readiness.</summary>
public sealed class MapPostEffect3ControlledHostCompleted
{
    private readonly IMapPostEffect3ControlledHostOutputAuthority authority;
    private readonly MapPostEffect3ControlledHostWriterScope scope;
    private readonly object presentation;
    private readonly object? copy;
    private readonly MapPostEffect3ControlledHostTailRecipe tail;
    private readonly MapPostEffect3OwnedControlledCameraFrame controlledCamera;
    private readonly MapPostEffect3Functor1SourceReceipt controllerReturn,directoryReturn;
    private readonly MapPostEffect3Functor1SelectedDeviceJoin deviceJoin;
    private readonly MapPostEffect3ApplyViewSlotSource applyView;
    private readonly int finalSourcePass;
    private readonly long presentationRevision;
    private readonly byte[] prefix;
    private MapPostEffect3ControlledHostCompleted(IMapPostEffect3ControlledHostOutputAuthority authority,
        MapPostEffect3ControlledHostWriterScope scope)
    {
        this.authority=authority;this.scope=scope;CurrentStock=authority.CurrentStock;
        controlledCamera=authority.ControlledCamera;controllerReturn=authority.SourceControllerReturned;
        directoryReturn=authority.CpuDirectoryReturned;deviceJoin=authority.ActualDeviceJoin;
        applyView=authority.ApplyView;finalSourcePass=authority.FinalOutputSourcePassIndex;
        Camera=authority.ControlledCamera.CaptureAcceptedCamera();Context=authority.CurrentContext;
        presentation=authority.ActualPresentationIdentity;presentationRevision=authority.ActualPresentationRevision;
        tail=authority.PostApplyTail;copy=authority.ActualPostApplyExposureCopyIdentity;prefix=Camera.Prefix.ToArray();
    }
    public long FrameNumber=>Camera.FrameNumber;
    public MapCameraConstructionPacket Camera{get;}
    public MainMapContextCurrentPacket Context{get;}
    public MapCopy97CurrentStockPacket CurrentStock{get;}
    public uint ReturnedRt0ColorWriteMask=>CurrentStock.Prefix.ColorWriteMask;
    public bool ReturnedCustomVertexShaderIsAbsent=>CurrentStock.CurrentCustomVertexShaderIsAbsent;
    public bool ProvesNativeRawStateRestoration=>false;
    public bool ProvesNextFramePixelsReady=>false;
    public string SourceEvidence=>scope.SourceEvidence+";"+tail.SourceEvidence+";"+authority.SourceEvidence+
        ";F1-End-restores-entry-RT0-mask;stock-sixwords/B0-preserved;ScreenEnd-restores-clip-mask/neutral-plane-rebuilt;not-legacy-final";
    public static MapPostEffect3ControlledHostCompleted CaptureAfterActualReturnedOutput(
        IMapPostEffect3ControlledHostOutputAuthority actualAuthority,MapPostEffect3ControlledHostWriterScope actualWriterScope)
    {
        ArgumentNullException.ThrowIfNull(actualAuthority);ArgumentNullException.ThrowIfNull(actualWriterScope);
        actualAuthority.RequireActualReturnedOutput();
        var completed=new MapPostEffect3ControlledHostCompleted(actualAuthority,actualWriterScope);
        completed.RequireCompletedSourceHostWriterScope();return completed;
    }
    public void RequireCompletedSourceHostWriterScope()
    {
        authority.RequireActualReturnedOutput();scope.Require(authority.ControlledCamera,Context,authority.ApplyView);
        var controller=authority.SourceControllerReturned;var directory=authority.CpuDirectoryReturned;
        controller.RequireCurrent();directory.RequireCurrent();
        controller.Invocation.RequireCurrentExitedDirectory(directory,authority.TextureParameters);
        authority.ActualDeviceJoin.RequireActualReceipts();tail.RequireSame(authority.ControlledCamera,authority.TextureParameters);
        if(controller.Operation!=MapPostEffect3Functor1SourceOperation.ControllerReturned||!controller.Invocation.IsControllerReturned||
            !ReferenceEquals(controller.Invocation,directory.Invocation)||authority.ControlledCamera.Invocation.NativeControllerReturn!=true||
            !ReferenceEquals(controller.Invocation.State,controlledCamera.State)||
            !ReferenceEquals(authority.ControlledCamera,controlledCamera)||!ReferenceEquals(controller,controllerReturn)||
            !ReferenceEquals(directory,directoryReturn)||!ReferenceEquals(authority.ActualDeviceJoin,deviceJoin)||
            !ReferenceEquals(authority.ApplyView,applyView)||authority.FinalOutputSourcePassIndex!=finalSourcePass||
            !ReferenceEquals(authority.CurrentStock,CurrentStock)||!ReferenceEquals(authority.CurrentContext,Context)||
            authority.ControlledCamera.FrameNumber!=FrameNumber||authority.ActualDeviceJoin.FrameNumber!=FrameNumber||
            !Camera.Prefix.Span.SequenceEqual(prefix)||!Camera.Prefix.Span.SequenceEqual(authority.ControlledCamera.CaptureAcceptedCamera().Prefix.Span)||
            !ReferenceEquals(authority.ActualPresentationIdentity,presentation)||presentationRevision<=0||
            authority.ActualPresentationRevision!=presentationRevision||!ReferenceEquals(authority.PostApplyTail,tail)||
            !ReferenceEquals(authority.ActualPostApplyExposureCopyIdentity,copy)||tail.RequiresExposureCopy&&copy is null||
            !tail.RequiresExposureCopy&&copy is not null||authority.FinalOutputSourcePassIndex is <108 or >116||
            !authority.ActualDeviceJoin.ActualReceipts.Any(r=>r.SourcePassIndex==authority.FinalOutputSourcePassIndex)||
            string.IsNullOrWhiteSpace(authority.SourceEvidence))
            throw new InvalidDataException("Controlled Host completion lacks the exact returned module/presentation/post-Apply writer authority.");
    }
}
