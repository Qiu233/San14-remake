using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Actual source frontend fields for one of its eight slots.
/// FactorWord is F4+4*slot; RGB and Alpha are separate 1C8/1D0 bytes.</summary>
public readonly record struct OriginalModelFrontendColorSlot(bool BlendEnabled,
    byte RgbWriteMask,byte AlphaWriteEnabled,uint FactorWord)
{
    public byte RgbaWriteMask=>(byte)((RgbWriteMask&7)|((AlphaWriteEnabled&1)<<3));
}

/// <summary>Retained source state, independent of the currently bound target
/// count. A target push/pop does not construct another color state.</summary>
public sealed class OriginalModelFrontendColorState
{
    internal OriginalModelFrontendColorState(OriginalModelFrontendColorSlot[] slots)
        =>Slots=Array.AsReadOnly(slots);
    public IReadOnlyList<OriginalModelFrontendColorSlot> Slots{get;}
}

/// <summary>Actual D3D enum values read from the source factor/op tables.
/// Native PS output absence is separate from this frontend write mask.</summary>
public readonly record struct OriginalModelNativeColorAttachment(bool BlendEnabled,
    uint SourceColor,uint DestinationColor,uint ColorOperation,
    uint SourceAlpha,uint DestinationAlpha,uint AlphaOperation,byte RgbaWriteMask,
    uint FactorWord);

/// <summary>Current T240 Aux masks. FFFFFFFF means retain the dispatcher entry
/// mask, not an actual per-record write of all bits.</summary>
public readonly record struct OriginalModelAuxiliaryColorMasks(uint C8,uint CC,uint D0,uint D4)
{
    internal uint At(int index)=>index switch{0=>C8,1=>CC,2=>D0,3=>D4,_=>uint.MaxValue};
}

public sealed record OriginalModelLateRecordColorTransition(int SourceMaskIndex,
    uint SourceMask,bool HasRecordMaskWriter,OriginalModelFrontendColorState DrawState,
    OriginalModelFrontendColorState ExitState);

/// <summary>Only the effective ordinary disabled-blend color state. No
/// factor word or unbound-slot history is supplied or inferred here.</summary>
public sealed class OriginalModelOrdinaryActiveColorState
{
    internal OriginalModelOrdinaryActiveColorState(byte[] masks)=>RgbaWriteMasks=Array.AsReadOnly(masks);
    public bool BlendEnabled=>false;
    public IReadOnlyList<byte> RgbaWriteMasks{get;}
}

public sealed record OriginalModelLateRecordActiveColorTransition(int SourceMaskIndex,
    uint SourceMask,bool HasRecordMaskWriter,OriginalModelOrdinaryActiveColorState DrawState,
    OriginalModelOrdinaryActiveColorState ExitState);

/// <summary>Load-once common frontend constructor, D712F0 writer and actual
/// ordinary dispatcher color semantics. Callers retain the actual state
/// through Sky/graph/current writers; no frame number selects a recipe.</summary>
public sealed class LoadedOriginalModelFrontendColorSource
{
    internal LoadedOriginalModelFrontendColorSource(string locale,string executable,
        uint initialFactor,byte rgb,byte alpha,uint[] color,uint[] alphaTable,uint[] ops)
    {Locale=locale;ExecutableSha256=executable;this.initialFactor=initialFactor;initialRgb=rgb;initialAlpha=alpha;
        colorFactors=color;alphaFactors=alphaTable;operations=ops;}
    private readonly uint initialFactor;
    private readonly byte initialRgb,initialAlpha;
    private readonly uint[] colorFactors,alphaFactors,operations;
    public string Locale{get;}
    public string ExecutableSha256{get;}
    public string SourceProvenance=>$"SAN14CE_{Locale}.exe@{ExecutableSha256};"+
        "frontend966EA0-eightSlots;D712F0-currentBoundSlots/enable/RGB/alpha/conditionalF4-broadcast;"+
        "D43190-ordinary-entry/D48709-AuxC8..D4/D48742-currentMask/D48CB8-exitMask;"+
        "D223C0-loaded-FFFFFFFF/D1E410-current-copy-and-overlay;not-GPU-completion";

    /// <summary>Once at actual frontend creation. Normal requests continue
    /// their current state rather than calling this to reset it.</summary>
    public OriginalModelFrontendColorState CreateFreshFrontendState()=>new(
        Enumerable.Range(0,8).Select(_=>new OriginalModelFrontendColorSlot(false,initialRgb,initialAlpha,initialFactor)).ToArray());

    /// <summary>For a real caller's already retained/observed eight slots.</summary>
    public OriginalModelFrontendColorState ReadCurrentFrontend(IReadOnlyList<OriginalModelFrontendColorSlot> actualSlots)
    {
        ArgumentNullException.ThrowIfNull(actualSlots);
        if(actualSlots.Count!=8)throw new InvalidDataException("Native color frontend has exactly eight retained slots.");
        return new(actualSlots.ToArray());
    }

    /// <summary>Exact D712F0. Disabled blending retains all factor words.
    /// An enabled call broadcasts only when actual RT0 F4 differs; equal
    /// RT0 does not overwrite heterogeneous retained later-slot factors.</summary>
    public OriginalModelFrontendColorState ApplyNativeFlags(OriginalModelFrontendColorState actualPrevious,
        int actualBoundColorCount,ulong actualFlags)
    {
        ArgumentNullException.ThrowIfNull(actualPrevious);RequireCount(actualBoundColorCount);
        if(actualBoundColorCount==0)return actualPrevious;
        var slots=actualPrevious.Slots.ToArray();bool anyBlend=false;
        for(int slot=0;slot<actualBoundColorCount;slot++)
        {
            bool enabled=(actualFlags&(1UL<<(slot+8)))!=0;
            byte write=(actualFlags&(1UL<<(slot+56)))!=0?(byte)1:(byte)0;
            slots[slot]=slots[slot] with{BlendEnabled=enabled,RgbWriteMask=(byte)(write*7),AlphaWriteEnabled=write};
            anyBlend|=enabled;
        }
        if(anyBlend)
        {
            uint factor=(byte)(actualFlags>>44);
            factor=(factor<<4)|(byte)(actualFlags>>24);
            factor=(factor<<4)|(byte)(actualFlags>>36);
            factor=(factor<<4)|(byte)(actualFlags>>16);
            factor=(factor<<4)|(uint)((actualFlags>>52)&15);
            factor=(factor<<4)|(uint)((actualFlags>>32)&15);
            if(slots[0].FactorWord!=factor)
                for(int slot=0;slot<slots.Length;slot++)slots[slot]=slots[slot] with{FactorWord=factor};
        }
        return new(slots);
    }

    /// <summary>RenderType0, ordinary Flag.bit0-clear queue49/50/54/55/58
    /// entry at D436BB..D436DA. It writes all actually bound slots without
    /// inventing another constructor or replacing their retained factors.</summary>
    public OriginalModelFrontendColorState PrepareOrdinaryQueueEntry(OriginalModelQueueStateSource actualQueue,
        OriginalModelFrontendColorState actualPrevious,int actualBoundColorCount)
    {
        RequireOrdinaryQueue(actualQueue);
        return ApplyNativeFlags(actualPrevious,actualBoundColorCount,(ulong)BoundMask(actualBoundColorCount)<<56);
    }

    /// <summary>The same ordinary entry projected onto its effective fields:
    /// bound slots have blending disabled and RGBA15 even for an empty queue.
    /// Inactive factor words remain outside this projection.</summary>
    public OriginalModelOrdinaryActiveColorState PrepareOrdinaryQueueActiveColorEntry(
        OriginalModelQueueStateSource actualQueue,int actualBoundColorCount)
    {
        RequireOrdinaryQueue(actualQueue);RequireCount(actualBoundColorCount);
        return new(Enumerable.Repeat((byte)15,actualBoundColorCount).ToArray());
    }

    /// <summary>Capture the four actual current words once per selected Aux.
    /// The finite source-created no-callback/no-overlay scope has their
    /// loaded FFFFFFFF values. Explicit current values come from its full Aux.</summary>
    public OriginalModelAuxiliaryColorMasks ReadCurrentAuxiliaryMasks(OriginalMapFreshCommonResourceSnapshot actualCommon)
    {
        ArgumentNullException.ThrowIfNull(actualCommon);
        if(!actualCommon.CommonViewResource240Present)
            throw new NotSupportedException("Ordinary per-record color selection needs its actual current T240 Aux.");
        if(actualCommon.Scope==OriginalMapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication)
            return new(uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue);
        byte[] aux=actualCommon.CopyCurrentAuxiliary460()??throw new InvalidDataException("Explicit current Aux bytes are absent.");
        uint Word(int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(aux.AsSpan(offset,4));
        return new(Word(0xC8),Word(0xCC),Word(0xD0),Word(0xD4));
    }

    /// <summary>D486E4..D48742 selects the mask; FFFFFFFF/index>=4 retains
    /// the actual current state. A real override writes its AND-bound mask,
    /// then D48CB8 restores the bound mask at this record's exit.</summary>
    public OriginalModelLateRecordColorTransition PrepareLateRecord(OriginalModelFrontendColorState actualEntry,
        int actualBoundColorCount,uint actualRecordMask78,OriginalModelAuxiliaryColorMasks actualAuxMasks)
    {
        ArgumentNullException.ThrowIfNull(actualEntry);uint bound=BoundMask(actualBoundColorCount);
        var(index,mask)=SelectLateRecordMask(actualRecordMask78,actualAuxMasks);
        if(mask==uint.MaxValue)return new(index,mask,false,actualEntry,actualEntry);
        var draw=ApplyNativeFlags(actualEntry,actualBoundColorCount,(ulong)(mask&bound)<<56);
        return new(index,mask,true,draw,ApplyNativeFlags(draw,actualBoundColorCount,(ulong)bound<<56));
    }

    /// <summary>Uses the original record selector without constructing
    /// inactive blend factors. A real override writes selected slots and
    /// restores the ordinary bound mask on exit.</summary>
    public OriginalModelLateRecordActiveColorTransition PrepareLateRecordActiveColor(
        OriginalModelOrdinaryActiveColorState actualEntry,uint actualRecordMask78,
        OriginalModelAuxiliaryColorMasks actualAuxMasks)
    {
        ArgumentNullException.ThrowIfNull(actualEntry);
        int count=actualEntry.RgbaWriteMasks.Count;RequireCount(count);
        var(index,mask)=SelectLateRecordMask(actualRecordMask78,actualAuxMasks);
        if(mask==uint.MaxValue)return new(index,mask,false,actualEntry,actualEntry);
        var draw=new OriginalModelOrdinaryActiveColorState(Enumerable.Range(0,count)
            .Select(slot=>(mask&(1u<<slot))!=0?(byte)15:(byte)0).ToArray());
        var exit=new OriginalModelOrdinaryActiveColorState(Enumerable.Repeat((byte)15,count).ToArray());
        return new(index,mask,true,draw,exit);
    }

    public IReadOnlyList<OriginalModelNativeColorAttachment> ReadDrawAttachments(OriginalModelFrontendColorState actualState,
        int actualBoundColorCount)
    {
        ArgumentNullException.ThrowIfNull(actualState);RequireCount(actualBoundColorCount);
        var result=new OriginalModelNativeColorAttachment[actualBoundColorCount];
        for(int slot=0;slot<result.Length;slot++)
        {
            var current=actualState.Slots[slot];uint word=current.FactorWord;
            uint Lookup(uint[] table,uint index)
            {
                if(index>=table.Length||table[index]==0)throw new NotSupportedException("Current native factor/op is outside its actual supported source enum table.");
                return table[index];
            }
            result[slot]=new(current.BlendEnabled,Lookup(colorFactors,(word>>8)&15),Lookup(colorFactors,(word>>16)&15),
                Lookup(operations,word&15),Lookup(alphaFactors,(word>>12)&15),Lookup(alphaFactors,(word>>20)&15),
                Lookup(operations,(word>>4)&15),current.RgbaWriteMask,word);
        }
        return Array.AsReadOnly(result);
    }
    private static uint BoundMask(int count){RequireCount(count);return(1u<<count)-1;}
    private static (int Index,uint Mask) SelectLateRecordMask(uint recordMask,OriginalModelAuxiliaryColorMasks masks)
    {
        int index=checked((int)(3*((recordMask>>2)&1)+4*((recordMask>>3)&1)+(recordMask&3)))-1;
        return(index,masks.At(index));
    }
    private static void RequireOrdinaryQueue(OriginalModelQueueStateSource queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        if(queue.Node.Index is not(49 or 50 or 54 or 55 or 58)||queue.RenderType!=0||(queue.Flag&1)!=0)
            throw new NotSupportedException("Ordinary color entry requires its current original late queue profile.");
    }
    private static void RequireCount(int count)
    {if(count is <0 or >8)throw new ArgumentOutOfRangeException(nameof(count),"Native frontend supports zero through eight bound colors.");}
}

public static class OriginalModelFrontendColorSource
{
    public static LoadedOriginalModelFrontendColorSource Load(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadModelFrontendColorSource(repository.SourceDirectory,locale);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalModelFrontendColorSource LoadModelFrontendColorSource(string directory,string locale)
    {
        var profile=Profile(locale);bool tc=locale=="TC";
        byte[] exe=File.ReadAllBytes(Path.Combine(directory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"Model color source executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,profile.TextHash,"Model color source decoded text");
        void Check(int tcRva,int scRva,int size,string tcHash,string scHash,string name)
            =>VerifyHash(text.AsSpan((tc?tcRva:scRva)-start,size),tc?tcHash:scHash,name);
        Check(0x95DC10,0x95ECC0,1220,"A072EF4A3E3E0A814D48B99FA052561066929C9898277595C0C86BED9E8AF7A7",
            "4CA5B91FA5A658D32875107CA149593A5173BC976095CD7FDE0E859DA6296342","Common frontend constructor");
        Check(0x966EA0,0x968020,730,"96CF43A335C06277B910CA515C71D79FD79311D253EFBD351A227C2EE1BA2C9E",
            "96CF43A335C06277B910CA515C71D79FD79311D253EFBD351A227C2EE1BA2C9E","Eight-slot native state constructor");
        Check(0xD712F0,0xD720F0,527,"8ED195AEB40A5A22887F1130F770628E34D6F9B5A06AC5A9D805EF755345084A",
            "E7A87DFAAB492CB37D5E0FFDEA8CCA3C663974D5755230603E66CCF784122C9A","Current blend/write-mask writer");
        Check(0x94AF20,0x94BFF0,816,"829C6848FFFE0E90CB41F745A68433CE3874808DEA0833070834CBDBDCEE88CB",
            "31377E7521E83D47FECE2A426CC211421508A36E04ABA113F7EADE8427B5DFCB","Actual native blend descriptor creator");
        Check(0xD223C0,0xD230F0,4119,"BDC9FB792C97082D61388427ED00F53EEF05FEACD18055B7EB313892B6B3F3E4",
            "B680E06461F0AE39C0315D7D8E1ED4D64BE1089FF64B11B4B62C66DD8342EDF6","Loaded Common per-record masks");
        Check(0xD1E410,0xD1F140,3293,"6BCDC7C8BBAEAE375183BFCCF8C4B3634DFB00BBAF7A3D1BD782D93963C270B3",
            "6BCDC7C8BBAEAE375183BFCCF8C4B3634DFB00BBAF7A3D1BD782D93963C270B3","Current Aux copy/overlay");
        Check(0xD43190,0xD43EC0,23680,"343F81F8CDED4A7E61F0A06A5C607F020DC9A64821E1F597E245C6BC2F8BDF64",
            "6C98D5E3A7D6B1213B1DA767C730C7CC4439CCCB844839C79C22227CA83917D0","Complete current ordinary dispatcher color branches");
        uint[] Table(int tcRva,int scRva,int count,string hash)
        {
            var bytes=image.Raw(tc?tcRva:scRva,count*4);VerifyHash(bytes,hash,"Native color enum table");
            var values=new uint[count];for(int i=0;i<count;i++)values[i]=BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(i*4,4));
            return values;
        }
        uint[] color=Table(0x14BF7E0,0x14C06C0,12,"AEB7EA75E3369453455A6072886ECC7A087D7456FD8C780A6C8BC955B0200C81");
        uint[] alpha=Table(0x14BF810,0x14C06F0,12,"22D23E8B591347069F01771E826DD614BF0E1A0A35671E7A5CA0D41E0BDD3C2F");
        uint[] op=Table(0x14BEEA8,0x14BFD88,6,"C26517843D03C38E647B461BCAB86969D57485FA643636BC0C06E491DD05D057");
        int constructor=tc?0x966EA0:0x968020;
        uint factor=BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(constructor-start+0x65+6,4));
        byte rgb=text[constructor-start+0x50+3],alphaEnable=text[constructor-start+0x54+2];
        if(factor!=0x1100||rgb!=7||alphaEnable!=1)throw new InvalidDataException("Actual frontend color constructor literals changed.");
        return new(locale,profile.ExeHash,factor,rgb,alphaEnable,color,alpha,op);
    }
}
