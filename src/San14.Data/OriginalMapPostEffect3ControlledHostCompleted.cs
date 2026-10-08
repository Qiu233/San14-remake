namespace San14.Data;

/// <summary>Implemented by a closed Game adapter joining its actual module
/// PortReturn, current completed presentation target and required caller-tail
/// command. This source interface creates no device authority.</summary>
public interface IOriginalMapPostEffect3ControlledHostOutputAuthority
{
    OriginalMapPostEffect3OwnedControlledCameraFrame ControlledCamera{get;}
    OriginalMainMapContextCurrentPacket CurrentContext{get;}
    OriginalMapPostEffect3ApplyViewSlotSource ApplyView{get;}
    OriginalMapCopy97CurrentStockPacket CurrentStock{get;}
    OriginalMapPostEffect3TextureParameterState TextureParameters{get;}
    OriginalMapPostEffect3Functor1SourceReceipt CpuDirectoryReturned{get;}
    OriginalMapPostEffect3Functor1SourceReceipt SourceControllerReturned{get;}
    OriginalMapPostEffect3Functor1SelectedDeviceJoin ActualDeviceJoin{get;}
    int FinalOutputSourcePassIndex{get;}
    object ActualPresentationIdentity{get;}
    long ActualPresentationRevision{get;}
    OriginalMapPostEffect3ControlledHostTailRecipe PostApplyTail{get;}
    object? ActualPostApplyExposureCopyIdentity{get;}
    string SourceEvidence{get;}
    void RequireActualReturnedOutput();
}

/// <summary>Source branch and wrapper identities for EB84C0's post-Functor
/// exposure publication. A different global50 target requires a real stock
/// command even after every selected Functor1 shader returned.</summary>
public sealed class OriginalMapPostEffect3ControlledHostTailRecipe
{
    private readonly OriginalMapPostEffect3TextureParameterState table;
    private readonly OriginalMapPostEffect3OwnedControlledCameraFrame camera;
    private readonly OriginalMapPostEffect3ControlledAuxFields aux;
    private readonly long? nullRevision;
    internal OriginalMapPostEffect3ControlledHostTailRecipe(LoadedOriginalMapPostEffect3ControlledHostSource source,
        OriginalMapPostEffect3OwnedControlledCameraFrame camera,OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3ControlledAuxFields aux,object? inputWrapper,object? globalWrapper,string evidence)
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
        SourceEvidence=source.SourceEvidence+";"+evidence+";EB84CB-input-null-skip/EB84DF-global50-null-skip/EB84E8-same-wrapper-skip/otherwise-source-caller-copy";
        RequireCurrent();
    }
    public long FrameNumber=>camera.FrameNumber;
    public uint Camera1510Word{get;}
    public bool LookupRequested{get;}
    public bool RequiresExposureCopy{get;}
    public OriginalMapPostEffect3ResolvedTextureParameter? ActualModuleExposurePointer{get;}
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
    internal void RequireSame(OriginalMapPostEffect3OwnedControlledCameraFrame actualCamera,
        OriginalMapPostEffect3TextureParameterState actualTable)
    {RequireCurrent();if(!ReferenceEquals(actualCamera,camera)||!ReferenceEquals(actualTable,table))
        throw new InvalidDataException("Post-Apply tail recipe was replaced by another prepared module/table.");}
}

public sealed class LoadedOriginalMapPostEffect3ControlledHostSource
{
    internal LoadedOriginalMapPostEffect3ControlledHostSource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)
        =>(Native,SourceEvidence)=(native,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public bool RequestsExposureLookupCandidate(uint actualCamera1510Word,uint actualAuxFlags8)
        =>(actualAuxFlags8&0x2000000)!=0||(actualCamera1510Word&1)!=0;
    /// <summary>Pure source branch. Wrapper identity is required: equal
    /// dimensions, pixels or descriptors do not make the pointers equal.</summary>
    public bool RequiresExposureCopyCandidate(object? actualInputWrapper,object? actualGlobalWrapper)
        =>actualInputWrapper is not null&&actualGlobalWrapper is not null&&!ReferenceEquals(actualInputWrapper,actualGlobalWrapper);
    public static LoadedOriginalMapPostEffect3ControlledHostSource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadControlledHostSource(OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    public OriginalMapPostEffect3ControlledHostTailRecipe PrepareTail(
        OriginalMapPostEffect3OwnedControlledCameraFrame actualCamera,OriginalMapPostEffect3TextureParameterState actualTable,
        OriginalMapPostEffect3ControlledAuxFields actualAux,object? actualLookedUpModuleWrapper,
        object? actualGlobalExposureWrapper,string actualWrapperProducerEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWrapperProducerEvidence);
        if(actualCamera.CaptureAcceptedCamera().ExecutableLocale!=Native.Locale)
            throw new InvalidDataException("Controlled Host tail and current Camera require the same native locale.");
        return new(this,actualCamera,actualTable,actualAux,actualLookedUpModuleWrapper,actualGlobalExposureWrapper,actualWrapperProducerEvidence);
    }
    public OriginalMapPostEffect3ControlledHostWriterScope ConstructOwnedWriterScope(OriginalMainMapContextCurrentSource actualQSource)
    {
        if(actualQSource.Host.Locale!=Native.Locale||actualQSource.Host.ExecutableSha256!=Native.ExecutableSha256)
            throw new InvalidDataException("Controlled Host writer scope and Q require the same native source generation.");
        return new(this,new OriginalMapPostEffect3OwnedMainHostCallbackScope(actualQSource));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3ControlledHostSource ReadControlledHostSource(LoadedOriginalDeferredSamplerNativeSource native)
    {
        const string sourceScope="OriginalMapPostEffect3ControlledHostCompleted.cs";
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Controlled Host executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"Controlled Host decoded text");
        void Check(int at,int count,string sha)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,sourceScope,at,count,sha);
            VerifyHash(text.AsSpan(actual.Rva-start,count),actual.Sha,"Controlled Host caller tail");
        }
        Check(0xEB7F81,72,"49E42A9E51E125AF5E98A40B33B8CDE5242BC79DD75CA51CE7D91D706117D28B");
        Check(0xEB84C0,329,"C957D98A4CA91EE229C7EAFDE8BAB7C390AF12A96D7EB0B044E275EFD2F66ED0");
        Check(0xEB99DF,114,"9FFE6BD39EAE2398082ED5252904EF5933F2CA4696CE968935AA7CD353021D7E");
        Check(0x95A370,302,"87C8929F3B33304874C693AC9C197B74AFAA96DC1A71E8B96379CE8903F35E60");
        Check(0x956A60,80,"9954DE9AC927D4AF5BB41EDF546A731B296C04B18B193B8552830AC504B9AF29");
        return new(native,native.ResourceUri+"@"+native.ExecutableSha256+
            $";{native.Locale}:paired-Host-current-lookup/stock-tail-profile;TC-reference-role:EB7F99/FB4-EA2OutputExposureScale-current-lookup;EB84CB-null-input-skip;"+
            "EB84DF-null-global50-skip/EB84E8-identical-wrapper-skip;EB8515..8604/EB99DF..9A51-stock-exposure-copy;"+
            "95A370-source-clip-Begin-End/956A60-B0-bind-domain;finite-owned-Q-tail;no-legacy-final-or-device-completion");
    }
}

/// <summary>Same owned Q's finite post-Apply callback/overlay family. Reuses
/// the Q constructor/leaf proof, while keeping F1 and fallback outputs distinct.</summary>
public sealed class OriginalMapPostEffect3ControlledHostWriterScope
{
    private readonly LoadedOriginalMapPostEffect3ControlledHostSource source;
    private readonly OriginalMapPostEffect3OwnedMainHostCallbackScope q;
    internal OriginalMapPostEffect3ControlledHostWriterScope(LoadedOriginalMapPostEffect3ControlledHostSource source,
        OriginalMapPostEffect3OwnedMainHostCallbackScope q)=>(this.source,this.q)=(source,q);
    public void MarkImportedOrUnknownCallbackOrOverlayWriter(string actualWriter)=>q.MarkImportedOrUnknownCallbackOrOverlayWriter(actualWriter);
    internal void Require(OriginalMapPostEffect3OwnedControlledCameraFrame camera,OriginalMainMapContextCurrentPacket context,
        OriginalMapPostEffect3ApplyViewSlotSource view)
    {
        RequireCurrentContext(context);
        if(camera.FrameNumber!=context.FrameNumber||view.FrameNumber!=context.FrameNumber||view.Index!=0||
            view.ContextOwnerIdentity!=context.Source.OwnedHostIdentity||camera.Route.SelectedModuleFunctorIndex!=1||
            !camera.Route.UsesOwnedController||camera.Route.ExecutesEc3360Fallback||!camera.Route.BypassesLegacyA1Branch||
            camera.CaptureAcceptedCamera().ExecutableLocale!=context.Source.Host.Locale)
            throw new NotSupportedException("Controlled Host completion needs the exact current Main slot0/owned Q/Functor1 writer family.");
    }
    internal void RequireCurrentContext(OriginalMainMapContextCurrentPacket context)
    {
        q.RequireCurrentSource(context);
        bool reset=context.ContextFlags3F0==0&&context.Phase==OriginalMainMapContextCurrentPhase.DeferredPreparationReset;
        bool thin=context.ContextFlags3F0 is 0 or 0x400&&
            context.Phase==OriginalMainMapContextCurrentPhase.NativeThinSurfaceFlagWriter;
        bool specialized=context.ContextFlags3F0==0x40&&
            context.Phase==OriginalMainMapContextCurrentPhase.NativeSpecializedGeometryFlagWriter;
        if(context.ContextFlags3F4!=0||!reset&&!thin&&!specialized)
            throw new NotSupportedException("Functor1 Host Apply-tail scope requires its actual reset/Q0, thin-surface/Q0-or400 or specialized57-OR40/Q40 writer; other bits/imported Q are outside this source profile.");
    }
    public string SourceEvidence=>source.SourceEvidence+";"+q.Source.SourceProvenance+
        ";same-owned-Q-v170-ret-leaf/16-source-null-overlay-slots;Functor1-reset/source-thin-or-actual-specialized57-OR40;"+
        "controlled-module-return;shared-tail-viewport-restoration;Apply-tail-before-outer-E9E6D4-OR20;no-EC3360-output-claim";
}

/// <summary>Closed source Host acknowledgement with a live actual Game
/// authority. Does not create a GPU fence, raw native cache observation or
/// any next-frame pixel readiness.</summary>
public sealed class OriginalMapPostEffect3ControlledHostCompleted
{
    private readonly IOriginalMapPostEffect3ControlledHostOutputAuthority authority;
    private readonly OriginalMapPostEffect3ControlledHostWriterScope scope;
    private readonly object presentation;
    private readonly object? copy;
    private readonly OriginalMapPostEffect3ControlledHostTailRecipe tail;
    private readonly OriginalMapPostEffect3OwnedControlledCameraFrame controlledCamera;
    private readonly OriginalMapPostEffect3Functor1SourceReceipt controllerReturn,directoryReturn;
    private readonly OriginalMapPostEffect3Functor1SelectedDeviceJoin deviceJoin;
    private readonly OriginalMapPostEffect3ApplyViewSlotSource applyView;
    private readonly int finalSourcePass;
    private readonly long presentationRevision;
    private readonly byte[] prefix;
    private OriginalMapPostEffect3ControlledHostCompleted(IOriginalMapPostEffect3ControlledHostOutputAuthority authority,
        OriginalMapPostEffect3ControlledHostWriterScope scope)
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
    public OriginalMapCameraConstructionPacket Camera{get;}
    public OriginalMainMapContextCurrentPacket Context{get;}
    public OriginalMapCopy97CurrentStockPacket CurrentStock{get;}
    public uint ReturnedRt0ColorWriteMask=>CurrentStock.Prefix.ColorWriteMask;
    public bool ReturnedCustomVertexShaderIsAbsent=>CurrentStock.CurrentCustomVertexShaderIsAbsent;
    public bool ProvesNativeRawStateRestoration=>false;
    public bool ProvesNextFramePixelsReady=>false;
    public string SourceEvidence=>scope.SourceEvidence+";"+tail.SourceEvidence+";"+authority.SourceEvidence+
        ";F1-End-restores-entry-RT0-mask;stock-sixwords/B0-preserved;ScreenEnd-restores-clip-mask/neutral-plane-rebuilt;not-legacy-final";
    public static OriginalMapPostEffect3ControlledHostCompleted CaptureAfterActualReturnedOutput(
        IOriginalMapPostEffect3ControlledHostOutputAuthority actualAuthority,OriginalMapPostEffect3ControlledHostWriterScope actualWriterScope)
    {
        ArgumentNullException.ThrowIfNull(actualAuthority);ArgumentNullException.ThrowIfNull(actualWriterScope);
        actualAuthority.RequireActualReturnedOutput();
        var completed=new OriginalMapPostEffect3ControlledHostCompleted(actualAuthority,actualWriterScope);
        completed.RequireCompletedSourceHostWriterScope();return completed;
    }
    public void RequireCompletedSourceHostWriterScope()
    {
        authority.RequireActualReturnedOutput();scope.Require(authority.ControlledCamera,Context,authority.ApplyView);
        var controller=authority.SourceControllerReturned;var directory=authority.CpuDirectoryReturned;
        controller.RequireCurrent();directory.RequireCurrent();
        controller.Invocation.RequireCurrentExitedDirectory(directory,authority.TextureParameters);
        authority.ActualDeviceJoin.RequireActualReceipts();tail.RequireSame(authority.ControlledCamera,authority.TextureParameters);
        if(controller.Operation!=OriginalMapPostEffect3Functor1SourceOperation.ControllerReturned||!controller.Invocation.IsControllerReturned||
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
