using Godot;
using San14.Data;

internal partial class CommandSelectionView
{
    private UiSourceRoundButton? intelligenceApply;
    private bool intelligenceCanApply;
    private Control? intelligenceEconomyCanvas;
    private UiSourceBottomButton? intelligenceAutomatic;
    private UiSourceText? intelligenceBooksBefore,intelligenceBooksAfter,intelligenceBooksDelta,
        intelligenceUpkeep,intelligenceGold;

    /// <summary>The Home Force table activates a row into an assignment
    /// editor; its separate decision applies the entire assignment draft.</summary>
    internal void ConfigureIntelligenceOverview(GameResourceRepository repository,PreviewText text,
        IReadOnlyList<CommandSelectionColumn> columns,IReadOnlyList<CommandSelectionRow> rows,
        Func<bool> admitted,Action<int> editForce,Action accepted,Action returned,Action automatic,Func<int> scrollSpeed)
    {
        ConfigureCommonBackdrop(repository);
        Configure(repository,text,0x9C,columns,rows,[],1,admitted,
            selected=>{if(selected is {Count:1})editForce(selected[0]);else returned();},scrollSpeed,
            retainOnAccept:true,retainOnReturn:true,singleSelection:true,activateRows:true,returnOnly:true);
        var footer=new UiSourceLayout(repository,0xD8913AB2);Vector2 mount=new(0,904);
        // Reuse the ordinary two-decision source positions. The shared list's
        // row activation is deliberately independent of this formal decision.
        chrome.Canvas.GetNode<UiSourceRoundButton>("ReturnAdministration").Position=footer.Rectangle(4,mount).Position;
        intelligenceApply=new() {Name="ApplyIntelligenceAssignments",Position=footer.Rectangle(5,mount).Position,
            InputAdmission=ReadAdmission};chrome.Canvas.AddChild(intelligenceApply);
        intelligenceApply.Configure(repository,chrome.Assets,chrome.SpriteShader,text.SourceLocale,
            "000_cmn_txt_decision_kettei",()=>{if(ReadAdmission()&&intelligenceCanApply)accepted();});
        intelligenceApply.Disabled=true;
        intelligenceAutomatic=new() {Name="AutomaticIntelligenceAssignments",Position=footer.Rectangle(14,mount).Position,
            InputAdmission=ReadAdmission};chrome.Canvas.AddChild(intelligenceAutomatic);
        var messages=repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        intelligenceAutomatic.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
            messages.Records[0x4B8].Text??throw new InvalidDataException("Missing automatic intelligence caption."),
            ()=>{if(ReadAdmission())automatic();});
        intelligenceEconomyCanvas=new Control {Name="IntelligenceEconomyCanvas",Size=new(1920,1080),
            MouseFilter=MouseFilterEnum.Ignore};
        AddChild(intelligenceEconomyCanvas);
        Resized+=FitIntelligenceEconomy;FitIntelligenceEconomy();
        var economy=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"06_03_01_naisei_offset_00"));
        foreach(var node in economy.Nodes.Where(n=>n.Type is 1 or 3))
            economy.Art(intelligenceEconomyCanvas,node.Index,economy.Rectangle(node.Index),chrome.Assets,chrome.SpriteShader,
                node.Index==3?MapSpritePalettes.Create()[10]:null);
        foreach(var (node,message) in new[]{(4,0x292),(9,0x403),(11,0x56B),(13,0x20C)})
            economy.Text(intelligenceEconomyCanvas,node,default,messages.Records[message].Text??
                throw new InvalidDataException("Missing intelligence summary caption."),chrome.Font);
        intelligenceBooksBefore=economy.Text(intelligenceEconomyCanvas,5,default,"",chrome.Font);
        intelligenceBooksAfter=economy.Text(intelligenceEconomyCanvas,7,default,"",chrome.Font);
        intelligenceBooksDelta=economy.Text(intelligenceEconomyCanvas,8,default,"",chrome.Font);
        intelligenceUpkeep=economy.Text(intelligenceEconomyCanvas,10,default,text.Get("detail_unknown"),chrome.Font);
        intelligenceGold=economy.Text(intelligenceEconomyCanvas,14,default,text.Get("detail_unknown"),chrome.Font);
    }

    // The ordinary HomeSet child borrows this state's common blur and economy,
    // while only the force-list dialog and its header/footer leave the screen.
    internal void SuspendIntelligenceOverviewContent()
    {
        if(abandoned||intelligenceEconomyCanvas is null)return;
        chrome.Hide();Show();
    }

    internal void ResumeIntelligenceOverviewContent()
    {
        if(abandoned||intelligenceEconomyCanvas is null)return;
        chrome.Show();Show();
    }

    private void FitIntelligenceEconomy()
    {
        if(intelligenceEconomyCanvas is null)return;
        float scale=Math.Min(Size.X/1920,Size.Y/1080);
        intelligenceEconomyCanvas.Scale=Vector2.One*scale;
        intelligenceEconomyCanvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }

    internal void ApplyIntelligenceOverviewDecision(bool canApply,string? notice=null)
    {
        intelligenceCanApply=canApply;
        if(intelligenceApply is not null)intelligenceApply.Disabled=!canApply;
        if(notice is not null)ApplyNotice(notice);
    }

    internal void ApplyIntelligenceAutomaticAdmission(bool allowed)
    {if(intelligenceAutomatic is not null)intelligenceAutomatic.Disabled=!allowed;}

    internal void ApplyIntelligenceOverviewEconomy(int books,int cost,int? gold=null,int? monthlyCost=null)
    {
        intelligenceBooksBefore!.Text=books.ToString(System.Globalization.CultureInfo.InvariantCulture);
        intelligenceBooksAfter!.Text=(books-cost).ToString(System.Globalization.CultureInfo.InvariantCulture);
        intelligenceBooksDelta!.Text=$"(-{cost})";
        if(gold is int money)intelligenceGold!.Text=money.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if(monthlyCost is int upkeep)intelligenceUpkeep!.Text=upkeep.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
