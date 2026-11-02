using System.Collections.Frozen;

namespace San14.Data;

public sealed partial class SourceWorldContext : IStaticTablePostLoadState
{
    // The CE table profile is immutable. Resolve its bounds once rather than
    // scanning all tables and allocating a predicate on each pointer access.
    private static readonly FrozenDictionary<int,int> StaticTableCounts = StaticTablePostLoad.Ranges
        .ToFrozenDictionary(range => range.TableOffset, range => range.Count);
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

    // Only the generic pointer channel borrows this immutable CE layout.
    // Registry, map and specialized tables retain independent knowledge.
    private static readonly FrozenDictionary<int,int> GenericStaticIdentityDefinitions = StaticTableCounts
        .Where(pair => !StaticRegistryKinds.ContainsKey(pair.Key) &&
            pair.Key is not (0xE270 or 0x73860 or 0x78490 or 0x85F20))
        .ToFrozenDictionary(pair => pair.Key, pair => pair.Value);
    private IReadOnlyDictionary<int,int>? _genericStaticPointerDefinitions;

    private bool TryReadGenericStaticPointer(int tableOffset,int slot,out int? physical)
    {
        if (_staticTablePointers.TryGetValue((tableOffset,slot),out physical)) return true;
        if (_genericStaticPointerDefinitions?.ContainsKey(tableOffset) == true)
        { physical = slot; return true; }
        physical = null; return false;
    }

    /// <summary>Explicit successful allocation of the independent normal CE
    /// pointer-table profile. Creates pointers only, leaving consumed fields,
    /// source provenance, descriptors and runtime lists untouched.</summary>
    public void InitializeStaticTablePointers()
    {
        _staticTablePointers.Clear();
        _genericStaticPointerDefinitions = GenericStaticIdentityDefinitions;
        foreach (StaticTableRange range in StaticTablePostLoad.Ranges)
        {
            if (GenericStaticIdentityDefinitions.ContainsKey(range.TableOffset)) continue;
            if (range.TableOffset == 0x73A50)
            {
                _officerSources.InitializeTableReferences(range.Count);
                continue;
            }
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
            default:
                if (_genericStaticPointerDefinitions?.ContainsKey(tableOffset) == true && physical == slot)
                    _staticTablePointers.Remove((tableOffset,slot));
                else _staticTablePointers[(tableOffset,slot)] = physical;
                break;
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
                if (!TryReadGenericStaticPointer(tableOffset,slot,out physical)) return null;
                break;
        }
        PostLoadCallback? callback = physical.HasValue && _staticPostLoadCallbacks.TryGetValue((tableOffset,physical.Value),out var provided)
            ? provided : PostLoadCallback.NoOp;
        return new(physical,callback);
    }

    public void ApplyStaticTablePostLoad() => StaticTablePostLoad.Apply(this);
    private int StaticTableCount(int tableOffset) => tableOffset == 0xE270 ? MapTileCount :
        StaticTableCounts.TryGetValue(tableOffset, out int count) ? count : throw new InvalidDataException($"Static table{tableOffset:X} is outside the supported CE profile.");
}
