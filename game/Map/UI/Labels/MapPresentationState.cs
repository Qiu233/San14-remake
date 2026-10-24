using San14.Data;

/// <summary>The application owns MapInfo and its layout-manager state across
/// World replacement and renderer reload. These are presentation lifetimes.</summary>
internal sealed class MapPresentationState
{
    private int nextLayout;
    private SceneryMapPolicy policy=new(0,6,6,null);
    internal event Action? Changed;
    internal SceneryMapPolicy Policy=>policy;
    internal MapSceneSelectionPolicy SelectionPolicy {get;private set;}=new(false,true,null);
    // The actually constructed manager clears both source fields. No runtime
    // foreign owner is queried or inferred from a rendering receipt.
    internal bool FieldRefreshSuppressed {get;private set;}
    internal bool AlphaRefreshSuppressed {get;private set;}
    // The source application image initializes its shared projection policy
    // to zero. Keep that fresh value across resident-map lifetimes; map entry
    // is not a writer for this independent application policy.
    internal bool? ProjectionClipBypass {get;private set;}=false;
    internal int AllocateLayoutIdentity()=>checked(++nextLayout);
    internal void SetBaseInformationVisible(bool visible)
    {
        policy = policy with
        {
            Category16 = (byte)(visible ? policy.Category16 | 4 : policy.Category16 & ~4),
            Flags15 = visible ? (byte)(policy.Flags15 & ~4) : policy.Flags15
        };
        Changed?.Invoke();
    }
    internal void SetDiplomacyInformationVisible(bool visible)
    {
        policy=policy with
        {
            Flags15=(byte)(visible?policy.Flags15|4:policy.Flags15&~4),
            Category16=visible?(byte)(policy.Category16&~4):policy.Category16
        };
        Changed?.Invoke();
    }
    internal SceneryMapPresentation Capture()=>new(policy,FieldRefreshSuppressed,
        AlphaRefreshSuppressed,ProjectionClipBypass);

    /// <summary>This implementation's explicit ordinary UI handoff, after
    /// validating the session/view. It is not all original intro/tutorial paths.</summary>
    internal void EnterOrdinaryMap()
    {
        policy=new(0,(byte)(policy.Flags15&~2),(byte)(policy.Category16&~2),false);
        SelectionPolicy=new(true,true,null);
        Changed?.Invoke();
    }

    internal void BeginResidentMap()
    {
        policy=new(0,6,6,null);
        SelectionPolicy=new(false,true,null);
        Changed?.Invoke();
    }

    internal void LeaveResidentMap()
    {
        SelectionPolicy=new(false,true,null);
        Changed?.Invoke();
    }
}

internal sealed record MapSceneSelectionPolicy(bool? Enabled,bool FilterKnown,
    Func<HexReference?,bool?>? Filter)
{
    internal bool? Allows(HexReference? candidate)=>Enabled switch
    {
        false=>false,
        true=>FilterKnown?(Filter is null?true:Filter(candidate)):null,
        null=>null
    };
}

internal sealed record SceneryMapPresentation(SceneryMapPolicy Policy,
    bool FieldRefreshSuppressed,bool AlphaRefreshSuppressed,bool? ProjectionClipBypass);

/// <summary>Known null is an actual absent Hex; unknown retains a missing
/// ray/selection policy instead of borrowing the clicked command Hex.</summary>
internal readonly record struct SceneryMapHexSelection(bool Known,HexReference? Hex);
