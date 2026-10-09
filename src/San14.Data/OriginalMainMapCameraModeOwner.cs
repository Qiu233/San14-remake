namespace San14.Data;

/// <summary>Main10CF0 reset and17BD0 current288 writer for the ordinary
/// primary controller selected after CTitleState clears optional owner280.
/// This owner consumes an accepted capture; it never advances the camera.</summary>
public sealed class OriginalMainMapCameraModeOwner
{
    private long revision;
    private OriginalMainMapCameraModePacket? current;
    private int override28C=-1;
    private OriginalMainMapCameraModeOwner(string locale,string owner)
    {Locale=locale;OwnedMainIdentity=owner;}
    public string Locale{get;}
    public string RuleVersion=>RenderRuleIdentity.Version;
    public string OwnedMainIdentity{get;}
    public int CurrentOverride28C=>override28C;
    public int CurrentMode288{get;private set;} //10D22 reset0; subsequently17D3D.
    public string SourceProvenance=>$"{RenderRuleIdentity.For("main-camera-mode",Locale)};owner={OwnedMainIdentity};optional280-cleared-by-title;primary-ECb0-mode288";
    public static OriginalMainMapCameraModeOwner CreateFresh(string locale,string ownedMainIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedMainIdentity);
        _=RenderRuleIdentity.For("main-camera-mode",locale);
        return new(locale,ownedMainIdentity);
    }
    /// <summary>Explicit remake edit of the override read by17D0B. Negative
    /// values select the source automatic branch; nonnegative values are copied
    /// verbatim by the source. No original UI setter is inferred.</summary>
    public void SetCurrentOverride28C(int value)
    {override28C=value;revision=checked(revision+1);current=null;}
    public OriginalMainMapCameraModePacket PrepareCurrent(OriginalMainMapCameraFrame mainFrame)
    {
        ArgumentNullException.ThrowIfNull(mainFrame);
        if(mainFrame.Camera.ExecutableLocale!=Locale||mainFrame.Camera.FrameNumber<1||
            mainFrame.Camera.Source.Camera.ObjectId!=OriginalMainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Current mode requires this locale's accepted ordinary primary Main38D frame.");
        float index=OriginalMapSeasonMainCameraPhase.FromOwnerZoomFields(mainFrame.ZoomSelection.Band1E0,mainFrame.ZoomSelection.Fraction1E4);
        //17D17/17D2C/17E5B/17E75 use strict less-than thresholds.
        CurrentMode288=override28C>=0?override28C:index<2.5f?0:index<3.5f?1:index<4.5f?2:index<5.5f?3:4;
        revision=checked(revision+1);
        return current=new(this,mainFrame,revision,CurrentMode288,index,override28C,
            SourceProvenance+$";frame={mainFrame.Camera.FrameNumber};ECb0={index:R};override28C={override28C};writer17D3D={CurrentMode288}");
    }
    internal void Validate(OriginalMainMapCameraModePacket packet,OriginalMainMapCameraFrame frame,long capturedRevision)
    {
        if(current!=packet||revision!=capturedRevision||!ReferenceEquals(frame,packet.AcceptedMainFrame))
            throw new InvalidDataException("Main mode receipt is stale, edited or belongs to a different accepted capture.");
    }
}

public sealed class OriginalMainMapCameraModePacket
{
    private readonly OriginalMainMapCameraModeOwner owner;
    private readonly long revision;
    internal OriginalMainMapCameraModePacket(OriginalMainMapCameraModeOwner owner,OriginalMainMapCameraFrame frame,
        long revision,int mode,float index,int explicitOverride,string provenance)
    {this.owner=owner;this.revision=revision;AcceptedMainFrame=frame;Mode288=mode;EcB0Index=index;Override28C=explicitOverride;SourceProvenance=provenance;}
    public OriginalMainMapCameraFrame AcceptedMainFrame{get;}
    public long FrameNumber=>AcceptedMainFrame.Camera.FrameNumber;
    public int Mode288{get;}
    public float EcB0Index{get;}
    public int Override28C{get;}
    public string SourceProvenance{get;}
    public void ValidateCurrent(OriginalMainMapCameraModeOwner actualOwner,OriginalMainMapCameraFrame actualFrame)
    {
        ArgumentNullException.ThrowIfNull(actualOwner);ArgumentNullException.ThrowIfNull(actualFrame);
        if(!ReferenceEquals(owner,actualOwner))throw new InvalidDataException("Main mode receipt owner mismatch.");
        owner.Validate(this,actualFrame,revision);
    }
}
