namespace San14.Data;

public enum OfficerPortraitRouteKind { Hidden, StockNamed, Unresolved }

/// <summary>A current bank10 resource identity. StockNamed authorizes its exact
/// declared resource/sprite pair, not a fallback on resource loading failure.</summary>
public sealed record OfficerPortraitRoute(int OfficerId, OfficerPortraitRouteKind Kind,
    ushort? CurrentAppearanceIndex, int? ResolvedAppearanceIndex,
    string? ResourceName, string? SpriteName, int? EligibilityPersonId, string? UnresolvedInput)
{
    //Actual bank10 input after live content/app/user gates, before static
    //portrait-name remapping. A saved presentation may retain this decision.
    public ushort? AdmittedAppearanceIndex {get;init;}
}

internal static class OrdinaryPortraitEligibility
{
    internal static bool NeedsApplicationSetting(int personId)
        => (uint)(personId - 2003) <= 15 && ((0xFBFFu >> (personId - 2003)) & 1) != 0 ||
            (uint)(personId - 2028) <= 60 && ((0x1F83FF0000022019ul >> (personId - 2028)) & 1) != 0;

    internal static bool NeedsUnlock(int personId) => (uint)(personId - 2078) < 5;

    //For bank10's second argument0: the remaining47 identities do not consume
    //external setting/unlock bits. Scenario63 covers all36 setting identities.
    internal static bool IsKnownAllowed(int personId, int? currentScenario)
        => !NeedsApplicationSetting(personId) && !NeedsUnlock(personId) ||
            currentScenario == 63 && !NeedsUnlock(personId);
}
