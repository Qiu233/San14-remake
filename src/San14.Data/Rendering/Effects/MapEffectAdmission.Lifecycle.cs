using System.Numerics;

namespace San14.Data;

public sealed partial class MapEffectAdmissionManager
{
    /// <summary>Update ordinary origin visibility and camera admission before
    /// creation. True requests an actual instance; it does not certify one.</summary>
    public bool UpdateAdmission(MapEffectAdmissionHandle handle, MapEffectAdmissionView view)
    {
        if (Find(handle) is not { } slot || (uint)slot.Kind >= LoadedMapEffectInfo.RowCount || (slot.Flags & Disabled) != 0) return false;
        if ((slot.Flags & SingleAdmission) != 0) return (slot.Flags & Active) != 0 && slot.Instance is null && AllowsInstance(slot);
        if (slot.Variant is >= 1 and <= 3) slot.Flags |= Active;
        else
        {
            Vector4 p = new(slot.Transform.M41, slot.Transform.M42, slot.Transform.M43, slot.Transform.M44);
            Matrix4x4 m = view.ClipMatrix;
            Vector4 clip = new(
                ((p.Y*m.M21 + p.X*m.M11) + p.Z*m.M31) + p.W*m.M41,
                ((p.Y*m.M22 + p.X*m.M12) + p.Z*m.M32) + p.W*m.M42,
                ((p.Y*m.M23 + p.X*m.M13) + p.Z*m.M33) + p.W*m.M43,
                ((p.Y*m.M24 + p.X*m.M14) + p.Z*m.M34) + p.W*m.M44);
            bool visible = -clip.W <= clip.X && clip.X <= clip.W && -clip.W <= clip.Y && clip.Y <= clip.W &&
                -clip.W <= clip.Z && clip.Z <= clip.W;
            if (visible && view.CameraLevel <= 2f)
            {
                float x = view.Eye.X - p.X, y = view.Eye.Y - p.Y, z = view.Eye.Z - p.Z;
                visible = MathF.Sqrt((x*x + y*y) + z*z) <= view.CameraLevel * 5000f + 15000f;
            }
            var declaration = declarations[slot.Kind];
            bool admitted = visible && ((declaration.Flags & 4) != 0 || view.CameraLevel <= declaration.QualityThreshold);
            if (admitted) slot.Flags |= Active; else slot.Flags &= ~Active;
        }
        return (slot.Flags & Active) != 0 && slot.Instance is null && AllowsInstance(slot);
    }

    public bool SetPinned(MapEffectAdmissionHandle handle, bool pinned)
    {
        if (Find(handle) is not { } slot) return false;
        if (pinned) slot.Flags |= Pinned; else slot.Flags &= ~Pinned;
        return true;
    }

    /// <summary>A stop on an uninstantiated reservation resets it immediately.
    /// An existing instance receives its stop through TryConsumeStop.</summary>
    public bool RequestStop(MapEffectAdmissionHandle handle)
    {
        if (Find(handle) is not { } slot) return false;
        if (slot.Instance is null) Reset(slot); else slot.Flags |= StopRequested;
        return true;
    }

    public bool TryConsumeStop(MapEffectAdmissionHandle handle)
    {
        if (Find(handle) is not { Instance: not null } slot || (slot.Flags & StopRequested) == 0) return false;
        slot.Flags = (slot.Flags & ~StopRequested) | StopIssued; return true;
    }

    /// <summary>After the host advances the actual instance, collect completed
    /// or inactive state. Pinned completions remain observable by their owner.
    /// Inactive normal reservations retain their kind until hard retirement.</summary>
    public bool Collect(MapEffectAdmissionHandle handle, Action<G1EffectInstance> release)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (Find(handle) is not { } slot) return false;
        if (slot.Instance is { IsInitialized: true, IsFinished: true }) slot.Flags |= Completed;
        if ((slot.Flags & Pinned) != 0) return true;
        if ((slot.Flags & Completed) != 0 || (slot.Flags & (StopRequested | StopIssued)) != 0 && (slot.Flags & Active) == 0)
        { Release(slot, release); Reset(slot); return false; }
        if ((slot.Flags & Active) == 0)
        {
            Release(slot, release);
            if ((slot.Flags & Held) == 0) { Reset(slot); return false; }
        }
        return true;
    }

    /// <summary>Hard retirement bypasses pin and held flags, after the host
    /// actually releases the instance. It is separate from native acceptance.</summary>
    public bool Retire(MapEffectAdmissionHandle handle, Action<G1EffectInstance> release)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (Find(handle) is not { } slot) return false;
        Release(slot, release); Reset(slot); return true;
    }
}
