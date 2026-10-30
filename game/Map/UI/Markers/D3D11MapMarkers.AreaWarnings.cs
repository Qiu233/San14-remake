using Godot;
using San14.Data;

internal partial class D3D11MapMarkers
{
    private sealed class AreaWarningView(Control image)
    {
        internal Control Image {get;}=image;
        internal bool Visible;
        internal int Palette=12;
    }
    private readonly Dictionary<int,AreaWarningView> areaWarnings=[];
    private UiSourceLayout? areaWarningLayout;
    private Shader? areaWarningShader;

    internal void ApplyAreaOccupationWarnings(AreaOccupationWarningPublication publication)
    {
        SetMeta("source_area_warning_issue",publication.UnresolvedOwner??"");
        if(publication.Reset==true)
            foreach(var value in areaWarnings.Values)
            {value.Visible=false;value.Palette=12;TintAreaWarning(value);}
        foreach(var row in publication.Entries)
        {
            //1A48E0 addresses existing Area labels only. A Base overlapping
            //that center owns a different normal label and is not a new target.
            if(!_markers.ContainsKey((Kind.Area,row.AreaId)))continue;
            if(!areaWarnings.TryGetValue(row.AreaId,out var view))
            {
                if(row.Visible!=true)continue;
                areaWarningLayout??=new(new(_areaSources!.Flag.Layout,_areaSources.Flag.Source));
                areaWarningShader??=UiSourceSprite.CreateShader();
                var rectangle=areaWarningLayout.Rectangle(18,
                    new(_areaLayout!.SourceLayoutRootX,_areaLayout.SourceLayoutRootY));
                var image=areaWarningLayout.Art(this,18,rectangle,_assets!,areaWarningShader);
                image.Name="AreaOccupationWarning"+row.AreaId;
                view=new(image);areaWarnings.Add(row.AreaId,view);
            }
            if(row.Visible is bool visible)view.Visible=visible;
            if(row.Palette is int palette)view.Palette=palette;
            view.Image.SetMeta("source_unresolved_owner",row.UnresolvedOwner??"");
            view.Image.SetMeta("source_area_id",row.AreaId);
            TintAreaWarning(view);
        }
        PositionAreaOccupationWarnings();
    }
    private void TintAreaWarning(AreaWarningView view)
    {
        var colors=_spritePalettes![view.Palette];
        void Apply(Node node)
        {
            if(node is UiSourceSprite image)image.SetColors(colors);
            foreach(var child in node.GetChildren())Apply(child);
        }
        Apply(view.Image);view.Image.SetMeta("source_palette",view.Palette);
    }
    private void PositionAreaOccupationWarnings()
    {
        foreach(var(id,view) in areaWarnings)
        {
            if(!_markers.TryGetValue((Kind.Area,id),out var marker)){view.Image.Hide();continue;}
            view.Image.Visible=view.Visible&&marker.OnScreen&&_baseInformationVisible&&
                CampaignMarkerVisible(marker)&&!DirectionSuppresses(marker);
            view.Image.Scale=Size/CanonicalSize;
            var rectangle=areaWarningLayout!.Rectangle(18,
                new(_areaLayout!.SourceLayoutRootX,_areaLayout.SourceLayoutRootY));
            view.Image.Position=(marker.Screen+rectangle.Position)*view.Image.Scale;
            view.Image.Modulate=Alpha(Colors.White,marker.AreaPresentation.FlagAlpha);
        }
    }
}
