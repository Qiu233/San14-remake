using System.Buffers.Binary;

namespace San14.Data;

public sealed record MapPostEffect3ApplyInitialCommonPass(int CachedIndex,string Name,
    uint NameHash,IReadOnlyList<int> ActualSelectedPassIndices);

/// <summary> fresh standard/Common handler fields, independently
/// distinct from RTScalar state. Its virtual10 returns1 and writes
/// no scalar values. Sparse unknown self bytes remain unsupported.</summary>
public sealed class MapPostEffect3ApplyInitialCommon
{
    private readonly Dictionary<int,byte> knownBytes;
    private MapPostEffect3ApplyInitialCommon(long frame,string owner,
        Dictionary<int,byte> bytes,IReadOnlyList<MapPostEffect3ApplyInitialCommonPass> passes)
    {FrameNumber=frame;OwnerIdentity=owner;knownBytes=bytes;Passes=passes;}
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}



    public IReadOnlyList<MapPostEffect3ApplyInitialCommonPass> Passes{get;}
    public string SourceEvidence=>"TC;VT;=AL1-no-writes"+OwnerIdentity;

    /// <summary>Only actual Functor0 is currently admitted for these Common
    /// source pass-name identities. This does not select it in the Apply ABI.</summary>
    public static MapPostEffect3ApplyInitialCommon ConstructFresh(
        MapPostEffect3Container module,long frame,string owner)
    {
        ArgumentNullException.ThrowIfNull(module);ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        MapPostEffect3ParameterState.ValidateSource(module);
        if(frame<=0)throw new InvalidDataException("Common handler requires an owned frame.");
        var actual=module.ReadFunctorPasses(0);var passes=new List<MapPostEffect3ApplyInitialCommonPass>();
        foreach(var d in Definitions)
        {
            int[] matching=actual.Where(p=>p.NameHash==d.Hash).Select(p=>p.Index).ToArray();
            if(matching.Length!=1)throw new InvalidDataException("Common handler pass hash is not unique in the actual selected source functor.");
            passes.Add(new(passes.Count,d.Name,d.Hash,Array.AsReadOnly(matching)));
        }
        var bytes=new Dictionary<int,byte>();
        void B(int at,byte value)=>bytes.Add(at,value);
        void W(int at,uint value)
        {byte[] word=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(word,value);for(int i=0;i<4;i++)B(at+i,word[i]);}
        // Eight frozen literal vectors at.., repeated twice.
        for(int matrix=0;matrix<2;matrix++)for(int lane=0;lane<16;lane++)
            W(0x60+matrix*64+lane*4,lane%5==0?0x3F800000u:0u);
        W(0xE0,0x3F800000);W(0xE4,0x3F800000);B(0xE8,1);W(0xEC,0);
        W(0xF0,0x01010101);B(0xF4,1);
        return new(frame,owner,bytes,Array.AsReadOnly(passes.ToArray()));
    }
    public ReadOnlyMemory<byte> ReadKnownSelfBytes(int offset,int count)
    {
        if(offset<0||count<=0||offset>int.MaxValue-count)throw new ArgumentOutOfRangeException(nameof(offset));
        byte[] result=new byte[count];
        for(int i=0;i<count;i++)result[i]=knownBytes.TryGetValue(offset+i,out byte b)?b:
            throw new NotSupportedException($"Common fresh self byte {offset+i:X} has no admitted constructor writer.");
        return result;
    }
    public MapPostEffect3ApplyInitialPlan ApplyNoOpInitializer(MapPostEffect3ParameterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(state.FrameNumber!=FrameNumber||state.SourceSha256!=MapFinalOutputPostEffect3Source.PayloadSha256)
            throw new InvalidDataException("Common no-op initializer belongs to a different scalar bank frame/source.");
        // body is mov al,1;ret. Never initialize/clear the bank here.
        return new(FrameNumber,OwnerIdentity,
            Array.Empty<MapPostEffect3ApplyInitialWrite>(),Array.Empty<string>(),true,null,
            SourceEvidence+";owned-state="+state.OwnerIdentity);
    }
    private readonly record struct Definition(string Name,uint Hash);
    private static readonly Definition[] Definitions=
    [
        new("GenerateLightShaftMaskedLightScene",0x165B0921u),
        new("LightShaftGaussBlur",0x6D73514Eu),
        new("LightShaftRadialBlur2",0xBD9A7584u),
        new("LightShaftHorizontalBlur",0x60DDF8FBu),
        new("LightShaftVerticalBlur",0x9F7EDFCDu),
        new("LightShaftRadialBlur1",0x3714BAE5u),
        new("CompositLightShaft",0x187744E5u),
        new("MakeDofSampling",0xD3A928B9u),
        new("PrepareGatherDof",0xFB832229u),
        new("MakeCoCNeighborTile",0x5379E924u),
        new("PrefilterGatherDof",0xEFD61CD5u),
        new("DrawGatherDof1st",0x10A2985Au),
        new("DrawGatherDof2nd",0xF806CD30u),
        new("CompositeGatherDofCS",0xF37CD90Cu),
        new("MakeVelocityMap",0x024DA01Cu),
        new("MakeMbTileMax",0x0C7E6CDAu),
        new("MakeMbNeighborCS",0x9A7B7352u),
        new("ReconstructMotionBlurCS1",0x81FD0072u),
        new("ReconstructMotionBlurCS2",0x062D4D73u),
        new("DownLumi0",0x8DE616DBu),
        new("DownLumi1",0x822F87FAu),
        new("DownLumi2",0x7678F919u),
        new("DownLumi3",0x6AC26A38u),
        new("MakeBloom",0x1F15F818u),
        new("MakeStopImage",0x8022AA22u),
        new("FFT1d1",0xCD8FB3F0u),
        new("FFT1d2",0x0275EF31u),
        new("MakeSingleStarBurst",0x6F1BC7F5u),
        new("MakeSpectrumStarBurst",0xE19E187Eu),
        new("ComputeGhostInfo",0x14A05749u),
        new("DrawIndirectGhost",0x78904907u),
        new("ClearGhostImage",0x9FFE1CCAu),
        new("MakeHalo",0xA885CF37u),
        new("GhostSprite",0x1EF3A533u),
        new("UpScaleAdd2",0xB430C7B5u),
        new("PostGaussBlurCS2",0xFD1505F0u),
        new("UpScaleAdd1",0xAC7F2256u),
        new("PostGaussBlurCS1",0xAC6B27EFu),
        new("UpScaleAdd0",0xA4CD7CF7u),
        new("PostGaussBlurCS0",0x5BC149EEu),
        new("CalcToneScale",0x14E93CE0u),
        new("AnamorphicBlur",0xA02526B6u),
        new("AddAnamorphic",0x88B116F2u),
        new("Composite",0xB28B5E3Cu),
    ];
}
