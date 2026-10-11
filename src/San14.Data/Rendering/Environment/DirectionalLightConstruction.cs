using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record DirectionalLightResourceBinding(uint SourceObjectKey, ulong DataHandle,
    string OwnerProvenance, bool CpuDataReady, uint InterfaceClassId);
public sealed record DirectionalLightConstructionInputs(uint ContextFlags3E4,
    string RetainedTemplateProvenance, IReadOnlyList<DirectionalLightResourceBinding> ResourceBindings);
public sealed record DirectionalLightConstructorMetadata(uint SourceObjectId, uint Flags30,
    uint MaximumPrimitiveCount, uint SourceGroupMask, uint Value3C, uint Value40, uint Value44,
    uint ProviderObjectKey, byte SourceF20);
public sealed record DirectionalLightByteRange(int Offset,int Length);
public sealed record DirectionalLightResourceSlot(int Slot,int LightDataOffset,uint DeclaredObjectKey,
    ulong EffectiveDataHandle,uint RequiredInterfaceClassId,uint? ResolvedObjectKey);

public sealed class DirectionalLightCurrentCameraPacket
{
    private readonly byte[] bytes;
    internal DirectionalLightCurrentCameraPacket(DirectionalLightConstructionPacket source,
        CameraDirectionalLightDataPacket result)
    {Construction=source;bytes=result.Bytes.ToArray();Applied=result.Applied;}
    public DirectionalLightConstructionPacket Construction { get; }
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
    public bool Applied { get; }
}

/// <summary>Independently owned complete source-constructor and null-Camera
/// v430 result. No original process pointer or HDRI template is consumed.</summary>
public sealed class DirectionalLightConstructionPacket
{
    private readonly byte[] seed, prepared;
    private readonly DirectionalLightConstructionInputs inputs;
    internal DirectionalLightConstructionPacket(uint objectId, string sourceUri, string sourceHash,
        DirectionalLightConstructionInputs input,
        byte[] constructor, byte[] loaderPrepared, DirectionalLightConstructorMetadata metadata,
        byte[] sourceWriteMask,uint[] declaredSlotKeys,uint?[] resolvedSlotKeys)
    {
        SourceObjectId=objectId; SourceUri=sourceUri; SourceSha256=sourceHash;
        inputs=input;
        seed=constructor.ToArray();prepared=loaderPrepared.ToArray();Metadata=metadata;
        ConstructorSha256=Convert.ToHexString(SHA256.HashData(seed));
        PreparedSha256=Convert.ToHexString(SHA256.HashData(prepared));
        ConstructorWrittenRanges=DirectionalLightConstruction.Ranges(sourceWriteMask,1);
        ConstructorRetainedRanges=DirectionalLightConstruction.Ranges(sourceWriteMask,0);
        ResourceSlots=Array.AsReadOnly(declaredSlotKeys.Select((key,slot)=>new DirectionalLightResourceSlot(slot,
            0xFE0+slot*8,key,BinaryPrimitives.ReadUInt64LittleEndian(prepared.AsSpan(0xFE0+slot*8,8)),
            slot==21?0x970DB365u:0xB672FAA2u,resolvedSlotKeys[slot])).ToArray());
    }
    public uint SourceObjectId { get; }
    public string SourceUri { get; }
    public string SourceSha256 { get; }
    public string ConstructorSha256 { get; }
    public string PreparedSha256 { get; }
    public DirectionalLightConstructorMetadata Metadata { get; }
    public ReadOnlyMemory<byte> ConstructorBytes => seed.ToArray();
    public ReadOnlyMemory<byte> Bytes => prepared.ToArray();
    public uint ContextFlags3E4 => inputs.ContextFlags3E4;
    public string RetainedTemplateProvenance => inputs.RetainedTemplateProvenance;
    public bool CurrentCameraApplied => false;
    public bool ProviderSelectionApplied => false;
    public IReadOnlyList<DirectionalLightByteRange> ConstructorWrittenRanges { get; }
    public IReadOnlyList<DirectionalLightByteRange> ConstructorRetainedRanges { get; }
    public IReadOnlyList<DirectionalLightResourceSlot> ResourceSlots { get; }
    // v430 classifies1F4/1F8, but CE4A00 propagates the two retained hemi W
    // words into130.W/140.W. Current mode6 Camera overwrites the96 fog bytes.
    public IReadOnlyList<DirectionalLightByteRange> PreparedTemplateDependentRanges { get; }=
        Array.AsReadOnly(new[]{new DirectionalLightByteRange(0x10C,4),new(0x11C,4),new(0x13C,4),new(0x14C,4),
            new(0x150,96),new(0xF21,3),new(0xF2C,4),new(0x10B8,8)});
    public IReadOnlyList<DirectionalLightResourceBinding> ResourceBindings => inputs.ResourceBindings;
}

/// <summary>Project-owned Directional construction from resource properties.
/// Packed light state, resource lookup and numeric preparation are implemented
/// directly in C# and have no original executable or instruction dependency.</summary>
public static partial class DirectionalLightConstruction
{
    public const int LightDataBytes=0x10C0;

    public static DirectionalLightConstructionPacket Apply(MapSeasonDirectionalLightSourceSet source,
        uint sourceObjectId,ReadOnlySpan<byte> explicitComplete10C0,DirectionalLightConstructionInputs input)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(input);
        if(explicitComplete10C0.Length!=LightDataBytes||string.IsNullOrWhiteSpace(input.RetainedTemplateProvenance))
            throw new InvalidDataException("Directional construction needs a complete10C0 template with explicit provenance.");
        var checkedSource=MapSeasonDirectionalLightSources.Read(source.Database);
        if(!checkedSource.Roots.SelectMany(r=>r.Children.Prepend(r.Root)).Any(r=>r.ObjectId==sourceObjectId))
            throw new InvalidDataException("Directional construction needs an actual selected season root or ordered provider child.");
        var declaration=checkedSource.Database.Objects.Document.Objects.Single(o=>o.ObjectId==sourceObjectId);
        var properties=declaration.Properties.Select(p=>p with {RawValue=p.RawValue.ToArray()}).ToArray();
        var bindings=input.ResourceBindings?.ToArray()??throw new InvalidDataException("Explicit Directional resource lookup results required.");
        if(bindings.Any(b=>b is null||string.IsNullOrWhiteSpace(b.OwnerProvenance)||b.SourceObjectKey==0)||
            bindings.Select(b=>b.SourceObjectKey).Distinct().Count()!=bindings.Length)
            throw new InvalidDataException("Directional resource bindings need distinct source keys and ownership provenance.");
        foreach(var b in bindings)
            if((b.DataHandle==0&&b.CpuDataReady)||(b.DataHandle!=0&&!b.CpuDataReady))
                throw new InvalidDataException("Directional binding must explicitly identify unavailable/null or ready Data.");
        var ownedInput=input with {ResourceBindings=Array.AsReadOnly(bindings)};
        var result=ConstructOwned(properties,ownedInput,explicitComplete10C0);
        uint Word(int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(result.Cpu.AsSpan(offset,4));
        uint provider=BinaryPrimitives.ReadUInt32LittleEndian(properties[57].RawValue);
        var metadata=new DirectionalLightConstructorMetadata(sourceObjectId,Word(0x30),Word(0x34),Word(0x38),
            Word(0x3C),Word(0x40),Word(0x44),provider,result.Prepared[0xF20]);
        return new(sourceObjectId,checkedSource.Source.ResourceUri,checkedSource.Source.DecodedSha256,
            ownedInput,result.Constructor,result.Prepared,metadata,result.SeedWriteMask,result.DeclaredSlotKeys,result.ResolvedSlotKeys);
    }

    public static DirectionalLightCurrentCameraPacket ApplyCurrentCamera(DirectionalLightConstructionPacket construction,
        ReadOnlySpan<byte> explicitPreparedCameraPrefix)
    {
        ArgumentNullException.ThrowIfNull(construction);
        return new(construction,
            PrepareCamera(construction,explicitPreparedCameraPrefix));
    }
    internal static CameraDirectionalLightDataPacket PrepareCamera(DirectionalLightConstructionPacket construction,
        ReadOnlySpan<byte> camera)
    {
        byte[] light=construction.Bytes.ToArray();
        if(light[0x10B0]==2)
        {
            // CBC770 is a LightData consumer, independent of the originating
            // actor class. Its mode2/Camera3B8=1 branch writes onlyF1E=2.
            var result=MapSourceCreatedCameraLightData.ApplySourceMode2PreparedCamera(light,camera,construction.ContextFlags3E4);
            return new(result.Bytes,result.ContextApplied,false);
        }
        return CameraDirectionalLightData.Apply(light,camera,construction.ContextFlags3E4);
    }

    public static IReadOnlyList<uint> RequiredResourceKeys(MapSeasonDirectionalLightSourceSet source,uint sourceObjectId,uint contextFlags3E4)
        => RequiredResourceKeys(source,sourceObjectId,contextFlags3E4,[]);

    /// <summary>Enumerate conditional original lookups with the supplied known
    /// ready/unavailable results. Unspecified requests are unavailable only for
    /// this metadata pass. The no-results overload therefore includes fallback
    /// requests and does not describe a ready GPU binding generation.</summary>
    public static IReadOnlyList<uint> RequiredResourceKeys(MapSeasonDirectionalLightSourceSet source,uint sourceObjectId,uint contextFlags3E4,
        IReadOnlyList<DirectionalLightResourceBinding> knownLookupResults)
    {
        ArgumentNullException.ThrowIfNull(source);
        var checkedSource=MapSeasonDirectionalLightSources.Read(source.Database);
        var obj=checkedSource.Database.Objects.Document.Objects.Single(o=>o.ObjectId==sourceObjectId&&o.ClassId==DirectionalLightSource.SourceClass);
        var properties=obj.Properties.Select(p=>p with {RawValue=p.RawValue.ToArray()}).ToArray();
        byte[][] fields=ReadFields(properties);
        return ResolveResources(fields,DeclaredKeys(fields),knownLookupResults,true,null);
    }

    internal static IReadOnlyList<DirectionalLightByteRange> Ranges(byte[] mask,byte value)
    {
        var rows=new List<DirectionalLightByteRange>();int i=0;
        while(i<mask.Length){if(mask[i]!=value){i++;continue;}int start=i;while(i<mask.Length&&mask[i]==value)i++;rows.Add(new(start,i-start));}
        return rows.AsReadOnly();
    }

    internal sealed record ConstructionResult(byte[] Constructor,byte[] Prepared,byte[] Cpu,byte[] SeedWriteMask,uint[] DeclaredSlotKeys,uint?[] ResolvedSlotKeys);
}
