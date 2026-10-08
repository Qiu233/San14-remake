using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Typed target resolved by the actual native key/alias lookup. Null
/// in the lookup map means explicitly absent; a missing map key is unknown.</summary>
public sealed record OriginalMapPostEffect3ApplyInitialTarget(long FrameNumber,
    string OwnerIdentity,uint NativeType,int ElementCount,int ByteLength);

public sealed class OriginalMapPostEffect3ApplyInitialDefault
{
    private readonly byte[] value;
    internal OriginalMapPostEffect3ApplyInitialDefault(string name,uint hash,uint type,
        int count,int cachedOffset,string valueHex,string writerEvidence)
    {Name=name;PrimaryHash=hash;NativeType=type;ElementCount=count;CachedControllerOffset=cachedOffset;
     value=Convert.FromHexString(valueHex);SourceEvidence=writerEvidence;}
    public string Name{get;}
    public uint PrimaryHash{get;}
    public uint NativeType{get;}
    public int ElementCount{get;}
    public int CachedControllerOffset{get;}
    public ReadOnlyMemory<byte> Value=>value;
    public string SourceEvidence{get;}
}

public sealed record OriginalMapPostEffect3ApplyInitialWrite(
    OriginalMapPostEffect3ApplyInitialDefault Definition,OriginalMapPostEffect3ApplyInitialTarget Target);

public sealed record OriginalMapPostEffect3ApplyInitialPlan(long FrameNumber,string OwnerIdentity,
    uint HandlerVtableRva,uint InitializerRva,
    IReadOnlyList<OriginalMapPostEffect3ApplyInitialWrite> Writes,
    IReadOnlyList<string> ExplicitlyAbsentNames,bool InitializerAllLookupsAccepted,
    int? ExposureCapability290,string SourceEvidence)
{
    public bool IsCompleteModuleState=>false;
}

/// <summary>Exact Lens and Exposure handler default tables, independently
/// recovered from TC114B710 and11583E0. Other scalar records remain unknown.
/// This does not globally zero an RTScalar bank or prove an original frame.</summary>
public static class OriginalMapPostEffect3ApplyInitialScalars
{
    public const string NativeRevision="0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464";
    public static IReadOnlyList<OriginalMapPostEffect3ApplyInitialDefault> LensDefaults=>Array.AsReadOnly(Lens);
    public static IReadOnlyList<OriginalMapPostEffect3ApplyInitialDefault> ExposureDefaults=>Array.AsReadOnly(Exposure);

    public static OriginalMapPostEffect3ApplyInitialPlan PrepareLens(
        OriginalMapPostEffect3Container actualModule,long frame,string owner,
        IReadOnlyDictionary<string,OriginalMapPostEffect3ApplyInitialTarget?> actualResolvedLookups,string lookupEvidence,string locale="TC")
        =>Prepare(actualModule,frame,owner,actualResolvedLookups,lookupEvidence,Lens,0x173A590,0x114B710,locale);
    public static OriginalMapPostEffect3ApplyInitialPlan PrepareExposure(
        OriginalMapPostEffect3Container actualModule,long frame,string owner,
        IReadOnlyDictionary<string,OriginalMapPostEffect3ApplyInitialTarget?> actualResolvedLookups,string lookupEvidence,string locale="TC")
        =>Prepare(actualModule,frame,owner,actualResolvedLookups,lookupEvidence,Exposure,0x173BB68,0x11583E0,locale);

    public static OriginalMapPostEffect3ApplyInitialPlan InitializeLens(
        OriginalMapPostEffect3Container actualModule,OriginalMapPostEffect3ParameterState actualState,string handlerOwner,string locale="TC")
        =>Initialize(actualModule,actualState,handlerOwner,Lens,false,locale);
    public static OriginalMapPostEffect3ApplyInitialPlan InitializeExposure(
        OriginalMapPostEffect3Container actualModule,OriginalMapPostEffect3ParameterState actualState,string handlerOwner,string locale="TC")
        =>Initialize(actualModule,actualState,handlerOwner,Exposure,true,locale);

    private static OriginalMapPostEffect3ApplyInitialPlan Initialize(OriginalMapPostEffect3Container module,
        OriginalMapPostEffect3ParameterState state,string owner,
        OriginalMapPostEffect3ApplyInitialDefault[] definitions,bool exposure,string locale)
    {
        ArgumentNullException.ThrowIfNull(module);ArgumentNullException.ThrowIfNull(state);
        if(state.SourceSha256!=module.Sha256)throw new InvalidDataException("Initializer State and module sources differ.");
        var lookups=new Dictionary<string,OriginalMapPostEffect3ApplyInitialTarget?>();
        foreach(var d in definitions)
        {
            int? index=state.FindScalarIndex(d.PrimaryHash);
            if(index is not int i){lookups.Add(d.Name,null);continue;}
            var target=state.Declarations[i];
            lookups.Add(d.Name,new(state.FrameNumber,state.OwnerIdentity+"/scalar/"+i,
                target.NativeValueType,target.ElementCount,target.ValueByteCount));
        }
        string evidence="actual-owned-RTScalar="+state.OwnerIdentity+";primary-lookup;typed-default-validation";
        var plan=exposure?PrepareExposure(module,state.FrameNumber,owner,lookups,evidence,locale):
            PrepareLens(module,state.FrameNumber,owner,lookups,evidence,locale);
        foreach(var w in plan.Writes)
            if(!state.SetTypedByHash(w.Definition.PrimaryHash,w.Definition.NativeType,w.Definition.ElementCount,
                w.Definition.Value.Span,w.Definition.SourceEvidence+";"+plan.SourceEvidence))
                throw new InvalidDataException("Validated initializer State target changed before its default write.");
        return plan;
    }

    private static OriginalMapPostEffect3ApplyInitialPlan Prepare(OriginalMapPostEffect3Container module,
        long frame,string owner,IReadOnlyDictionary<string,OriginalMapPostEffect3ApplyInitialTarget?> lookups,
        string evidence,OriginalMapPostEffect3ApplyInitialDefault[] defaults,uint vtable,uint initializer,string locale)
    {
        ArgumentNullException.ThrowIfNull(module);ArgumentNullException.ThrowIfNull(lookups);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        if(frame<=0||module.Sha256!=OriginalMapFinalOutputPostEffect3Source.PayloadSha256||
            Convert.ToHexString(SHA256.HashData(module.Bytes.Span))!=module.Sha256)
            throw new InvalidDataException("Initial defaults require the actual pinned module and owned frame.");
        var writes=new List<OriginalMapPostEffect3ApplyInitialWrite>();var absent=new List<string>();
        int? capability=null;
        for(int i=0;i<defaults.Length;i++)
        {
            var definition=defaults[i];
            if(!lookups.TryGetValue(definition.Name,out var target))
                throw new InvalidDataException("Initializer lookup is unknown: "+definition.Name);
            if(target is null)absent.Add(definition.Name);
            else
            {
                if(target.FrameNumber!=frame||string.IsNullOrWhiteSpace(target.OwnerIdentity)||
                    target.NativeType!=definition.NativeType||target.ElementCount!=definition.ElementCount||
                    target.ByteLength!=definition.Value.Length)
                    throw new InvalidDataException("Initializer target differs from its native type/count/value extent.");
                writes.Add(new(definition,target));
            }
            //1158D19..1158D5B updates capability in actual lookup order. A
            //present final optional field sets4; an absent one sets1/2/3.
            if(initializer==0x11583E0&&i>=17)capability=target is not null?4:i-16;
        }
        const string scope="OriginalMapPostEffect3ApplyInitialScalars.cs";
        uint actualVtable=(uint)OriginalMapPostEffect3NativeSourceProfile.DataRva(locale,scope,(int)vtable);
        uint actualInitializer=(uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(locale,scope,(int)initializer);
        return new(frame,owner,actualVtable,actualInitializer,Array.AsReadOnly(writes.ToArray()),
            Array.AsReadOnly(absent.ToArray()),absent.Count==0,capability,
            $"{locale}:{actualInitializer:X};handlerVT={actualVtable:X};paired-original-default-table-TC-reference={initializer:X};"+evidence);
    }
    private static readonly OriginalMapPostEffect3ApplyInitialDefault[] Lens=
    [
        new("StarBurstSampleScale",0x72A90CA5u,1u,1,0xF8,"D3D1043E","TC:0114B710;0114B759"),
        new("StopBladeCount",0x84A28F6Cu,6u,1,0x100,"06000000","TC:0114B710;0114B8B6"),
        new("StopPolyRate",0xA66BBAE1u,1u,1,0x108,"476E6F3F","TC:0114B710;0114B87F"),
        new("ScreenHeight",0x710D5F66u,1u,1,0x110,"00008744","TC:0114B710;0114B893"),
        new("EnableAnamorphic",0x3C8B6516u,6u,1,0x118,"00000000","TC:0114B710;0114B8EE"),
        new("HaloAberrationWavesRateRB",0xAB12372Bu,1u,2,0x120,"48E17A3F5C8F823F","TC:0114B710;0114BA30,0114BAB1"),
        new("LensFlareProjPosFovScale",0xF866D4B0u,1u,4,0x128,"00000000000000000000000000000000","TC:0114B710;0114BA80"),
        new("LensFlareIntensity",0xD469F14Fu,1u,1,0x130,"00000000","TC:0114B710;0114BAE1"),
        new("GhostSpriteNum",0x47A903D9u,6u,1,0x138,"00000000","TC:0114B710;0114BB00"),
        new("FlareScreenWhAspect",0xC57FE4C3u,1u,1,0x140,"398EE33F","TC:0114B710;0114BA98"),
        new("LensHaloInfo0",0x21B018A4u,4u,1,0x148,"00000000000000000000000000000000","TC:0114B710;0114BB2F"),
        new("AnamorphicCoatColor",0xC514858Bu,3u,1,0x150,"000000000000000000000000","TC:0114B710;0114BCE5,0114BCF0"),
        new("ChangeFStop",0xACBFA86Bu,6u,1,0x158,"00000000","TC:0114B710;0114BD0B"),
        new("EnableExternalStarBurst",0x9991F7C5u,6u,1,0x160,"00000000","TC:0114B710;0114BD26"),
        new("DiaphragmType",0x414B6021u,6u,1,0x168,"00000000","TC:0114B710;0114BD41"),
        new("Enable4KResFix",0x93B076F4u,6u,1,0x170,"00000000","TC:0114B710;0114BD5C"),
    ];
    private static readonly OriginalMapPostEffect3ApplyInitialDefault[] Exposure=
    [
        new("DeltaTime",0x251F13DFu,1u,1,0x1F0,"8988883C","TC:011583E0;01158434"),
        new("AdjustMiddleGray",0x016F30A8u,1u,1,0x1F8,"7FDC5E3E","TC:011583E0;01158518"),
        new("EvRangeMinMax",0x5AAFE047u,1u,2,0x200,"000070C100003041","TC:011583E0;01158563,01158568"),
        new("AdaptedScaleLight",0x0E9A1734u,1u,1,0x208,"0000003F","TC:011583E0;011585A7"),
        new("AdaptedScaleDark",0xC1F824C0u,1u,1,0x210,"0000003F","TC:011583E0;011585C2"),
        new("LuminanceRangeMinMax",0x8048B9A3u,1u,2,0x218,"FEFFFF3800008044","TC:011583E0;011586CA,01158738"),
        new("OmittedLuminancePercentsLowHigh",0x4A5EE495u,1u,2,0x220,"0000A0410000A042","TC:011583E0;0115871F,01158797"),
        new("ScreenWidth",0x8F1FA839u,1u,1,0x228,"0000F044","TC:011583E0;01158748"),
        new("ScreenHeight",0x710D5F66u,1u,1,0x230,"00008744","TC:011583E0;0115879F"),
        new("EnableAutoExposure",0x8767AFDEu,6u,1,0x238,"01000000","TC:011583E0;0115880B"),
        new("UserExposureScale",0x5CE025BDu,1u,1,0x240,"6F12033A","TC:011583E0;01158956"),
        new("BloomThresholdCurveParams",0xB346E535u,1u,2,0x248,"412ABF3F0000003F","TC:011583E0;011589A5,011589EE"),
        new("BloomRadius",0xAC8F38CDu,1u,1,0x250,"00008040","TC:011583E0;011589BE"),
        new("UseBloomAntiFlicker",0xFDDBFB5Bu,6u,1,0x258,"00000000","TC:011583E0;01158A20"),
        new("EnableBloom",0xB344DE25u,6u,1,0x260,"00000000","TC:011583E0;01158A3B"),
        new("EnableDebugBloomSoftKneeCurve",0x40C4A4F6u,6u,1,0x268,"00000000","TC:011583E0;01158BB9"),
        new("EnableDebugHistgram",0x33E0E01Cu,6u,1,0x270,"00000000","TC:011583E0;01158BD4"),
        new("Viewport",0x26F729DCu,4u,1,0x278,"00000000000000000000803F0000803F","TC:011583E0;01158BE5,01158BF0,01158BF7,01158C01"),
        new("Enable4KCBFix",0x46F33989u,6u,1,0x280,"00000000","TC:011583E0;01158C1F"),
        new("EnableBloomColorFix",0x5FD14A93u,6u,1,0x288,"00000000","TC:011583E0;01158C3A"),
    ];
}
