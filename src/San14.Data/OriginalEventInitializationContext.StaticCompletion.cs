namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalStaticLoadCompletionState
{
    private readonly byte?[] _staticCompletionScratch=new byte?[0x7D4];
    private readonly Dictionary<int,int?> _completionWorldFields2214=[];
    public OriginalStartupModelReference? StartupModelSingleton { get; set; }
    public Func<OriginalStartupModelReference?>? StartupModelReader { get; set; }
    public Action<OriginalStartupModelReference,int>? StartupModelResetter { get; set; }
    public Action<OriginalStartupModelReference,int>? StartupModelBuilder { get; set; }
    public OriginalRegistryListHandle? StaticCompletionListHandle { get; set; }
    public OriginalStartupAssociationPool? StartupAssociationPool { get; set; }
    public OriginalStartupWorldReference? StaticCompletionWorld { get; set; }
    public Func<OriginalStartupWorldReference?>? StaticCompletionWorldReader { get; set; }

    public OriginalStartupModelReference? ReadStartupModelSingleton() => StartupModelReader is null ? StartupModelSingleton : StartupModelReader();
    public void ResetStartupModels(OriginalStartupModelReference model,int argument)
        => (StartupModelResetter ?? throw new InvalidDataException("Required original startup model reset is not bound."))(model,argument);
    public void BuildStartupModels(OriginalStartupModelReference model,int argument)
        => (StartupModelBuilder ?? throw new InvalidDataException("Required original startup model build is not bound."))(model,argument);
    public OriginalRegistryListHandle? ReadStaticCompletionListHandle() => StaticCompletionListHandle;
    public bool? ReadStaticCompletionPoolPresent() => StartupAssociationPool?.Present;
    public int? ReadStaticCompletionDescriptorKey(OriginalRegistryListHandle handle) => RegistryKey(handle);
    public void EnterStaticCompletionPoolLock() => AssociationPool().EnterLock();
    public void ExitStaticCompletionPoolLock() => AssociationPool().ExitLock();
    public bool? ReadStaticCompletionHeadTablePresent() => StartupAssociationPool?.HeadTablePresent;
    public int? ReadStaticCompletionKeyCapacity() => StartupAssociationPool?.DescriptorCapacity;
    public void ClearAddressableStaticCompletionList(int key) => AssociationPool().Clear(key);
    private OriginalStartupAssociationPool AssociationPool() => StartupAssociationPool
        ?? throw new InvalidDataException("Current secondary startup association pool is unknown.");
    public void ResetStaticCompletionScratch() => Array.Fill(_staticCompletionScratch,(byte?)0);
    public byte? ReadStaticCompletionScratchByte(int offset)
    {Check(offset,_staticCompletionScratch.Length,"static completion scratch offset");return _staticCompletionScratch[offset];}
    public void SetStaticCompletionScratchByte(int offset,byte? value)
    {Check(offset,_staticCompletionScratch.Length,"static completion scratch offset");_staticCompletionScratch[offset]=value;}
    public OriginalStartupWorldReference? ReadStaticCompletionWorld() => StaticCompletionWorldReader is null ? StaticCompletionWorld : StaticCompletionWorldReader();
    public int? ReadStaticCompletionWorldField2214(int identity) => _completionWorldFields2214.GetValueOrDefault(identity);
    public void WriteStaticCompletionWorldField2214(OriginalStartupWorldReference world,int value)
    {
        int identity=world.Identity ?? throw new InvalidDataException("Null completion world cannot accept2214.");
        CheckOpaque(identity,"completion world identity");_completionWorldFields2214[identity]=value;
    }
    public void ApplyStaticLoadCompletion() => OriginalStaticLoadCompletion.Apply(this);
}
