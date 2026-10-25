using System.Numerics;

namespace San14.Data;

public enum MapEffectAdmissionPool { Normal, Dedicated }

public enum MapEffectAdmissionStatus
{
    Allocated, InvalidKind, MissingSource, UnsupportedVariant, CameraLevel,
    PoolExhausted, DegenerateTransform, InstanceAlreadyPresent
}

/// <summary>A source slot snapshot. Its existence does not certify a renderer
/// instance, uploaded resource, accepted publication or completed GPU work.</summary>
public readonly record struct MapEffectAdmissionSlot(MapEffectAdmissionPool Pool,
    int SlotIndex, int Kind, uint LifecycleFlags, bool HasInstance, Matrix4x4 Transform);

/// <summary>One allocation's identity within a map's shared manager. A retired
/// handle cannot address a later allocation which reuses the physical slot.</summary>
public sealed class MapEffectAdmissionHandle
{
    internal MapEffectAdmissionHandle(MapEffectAdmissionManager manager, MapEffectAdmissionPool pool,
        int slotIndex, int kind, uint variant)
    { Manager = manager; Pool = pool; SlotIndex = slotIndex; Kind = kind; Variant = variant; }
    internal MapEffectAdmissionManager Manager { get; }
    public MapEffectAdmissionPool Pool { get; }
    public int SlotIndex { get; }
    public int Kind { get; }
    public uint Variant { get; }
}

public sealed record MapEffectAdmissionAttempt(MapEffectAdmissionStatus Status,
    MapEffectAdmissionHandle? Handle, MapEffectAdmissionSlot? ReturnedSlot)
{
    /// <summary>The shared source slot was initialized; no renderer acceptance
    /// or live particle is implied.</summary>
    public bool Allocated => Status == MapEffectAdmissionStatus.Allocated;
    public bool KnownSkip => !Allocated;
}

/// <summary>The actual Main camera used by the manager's origin admission.
/// ClipMatrix is Main's ordinary projection product, not the particle writer's
/// far-normalized culling camera. CameraLevel is the current continuous zoom.</summary>
public readonly record struct MapEffectAdmissionView(Matrix4x4 ClipMatrix, Vector3 Eye, float CameraLevel);

/// <summary>One map's shared source manager slots. It owns admission and slot
/// identity; the host owns instance advancement, graphics work and disposal.
/// No loading result is interpreted as native acceptance.</summary>
public sealed partial class MapEffectAdmissionManager
{
    public const int NormalCapacity = 2080, DedicatedCapacity = 512;
    private const uint Active = 1, Pinned = 2, Held = 4, Completed = 8,
        StopRequested = 0x10, StopIssued = 0x20, SingleAdmission = 0x40, Disabled = 0x80;
    private sealed class Slot
    {
        internal int Kind = -1;
        internal uint Flags;
        internal object? Source;
        internal uint Variant;
        internal Matrix4x4 Transform = Matrix4x4.Identity;
        internal G1EffectInstance? Instance;
        internal MapEffectAdmissionHandle? Handle;
    }
    private readonly Slot[] normal = CreateSlots(NormalCapacity), dedicated = CreateSlots(DedicatedCapacity);
    private readonly G1EffectInfoRow[] declarations;
    public int NormalCursor { get; private set; }
    public int DedicatedCursor { get; private set; }

    public MapEffectAdmissionManager(LoadedMapEffectInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        declarations = Enumerable.Range(0, LoadedMapEffectInfo.RowCount).Select(info.ReadRow).ToArray();
    }

    private static Slot[] CreateSlots(int count) => Enumerable.Range(0, count).Select(_ => new Slot()).ToArray();

    public MapEffectAdmissionAttempt TryAllocate(int kind, object? source, Matrix4x4 transform,
        float sourceCameraLevel, uint variant = 0)
    {
        if ((uint)kind >= LoadedMapEffectInfo.RowCount) return new(MapEffectAdmissionStatus.InvalidKind, null, null);
        var declaration = declarations[kind];
        // This historical "quality" byte is a Main-camera zoom threshold.
        // Only the dedicated-pool initial allocation uses this early filter.
        if (!declaration.AcceptsQuality(sourceCameraLevel)) return new(MapEffectAdmissionStatus.CameraLevel, null, null);
        if (source is null) return new(MapEffectAdmissionStatus.MissingSource, null, null);
        if (variant >= 4) return new(MapEffectAdmissionStatus.UnsupportedVariant, null, null);
        var pool = declaration.UsesDedicatedPool ? MapEffectAdmissionPool.Dedicated : MapEffectAdmissionPool.Normal;
        Slot[] slots = pool == MapEffectAdmissionPool.Normal ? normal : dedicated;
        int cursor = pool == MapEffectAdmissionPool.Normal ? NormalCursor : DedicatedCursor;
        for (int offset = 0; offset < slots.Length; offset++)
        {
            int index = pool == MapEffectAdmissionPool.Normal
                ? (cursor + offset) % NormalCapacity : (cursor + offset) & (DedicatedCapacity - 1);
            var slot = slots[index];
            if ((slot.Flags & Pinned) != 0 || (uint)slot.Kind < LoadedMapEffectInfo.RowCount || (slot.Flags & Held) != 0) continue;
            // The source returns the selected slot even when initialization
            // does nothing, and advances the OLD cursor, not the selected index.
            if (pool == MapEffectAdmissionPool.Normal) NormalCursor = cursor < NormalCapacity ? cursor + 1 : 0;
            else DedicatedCursor = cursor < DedicatedCapacity ? cursor + 1 : 0;
            if (slot.Instance is not null)
                return new(MapEffectAdmissionStatus.InstanceAlreadyPresent, null, Snapshot(pool, index, slot));
            if (Determinant(transform) == 0f)
                return new(MapEffectAdmissionStatus.DegenerateTransform, null, Snapshot(pool, index, slot));
            slot.Kind = kind; slot.Variant = variant; slot.Source = source;
            slot.Transform = transform; slot.Flags = Active | (pool == MapEffectAdmissionPool.Normal ? Held : 0);
            slot.Handle = new(this, pool, index, kind, variant);
            return new(MapEffectAdmissionStatus.Allocated, slot.Handle, Snapshot(pool, index, slot));
        }
        return new(MapEffectAdmissionStatus.PoolExhausted, null, null);
    }

    public bool IsValid(MapEffectAdmissionHandle handle) => Find(handle) is { } slot && (uint)slot.Kind < LoadedMapEffectInfo.RowCount;
    public bool HasInstance(MapEffectAdmissionHandle handle) => Find(handle) is { Instance: not null } slot && (uint)slot.Kind < LoadedMapEffectInfo.RowCount;
    public bool IsCompleted(MapEffectAdmissionHandle handle) => Find(handle) is { } slot && (slot.Flags & Completed) != 0;
    public MapEffectAdmissionSlot? ReadSlot(MapEffectAdmissionHandle handle) => Find(handle) is { } slot
        ? Snapshot(handle.Pool, handle.SlotIndex, slot) : null;

    public bool TryUpdateTransform(MapEffectAdmissionHandle handle, object? source, Matrix4x4 transform)
    {
        if (Find(handle) is not { } slot || !ReferenceEquals(source, slot.Source) || Determinant(transform) == 0f) return false;
        slot.Transform = transform; return true;
    }

    public bool AttachInstance(MapEffectAdmissionHandle handle, G1EffectInstance actualInstance)
    {
        ArgumentNullException.ThrowIfNull(actualInstance);
        if (Find(handle) is not { Instance: null } slot || (slot.Flags & Active) == 0 || !AllowsInstance(slot)) return false;
        if (actualInstance.Resources.Kind != slot.Kind) throw new ArgumentException("The actual instance belongs to another effect kind.", nameof(actualInstance));
        slot.Instance = actualInstance; return true;
    }

    private bool AllowsInstance(Slot slot) => (declarations[slot.Kind].Flags & ((slot.Flags & Held) == 0 ? 1 : 2)) != 0;
    private Slot? Find(MapEffectAdmissionHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!ReferenceEquals(handle.Manager, this)) return null;
        var slot = (handle.Pool == MapEffectAdmissionPool.Normal ? normal : dedicated)[handle.SlotIndex];
        return ReferenceEquals(slot.Handle, handle) ? slot : null;
    }
    private static MapEffectAdmissionSlot Snapshot(MapEffectAdmissionPool pool, int index, Slot slot) =>
        new(pool, index, slot.Kind, slot.Flags, slot.Instance is not null, slot.Transform);
    private static void Reset(Slot slot)
    {
        slot.Kind = -1; slot.Flags = 0; slot.Source = null; slot.Variant = 0;
        slot.Transform = Matrix4x4.Identity; slot.Instance = null; slot.Handle = null;
    }
    private static void Release(Slot slot, Action<G1EffectInstance> release)
    {
        if (slot.Instance is not { } actual) return;
        release(actual); // A failure retains the real owner and prevents reuse.
        slot.Instance = null;
    }
    private static float Determinant(Matrix4x4 value) => OrdinaryTacticBeamMath.TransformDeterminant(value);
}
