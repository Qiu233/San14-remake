using Godot;
using San14.Data;

/// <summary>The original three-state CLogBtnLayout. Saved record facts own its
/// text, emphasis and map marker; input only chooses the source art pose.</summary>
internal partial class MapBottomLogRow : UiSourceButton
{
    private Rect2 hit;
    internal override int VisualState=>Disabled?0:InputVisualState;
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);

    internal void Configure(MapSummaryResources resources,MapBottomLogEntry entry,
        OfficerPortraitRoute? portrait,Func<bool> admitted,Action<int> focus)
    {
        InputAdmission=admitted;ActionMode=ActionModeEnum.Release;
        var source=resources.Layout("00_02_00_mein_offset_42");
        Size=source.Rectangle(0).Size;hit=source.Rectangle(1);
        var profile=resources.Assets.ButtonStateProfile(source.Source,
            ["0_normal","1_focus ","2_push ","0_normal","1_focus "]);
        var art=new UiSourceButtonStateArt(this,source,resources.Assets,resources.Shader,
            [[3],[12,13],[22],[],[]],"LogRecordState",profile);
        var backgrounds=art.States.SelectMany(SpriteParts).ToArray();
        int[] bodies=[4,14,23],markers=[5,15,24],faces=[6,16,25],flags=[8,18,27];
        var flagSource=resources.Layout("cmn_flag_force");
        var fill=MapSpritePalettes.ForceFill(entry.ForceColour);
        var letter=resources.Palettes.ForceLetter(entry.ForceColour);
        for(int state=0;state<3;state++)
        {
            var owner=art.States[state];
            var box=source.Rectangle(bodies[state]);
            var paragraph=new UiSourceParagraph {Name="LogRecordBody",Position=box.Position,Size=box.Size};
            owner.AddChild(paragraph);
            int textPreset=state switch {1=>4,2=>5,_=>entry.Emphasized?16:0};
            var textProfile=MapFontProfile.FromNode(source.Nodes[bodies[state]],source.Source.Source.ResourceUri)
                .WithPreset(resources.Presets[textPreset]);
            // Mode2 fits the retained literal paragraph as one body. Preserve
            // colour atoms and LF before entering the explicit line renderer.
            string widest=DeploymentNarrationParagraphPlan.Runs(entry.Body,allowPresetColours:true)
                .MaxBy(line=>resources.Font.Measure(line.VisibleText,textProfile).Width)!.VisibleText;
            textProfile=resources.Font.Source.FitToWidth(widest,textProfile,box.Size.X) with {WidthFitMode=0};
            paragraph.Configure(resources.Font,textProfile);paragraph.Apply(entry.Body);
            if(entry.CanFocus)source.Art(owner,markers[state],source.Rectangle(markers[state]),resources.Assets,resources.Shader);
            if(resources.Assets.RequireStockPortraitSource(portrait) is { } image)
            {
                box=source.Rectangle(faces[state]);
                var face=new UiSourcePortrait {Name="LogRecordFace",Position=box.Position,Size=box.Size};
                owner.AddChild(face);face.ConfigureFace2(resources.Repository,resources.Assets,resources.Shader,image);
            }
            box=source.Rectangle(flags[state]);
            var flag=new Control {Name="LogRecordForce",Position=box.Position,Size=flagSource.Rectangle(0).Size,
                Scale=box.Size/flagSource.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};
            owner.AddChild(flag);
            flagSource.Art(flag,1,flagSource.Rectangle(1),resources.Assets,resources.Shader,new(fill,fill,fill,fill));
            var caption=flagSource.Text(flag,2,default,entry.ForceName,resources.Font);
            var flagProfile=MapFontProfile.FromNode(flagSource.Nodes[2],flagSource.Source.Source.ResourceUri);
            caption.Configure(resources.Font,flagProfile with {ColourLT=letter.CpuPacked,ColourRT=letter.CpuPacked,
                ColourLB=letter.CpuPacked,ColourRB=letter.CpuPacked});
        }
        ConfigureStates(art.States,state=>
        {
            art.ApplyState(state);
            var palette=resources.Palettes[state switch {1=>26,2=>27,_=>entry.Emphasized?12:9}];
            foreach(var sprite in backgrounds)sprite.SetColors(palette);
        });
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted&&entry.CanFocus)focus(entry.HexId);};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("source_log_slot",entry.Slot);SetMeta("source_log_can_focus",entry.CanFocus);
        SetMeta("source_log_emphasized",entry.Emphasized);

        static IEnumerable<UiSourceSprite> SpriteParts(Node node)
        {
            if(node is UiSourceSprite sprite)yield return sprite;
            foreach(var child in node.GetChildren())
                foreach(var part in SpriteParts(child))yield return part;
        }
    }
}

/// <summary>Four collapsed hosts sample the original log curve at their real
/// resting frames. The transient notification queue remains owned separately.</summary>
internal partial class MapBottomLogRestingRow : UiSourceAnimationView
{
    internal void Configure(MapSummaryResources resources,MapBottomLogRow row,int slot)
    {
        var source=resources.Layout("00_02_00_mein_offset_41");
        var clips=resources.Assets.SourceAnimationClips(source,new HashSet<string>(StringComparer.Ordinal){"log"});
        AutomaticAnimationClock=false;
        ConfigureAnimation(source,clips,[0,1]);
        AnimationPanes[1].AddChild(row);
        row.Disabled=slot==3;
        if(slot==3)row.MouseFilter=MouseFilterEnum.Ignore;
        ApplyAnimationAt("log",(slot+1)*6*(double)LcskAnimationClip.SecondsPerFrame);
    }
}
