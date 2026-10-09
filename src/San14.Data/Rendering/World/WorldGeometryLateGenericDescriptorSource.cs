using System.Buffers.Binary;

namespace San14.Data;

public readonly record struct WorldGeometryLateGenericDescriptorMapping(
    uint Value2C,ushort Count30,ushort? RetainedPadding32);
/// <summary>Generic+C8's parameter layout descriptor. ModuleMask10 is the
/// source module+70 shared parameter mask. This object is independent of
/// the six shader stage wrappers at Generic28..50.</summary>
public sealed class WorldGeometryLateGenericDescriptor
{
    private readonly ushort[] words;
    private readonly WorldGeometryLateGenericDescriptorMapping[] mappings;
    internal WorldGeometryLateGenericDescriptor(object actualOwner,ulong mask,ushort[] stageWords,
        WorldGeometryLateGenericDescriptorMapping[] entries)
    { ActualOwner=actualOwner;ModuleMask10=mask;words=stageWords;mappings=entries; }
    public object ActualOwner { get; }
    public ulong ModuleMask10 { get; }
    public ReadOnlyMemory<ushort> StageWords20To2A=>words;
    public IReadOnlyList<WorldGeometryLateGenericDescriptorMapping> MappingEntries2C=>Array.AsReadOnly(mappings);
    public bool SuppressesStagePreparation(int stage)=>(words[stage]&0x8000)==0;
}

public enum WorldGeometryLateGenericDescriptorPublicationBranch
{
    SelectorOutsidePair, NoSpecialFeature, CachedDescriptorNull,
    AlreadyCurrentDescriptor, RendererAbsent, ModuleMaskMismatch, StageCountMismatch, PublishCachedDescriptor
}
public sealed record WorldGeometryLateGenericDescriptorPublication(
    WorldGeometryLateGenericDescriptorPublicationBranch Branch,int? SpecialFeatureIndex,
    WorldGeometryLateGenericDescriptor? Descriptor)
{
    public bool CallsGenericC8Publisher=>Branch is WorldGeometryLateGenericDescriptorPublicationBranch.AlreadyCurrentDescriptor
        or WorldGeometryLateGenericDescriptorPublicationBranch.RendererAbsent
        or WorldGeometryLateGenericDescriptorPublicationBranch.ModuleMaskMismatch
        or WorldGeometryLateGenericDescriptorPublicationBranch.StageCountMismatch
        or WorldGeometryLateGenericDescriptorPublicationBranch.PublishCachedDescriptor;
    public bool ChangesGenericC8=>Branch==WorldGeometryLateGenericDescriptorPublicationBranch.PublishCachedDescriptor;
}

/// <summary>TC12F6060 descriptor construction and E30810 cached publisher
/// selection. Inputs are current native CPU groups or their managed owners;
/// getters never import source files or inspect translated GPU reflection.
/// It does not substitute an unfilled descriptor for116BD00's completed one.</summary>
public static class WorldGeometryLateGenericDescriptorSource
{
    public const string SourceProvenance="TC12F6060..12F648D;116CB10/116BD00;E30810..E308D4;984700..984DC8";
    /// <summary>Construct exactly the descriptor before116BD00 fills it.
    /// Six counts carry bit15 and the two written mapping fields are zero.
    /// Each unused padding word retains unknown allocation storage. The
    /// four shared groups are68/70/78/80;88 counts only moduleMask bits.</summary>
    public static WorldGeometryLateGenericDescriptor? ConstructActualUnfilled(
        object actualDescriptorOwner,ulong actualModuleMask70,
        IReadOnlyList<ReadOnlyMemory<byte>> actualSharedGroups68To80,ReadOnlyMemory<byte> actualGroup88,
        IReadOnlyList<ReadOnlyMemory<byte>?> actualRendererGroups30To50)
    {
        ArgumentNullException.ThrowIfNull(actualDescriptorOwner);
        int[] counts=CountActualStageParameters(actualModuleMask70,actualSharedGroups68To80,actualGroup88,actualRendererGroups30To50);
        uint total=0;foreach(int count in counts)total=unchecked(total+(uint)count);
        if(total==0)return null; //12F63BA..12F63BC.
        var stageWords=counts.Select(count=>unchecked((ushort)(count|0x8000))).ToArray();
        return new(actualDescriptorOwner,actualModuleMask70,stageWords,
            Enumerable.Repeat(new WorldGeometryLateGenericDescriptorMapping(0,0,null),checked((int)total)).ToArray());
    }
    /// <summary>Import a completed descriptor, including original words and
    /// mappings. Header pointers remain actual owner identities, not fabricated
    /// native addresses. This import belongs to the source load/publication.</summary>
    public static WorldGeometryLateGenericDescriptor ImportActualCompleted(
        object actualDescriptorOwner,ReadOnlySpan<byte> actualNativeDescriptor)
    {
        ArgumentNullException.ThrowIfNull(actualDescriptorOwner);
        if(actualNativeDescriptor.Length<0x2C)throw new InvalidDataException("Actual Generic C8 descriptor header is truncated.");
        ushort[] words=new ushort[6];int entries=0;
        for(int stage=0;stage<6;stage++){words[stage]=R16(actualNativeDescriptor,0x20+stage*2);entries+=words[stage]&0x7FFF;}
        int bytes=checked(entries*8);
        if(actualNativeDescriptor.Length<0x2C+bytes)throw new InvalidDataException("Actual Generic C8 descriptor mappings are truncated.");
        var mappings=new WorldGeometryLateGenericDescriptorMapping[entries];
        for(int entry=0;entry<entries;entry++){
            int at=0x2C+entry*8;mappings[entry]=new(BinaryPrimitives.ReadUInt32LittleEndian(actualNativeDescriptor.Slice(at,4)),
                R16(actualNativeDescriptor,at+4),R16(actualNativeDescriptor,at+6));
        }
        return new(actualDescriptorOwner,R64(actualNativeDescriptor,0x10),words,mappings);
    }
    public static int[] CountActualStageParameters(ulong actualModuleMask70,
        IReadOnlyList<ReadOnlyMemory<byte>> actualSharedGroups68To80,ReadOnlyMemory<byte> actualGroup88,
        IReadOnlyList<ReadOnlyMemory<byte>?> actualRendererGroups30To50)
    {
        if(actualSharedGroups68To80.Count!=4||actualRendererGroups30To50.Count!=5)
            throw new InvalidDataException("Descriptor constructor requires source four shared and five nullable renderer groups.");
        int[] counts=new int[6];
        foreach(var group in actualSharedGroups68To80)AddGroup(group.Span);
        foreach(var group in actualRendererGroups30To50)if(group is {} present)AddGroup(present.Span);
        ReadOnlySpan<byte> masked=actualGroup88.Span;ulong bits=actualModuleMask70;int parameter=0;
        while(bits!=0){
            if((bits&1)!=0)for(int stage=0;stage<6;stage++){
                int at=0x70+parameter*0x80+(stage<2?0x1E+stage*2:0x4C+(stage-2)*2);
                if(unchecked((short)R16(masked,at))>=0)counts[stage]=unchecked(counts[stage]+1);
            }
            bits>>=1;parameter++;
        }
        return counts;
        void AddGroup(ReadOnlySpan<byte> data){for(int stage=0;stage<6;stage++)counts[stage]=unchecked(counts[stage]+R32(data,0x58+stage*4));}
    }
    /// <summary>E30810 skips a null cached descriptor; it does not publish
    /// null. The caller retains prior Generic+C8 on every skipped/rejected
    /// branch. Only exact feature firstDWORD10000000 selects this path.</summary>
    public static WorldGeometryLateGenericDescriptorPublication SelectActualCachedPublication(
        int actualSelector,IReadOnlyList<uint> actualFeatureFirstWords,
        WorldGeometryLateGenericDescriptor? actualCached15C8,WorldGeometryLateGenericDescriptor? actualCached15D0,
        bool actualCurrentRendererPresent,WorldGeometryLateGenericDescriptor? actualCurrentC8,
        ulong actualCurrentModuleMask70,ReadOnlySpan<int> actualCurrentStageParameterCounts)
    {
        if((uint)actualSelector>=2)return new(WorldGeometryLateGenericDescriptorPublicationBranch.SelectorOutsidePair,null,null);
        int feature=-1;for(int i=0;i<actualFeatureFirstWords.Count;i++)if(actualFeatureFirstWords[i]==0x10000000){feature=i;break;}
        if(feature<0)return new(WorldGeometryLateGenericDescriptorPublicationBranch.NoSpecialFeature,null,null);
        var descriptor=actualSelector==0?actualCached15C8:actualCached15D0;
        if(descriptor is null)return new(WorldGeometryLateGenericDescriptorPublicationBranch.CachedDescriptorNull,feature,null);
        if(actualCurrentC8 is not null&&ReferenceEquals(actualCurrentC8.ActualOwner,descriptor.ActualOwner))
            return new(WorldGeometryLateGenericDescriptorPublicationBranch.AlreadyCurrentDescriptor,feature,descriptor);
        if(!actualCurrentRendererPresent)return new(WorldGeometryLateGenericDescriptorPublicationBranch.RendererAbsent,feature,descriptor);
        if(actualCurrentModuleMask70!=descriptor.ModuleMask10)return new(WorldGeometryLateGenericDescriptorPublicationBranch.ModuleMaskMismatch,feature,descriptor);
        if(actualCurrentStageParameterCounts.Length!=6)throw new InvalidDataException("C8 publisher requires six actual stage parameter counts.");
        for(int stage=0;stage<6;stage++)if(descriptor.SuppressesStagePreparation(stage)&&actualCurrentStageParameterCounts[stage]!=descriptor.StageWords20To2A.Span[stage])
            return new(WorldGeometryLateGenericDescriptorPublicationBranch.StageCountMismatch,feature,descriptor);
        return new(WorldGeometryLateGenericDescriptorPublicationBranch.PublishCachedDescriptor,feature,descriptor);
    }
    private static ushort R16(ReadOnlySpan<byte> data,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(at,2));
    private static int R32(ReadOnlySpan<byte> data,int at)=>BinaryPrimitives.ReadInt32LittleEndian(data.Slice(at,4));
    private static ulong R64(ReadOnlySpan<byte> data,int at)=>BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(at,8));
}
