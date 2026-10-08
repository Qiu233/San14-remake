using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The six retained stock words and custom-VS state at the current
/// node97 of one owned source Host. This is a source-semantic packet, not a
/// GPU submission receipt. The Game owner must validate actual stage receipts
/// before requesting it.</summary>
public sealed class OriginalMapCopy97CurrentStockPacket
{
    internal OriginalMapCopy97CurrentStockPacket(OriginalMapCopy97CurrentStockSource source,
        OriginalMapOwnedGraphicsMaskPrefix prefix, OriginalMapSourceCreatedF103CurrentFrame frame,
        OriginalTerrainGbufferColorMaskRecipe terrain,
        OriginalModelOrdinaryNativeStencilReceipt[] models,
        OriginalDeferredEarlyF103SelectedSource early, OriginalDeferredEarlyCausticsSourceNull caustics,
        OriginalMapSourceCreatedF103CurrentFrame? geometryFrame,
        long previousCompletedFrame,Vector2 screenScale,Vector4 colorScale,bool customVsAbsent)
    { Source=source; Prefix=prefix; Frame=frame; Terrain=terrain; Models=models; Early=early; Caustics=caustics; GeometryFrame=geometryFrame;
      PreviousCompletedSourceFrameNumber=previousCompletedFrame;CurrentScreenScale29E0=screenScale;
      CurrentColorScale29F0=colorScale;CurrentCustomVertexShaderIsAbsent=customVsAbsent; }
    internal OriginalMapOwnedGraphicsMaskPrefix Prefix { get; }
    internal OriginalMapSourceCreatedF103CurrentFrame Frame { get; }
    internal OriginalTerrainGbufferColorMaskRecipe Terrain { get; }
    internal OriginalModelOrdinaryNativeStencilReceipt[] Models { get; }
    internal OriginalDeferredEarlyF103SelectedSource Early { get; }
    internal OriginalDeferredEarlyCausticsSourceNull Caustics { get; }
    internal OriginalMapSourceCreatedF103CurrentFrame? GeometryFrame { get; }
    public OriginalMapCopy97CurrentStockSource Source { get; }
    public long FrameNumber => Frame.FrameNumber;
    public string ViewIdentity => Frame.Lighting.Binding.ViewIdentity;
    public string OwnedFrontendIdentity => Source.OwnedFrontendIdentity;
    public long PreviousCompletedSourceFrameNumber { get; }
    public bool UsesConstructedFrontendState => PreviousCompletedSourceFrameNumber==0;
    public Vector2 CurrentScreenScale29E0 { get; }
    public Vector4 CurrentColorScale29F0 { get; }
    public bool CurrentCustomVertexShaderIsAbsent { get; }
    // Global stencil remains outside this stock-field proof. In particular,
    // the preceding models/terrain are not a source of stencil-disabled.
    public string SourceProvenance => Source.SourceProvenance+";frame="+FrameNumber+
        ";"+Prefix.Current.CurrentProducerProvenance+";"+Frame.View.CurrentProducerProvenance+
        ";entry-from="+(UsesConstructedFrontendState?"owned-constructor":$"completed-source-Host-frame{PreviousCompletedSourceFrameNumber}")+
        ";retained-through-actual-owned-SkyWORK/class18/Terrain/ordinary78;not-GPU-receipt";
    public void ValidateCurrent() => Source.ValidateCurrent(this);
    /// <summary>Accept only this actual current stock prefix's private mask
    /// configuration snapshot; frame/native stock checks continue per draw.</summary>
    public OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot CaptureAcceptedImmutableMaskSources()
    {ValidateCurrent();return Source.CaptureAcceptedImmutableStockSources(this);}
    public void RevalidateAcceptedStaticSourceBoundary()=>Source.RevalidateAcceptedStockSourceBoundary(this);
}

/// <summary>Finite owned node97 producer. The constructor initializes B0=0,
/// 29E0/29E4=1 and 29F0..29FC=1. The admitted source Sky/WORK, unchanged class18
/// hooks, ordinary Landscape and source78/126/95 hooks preserve these fields.
/// Ordinary VS caches A8/B8/C90 are separate from stock custom-VS B0. An
/// admitted post97/Exposure/Tone2/DOF0 source SPK families preserve these fields.
/// Later entries retain the last actual complete source Host output. An
/// imported/extended writer or failed attempt invalidates the scope.</summary>
public sealed partial class OriginalMapCopy97CurrentStockSource
{
    private readonly OriginalMapOwnedGraphicsColorMaskHistorySource frontend;
    private OriginalMapCopy97CurrentStockPacket? prepared;
    private Vector2 completedScreenScale=Vector2.One;
    private Vector4 completedColorScale=Vector4.One;
    private bool completedCustomVsAbsent=true;
    private bool valid=true;
    private OriginalMapCopy97CurrentStockSource(OriginalMapOwnedGraphicsColorMaskHistorySource source)
    { frontend=source; }
    public string OwnedFrontendIdentity => frontend.OwnedFrontendIdentity;
    public string ViewIdentity => frontend.OwnedViewIdentity;
    public OriginalMapOwnedGraphicsColorMaskHistorySource OwnedFrontendSource => frontend;
    public long CompletedSourceFrameNumber { get; private set; }
    public string SourceProvenance => $"owned-Copy97-current-stock-history/{frontend.Locale}/{frontend.ExecutableSha256};"+
        "95DC10/966EA0-B0-null/29E0-29F0-six-3F800000;"+
        "opcode47/62/64-separate-from-ordinary-shader-binding;"+
        "canonical-SkyWORK/HeightFog/class18-GBuffer/TerrainF8/early78-hooks-retain;"+
        "Copy97/Filter137/source-null139-140/late166/Exposure/Tone2-DOF0/sourceSPK-retain;"+
        OwnedFrontendIdentity;

    /// <summary>Borrow the exact existing owned frontend; this creates no
    /// replacement frontend or constructor reset. Its canonical Sky/final
    /// generation and executable identity were verified by ReadFresh.</summary>
    public static OriginalMapCopy97CurrentStockSource ReadFresh(
        OriginalMapOwnedGraphicsColorMaskHistorySource actualOwnedFrontend)
    {
        ArgumentNullException.ThrowIfNull(actualOwnedFrontend);
        if(actualOwnedFrontend.CompletedSourceFrameNumber!=0 || actualOwnedFrontend.WorkSubmissionStarted)
            throw new InvalidDataException("First stock owner must be attached before the actual first WORK attempt.");
        return new(actualOwnedFrontend);
    }

    public OriginalMapCopy97CurrentStockPacket PrepareFirstCurrent(
        OriginalMapOwnedGraphicsMaskPrefix actualSkyPrefix, OriginalMapSourceCreatedF103CurrentFrame actualFrame,
        IReadOnlyList<OriginalModelOrdinaryNativeStencilReceipt> actualSubmittedModelSources,
        OriginalTerrainGbufferColorMaskRecipe actualTerrain,
        OriginalDeferredEarlyF103SelectedSource actualExecutedEarly78,
        OriginalDeferredEarlyCausticsSourceNull actualEarlyCaustics,
        OriginalMapSourceCreatedF103CurrentFrame? actualGeometryFrame=null)
    {
        ArgumentNullException.ThrowIfNull(actualSkyPrefix);
        if(!actualSkyPrefix.UsesConstructedFrontendState || CompletedSourceFrameNumber!=0)
            throw new InvalidDataException("The first-only stock API cannot acquire a later frontend entry.");
        return PrepareCurrent(actualSkyPrefix,actualFrame,actualSubmittedModelSources,actualTerrain,
            actualExecutedEarly78,actualEarlyCaustics,actualGeometryFrame);
    }

    public OriginalMapCopy97CurrentStockPacket PrepareCurrent(
        OriginalMapOwnedGraphicsMaskPrefix actualSkyPrefix, OriginalMapSourceCreatedF103CurrentFrame actualFrame,
        IReadOnlyList<OriginalModelOrdinaryNativeStencilReceipt> actualSubmittedModelSources,
        OriginalTerrainGbufferColorMaskRecipe actualTerrain,
        OriginalDeferredEarlyF103SelectedSource actualExecutedEarly78,
        OriginalDeferredEarlyCausticsSourceNull actualEarlyCaustics,
        OriginalMapSourceCreatedF103CurrentFrame? actualGeometryFrame=null)
    {
        ArgumentNullException.ThrowIfNull(actualSkyPrefix);ArgumentNullException.ThrowIfNull(actualFrame);
        ArgumentNullException.ThrowIfNull(actualSubmittedModelSources);ArgumentNullException.ThrowIfNull(actualTerrain);
        ArgumentNullException.ThrowIfNull(actualExecutedEarly78);ArgumentNullException.ThrowIfNull(actualEarlyCaustics);
        RequireScope(actualSkyPrefix,actualFrame,actualSubmittedModelSources,actualTerrain,actualExecutedEarly78,actualEarlyCaustics,actualGeometryFrame);
        if(prepared is not null)
        {
            if(!ReferenceEquals(prepared.Prefix,actualSkyPrefix) || !ReferenceEquals(prepared.Frame,actualFrame) ||
                !ReferenceEquals(prepared.Terrain,actualTerrain) || !ReferenceEquals(prepared.Early,actualExecutedEarly78) ||
                !ReferenceEquals(prepared.Caustics,actualEarlyCaustics) ||
                !ReferenceEquals(prepared.GeometryFrame,actualGeometryFrame) ||
                !prepared.Models.SequenceEqual(actualSubmittedModelSources))
                throw new InvalidDataException("Stock fields are already prepared for another source generation/attempt.");
            return prepared;
        }
        return prepared=new(this,actualSkyPrefix,actualFrame,actualTerrain,actualSubmittedModelSources.ToArray(),
            actualExecutedEarly78,actualEarlyCaustics,actualGeometryFrame,CompletedSourceFrameNumber,
            completedScreenScale,completedColorScale,completedCustomVsAbsent);
    }
    /// <summary>Source semantics only; the Game wrapper must supply actual
    /// complete final submission acknowledgement. Validation never commits.</summary>
    public void ValidateCompletedSourceHostOutput(OriginalMapCopy97CurrentStockPacket packet,
        OriginalMapFinalOutputProductionPrepared actualCompletedFinal)
    {
        ArgumentNullException.ThrowIfNull(packet);ArgumentNullException.ThrowIfNull(actualCompletedFinal);
        ValidateCurrent(packet);frontend.ValidateCompletedSourceHostOutput(packet.Prefix,actualCompletedFinal);
        // The actual final producer takes its Camera bytes from this accepted
        // Lighting preparation, retaining the Main Camera's source and pose.
        // Its light-written tail is not the unmodified Main constructor tail.
        if(actualCompletedFinal.FrameNumber!=packet.FrameNumber ||
            actualCompletedFinal.Camera.Source.Camera.ObjectId!=packet.Frame.Camera.Source.Camera.ObjectId ||
            actualCompletedFinal.Camera.Source.Resource.Source.DecodedSha256!=packet.Frame.Camera.Source.Resource.Source.DecodedSha256 ||
            actualCompletedFinal.Camera.CurrentPose!=packet.Frame.Camera.CurrentPose ||
            !actualCompletedFinal.Camera.Prefix.Span.SequenceEqual(packet.Frame.Lighting.PreparedCameraPrefix.Span))
            throw new InvalidDataException("Stock history cannot commit another actual Camera/final frame.");
    }
    /// <summary>The admitted post97 and complete Exposure/Tone2/DOF0 source
    /// SPK chain retains B0 and the six words. A partial output cannot commit.
    /// Call before the paired mask history clears its accepted prefix.</summary>
    public void ReplayCompletedSourceHostOutput(OriginalMapCopy97CurrentStockPacket packet,
        OriginalMapFinalOutputProductionPrepared actualCompletedFinal)
    {
        ValidateCompletedSourceHostOutput(packet,actualCompletedFinal);
        completedScreenScale=packet.CurrentScreenScale29E0;completedColorScale=packet.CurrentColorScale29F0;
        completedCustomVsAbsent=packet.CurrentCustomVertexShaderIsAbsent;
        CompletedSourceFrameNumber=packet.FrameNumber;prepared=null;acceptedStockSources=null;
    }
    /// <summary>Actual mode1 Camera join, independent of the old A1 bank.
    /// The complete caller writer scope must be closed before acknowledging it.</summary>
    public void ValidateCompletedSourceHostOutput(OriginalMapCopy97CurrentStockPacket packet,
        OriginalMapPostEffect3ApplyMode1Completed actualCompletedFinal)
    {
        ArgumentNullException.ThrowIfNull(packet);ArgumentNullException.ThrowIfNull(actualCompletedFinal);
        ValidateCurrent(packet);actualCompletedFinal.Validate();
        actualCompletedFinal.WriterScope?.RequireSameContext(packet.Frame.Context);
        if(actualCompletedFinal.FrameNumber!=packet.FrameNumber||
            actualCompletedFinal.Camera.Source.Camera.ObjectId!=packet.Frame.Camera.Source.Camera.ObjectId||
            actualCompletedFinal.Camera.Source.Resource.Source.ResourceUri!=packet.Frame.Camera.Source.Resource.Source.ResourceUri||
            actualCompletedFinal.Camera.Source.Resource.Source.DecodedSha256!=packet.Frame.Camera.Source.Resource.Source.DecodedSha256||
            actualCompletedFinal.Camera.CurrentPose!=packet.Frame.Camera.CurrentPose||
            !actualCompletedFinal.Camera.Prefix.Span.SequenceEqual(packet.Frame.Lighting.PreparedCameraPrefix.Span))
            throw new InvalidDataException("Mode1 stock acknowledgement belongs to another actual Camera/lighting/frame.");
        frontend.ValidateCompletedSourceHostOutput(packet.Prefix,actualCompletedFinal);
    }
    public void ReplayCompletedSourceHostOutput(OriginalMapCopy97CurrentStockPacket packet,
        OriginalMapPostEffect3ApplyMode1Completed actualCompletedFinal)
    {
        ValidateCompletedSourceHostOutput(packet,actualCompletedFinal);
        completedScreenScale=packet.CurrentScreenScale29E0;completedColorScale=packet.CurrentColorScale29F0;
        completedCustomVsAbsent=actualCompletedFinal.FallbackCustomVertexShaderIsAbsent;
        CompletedSourceFrameNumber=packet.FrameNumber;prepared=null;acceptedStockSources=null;
    }
    public void MarkFailedImportedOrUnknownWriter(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter);valid=false; }
    internal void ValidateCurrent(OriginalMapCopy97CurrentStockPacket packet)
    {
        if(!ReferenceEquals(prepared,packet) || !ReferenceEquals(packet.Source,this))
            throw new InvalidDataException("Current stock packet is foreign or no longer owned.");
        if(acceptedStockSources is{} accepted){accepted.RequireCurrent(packet);return;}
        RequireScope(packet.Prefix,packet.Frame,packet.Models,packet.Terrain,packet.Early,packet.Caustics,packet.GeometryFrame);
    }
    private void RequireScope(OriginalMapOwnedGraphicsMaskPrefix prefix,OriginalMapSourceCreatedF103CurrentFrame frame,
        IReadOnlyList<OriginalModelOrdinaryNativeStencilReceipt> models,OriginalTerrainGbufferColorMaskRecipe terrain,
        OriginalDeferredEarlyF103SelectedSource early,OriginalDeferredEarlyCausticsSourceNull caustics,
        OriginalMapSourceCreatedF103CurrentFrame? geometryFrame)
    {
        Interlocked.Increment(ref fullNativeSemanticSourceValidationCount);
        if(!valid || !ReferenceEquals(prefix.Source,frontend) ||
            prefix.PreviousCompletedFrameNumber!=CompletedSourceFrameNumber ||
            frontend.CompletedSourceFrameNumber!=CompletedSourceFrameNumber || !frontend.WorkSubmissionStarted ||
            prefix.UsesConstructedFrontendState!=(CompletedSourceFrameNumber==0) || frame.FrameNumber<=CompletedSourceFrameNumber ||
            prefix.FrameNumber!=frame.FrameNumber || prefix.ViewIdentity!=ViewIdentity ||
            frame.Lighting.Binding.ViewIdentity!=ViewIdentity || terrain.FrameNumber!=frame.FrameNumber ||
            terrain.ViewIdentity!=ViewIdentity || terrain.Current.ContextOwnerIdentity!=frame.Context.Source.OwnedHostIdentity ||
            terrain.Current.SelectedLightOwnerIdentity!=frame.Lighting.Binding.LightOwnerIdentity ||
            terrain.Current.CurrentContextFlags3E4!=frame.Lighting.ContextFlags3E4 ||
            terrain.Current.SelectedLightF24Bits!=BinaryPrimitives.ReadUInt32LittleEndian(frame.Feature.EffectiveLocalLightData.Span.Slice(0xF24,4)) ||
            models.Count==0)
            throw new InvalidDataException("Current stock fields require this exact owned attempt and completed prior output, View/Q/light and nonempty model source prefix.");
        RequireGeometryContextJoin(frame,geometryFrame,terrain);
        prefix.RequireCanonicalSkyDeclaredHookScope();frame.Selection.Validate();
        bool legacy = frame.Selection.Combination.Index==1022 && frame.Selection.PixelProgramIndex==767;
        bool world = frame.Selection.Combination.Index==510 && frame.Selection.PixelProgramIndex==383 &&
            frame.Selection.PixelProgramSha256==OriginalDeferredSelected383Constants.PixelProgramSha256 &&
            frame.Lighting.SourceLightClassId==OriginalDirectionalLightSource.SourceClass &&
            frame.Lighting.SourceIdentity.DeclaredGroupMask==0x80 &&
            ReferenceEquals(frame.Selection.OwnedFeatureSource,frame.Feature);
        if(frame.Selection.SourcePlan.Node.Index!=166 || !legacy && !world)
            throw new NotSupportedException("Stock current prefix has no proof for another selected Host shader route.");
        foreach(var model in models)
        {
            var p=model.CurrentPublication;
            if(p.FrameNumber!=frame.FrameNumber || p.ViewIdentity!=ViewIdentity || !model.ShouldSubmitPrimitive ||
                model.Source.QueueNodeIndex is not(54 or 55) || model.Source.StateIndex!=18 ||
                p.SourceConstructorAndStartupRegistryRetained!=true || p.ModifierCacheUnchanged!=true ||
                p.CompletePluginChainAndDefaultCallbacksUnchanged!=true || p.CurrentSelectedProviderAndBindingsUnchanged!=true ||
                p.NoPrimitiveOrContextStencilOverrides!=true)
                throw new InvalidDataException("Current model prefix contains a foreign, retained/edited or unsupported source hook generation.");
        }
        // Revalidate the actual canonical DC52 ordinary renderer generation.
        _=terrain.Source.PrepareCurrent(terrain.Current);
        early.ValidateParameterFrame(frame.Camera,frame.Lighting);caustics.Validate(early);
        if(early.SourcePlan.Node.Index!=78 || early.Combination.Index!=126 || early.PixelProgramIndex!=95 ||
            !ReferenceEquals(early.OwnedFeatureSource,frame.Feature) || early.Current.ContextFlags3F0!=frame.Context.ContextFlags3F0 ||
            early.Current.NativeColorMode!=0x60 || early.EarlyCurrent.RawQueryD5BA00!=0 || early.EarlyCurrent.RawQueryD5B9D0!=0)
            throw new NotSupportedException("Current stock source proof requires actual ordinary78/126/95 and its source-null Caustics owner.");
    }
}
