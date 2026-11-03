using Godot;
using San14.Data;

internal partial class DistrictSetupView
{
    private GameResourceRepository presentationRepository=null!;
    private AreaAdministrationOfficerContent presentationContent=null!;
    private Control governorPortraitHost=null!;
    private UiSourcePortrait? governorPortrait;
    private LoadedUiSpriteSource? governorPortraitSource;
    private DistrictSetupOfficerChart governorChart=null!;
    private readonly List<DistrictSetupDistrictComparisonView> districtComparisons=[];

    internal void ConfigurePresentation(GameResourceRepository repository,PreviewText text,
        UiSourceLayout basic,Vector2 mount)
    {
        presentationRepository=repository;presentationContent=new();
        var faceBox=basic.Rectangle(89,mount);
        //92CA60 converts the scaled placeholder dimensions to integers.
        faceBox=new(new Vector2((int)faceBox.Position.X,(int)faceBox.Position.Y),
            new Vector2((int)faceBox.Size.X,(int)faceBox.Size.Y));
        governorPortraitHost=new Control {Name="DistrictGovernorPortrait",Position=faceBox.Position,
            Size=faceBox.Size,MouseFilter=MouseFilterEnum.Ignore};panes[0].AddChild(governorPortraitHost);
        var frame=new UiSourceLayout(repository,0x2929DE3A).ResizedSubtree(0,basic.Rectangle(91,mount).Size,true);
        var frameHost=new Control {Name="DistrictGovernorFrame",Position=basic.Rectangle(91,mount).Position+new Vector2(-1,0),
            Size=basic.Rectangle(91,mount).Size,MouseFilter=MouseFilterEnum.Ignore};panes[0].AddChild(frameHost);
        //The frame's nine authored groups are ordinary/white/alpha1.
        //Use the existing Type3 Art path, not the Type1 material reader.
        foreach(var node in frame.Nodes.Where(node=>node.Type is 1 or 3))
            frame.Art(frameHost,node.Index,frame.Rectangle(node.Index),chrome.Assets,chrome.SpriteShader);
        governorChart=new DistrictSetupOfficerChart {Name="DistrictGovernorAbilities",Position=basic.Rectangle(93,mount).Position};
        panes[0].AddChild(governorChart);governorChart.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,text);
        governorChart.Scale=basic.Rectangle(93,mount).Size/governorChart.Size;
        foreach(var node in basic.Nodes.Where(node=>node.Type is 1 or 3))
            if(Descendant(node.Index,106))basic.Art(panes[0],node.Index,basic.Rectangle(node.Index,mount),
                chrome.Assets,chrome.SpriteShader,sourceMaterial:true);
        var messages=repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        foreach(var (node,message) in new[]{(108,0x1FE),(109,0x205),(110,0x211)})
            basic.Text(panes[0],node,mount,messages.Records[message].Text??throw new InvalidDataException("Missing district setup comparison caption."),chrome.Font);
        foreach(int node in new[]{112,124,128,132,136,140,144,148})
        {
            var row=new DistrictSetupDistrictComparisonView {Name="DistrictComparison"+districtComparisons.Count,
                Position=basic.Rectangle(node,mount).Position};panes[0].AddChild(row);
            row.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,text);
            row.Scale=basic.Rectangle(node,mount).Size/row.Size;districtComparisons.Add(row);row.Hide();
        }
        SetMeta("district_setup_presentation_layouts","A9487A40/2929DE3A/27119CFA/E4B493F4");
        bool Descendant(int index,int ancestor)
        {
            for(int? at=index;at is int current;at=basic.Nodes[current].ParentIndex)
                if(current==ancestor)return true;
            return false;
        }
    }

    internal void ApplyPresentation(DistrictSetupPresentation? value,string? unavailable=null)
    {
        if(abandoned)return;
        if(value is not null&&value.Districts.Count>districtComparisons.Count)
            throw new InvalidDataException("District setup supports its eight authored comparison rows.");
        var leader=presentationContent.Create(value?.Leader);
        var face=chrome.Assets.RequireStockPortraitSource(leader.Portrait);
        if(!ReferenceEquals(governorPortraitSource,face))
        {
            if(governorPortrait is not null)
            {governorPortrait.Hide();governorPortraitHost.RemoveChild(governorPortrait);governorPortrait.QueueFree();governorPortrait=null;}
            governorPortraitSource=face;
            if(face is {} image)
            {
                governorPortrait=new UiSourcePortrait {Name="DistrictGovernorFace2",Size=governorPortraitHost.Size};
                governorPortraitHost.AddChild(governorPortrait);
                governorPortrait.ConfigureFace2(presentationRepository,chrome.Assets,chrome.SpriteShader,image);
            }
        }
        governorChart.Apply(leader.Abilities);
        for(int i=0;i<districtComparisons.Count;i++)
            districtComparisons[i].Apply(value is not null&&i<value.Districts.Count?value.Districts[i]:null);
        string? missing=unavailable??value?.UnresolvedOwner??value?.Leader?.Portrait.UnresolvedInput;
        if(missing is not null)SetMeta("district_setup_presentation_issue",missing);
        else if(HasMeta("district_setup_presentation_issue"))RemoveMeta("district_setup_presentation_issue");
    }
}
