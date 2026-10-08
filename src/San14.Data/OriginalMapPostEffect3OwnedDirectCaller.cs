using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Explicit fresh owned graphics configuration. TC's file-backed
/// initializer at1A8C934 is1; E7212A consumes that word. This is not a read of
/// the original process's mutable configuration or a renderer-wide switch.</summary>
public sealed class OriginalMapPostEffect3FreshGraphicsConfiguration
{
    private OriginalMapPostEffect3FreshGraphicsConfiguration(string owner,string evidence,uint gamma,string locale)
        =>(OwnerIdentity,SourceEvidence,GammaEnableWord,ExecutableLocale)=(owner,evidence,gamma,locale);
    public string OwnerIdentity{get;}
    public string SourceEvidence{get;}
    public uint GammaEnableWord{get;}
    public string ExecutableLocale{get;}
    public static OriginalMapPostEffect3FreshGraphicsConfiguration LoadFreshTc(
        OriginalGameRepository repository,string actualOwnedGraphicsConfigurationIdentity)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnedGraphicsConfigurationIdentity);
        // This separate legacy F0 initializer remains scoped to TC. Ordinary
        // F1 uses its actual same-locale ControlledFreshCaller configuration.
        const string locale="TC";
        var native=OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale);
        string expected=native.ExecutableSha256;
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);
        if(Convert.ToHexString(SHA256.HashData(exe))!=expected)
            throw new InvalidDataException("Fresh PostEffect3 graphics configuration requires its actual pinned executable.");
        using var pe=new PEReader(new MemoryStream(exe,false));
        const int rva=0x1A8C934;
        if(pe.PEHeaders.PEHeader?.ImageBase!=0x140000000UL)
            throw new InvalidDataException("Fresh graphics initializer has the wrong native image base.");
        var section=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".data"&&rva>=s.VirtualAddress&&
            (long)rva+4<=(long)s.VirtualAddress+s.SizeOfRawData);
        uint value=BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(section.PointerToRawData+rva-section.VirtualAddress,4));
        if(value!=1)throw new InvalidDataException("Original fresh GammaEnable initializer changed.");
        return new(actualOwnedGraphicsConfigurationIdentity,native.ResourceUri+"@"+expected+
            $";file-backed-data-initializer{rva:X8}=1;original-GammaEnable;explicit-fresh-owned-configuration;no-original-current-state",value,locale);
    }
}

/// <summary>The actual pre-Apply output-target gamma predicate and same-frame
/// Q owner. Wrapper+60==1 is a native target property, not the Scene's format
/// code. Its caller supplies the already resolved target receipt explicitly.</summary>
public sealed record OriginalMapPostEffect3DirectCallerOutputTarget(long FrameNumber,
    string TargetOwnerIdentity,string ContextOwnerIdentity,bool NativeTarget60Equals1,
    bool ContextVirtual80Result,string TargetAndContextEvidence);

/// <summary>Exact Direct EB7EC1 scalar ABI plus the independent D586B0 lookup
/// arguments. This receipt selects no light and performs no parameter writes.</summary>
public sealed class OriginalMapPostEffect3OwnedDirectCaller
{
    private readonly OriginalMapCameraConstructionPacket camera;
    private OriginalMapPostEffect3OwnedDirectCaller(OriginalMapCameraConstructionPacket snapshot,
        OriginalMapPostEffect3ApplyUncontrolledSimpleInputs simple,bool shaft,int viewIndex,byte group,
        string context,string evidence)
    {camera=snapshot;SimpleInputs=simple;ActualLightShaftArgument=shaft;ActualViewIndexArgument=viewIndex;
        ActualAuxGroupArgument=group;ContextOwnerIdentity=context;SourceEvidence=evidence;}
    public OriginalMapPostEffect3ApplyUncontrolledSimpleInputs SimpleInputs{get;}
    public long FrameNumber=>SimpleInputs.FrameNumber;
    public bool ActualLightShaftArgument{get;}
    public int ActualViewIndexArgument{get;}
    public byte ActualAuxGroupArgument{get;}
    public uint D586B0ExclusionMask=>~(1u<<((ActualAuxGroupArgument-1)&31));
    public string ContextOwnerIdentity{get;}
    public string SourceEvidence{get;}

    /// <summary>The existing World frame's Common scope proves its saved
    /// template was applied with no later callback. Source3A8 is joined to
    /// that actual Common owner. Renderer delta is an explicit owned time
    /// input; MAXSS(0,delta) is preserved, without asserting native timestamps.
    /// The caller must resolve Apply's current View slot and output predicates.</summary>
    public static OriginalMapPostEffect3OwnedDirectCaller PrepareFreshWorldFrame(
        OriginalMapSourceCreatedF103CurrentFrame actualWorldFrame,LoadedObjects selectedCommonTemplate,
        OriginalMapPostEffect3FreshGraphicsConfiguration actualGraphics,
        OriginalMapPostEffect3DirectCallerOutputTarget actualOutputTarget,
        float actualRendererElapsedDelta,string actualClockOwnerIdentity,
        int actualResolvedApplyViewIndex,string actualApplyViewIndexEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualWorldFrame);ArgumentNullException.ThrowIfNull(selectedCommonTemplate);
        ArgumentNullException.ThrowIfNull(actualGraphics);ArgumentNullException.ThrowIfNull(actualOutputTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualClockOwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualApplyViewIndexEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOutputTarget.TargetOwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOutputTarget.TargetAndContextEvidence);
        var current=actualWorldFrame.Lighting.CapturePreparedCameraFromSameAcceptedMain(actualWorldFrame.Camera);
        var common=actualWorldFrame.Common;
        var sourceCommon=OriginalMapFreshCommonResources.ReadSourceTemplate(selectedCommonTemplate);
        if(current.ExecutableLocale!=actualGraphics.ExecutableLocale||current.ExecutableLocale!=actualWorldFrame.Context.Source.Host.Locale||
            current.FrameNumber<=0||actualWorldFrame.FrameNumber!=common.FrameNumber||
            actualOutputTarget.FrameNumber!=current.FrameNumber||actualWorldFrame.Context.FrameNumber!=current.FrameNumber||
            actualOutputTarget.ContextOwnerIdentity!=actualWorldFrame.Context.Source.OwnedHostIdentity||
            actualWorldFrame.Context.Source.Host.ContextOwnerVtableRva!=(uint)OriginalMapPostEffect3NativeSourceProfile.DataRva(
                current.ExecutableLocale,"OriginalMapPostEffect3OwnedDirectCaller.cs",0x13C4ED0)||actualOutputTarget.ContextVirtual80Result||
            common.Scope!=OriginalMapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication||
            !common.CommonViewResource240Present||common.CommonViewFlags8!=sourceCommon.Flags8||
            common.CurrentViewAux3F2!=sourceCommon.ViewAux3F2||actualResolvedApplyViewIndex is <0 or >=32||
            !float.IsFinite(actualRendererElapsedDelta))
            throw new InvalidDataException("Direct Apply needs the actual same-locale/frame World/Common/output owners, fresh no-callback scope and finite owned clock.");
        var row=selectedCommonTemplate.Document.Objects.Single(o=>o.ObjectId==OriginalMapFreshCommonResources.SourceObjectId);
        var fxaa=row.Properties[10];
        if(fxaa.Index!=10||fxaa.NameHash!=0xBB7C520E||fxaa.TypeCode!=4||fxaa.ElementCount!=1||fxaa.RawValue.Length!=4)
            throw new InvalidDataException("Direct Apply Common3A8 differs from its source FXAA loader field.");
        int auxFxaa=BinaryPrimitives.ReadInt32LittleEndian(fxaa.RawValue);
        var raw=OriginalMapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(current,
            common.SelectedViewOwnerIdentity+"/DirectEB7EC1-camera");
        int cameraFxaa=unchecked((int)raw.Word(0x414));
        float sourceGamma=BinaryPrimitives.ReadSingleLittleEndian(current.Prefix.Span.Slice(0x174,4));
        float gamma=ResolveGammaArgument(sourceGamma,actualOutputTarget.NativeTarget60Equals1,actualOutputTarget.ContextVirtual80Result);
        // EB620B/E621C..E6247 uses the prepared Camera's E28 and E1C/E20/E24.
        // This fresh-source scope has no later tail writer, so the exact source
        // property/copy joins are used; SceneType142 is not part of this bool.
        float P(int ordinal,uint hash,int count=1,int component=0)
        {
            var p=current.Source.Camera.Properties[ordinal];
            if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=8||p.ElementCount!=count||p.RawValue.Length!=count*4)
                throw new InvalidDataException("Direct Apply shaft predicate differs from its fresh Camera loader field.");
            return BinaryPrimitives.ReadSingleLittleEndian(p.RawValue.AsSpan(component*4,4));
        }
        float length=P(136,0xB2EB70EA),r=P(135,0x41C4B441,3,0),g=P(135,0x41C4B441,3,1),b=P(135,0x41C4B441,3,2);
        if(new[]{length,r,g,b}.Any(v=>!float.IsFinite(v)))
            throw new InvalidDataException("Direct Apply shaft source must be finite.");
        bool shaft=length>0&&(r>0||g>0||b>0);
        float delta=actualRendererElapsedDelta>=0?actualRendererElapsedDelta:0;
        string evidence="TC:EB7EC1-direct-E71690;EB6886/EB6890:Aux3A8-OR-Camera414;"+
            "EB5F5A/EB5F89/EB5FA1/EB7E3D:Gamma-two-reciprocal-divss;"+
            "EB7E26:MAXSS-zero-owned-renderer-delta;owned-clock="+actualClockOwnerIdentity+";"+
            "EB620B..EB6247/EB648C/EB7E8B:shaft-predicate;"+common.ProducerProvenance+";"+
            actualOutputTarget.TargetAndContextEvidence+";"+actualGraphics.SourceEvidence+";"+actualApplyViewIndexEvidence;
        var simple=new OriginalMapPostEffect3ApplyUncontrolledSimpleInputs(current.FrameNumber,auxFxaa|cameraFxaa,
            gamma,delta,actualGraphics.GammaEnableWord,common.CommonViewFlags8,evidence);
        return new(current.CopyAcceptedSnapshot(),simple,shaft,actualResolvedApplyViewIndex,sourceCommon.ViewAux3F2,
            common.CommonTableOwnerIdentity,evidence+";E71A28/E71A3B/E71A43:~(1u<<((Aux3F2-1)&31));E71A49-current-ViewIndex");
    }
    public void ValidateSameCamera(OriginalMapCameraConstructionPacket candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if(candidate.FrameNumber!=FrameNumber||candidate.ExecutableLocale!=camera.ExecutableLocale||
            candidate.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            candidate.Source.Resource.Source.ResourceUri!=camera.Source.Resource.Source.ResourceUri||
            candidate.Source.Resource.Source.DecodedSha256!=camera.Source.Resource.Source.DecodedSha256||
            !candidate.Prefix.Span.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Direct Apply receipt belongs to another actual Camera frame.");
    }
    public static float ResolveGammaArgument(float actualCamera174,bool nativeTarget60Equals1,bool actualContextVirtual80)
    {
        if(!float.IsFinite(actualCamera174)||actualCamera174<=0)
            throw new InvalidDataException("Direct Apply gamma must be positive and finite.");
        float adjusted=nativeTarget60Equals1||actualContextVirtual80?
            Div(actualCamera174,BitConverter.UInt32BitsToSingle(0x400CCCCD)):actualCamera174;
        return Div(1,Div(1,adjusted));
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Div(float a,float b)=>a/b;
}
