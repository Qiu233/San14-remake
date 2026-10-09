namespace San14.Data;

public sealed record StartupModelReference(int? Identity);
public sealed record StartupWorldReference(int? Identity);

public interface IStaticLoadCompletionState
{
    StartupModelReference? ReadStartupModelSingleton();
    void ResetStartupModels(StartupModelReference model, int argument);
    void BuildStartupModels(StartupModelReference model, int argument);
    RegistryListHandle? ReadStaticCompletionListHandle();
    bool? ReadStaticCompletionPoolPresent();
    int? ReadStaticCompletionDescriptorKey(RegistryListHandle handle);
    void EnterStaticCompletionPoolLock();
    bool? ReadStaticCompletionHeadTablePresent();
    int? ReadStaticCompletionKeyCapacity();
    void ClearAddressableStaticCompletionList(int key);
    void ExitStaticCompletionPoolLock();
    void ResetStaticCompletionScratch();
    StartupWorldReference? ReadStaticCompletionWorld();
    void WriteStaticCompletionWorldField2214(StartupWorldReference world, int value);
}

/// <summary>The staticFlag1 tail of32F910 after its map helpers: fresh model
/// reset/build, registry140 node recycling,281B10 and fresh world2214=1.
/// The two model operations remain real required host stages, never empty
/// callbacks supplied automatically. This is not first-player readiness.</summary>
public static class StaticLoadCompletion
{
    public static void Apply(IStaticLoadCompletionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        StartupModelReference first=Model(state.ReadStartupModelSingleton());
        state.ResetStartupModels(first,0);
        StartupModelReference second=Model(state.ReadStartupModelSingleton());
        state.BuildStartupModels(second,0);
        //D20F0 can query registry140; clear it only after model construction.
        RegistryListHandle handle=state.ReadStaticCompletionListHandle()
            ?? throw new InvalidDataException("Current static completion descriptor140 is unknown.");
        if(handle.Identity.HasValue && Known(state.ReadStaticCompletionPoolPresent(),"pool presence"))
        {
            int key=Known(state.ReadStaticCompletionDescriptorKey(handle),"saved descriptor key");
            state.EnterStaticCompletionPoolLock();
            try
            {
                if(Known(state.ReadStaticCompletionHeadTablePresent(),"head table presence"))
                {
                    int capacity=Known(state.ReadStaticCompletionKeyCapacity(),"key capacity");
                    if(capacity<0)throw new InvalidDataException("Static completion pool capacity is negative.");
                    if(unchecked((uint)key)<(uint)capacity)state.ClearAddressableStaticCompletionList(key);
                }
            }
            finally
            {
                // Preserve committed list effects on a missing-input exception,
                // but release the managed host lock so recovery cannot deadlock.
                state.ExitStaticCompletionPoolLock();
            }
        }
        state.ResetStaticCompletionScratch();
        StartupWorldReference world=state.ReadStaticCompletionWorld()
            ?? throw new InvalidDataException("Fresh static completion world pointer is unknown.");
        if(!world.Identity.HasValue)throw new InvalidDataException("Fresh static completion world is null at2214 write.");
        state.WriteStaticCompletionWorldField2214(world,1);
    }
    private static StartupModelReference Model(StartupModelReference? reference)
    {
        if(reference is null)throw new InvalidDataException("Current startup model singleton is unknown.");
        if(!reference.Identity.HasValue)throw new InvalidDataException("Current startup model singleton is null at its model operation.");
        return reference;
    }
    private static T Known<T>(T? value,string field) where T:struct
        => value ?? throw new InvalidDataException($"Current static completion {field} is unknown.");
}
