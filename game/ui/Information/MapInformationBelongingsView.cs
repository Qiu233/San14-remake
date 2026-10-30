using Godot;
using San14.Data;
using System.Globalization;

/// <summary>The ordinary belongings state's two independently retained source
/// lists. Only the Gem basic group belongs to this caller.</summary>
internal partial class MapInformationBelongingsView : DetailsView
{
    private readonly Dictionary<int,MapInformationListView> lists=[];
    private UiAssets assets=null!;private MapFont font=null!;private Shader shader=null!;
    private Control canvas=null!;
    private BelongingsInformationPage publication=null!;
    private Func<bool>? admitted;private Action<int>? inspect;
    private bool abandoned;private int selected;
    private string unknown="";

    internal void Configure(GameResourceRepository repository,PreviewText text,BelongingsInformationPage publication,
        Func<bool> admitted,Action close,Action<int> inspect,Func<int> scrollSpeed)
    {
        this.publication=publication;this.admitted=admitted;this.inspect=inspect;unknown=text.Get("detail_unknown");
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new(){Name="SourceHeaderCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        var header=new UiSourceHeader();canvas.AddChild(header);header.Configure(repository,assets,shader,font,publication.Title);
        ConfigureSourcePage("InformationBelongings",header,close);
        string[] captions=[publication.ItemCaption,publication.GemCaption];
        Select(0);Resized+=Fit;Fit();
        void Select(int index)
        {
            if(abandoned||(uint)index>1)return;
            foreach(var retained in lists.Values){retained.Hide();retained.ProcessMode=ProcessModeEnum.Disabled;}
            selected=index;
            if(!lists.TryGetValue(index,out var view))
            {
                var pages=Source(index);var descriptor=pages.Groups[0];
                var page=pages.ReadPage(0);
                view=new();lists.Add(index,view);AddChild(view);MoveChild(view,0);
                view.Configure(repository,text,publication.Title,
                    descriptor.Columns.Where(c=>c.Visible).Select(c=>new MapInformationListColumn(Key(c.Id),c.Caption,c.Width,c.CanSort,c.InitialAscending)).ToArray(),
                    Rows(index,page),()=>ReadAdmission()&&selected==index,RequestClose,sourceIndex=>
                    {
                        if(!ReadAdmission()||selected!=index)return;
                        pages.SelectSourceIndex(sourceIndex);
                        if(index==0&&pages.SelectedDefinitionId is int item)inspect(item);
                    },scrollSpeed,(key,ascending)=>
                    {
                        int column=key=="name"?0:int.Parse(key,CultureInfo.InvariantCulture);
                        var result=pages.Sort(0,column,ascending);
                        if(!result.Available||result.Value is not { } sorted){GD.PushWarning(result.UnresolvedInput??"belongings.sort");return null;}
                        return Rows(index,sorted);
                    },page.UnresolvedOwner is null?null:text.Get("information_partial"),captions,index,Select,
                    includeHeader:false,completeColumnProfile:true,normalColumnLayout:true,retainSortDirections:true,rowExtentWidth:1464);
                view.Name=index==0?"BelongingsItems":"BelongingsGems";
            }
            view.ProcessMode=ProcessModeEnum.Inherit;view.Show();
        }
    }
    internal void ApplyItemDetailReturn(InformationDetailSelectionReturn returned)
    {
        var result=publication.Items.AdoptDetailOrder(0,returned.OrderedIds,returned.SelectedId,returned.NameSortAscending);
        if(!result.Available||result.Value is not { } page){GD.PushWarning(result.UnresolvedInput??"belongings.detail_return");return;}
        if(lists.TryGetValue(0,out var list))
        {
            int sourceIndex=page.Rows.First(r=>r.DefinitionId==returned.SelectedId).SourceIndex;
            list.ApplyDetailReturn(Rows(0,page),sourceIndex,returned.NameSortAscending);
        }
    }
    private DefinitionInformationPageSet Source(int index)=>index==0?publication.Items:publication.Gems;
    private IReadOnlyList<MapInformationListRow> Rows(int index,DefinitionInformationPage page)
    {
        var columns=Source(index).Groups[0].Columns;
        return page.Rows.Select(row=>new MapInformationListRow(row.SourceIndex,
            row.Cells.Select(cell=>cell.UnresolvedOwner is null?cell.Text??"":unknown).ToArray(),true,
            row.Cells.Select((cell,slot)=>new MapInformationListCellStyle(columns[slot].CellFormat,cell.PresetIndex,cell.MarkerColour?.CpuPacked)).ToArray())).ToArray();
    }
    private static string Key(int id)=>id==0?"name":id.ToString(CultureInfo.InvariantCulture);
    private bool ReadAdmission()=>!abandoned&&IsVisibleInTree()&&(admitted?.Invoke()??false);
    private void Fit()
    {
        if(canvas is null)return;float scale=Math.Min(Size.X/1920,Size.Y/1080);
        canvas.Scale=Vector2.One*scale;canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    public override void _ExitTree()
    {
        abandoned=true;admitted=null;inspect=null;
        foreach(var list in lists.Values)list.Abandon();lists.Clear();publication=null!;
        base._ExitTree();font?.Dispose();assets?.Dispose();shader?.Dispose();
    }
}
