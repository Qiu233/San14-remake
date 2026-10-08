namespace San14.Data;

public sealed record OriginalStaticMapInitializationResult(
    OriginalSpecialModeForceInitializationResult SpecialModeForces,
    IReadOnlyList<OriginalBaseNeighborhoodResult> BaseNeighborhoods,
    int FinalHelperReturnValue);

/// <summary>
/// The five map-related helpers inside static32F910, in original order. Earlier
/// registration and later32F910 effects remain mandatory external stages.
/// This routine neither creates missing inputs nor makes Core ready for play.
/// </summary>
public static class OriginalStaticMapInitialization
{
    /// <summary>Static source post-load order through32EE00. Actual renderer,
    /// predicate and object-pair providers are required; worker follows.</summary>
    public static OriginalStaticMapInitializationResult ApplyThroughUnitModelPostLoad(OriginalEventInitializationContext context)
    {
        var result = ApplyThroughPostLoad(context);
        _ = context.ApplyUnitModelPostLoad();
        return result;
    }

    /// <summary>Static32F910 then324D00, in their caller's original order.
    /// The later32EE00 and new-game worker remain required before commands.</summary>
    public static OriginalStaticMapInitializationResult ApplyThroughPostLoad(OriginalEventInitializationContext context)
    {
        OriginalStaticMapInitializationResult result = ApplyStaticFunction(context);
        _ = context.ApplyStaticPostLoadFunction();
        return result;
    }

    /// <summary>Runs static32F910 including its completion tail. Every model,
    /// pool and world input must be bound explicitly. The later324D00 and
    /// new-game worker still precede player readiness.</summary>
    public static OriginalStaticMapInitializationResult ApplyStaticFunction(OriginalEventInitializationContext context)
    {
        OriginalStaticMapInitializationResult result = ApplyAfterStaticSourceLoad(context);
        context.ApplyStaticLoadCompletion();
        return result;
    }

    /// <summary>Static32F910 from its first table dispatch through its map
    /// helpers. Source loading/resets precede this call; the original completion
    /// tail and later post-load/new-game workers remain separate required stages.</summary>
    public static OriginalStaticMapInitializationResult ApplyAfterStaticSourceLoad(OriginalEventInitializationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ApplyStaticTablePostLoad();
        context.ApplyPersonalityManager();
        return ApplyRegisteredSegment(context);
    }

    /// <summary>
    /// Runs the contiguous registered portion of static32F910: twelve object
    /// registries, person aliases and force associations, doctrine ordering,
    /// base registration, then the five map helpers. The caller supplies the
    /// preceding resets, manager state and loaded source tables. Later startup
    /// effects remain required before the game can enter its command phase.
    /// </summary>
    public static OriginalStaticMapInitializationResult ApplyRegisteredSegment(OriginalEventInitializationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.InitializeObjectRegistries();
        context.InitializePostRegistrationLookups(1);
        context.RebuildRegisteredBases();
        return Apply(context);
    }

    public static OriginalStaticMapInitializationResult Apply(OriginalEventInitializationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        OriginalSpecialModeForceInitializationResult forces = OriginalSpecialModeForceInitialization.Apply(1, context);
        IReadOnlyList<OriginalBaseNeighborhoodResult> bases = context.InitializeAreaCitiesAndBaseNeighborhoods(1);
        _ = context.RebuildMapOverlays(1);
        _ = context.InitializeAreaMapStatistics(1);
        // The complete331220 nonzero-flag path returns0 without consuming state.
        // Its caller ignores that value; it is not an initialization failure.
        return new(forces, bases, 0);
    }
}
