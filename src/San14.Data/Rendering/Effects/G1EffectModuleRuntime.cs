using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Source-ordered module update: frame preparation, auxiliaries,
/// generics, outer emission. Unsupported branches remain explicit requests.</summary>
internal sealed class G1EffectModuleRuntime : IDisposable
{
    private readonly G1EffectModule declaration;
    private readonly int systemIndex;
    private readonly Action<G1EffectRuntimeMissing> missing;
    private readonly List<G1EffectGenericRuntime> roots = [];
    private readonly List<G1EffectEmitterRuntime> emitters = [];
    private G1EffectGenericRuntime[] generics = [];
    private readonly HashSet<object> failed = [];
    private readonly G1EffectModuleLifetime lifetime;
    private readonly G1EffectMotionRuntime motion;
    private bool disposed;

    internal G1EffectModuleRuntime(G1EffectModule declaration, int systemIndex,
        G1EffectContext context, Action<G1EffectRuntimeMissing> missing)
    {
        this.declaration = declaration; this.systemIndex = systemIndex; this.missing = missing;
        lifetime = new(declaration);
        motion = new(declaration, declaration.Records.Where(record => record.Table == G1EffectTable.Auxiliary).ToArray(), context);
        try
        {
            foreach (var generic in declaration.Records.Where(record => record.Table == G1EffectTable.Generic))
                roots.Add(new(declaration, generic, context));
            generics = roots.SelectMany(Tree).ToArray();
            var byOffset = Generics.ToDictionary(runtime => runtime.Record.FileOffset);
            foreach (var generic in Generics)
            {
                generic.DirectModuleInstanceKey = this;
                generic.DirectPreparedObjectData = motion.EnumeratePreparedData;
                generic.DirectLiveObjectData = motion.EnumerateLiveData;
                generic.ParticleMotion = (record, slot, dt) =>
                {
                    motion.ParentStateResolver = generic.ResolveParentState;
                    motion.Apply(record, slot, dt);
                };
                generic.ModuleEmissionScale = lifetime.Strength;
            }
            foreach (var emitter in declaration.Records.Where(record => record.Table == G1EffectTable.Emitter))
                emitters.Add(new(declaration, G1EffectEmitterDeclaration.Read(declaration, emitter),
                    context, offset => byOffset[offset]));
        }
        catch { foreach (var generic in roots) generic.Dispose(); throw; }
    }

    internal IReadOnlyList<G1EffectGenericRuntime> Generics => generics;
    private static IEnumerable<G1EffectGenericRuntime> Tree(G1EffectGenericRuntime node)
    { yield return node; foreach (var child in node.Children) foreach (var next in Tree(child)) yield return next; }
    internal bool Enabled => lifetime.Enabled;
    internal bool IsAlive => lifetime.Enabled && (failed.Count != 0 ||
        Generics.Any(generic => generic.Declaration.UsesDirectBirth && !generic.HasDirectStorage) ||
        G1EffectModuleLifetime.IsAlive(roots.Any(root => root.IsAlive), emitters.Any(emitter => emitter.CanEmit),
            lifetime.Strength, 0, false));

    internal void SetParameter(int slot,float value)
    { foreach (var emitter in emitters) emitter.SetParameter(slot,value); }
    internal void SetOpacity(float value)
    {
        lifetime.SetOpacity(value);
        foreach (var generic in Generics) generic.ModuleEmissionScale = lifetime.Strength;
    }
    internal void BeginStop() => lifetime.BeginStop();
    internal void Prewarm(Matrix4x4 world)
    { for (int step = 0; step < declaration.PrewarmSteps; step++) Advance(declaration.PrewarmStepSeconds, world); }

    internal void Advance(float seconds, Matrix4x4 world)
    {
        if (!lifetime.Enabled) return;
        if (lifetime.AdvanceStop(seconds, IsAlive))
        {
            foreach (var generic in roots) generic.Dispose();
            return;
        }
        foreach (var generic in Generics) generic.ModuleEmissionScale = lifetime.Strength;
        foreach (var emitter in emitters)
            Run(emitter, emitter.Scheduler.Declaration.Record.FileOffset, "EmitterFrame",
                () => emitter.PrepareFrame(seconds, world));
        Run(motion, declaration.FileOffset, "AuxiliaryUpdate", () => motion.Advance(seconds));
        foreach (var generic in roots)
            Run(generic, generic.Record.FileOffset, "GenericUpdate", () => generic.Advance(seconds));
        foreach (var emitter in emitters)
            Run(emitter, emitter.Scheduler.Declaration.Record.FileOffset, "EmitterBirth",
                () => emitter.Advance(seconds, lifetime.Strength, world));
    }

    private void Run(object owner, int record, string operation, Action action)
    {
        if (failed.Contains(owner)) return;
        try { action(); }
        catch (NotSupportedException error)
        {
            failed.Add(owner);
            missing(new(systemIndex,declaration.Index,record,operation,error.Message));
        }
    }

    internal IEnumerable<G1EffectParticleRenderSource> EnumerateRenderSources(LoadedMapEffectResources resources)
    {
        if (!lifetime.Enabled) yield break;
        foreach (var generic in roots)
            foreach (var source in generic.EnumerateRenderSources(resources,lifetime.Strength)) yield return source;
    }
    public void Dispose()
    { if(disposed)return; foreach(var generic in roots)generic.Dispose(); disposed=true; }
}
