using San14.Data;

/// <summary>Actual successful54 and55 primitive snapshots. Only the final
/// layer receipt remains current; the54 snapshot records completed work
/// without pretending its superseded receipt is still current.</summary>
public sealed class OriginalNativeMapStaticQueuesCompletedFrame
{
    internal OriginalNativeMapStaticQueuesCompletedFrame(OriginalNativeMapStaticQueuePlan plan,
        OriginalModelGeometrySubmittedFrame finalSubmission,
        OriginalModelGeometrySubmittedPrimitive[] actualPrimitives)
        => (Plan, FinalSubmission, ActualPrimitives) =
            (plan, finalSubmission, Array.AsReadOnly(actualPrimitives));
    public OriginalNativeMapStaticQueuePlan Plan { get; }
    public OriginalModelGeometrySubmittedFrame FinalSubmission { get; }
    public IReadOnlyList<OriginalModelGeometrySubmittedPrimitive> ActualPrimitives { get; }
    public OriginalModelGeometrySubmittedPrimitive? LastActualPrimitive => ActualPrimitives.LastOrDefault();

    public void ValidateCurrent(OriginalNativeMapFrameContext context,
        OriginalMapSourceCreatedF103CurrentFrame f103)
    {
        FinalSubmission.RequireCurrent();
        if (!ReferenceEquals(context, Plan.Context) || !ReferenceEquals(f103, Plan.Admission.Frame) ||
            !ReferenceEquals(context.TerrainFrame, FinalSubmission.Target))
            throw new InvalidDataException("Static54/55 completion belongs to another current context, target or F103 preparation.");
    }
}
