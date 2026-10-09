namespace San14.Data;

public sealed record SkyFirstGraphicsPrefixCurrent(long FrameNumber,
    string ViewIdentity, bool? NoColorMaskWriterBetweenConstructorAndWork,
    string CurrentProducerProvenance);

/// <summary>First owned GraphicsContext prefix only. Its original constructor
/// initializes RT0 RGBA15; the actual D3A850 Sky stage forces Cull1. Beginning
/// WORK consumes the fresh scope even when the subsequent GPU submission fails.</summary>
public sealed class SkyFirstGraphicsPrefix
{
    internal SkyFirstGraphicsPrefix(SkyFirstGraphicsPrefixSource source,
        SkyFirstGraphicsPrefixCurrent current)
    { Source=source; Current=current; }
    public SkyFirstGraphicsPrefixSource Source { get; }
    public SkyFirstGraphicsPrefixCurrent Current { get; }
    public long FrameNumber => Current.FrameNumber;
    public string ViewIdentity => Current.ViewIdentity;
    public string OwnedFrontendIdentity => Source.OwnedFrontendIdentity;
    public uint ColorWriteMask => 15;
    public uint SkyStarNativeCullMode => 1;
    public string SourceProvenance => Source.SourceProvenance+";"+Current.CurrentProducerProvenance+
        ";first-owned-prefix-only;no-GPU-receipt/no-next-frame-history";
}

public sealed class SkyFirstGraphicsPrefixSource
{
    private SkyFirstGraphicsPrefix? prepared;
    private bool valid=true,started;
    private SkyFirstGraphicsPrefixSource(string locale,string frontend,string view)
    { Locale=locale; OwnedFrontendIdentity=frontend; OwnedViewIdentity=view; }
    public string Locale { get; }
    public string RuleVersion => RenderRuleIdentity.Version;
    public string OwnedFrontendIdentity { get; }
    public string OwnedViewIdentity { get; }
    public bool WorkSubmissionStarted => started;
    public string SourceProvenance => RenderRuleIdentity.For("first-graphics-prefix",Locale)+";"+
        "95DC10-95DF26-966EA0;8slots-RGB7/Alpha1=15;CtorCull2-is-not-SkyCull;"+
        "D3A850-D3AB5F-D3AB7D-opcode1D-Cull1;E40540-11979F0-118EF80;"+OwnedFrontendIdentity;

    public static SkyFirstGraphicsPrefixSource ReadFresh(string locale,string ownedFrontendIdentity,string ownedViewIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedFrontendIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedViewIdentity);
        if(locale is not("SC" or "TC"))throw new InvalidDataException("Sky graphics prefix requires exact SC or TC.");
        return new(locale,ownedFrontendIdentity,ownedViewIdentity);
    }

    /// <summary>Resource upload and cached draw construction do not consume
    /// this prefix. A later frame or changed current publication requires its
    /// actual mask writer/restore history rather than a second fresh seed.</summary>
    public SkyFirstGraphicsPrefix PrepareFirstPrefix(SkyFirstGraphicsPrefixCurrent current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if(!valid || started || current.FrameNumber<1 || current.ViewIdentity!=OwnedViewIdentity ||
            current.NoColorMaskWriterBetweenConstructorAndWork!=true ||
            string.IsNullOrWhiteSpace(current.CurrentProducerProvenance))
            throw new InvalidDataException("Sky first-prefix requires this fresh owned frontend and complete absence of an intervening color-mask writer; subsequent/current history is unresolved.");
        if(prepared is not null)
        {
            if(prepared.Current!=current)
                throw new InvalidDataException("First Sky graphics prefix was already bound to another render attempt/publication.");
            return prepared;
        }
        return prepared=new(this,current);
    }

    /// <summary>Call immediately before the first actual WORK submission,
    /// including an attempt that later fails. Bootstrap allocation/upload is
    /// not submission. No successful GPU execution is asserted here.</summary>
    public void ConsumeBeforeWorkSubmission(SkyFirstGraphicsPrefix actualPrefix)
    {
        ArgumentNullException.ThrowIfNull(actualPrefix);
        if(!valid || started || !ReferenceEquals(actualPrefix,prepared) || !ReferenceEquals(actualPrefix.Source,this))
            throw new InvalidDataException("Sky graphics first-prefix is foreign, consumed or invalidated.");
        started=true;
    }
    public void MarkImportedEditedOrUnknownMaskWriter(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter);valid=false; }
}
