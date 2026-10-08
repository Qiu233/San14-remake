using San14.Data.Formats;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalPostRegistrationLookupState
{
    private readonly Dictionary<int,byte?> _forceRanks47=[];
    private readonly Dictionary<int,int?> _rankCapacityPointers=[],_rankPersonCapacities=[];
    private readonly Dictionary<int,OriginalRegistryListHandle?> _forceDerivedPersonDescriptors=[];
    private readonly Dictionary<int,int?> _doctrinePanelPointers=[],_doctrinePanelIdPointers=[];
    private readonly Dictionary<int,byte?> _doctrinePanelFields7F=[];
    public SourceProvenance? RankPersonCapacitySource { get; private set; }
    public SourceProvenance? DoctrinePanelOrderingSource { get; private set; }
    public void WritePersonLookupReference(int id,OriginalTransferPersonReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        SetPersonTablePointer(id,reference.PhysicalSlot);
    }
    public OriginalTransferPersonReference? ReadPersonLookupReference(int id)
    {
        Check(id,6001,"person lookup ID");
        if(_personPointers.TryGetValue(id,out int? physical))return new(physical);
        return _personLookupCleared ? new(null) : null;
    }
    public void SetForceRankSlot47(int physical,byte? rank)
    {Check(physical,52,"force rank receiver");_forceRanks47[physical]=rank;}
    public void SetForceDerivedPersonDescriptor(int physical,OriginalRegistryListHandle? handle)
    {Check(physical,52,"force derived-person receiver");_forceDerivedPersonDescriptors[physical]=handle;}
    public void MaterializeRankPersonCapacities(LoadedRankPersonCapacities loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        if(loaded.Catalog.Capacities.Count!=13)throw new InvalidDataException("Rank person capacity profile requires13 entries.");
        for(int slot=0;slot<13;slot++){_rankCapacityPointers[slot]=slot;_rankPersonCapacities[slot]=loaded.Catalog.Capacities[slot];}
        RankPersonCapacitySource=loaded.Source;
    }
    public int ReadRemainingForceDerivedPersonCapacity(OriginalForceReference force)
    {
        int physical=ReadForcePhysicalSlot(force)??throw new InvalidDataException("Current force physical identity is unknown.");
        byte FirstRank()=>_forceRanks47.GetValueOrDefault(physical)
            ??throw new InvalidDataException("Current force rank47 is unknown.");
        int? RankPointer(byte rank)=>_rankCapacityPointers.TryGetValue(rank<=12?rank:0,out int? value) ? value
            :throw new InvalidDataException("Current rank table pointer is unknown.");
        if(!RankPointer(FirstRank()).HasValue)return 0;
        int rankPhysical=RankPointer(FirstRank())
            ??throw new InvalidDataException("Second rank table lookup is explicitly null at the capacity getter.");
        OriginalRegistryListHandle descriptor=_forceDerivedPersonDescriptors.GetValueOrDefault(physical)
            ??throw new InvalidDataException("Current force derived-person descriptor260 is unknown.");
        int count=ReadTemporaryHexListCount(new(RegistryKey(descriptor)))
            ??throw new InvalidDataException("Current force derived-person count is unknown.");
        int maximum=_rankPersonCapacities.GetValueOrDefault(rankPhysical)
            ??throw new InvalidDataException("Current rank person capacity is unknown.");
        return unchecked(maximum-count);
    }
    public bool AddForceDerivedPerson(OriginalForceReference force,OriginalTransferPersonReference person)
    {
        ArgumentNullException.ThrowIfNull(person);
        if(ReadRemainingForceDerivedPersonCapacity(force)<=0)return false;
        if(ReadRegistryLockEnabled()??throw new InvalidDataException("Current derived-person lock flag is unknown."))EnterRegistryLock();
        int physical=ReadForcePhysicalSlot(force)??throw new InvalidDataException("Current force identity is unknown.");
        OriginalRegistryListHandle descriptor=_forceDerivedPersonDescriptors.GetValueOrDefault(physical)
            ??throw new InvalidDataException("Current derived-person descriptor is unknown after locking.");
        OriginalRegistryListNodeReference node=AllocateRegistryNode(descriptor)
            ??throw new InvalidDataException("Current derived-person allocation result is unknown.");
        if(node.Identity.HasValue)WriteRegistryNodePayload(node,new(OriginalRegistryKind.Person,person.PhysicalSlot));
        if(ReadRegistryLockEnabled()??throw new InvalidDataException("Current derived-person unlock flag is unknown."))ExitRegistryLock();
        return true;
    }
    public void SetDoctrinePanelTablePointer(int slot,int? physical)=>SetPointer(_doctrinePanelPointers,slot,80,physical,80);
    public OriginalDoctrinePanelReference? ReadDoctrinePanelTableReference(int slot)
    {Check(slot,80,"doctrine panel table slot");return _doctrinePanelPointers.TryGetValue(slot,out int? physical)?new(physical):null;}
    public byte? ReadDoctrinePanelRawField7F(OriginalDoctrinePanelReference reference)=>
        _doctrinePanelFields7F.GetValueOrDefault(MapPhysical(reference.PhysicalSlot,80,"doctrine panel"));
    public void WriteDoctrinePanelLookupReference(int id,OriginalDoctrinePanelReference reference)
    {Check(id,256,"doctrine ordering ID");_doctrinePanelIdPointers[id]=reference.PhysicalSlot;}
    public OriginalDoctrinePanelReference? ReadDoctrinePanelLookupReference(int id)
    {Check(id,256,"doctrine ordering ID");return _doctrinePanelIdPointers.TryGetValue(id,out int? physical)?new(physical):null;}
    public void MaterializeDoctrinePanelOrdering(S14DoctrinePanelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if(catalog.FormatMarker!=0x66 || catalog.Panels.Count!=80 || catalog.Panels.Where((row,index)=>row.Slot!=index || row.RawBytes.Length!=33).Any())
            throw new InvalidDataException("Doctrine ordering requires the complete inspected80-row source catalog.");
        foreach(var row in catalog.Panels){SetDoctrinePanelTablePointer(row.Slot,row.Slot);_doctrinePanelFields7F[row.Slot]=row.RawBytes[26];}
        DoctrinePanelOrderingSource=null;
    }
    public void MaterializeDoctrinePanelOrdering(LoadedDoctrinePanels loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        MaterializeDoctrinePanelOrdering(loaded.Catalog);
        DoctrinePanelOrderingSource=loaded.Source;
    }
    public void InitializePostRegistrationLookups(int staticFlag)=>OriginalPostRegistrationLookups.Apply(staticFlag,this);
}
