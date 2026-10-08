namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalStaticTablePostLoadState
{
    private readonly Dictionary<(int Table,int Slot),int?> _staticTablePointers = [];
    private readonly Dictionary<(int Table,int Physical),uint?> _staticSlot4Targets = [];
    private static readonly IReadOnlyDictionary<int,OriginalRegistryKind> StaticRegistryKinds = new Dictionary<int,OriginalRegistryKind>
    {
        [0x6CAF0]=OriginalRegistryKind.Area, [0x7E838]=OriginalRegistryKind.Army,
        [0xDD38]=OriginalRegistryKind.City, [0xE0D0]=OriginalRegistryKind.District,
        [0xDF30]=OriginalRegistryKind.Force, [0xDED8]=OriginalRegistryKind.Gate,
        [0x77B18]=OriginalRegistryKind.Item, [0x6DA98]=OriginalRegistryKind.Object,
        [0x73A50]=OriginalRegistryKind.Person, [0x80BA8]=OriginalRegistryKind.CautionTroops,
        [0x81B50]=OriginalRegistryKind.Table81B50, [0x85A90]=OriginalRegistryKind.Scenery
    };

    /// <summary>Explicit successful allocation of the independent normal CE
    /// pointer-table profile. Creates pointers only, leaving consumed fields,
    /// source provenance, descriptors and runtime lists untouched.</summary>
    public void InitializeStaticTablePointers()
    {
        foreach (OriginalStaticTableRange range in OriginalStaticTablePostLoad.Ranges)
            for (int slot = 0; slot < range.Count; slot++) SetStaticTablePointer(range.TableOffset,slot,slot);
    }

    public void SetStaticTablePointer(int tableOffset, int slot, int? physical)
    {
        int count = StaticTableCount(tableOffset);
        Check(slot,count,"static table slot");
        if (physical.HasValue) Check(physical.Value,count,"static table physical receiver");
        if (StaticRegistryKinds.TryGetValue(tableOffset,out OriginalRegistryKind kind))
            SetRegistryTablePointer(kind,slot,physical);
        else switch (tableOffset)
        {
            case 0xE270: SetHexTablePointer(slot,physical); break;
            case 0x73860: SetSurfaceTypeTablePointer(slot,physical); break;
            case 0x78490: _rankCapacityPointers[slot]=physical; break;
            case 0x85F20: SetDoctrinePanelTablePointer(slot,physical); break;
            default: _staticTablePointers[(tableOffset,slot)]=physical; break;
        }
    }

    public void SetStaticTableSlot4Target(int tableOffset, int physical, uint? target)
    {
        Check(physical,StaticTableCount(tableOffset),"static virtual receiver");
        _staticSlot4Targets[(tableOffset,physical)]=target;
    }

    public OriginalStaticTableReference? ReadStaticTableReference(int tableOffset, int slot)
    {
        Check(slot,StaticTableCount(tableOffset),"static table slot");
        int? physical;
        if (StaticRegistryKinds.TryGetValue(tableOffset,out OriginalRegistryKind kind))
        {
            OriginalRegistryObjectReference? reference = ReadRegistryTableReference(kind,slot);
            if (reference is null) return null;
            physical = reference.PhysicalSlot;
        }
        else switch (tableOffset)
        {
            case 0xE270:
                OriginalHexReference? hex = ReadHexTableReference(slot);
                if (hex is null) return null;
                physical=hex.PhysicalSlot;break;
            case 0x73860:
                OriginalSurfaceTypeReference? terrain = ReadSurfaceTypeTableReference(slot);
                if (terrain is null) return null;
                physical=terrain.PhysicalSlot;break;
            case 0x78490:
                if (!_rankCapacityPointers.TryGetValue(slot,out physical)) return null;
                break;
            case 0x85F20:
                OriginalDoctrinePanelReference? panel = ReadDoctrinePanelTableReference(slot);
                if (panel is null) return null;
                physical=panel.PhysicalSlot;break;
            default:
                if (!_staticTablePointers.TryGetValue((tableOffset,slot),out physical)) return null;
                break;
        }
        uint? target = physical.HasValue && _staticSlot4Targets.TryGetValue((tableOffset,physical.Value),out uint? provided)
            ? provided : 0x370500;
        return new(physical,target);
    }

    public void ApplyStaticTablePostLoad() => OriginalStaticTablePostLoad.Apply(this);
    private static int StaticTableCount(int tableOffset) => OriginalStaticTablePostLoad.Ranges.SingleOrDefault(range=>range.TableOffset==tableOffset)?.Count
        ?? throw new InvalidDataException($"Static table{tableOffset:X} is outside the supported CE profile.");
}
