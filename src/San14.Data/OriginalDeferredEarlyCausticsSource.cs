using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum OriginalDeferredEarlyCausticsViewScope
{
    FreshPreparedView1B90AbsentNoExternalAssignments,
    RetainedOrEditedPreparedView1B90
}

/// <summary>Actual fresh Caustics texture source after the same Main38D/View0
/// source preparation. The source size gate skips the sole encoded3A image
/// setter. There is no RID, generated zero image or inferred image lookup.</summary>
public sealed class OriginalDeferredEarlyCausticsSourceNull
{
    private readonly OriginalMainMapContextCurrentPacket context;
    internal OriginalDeferredEarlyCausticsSourceNull(OriginalDeferredEarlyF103SelectedSource selected,
        OriginalMainMapContextCurrentPacket currentContext, string sourceOwnerIdentity,string provenance)
    {
        context=currentContext;
        FrameNumber=selected.Current.FrameNumber;SourceOwnerIdentity=sourceOwnerIdentity;SourceProvenance=provenance;
        ShaderSourceSha256=selected.SourcePlan.Shader.Source.DecodedSha256;PixelProgramSha256=selected.PixelProgramSha256;
        AcceptedCameraPrefixSha256=Hash(selected.Camera.Prefix.Span);
        EffectiveLightDataSha256=selected.OwnedFeatureSource!.EffectiveLightDataSha256;
        Binding=selected.OwnedFeatureSource.Binding;
        CurrentViewProducerProvenance=selected.OwnedFeatureSource.CurrentView.CurrentProducerProvenance;
        QueryD5BeA0=selected.Current.QueryD5BeA0;
    }
    public long FrameNumber { get; }
    public string SourceOwnerIdentity { get; }
    public string SourceProvenance { get; }
    public string SourceEvidence=>SourceProvenance;
    public string ShaderSourceSha256 { get; }
    public string PixelProgramSha256 { get; }
    public string AcceptedCameraPrefixSha256 { get; }
    public string EffectiveLightDataSha256 { get; }
    public OriginalMapLightingBinding Binding { get; }
    public string CurrentViewProducerProvenance { get; }
    /// <summary>The actual A40 reader result. False skips the active Caustics
    /// branch before size tests; true uses the owned source zero-size gate.</summary>
    public bool QueryD5BeA0 { get; }
    public uint PixelRegister=>1;
    public uint SamplerRegister=>1;
    public void Validate(OriginalDeferredEarlyF103SelectedSource selected)
    {
        ArgumentNullException.ThrowIfNull(selected);OriginalDeferredEarlyCausticsOwner.ValidateSelection(selected);
        if(selected.Current.FrameNumber!=FrameNumber||selected.SourcePlan.Shader.Source.DecodedSha256!=ShaderSourceSha256||
            selected.Current.ContextFlags3F0!=context.ContextFlags3F0||
            selected.PixelProgramSha256!=PixelProgramSha256||Hash(selected.Camera.Prefix.Span)!=AcceptedCameraPrefixSha256||
            selected.OwnedFeatureSource?.EffectiveLightDataSha256!=EffectiveLightDataSha256||
            selected.OwnedFeatureSource?.Binding!=Binding||
            selected.Current.QueryD5BeA0!=QueryD5BeA0||
            selected.OwnedFeatureSource?.CurrentView.CurrentProducerProvenance!=CurrentViewProducerProvenance)
            throw new InvalidDataException("Caustics source-null authority belongs to another accepted Camera/light frame.");
    }
    internal bool MatchesCurrentContext(OriginalMainMapContextCurrentPacket actual)
        => ReferenceEquals(actual,context);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
}

public sealed class OriginalDeferredEarlyCausticsPacket
{
    private readonly OriginalDeferredEarlyNativeConstantWrite[] writes;
    internal OriginalDeferredEarlyCausticsPacket(OriginalDeferredEarlyCausticsSourceNull sourceNull,
        IEnumerable<OriginalDeferredEarlyNativeConstantWrite> nativeWrites)
    { SourceNull=sourceNull;writes=nativeWrites.Select(w=>w with{Bytes=w.Bytes.ToArray()}).ToArray(); }
    public long FrameNumber=>SourceNull.FrameNumber;
    public string SourceOwnerIdentity=>SourceNull.SourceOwnerIdentity;
    public string CurrentProducerProvenance=>SourceNull.SourceProvenance;
    public OriginalDeferredEarlyCausticsSourceNull SourceNull { get; }
    public IReadOnlyList<OriginalDeferredEarlyNativeConstantWrite> NativeWrites=>Array.AsReadOnly(
        writes.Select(w=>w with{Bytes=w.Bytes.ToArray()}).ToArray());
    public IReadOnlyDictionary<string,int> NativeParameterOrdinals { get; }=new ReadOnlyDictionary<string,int>(
        new Dictionary<string,int>{{"vCausticsRange",0},{"fCausticsScaler",1},{"mW2Ca",2}});
}

/// <summary>Owns a fresh source Caustics plugin and the fresh prepared-View
/// Caustics projection. An existing View1B90 packet is retained by D6ABE0 and
/// cannot acquire this source-zero proof. This owner exposes no image, parameter
/// or packet setters, so no later edits are silently replaced with defaults.</summary>
public sealed class OriginalDeferredEarlyCausticsOwner
{
    public const string ShaderSourceSha256="AF9DEDA3A8D19495640113ABD6186380D90B47421A6016520D834A5FB4D26A99";
    public const string PixelProgramSha256="19103754FF90ADC50E3CC7269C1AA93013D0FC3EA8341D3EE7BB2396D9F3030C";
    private readonly LoadedOriginalDeferredEarlyCausticsSource native;
    private readonly string shaderUri;
    private OriginalMapLightingBinding? retainedBinding;
    private OriginalMainMapContextCurrentSource? retainedQSource;
    private OriginalDeferredEarlyCausticsPacket? retainedPacket;
    public string SourceOwnerIdentity { get; }="owned-fresh-source-Caustics:"+Guid.NewGuid().ToString("N");
    private OriginalDeferredEarlyCausticsOwner(LoadedOriginalDeferredEarlyCausticsSource native,LoadedOriginalDeferredTexturePlan source)
    { this.native=native;shaderUri=source.Shader.Source.ResourceUri; }

    public static OriginalDeferredEarlyCausticsOwner CreateFresh(LoadedOriginalDeferredEarlyCausticsSource native,
        LoadedOriginalDeferredTexturePlan source,OriginalDeferredEarlyCausticsViewScope scope)
    {
        ArgumentNullException.ThrowIfNull(native);ArgumentNullException.ThrowIfNull(source);
        if(scope!=OriginalDeferredEarlyCausticsViewScope.FreshPreparedView1B90AbsentNoExternalAssignments)
            throw new NotSupportedException("A retained or edited Caustics prepared-View packet requires its actual current producer.");
        OriginalDeferredEarlySceneConstants.ValidateSource(source);
        if(source.Node.Index!=78||source.Shader.Source.DecodedSha256!=ShaderSourceSha256)
            throw new InvalidDataException("Fresh Main Caustics owner requires the exact original node78 shader.");
        return new(native,source);
    }

    /// <summary>Prepare the closed source Main38D/View0/Color60 Q0 phase or
    /// the actual specialized57 OR40 writer on this same current Host.
    /// Width and height are the exact zero source properties copied through the
    /// fresh embedded View and A40 publisher, so the active setter branch is
    /// skipped. Initial range/scaler/matrix and null image remain source writes.</summary>
    public OriginalDeferredEarlyCausticsPacket Prepare(OriginalDeferredEarlyF103SelectedSource selected,
        OriginalMapSourceCreatedF103CurrentFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ValidateSelection(selected);
        var raw = frame.ViewFields.CreateF103Inputs(frame.Common.CommonViewResource240Present,
            frame.Common.Resource240Flags0C) with { CurrentProducerProvenance = frame.View.CurrentProducerProvenance };
        // F103 adds its preparation/Common/color provenance to the raw View
        // provenance. Join the actual immutable frame, not equal label strings.
        if (!ReferenceEquals(selected.OwnedFeatureSource, frame.Feature) ||
            !ReferenceEquals(frame.Feature.CurrentView, frame.View) || raw != frame.View ||
            frame.FrameNumber != selected.Current.FrameNumber || frame.Context.FrameNumber != frame.FrameNumber ||
            frame.ViewFields.Binding != frame.Lighting.Binding)
            throw new InvalidDataException("Caustics does not belong to this actual current F103 View/feature frame.");
        return PrepareCurrent(selected, frame.Camera, frame.Lighting, frame.ViewFields, frame.Context, frame);
    }

    /// <summary>Renew frame-bound authority for another real World modifier,
    /// retaining the same physical View/plugin cache and its source values.</summary>
    public OriginalDeferredEarlyCausticsPacket PrepareWorld(OriginalDeferredEarlyF103SelectedSource selected,
        OriginalMapSourceCreatedWorldF103Pass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        var frame=pass.Frame;
        if(!ReferenceEquals(selected.OwnedFeatureSource,frame.Feature)||
            !frame.Feature.SelectedModifierLightData.Span.SequenceEqual(pass.InnerLighting.LocalLightData.Span)||
            pass.InnerLighting.SourceIdentity.MainLightObjectId!=pass.Association.InnerRecord.Source.SourceObjectId)
            throw new InvalidDataException("World Caustics requires its actual independent inner feature owner.");
        return PrepareCurrent(selected,frame.Camera,frame.Lighting,frame.ViewFields,frame.Context,frame,true);
    }

    public OriginalDeferredEarlyCausticsPacket Prepare(OriginalDeferredEarlyF103SelectedSource selected,
        OriginalMapCameraConstructionPacket acceptedCamera,OriginalMapSourceCreatedLightingPacket lighting,
        OriginalMapSourceCreatedViewFieldsPacket currentView,OriginalMainMapContextCurrentPacket currentQ)
        => PrepareCurrent(selected, acceptedCamera, lighting, currentView, currentQ, null);

    private OriginalDeferredEarlyCausticsPacket PrepareCurrent(OriginalDeferredEarlyF103SelectedSource selected,
        OriginalMapCameraConstructionPacket acceptedCamera,OriginalMapSourceCreatedLightingPacket lighting,
        OriginalMapSourceCreatedViewFieldsPacket currentView,OriginalMainMapContextCurrentPacket currentQ,
        OriginalMapSourceCreatedF103CurrentFrame? joinedFrame,bool worldModifier=false)
    {
        ArgumentNullException.ThrowIfNull(acceptedCamera);ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(currentView);ArgumentNullException.ThrowIfNull(currentQ);
        ValidateSelection(selected);selected.ValidateParameterFrame(acceptedCamera,lighting);currentQ.Source.Validate(currentQ);
        currentView.Camera.Validate(acceptedCamera);
        if(acceptedCamera.ExecutableLocale!=native.Locale||currentQ.FrameNumber!=selected.Current.FrameNumber||
            !IsSupportedCurrentContext(currentQ)||selected.Current.ContextFlags3F0!=currentQ.ContextFlags3F0||
            currentView.Binding!=lighting.Binding||
            (currentQ.ContextFlags3F0==0x40 && (joinedFrame is null || !ReferenceEquals(joinedFrame.Context,currentQ)))||
            selected.SourcePlan.Shader.Source.ResourceUri!=shaderUri||
            joinedFrame is null && currentView.SourceProvenance!=selected.OwnedFeatureSource!.CurrentView.CurrentProducerProvenance)
            throw new InvalidDataException("Caustics requires its same accepted source Camera/View/light/Q frame.");
        var canonical=OriginalMapCameraConstructionSource.ReadPerspective(acceptedCamera.Source.Resource,0x38D04576);
        foreach(var field in new[]{(Ordinal:658,Hash:0x98DE41BDu),(Ordinal:659,Hash:0xB2102CBFu)})
        {
            var p=canonical.Camera.Properties.Single(p=>p.NameHash==field.Hash&&p.Index==field.Ordinal);
            if(p.TypeCode!=8||p.ElementCount!=1||p.RawValue.Length!=4||BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue)!=0)
                throw new InvalidDataException("Fresh Main Caustics size differs from its exact copied zero source property.");
        }
        if(retainedBinding is not null&&(retainedBinding!=lighting.Binding||!ReferenceEquals(retainedQSource,currentQ.Source)))
            throw new InvalidDataException("The retained Caustics View1B90 cache belongs to another owned View/Camera/light binding.");
        if(retainedPacket is not null)
        {
            if(selected.Current.FrameNumber<retainedPacket.FrameNumber)
                throw new InvalidDataException("Caustics cannot reuse its retained View1B90 cache in an older frame.");
            if(selected.Current.FrameNumber==retainedPacket.FrameNumber)
            {
                if(!worldModifier && retainedPacket.SourceNull.MatchesCurrentContext(currentQ))
                {retainedPacket.SourceNull.Validate(selected);return retainedPacket;}
                string renewal=retainedPacket.CurrentProducerProvenance+
                    ";actual-same-View-cache/current-admitted-context-and-modifier;"+currentQ.SourceProvenance;
                var renewed=new OriginalDeferredEarlyCausticsSourceNull(selected,currentQ,SourceOwnerIdentity,renewal);
                var renewedPacket=new OriginalDeferredEarlyCausticsPacket(renewed,retainedPacket.NativeWrites.Select(write=>
                    write with {CurrentProducerProvenance=renewal}));
                if(!worldModifier)retainedPacket=renewedPacket;
                return renewedPacket;
            }
        }
        string cachePhase=selected.Current.QueryD5BeA0
            ? retainedPacket is null?"fresh-View1B90-absent/published-source-cache":
                "retained-owned-View1B90-source-cache/from-frame="+retainedPacket.FrameNumber
            : "owned-Caustics-plugin-default-cache;D5BEA0-false/no-A40-copy";
        string provenance=native.SourceProvenance+";"+canonical.ConstructorOrigin+";"+currentView.SourceProvenance+
            ";frame="+selected.Current.FrameNumber+";owner="+SourceOwnerIdentity+
            ";"+cachePhase+"/no-external-packet-or-plugin-assignments;"+
            (selected.Current.QueryD5BeA0
                ? "AA4-AA8/2064-2068/ViewD34-D38/A40-A00-A04:exact-source-zero;strict-size-gate-skipped;"
                : "EAD5F3-read-result0/EAE3B0-skip-before-size-tests;no-native-A40-publication-claim;")+
            "Caustics9BF150:range+50=zero/scaler+60=one/matrix+70=source-literal;"+
            "Caustics9BF070:image+30/+38=null;encoded3A-setter-not-reached";
        byte[] one=new byte[4];BinaryPrimitives.WriteSingleLittleEndian(one,1);
        var proof=new OriginalDeferredEarlyCausticsSourceNull(selected,currentQ,SourceOwnerIdentity,provenance);
        var packet=new OriginalDeferredEarlyCausticsPacket(proof,[new("vCausticsRange",new byte[16],provenance),new("fCausticsScaler",one,provenance),
            new("mW2Ca",native.IdentityBytes,provenance)]);
        retainedBinding=lighting.Binding;retainedQSource=currentQ.Source;retainedPacket=packet;return packet;
    }

    internal static void ValidateSelection(OriginalDeferredEarlyF103SelectedSource selected)
    {
        ArgumentNullException.ThrowIfNull(selected);selected.Validate();
        if(selected.SourcePlan.Node.Index!=78||selected.SourcePlan.ViewId!=0||selected.Combination.Index!=126||
            selected.PixelProgramIndex!=95||selected.PixelProgramSha256!=PixelProgramSha256||
            selected.SourcePlan.Shader.Source.DecodedSha256!=ShaderSourceSha256||
            selected.Camera.Source.Camera.ObjectId!=0x38D04576||selected.Current.NativeColorMode!=0x60||
            selected.Current.ContextFlags3F0 is not(0 or 0x40)||selected.OwnedFeatureSource is null)
            throw new InvalidDataException("Caustics source-null scope requires actual owned node78/126/95 Main38D/View0/Color60/Q0 or source57-OR40. " +
                $"Actual node={selected.SourcePlan.Node.Index}, view={selected.SourcePlan.ViewId}, combo={selected.Combination.Index}, " +
                $"PS={selected.PixelProgramIndex}, camera={selected.Camera.Source.Camera.ObjectId:X8}, color={selected.Current.NativeColorMode:X}, " +
                $"Q={selected.Current.ContextFlags3F0:X}, query={selected.Current.QueryD5BeA0}, ownedFeature={selected.OwnedFeatureSource is not null}.");
        var globals=selected.PixelDefinitions.Buffers.Single(b=>b.Name.Text=="$Globals");
        if(globals.ByteSize!=480||new[]{("fCausticsScaler",192,4),("vCausticsRange",352,16),("mW2Ca",368,64)}
            .Any(f=>!globals.Variables.Any(v=>v.Name.Text==f.Item1&&v.StartOffset==f.Item2&&v.ByteSize==f.Item3)))
            throw new InvalidDataException("Caustics source CB layout changed.");
        foreach(uint type in new uint[]{2,3})
        {
            string name=type==2?"sCausticsSampler":"__smpsCausticsSampler";
            var b=selected.PixelDefinitions.Bindings.Single(b=>b.InputType==type&&b.Name.Text==name);
            if(b.BindPoint!=1||b.BindCount!=1||(type==2&&(b.Dimension!=4||b.ReturnType!=5)))
                throw new InvalidDataException("Caustics source descriptor differs from exact paired t1/s1 float2D.");
        }
    }
    internal static bool IsSupportedCurrentContext(OriginalMainMapContextCurrentPacket current)
        => current.ContextFlags3F4==0 && (current.ContextFlags3F0==0 ||
            current.ContextFlags3F0==0x40 && current.Phase==OriginalMainMapContextCurrentPhase.NativeSpecializedGeometryFlagWriter);
}
