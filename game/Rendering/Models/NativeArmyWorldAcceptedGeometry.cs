using San14.Data;

/// <summary>The actual49/50 Army consumer join. Callers use this helper after
/// source pop52 and later geometry phases; it borrows the same current target
/// generation without demanding node48 remains bound or inventing node53
/// ordinary/flag receipts.</summary>
public static class NativeArmyWorldAcceptedGeometry
{
    public static ArmyWorldQueuesCompletedPublication RequireCurrent(
        NativeMapArmyPopulationAcceptedFrame actualArmy,
        NativeMapFrameContext actualContext, MapSourceCreatedF103CurrentFrame actualF103)
    {
        ArgumentNullException.ThrowIfNull(actualArmy); ArgumentNullException.ThrowIfNull(actualContext);
        ArgumentNullException.ThrowIfNull(actualF103);
        if (actualArmy.WorldQueuesCompletedPublication is not { } completed ||
            actualArmy.NativePrimitiveAdmission is not { } admission ||
            !ReferenceEquals(actualArmy.Prepared.Context, actualContext) ||
            !ReferenceEquals(admission.Population.Prepared, actualArmy.Prepared) ||
            !ReferenceEquals(completed.BoundTarget48.Context, actualContext))
            throw new InvalidDataException("Army World consumer requires its actual accepted49/50 parent, current F103 and node48 target generation.");
        WorldGeometryShadingPhase.RequireSameGeometrySnapshot(admission.Frame, actualF103);
        admission.ValidateCurrentAccepted(actualArmy); return completed;
    }
}
