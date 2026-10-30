using System.Collections.Immutable;
using San14.Core;
namespace San14.Data;
public sealed partial class ArmyCampaignRuleAdapter
{
    //The gauge constructor8083A0 reads these values while37B8E0 opens the
    //real duel, before phase21 aftermath and subsequent reward/death writes.
    private CampaignDuelPresentationSnapshot? CaptureDuelPresentationSnapshotCurrent(GameState state,
        SourceWorldContext current,CampaignDuelPair pair)
    {
        if(pair.PlayerSide==-1||pair.PresentationSetting is null or 0)return null;
        return new(Person(pair.ActorOfficerId),Person(pair.OpponentOfficerId));
        CampaignDuelPresentationPersonSnapshot Person(int id)
        {
            var issues=new List<string>();string? name=null;ushort? appearance=null,admitted=null;
            CampaignDuelFlagSnapshot? flag=null;int? strength=null;var traits=new List<CampaignDuelTraitSnapshot>();
            int? physical=null;OfficerState? person=null;
            Read(()=>physical=current.ReadPersonLookupReference(id)?.PhysicalSlot??throw Missing("person_pointer"));
            Read(()=>person=state.Officers.GetValueOrDefault(id)??throw Missing("person_owner"));
            Read(()=>name=Head((person?.CurrentSurname??throw Missing("current_surname"))+
                (person.CurrentGivenName??throw Missing("current_given_name"))));
            Read(()=>
            {
                appearance=person?.PortraitAppearanceIndex??throw Missing("portrait_A6");
                var route=ReadOfficerPortraitAppearance(state,current,_definitions,id,appearance.Value,ArmyMapUiSources.Create());
                if(route.Kind==OfficerPortraitRouteKind.Unresolved)throw new InvalidDataException(route.UnresolvedInput??Missing("portrait_route").Message);
                admitted=route.AdmittedAppearanceIndex??throw Missing("portrait_admission");
            });
            Read(()=>
            {
                var force=current.ReadPersonForce(physical??throw Missing("person_pointer"))??throw Missing("person_force");
                bool valid=current.ReadForceNativeValidity(force)??throw Missing("force_validity");
                if(!valid)return; //Actual constructor does not create its flag.
                byte colour=current.ReadForceReportColor(force)??throw Missing("force49_colour");
                _mapInformationNames??=ArmyFlagPaneNames.Load(_scenario);
                var value=ReadDetailForceAppearance(state,current,_mapInformationNames,force,true,MapSpritePalettes.Create())
                    ??throw Missing("flag_current");
                flag=new(colour,Head(value.Glyph));
            });
            Read(()=>strength=new TacticRemakeCurrentAbilities(current,_scenario,Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language)).ResolveDimension(state,id,1,
                    (force,effect)=>_baseCapacityInputs.ReadPanelModifier(state,force,effect))??throw Missing("effective_strength"));
            Read(()=>
            {
                if(Seed.Language is not("TC" or "SC"))throw Missing("trait_category16_locale");
                //TC89CB70 / SC89D610 and their actual category16 tables are
                //paired in sc-rules150; retain the first five admitted slots.
                for(int index=0;index<9&&traits.Count<5;index++)
                {
                    var trait=TacticRemakeCurrentTraits.ResolveCommanderPersonality(current,physical??throw Missing("person_pointer"),index)
                        ??throw Missing("trait_pointer");
                    int key=trait.PhysicalSlot??throw Missing("trait_identity");
                    if(key is <1 or >355 or 87)continue;
                    byte first=current.ReadPersonalityByte(trait,0xB6)??throw Missing("trait_effect1");
                    byte second=current.ReadPersonalityByte(trait,0xBA)??throw Missing("trait_effect2");
                    if(!DuelPresentationTraitCategory(first)&&!DuelPresentationTraitCategory(second))continue;
                    byte style=current.ReadPersonalityByte(trait,0xBE)??throw Missing("trait_style");
                    traits.Add(new(key,Head(current.ReadPersonalityName(trait)??throw Missing("trait_name")),style));
                }
            });
            return new(id,name,appearance,admitted,flag,strength,traits.ToImmutableArray(),issues.ToImmutableArray());
            void Read(Action read){try{read();}catch(InvalidDataException error){issues.Add(error.Message);}}
        }
        static string Head(string text)=>text.IndexOf('\0') is int end&&end>=0?text[..end]:text;
        static InvalidDataException Missing(string field)=>new("duel.source-presentation."+field);
    }
}
