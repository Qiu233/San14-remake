using Godot;
using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    // Dependencies of the seven ordinary detail consumers. Current people,
    // flags, quantities and imported item pictures keep their existing owners.
    private static void PrepareOrdinaryInformationDetails(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, LoadedMessages messages, string locale,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken cancellationToken)
    {
        var presets = MapTextPresets.Create();
        string Caption(int id) => messages.Messages.Sections[1].Records[id].Text
            ?? throw new InvalidDataException("Missing ordinary information caption.");
        Template(ForceInformationSources.LayoutName, [2,4,128,176]);
        Template(AreaInformationSources.LayoutName,
            new[] {2,4,18,24,61,92,93,94,95,96,179}.Concat(Enumerable.Range(0,8).Select(i=>132+6*i)).ToArray());
        Template(DistrictInformationSources.LayoutName, [2,4,93]);
        Template(GateInformationSources.LayoutName, [2,7,17,25,35,40,86,87]);
        Template(ItemInformationSources.LayoutName, [2,7,50]);
        Template("02_07_06_info_collection_sibun_cn", [2]);
        Art(assets.SourceLayout(ForceInformationSources.NameplateLayoutName), [1,2,3]);
        Template(AreaInformationSources.NameplateLayoutName, []);
        Template(DistrictInformationSources.NameplateLayoutName, [4]);
        Art(assets.SourceLayout(0xE5FA7ED8), [1,2,3,5]);
        Art(assets.SourceLayout(0x9F7499D5), [1]);
        Art(assets.SourceLayout(0xE35D91DD), [1]);
        Art(assets.SourceLayout(0xAFC125D8), [1,2]);
        Art(assets.SourceLayout("cmn_mrker"), [2]);
        foreach (string icon in new[] { "cmn_icon_map_city", "cmn_icon_map_gate", "cmn_icon_map_unit", "000_cmn_mrk_base_red" })
            assets.PrepareCommonSprite(icon);

        var header = assets.SourceLayout(0xDC8AC8AA);
        foreach (int id in new[] {0x45C,0x462,AreaInformationSources.TitleId,DistrictInformationSources.TitleId,
            GateInformationSources.TitleId,ItemInformationSources.TitleId,0x841})
            Text(header,2,Caption(id),56,header.Rectangle(2).Size.X);
        string basic = Caption(0x3CD);
        foreach (uint file in new uint[] {0x9AFB27B7,0x172EDC24,0xD45873AA})
        {
            var tab = assets.SourceLayout(file); Art(tab,[3,5,8,11,13]);
            string caption = file == 0xD45873AA ? Caption(0x452) : basic;
            for (int state=0;state<4;state++)
                Text(tab,16,caption,8+state,tab.Rectangle(16).Size.X+caption.Length*28+36-tab.Nodes[0].Value44);
        }
        var column = assets.SourceLayout(0x7A7B5B8C);
        foreach (var (width,id) in new[] {(160,0x1FD),(132,0x202),(216,0),(188,ItemInformationSources.SelectionCaptionId),(160,0xAE)})
        {
            string caption = id == 0 ? "" : Caption(id);
            for (int state=0;state<4;state++)
                Text(column,13,caption,3+state,column.Rectangle(13).Size.X+width-column.Nodes[0].Value44);
        }
        foreach (var binding in ForceInformationSources.Captions) Fixed(ForceInformationSources.LayoutName,binding.Node,binding.Message,13);
        foreach (var binding in AreaInformationSources.Captions) Fixed(AreaInformationSources.LayoutName,binding.Node,binding.Message,13);
        foreach (var binding in DistrictInformationSources.Captions) Fixed(DistrictInformationSources.LayoutName,binding.Node,binding.Message,13);
        foreach (var binding in GateInformationSources.Captions)
            Fixed(GateInformationSources.LayoutName,binding.Node,binding.Message,binding.Node is 61 or 68 ? 0 : 13);
        foreach (var binding in ItemInformationSources.Captions) Fixed(ItemInformationSources.LayoutName,binding.Node,binding.Message,13);
        Fixed("02_07_06_info_collection_sibun_cn",16,0x400,13);
        foreach (int id in new[] {0x207,0x41D,0x211,0x224,ItemInformationSources.PoemCaptionId,0x3EC})
        {
            string caption = Caption(id);
            var button = assets.CommandButtonLayout(new(Math.Max(96,caption.Length*28+20),48));
            Art(button,[3,5,6,8,9,11,13,14]); _=assets.ButtonStateProfile(button.Source);
            for (int state=0;state<5;state++) Text(button,16,caption,28+state,button.Rectangle(16).Size.X);
        }
        foreach (uint file in new uint[] {0x946BF43B,0x1243B03C})
        {
            var source=assets.SourceLayout(file);
            var omitted=Enumerable.Range(4,10).Concat(Enumerable.Range(24,15)).Concat(Enumerable.Range(43,10))
                .Concat(Enumerable.Range(91,13)).Concat(Enumerable.Range(105,5))
                .Concat(new[] {2,19,40,57,72,77,82,114,115,116,121,129,158,159,160,161,162,166}).ToHashSet();
            // The body renderer replaces these mounts, including the source
            // standing character and all current tactical/radar publications.
            Art(source,source.Nodes.Where(n=>(n.Type is 1 or 3)&&!omitted.Contains(n.Index)).Select(n=>n.Index).ToArray());
            foreach (var (node,id) in new[] {(14,0x1FD),(16,0x219),(18,0x1FE),(39,0x22B),(42,0x333),(56,600),
                (59,0x225),(65,0x20E),(68,0x226),(71,0x25E),(76,0x25C),(81,0x260),(86,0x20C),(89,0x20D)})
                Text(source,node,Caption(id),13,source.Rectangle(node).Size.X);
            foreach (var (node,id) in file==0x1243B03C
                ? new[] {(113,0x234),(118,0x461),(125,0x411)}
                : new[] {(115,0x3E0),(120,0x299),(125,0x3E1),(130,0x20A),(135,0x3EC),
                    (141,0x279),(146,0x27A),(151,0x27B),(156,0x27C)})
                Text(source,node,Caption(id),13,source.Rectangle(node).Size.X);
            int technologyNode=file==0x1243B03C?128:165;
            Text(source,technologyNode,messages.Messages.Sections[0].Records[0x114].Text
                ?? throw new InvalidDataException("Missing army technology caption."),13,source.Rectangle(technologyNode).Size.X);
            if(file==0x1243B03C)Text(source,120,Caption(0x46A),0,source.Rectangle(120).Size.X);
            foreach (var (mount,radio,id) in new[] {(25,0xA97E37BEu,0x368),(32,0x01649C44u,0x369)})
            {
                var button=assets.SourceLayout(radio).ResizedSubtree(0,source.Rectangle(mount).Size,true);
                Art(button,[3,5,6,8,9,11,13,14]); _=assets.ButtonStateProfile(button.Source);
                for(int state=0;state<5;state++) Text(button,16,Caption(id),3+state,button.Rectangle(16).Size.X);
            }
        }
        // Fixed definitions identify actual installed image packs; unused
        // numeric gaps do not become fabricated resource dependencies.
        foreach (int image in repository.LoadItemDefinitions($"0010_{locale}/fixdataexce.s14").Catalog.Definitions
            .Select(item=>(int)item.RawBytes[21]).Where(image=>image is >0 and <201).Append(999).Distinct())
        {
            var route=CollectionImageRoute.ForItem(image);
            cancellationToken.ThrowIfCancellationRequested();
            assets.PrepareCommonSprite(route.SpriteName,route.TexturePack);
        }
        // Caravan gems and foreign weapons use independent definition
        // tables. Prepare their installed images through the same cache.
        string fixedPath=$"0010_{locale}/fixdataexce.s14";
        var gems=repository.LoadSkillDefinitions(fixedPath).Gems.Gems;
        var weapons=repository.LoadForeignTechnologies(fixedPath).Definitions;
        foreach(int image in Enumerable.Range(1,30).Select(slot=>
            gems[slot].RawField53>=201?999:(int)gems[slot].RawField53)
            .Concat(Enumerable.Range(1,8).Select(slot=>(int)weapons[slot].Value)).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var route=CollectionImageRoute.ForItem(image);
            assets.PrepareCommonSprite(route.SpriteName,route.TexturePack);
        }
        var collection=CollectionCatalog.Load(repository,locale);
        foreach (var route in collection.Poems.Select(row=>collection.ReadPoem(row.ItemId).Image).Distinct())
        { cancellationToken.ThrowIfCancellationRequested(); assets.PrepareCommonSprite(route.SpriteName,route.TexturePack); }

        void Template(string name,int[] replacements)
        {
            var source=assets.SourceLayout(name);
            bool Used(int index)
            {
                for(int? at=index;at is int node;at=source.Nodes[node].ParentIndex)
                    if(replacements.Contains(node))return false;
                return true;
            }
            Art(source,source.Nodes.Where(n=>(n.Type is 1 or 3)&&Used(n.Index)).Select(n=>n.Index).ToArray());
            foreach(var node in source.Nodes.Where(n=>n.Type==6&&Used(n.Index)))_=source.Type6(node.Index);
        }
        void Art(UiSourceLayout source,int[] nodes)
        { cancellationToken.ThrowIfCancellationRequested();assets.PrepareLayoutArt(source.Source,nodes,commonImports:true); }
        void Fixed(string name,int node,int id,int preset)
        {var source=assets.SourceLayout(name);Text(source,node,Caption(id),preset,source.Rectangle(node).Size.X);}
        void Text(UiSourceLayout source,int node,string caption,int preset,float width)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MapFont.PrepareText(font,caption,MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri)
                .WithPreset(presets[preset]),width,caches);
        }
    }
}
