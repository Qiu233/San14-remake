namespace San14.Data;

public sealed partial class SourceWorldContext : IStaticTablePostLoadState
{
    private readonly Dictionary<(int Table,int Slot),int?> _staticTablePointers = [];
    private readonly Dictionary<(int Table,int Physical),PostLoadCallback?> _staticPostLoadCallbacks = [];
    private static readonly IReadOnlyDictionary<int,RegistryKind> StaticRegistryKinds = new Dictionary<int,RegistryKind>
    {
        [0x6CAF0]=RegistryKind.Area, [0x7E838]=RegistryKind.Army,
        [0xDD38]=RegistryKind.City, [0xE0D0]=RegistryKind.District,
        [0xDF30]=RegistryKind.Force, [0xDED8]=RegistryKind.Gate,
        [0x77B18]=RegistryKind.Item, [0x6DA98]=RegistryKind.Object,
        [0x73A50]=RegistryKind.Person, [0x80BA8]=RegistryKind.CautionTroops,
        [0x81B50]=RegistryKind.Table81B50, [0x85A90]=RegistryKind.Scenery
    };

    /// <summary>Explicit successful allocation of the independent normal CE
    /// pointer-table profile. Creates pointers only, leaving consumed fields,
    /// source provenance, descriptors and runtime lists untouched.</summary>
    public void InitializeStaticTablePointers()
    {
        foreach (StaticTableRange range in StaticTablePostLoad.Ranges)
        {
            if (range.TableOffset == 0xE270 && _hexIdentityDefinitions is not null)
            {
                //The campaign definition already owns the identity table.
                //A new allocation resets any local pointer substitutions.
                _hexPointers.Clear();
                continue;
            }
            for (int slot = 0; slot < range.Count; slot++) SetStaticTablePointer(range.TableOffset,slot,slot);
        }
    }

    public void SetStaticTablePointer(int tableOffset, int slot, int? physical)
    {
        int count = StaticTableCount(tableOffset);
        Check(slot,count,"static table slot");
        if (physical.HasValue) Check(physical.Value,count,"static table physical receiver");
        if (StaticRegistryKinds.TryGetValue(tableOffset,out RegistryKind kind))
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

    public void SetStaticTablePostLoadCallback(int tableOffset, int physical, PostLoadCallback? callback)
    {
        Check(physical,StaticTableCount(tableOffset),"static virtual receiver");
        _staticPostLoadCallbacks[(tableOffset,physical)]=callback;
    }

    public StaticTableReference? ReadStaticTableReference(int tableOffset, int slot)
    {
        Check(slot,StaticTableCount(tableOffset),"static table slot");
        int? physical;
        if (StaticRegistryKinds.TryGetValue(tableOffset,out RegistryKind kind))
        {
            RegistryObjectReference? reference = ReadRegistryTableReference(kind,slot);
            if (reference is null) return null;
            physical = reference.PhysicalSlot;
        }
        else switch (tableOffset)
        {
            case 0xE270:
                HexReference? hex = ReadHexTableReference(slot);
                if (hex is null) return null;
                physical=hex.PhysicalSlot;break;
            case 0x73860:
                SurfaceTypeReference? terrain = ReadSurfaceTypeTableReference(slot);
                if (terrain is null) return null;
                physical=terrain.PhysicalSlot;break;
            case 0x78490:
                if (!_rankCapacityPointers.TryGetValue(slot,out physical)) return null;
                break;
            case 0x85F20:
                DoctrinePanelReference? panel = ReadDoctrinePanelTableReference(slot);
                if (panel is null) return null;
                physical=panel.PhysicalSlot;break;
            default:
                if (!_staticTablePointers.TryGetValue((tableOffset,slot),out physical)) return null;
                break;
        }
        PostLoadCallback? callback = physical.HasValue && _staticPostLoadCallbacks.TryGetValue((tableOffset,physical.Value),out var provided)
            ? provided : PostLoadCallback.NoOp;
        return new(physical,callback);
    }

    public void ApplyStaticTablePostLoad() => StaticTablePostLoad.Apply(this);
    private static int StaticTableCount(int tableOffset) => StaticTablePostLoad.Ranges.SingleOrDefault(range=>range.TableOffset==tableOffset)?.Count
        ?? throw new InvalidDataException($"Static table{tableOffset:X} is outside the supported CE profile.");
}
