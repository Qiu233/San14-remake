using System.Numerics;

namespace San14.Data;

/// <summary>The CPU state of one actually constructed Field and its bound
/// manager/wrapper. A resource declaration alone never creates this instance.
/// Scene membership and GPU capture/filter completion have separate owners.</summary>
public sealed class ReflectionCaptureFieldInstance : IDisposable
{
    private bool disposed;
    private uint requestFlags = 0x201;
    private int wrapperNextIndex;

    public ReflectionCaptureFieldInstance(ReflectionCaptureFieldDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        LocalPosition = source.WorldPosition;
        ManagerPosition = new(source.WorldPosition, 1f);
    }

    public ReflectionCaptureFieldDefinition Source { get; }
    public Vector3 LocalPosition { get; private set; }
    public Vector4 ManagerPosition { get; private set; }
    public uint RegisteredViewMask { get; private set; }
    public int CurrentCaptureStart { get; private set; }
    public uint RequestFlags => requestFlags;
    public int WrapperNextIndex => wrapperNextIndex;

    /// <summary>F37F10 writes local XYZ and the associated manager XYZ/W1,
    /// then requests a new capture. This is the local setter; the separate
    /// parent-relative world setter must first resolve its actual parent.</summary>
    public void SetLocalPosition(Vector3 position)
    {
        RequireCurrent();
        LocalPosition = position;
        ManagerPosition = new(position, 1f);
        requestFlags = (requestFlags & 0xFFFFFFF9u) | 1u;
    }

    /// <summary>The actual Field v30 registration callback, not construction
    /// or resource discovery. The caller still owns the real Actor List join.</summary>
    public void RegisterForCurrentViews()
    {
        RequireCurrent();
        RegisteredViewMask = uint.MaxValue;
    }

    /// <summary>Runs v430 for this fresh-factory state. Source next6C remains
    /// FFFFFFFF and count70 remains0 until their separate writer is connected.
    /// A present Scene Frame in mode2 must supply its actual slot0 View.</summary>
    public ReflectionCaptureFieldScheduleResult ScheduleCurrentActor(
        uint currentContextRequests, bool sceneFramePresent,
        MapSourceCreatedViewFieldsPacket? actualFrameSlot0)
    {
        RequireCurrent();
        var result = ReflectionCaptureFieldSchedule.Prepare(new(requestFlags,
            uint.MaxValue, 0, currentContextRequests, sceneFramePresent,
            actualFrameSlot0?.Camera.ReflectionProbeRsmCaptureMode15A0,
            true, wrapperNextIndex, Source.FieldDimension));
        CurrentCaptureStart = result.CurrentStart68;
        requestFlags = result.Flags74;
        return result;
    }

    /// <summary>FD9A10's cursor write after the real capture submission.
    /// The source uses equality with low32 grid population, not a clamp.
    /// This write grants no capture, SH, filtering or shader-ready receipt.</summary>
    public void AdvanceAfterCaptureSubmission(int submittedOrdinal)
    {
        RequireCurrent();
        int next = unchecked(submittedOrdinal + 1);
        uint population = unchecked(Source.FieldDimension.Z * Source.FieldDimension.Y * Source.FieldDimension.X);
        wrapperNextIndex = unchecked((uint)next) == population ? -1 : next;
    }

    public ReflectionComputeProbeGeometry CaptureCurrentGeometry()
    {
        RequireCurrent();
        return new(Source.FieldDimension.X, Source.FieldDimension.Y, Source.FieldDimension.Z,
            Source.CellSize, Source.TileSize, Source.ViewRange,
            new(ManagerPosition.X, ManagerPosition.Y, ManagerPosition.Z));
    }

    public ReflectionCaptureFieldCellGeometry CaptureCell(uint ordinal)
    {
        RequireCurrent();
        return ReflectionCaptureFieldGeometry.ReadCell(Source, ordinal, ManagerPosition);
    }

    /// <summary>FD9230 resets BC only after successful wrapper storage setup.</summary>
    public void ResetCaptureCursorAfterFilterStorageRebuilt()
    { RequireCurrent(); wrapperNextIndex = 0; }

    public void Dispose() => disposed = true;
    public void RequireCurrent() => ObjectDisposedException.ThrowIf(disposed, this);
}
