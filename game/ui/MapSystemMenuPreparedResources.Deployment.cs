using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed partial class MapSystemMenuPreparedResources
{
    // Static inputs of the ordinary Make/person/target and transport pages.
    // Each declaration below follows an actual producer, with current faces,
    // flags, quantity labels, radar values and authored placeholders excluded.
    private static void PrepareDeploymentSources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, LoadedMessages messages, string locale,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken cancellationToken)
    {
        var presets=MapTextPresets.Create();
        var records=messages.Messages.Sections[1].Records;
        // The general multi-source dispatch target uses this same application
        // resource bundle; only its aggregate values remain page-owned.
        _=assets.SourceLayout(GeneralUnitTargetHeaderSources.LayoutName);
        var dispatch=assets.SourceLayout(GeneralUnitTargetHeaderSources.ContentLayoutName);
        assets.PrepareLayoutArt(dispatch.Source,dispatch.Nodes.Where(node=>node.Type is 1 or 3)
            .Select(node=>node.Index),commonImports:true);
        foreach(var node in dispatch.Nodes.Where(node=>node.Type==6))_=dispatch.Type6(node.Index);
        foreach(var (node,message) in GeneralUnitTargetHeaderSources.Captions)
            Text(dispatch,node,Caption(message),13);
        Art(0xFC796D9B,[40]); Blur(0xFC796D9B,[39]);
        Art(0xCFF3CC08,[1,2,16,17]);
        Art(0xB20B608C,[1,5,32,33,34]);
        Art(0x8EE99810,[1,4,15,16,17,18,19]);
        Art(0xC11503D0,[27,28,29,32,40,46,52,54,60,66,72,74,84]);
        Art(0x7E8AD398,[1,4]);
        Art(0x27119CFA,[1,4]);
        Art(0xCB7DEBFC,[1,11]);
        Art(0xF2FBFA11,[19,23,24,25,26,27,28,30,33,34]);
        Art(0xB5E86370,[2,4,5]);
        Art(0x5794E0A4,[3,5,8,11,13]);
        Art(0x2B7AA77D,[3,5,6,8,9,11,13,14]);
        Art(0xBCF8A04D,[3,5,8,11,13]);
        Art(0xE78964A4,[2,3,6,8,10,12]);
        Art(0x0AF34C58,[2,112]); Blur(0x0AF34C58,[1]);
        Art(0xDE6DAAC5,[3,5,6,8,9,11,13,14,17]);
        Art(0x0ABE99AC,[2,3]);
        Art(0xA3B4A82E,[3]); Blur(0xA3B4A82E,[2]);
        Art(0x7A7B5B8C,[3,5,8,11,16,17]);
        Art(assets.SourceLayout("cmn_btn_clm").Source.Source.FileId,[3,5,8,11]);
        Art(0xF2C06161,[4,6,9,12,14,23,25,28,31,33]);
        Art(0x38AD0211,[2,3]);
        Art(0x4BC1E64C,[2,3,5,7,9]); // node3 retains invalid formation; node9 is the authored static base.
        _=assets.SourceLayout(0xACEC3A70);
        Art(0x26B05ED2,[2,5]);
        foreach(uint file in new uint[]{0x3E2AD14F,0xE6446CC9}) Art(file,[3,5,8,11,13]);
        foreach(uint file in new uint[]{0xF0E45A19,0xE1B56178,0xD28668D7}) Art(file,[4,6,9,11,13]);
        foreach(uint file in new uint[]{0xACFB207E,0xAED4739D,0xA9487A40}) PortraitMasks(file,1);
        PortraitMasks(0xB20B608C,3); PortraitMasks(0xCB7DEBFC,9);
        foreach(int pane in new[]{38,44,50,58,64,70}) PortraitMasks(0xC11503D0,pane);

        var transport=assets.SourceLayout(TransportSetupLayoutSource.Load(repository).Source.FileId);
        int[] replaced=[27,96,102,113,119,130,136],hidden=[153,163,176,189];
        bool Used(int index)
        {
            for(int? at=index;at is int node;at=transport.Nodes[node].ParentIndex)
                if(replaced.Contains(node)||hidden.Contains(node))return false;
            return true;
        }
        assets.PrepareLayoutArt(transport.Source,transport.Nodes.Where(node=>(node.Type is 1 or 3)&&Used(node.Index))
            .Select(node=>node.Index),commonImports:true);
        foreach(var node in transport.Nodes.Where(node=>node.Type==6&&Used(node.Index))) _=transport.Type6(node.Index);

        foreach(string icon in new[]{"000_cmn_icon_close","000_cmn_icon_info","000_cmn_mrk_mass_base","000_cmn_mrk_mass"})
            assets.PrepareCommonSprite(icon);
        for(int state=0;state<4;state++)assets.PrepareCommonSprite("000_cmn_btn_hanyo_l_"+state);
        // Summary rows use the non-outlined formation variant; the actual PK
        // slot is distinct from the HUD's outlined formation sprite.
        for(int formation=1;formation<=15;formation++)
            assets.PrepareCommonSprite("000_cmn_icon_jin_"+formation,formation==15?"texture_pk_common":null);

        Fixed(0xCFF3CC08,[(3,0x32A),(5,0x32B),(7,0x32C),(9,0x20D),(11,0x225),(13,0x292)],13);
        Fixed(0xB20B608C,[(20,0x360),(49,0x22B),(79,0x20E),(90,0x234)],13);
        Fixed(0xC11503D0,[(30,0x3D2),(16,0x23D),(33,0x42C),(34,0x42D),(35,0x42E),(55,0x2BF),(75,0x2BC),(85,0x2BD)],13);
        foreach(uint file in new uint[]{0x27119CFA,0x7E8AD398})
            Fixed(file,Enumerable.Range(0,5).Select(index=>(5+index*2,0x238+index)),13);
        foreach(int style in new[]{13,73,74})
            Fixed(0x8EE99810,[(5,0x22F),(7,0x22C),(9,0x22D),(11,0x230),(13,0x22E)],style);
        Fixed(transport.Source.Source.FileId,[(5,0x205),(10,0x20E),(15,0x20C),(20,0x20D),(147,0x292)],13);
        DefaultFixed(transport,[(25,0x541),(95,0x20E),(112,0x20C),(129,0x20D)]);
        foreach(int id in new[]{0x59F,0x32E,0x26B,0x5B9}) Fixed(0xDC8AC8AA,[(2,id)],56);
        var make=assets.SourceLayout(0xFC796D9B);
        foreach(var (file,mount,id) in new[]{(0x172EDC24u,41,0x35D),(0xD45873AAu,43,0x35E)})
            TabText(assets.SourceLayout(file),Caption(id),make.Rectangle(mount).Size.X);
        // Transport's Basic tab uses the middle tab body even though it is
        // the only tab. It does not use the person page's first-tab profile.
        string transportBasic=Caption(0x3CD);
        TabText(assets.SourceLayout(0x9AFB27B7),transportBasic,transportBasic.Length*28+36);
        for(int index=0;index<5;index++)
        {
            uint file=index==0?0xF0E45A19u:index==4?0xD28668D7u:0xE1B56178u;
            var row=assets.SourceLayout(file).ResizedSubtree(0,new(make.Rectangle(53).Size.X,40),false);
            for(int style=57;style<=60;style++)Text(row,15,Caption(0x37F+index),style);
        }
        var calculator=assets.SourceLayout(0x0AF34C58);
        foreach(var (node,id,preset) in new[]{(108,0x5E1,(int?)13),(110,0x60B,(int?)null)})
        {
            var profile=MapFontProfile.FromNode(calculator.Nodes[node],calculator.Source.Source.ResourceUri) with
                {RawAlign=0x11,RawLineArrange=2};
            if(preset is int style)profile=profile.WithPreset(presets[style]);
            MapFont.PrepareText(font,Caption(id),profile,calculator.Rectangle(node).Size.X,caches);
        }
        int[] calculatorMounts=[75,81,3,21,27,33,39,45,51,57,63,69,9,15,94,88,100];
        string[] calculatorCaptions=[Caption(0x60A),Caption(0x609),"0","1","2","3","4","5","6","7","8","9",
            "00","000",Caption(0x5B),Caption(0x56D),Caption(0x26D)];
        for(int index=0;index<calculatorMounts.Length;index++)
        {
            var button=assets.SourceLayout(0xDE6DAAC5).ResizedSubtree(0,calculator.Rectangle(calculatorMounts[index]).Size,true);
            var profile=MapFontProfile.FromNode(button.Nodes[16],button.Source.Source.ResourceUri) with
                {WidthFitMode=2,RawCharacterMode=2};
            for(int style=3;style<=6;style++)MapFont.PrepareText(font,calculatorCaptions[index],
                profile.WithPreset(presets[style]),button.Rectangle(16).Size.X,caches);
        }
        foreach(string threshold in new[]{"3","5"})
            for(int style=3;style<=7;style++)Text(assets.SourceLayout(0xBCF8A04D),15,threshold,style);
        foreach(int id in new[]{0x3A,0x3B,0x32F,0x5F,0x60})
            for(int style=3;style<=7;style++)Fixed(0xCEE6F358,[(16,id)],style);
        int[] tabs=[0x3CD,0x22B,0x235,0x3DD,0x3DE,0x23D];
        for(int index=0;index<tabs.Length;index++)
        {
            var source=assets.SourceLayout(index==0?0x172EDC24u:index==tabs.Length-1?0xD45873AAu:0x9AFB27B7u);
            string caption=Caption(tabs[index]);
            float width=source.Rectangle(16).Size.X+caption.Length*28+36-source.Nodes[0].Value44;
            for(int style=8;style<=11;style++)Text(source,16,caption,style,width);
        }
        var columnSource=assets.SourceLayout(0x7A7B5B8C);
        foreach(var column in DeploymentPersonColumns.Basic.Concat(DeploymentPersonColumns.Formations)
            .Concat(DeploymentPersonColumns.TacticsFirst).Concat(DeploymentPersonColumns.TacticsSecond).Concat(DeploymentPersonColumns.Traits))
        {
            var profile=MapFontProfile.FromNode(columnSource.Nodes[13],columnSource.Source.Source.ResourceUri) with
                {WidthFitMode=2,WidthReserve=0};
            for(int style=3;style<=6;style++)MapFont.PrepareText(font,Caption(column.CaptionMessage),
                profile.WithPreset(presets[style]),column.Width-14,caches);
        }
        // The return control writes the existing footer narration profile.
        var narration=assets.SourceLayout(0xAB6ADAA7);
        var narrationNode=narration.Nodes.Single(node=>node.Type==2);
        MapFont.PrepareText(font,new AdministrationMessages(repository,locale).Format(0x2733),
            MapFontProfile.FromNode(narrationNode,narration.Source.Source.ResourceUri),
            narration.Rectangle(narrationNode.Index).Size.X,caches);
        // Dedicated ordinary Formation balloon; mounts4/7 replace their
        // authored children. Both category backgrounds are statically shared.
        Art(0x291C8AEE,[2,8,9]); Blur(0x291C8AEE,[1]);
        Art(0xF725023A,[1,2]);
        var formationMarker=assets.SourceLayout("cmn_mrker_s");
        foreach(string sprite in new[]{"000_cmn_mrk_base_yellow","000_cmn_mrk_base_cyan"})
            assets.PrepareCommonSprite(sprite);
        DefaultFixed(formationMarker,[(2,0x368),(2,0x369)]);
        Fixed(0xF725023A,[(3,0x36A)],13);
        var formationRange=assets.SourceLayout(0xF725023A);
        foreach(bool highlighted in new[]{false,true})
            foreach(int message in new[]{0x338,0x339})
                MapFont.PrepareText(font,Caption(message),
                    DeploymentFormationBalloonView.RangeProfile(formationRange,highlighted),
                    formationRange.Rectangle(4).Size.X,caches);
        // The common full tactic body is already prepared by the shared
        //gallery consumer. Deployment adds only the separator and its seven
        //literal disabled reasons, using the same startup resource owner.
        Art(0x19ED924D,[19]);
        var tacticBalloon=assets.SourceLayout(0x19ED924D);
        var deploymentMessages=new AdministrationMessages(repository,locale);
        for(int message=0x5688;message<=0x568E;message++)
            CollectionTacticBalloonView.PrepareWarningText(tacticBalloon,font,
                deploymentMessages.Format(message),caches);
        cancellationToken.ThrowIfCancellationRequested();

        string Caption(int id)=>records[id].Text??throw new InvalidDataException("Missing deployment static caption.");
        void Art(uint file,int[] nodes)
        {cancellationToken.ThrowIfCancellationRequested();var source=assets.SourceLayout(file);assets.PrepareLayoutArt(source.Source,nodes,commonImports:true);}
        void Blur(uint file,int[] nodes){var source=assets.SourceLayout(file);foreach(int node in nodes)_=source.Type6(node);}
        void TabText(UiSourceLayout source,string caption,float width)
        {for(int style=8;style<=11;style++)Text(source,16,caption,style,source.Rectangle(16).Size.X+width-source.Nodes[0].Value44);}
        void DefaultFixed(UiSourceLayout source,IEnumerable<(int Node,int Message)> bindings)
        {foreach(var binding in bindings)MapFont.PrepareText(font,Caption(binding.Message),
            MapFontProfile.FromNode(source.Nodes[binding.Node],source.Source.Source.ResourceUri),source.Rectangle(binding.Node).Size.X,caches);}
        void Fixed(uint file,IEnumerable<(int Node,int Message)> bindings,int preset)
        {var source=assets.SourceLayout(file);foreach(var binding in bindings)Text(source,binding.Node,Caption(binding.Message),preset);}
        void Text(UiSourceLayout source,int node,string caption,int preset,float? width=null)
        {MapFont.PrepareText(font,caption,MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri)
            .WithPreset(presets[preset]),width??source.Rectangle(node).Size.X,caches);}
        void PortraitMasks(uint file,int node)
        {
            var source=assets.SourceLayout(file);
            var group=LcskSpriteNodeReader.Read(source.Source.Layout,source.Nodes[node]).Groups.Single();
            if(group.ComponentCount is not (2 or 3)||group.Parts.Count!=group.ComponentCount)
                throw new InvalidDataException("Deployment portrait requires its actual source masks.");
            foreach(var part in group.Parts.Skip(1))
            {var mask=assets.SourceLayoutSprite(source.Source,part.SpriteName);assets.PrepareSprite(mask,MapPortraitQuad.UsesSrgbView(repository,assets,part.SpriteName,mask));}
        }
    }
}
