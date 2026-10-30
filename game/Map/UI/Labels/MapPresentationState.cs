using San14.Data;

/// <summary>The application owns MapInfo and its layout-manager state across
/// World replacement and renderer reload. These are presentation lifetimes.</summary>
internal sealed class MapPresentationState
{
    private int nextLayout;
    private SceneryMapPolicy policy=new(0,6,6,null);
    internal event Action? Changed;
    internal SceneryMapPolicy Policy=>policy;
    internal void SetGeographicAdvantagesVisible(bool visible)
    {
        int mode=visible?1:0;
        if(policy.Mode==mode)return;
        policy=policy with {Mode=mode};
        Changed?.Invoke();
    }
    // The original persistent map manager starts with a loader-zero tag
    // mode. Leaving MapSelect restores presentation without resetting it.
    private bool intelligenceSpyTags;
    internal bool IntelligenceForceRelationTags=>!intelligenceSpyTags;
    internal void SetIntelligenceForceRelationTags(bool relationshipMode)
    {intelligenceSpyTags=!relationshipMode;}
    internal SceneryMapPolicy BeginDiplomacyTargetMode(bool force,MapTargetPresentation presentation=MapTargetPresentation.Standard)
    {
        var previous=policy;
        // MapSelect keeps only category5 for Force, or City/Gate for type2.
        policy=policy with{Mode=presentation==MapTargetPresentation.IntelligenceAwayForce?11:force?7:9,Flags15=force?(byte)4:(byte)0,Category16=0};
        Changed?.Invoke();return previous;
    }
    internal SceneryMapPolicy BeginStrategicTargetMode(StrategicTargetMapQuery query)
    {
        var previous=policy;
        // Only Force uses category5. Area and City retain their own marker
        // groups; sabotage additionally enables facility category3 in its
        // dedicated marker owner, independently of Scenery category4.
        policy=policy with{Mode=query.MarkerMode,
            Flags15=query.Kind==San14.Core.StrategicCommandTargetKind.Force?(byte)4:(byte)0,Category16=0};
        Changed?.Invoke();return previous;
    }
    internal void RestoreStrategicTargetMode(SceneryMapPolicy previous)
    { policy=previous;Changed?.Invoke(); }
    internal void RestoreDiplomacyTargetMode(SceneryMapPolicy previous)
    { policy=previous;Changed?.Invoke(); }
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
        // Title finishes AddAllMapLayout before UserStrategy entry: its exit
        // clears configured category5. The later entry only clears overrides.
        policy=policy with {Flags15=(byte)(policy.Flags15&~4)};
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
