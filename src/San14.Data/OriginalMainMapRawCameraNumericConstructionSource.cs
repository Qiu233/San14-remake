using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct OriginalMainMapRawCameraCpuPropertyBinding(
    int Ordinal,int CpuOffset,uint NameHash,uint TypeCode,int ElementCount,int ByteCount);

/// <summary>One explicitly loaded stock Camera's direct numeric declaration.
/// Resource keys and scratch stack variables are not CPU pointer values.
/// Apply these fragments once while creating that Camera, before the actual
/// resource registry and subsequent V418/V460 consumers complete its loader.</summary>
public sealed class LoadedOriginalMainMapRawCameraNumericDeclaration
{
    private readonly (int Offset,byte[] Value)[] directValues;
    private readonly byte[] position,quaternion,focalLength;
    internal LoadedOriginalMainMapRawCameraNumericDeclaration(uint objectId,string evidence,
        (int Offset,byte[] Value)[] directValues,byte[] position,byte[] quaternion,byte[] focalLength)
        =>(ObjectId,SourceEvidence,this.directValues,this.position,this.quaternion,this.focalLength)=
            (objectId,evidence,directValues,position,quaternion,focalLength);
    public uint ObjectId{get;}
    public string SourceEvidence{get;}
    public int DirectPropertyCount=>directValues.Length;
    public Vector3 DeclaredPosition=>new(F(position,0),F(position,4),F(position,8));
    public Quaternion DeclaredQuaternion=>new(F(quaternion,0),F(quaternion,4),F(quaternion,8),F(quaternion,12));
    public float DeclaredFocalLength=>F(focalLength,0);

    /// <summary>The1023 actual C4F6F0 CPU destinations, in descriptor order.
    /// Does not copy78 resource keys into raw2Cxx or execute property callbacks.</summary>
    public void ApplyDirectCpuProperties(Span<byte> freshCpu)
    {
        LoadedOriginalMainMapRawCameraNumericConstructionSource.RequireStorage(freshCpu.Length);
        foreach(var value in directValues)value.Value.CopyTo(freshCpu[value.Offset..]);
    }

    /// <summary>Fresh unbound source position/quaternion setters and the
    /// actual loader's scratch FocalLength publication. Serialized quaternion
    /// components are copied without normalization. A real bound transform
    /// requires its own original inverse-transform setter path.</summary>
    public void ApplyDeclaredUnboundPoseAndFocalLength(Span<byte> freshCpu)
    {
        LoadedOriginalMainMapRawCameraNumericConstructionSource.RequireStorage(freshCpu.Length);
        if(BinaryPrimitives.ReadUInt64LittleEndian(freshCpu[0x2CA8..])!=0)
            throw new NotSupportedException("Bound raw Camera initialization requires its actual transform setters.");
        position.CopyTo(freshCpu[0x12FC..]);quaternion.CopyTo(freshCpu[0x1308..]);focalLength.CopyTo(freshCpu[0x0C..]);
    }
    private static float F(ReadOnlySpan<byte> value,int offset)=>BinaryPrimitives.ReadSingleLittleEndian(value[offset..]);
}

/// <summary>The full native4260 allocation's fixed constructor fields,1023
/// loader CPU bindings, and CAB4E0 numeric backup writer. Loaded once. These
/// are finite source fragments, not a completed Main Data/resource loader.
/// Resource2Cxx resolution, current getters, V418/V460 and post-loader state
/// retain their original independent producer order.</summary>
public sealed class LoadedOriginalMainMapRawCameraNumericConstructionSource
{
    private readonly byte[] constructorFields,constructorWrittenBytes;
    private readonly OriginalMainMapRawCameraCpuPropertyBinding[] bindings;
    private readonly OriginalMainMapRawCameraWordRange[] backupRanges;
    internal LoadedOriginalMainMapRawCameraNumericConstructionSource(string locale,string evidence,
        byte[] fields,byte[] written,OriginalMainMapRawCameraCpuPropertyBinding[] bindings,
        OriginalMainMapRawCameraWordRange[] backupRanges)
    {
        Locale=locale;SourceEvidence=evidence;constructorFields=fields;constructorWrittenBytes=written;
        this.bindings=bindings;this.backupRanges=backupRanges;
        DirectCpuPropertyBindings=Array.AsReadOnly(bindings);
        ConstructorWrittenByteCount=written.Count(value=>value!=0);
    }
    public const int NativeAllocationByteLength=0x4260;
    public string Locale{get;}
    public string SourceEvidence{get;}
    public int ConstructorWrittenByteCount{get;}
    public int ConstructorUnwrittenByteCount=>NativeAllocationByteLength-ConstructorWrittenByteCount;
    public int NumericBackupWordCount=>1212;
    public IReadOnlyList<OriginalMainMapRawCameraCpuPropertyBinding> DirectCpuPropertyBindings{get;}

    /// <summary>Actual fixed constructor stores only, including source VT
    /// address words as imported type metadata. They are not callable native
    /// pointers or resolved resource handles. The46 originally unwritten
    /// allocation bytes preserve the supplied storage's previous contents.</summary>
    public void ApplyFreshConstructor(Span<byte> freshlyAllocatedCpu)
    {
        RequireStorage(freshlyAllocatedCpu.Length);
        for(int i=0;i<NativeAllocationByteLength;i++)
            if(constructorWrittenBytes[i]!=0)freshlyAllocatedCpu[i]=constructorFields[i];
    }

    /// <summary>SourceCAB4E0's1196 direct and16 loop DWORD copies and one
    /// raw byte copy126C→41B4. This stores the current fields as the original
    /// numeric baseline; it does not resolve or back up resource pointers.</summary>
    public void CopyCurrentNumericBackup(Span<byte> actualCurrentCpu)
    {
        RequireStorage(actualCurrentCpu.Length);
        foreach(var range in backupRanges)
            actualCurrentCpu.Slice(range.SourceCpuOffset,range.WordCount*4)
                .CopyTo(actualCurrentCpu.Slice(range.DestinationCpuOffset,range.WordCount*4));
        actualCurrentCpu[0x41B4]=actualCurrentCpu[0x126C];
    }

    /// <summary>Import one actual stock Main38D or Game80=36C declaration
    /// once for this loaded generation. CPU property bytes are privately
    /// retained; normal initialization and updates do not re-read/hash assets.</summary>
    public LoadedOriginalMainMapRawCameraNumericDeclaration ReadStockPerspectiveDeclaration(
        LoadedObjects actualSource,uint objectId)
    {
        ArgumentNullException.ThrowIfNull(actualSource);
        if(objectId is not (0x38D04576u or 0x36C14145u))
            throw new NotSupportedException("This finite raw loader owns the explicit Main78 and independent Game80 declarations.");
        var source=actualSource.Source;
        if(source.FileId!=OriginalMapCameraConstructionSource.SourceFileId||
            source.ResourceUri!=OriginalMapCameraConstructionSource.SourceResourceUri||
            source.DecodedSha256!=OriginalMapCameraConstructionSource.SourceSha256||source.OverrideSource is not null)
            throw new InvalidDataException("Raw numeric initialization requires the loaded stock system908AB6E2 generation.");
        OriginalDeferredSamplerSources.VerifyRawCameraNumericDeclaration(source.Bytes);
        // Read the actual verified bytes once rather than trusting a supplied
        // mutable parser object. Normal callers keep this loaded declaration.
        var objects=DokReader.Read(source.Bytes).Objects;
        var selected=objects.Single(value=>value.ObjectId==objectId);
        if(selected.Kind!=DokObjectKind.Inline||selected.ClassId!=OriginalMapCameraConstructionSource.PerspectiveCameraClassId||
            selected.Properties.Count!=1111)
            throw new InvalidDataException("Stock raw Camera declaration is not its original inline1111-field Perspective.");
        var values=new (int Offset,byte[] Value)[bindings.Length];
        for(int i=0;i<bindings.Length;i++)
        {
            var b=bindings[i];var p=selected.Properties[b.Ordinal];
            if(p.Index!=b.Ordinal||p.NameHash!=b.NameHash||p.TypeCode!=b.TypeCode||p.ElementCount!=b.ElementCount||
                p.RawValue.Length!=b.ByteCount)
                throw new InvalidDataException("Raw Camera declaration differs from its actual loader CPU binding at"+b.Ordinal);
            values[i]=(b.CpuOffset,p.RawValue.ToArray());
        }
        byte[] Field(int ordinal,uint hash,int count)
        {
            var p=selected.Properties[ordinal];
            if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=8||p.ElementCount!=count||p.RawValue.Length!=count*4)
                throw new InvalidDataException("Raw Camera pose/focal declaration differs from its loader source.");
            return p.RawValue.ToArray();
        }
        return new(objectId,SourceEvidence+";"+source.ResourceUri+$"#{objectId:X8};direct-numeric-fragment/not-resource-ready",
            values,Field(0,0x27D889E5,3),Field(1,0xF52EA910,4),Field(2,0x04C1A415,1));
    }
    internal static void RequireStorage(int count)
    {if(count<NativeAllocationByteLength)throw new InvalidDataException("Raw numeric construction requires the actual4260 allocation, including the2F48..423F numeric backup.");}
}

public static class OriginalMainMapRawCameraNumericConstructionSource
{
    public static LoadedOriginalMainMapRawCameraNumericConstructionSource Load(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadRawCameraNumericConstructionSource(repository.SourceDirectory,locale);
    }
}
