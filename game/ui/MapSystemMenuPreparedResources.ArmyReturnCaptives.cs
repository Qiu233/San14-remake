using Godot;
using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    // Only real CTreatPrisonerLayout dependencies. Shared officer inspectors
    // use the identical hidden-placeholder set when painting their three pages.
    private static void PrepareArmyReturnCaptiveSources(GameResourceRepository repository,UiAssets assets,MapFontSource font,
        LoadedMessages messages,string locale,Dictionary<MapFont.PreparedTextKey,MapTextCache> caches,CancellationToken cancellation)
    {
        var presets=MapTextPresets.Create();
        var data=messages.Messages.Sections[1].Records;
        string Caption(int id)=>data[id].Text??throw new InvalidDataException("Missing captive page caption.");
        _=assets.SourceLayout("00_11_08_00_shogu");
        var summary=assets.SourceLayout("00_11_08_00_offset_00");Art(summary,[1]);
        Text(summary,2,0x205);Text(summary,4,0x20C);Text(summary,7,0x697);
        var upkeepProfile=MapFontProfile.FromNode(summary.Nodes[6],summary.Source.Source.ResourceUri);
        MapFont.PrepareText(font,ArmyReturnCaptiveSelectionView.MonthlyMaintenanceCaption(messages,locale),
            upkeepProfile,summary.Rectangle(6).Size.X,caches);
        var speech=assets.SourceLayout("11_01_02_00_info_busho_offset_00");Art(speech,[1]);
        _=MapFontProfile.FromNode(speech.Nodes[2],speech.Source.Source.ResourceUri);
        var count=assets.SourceLayout("00_11_08_00_offset_03");Art(count,[1]);Text(count,2,0x543);
        var footer=assets.SourceLayout("00_11_08_00_offset_01");Art(footer,[1]);
        var action=assets.SourceLayout("cmn_btn_ft").ResizedSubtree(0,new(156,64),true);
        Art(action,[3,5,6,8,9,11,13,14]);
        foreach(int id in new[]{0x12,0x3C,0x3D,0x3E})Text(action,16,id,[3,4,5,6,7]);
        var all=assets.SourceLayout(0xCEE6F358);Art(all,[3,5,8,11,13]);
        Text(all,16,0x3A,[3,4,5,6,7]);Text(all,16,0x3B,[3,4,5,6,7]);
        var header=assets.SourceLayout(0xDC8AC8AA);Text(header,2,0x4AC,[56]);Text(header,2,0x4AD,[56]);
        Art(assets.SourceLayout(0x18DB7286),[1]);
        _=assets.PrepareCommonCaption(locale,"000_cmn_txt_decision_jyudaku");
        _=assets.PrepareCommonCaption(locale,"000_cmn_txt_decision_kyohi");
        var column=assets.SourceLayout(0x7A7B5B8C);Art(column,[3,5,8,11,16,17]);
        Text(column,13,0x3EC,[3,4,5,6]);Text(column,13,0x628,[3,4,5,6]);
        var row=assets.SourceLayout(0xF2C06161);Art(row,[4,6,9,12,14,23,25,28,31,33]);
        Text(row,17,0x628,[8,9,10,11,67,68]);
        foreach(int id in new[]{0x3CD,0x452,0x297})
        {
            string caption=Caption(id);var tab=assets.CommandButtonLayout(new(Math.Max(96,caption.Length*28+20),48));
            Art(tab,[3,5,6,8,9,11,13,14]);Text(tab,16,id,[28,29,30,31,32]);
        }
        var guide=assets.SourceLayout(0xAB6ADAA7);
        var guideProfile=MapFontProfile.FromNode(guide.Nodes[4],guide.Source.Source.ResourceUri);
        var narration=new AdministrationMessages(repository,locale);
        foreach(int id in new[]{0x2C42,0x2C43,0x2C44,0x2C47,0x2C48,0x2C49,0x2C4A,0x2C4B,0x2C4C,0x2C4D,0x2C4E,0x2C50,0x3249})
            MapFont.PrepareText(font,narration.Format(id),guideProfile,guide.Rectangle(4).Size.X,caches);
        uint[] pages=[0x9EA0EB77,0xA7BDE2D2,0x7D57CF94];
        for(int index=0;index<pages.Length;index++)
        {
            var page=assets.SourceLayout(pages[index]);var hidden=OfficerDetailsView.CurrentPageHiddenNodes(index);
            bool Visible(int node){for(int? at=node;at is int value;at=page.Nodes[value].ParentIndex)if(hidden.Contains(value))return false;return true;}
            Art(page,page.Nodes.Where(node=>Visible(node.Index)&&node.Type is 1 or 3).Select(node=>node.Index));
            foreach(var node in page.Nodes.Where(node=>Visible(node.Index)&&node.Type==6))_=page.Type6(node.Index);
            foreach(var (node,message) in OfficerDetailsView.PageCaptions(index))Text(page,node,message);
        }
        var name=assets.SourceLayout(0x2E73723A);
        Art(name,name.Nodes.Where(node=>node.Type is 1 or 3 && node.Index!=4&&node.ParentIndex!=4).Select(node=>node.Index));
        foreach(string mark in new[]{"000_cmn_mrk_base_green","000_cmn_mrk_base_yellow","000_cmn_mrk_base_red"})assets.PrepareCommonSprite(mark);

        void Art(UiSourceLayout source,IEnumerable<int> indices)
        {cancellation.ThrowIfCancellationRequested();assets.PrepareLayoutArt(source.Source,indices,commonImports:true);}
        void Text(UiSourceLayout source,int node,int message,int[]? states=null)
        {
            var profile=MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri);
            foreach(int? state in states?.Select(value=>(int?)value)??new int?[]{null})
                MapFont.PrepareText(font,Caption(message),state is int value?profile.WithPreset(presets[value]):profile,source.Rectangle(node).Size.X,caches);
        }
    }
}
