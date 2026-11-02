using Godot;
using San14.Core;
using San14.Data;
using NMatrix = System.Numerics.Matrix4x4;

internal sealed record SceneryMapPolicy(int Mode,byte Flags15,byte Category16,bool? IsTitleState);
internal sealed record SceneryMapAlphaInputs(bool? InteractionForcesOpaque,
    bool? TextTypeOneAnimation,bool? FlagTypeOneAnimation,bool? PositionTypeThreeAnimation);

/// <summary>Actual resident Scenery children. Application delegates expose
/// this attached controller even while hidden. Worker dirty writes publish
/// pending work only; accepted stable presentation consumes it on the UI thread.</summary>
internal partial class D3D11SceneryMapLabels : Control
{
    private sealed record Child(LandmarkMapEntry Entry,SceneryMapLabelView View)
    {
        internal bool Enabled { get; set; } = true;
        internal int DisplayOverride { get; set; }
        internal int AppliedDisplay { get; set; } = -1;
        internal byte TextAlpha { get; set; } = 255;
        internal byte FlagAlpha { get; set; } = 255;
    }
    private readonly Control root=new(){Name="CanonicalSceneryLabels",MouseFilter=MouseFilterEnum.Ignore};
    private readonly List<Child> children=[];
    private readonly Dictionary<int,Child> childrenByLandmark=[];
    private readonly object lifetime=new();
    private SceneryLayoutReference reference=new(null);
    private bool attached,retired;
    private long dirtyRevision=1,consumedRevision;
    private int? priorDisplay;
    private SceneryMapPolicy? priorPolicy;
    private GameState? priorWorld;
    private SceneryMapFlagCurrentSource flags=null!;
    private Action<int>? selected;
    private Func<int,bool>? canPress;
    private MainMapCameraFrame? preparedFrame,acceptedFrame;
    private GameState? acceptedWorld;
    private sealed record PendingPresentation(GameState World,SceneryMapPolicy Policy,
        int Display,long DirtyRevision,bool Complete);
    private PendingPresentation? pending;
    private SourceWorldContext? inputCurrent;
    private SceneryMapHexSelection primarySelection;
    private int? auxiliaryHexLookup=48400;
    internal int? AuxiliaryHexLookup=>auxiliaryHexLookup;
    private Child? hoveredChild;
    internal IReadOnlyList<int> PhysicalReceivers { get; private set; } = Array.Empty<int>();

    internal D3D11SceneryMapLabels()
    {
        MouseFilter=MouseFilterEnum.Ignore;ClipContents=true;
        AddChild(root);Resized+=UpdateScale;
    }

    internal void Configure(int identity,SourceWorldContext actualInitialCurrent,
        SceneryMapMarkerSources source,UiAssets assets,Shader shader,MapFont font,
        MapTextPresets presets,MapSpritePalettes palettes,AreaMapMarkerSources markerColors,
        Func<Vector2I?> canonicalPointer,Action<int> selected,Func<int,bool>? canPress=null)
    {
        if(identity<0)throw new ArgumentOutOfRangeException(nameof(identity));
        if(!IsInsideTree())throw new InvalidOperationException("Attach the actual Scenery controller before initialization.");
        lock(lifetime)
            if(attached||retired)throw new InvalidOperationException("Scenery controller cannot be rebound.");
        var entries=ArmyCampaignRuleAdapter.ReadLandmarkMapEntries(actualInitialCurrent)
            ??throw new InvalidDataException("Current Scenery registry or initialized label content is unknown.");
        this.selected=selected;
        this.canPress=canPress;
        flags=new(markerColors,palettes);
        foreach(var entry in entries)
        {
            var view=new SceneryMapLabelView {Name="Scenery"+entry.LandmarkId};
            root.AddChild(view);
            try
            {
                view.Configure(source,assets,shader,font,presets,palettes,entry.Name,
                    ()=>
                    {
                        if(acceptedFrame is not null&&acceptedWorld is not null&&
                            ReferenceEquals(hoveredChild?.View,view)&&view.SourceInputAdmitted&&view.ActualHovered)
                            this.selected?.Invoke(entry.LandmarkId);
                    });
                view.BindCanonicalPointer(canonicalPointer);
                view.SetPresentationInputAdmitted(false);
                var child=new Child(entry,view);
                childrenByLandmark.Add(entry.LandmarkId,child);
                children.Add(child);
                view.BindSourceInput(()=>Enter(child),()=>Leave(child),()=>Down(child),()=>Release(child),InputChanged);
            }
            catch {view.QueueFree();throw;}
        }
        // Empty is admitted only from the actual known-empty registry above.
        // It is never manufactured to stand in for unknown construction.
        PhysicalReceivers=Array.AsReadOnly(children.Select(child=>child.Entry.LandmarkId).ToArray());
        UpdateScale();Visible=false;
        lock(lifetime){reference=new(identity);attached=true;}
    }

    internal SceneryLayoutReference ReadLayoutReference()
    {
        lock(lifetime)return attached?reference:new(null);
    }

    internal void WriteDirty(SceneryLayoutReference target,int value)
    {
        lock(lifetime)
        {
            if(!attached||target.Identity!=reference.Identity)
                throw new InvalidDataException("Scenery dirty write targets a detached or replaced controller.");
            // A zero store clears only the notifications reached before it.
            if(value==0)consumedRevision=dirtyRevision;
            else dirtyRevision++;
        }
    }

    internal void SetChildControl(int landmarkId,bool enabled,int displayOverride)
    {
        var child=childrenByLandmark[landmarkId];
        child.Enabled=enabled;child.DisplayOverride=displayOverride;
        child.View.SetControlEnabled(enabled);
        lock(lifetime)dirtyRevision++;
    }

    internal void SetDomesticControl(int landmarkId,bool enabled)
    {
        var child=childrenByLandmark[landmarkId];
        SetChildControl(landmarkId,enabled,enabled?child.DisplayOverride:1);
        if(!enabled)
        {
            // 1BB980 also calls v2A8(0). Projection may show the root again,
            //but its text and flag children must stay hidden under override1.
            child.View.SetPresentation(-1,false,child.TextAlpha,child.FlagAlpha);
            child.View.SetRootVisible(false);
        }
    }
    internal void EndDomesticControls()
    {
        foreach(var child in children)SetChildControl(child.Entry.LandmarkId,true,0);
    }

    internal void BindCurrentInput(SourceWorldContext current,SceneryMapHexSelection primary)
    {inputCurrent=current;primarySelection=primary;}

    internal SceneryMapAlphaInputs? ReadCurrentAlphaInputs(int landmarkId)
    {
        var child=childrenByLandmark[landmarkId];
        if(inputCurrent is {} current)
        {
            var own=ReadSceneryHex(current,child.Entry.LandmarkId);
            var primary=NormalizePrimary(current,primarySelection);
            SceneryMapHexSelection auxiliary=auxiliaryHexLookup is not int key?new(false,null):
                key>=48400?new(true,null):ReadHex(current,key);
            child.View.UpdateSelectionFocus(Matches(own,primary),Matches(own,auxiliary));
        }
        return child.View.ReadCurrentAlphaInputs();
    }
    private static bool? Matches(SceneryMapHexSelection a,SceneryMapHexSelection b)
        => a.Known&&b.Known?a.Hex?.PhysicalSlot==b.Hex?.PhysicalSlot:null;
    private static SceneryMapHexSelection ReadHex(SourceWorldContext current,int key)
    {
        var reference=current.ReadHexTableReference(key<48400?key:0);
        return reference is null?new(false,null):new(true,reference);
    }
    private static SceneryMapHexSelection ReadSceneryHex(SourceWorldContext current,int id)
    {
        bool? valid=current.ReadSceneryNativeValidity(new(id));
        if(valid==false)return new(true,null);
        if(valid is null||current.ReadSceneryHexIndex(new(id)) is not ushort raw)return new(false,null);
        return ReadHex(current,raw);
    }
    private static SceneryMapHexSelection NormalizePrimary(SourceWorldContext current,SceneryMapHexSelection primary)
    {
        if(!primary.Known||primary.Hex?.PhysicalSlot is not int)return primary;
        if(current.ReadHexObjectLink(primary.Hex) is not ushort raw)return new(false,null);
        var obj=current.ReadObjectTableReference(raw<3001?raw:0);
        if(obj is null)return new(false,null);
        bool? valid=current.ReadObjectNativeValidity(obj);
        if(valid==false)return primary;
        if(valid is null||current.ReadObjectHexIndex(obj) is not ushort hex)return new(false,null);
        return ReadHex(current,hex);
    }
    private void InputChanged()
    {var target=ReadLayoutReference();if(target.Identity is not null)WriteDirty(target,1);}
    private void Enter(Child child)
    {
        if(!child.View.SourceInputAdmitted||ReferenceEquals(child,hoveredChild))return;
        if(hoveredChild is {} prior)Leave(prior);
        hoveredChild=child;child.View.SourcePointerEnter();
        if(inputCurrent is {} current)
        {
            var hex=ReadSceneryHex(current,child.Entry.LandmarkId);
            if(!hex.Known)auxiliaryHexLookup=null;
            else if(hex.Hex?.PhysicalSlot is int physical)auxiliaryHexLookup=physical;
        }
    }
    private void Leave(Child child)
    {
        if(!ReferenceEquals(child,hoveredChild))return;
        hoveredChild=null;auxiliaryHexLookup=48400;child.View.SourcePointerLeave();
    }
    private void Down(Child child)
    {
        if(ReferenceEquals(child,hoveredChild)&&child.View.SourceInputAdmitted&&canPress?.Invoke(child.Entry.LandmarkId)!=false)
            child.View.SourcePointerDown();
    }
    private void Release(Child child)
    {if(ReferenceEquals(child,hoveredChild)&&child.View.SourceInputAdmitted&&child.View.ActualHovered)child.View.SourcePointerRelease();}

    internal void Present(GameState world,SourceWorldContext sameCurrent,D3D11SceneryFrame units,
        MainMapCameraFrame acceptedCamera,SceneryMapPolicy policy,bool cameraStable,
        NMatrix viewport,bool? clipBypass,MapControlSurfaceExtent? applicationSurface,
        bool applicationSurfaceKnown,bool? fieldRefreshSuppressed,bool? alphaRefreshSuppressed,
        Func<int,SceneryMapAlphaInputs?> readCurrentAlpha)
    {
        lock(lifetime)if(!attached)return;
        preparedFrame=null;pending=null;
        Visible=(policy.Category16&1)!=0||((policy.Category16&2)==0&&(policy.Category16&4)!=0);
        // The last displayed layout remains interactive during ordinary frame
        // preparation. Only an actual lifetime/visibility/transition boundary
        // invalidates its input; a new frame number is not a cancellation.
        if(acceptedFrame is null||!ReferenceEquals(acceptedWorld,world)||!Visible||!cameraStable)
            CloseInput();
        // Hidden/transition frames retain the actual owner and all dirty work.
        if(!Visible||!cameraStable)
        {lock(lifetime)if(dirtyRevision==consumedRevision)dirtyRevision++;return;}
        if(units.CameraFrame!=acceptedCamera.Camera.FrameNumber)
            throw new InvalidDataException("Scenery Unit publication belongs to a different accepted camera.");
        int level=MapLabelPresentation.SettledCameraLevel(acceptedCamera.ZoomSelection);
        int display=SceneryMapMarkerSources.DisplayLevel(level,policy.Mode);
        long capturedRevision;bool dirty;
        lock(lifetime){capturedRevision=dirtyRevision;dirty=capturedRevision!=consumedRevision;}
        bool refresh=dirty||priorDisplay!=display||priorPolicy!=policy||!ReferenceEquals(priorWorld,world);
        bool complete=true;
        foreach(var child in children)
        {
            bool alphaRead=false;
            SceneryMapAlphaInputs? alphaInput=null;
            int actualDisplay=policy.Mode switch
            {
                0 or 1 or 2 or 8=>display,
                10=>-1,
                _=>display==-1?-1:child.Enabled?0:1
            };
            if(refresh&&child.DisplayOverride==0)
            {
                child.AppliedDisplay=policy.Mode==12?display:actualDisplay;
                child.View.SetPresentation(child.AppliedDisplay,actualDisplay!=-1,child.TextAlpha,child.FlagAlpha);
            }
            if(refresh&&unchecked((uint)actualDisplay)<2)
            {
                if(fieldRefreshSuppressed==false)
                {
                    var appearance=flags.Read(world,sameCurrent,new(child.Entry.LandmarkId),
                        new(policy.Mode,policy.Flags15,policy.IsTitleState));
                    child.View.RefreshFlag(appearance);
                    if(appearance.Kind==SceneryMapFlagUpdateKind.Unknown||appearance.Unavailable.Count>0)complete=false;
                }
                else if(fieldRefreshSuppressed is null)complete=false;
            }
            if(!units.Units.TryGetValue(child.Entry.LandmarkId,out var unit)||
                !unit.CurrentPositionComplete||unit.Position is not { } position)
            {child.View.SetRootVisible(false);complete=false;continue;}
            float fade=MapLabelPresentation.DistanceFade(new(position.X,position.Y,position.Z),
                acceptedCamera.Camera.CurrentPose.Position80,acceptedCamera.ControllerState.Distance90);
            if(refresh&&child.View.RootVisible)
            {
                if(level<4&&fade>1f)child.View.SetRootVisible(false);
                else if(!ApplyAlpha(child))complete=false;
            }
            if(actualDisplay==-1)continue;
            bool? valid=sameCurrent.ReadSceneryNativeValidity(new(child.Entry.LandmarkId));
            if(valid==false)continue; // Native projection returns before its visibility store.
            if(valid is null||!applicationSurfaceKnown)
            {child.View.SetRootVisible(false);complete=false;continue;}
            var point=MapControlProjection.Project(position,new(0,300,0),
                acceptedCamera.Camera.SourceWorldToProjection,viewport,clipBypass==true,applicationSurface);
            // A point admitted by the strict clip branch has the same output
            // on both bypass alternatives. A rejected point cannot prove the
            // result while that actual owner is still unknown.
            if(point is null&&clipBypass is null)complete=false;
            bool onScreen=point.HasValue&&!(level<4&&fade>1f);
            child.View.SetRootVisible(onScreen);
            if(onScreen&&point is { } pixel)
            {
                var rawPosition=child.View.CanonicalPosition;
                // The common projection updates alpha only when its raw
                // projected point differs from the old parent position.
                if(rawPosition!=new Vector2I(pixel.X,pixel.Y))
                {
                    var animation=ReadAlpha();
                    if(animation?.PositionTypeThreeAnimation is not bool positionAnimated)
                    {
                        ApplyAlpha(child); // Independent reached writer even when position knowledge is missing.
                        child.View.SetRootVisible(false);complete=false;continue;
                    }
                    if(!positionAnimated)rawPosition=new(pixel.X,pixel.Y);
                    if(!ApplyAlpha(child))complete=false;
                }
                child.View.SetCanonicalPosition(new(unchecked(rawPosition.X-21),unchecked(rawPosition.Y-40)));
            }

            bool ApplyAlpha(Child target)
            {
                if(target.DisplayOverride!=0||alphaRefreshSuppressed==true)return true;
                if(alphaRefreshSuppressed is null)return false;
                var input=ReadAlpha();
                if(input?.InteractionForcesOpaque is not bool opaque)return false;
                byte? text=opaque?byte.MaxValue:input.TextTypeOneAnimation==false?
                    MapLabelPresentation.TextAlpha(level,fade):null;
                byte? flag=opaque?byte.MaxValue:input.FlagTypeOneAnimation==false?
                    MapLabelPresentation.FlagAlpha(level,fade):null;
                if(text is byte t)target.TextAlpha=t;
                if(flag is byte f)target.FlagAlpha=f;
                target.View.SetAlpha(text,flag,opaque);
                return opaque||input.TextTypeOneAnimation.HasValue&&input.FlagTypeOneAnimation.HasValue;
            }

            SceneryMapAlphaInputs? ReadAlpha()
            {
                if(!alphaRead){alphaInput=readCurrentAlpha(child.Entry.LandmarkId);alphaRead=true;}
                return alphaInput;
            }
        }
        preparedFrame=acceptedCamera;
        pending=new(world,policy,display,capturedRevision,complete);
        // Projection/alpha can discover missing knowledge even on a clean
        // unchanged World. Publish one retry notification independently of
        // the old cache-change cause, without erasing concurrent worker work.
        if(!complete)lock(lifetime)
            if(dirtyRevision==consumedRevision)dirtyRevision++;
    }

    internal void AcceptPrepared(MainMapCameraFrame actualAcceptedCamera)
    {
        lock(lifetime)if(!attached)return;
        if(!ReferenceEquals(preparedFrame,actualAcceptedCamera)||pending is not { } presentation)return;
        acceptedFrame=actualAcceptedCamera;
        acceptedWorld=presentation.World;
        if(presentation.Complete)
        {
            priorDisplay=presentation.Display;priorPolicy=presentation.Policy;priorWorld=presentation.World;
            lock(lifetime)consumedRevision=Math.Max(consumedRevision,presentation.DirtyRevision);
        }
        pending=null;
        foreach(var child in children)child.View.SetPresentationInputAdmitted(true);
        var actualHover=children.FirstOrDefault(c=>c.View.SourceInputAdmitted&&c.View.ActualHovered);
        if(actualHover is {} next)Enter(next);
        else if(hoveredChild is {} old)Leave(old);
    }

    private void UpdateScale()=>root.Scale=new(Size.X/1920f,Size.Y/1080f);

    internal void SuspendPresentation()
    {
        preparedFrame=null;pending=null;CloseInput();
    }
    private void CloseInput()
    {
        acceptedFrame=null;acceptedWorld=null;
        if(hoveredChild is {} old)Leave(old);
        foreach(var child in children)
        {child.View.CancelCommandPress();child.View.SetPresentationInputAdmitted(false);}
    }

    internal void Abandon()
    {
        lock(lifetime){attached=false;retired=true;reference=new(null);}
        selected=null;canPress=null;
        hoveredChild=null;inputCurrent=null;
        preparedFrame=null;acceptedFrame=null;acceptedWorld=null;pending=null;
        foreach(var child in children)child.View.Abandon();
    }
    public override void _ExitTree()=>Abandon();
}
