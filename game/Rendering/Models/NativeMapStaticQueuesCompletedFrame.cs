using San14.Data;

/// <summary>Actual successful54 and55 primitive snapshots. Only the final
/// layer receipt remains current; the54 snapshot records completed work
/// without pretending its superseded receipt is still current.</summary>
public sealed class NativeMapStaticQueuesCompletedFrame
{
    internal NativeMapStaticQueuesCompletedFrame(NativeMapStaticQueuePlan plan,
        ModelGeometrySubmittedFrame finalSubmission,
        ModelGeometrySubmittedPrimitive[] actualPrimitives)
        => (Plan, FinalSubmission, ActualPrimitives) =
            (plan, finalSubmission, Array.AsReadOnly(actualPrimitives));
    public NativeMapStaticQueuePlan Plan { get; }
    public ModelGeometrySubmittedFrame FinalSubmission { get; }
    public IReadOnlyList<ModelGeometrySubmittedPrimitive> ActualPrimitives { get; }
    public ModelGeometrySubmittedPrimitive? LastActualPrimitive => ActualPrimitives.LastOrDefault();

    public void ValidateCurrent(NativeMapFrameContext context,
        MapSourceCreatedF103CurrentFrame f103)
    {
        FinalSubmission.RequireCurrent();
        if (!ReferenceEquals(context, Plan.Context) || !ReferenceEquals(f103, Plan.Admission.Frame) ||
            !ReferenceEquals(context.TerrainFrame, FinalSubmission.Target))
            throw new InvalidDataException("Static54/55 completion belongs to another current context, target or F103 preparation.");
    }
}
