using San14.Data;

/// <summary>Reads the renderer's actual complete submission. Split queues
/// keep successful54 primitives alongside the current55 receipt; they never
/// manufacture a legacy whole-batch stencil completion.</summary>
public static class NativeMapStaticSubmission
{
    public static IReadOnlyList<ModelGeometrySubmittedPrimitive> RequireCurrent(
        NativeMapModelPopulationAcceptedFrame population, NativeMapFrameContext context,
        MapSourceCreatedF103CurrentFrame frame)
    {
        if (!ReferenceEquals(population.Context, context) ||
            !ReferenceEquals(population.Frame.Visibility, frame.CpuPopulation?.Visibility))
            throw new InvalidDataException("Static submission belongs to another current population or scene context.");
        if (population.StaticQueueSubmission is { } queues)
        {
            var sourceGeometry = queues.Plan.Admission.Frame;
            WorldGeometryShadingPhase.RequireSameGeometrySnapshot(sourceGeometry, frame);
            queues.ValidateCurrent(context, sourceGeometry);
            return queues.ActualPrimitives;
        }
        var stencil = population.NativeStencilSubmission ??
            throw new InvalidDataException("Static renderer has no completed source submission.");
        var geometry = population.GeometrySubmission ??
            throw new InvalidDataException("Static renderer has no actual geometry submission.");
        stencil.ValidateCurrent(context, frame);
        if (!ReferenceEquals(stencil.ActualSubmission, geometry))
            throw new InvalidDataException("Static stencil and geometry do not identify the same actual batch.");
        return geometry.Primitives;
    }

    public static int UnassignedPrimitiveCount(NativeMapModelPopulationAcceptedFrame population)
        => population.StaticQueueSubmission is { } queues
            ? queues.Plan.Admission.UnassignedKind1PrimitiveCount
            : population.NativeStencilSubmission?.Submission.UnassignedPrimitiveCount ??
                throw new InvalidDataException("Static renderer has no completed source admission.");
}
