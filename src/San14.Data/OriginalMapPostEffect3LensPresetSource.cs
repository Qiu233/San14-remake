using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapPostEffect3LensPresetRelocation(int PointerOffset,int TargetOffset);

/// <summary>Successful owned original class162FB139 declaration and builtin0
/// CPU payload. This does not observe a native active Camera or GPU pass.</summary>
public sealed class LoadedOriginalMapPostEffect3LensPresetSource
{
    internal LoadedOriginalMapPostEffect3LensPresetSource(uint camera,string cameraSha,string locale,string evidence)
        =>(CameraObjectId,CameraSourceSha256,ExecutableLocale,SourceEvidence)=(camera,cameraSha,locale,evidence);
    public uint CameraObjectId{get;}
    public string CameraSourceSha256{get;}
    public string ExecutableLocale{get;}
    public string SourceEvidence{get;}
    public uint SourceObjectId=>0x3D60C3C7;
    public uint SourceClassId=>0x162FB139;
    public int BuiltinIndex=>0;
    public int PayloadByteCount=>656;
    public int NativeAllocationByteCount=>672;
    public int GhostCount=>4;
    public int FlareCount=>16;
    public bool NativeRetainedPaddingPreserved=>false;
    public IReadOnlyList<int> BackendPaddingOffsets=>Array.AsReadOnly(OriginalMapPostEffect3LensPresetBuiltin0.RetainedPaddingOffsets.ToArray());
    public ReadOnlyMemory<byte> PrimitiveBytes=>OriginalMapPostEffect3LensPresetBuiltin0.CopyPrimitive();
    public string PrimitiveSha256=>OriginalMapPostEffect3LensPresetBuiltin0.PrimitiveSha256;
    public bool PrimitivePointerWordsRequireRelocation=>true;
    public IReadOnlyList<OriginalMapPostEffect3LensPresetRelocation> Relocations=>Array.AsReadOnly(
        new[]{new OriginalMapPostEffect3LensPresetRelocation(0x60,0x90),new OriginalMapPostEffect3LensPresetRelocation(0x70,0xD0)});
    public ReadOnlyMemory<byte> NativeWrittenByteMask
    {
        get{byte[] mask=Enumerable.Repeat((byte)1,PayloadByteCount).ToArray();
            foreach(int offset in OriginalMapPostEffect3LensPresetBuiltin0.RetainedPaddingOffsets)mask[offset]=0;
            return mask;}
    }
    public void ValidateForCamera(LoadedOriginalMapCameraConstructionSource actualCameraSource)
    {
        ArgumentNullException.ThrowIfNull(actualCameraSource);
        var canonical=OriginalMapCameraConstructionSource.ReadPerspective(actualCameraSource.Resource,actualCameraSource.Camera.ObjectId);
        if(canonical.Camera.ObjectId!=CameraObjectId||canonical.Resource.Source.DecodedSha256!=CameraSourceSha256)
            throw new InvalidDataException("LensPreset belongs to another source Camera.");
        OriginalMapPostEffect3LensPresetSource.RequireCameraKey(canonical);
    }

    /// <summary>Actual managed-owned pin addresses, never guessed native process
    /// addresses. Camera15CC output requires its separate explicit producer.</summary>
    public OriginalMapPostEffect3LensPresetParameterOwner PinCurrentParameters(
        OriginalMapPostEffect3LensPresetIntermediatePacket currentIntermediate,string ownerIdentity)
        =>new(this,currentIntermediate,ownerIdentity);
    public OriginalMapPostEffect3LensPresetParameterOwner PinCurrentParameters(
        OriginalMapPostEffect3LensPresetPreparedIntermediatePacket currentIntermediate,string ownerIdentity)
        =>new(this,currentIntermediate,ownerIdentity);
}

public static class OriginalMapPostEffect3LensPresetSource
{
    public const string ResourceSha256="357F6A380CF19EB2A86B439AF24CF2BACDD38074AAF9BC3AE99F0EAD21EE9271";
    internal static void RequireCameraKey(LoadedOriginalMapCameraConstructionSource source)
    {
        var p=source.Camera.Properties[285];
        if(p.Index!=285||p.NameHash!=0xA4541BE1||p.TypeCode!=5||p.ElementCount!=1||p.RawValue.Length!=4||
            BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue)!=0x3D60C3C7)
            throw new InvalidDataException("LensPreset requires original Camera property285/A4541BE1/3D60C3C7.");
    }
    public static LoadedOriginalMapPostEffect3LensPresetSource LoadForCamera(OriginalGameRepository repository,
        LoadedOriginalMapCameraConstructionSource cameraSource,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(cameraSource);
        if(locale is not ("TC" or "SC"))throw new ArgumentOutOfRangeException(nameof(locale));
        var camera=OriginalMapCameraConstructionSource.ReadPerspective(cameraSource.Resource,cameraSource.Camera.ObjectId);
        RequireCameraKey(camera);
        var source=repository.LoadObjects("system",0xDAB638B2);
        if(source.Source.OverrideSource is not null||source.Source.DecodedSha256!=ResourceSha256||
            Convert.ToHexString(SHA256.HashData(source.Source.Bytes))!=ResourceSha256)
            throw new InvalidDataException("LensPreset source archive differs from the pinned original DAB638B2.");
        var rows=DokReader.Read(source.Source.Bytes).Objects.Where(o=>o.ObjectId==0x3D60C3C7).ToArray();
        if(rows.Length!=1||rows[0].Kind!=DokObjectKind.Inline||rows[0].ClassId!=0x162FB139||rows[0].Properties.Count!=29)
            throw new InvalidDataException("Original LensPreset object/class/29-property declaration differs.");
        var preset=rows[0];
        foreach(var (index,hash) in new[]{(27,0x29B2A52Eu),(28,0x081439EAu)})
        {
            var p=preset.Properties[index];
            if(p.Index!=index||p.NameHash!=hash||p.TypeCode!=4||p.ElementCount!=1||p.RawValue.Length!=4)
                throw new InvalidDataException("LensPreset native branch scalar declaration differs.");
            if(BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue)!=0)
                throw new NotSupportedException("LensPreset source currently supports the actual stock builtin0, flags0 branch only.");
        }
        byte[] primitive=OriginalMapPostEffect3LensPresetBuiltin0.CopyPrimitive();
        if(primitive.Length!=656||Convert.ToHexString(SHA256.HashData(primitive))!=OriginalMapPostEffect3LensPresetBuiltin0.PrimitiveSha256)
            throw new InvalidDataException("LensPreset builtin0 recovered source bytes differ.");
        string evidence=$"{source.Source.ResourceUri}#object/3D60C3C7@{ResourceSha256};"+
            $"Camera285/A4541BE1={camera.Camera.ObjectId:X8};source-created-successful-builtin0-resolution;"+
            "TC-E20750/E20A83/1148C80/1148180;SC-paired-opcodes-and-readonly-data;"+
            "payload656/alloc672/ghost4/flare16;retained-padding10-backend-zero/non-native-byte-identity;"+
            "Split=Data20.Cpu08.payload;Intermediate=Camera15CC-separate-current-producer;locale="+locale;
        return new(camera.Camera.ObjectId,camera.Resource.Source.DecodedSha256,locale,evidence);
    }
}

/// <summary>Exact92B output of the separate native1148F00 Camera preparation.
/// No implicit zero buffer is supplied by the LensPreset declaration loader.</summary>
public sealed class OriginalMapPostEffect3LensPresetIntermediatePacket
{
    private readonly byte[] bytes;
    private OriginalMapPostEffect3LensPresetIntermediatePacket(OriginalMapCameraConstructionPacket camera,
        byte[] bytes,string evidence)=>(Camera,FrameNumber,this.bytes,SourceEvidence)=(camera.CopyAcceptedSnapshot(),camera.FrameNumber,bytes,evidence);
    public OriginalMapCameraConstructionPacket Camera{get;}
    public long FrameNumber{get;}
    public string SourceEvidence{get;}
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
    internal static OriginalMapPostEffect3LensPresetIntermediatePacket FromProducedMath(
        OriginalMapCameraConstructionPacket camera,ReadOnlySpan<byte> produced,string evidence)
    {
        if(camera.FrameNumber<=0||produced.Length!=92||BinaryPrimitives.ReadUInt32LittleEndian(produced[88..])!=0)
            throw new InvalidDataException("Native Lens intermediate writer requires all92 bytes and its explicit zero tail.");
        for(int i=0;i<88;i+=4)if(!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(produced[i..])))
            throw new NotSupportedException("Native Lens intermediate left its recovered finite mathematical domain.");
        return new(camera,produced.ToArray(),evidence);
    }
    /// <summary>Explicit owned whole-Camera writer output only; this method
    /// validates its early-prefix identity and slices Camera15CC..1627.
    /// It does not certify how the caller produced this additional output.</summary>
    public static OriginalMapPostEffect3LensPresetIntermediatePacket FromExplicitOwnedPreparedCamera(
        OriginalMapCameraConstructionPacket acceptedCamera,ReadOnlySpan<byte> actualOwnedWholeCamera,
        string actualProducerEvidence)
    {
        ArgumentNullException.ThrowIfNull(acceptedCamera);ArgumentException.ThrowIfNullOrWhiteSpace(actualProducerEvidence);
        if(acceptedCamera.FrameNumber<=0||actualOwnedWholeCamera.Length<0x1628||
            !actualOwnedWholeCamera[..0x3C0].SequenceEqual(acceptedCamera.Prefix.Span))
            throw new InvalidDataException("LensPreset intermediate requires the actual owned same-frame whole Camera and identical accepted early prefix.");
        byte[] bytes=actualOwnedWholeCamera.Slice(0x15CC,92).ToArray();
        if(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(88,4))!=0)
            throw new InvalidDataException("Native1148F00 intermediate tail58 must be its explicit zero write.");
        return new(acceptedCamera,bytes,actualProducerEvidence+";explicit-owned-Camera15CC/92B-native-output;producer-not-inferred");
    }
}

/// <summary>Lifetime owner for pointer-valued native parameters. Its addresses
/// reference pinned owned CPU data; they are not GPU descriptors. Keep this
/// owner alive until all CPU parameter consumers complete.</summary>
public sealed class OriginalMapPostEffect3LensPresetParameterOwner:IDisposable
{
    private readonly byte[] split,intermediate;
    private readonly bool supportsPrepareBase;
    private GCHandle splitPin,intermediatePin;
    private readonly HashSet<OriginalMapPostEffect3ParameterState> boundStates=[];
    private bool disposed;
    internal OriginalMapPostEffect3LensPresetParameterOwner(LoadedOriginalMapPostEffect3LensPresetSource source,
        OriginalMapPostEffect3LensPresetIntermediatePacket input,string owner)
        :this(source,input.Camera,input.FrameNumber,input.Bytes,input.SourceEvidence,owner,false){}
    internal OriginalMapPostEffect3LensPresetParameterOwner(LoadedOriginalMapPostEffect3LensPresetSource source,
        OriginalMapPostEffect3LensPresetPreparedIntermediatePacket input,string owner)
        :this(source,input.Camera,input.FrameNumber,input.Bytes,input.SourceEvidence,owner,true){}
    private OriginalMapPostEffect3LensPresetParameterOwner(LoadedOriginalMapPostEffect3LensPresetSource source,
        OriginalMapCameraConstructionPacket camera,long frame,ReadOnlyMemory<byte> bytes,string inputEvidence,string owner,bool prepareBase)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        source.ValidateForCamera(camera.Source);
        if(camera.ExecutableLocale!=source.ExecutableLocale||bytes.Length!=(prepareBase?96:92))
            throw new InvalidDataException("LensPreset native locale/owned Intermediate extent differs from the actual Camera.");
        Camera=camera.CopyAcceptedSnapshot();Source=source;FrameNumber=frame;OwnerIdentity=owner;
        SourceEvidence=source.SourceEvidence+";"+inputEvidence;supportsPrepareBase=prepareBase;
        split=source.PrimitiveBytes.ToArray();intermediate=bytes.ToArray();
        splitPin=GCHandle.Alloc(split,GCHandleType.Pinned);
        try
        {
            intermediatePin=GCHandle.Alloc(intermediate,GCHandleType.Pinned);
            ulong address=unchecked((ulong)splitPin.AddrOfPinnedObject().ToInt64());
            BinaryPrimitives.WriteUInt64LittleEndian(split.AsSpan(0x60,8),checked(address+0x90));
            BinaryPrimitives.WriteUInt64LittleEndian(split.AsSpan(0x70,8),checked(address+0xD0));
        }
        catch{splitPin.Free();if(intermediatePin.IsAllocated)intermediatePin.Free();throw;}
    }
    public LoadedOriginalMapPostEffect3LensPresetSource Source{get;}
    public OriginalMapCameraConstructionPacket Camera{get;}
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}
    public string SourceEvidence{get;private set;}
    public ReadOnlyMemory<byte> SplitBytes{get{ObjectDisposedException.ThrowIf(disposed,this);return split.ToArray();}}
    public ReadOnlyMemory<byte> IntermediateBytes{get{ObjectDisposedException.ThrowIf(disposed,this);return intermediate.AsSpan(0,92).ToArray();}}
    public ReadOnlyMemory<byte> IntermediateStorageBytes{get{ObjectDisposedException.ThrowIf(disposed,this);return intermediate.ToArray();}}
    public bool SupportsPrepareBase=>supportsPrepareBase;
    public uint NativeComparisonTag5C{get{RequirePrepareBaseStorage();return BinaryPrimitives.ReadUInt32LittleEndian(intermediate.AsSpan(0x5C,4));}}
    internal void RequirePrepareBaseStorage()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(!supportsPrepareBase||intermediate.Length!=96)
            throw new NotSupportedException("Lens PrepareBase requires a complete96B owner including its actual5C native frame marker.");
    }
    internal void CommitPrepareBase(OriginalMapPostEffect3LensPresetIntermediatePacket packet,uint nativeContextTag,string evidence)
    {
        RequirePrepareBaseStorage();Source.ValidateForCamera(packet.Camera.Source);
        if(packet.FrameNumber!=FrameNumber||!packet.Camera.Prefix.Span.SequenceEqual(Camera.Prefix.Span))
            throw new InvalidDataException("Lens PrepareBase update belongs to another owned Camera frame/prefix.");
        packet.Bytes.Span.CopyTo(intermediate);
        BinaryPrimitives.WriteUInt32LittleEndian(intermediate.AsSpan(0x5C,4),nativeContextTag);
        SourceEvidence+= ";"+packet.SourceEvidence+";"+evidence;
    }
    public void ValidatePointerWrites(OriginalMapPostEffect3ParameterState state)
    {
        ObjectDisposedException.ThrowIf(disposed,this);ArgumentNullException.ThrowIfNull(state);
        if(state.FrameNumber!=FrameNumber||!boundStates.Contains(state)||
            BinaryPrimitives.ReadUInt64LittleEndian(state.RequireValue("LensPresetSplit").Span)!=unchecked((ulong)splitPin.AddrOfPinnedObject().ToInt64())||
            BinaryPrimitives.ReadUInt64LittleEndian(state.RequireValue("LensPresetIntermediate").Span)!=unchecked((ulong)intermediatePin.AddrOfPinnedObject().ToInt64()))
            throw new InvalidDataException("LensPreset scalar state does not bind this actual current pinned owner.");
    }
    public void ApplyPointerWrites(OriginalMapPostEffect3ParameterState state)
    {
        ObjectDisposedException.ThrowIf(disposed,this);ArgumentNullException.ThrowIfNull(state);
        if(state.FrameNumber!=FrameNumber)throw new InvalidDataException("LensPreset parameter pointers belong to another frame.");
        foreach(string name in new[]{"LensPresetSplit","LensPresetIntermediate"})
        {
            int index=state.FindScalarIndex(name)??throw new InvalidDataException("Original LensPreset pointer parameter is absent: "+name);
            if(state.Declarations[index].ValueByteCount!=8)throw new InvalidDataException("Original LensPreset pointer parameter width differs.");
        }
        byte[] Word(GCHandle pin){byte[] result=new byte[8];BinaryPrimitives.WriteUInt64LittleEndian(result,
            unchecked((ulong)pin.AddrOfPinnedObject().ToInt64()));return result;}
        state.SetByName("LensPresetSplit",Word(splitPin),SourceEvidence+";E72483-actual-owned-pinned-Split-address");
        state.SetByName("LensPresetIntermediate",Word(intermediatePin),SourceEvidence+";E724AD-actual-owned-pinned-Camera15CC-address");
        boundStates.Add(state);
    }
    public void Dispose()
    {
        if(disposed)return;
        // E73770 clears both pointer-valued scalars after their CPU consumers.
        // Release the corresponding owned pins only after this explicit clear.
        foreach(var state in boundStates)
        {
            state.SetByName("LensPresetSplit",new byte[8],SourceEvidence+";E73770-owned-LensPresetSplit-cleanup");
            state.SetByName("LensPresetIntermediate",new byte[8],SourceEvidence+";E73770-owned-LensPresetIntermediate-cleanup");
        }
        boundStates.Clear();disposed=true;
        if(splitPin.IsAllocated)splitPin.Free();if(intermediatePin.IsAllocated)intermediatePin.Free();
    }
}
