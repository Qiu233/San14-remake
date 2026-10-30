namespace San14.Data;

/// <summary>Current field, wrapper and selected Frame inputs consumed by
/// field.v430. Nullable values are required only on their reached source branch.</summary>
public sealed record ReflectionCaptureFieldScheduleInputs(
    uint Flags74, uint Next6C, uint Count70, uint ContextRequests114,
    bool SceneFramePresent, uint? ActualView0Mode15A0,
    bool WrapperPresent, int? WrapperNextIndexBC,
    ReflectionCaptureFieldDimensions? ActualManagerDimensions);

public sealed record ReflectionCaptureFieldScheduleResult(
    int CurrentStart68, uint Flags74, uint ContextRequests114, uint EffectiveMode);

/// <summary>F36C40 field.v430 scheduling before its v420 registration call.
/// This selects the actual capture start and advances the source request bits;
/// it does not register a View, execute capture/filter work or attest readiness.</summary>
public static class ReflectionCaptureFieldSchedule
{
    public static ReflectionCaptureFieldScheduleResult Prepare(ReflectionCaptureFieldScheduleInputs current)
    {
        ArgumentNullException.ThrowIfNull(current);
        uint mode = (current.Flags74 >> 8) & 3u;
        if (mode == 2)
            mode = current.SceneFramePresent
                ? current.ActualView0Mode15A0 ?? throw Missing("current_Frame_View0_15A0")
                : 0;

        uint request = current.Flags74 & 7u;
        int start = -1;
        if (mode == 0 && current.WrapperPresent)
            start = request == 0
                ? current.WrapperNextIndexBC ?? throw Missing("current_wrapper_BC")
                : unchecked((int)(request - 1));
        else if (mode == 1 && current.Count70 != 0)
        {
            // The source dereferences wrapper.v60 on this branch; the product
            // is manager Z*Y*X with each IMUL retaining only its low32 bits.
            if (!current.WrapperPresent) throw Missing("current_wrapper_v60");
            var dimensions = current.ActualManagerDimensions ?? throw Missing("current_manager_dimensions");
            uint population = unchecked(dimensions.Z * dimensions.Y * dimensions.X);
            if (current.Next6C < population) start = unchecked((int)current.Next6C);
        }

        uint context = current.ContextRequests114;
        if (mode == 0 && request != 0) context |= 2u;
        uint flags = current.Flags74;
        if (request != 0)
            flags = (unchecked(flags + 1u) & 3u) | (flags & 0xFFFFFFF8u);
        return new(start, flags, context, mode);
    }

    private static InvalidDataException Missing(string owner) => new("reflection.field_schedule." + owner);
}
