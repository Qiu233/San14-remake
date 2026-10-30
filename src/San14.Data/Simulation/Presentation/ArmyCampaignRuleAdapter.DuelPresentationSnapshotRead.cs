using San14.Core;
namespace San14.Data;
public sealed partial class ArmyCampaignRuleAdapter
{
    private DuelPresentationPerson ReadDuelPresentationPersonSnapshot(CampaignDuelPresentationPersonSnapshot? saved,int id)
    {
        if(saved is null)return new(id,null,null,null,null,[],["duel.presentation.source_snapshot_legacy_unknown"]);
        if(saved.OfficerId!=id)throw new InvalidDataException("duel.presentation.snapshot_participant_identity");
        var issues=saved.UnresolvedFields.ToList();OfficerPortraitRoute? portrait=null;
        if(saved.PortraitAppearanceIndex is ushort original&&saved.PortraitAdmittedAppearanceIndex is ushort admitted)
        {
            try
            {
                //The source content/App/user admission has already happened.
                //Only normal resource routing remains; never scan the later World.
                if(DlcPortraitDescriptor.ForAppearance(admitted) is not null)
                {
                    var route=_definitions.PortraitResources.ResolveDlcFacePortrait(admitted);
                    portrait=new(id,OfficerPortraitRouteKind.StockNamed,original,admitted,route.ResourceName,route.SpriteName,null,null)
                        {AdmittedAppearanceIndex=admitted};
                }
                else if(admitted is >=7000 and <=7999)
                {
                    var route=_definitions.PortraitResources.ResolveUserFacePortrait(admitted);
                    portrait=new(id,OfficerPortraitRouteKind.StockNamed,original,admitted,route.ResourceName,route.SpriteName,null,null)
                        {AdmittedAppearanceIndex=admitted};
                }
                else
                {
                    if(admitted is not(>=0 and <=1310 or >=2000 and <=2199 or >=2500 and <=2509 or
                        >=3000 and <=3005 or >=5000 and <=5399 or >=6000 and <=6214))
                        throw new InvalidDataException("duel.presentation.snapshot_unregistered_portrait_range");
                    var route=ArmyMapUiSources.Create().PortraitNames(admitted);
                    portrait=new(id,OfficerPortraitRouteKind.StockNamed,original,route.ResolvedAppearanceIndex,
                        route.ResourceName,route.SpriteName,null,null){AdmittedAppearanceIndex=admitted};
                }
            }
            catch(InvalidDataException error){issues.Add(error.Message);}
        }
        DetailForceAppearance? flag=saved.Flag is {} sourceFlag?new(true,sourceFlag.Glyph,
            MapSpritePalettes.ForceFill(sourceFlag.ColorIndex),MapSpritePalettes.Create().ForceLetter(sourceFlag.ColorIndex),null):null;
        return new(id,saved.Name,portrait,flag,saved.Strength,
            saved.Traits.Select(trait=>new MapSummaryTraitRow(trait.SourceIndex,trait.Text,trait.Style,true)).ToArray(),issues);
    }
}
