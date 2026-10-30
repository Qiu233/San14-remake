using San14.Core;

namespace San14.Data;

public sealed record ForeignTradeRewardPanel(uint LayoutId,CollectionImageRoute? Image,
    IReadOnlyDictionary<int,string> Fields,bool WeaponDescription)
{
    public San14.Data.Formats.RgbaTexture? ExternalImage {get;init;}
    public string? ImageResourceError {get;init;}
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The real caravan discovery body. Current item flags and names
    /// are separate from the retained resource definition's artwork/attributes.</summary>
    public AreaAdministrationQuery<ForeignTradeRewardPanel> QueryForeignTradeRewardPanel(GameState state,
        ForeignTradeReportStep step)
    {
        try
        {
            var messages=_repository.LoadMessages(Seed.Language,San14.Data.Formats.MessageKind.Data).Messages.Sections;
            string Message(int section,int id)=>messages[section].Records[id].Text??
                throw ForeignTradeReturnMissing("reward_panel_message");
            string none=Message(1,0x0353);
            var fields=new Dictionary<int,string>();int image;
            if(step.ItemId is >=1 and <=201)
            {
                var item=state.Items?.GetValueOrDefault(step.ItemId)??throw ForeignTradeReturnMissing("reward_panel_Item");
                var definition=state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)??throw ForeignTradeReturnMissing("reward_panel_Item_name");
                var appearance=definition.Appearance??throw ForeignTradeReturnMissing("reward_panel_current_Item_definition");
                image=appearance.ImageIndex;
                int ability=appearance.AbilityKind,subtype=appearance.EffectKind;
                fields[6]=definition.Name;
                fields[7]=Message(1,0x0452);fields[8]=ability==0?none:
                    Message(0,0x0183+ability-1)+"+"+appearance.AbilityValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                fields[9]=Message(1,0x0411);fields[10]=subtype==0?none:Message(0,0x0839+subtype);fields[11]="";
            }
            else if(step.GemId is >=1 and <=30)
            {
                var current=CaptureReportContentCurrent(state);
                var gem=current.ReadSkillGemReference(step.GemId)??throw ForeignTradeReturnMissing("reward_panel_Gem");
                image=current.ReadSkillGemRawByte(gem,0x53)??throw ForeignTradeReturnMissing("reward_panel_Gem_image");
                int tactic=current.ReadSkillGemTactic52(gem)??throw ForeignTradeReturnMissing("reward_panel_Gem_tactic");
                fields[6]=_skills.Gems.Gems[step.GemId].Name;fields[7]=Message(1,0x0452);fields[8]=none;
                fields[9]=Message(1,0x0411);fields[10]=tactic is >=1 and <=200&&state.Tactics.TryGetValue(tactic,out var definition)
                    ?definition.Name+Message(1,0x0237):"";fields[11]="";
            }
            else if(step.WeaponId is >=1 and <=8)
            {
                var definition=_repository.LoadForeignTechnologies(_scenario.FixedDataSource.SourcePath).Definitions[step.WeaponId];
                fields[6]=definition.Name;fields[7]=Message(0,0x138B+step.WeaponId);
                return AreaAdministrationQuery<ForeignTradeRewardPanel>.Known(new(0x5F9F176F,Image(definition.Value),fields,true));
            }
            else throw ForeignTradeReturnMissing("reward_panel_identity");
            var external=image is >=201 and <=401?ItemEditorExternalImages.ReadImage(image):null;
            return AreaAdministrationQuery<ForeignTradeRewardPanel>.Known(new(0xCC79B335,
                external is not null?external.Texture is null?Image(999):null:Image(image>=201?999:image),fields,false)
                {ExternalImage=external?.Texture,ImageResourceError=external?.Error});

            static CollectionImageRoute Image(int index)=>CollectionImageRoute.ForItem(index);
        }
        catch(InvalidDataException error)
        {return AreaAdministrationQuery<ForeignTradeRewardPanel>.Unknown(error.Message);}
    }
}
