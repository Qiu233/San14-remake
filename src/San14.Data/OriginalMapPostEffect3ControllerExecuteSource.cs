using System.Buffers.Binary;
using System.Text;

namespace San14.Data;

/// <summary>Copied current controller fields consumed by E22860. Raw float
/// bits and native byte/dword widths are preserved; this input does not prove
/// texture readiness, a complete controller, or an original process sample.</summary>
public sealed record OriginalMapPostEffect3ControllerScalarSnapshot(long FrameNumber,string ControllerOwnerIdentity,
    int StopBladeCount6C,byte ChangeFStopA1,byte ExternalStarA0,int Diaphragm9C,int EnableDofA4,
    byte SpriteA9,uint BloomRadiusBitsDC,byte BloomEnableAA,string SourceEvidence)
{
    public static OriginalMapPostEffect3ControllerScalarSnapshot CaptureActual(long frameNumber,string controllerOwnerIdentity,
        ReadOnlySpan<byte> actualControllerPrefix,string sourceEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controllerOwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);
        if(frameNumber<0||actualControllerPrefix.Length<0xE0)
            throw new InvalidDataException("Controller scalar snapshot requires an actual frame and complete prefix through DC.");
        return new(frameNumber,controllerOwnerIdentity,I(actualControllerPrefix,0x6C),actualControllerPrefix[0xA1],
            actualControllerPrefix[0xA0],I(actualControllerPrefix,0x9C),I(actualControllerPrefix,0xA4),actualControllerPrefix[0xA9],
            BinaryPrimitives.ReadUInt32LittleEndian(actualControllerPrefix[0xDC..]),actualControllerPrefix[0xAA],sourceEvidence);
    }
    private static int I(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
}

/// <summary>The three actual ABI bytes, current QualityLevel DWORD and selected
/// module functor. Quality is an owned caller input, never a guessed executable
/// .data initializer. Arg5 is the raw byte consumed at entryRsp+28.</summary>
public sealed record OriginalMapPostEffect3ControllerExecuteCaller(long FrameNumber,byte Argument3R8B,
    byte Argument4R9B,byte Argument5EntryRsp28,int ActualQualityLevel,int ActualSelectedFunctorIndex,string SourceEvidence);

public sealed class LoadedOriginalMapPostEffect3ControllerExecuteSource
{
    private readonly uint[] callSites;
    private readonly uint namedSetter;
    internal LoadedOriginalMapPostEffect3ControllerExecuteSource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)
    {
        Native=native;SourceEvidence=evidence;
        callSites=Declarations.Select(d=>(uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(native.Locale,
            "OriginalMapPostEffect3ControllerExecuteSource.cs",(int)d.Call)).ToArray();
        namedSetter=(uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(native.Locale,"OriginalMapPostEffect3ControllerExecuteSource.cs",0x114B170);
    }
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public bool ProvesGpuExecution=>false;
    /// <summary>E243F0 writes A0..AE, and its final setter dirties AF. Other
    /// controller members and derived math are outside this scalar owner.</summary>
    public ReadOnlyMemory<byte> FreshResetFlagsA0ToAF=>new byte[]{0,1,0,0,0,0,0,0,1,1,1,0,0,0,0,1};
    public OriginalMapPostEffect3ControllerScalarOwner ConstructFreshScalarOwner(
        OriginalMapPostEffect3Container module,string controllerOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(module);ArgumentException.ThrowIfNullOrWhiteSpace(controllerOwnerIdentity);
        OriginalMapPostEffect3ParameterState.ValidateSource(module);
        if(module.Functors.Count!=2)throw new InvalidDataException("Controller execution needs the actual two source module functors.");
        return new(this,module,controllerOwnerIdentity);
    }

    internal IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> PrepareWrites(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3ControllerScalarSnapshot snapshot,
        OriginalMapPostEffect3ControllerExecuteCaller caller)
    {
        if(snapshot.FrameNumber!=state.FrameNumber||caller.FrameNumber!=state.FrameNumber||
            string.IsNullOrWhiteSpace(snapshot.ControllerOwnerIdentity)||string.IsNullOrWhiteSpace(snapshot.SourceEvidence)||
            string.IsNullOrWhiteSpace(caller.SourceEvidence)||state.SourceSha256!=OriginalMapFinalOutputPostEffect3Source.PayloadSha256)
            throw new InvalidDataException("Controller execution requires exact current scalar snapshot/caller frame and known provenance.");
        // E22B0E branches to false for nonzero arg5. A4 is CMP DWORD,
        // not a byte comparison. This intentionally differs from the old
        // audit JSON's incorrectly inverted arg5 predicate.
        bool first=caller.Argument3R8B!=0||caller.Argument4R9B!=0||
            (caller.Argument5EntryRsp28==0&&snapshot.EnableDofA4!=0);
        uint[] words=[unchecked((uint)snapshot.StopBladeCount6C),Bool(snapshot.ChangeFStopA1),Bool(snapshot.ExternalStarA0),
            unchecked((uint)snapshot.Diaphragm9C),Bool(snapshot.EnableDofA4),Bool(snapshot.SpriteA9),first?1u:0u,
            snapshot.BloomRadiusBitsDC,Bool(snapshot.BloomEnableAA),unchecked((uint)caller.ActualQualityLevel)];
        var writes=new List<OriginalMapPostEffect3ApplyValueWrite>(Declarations.Length);
        for(int j=0;j<Declarations.Length;j++)
        {
            var d=Declarations[j];int index=state.FindScalarIndex(d.Hash)??throw new InvalidDataException("Controller source scalar is absent: "+d.Name);
            var target=state.Declarations[index];
            if(index!=d.Index||target.NativeValueType!=d.Type||target.ElementCount!=1||target.ValueByteCount!=4||
                OriginalMapPostEffect3ParameterState.PrimaryNameHash(d.Name)!=d.Hash)
                throw new InvalidDataException("Controller source scalar declaration changed: "+d.Name);
            byte[] bytes=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(bytes,words[j]);
            writes.Add(new(d.Name,callSites[j],namedSetter,bytes,SourceEvidence+";"+snapshot.SourceEvidence+";"+caller.SourceEvidence+
                $";actual-controller={snapshot.ControllerOwnerIdentity};source-ordered-named-scalar-{j}"));
        }
        return writes.AsReadOnly();
    }
    private static uint Bool(byte value)=>value!=0?1u:0u;
    private static uint Bool(int value)=>value!=0?1u:0u;
    internal static readonly (string Name,int Index,uint Hash,uint Type,uint Call,int StringRva)[] Declarations=[
        ("StopBladeCount",58,0x84A28F6C,6,0xE2293E,0x16DC670),
        ("ChangeFStop",69,0xACBFA86B,6,0xE22A60,0x16DC6D8),
        ("EnableExternalStarBurst",70,0x9991F7C5,6,0xE22A7E,0x16DC6E8),
        ("DiaphragmType",71,0x414B6021,6,0xE22A9C,0x16DC700),
        ("EnableDof",85,0xD3729FAD,6,0xE22B4A,0x16DC728),
        ("EnableSpriteDof",86,0x6E82D592,6,0xE22B68,0x16DC738),
        ("IsFirstExec",87,0xCEB30472,6,0xE22B89,0x16DC748),
        ("BloomRadius",95,0xAC8F38CD,1,0xE22BC5,0x16DC758),
        ("EnableBloom",98,0xB344DE25,6,0xE22BE3,0x16DC768),
        ("QualityLevel",84,0x7A1FA336,6,0xE22C03,0x16DC778)
    ];
}

/// <summary>Owned controller scalar fields only. E243F0 reset runs once at
/// construction; E22860 clears A1 after the actual module call returns, including
/// native failure. This does not own LUTs, ghost buffers or factory handlers.</summary>
public sealed class OriginalMapPostEffect3ControllerScalarOwner
{
    private readonly LoadedOriginalMapPostEffect3ControllerExecuteSource source;
    private readonly OriginalMapPostEffect3Container module;
    private OriginalMapPostEffect3ControllerScalarSnapshot current;
    private OriginalMapPostEffect3ControllerScalarInvocation? pending;
    private long lastStartedFrame=-1;
    private long? acceptedSnapshotFrame;
    internal OriginalMapPostEffect3ControllerScalarOwner(LoadedOriginalMapPostEffect3ControllerExecuteSource source,
        OriginalMapPostEffect3Container module,string ownerIdentity)
    {
        this.source=source;this.module=module;OwnerIdentity=ownerIdentity;
        current=new(0,ownerIdentity,6,1,0,0,0,1,0x40400000,1,
            source.SourceEvidence+";E243F0-source-reset-6C=6/9C=0/A0=0/A1=1/A4DWORD=0/A9=1/AA=1/DCbits=40400000");
    }
    public string OwnerIdentity{get;}
    public bool IsCompleteController=>false;
    public byte ChangeFStopA1=>current.ChangeFStopA1;
    public OriginalMapPostEffect3ControllerScalarSnapshot CaptureCurrentSnapshot(long actualFrameNumber)
    {
        if(actualFrameNumber<0)throw new InvalidDataException("Controller snapshot frame cannot be negative.");
        return current with{FrameNumber=actualFrameNumber};
    }
    public void AcceptCurrentSnapshot(OriginalMapPostEffect3ControllerScalarSnapshot actualSnapshot)
    {
        ArgumentNullException.ThrowIfNull(actualSnapshot);
        if(pending is not null||actualSnapshot.ControllerOwnerIdentity!=OwnerIdentity||
            actualSnapshot.FrameNumber<=lastStartedFrame||string.IsNullOrWhiteSpace(actualSnapshot.SourceEvidence))
            throw new InvalidDataException("Controller snapshot update is stale, foreign or inside a pending module execution.");
        current=actualSnapshot with{};acceptedSnapshotFrame=actualSnapshot.FrameNumber;
    }
    public OriginalMapPostEffect3ControllerScalarInvocation BeginExecute(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3ControllerExecuteCaller caller)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(caller);
        OriginalMapPostEffect3ParameterState.ValidateSource(module);
        if(pending is not null||state.FrameNumber<=lastStartedFrame||state.OwnerIdentity!=OwnerIdentity||
            acceptedSnapshotFrame is long capturedFrame&&capturedFrame!=state.FrameNumber||
            caller.ActualSelectedFunctorIndex<0||caller.ActualSelectedFunctorIndex>=module.Functors.Count)
            throw new InvalidDataException("Controller scalar invocation is pending, stale, foreign or selects an absent source functor.");
        var snapshot=CaptureCurrentSnapshot(state.FrameNumber);
        var writes=source.PrepareWrites(state,snapshot,caller);
        // Validate all ten declarations before the first setter. Other scalar
        // bytes, initialized masks and writer evidence remain untouched.
        state.ApplyKnownCameraWrites(new(state.FrameNumber,writes,source.SourceEvidence));
        lastStartedFrame=state.FrameNumber;current=snapshot;
        return pending=new(this,state,snapshot,caller,writes,source.SourceEvidence);
    }
    internal void RequirePending(OriginalMapPostEffect3ControllerScalarInvocation invocation)
    {
        if(!ReferenceEquals(pending,invocation))throw new InvalidOperationException("Controller module invocation is no longer pending.");
    }
    internal void CompleteAfterModuleReturn(OriginalMapPostEffect3ControllerScalarInvocation invocation)
    {
        RequirePending(invocation);
        // E22C8E executes even if 11466F0 returned AL0. The scalar ChangeFStop
        // remains the value just submitted; only the controller byte is reset.
        current=current with{ChangeFStopA1=0,SourceEvidence=current.SourceEvidence+";E22C8E-controller-A1-unconditionally-cleared-after-actual-module-return"};
        pending=null;acceptedSnapshotFrame=null;
    }
}

public sealed class OriginalMapPostEffect3ControllerScalarInvocation
{
    private readonly OriginalMapPostEffect3ControllerScalarOwner owner;
    internal OriginalMapPostEffect3ControllerScalarInvocation(OriginalMapPostEffect3ControllerScalarOwner owner,
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3ControllerScalarSnapshot snapshot,
        OriginalMapPostEffect3ControllerExecuteCaller caller,IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> writes,string evidence)
        =>(this.owner,State,Snapshot,Caller,Writes,SourceEvidence)=(owner,state,snapshot,caller,writes,evidence);
    public OriginalMapPostEffect3ParameterState State{get;}
    public OriginalMapPostEffect3ControllerScalarSnapshot Snapshot{get;}
    public OriginalMapPostEffect3ControllerExecuteCaller Caller{get;}
    public IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> Writes{get;}
    public string SourceEvidence{get;}
    public bool? NativeControllerReturn{get;private set;}
    public bool ProvesGpuExecution=>false;
    public void RequirePending()=>owner.RequirePending(this);
    /// <summary>Report the genuine module AL, including a failed Functor. A
    /// managed exception before that return is not invented as native AL0.</summary>
    public bool ReportModuleReturn(byte actualNativeModuleReturn)
    {
        owner.CompleteAfterModuleReturn(this);
        bool result=(actualNativeModuleReturn&1)!=0;NativeControllerReturn=result;return result;
    }
}

public static class OriginalMapPostEffect3ControllerExecuteSource
{
    public static LoadedOriginalMapPostEffect3ControllerExecuteSource Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.ReadPostEffect3ControllerExecuteSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3ControllerExecuteSource ReadPostEffect3ControllerExecuteSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {

        return new(native,native.ResourceUri+$";{native.Locale}:paired-controller-execution/reset-profile;TC-reference-role:E24C89-defaults/E24C91-E243F0-reset;"+
            "E22860..E22CB2-complete-source-sha725BF0BA;114B170-named-exact4B-source-order;"+
            "E22AEA-CMP-DWORD-A4;E22B04/E22B09/E22B0E-IsFirstExec=arg3||arg4||(!arg5&&Dof);"+
            "E22BEF-QualityLevel-actual-current-1A8C788-required;E22C1D-actual-selected-functor;"+
            "E22C25-return-AL-bit0;E22C8E-A1-reset-regardless-of-native-success;no-texture/ghost/GPU-claim");
    }
}
