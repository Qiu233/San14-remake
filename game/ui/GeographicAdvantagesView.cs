using System.Globalization;
using System.Text.RegularExpressions;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary geographical mode's actual tinori list and City pane.</summary>
internal partial class GeographicAdvantagesView : Control
{
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private MapFont font=null!;
    private UiSourceLayout shell=null!,rowLayout=null!,cityLayout=null!,discoveryLayout=null!;
    private GeographicAdvantagePublication publication=null!;
    private Control rowClip=null!,rowContents=null!,cityContents=null!,discoveryContents=null!;
    private UiSourceScrollbar scrollbar=null!;
    private readonly List<UiSourceButton> buttons=[];
    private int selectedProvince=1;
    private MapTextPresets presets=null!;
    private LoadedMessages messages=null!;
    private bool summariesManaged;

    internal int SelectedProvinceId => selectedProvince;
    internal event Action<int>? ProvinceSelected;

    //These are the original shell mounts, not another pair of summary panes.
    internal Control SummaryMount(int slot) => slot switch
    {
        12 => discoveryContents,
        13 => cityContents,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    internal void SetSummaryOwnership(bool managed)
    {
        if(summariesManaged==managed)return;
        summariesManaged=managed;
        if(cityContents is null || discoveryContents is null)return;
        //Ownership changes once; later publication refreshes must not remove
        //the shared manager's resident controls from these same mounts.
        Clear(cityContents);Clear(discoveryContents);
        if(!managed)SelectCityPane();
    }

    internal void Configure(GameResourceRepository repository,PreviewText text,GeographicAdvantagePublication value)
    {
        this.repository=repository;assets=new(repository);shader=UiSourceSprite.CreateShader();font=new(repository,text.SourceLocale);
        presets=MapTextPresets.Create();messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data);
        var loaded=MapMarkerLayout.LoadNamed(repository,"03_81_08_tinori");
        // Hold the authored enter curve at its final sample. Geometry is read
        // from the resource rather than guessed from the hidden starting pane.
        var enter=LcskAnimationReader.Read(loaded.Layout,new HashSet<string>{"in"}).Single();
        var nodes=LcskNodeReader.Read(loaded.Layout);
        var geometry=new Dictionary<int,UiSourceNodeGeometry>();
        foreach(int index in new[]{4,8})
        {
            float x=nodes[index].HeaderValues[0],y=nodes[index].HeaderValues[1];
            foreach(var track in enter.Tracks.Where(t=>t.NodeId==index&&t.Curve.Keys.Count>0))
            {if(track.Channel==0)x=(float)track.Curve.Sample(enter.EndFrame);if(track.Channel==1)y=(float)track.Curve.Sample(enter.EndFrame);}
            geometry[index]=new(Offset:new(x,-y));
        }
        shell=new(loaded,geometry);Size=shell.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;
        rowLayout=new(MapMarkerLayout.LoadNamed(repository,"03_81_08_tinori_offset_00"));
        cityLayout=new(MapMarkerLayout.LoadNamed(repository,"03_81_08_tinori_offset_01"));
        discoveryLayout=new(MapMarkerLayout.LoadNamed(repository,"03_81_08_tinori_discovery_en"));
        var first=shell.Rectangle(9);var last=shell.Rectangle(17);
        rowClip=new(){Name="ProvinceListViewport",Position=first.Position,Size=new(first.Size.X,last.Position.Y-first.Position.Y),
            ClipContents=true,MouseFilter=MouseFilterEnum.Stop};AddChild(rowClip);
        rowContents=new(){Name="ProvinceRows",MouseFilter=MouseFilterEnum.Pass};rowClip.AddChild(rowContents);
        scrollbar=new(){Name="ProvinceScrollbar",Position=shell.Rectangle(18).Position,Size=new(16,rowClip.Size.Y)};AddChild(scrollbar);
        scrollbar.Configure(repository,assets,shader,Math.Max(0,value.Provinces.Count*110-(int)rowClip.Size.Y),0,
            offset=>rowContents.Position=new(0,-offset));
        rowClip.GuiInput+=scrollbar.HandleWheel;
        cityContents=new(){Name="ProvinceCityOwnership",Position=shell.Rectangle(5).Position,MouseFilter=MouseFilterEnum.Ignore};AddChild(cityContents);
        discoveryContents=new(){Name="ProvinceAdvantageSummary",Position=shell.Rectangle(7).Position,MouseFilter=MouseFilterEnum.Ignore};AddChild(discoveryContents);
        SetMeta("source_layout",shell.Source.Source.ResourceUri);SetMeta("source_tutorial_registry_key",76);
        Refresh(value);
    }

    internal void Refresh(GeographicAdvantagePublication value)
    {
        publication=value;Clear(rowContents);buttons.Clear();
        for(int i=0;i<value.Provinces.Count;i++)
        {
            var row=value.Provinces[i];var button=new UiSourceButton{Name="Province"+row.ProvinceId,Position=new(0,i*110),Size=new(522,110),Selected=row.ProvinceId==selectedProvince};
            rowContents.AddChild(button);buttons.Add(button);
            rowLayout.Art(button,2,rowLayout.Rectangle(2),assets,shader);
            foreach(int node in new[]{7,8,9,10,11,12,13})rowLayout.Art(button,node,rowLayout.Rectangle(node),assets,shader);
            rowLayout.Text(button,6,default,row.Name,font,presets[0]);
            Text(button,rowLayout,14,Data(0x2E1),13);Text(button,rowLayout,16,Data(0x233),13);Text(button,rowLayout,18,Data(0x6F5),13);
            Text(button,rowLayout,15,row.AdvantageName,0);
            Text(button,rowLayout,17,Numbers(Data(0x325),row.OwnedCityCounts.GetValueOrDefault(value.PlayerForceId),row.Cities.Count),0);
            Text(button,rowLayout,19,Numbers(Data(0x6F4),row.RequiredCities),0);
            Flag(button,rowLayout,3,row.QualifiedForceId??0);Flag(button,rowLayout,26,value.PlayerForceId);
            var states=new Control[5];
            for(int state=0;state<5;state++)
            {
                states[state]=new(){Name="State"+state,MouseFilter=MouseFilterEnum.Ignore};button.AddChild(states[state]);
                if(state==1)foreach(int node in new[]{21,22})rowLayout.Art(states[state],node,rowLayout.Rectangle(node),assets,shader);
                if(state==2)foreach(int node in new[]{24,25})rowLayout.Art(states[state],node,rowLayout.Rectangle(node),assets,shader);
            }
            button.ConfigureStates(states);button.Pressed+=()=>
            {
                selectedProvince=row.ProvinceId;SelectCityPane();
                ProvinceSelected?.Invoke(selectedProvince);
            };
            button.GuiInput+=scrollbar.HandleWheel;
        }
        SelectCityPane();
    }
    private void SelectCityPane()
    {
        for(int i=0;i<buttons.Count;i++)buttons[i].Selected=publication.Provinces[i].ProvinceId==selectedProvince;
        if(summariesManaged)return;
        Clear(cityContents);
        Clear(discoveryContents);
        var selected=publication.Provinces.FirstOrDefault(p=>p.ProvinceId==selectedProvince);if(selected is null)return;
        foreach(int node in new[]{1,3,4,5,12})discoveryLayout.Art(discoveryContents,node,discoveryLayout.Rectangle(node),assets,shader);
        if(selected.IllustrationIndex is <0 or >8)throw new InvalidDataException("geographic-advantages.illustration_index");
        string image=selected.IllustrationIndex.ToString("D3",CultureInfo.InvariantCulture);
        discoveryLayout.Art(discoveryContents,2,discoveryLayout.Rectangle(2),assets,shader,
            commonSpriteOverride:"sti_advantage"+image,commonTexturePack:"texture_stl_advantage_"+image);
        Text(discoveryContents,discoveryLayout,6,selected.AdvantageName,0);
        Text(discoveryContents,discoveryLayout,7,Data(0x411),13);
        Text(discoveryContents,discoveryLayout,13,selected.Name,0);
        Flag(discoveryContents,discoveryLayout,9,selected.QualifiedForceId??0);
        var description=new UiSourceParagraph{Position=discoveryLayout.Rectangle(8).Position,Size=discoveryLayout.Rectangle(8).Size};
        discoveryContents.AddChild(description);
        description.Configure(font,MapFontProfile.FromNode(discoveryLayout.Nodes[8],discoveryLayout.Source.Source.ResourceUri)
            .WithPreset(presets[0]) with {WidthFitMode=0});
        description.Apply(selected.Description);
        cityLayout.Art(cityContents,1,cityLayout.Rectangle(1),assets,shader);
        cityLayout.Art(cityContents,2,cityLayout.Rectangle(2),assets,shader);
        Text(cityContents,cityLayout,3,Data(0x6F3),13);
        for(int i=0;i<Math.Min(5,selected.Cities.Count);i++)
        {
            int block=i*8;var city=selected.Cities[i];
            foreach(int node in new[]{5+block,7+block})cityLayout.Art(cityContents,node,cityLayout.Rectangle(node),assets,shader);
            Text(cityContents,cityLayout,6+block,city.Name,13);
            var appearance=publication.Forces.GetValueOrDefault(city.ForceId);
            Text(cityContents,cityLayout,8+block,appearance?.DisplayName??"",0);
            Flag(cityContents,cityLayout,9+block,city.ForceId,true);
        }
    }
    private void Flag(Control parent,UiSourceLayout layout,int node,int force,bool neighbor=false)
    {
        if(publication.Forces.GetValueOrDefault(force) is not {} appearance)
        {SetMeta("force_appearance_unresolved_"+force,"geographic-advantages.force_flag");return;}
        var flag=new UiSourceForceFlag{Position=layout.Rectangle(node).Position};parent.AddChild(flag);
        flag.Configure(repository,assets,shader,font,appearance,neighbor:neighbor);
    }
    private void Text(Control parent,UiSourceLayout layout,int node,string value,int preset)=>layout.Text(parent,node,default,value,font,presets[preset]);
    private string Data(int id)=>string.Concat(messages.Messages.Sections[1].Records[id].Literals.Select(part=>part.Text));
    private static string Numbers(string format,params int[] values)
    {int i=0;return Regex.Replace(format,"%[0-9]*d",_=>i<values.Length?values[i++].ToString(CultureInfo.InvariantCulture):throw new InvalidDataException("geographic-advantages.numeric_format"));}
    private static void Clear(Control parent){foreach(Node child in parent.GetChildren()){parent.RemoveChild(child);child.QueueFree();}}
}
