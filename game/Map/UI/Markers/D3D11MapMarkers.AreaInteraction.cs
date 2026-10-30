using Godot;
using San14.Data;

internal partial class D3D11MapMarkers
{
    // The existing accepted marker pointer owns interaction. This one child
    // supplies the authored county focus/push ink inside that marker's bucket.
    private AreaInteractionView? areaInteraction;
    private Marker? areaInteractionMarker;
    private void RefreshAreaInteraction()
    {
        Marker? target=null;
        if (_hoveredHex is int hex && (_baseInformationVisible || campaignMarkerHexes is not null))
            foreach(var marker in _markers.Values)
                if(marker.Kind==Kind.Area && marker.Hex==hex && marker.OnScreen && CampaignMarkerVisible(marker) && TargetMarkerVisible(marker) && TargetMarkerEnabled(marker))
                {target=marker;break;}
        if(target is null || !baseInformationInk.TryGetValue(target,out var ink) || !ink.Ink.Visible)
        {if(areaInteraction is not null && GodotObject.IsInstanceValid(areaInteraction))areaInteraction.Hide();areaInteractionMarker=null;return;}
        if(areaInteraction is not null && !GodotObject.IsInstanceValid(areaInteraction))areaInteraction=null;
        if(areaInteraction is null || areaInteractionMarker!=target || areaInteraction.SourceName!=target.Name)
        {
            if(areaInteraction is not null){areaInteraction.GetParent()?.RemoveChild(areaInteraction);areaInteraction.Free();}
            var candidate=new AreaInteractionView{Name="CountySourceInteraction",MouseFilter=MouseFilterEnum.Ignore};
            ink.Ink.AddChild(candidate);
            try {candidate.Configure(_areaSources!,_assets!,cityAttentionShader!,_font!,_textPresets!,_spritePalettes!,target.Name);}
            catch {ink.Ink.RemoveChild(candidate);candidate.Free();throw;}
            areaInteraction=candidate;areaInteractionMarker=target;
        }
        int state=_pressedHex==target.Hex ? 2 : 1;
        var presentation=target.AreaPresentation;
        areaInteraction.Position=(target.Screen+new Vector2(_areaLayout!.SourceLayoutRootX,_areaLayout.SourceLayoutRootY))*Size/CanonicalSize;
        areaInteraction.Scale=Size/CanonicalSize;
        areaInteraction.Present(state,presentation.ShowName,presentation.FlagAlpha,presentation.TextAlpha,target.OutlineTint is not null);
    }
    private bool AreaInteractionReplaces(Marker marker)=>ReferenceEquals(marker,areaInteractionMarker) &&
        areaInteraction is not null && GodotObject.IsInstanceValid(areaInteraction) && areaInteraction.Visible;

    private sealed partial class AreaInteractionView : Control
    {
        private readonly Control[] flagStates=[new(),new()];
        private readonly Control[] textStates=[new(),new()];
        private readonly List<Control> outline=[];
        private UiSourceText caption=null!;
        private MapFont font=null!;
        private MapFontProfile profile=null!;
        private MapTextPresets presets=null!;
        private int selected=-1;
        internal string SourceName=>caption.Text;
        internal void Configure(AreaMapMarkerSources source,UiAssets assets,Shader shader,MapFont font,
            MapTextPresets presets,MapSpritePalettes palettes,string name)
        {
            this.font=font;this.presets=presets;
            var flag=new UiSourceLayout(new LoadedLayout(source.Flag.Layout,source.Flag.Source));
            int extra=(name.Length-1)*source.Text.Nodes[22].Value44;
            var geometry=new Dictionary<int,UiSourceNodeGeometry>();
            foreach(int node in new[]{6,7,13,14})geometry[node]=new(Size:new Vector2(source.Text.Nodes[node].Value44+extra,source.Text.Nodes[node].Value46));
            geometry[1]=new(Size:new Vector2(source.Text.Nodes[1].Value44+extra,source.Text.Nodes[1].Value46));
            geometry[22]=new(Size:new Vector2(name.Length*source.Text.Nodes[22].Value44,source.Text.Nodes[22].Value46));
            var text=new UiSourceLayout(new LoadedLayout(source.Text.Layout,source.Text.Source),geometry);
            var clips=new HashSet<string>{"1_focus","2_push"};
            var flagClips=LcskAnimationReader.Read(source.Flag.Layout,clips).ToDictionary(c=>c.Name);
            var textClips=LcskAnimationReader.Read(source.Text.Layout,clips).ToDictionary(c=>c.Name);
            for(int i=0;i<2;i++)
            {
                textStates[i].MouseFilter=flagStates[i].MouseFilter=MouseFilterEnum.Ignore;
                AddChild(textStates[i]);AddChild(flagStates[i]);
                int state=i+1,palette=4+state;
                string clip=state==1?"1_focus":"2_push";
                foreach(int node in state==1?new[]{6,7}:new[]{13,14})
                {
                    var part=text.Art(textStates[i],node,text.Rectangle(node),assets,shader,palettes[palette],
                        blend:node is 7 or 14?UiSourceSprite.Blend.Additive:UiSourceSprite.Blend.Ordinary);
                    part.Modulate=new(1,1,1,SettledAlpha(textClips[clip],node));
                }
                foreach(int node in state==1?new[]{6,7,8,9}:new[]{11,12,13,14})
                {
                    var part=flag.Art(flagStates[i],node,flag.Rectangle(node),assets,shader,palettes[palette],
                        blend:node is 7 or 9 or 12 or 14?UiSourceSprite.Blend.Additive:UiSourceSprite.Blend.Ordinary);
                    if(node is 6 or 7 or 11 or 12)outline.Add(part);
                    part.Modulate=new(1,1,1,SettledAlpha(flagClips[clip],node));
                }
            }
            profile=MapFontProfile.FromNode(source.Text.Nodes[22],source.Text.Source.ResourceUri);
            caption=text.Text(this,22,default,name,font,presets[4]);
        }
        private static float SettledAlpha(LcskAnimationClip clip,int node)
        {
            foreach(var track in clip.Tracks)
                if(track.NodeId==node&&track.Channel==10)return Math.Clamp((float)track.Curve.Sample(clip.EndFrame),0,1);
            return 1;
        }
        internal void Present(int state,bool name,byte flagAlpha,byte textAlpha,bool showOutline)
        {
            for(int i=0;i<2;i++)
            {
                flagStates[i].Visible=i==state-1;flagStates[i].Modulate=new(1,1,1,flagAlpha/255f);
                textStates[i].Visible=name&&i==state-1;textStates[i].Modulate=new(1,1,1,textAlpha/255f);
            }
            foreach(var part in outline)part.Visible=showOutline;
            caption.Visible=name;caption.Modulate=new(1,1,1,textAlpha/255f);
            if(selected!=state){caption.Configure(font,profile.WithPreset(presets[state==1?4:5]));selected=state;}
            Show();
        }
    }
}
