using System.Numerics;

namespace San14.Data;

public enum NormalSoundRequestKind { Ordinary, Spatial }
public enum NormalSoundRejection { None, Mode, InvalidId, RecentDuplicate, Priority, PoolFull, ServiceUnavailable, AllocationRejected }
public enum NormalSoundRetirementKind { Replaced, Finished, Timeout, ExplicitStop }
public readonly record struct NormalSoundHandleStatus(bool HasHandle, bool Active, bool Paused);
public sealed record NormalSoundManagerContext(bool ServiceAvailable, bool ManagerReady,
    bool BackendAvailable, bool SuppressOrdinaryRequests);
public sealed record SpatialSoundMode(byte Flags, bool MapControllerActive,
    bool CameraAvailable, Matrix4x4 ViewProjection);
public sealed record NormalSoundSlotRequest(int Slot, int ManagerId, NormalSoundRequestKind Kind,
    Vector3 Position, float TimeoutUpdates);
public sealed record NormalSoundSlotSnapshot(int ManagerId, int UpdateAge,
    NormalSoundRequestKind Kind, Vector3 Position, float TimeoutUpdates);
public sealed record NormalSoundAdmission(bool Allocated, int? Slot, NormalSoundRejection Rejection);
public sealed record NormalSoundRetirement(int Slot, NormalSoundRetirementKind Kind, float FadeSeconds = 0);

/// <summary>The application's one ordinary/spatial SE manager. The host owns
/// its actual handles, including reservations during asynchronous decoding.
/// Source metadata is remembered independently until the actual manager update.</summary>
public sealed class NormalSoundManager
{
    public const int SlotCount = 62;
    public const int OrdinarySlotCount = 8;
    public const int SpatialSlotLimit = 60;
    private readonly NormalSoundSlotSnapshot?[] slots = new NormalSoundSlotSnapshot?[SlotCount];
    private readonly byte[] priorities = new byte[220];
    private int ordinaryCursor, spatialCursor = 8;
    private int minimumSpatialPriority;

    public NormalSoundSlotSnapshot? ReadSlot(int slot)
        => (uint)slot < SlotCount ? slots[slot] : throw new ArgumentOutOfRangeException(nameof(slot));

    public bool IsRecentOrdinarySound(int managerId)
        => slots.Any(slot => slot is not null && slot.ManagerId == managerId && slot.UpdateAge < 3);

    /// <summary>Publish an actual manager priority change; fresh4017C0 has
    /// minimum0 and all220 per-cue priorities0.</summary>
    public void SetSpatialPriority(int minimum, IReadOnlyList<byte> cuePriorities)
    {
        ArgumentNullException.ThrowIfNull(cuePriorities);
        if (cuePriorities.Count != priorities.Length) throw new InvalidDataException("SE priority table needs220 entries.");
        minimumSpatialPriority = minimum;
        for (int i = 0; i < priorities.Length; i++) priorities[i] = cuePriorities[i];
    }

    public NormalSoundAdmission TryAllocateOrdinary(int managerId, float timeoutUpdates,
        NormalSoundManagerContext context, Func<int, NormalSoundHandleStatus> queryHandle,
        Func<NormalSoundSlotRequest, bool> allocate, Action<NormalSoundRetirement> retire)
    {
        if (context.SuppressOrdinaryRequests) return Reject(NormalSoundRejection.Mode);
        if ((uint)managerId >= 220) return Reject(NormalSoundRejection.InvalidId);
        if (IsRecentOrdinarySound(managerId)) return Reject(NormalSoundRejection.RecentDuplicate);
        for (int attempt = 0; attempt < OrdinarySlotCount; attempt++)
        {
            int slot = ordinaryCursor; ordinaryCursor = (ordinaryCursor + 1) % OrdinarySlotCount;
            var actual = queryHandle(slot);
            if (actual.Active || actual.Paused) continue;
            return Install(new(slot, managerId, NormalSoundRequestKind.Ordinary, Vector3.Zero, timeoutUpdates),
                actual, context, allocate, retire);
        }
        return Reject(NormalSoundRejection.PoolFull);
    }

    public NormalSoundAdmission TryAllocateSpatial(int managerId, Vector3 position,
        SpatialSoundMode mode, NormalSoundManagerContext context,
        Func<int, NormalSoundHandleStatus> queryHandle, Func<NormalSoundSlotRequest, bool> allocate,
        Action<NormalSoundRetirement> retire)
    {
        //405370's actual controller and camera gates precede405260.
        if ((mode.Flags & 2) != 0 || (mode.Flags & 1) == 0 && !mode.MapControllerActive ||
            !mode.CameraAvailable || !BaseCaptureSoundSource.IsInsideAudibleClip(position, mode.ViewProjection))
            return Reject(NormalSoundRejection.Mode);
        if ((uint)managerId >= 220) return Reject(NormalSoundRejection.InvalidId);
        foreach (var previous in slots)
        {
            if (previous is null || previous.ManagerId != managerId || previous.UpdateAge > 2) continue;
            float dx = previous.Position.X - position.X, dz = previous.Position.Z - position.Z;
            if ((float)Math.Sqrt(dx * dx + dz * dz) < 2000)
                return Reject(NormalSoundRejection.RecentDuplicate);
        }
        if (minimumSpatialPriority > priorities[managerId] && managerId != 0xCD)
            return Reject(NormalSoundRejection.Priority);
        int first = spatialCursor;
        do
        {
            int slot = spatialCursor;
            var actual = queryHandle(slot);
            //406460 tests the active flags only. A paused spatial receiver
            //can be replaced; the ordinary0..7 allocator also tests paused.
            if (!actual.Active)
                return Install(new(slot, managerId, NormalSoundRequestKind.Spatial, position, 0),
                    actual, context, allocate, retire);
            spatialCursor++;
            if (spatialCursor >= SpatialSlotLimit) spatialCursor = OrdinarySlotCount;
        } while (spatialCursor != first);
        return Reject(NormalSoundRejection.PoolFull);
    }

    private NormalSoundAdmission Install(NormalSoundSlotRequest request, NormalSoundHandleStatus previous,
        NormalSoundManagerContext context, Func<NormalSoundSlotRequest, bool> allocate,
        Action<NormalSoundRetirement> retire)
    {
        if (!context.ServiceAvailable || !context.ManagerReady || !context.BackendAvailable ||
            request.Kind == NormalSoundRequestKind.Spatial && context.SuppressOrdinaryRequests)
            return Reject(NormalSoundRejection.ServiceUnavailable);
        if (!float.IsFinite(request.TimeoutUpdates)) throw new InvalidDataException("Nonfinite SE timeout.");
        if (previous.HasHandle) retire(new(request.Slot, NormalSoundRetirementKind.Replaced));
        //404C60/404FF0 retain the attempted ID/age even when the engine has
        //not produced a live handle. The host's real allocation result remains
        //separate; a selected cue is never an allocated playback receipt.
        slots[request.Slot] = new(request.ManagerId, 0, request.Kind, request.Position, request.TimeoutUpdates);
        bool allocated = allocate(request);
        return new(allocated, request.Slot, allocated ? NormalSoundRejection.None : NormalSoundRejection.AllocationRejected);
    }

    /// <summary>One invocation of408260's manager update, not elapsed wall
    /// time. Reservations must be reported by the host as actual active handles.</summary>
    public void Update(bool serviceAvailable, Func<int, NormalSoundHandleStatus> queryHandle,
        Action<NormalSoundRetirement> retire)
    {
        if (!serviceAvailable) return;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (slots[slot] is not { } remembered) continue;
            var actual = queryHandle(slot);
            if (!actual.HasHandle) { slots[slot] = null; continue; }
            bool expired = remembered.TimeoutUpdates != 0 && (float)remembered.UpdateAge >= remembered.TimeoutUpdates;
            if (expired || !actual.Active && !actual.Paused)
            {
                retire(new(slot, expired ? NormalSoundRetirementKind.Timeout : NormalSoundRetirementKind.Finished,
                    expired ? .5f : 0));
                slots[slot] = null;
            }
            else slots[slot] = remembered with { UpdateAge = unchecked(remembered.UpdateAge + 1) };
        }
    }

    public bool StopSlot(int slot, Func<int, NormalSoundHandleStatus> queryHandle,
        Action<NormalSoundRetirement> retire)
    {
        if ((uint)slot >= SlotCount || slots[slot] is null) return false;
        if (queryHandle(slot).HasHandle) retire(new(slot, NormalSoundRetirementKind.ExplicitStop));
        slots[slot] = null; return true;
    }

    public void StopAll(Func<int, NormalSoundHandleStatus> queryHandle, Action<NormalSoundRetirement> retire)
    {
        for (int slot = 0; slot < SlotCount; slot++)
            if (queryHandle(slot).HasHandle) StopSlot(slot, queryHandle, retire);
    }

    public void StopActive(Func<int, NormalSoundHandleStatus> queryHandle, Action<NormalSoundRetirement> retire)
    {
        for (int slot = 0; slot < SlotCount; slot++)
            if (queryHandle(slot).Active) StopSlot(slot, queryHandle, retire);
    }

    private static NormalSoundAdmission Reject(NormalSoundRejection reason) => new(false, null, reason);
}
