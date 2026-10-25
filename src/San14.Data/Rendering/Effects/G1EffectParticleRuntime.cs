using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Mutable ownership of one generic and its shared child generics.
/// Only a completely executed birth and initial update can publish a slot.
/// Unsupported writers fail explicitly and release the uninitialized slot.</summary>
public sealed partial class G1EffectGenericRuntime : IDisposable
{
    private readonly G1EffectContext context;
    private readonly Dictionary<G1EffectParticleSlot, G1EffectParticleSlot?> initialized = [];
    private bool disposed;
    public G1EffectRecord Record { get; }
    public G1EffectRecord CurrentRecord { get; }
    public byte[] CurrentGenericBytes { get; }
    public G1EffectParticleDeclaration Declaration { get; }
    public G1EffectParticlePool Pool { get; }
    public IReadOnlyList<G1EffectGenericRuntime> Children { get; }
    public bool HasAllocatedPool => Pool.BlockCount != 0;
    public bool IsAlive => initialized.Count != 0 || Children.Any(child => child.IsAlive);
    /// <summary>The real prepared particle-motion consumer, when present.</summary>
    public Action<G1EffectRecord, Memory<byte>, float>? ParticleMotion { get; set; }

    public G1EffectGenericRuntime(G1EffectModule module, G1EffectRecord generic, G1EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(module); ArgumentNullException.ThrowIfNull(generic);
        ArgumentNullException.ThrowIfNull(context);
        this.context = context; Record = generic;
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
            throw new NotSupportedException($"Generic {Record.Type} direct-birth preparation and submission are not implemented.");
        var slot = Pool.TryAcquire();
        if (slot is null && context.Arena.TryAllocateParticleBlock(Pool)) slot = Pool.TryAcquire();
        if (slot is null) return null;
        bool parentReferenced = false;
        try
        {
            ExecuteBirth(slot, definition, inputs, ref parentReferenced);
            AdvanceParticle(slot, initialAgeSeconds);
            if (slot.IsAllocated) initialized.Add(slot, parentReferenced ? inputs.ParentSlot : null);
            else if (parentReferenced && inputs.ParentSlot is { } parent) Reference(parent, -1);
            return slot.IsAllocated ? slot : null;
        }
        catch
        {
            if (parentReferenced && inputs.ParentSlot is { } parent) Reference(parent, -1);
            if (slot.IsAllocated) Pool.Release(slot);
            throw;
        }
    }

    public void Advance(float seconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Record.Type == 10) return; // The source generic update dispatch is empty.
        if (Declaration.UsesDirectBirth)
            throw new NotSupportedException($"Generic {Record.Type} direct-birth update is not implemented.");
        foreach (var slot in Pool.EnumerateAllocated())
        {
            if (!initialized.ContainsKey(slot)) continue;
            if ((BinaryPrimitives.ReadUInt16LittleEndian(slot.Bytes.Span[6..]) & 4) == 0) Retire(slot);
            else AdvanceParticle(slot, seconds);
        }
        foreach (var child in Children) child.Advance(seconds);
        if (Record.Type == 3 && Int(0x218) != 0 && initialized.Count != 0)
            throw new NotSupportedException("Nested type-three controller emission requires its actual per-parent birth consumer.");
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
        if (slot.IsAllocated) Pool.Release(slot);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var child in Children) child.Dispose();
        foreach (var slot in EnumerateInitialized()) Retire(slot);
        context.Arena.ReleasePool(Pool);
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
            G1EffectParticleControllers.Apply(controller, slot.Bytes, seconds, context, CurrentRecord);
        if ((Int(stop) & 2) != 0 && (type == 3
            ? BinaryPrimitives.ReadSingleLittleEndian(slot.Bytes.Span[Int(0xBC)..]) < 0.0078125f
            : slot.Bytes.Span[0x33] < 2)) { Retire(slot); return; }
        if (type != 3)
            foreach (var controller in Controllers(0x214))
                G1EffectParticleControllers.Apply(controller, slot.Bytes, seconds, context, CurrentRecord);
        int motionField = type == 3 ? 0x200 : 0x204;
        if (Int(motionField) != 0)
        {
            if (ParticleMotion is null)
                throw new NotSupportedException("Generic particle-motion descriptor requires its prepared auxiliary consumer.");
            ParticleMotion(CurrentRecord, slot.Bytes, seconds);
        }
        if (type == 3)
            throw new NotSupportedException("Type-three transform synchronization and nested emitter lifetime update are not implemented.");
        int state = type switch { 0 or 1 => 0x60, 2 => 0x90, _ => 0xA0 };
        var expression = G1EffectUvAnimation.Read(Declaration);
        if (BinaryPrimitives.ReadUInt32LittleEndian(expression.Span) != 4)
            G1EffectUvAnimation.Update(expression.Span, slot.Bytes.Span.Slice(state, 16));
        uint frame = unchecked((uint)(long)BinaryPrimitives.ReadSingleLittleEndian(slot.Bytes.Span[(state + 4)..]));
        G1EffectUvAnimation.Evaluate(expression.Span, slot.Bytes.Span.Slice(0x40, 16), frame, next);
    }
}
