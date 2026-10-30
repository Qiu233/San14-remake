using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Mutable ownership of one generic and its shared child generics.
/// Only a completely executed birth and initial update can publish a slot.
/// Unsupported writers fail explicitly and release the uninitialized slot.</summary>
public sealed partial class G1EffectGenericRuntime : IDisposable
{
    private readonly G1EffectContext context;
    private readonly G1EffectModule module;
    private readonly Dictionary<G1EffectParticleSlot, G1EffectParticleSlot?> initialized = [];
    private readonly Dictionary<G1EffectParticleSlot,G1EffectParticleBirthInputs> birthInputs = [];
    private readonly Dictionary<Memory<byte>,G1EffectParticleSlot> slotStorage = [];
    private readonly HashSet<G1EffectParticleSlot> preparedFrames = [];
    private bool disposed;
    public G1EffectRecord Record { get; }
    public G1EffectRecord CurrentRecord { get; }
    public byte[] CurrentGenericBytes { get; }
    public G1EffectParticleDeclaration Declaration { get; }
    public G1EffectParticlePool Pool { get; }
    public IReadOnlyList<G1EffectGenericRuntime> Children { get; }
    public bool HasAllocatedPool => Pool.BlockCount != 0;
    public bool IsAlive => initialized.Count != 0 || directStorage?.IsAlive == true || Children.Any(child => child.IsAlive);
    public float? ModuleEmissionScale { get; set; }
    /// <summary>The real prepared particle-motion consumer, when present.</summary>
    public Action<G1EffectRecord, Memory<byte>, float>? ParticleMotion { get; set; }
    /// <summary>Generic-owned fields update after its parent slots and before its child generics.</summary>
    public Action<float>? NestedAuxiliaryAdvance { get; set; }

    public G1EffectGenericRuntime(G1EffectModule module, G1EffectRecord generic, G1EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(module); ArgumentNullException.ThrowIfNull(generic);
        ArgumentNullException.ThrowIfNull(context);
        this.context = context; this.module=module; Record = generic;
        CurrentGenericBytes = generic.Bytes.ToArray();
        CurrentRecord = generic with { Bytes = CurrentGenericBytes };
        Declaration = G1EffectParticleDeclaration.Read(CurrentRecord);
        Pool = new(Declaration);
        var children = new List<G1EffectGenericRuntime>();
        try
        {
            foreach (var child in generic.Children.Where(child => child.Table == G1EffectTable.Generic))
                children.Add(new(module, child, context));
            Children = children.AsReadOnly();
            _ = context.Arena.InitializePool(Pool);
        }
        catch
        {
            foreach (var child in children) child.Dispose();
            context.Arena.ReleasePool(Pool); throw;
        }
    }

    public G1EffectParticleSlot? Birth(G1EffectParticleDefinition definition,
        G1EffectParticleBirthInputs inputs, float initialAgeSeconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (definition.Particle.Record.FileOffset != Record.FileOffset)
            throw new ArgumentException("Definition belongs to another generic.", nameof(definition));
        if (Declaration.UsesDirectBirth)
            return null; // The ordinary source allocator rejects direct targets.
        var slot = Pool.TryAcquire();
        if (slot is null && context.Arena.TryAllocateParticleBlock(Pool)) slot = Pool.TryAcquire();
        if (slot is null) return null;
        bool parentReferenced = false;
        try
        {
            birthInputs.Add(slot,inputs);
            slotStorage.Add(slot.Bytes,slot);
            ExecuteBirth(slot, definition, inputs, ref parentReferenced);
            if(Record.Type==3 && Float(slot.Bytes,0x140)==0) SynchronizeNestedTransform(slot,0);
            AdvanceParticle(slot, initialAgeSeconds);
            if (slot.IsAllocated) initialized.Add(slot, parentReferenced ? inputs.ParentSlot : null);
            else if (parentReferenced && inputs.ParentSlot is { } parent) Reference(parent, -1);
            if(Record.Type==3 && slot.IsAllocated) EmitNested(slot,initialAgeSeconds);
            return slot.IsAllocated ? slot : null;
        }
        catch
        {
            foreach(var child in Children) child.RetireDescendantsOf(slot);
            if (parentReferenced && inputs.ParentSlot is { } parent) Reference(parent, -1);
            initialized.Remove(slot);birthInputs.Remove(slot);slotStorage.Remove(slot.Bytes);preparedFrames.Remove(slot);
            if (slot.IsAllocated) Pool.Release(slot);
            throw;
        }
    }

    public void Advance(float seconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Declaration.UsesDirectBirth)
        { directStorage?.Advance(seconds); return; }
        if (Record.Type == 10) return; // The source generic update dispatch is empty.
        foreach (var slot in Pool.EnumerateAllocated())
        {
            if (!initialized.ContainsKey(slot)) continue;
            if(Record.Type==3 && (Int(0x20C)&0x20)!=0 && Word(slot.Bytes,0x68)<2 && !NestedCanContinue(slot))
            { Retire(slot);continue; }
            if ((BinaryPrimitives.ReadUInt16LittleEndian(slot.Bytes.Span[6..]) & 4) == 0) Retire(slot);
            else AdvanceParticle(slot, seconds);
        }
        NestedAuxiliaryAdvance?.Invoke(seconds);
        foreach (var child in Children) child.Advance(seconds);
        if (Record.Type == 3)
            foreach(var slot in EnumerateInitialized()) EmitNested(slot,seconds);
        if (Pool.PruneEmptyBlock(out var storage)) context.Arena.Return(storage);
    }

    public IEnumerable<G1EffectParticleSlot> EnumerateInitialized() =>
        Pool.EnumerateAllocated().Where(slot => initialized.ContainsKey(slot));

    public IEnumerable<G1EffectParticleRenderSource> EnumerateRenderSources(
        LoadedMapEffectResources resources, float moduleOpacity)
    {
        if (Record.Type != 3)
            yield return new(resources, Declaration, CurrentGenericBytes, EnumerateInitialized(), moduleOpacity, Retire);
        foreach (var child in Children)
            foreach (var source in child.EnumerateRenderSources(resources, moduleOpacity)) yield return source;
    }

    public void Retire(G1EffectParticleSlot slot)
    {
        if (initialized.Remove(slot, out var parent) && parent is not null) Reference(parent, -1);
        birthInputs.Remove(slot);slotStorage.Remove(slot.Bytes);preparedFrames.Remove(slot);
        if (slot.IsAllocated) Pool.Release(slot);
    }

    private void RetireDescendantsOf(G1EffectParticleSlot parent)
    {
        foreach(var slot in Pool.EnumerateAllocated())
            if(initialized.TryGetValue(slot,out var linked) && ReferenceEquals(linked,parent))
            {
                foreach(var child in Children) child.RetireDescendantsOf(slot);
                Retire(slot);
            }
        foreach(var child in Children) child.RetireDescendantsOf(parent);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var child in Children) child.Dispose();
        foreach (var slot in EnumerateInitialized()) Retire(slot);
        context.Arena.ReleasePool(Pool);
        directStorage?.Dispose();directStorage=null;
    }

    /// <summary>The actual parent object view used by particle motion ASTs.</summary>
    public ReadOnlyMemory<byte> ResolveParentState(G1EffectParticleSlot slot)
    {
        if(!birthInputs.TryGetValue(slot,out var inputs)) throw new InvalidOperationException("Particle has no active birth context.");
        if(inputs.ParentSlot is {} parent) return parent.Bytes[0x60..];
        if(inputs.SourceContext.Length>=0x180) return inputs.SourceContext.Slice(0xA0);
        throw new InvalidDataException("Particle's source emitter has no prepared parent transform context.");
    }

    public ReadOnlyMemory<byte> ResolveParentState(G1EffectRecord generic,Memory<byte> particle)
    {
        if(slotStorage.TryGetValue(particle,out var slot)) return ResolveParentState(slot);
        throw new InvalidOperationException("Particle storage is not allocated by this generic.");
    }

    private int Int(int at) => BinaryPrimitives.ReadInt32LittleEndian(CurrentGenericBytes.AsSpan(at));
    private static void Reference(G1EffectParticleSlot slot, int change)
    {
        var field = slot.Bytes.Span[0x68..];
        BinaryPrimitives.WriteInt32LittleEndian(field,
            unchecked(BinaryPrimitives.ReadInt32LittleEndian(field) + change));
    }
    private IEnumerable<ReadOnlyMemory<byte>> Controllers(int countField)
    {
        int count = Int(countField);
        if (count == 0) yield break;
        int table = checked(countField + 4 + Int(countField + 4));
        for (int index = 0; index < count; index++)
        {
            int entry = checked(table + index * 4), at = checked(entry + Int(entry));
            int length = Int(at);
            if (at < 0 || length < 8 || at > CurrentGenericBytes.Length - length)
                throw new InvalidDataException("Generic controller escapes its declaration.");
            yield return CurrentGenericBytes.AsMemory(at, length);
        }
    }
    private void AdvanceParticle(G1EffectParticleSlot slot, float seconds)
    {
        uint type = Record.Type!.Value;
        if (type == 10) return;
        int elapsed = type switch { 0 or 1 => 0x6C, 2 => 0x9C, 3 => 0x140, 7 => 0xAC,
            _ => throw new NotSupportedException($"Generic {type} update is not implemented.") };
        int stop = type == 3 ? 0x20C : 0x210;
        float old = BinaryPrimitives.ReadSingleLittleEndian(slot.Bytes.Span[elapsed..]);
        float next = old + seconds;
        if (type != 3) BinaryPrimitives.WriteSingleLittleEndian(slot.Bytes.Span[elapsed..], next);
        float finish = BinaryPrimitives.ReadSingleLittleEndian(slot.Bytes.Span[(elapsed + (type == 3 ? 4 : -4))..]);
        if ((Int(stop) & 1) != 0 && finish <= next * 1.0000119f) { Retire(slot); return; }
        foreach (var controller in Controllers(type == 3 ? 0x204 : 0x208))
            G1EffectParticleControllers.Apply(controller, slot.Bytes, seconds, context, CurrentRecord, ModuleEmissionScale,
                BinaryPrimitives.ReadSingleLittleEndian(module.Data.Span[0x34..]));
        if ((Int(stop) & 2) != 0 && (type == 3
            ? BinaryPrimitives.ReadSingleLittleEndian(slot.Bytes.Span[Int(0xBC)..]) < 0.0078125f
            : slot.Bytes.Span[0x33] < 2)) { Retire(slot); return; }
        if (type != 3)
            foreach (var controller in Controllers(0x214))
                G1EffectParticleControllers.Apply(controller, slot.Bytes, seconds, context, CurrentRecord, ModuleEmissionScale,
                    BinaryPrimitives.ReadSingleLittleEndian(module.Data.Span[0x34..]));
        if(type!=3 && (Int(stop)&0x10000)!=0 && (Int(0x208)+Int(0x214))!=0)
        {
            ushort flags=BinaryPrimitives.ReadUInt16LittleEndian(slot.Bytes.Span[6..]);
            flags=slot.Bytes.Span[0x33]<2 ? (ushort)(flags&~2):(ushort)(flags|2);
            BinaryPrimitives.WriteUInt16LittleEndian(slot.Bytes.Span[6..],flags);
        }
        int motionField = type == 3 ? 0x200 : 0x204;
        if (Int(motionField) != 0)
        {
            if (ParticleMotion is null)
                throw new NotSupportedException("Generic particle-motion descriptor requires its prepared auxiliary consumer.");
            ParticleMotion(CurrentRecord, slot.Bytes, seconds);
        }
        if (type == 3)
        {
            SynchronizeNestedTransform(slot,seconds);
            PutFloat(slot.Bytes,0x140,next);
            return;
        }
        int state = type switch { 0 or 1 => 0x60, 2 => 0x90, _ => 0xA0 };
        var expression = G1EffectUvAnimation.Read(Declaration);
        if (BinaryPrimitives.ReadUInt32LittleEndian(expression.Span) != 4)
            G1EffectUvAnimation.Update(expression.Span, slot.Bytes.Span.Slice(state, 16));
        uint frame = unchecked((uint)(long)BinaryPrimitives.ReadSingleLittleEndian(slot.Bytes.Span[(state + 4)..]));
        G1EffectUvAnimation.Evaluate(expression.Span, slot.Bytes.Span.Slice(0x40, 16), frame, next);
    }

    private void SynchronizeNestedTransform(G1EffectParticleSlot slot,float seconds)
    {
        var next=Matrix(slot.Bytes,0x20);
        var inverse=Inverse(next);
        Matrix4x4 velocity=default;
        if(preparedFrames.Contains(slot) && seconds>0)
        {
            velocity=Multiply(Matrix(slot.Bytes,0x70),inverse);
            velocity.M11-=1;velocity.M22-=1;velocity.M33-=1;velocity.M44-=1;
            velocity*=(-1f/seconds);
        }
        PutMatrix(slot.Bytes,0xB0,inverse);
        PutMatrix(slot.Bytes,0xF0,velocity);
        PutMatrix(slot.Bytes,0x70,next);
        preparedFrames.Add(slot);
    }
}
