namespace San14.Data;

public sealed partial class SourceWorldContext : IStaticLoadCompletionState
{
    private readonly byte?[] _staticCompletionScratch=new byte?[0x7D4];
    private readonly Dictionary<int,int?> _completionWorldFields2214=[];
    public StartupModelReference? StartupModelSingleton { get; set; }
    public Func<StartupModelReference?>? StartupModelReader { get; set; }
    public Action<StartupModelReference,int>? StartupModelResetter { get; set; }
    public Action<StartupModelReference,int>? StartupModelBuilder { get; set; }
    public RegistryListHandle? StaticCompletionListHandle { get; set; }
    public StartupAssociationPool? StartupAssociationPool { get; set; }
    public StartupWorldReference? StaticCompletionWorld { get; set; }
    public Func<StartupWorldReference?>? StaticCompletionWorldReader { get; set; }

    public StartupModelReference? ReadStartupModelSingleton() => StartupModelReader is null ? StartupModelSingleton : StartupModelReader();
    public void ResetStartupModels(StartupModelReference model,int argument)
        => (StartupModelResetter ?? throw new InvalidDataException("Required original startup model reset is not bound."))(model,argument);
    public void BuildStartupModels(StartupModelReference model,int argument)
        => (StartupModelBuilder ?? throw new InvalidDataException("Required original startup model build is not bound."))(model,argument);
    public RegistryListHandle? ReadStaticCompletionListHandle() => StaticCompletionListHandle;
    public bool? ReadStaticCompletionPoolPresent() => StartupAssociationPool?.Present;
    public int? ReadStaticCompletionDescriptorKey(RegistryListHandle handle) => RegistryKey(handle);
    public void EnterStaticCompletionPoolLock() => AssociationPool().EnterLock();
    public void ExitStaticCompletionPoolLock() => AssociationPool().ExitLock();
    public bool? ReadStaticCompletionHeadTablePresent() => StartupAssociationPool?.HeadTablePresent;
    public int? ReadStaticCompletionKeyCapacity() => StartupAssociationPool?.DescriptorCapacity;
    public void ClearAddressableStaticCompletionList(int key) => AssociationPool().Clear(key);
    private StartupAssociationPool AssociationPool() => StartupAssociationPool
        ?? throw new InvalidDataException("Current secondary startup association pool is unknown.");
    public void ResetStaticCompletionScratch() => Array.Fill(_staticCompletionScratch,(byte?)0);
    public byte? ReadStaticCompletionScratchByte(int offset)
    {Check(offset,_staticCompletionScratch.Length,"static completion scratch offset");return _staticCompletionScratch[offset];}
    public void SetStaticCompletionScratchByte(int offset,byte? value)
    {Check(offset,_staticCompletionScratch.Length,"static completion scratch offset");_staticCompletionScratch[offset]=value;}
    public StartupWorldReference? ReadStaticCompletionWorld() => StaticCompletionWorldReader is null ? StaticCompletionWorld : StaticCompletionWorldReader();
    public int? ReadStaticCompletionWorldField2214(int identity) => _completionWorldFields2214.GetValueOrDefault(identity);
    public void WriteStaticCompletionWorldField2214(StartupWorldReference world,int value)
    {
        int identity=world.Identity ?? throw new InvalidDataException("Null completion world cannot accept2214.");
        CheckOpaque(identity,"completion world identity");_completionWorldFields2214[identity]=value;
    }
    public void ApplyStaticLoadCompletion() => StaticLoadCompletion.Apply(this);
}
