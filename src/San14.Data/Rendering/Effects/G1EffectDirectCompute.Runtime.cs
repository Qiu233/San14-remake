using San14.Data.Formats;

namespace San14.Data;

public sealed partial class G1EffectGenericRuntime
{
    public G1EffectModule DirectModuleDeclaration => module;
    public G1EffectContext DirectEffectContext => context;
    public IG1EffectDirectParticleStorage? DirectStorage => directStorage;
    internal Func<IEnumerable<G1EffectObjectDataPatch>>? DirectPreparedObjectData { get; set; }
    internal Func<IEnumerable<G1EffectObjectDataPatch>>? DirectLiveObjectData { get; set; }
    internal object? DirectModuleInstanceKey { get; set; }
}
