using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>906730's ordinary District page. Identity, economics and
    /// composite controls all borrow the same current from the supplied World.</summary>
    public DistrictInformationPage QueryDistrictInformation(GameState state,int districtId)
    {
        var text=new Dictionary<int,string>();
        var missing=new Dictionary<int,string>();
        var styles=new Dictionary<int,int>();
        var hidden=new HashSet<int>{38,54};
        string name="";
        int? leaderId=null,appearanceId=null,forceId=null,number=null;
        DetailForceAppearance? flag=null;
        DeploymentNarrationPortraitRoute? standing=null;
        DetailMiniMapValues? map=null;
        try
        {
            if(districtId is <1 or >51 || !state.Districts.ContainsKey(districtId))throw Missing("District_identity");
            var current=CaptureOwnedArmyReturnWorld(state).Context;
            BaseReturnCaptivesInputs.BindCurrent(state,current);
            var district=current.ReadDistrictTableReference(districtId) ?? throw Missing("District_pointer");
            int physical=K(district.PhysicalSlot,"District_physical");
            if(!K(current.ReadDistrictNativeValidity(district),"District_validity"))throw Missing("District_invalid");
            foreach(var caption in DistrictInformationSources.Captions)
            {Read(caption.Node,()=>text[caption.Node]=Data(1,caption.Message));styles[caption.Node]=13;}
            foreach(int node in new[]{12,19,24,32,37,42,48,53,58,64,69,74,82,87,92,99,104})styles[node]=0;
            Read(4,()=>
            {
                number=K(current.ReadDistrictNumber(physical),"District11_number");
                //22E6B0 prefixes a category9 inline sprite, published below
                //as NameIconSpriteId; it is not a Data section9 message.
                name=ReadDistrictInformationLeaderCaption(state,current,physical);
            });
            Read(4,()=>
            {
                int raw=K(current.ReadDistrictForceId(physical),"District10_Force");forceId=raw;
                var force=current.ReadForceTableReference(raw<52?raw:0) ?? throw Missing("Force_pointer");
                _mapInformationNames ??=ArmyFlagPaneNames.Load(_scenario);
                flag=ReadDetailForceAppearance(state,current,_mapInformationNames,force,
                    K(current.ReadForceNativeValidity(force),"Force_validity"),MapSpritePalettes.Create()) ?? throw Missing("Force_flag");
            });
            Read(12,()=>
            {
                var person=Leader();int at=K(person.PhysicalSlot,"Leader_pointer");
                if(!K(current.ReadPersonNativeValidity(at),"Leader_validity")){text[12]="";return;}
                int id=K(current.ReadPersonIdentity(at),"Leader_identity");leaderId=id;
                text[12]=state.Officers.GetValueOrDefault(id)?.Name ?? throw Missing("Leader_name");
            });
            Read(2,()=>
            {
                var person=Leader();int at=K(person.PhysicalSlot,"Portrait_Leader_pointer");
                bool valid=K(current.ReadPersonNativeValidity(at),"Portrait_Leader_validity");
                ushort appearance=valid?K(current.ReadPersonPortraitAppearance(at),"Portrait_A6"):(ushort)2198;
                int id=valid?K(current.ReadPersonIdentity(at),"Portrait_Leader_identity"):0;
                if(valid)leaderId=id;
                appearanceId=appearance;
                standing=valid?ReadNarrationPortrait(state,current,_definitions,id,appearance)
                    :new DeploymentNarrationPortraitRoute(2198,2198,"texture_chara_anime_2198",["face_2198_b","face_2198_a","face_2198_c"]){Kind=StandingPortraitKind.ThreePart};
            });
            Read(19,()=>
            {
                //22AAC0's fixed City iterator includes each exact current
                //District reference; it is not the Force's whole city registry.
                int count=0;
                for(int slot=0;slot<52;slot++)
                {
                    var city=current.ReadCityTableReference(slot) ?? throw Missing("City_table");
                    int owner=K(current.ReadCityDistrictReference(city)?.PhysicalSlot,"City_District");
                    if(owner==physical)count++;
                }
                text[19]=Integer(count);
            });
            Read(24,()=>text[24]=Integer(ReadDistrictInformationPersonCount(state,current,physical)));
            Read(82,()=>
            {
                byte index=K(current.ReadDistrictNumber(physical),"Policy_District11");
                if(index==1){text[82]="";return;}
                byte policy=K(current.ReadDistrictRawByte(physical,0x17),"Policy17");
                text[82]=policy is >=1 and <=5?Data(0,0x175+policy):"";
            });
            Read(87,()=>
            {
                byte kind=K(current.ReadDistrictRawByte(physical,0x19),"Target_kind19");
                if(kind is not(2 or 7 or 8)){text[87]="";return;}
                byte raw=K(current.ReadDistrictRawByte(physical,0x18),"Target18");
                text[87]=kind switch
                {
                    2=>FootholdName(raw),
                    7=>ForceName(raw),
                    _=>FormatSceneryPanelNumber(Data(1,0x494),K(current.ReadDistrictNumber(raw<52?raw:0),"Target_District11"))
                };
            });
            Read(92,()=>
            {
                byte priority=K(current.ReadDistrictRawByte(physical,0x1A),"Priority1A");
                text[92]=priority is >=1 and <=4?Data(0,0x17B+priority):"";
            });
            Read(93,()=>
            {
                var person=Leader();int at=K(person.PhysicalSlot,"MiniMap_Leader_pointer");
                //The source independently reads E2 even for an invalid leader.
                ushort home=K(current.ReadPersonFirstLocation(at),"MiniMap_Leader_E2");
                var location=BaseLookup.Find(home,current);
                ushort hex=current.ReadBaseCurrentHexIndex(location);
                byte owner=K(current.ReadDistrictForceId(physical),"MiniMap_District10");
                byte color=K(ReadMiniMapColor(current,owner),"MiniMap_Force_colour");
                map=new(ReadDetailMiniMapBases(current) ?? throw Missing("MiniMap_bases"),
                    DetailMiniMapProjection.ProjectDistrict(physical,hex,color));
            });
            Read(-2,()=>
            {
                var economy=ReadDistrictDetailEconomyCurrent(state,current,physical);
                foreach(var item in economy.Text)text[item.Key]=item.Value;
                foreach(var item in economy.UnresolvedFields)missing[item.Key]=item.Value;
            });
            TransferPersonReference Leader()
            {
                ushort raw=K(current.ReadDistrictLeader(physical),"District12_Leader");
                return (raw<=6000?current.ReadPersonLookupReference(raw):current.ReadTransferPersonTableReference(0)) ?? throw Missing("Leader_lookup");
            }
            string FootholdName(ushort raw)
            {
                var location=BaseLookup.Find(raw,current);
                int id=K(current.ReadBaseObjectLink(location),"Target_Base4E");
                return location.Kind==BaseKind.City
                    ?id==0?"":state.Cities.GetValueOrDefault(id)?.Name ?? throw Missing("Target_City_name")
                    :id==0?"":state.Gates.GetValueOrDefault(id)?.Name ?? throw Missing("Target_Gate_name");
            }
            string ForceName(int raw)
            {
                var target=current.ReadForceTableReference(raw<52?raw:0) ?? throw Missing("Target_Force_pointer");
                if(!K(current.ReadForceNativeValidity(target),"Target_Force_validity"))return "";
                return new CampaignHistoryWriter(_repository,_scenario,Seed.Language).FallbackName(state,current,target,
                    (person,full)=>full?person.Name:person.CurrentSurname ?? throw Missing("Target_Force_ruler_surname"));
            }
        }
        catch(InvalidDataException error){missing[-1]=error.Message;}
        return new(districtId,name,text){HiddenNodes=hidden,TextStyles=styles,UnresolvedFields=missing,
            LeaderId=leaderId,PortraitAppearanceId=appearanceId,StandingPortrait=standing,ForceId=forceId,DistrictNumber=number,
            ForceFlag=flag,MiniMap=map,NameIconSpriteId=number is int n?ArmyMapUiSources.DistrictIcon(checked((byte)n)).SpriteId:null,UnresolvedOwner=missing.Count==0?null:string.Join("; ",missing.Values.Distinct(StringComparer.Ordinal))};
        string Data(int section,int id)=>ReadHistoryDataText(section,id);
        void Read(int node,Action read){try{read();}catch(InvalidDataException error){missing[node]=error.Message;}}
        static string Integer(int value)=>value.ToString(CultureInfo.InvariantCulture);
        static T K<T>(T? value,string field)where T:struct=>value ?? throw Missing(field);
        static InvalidDataException Missing(string field)=>new("district-information."+field);
    }

    private string ReadDistrictInformationLeaderCaption(GameState state,SourceWorldContext current,int district)
    {
        ushort raw=Need(current.ReadDistrictLeader(district),"Leader12");
        var receiver=(raw<=6000?current.ReadPersonLookupReference(raw):current.ReadTransferPersonTableReference(0))
            ?? throw Missing("Leader_pointer");
        int at=Need(receiver.PhysicalSlot,"Leader_physical");
        if(!Need(current.ReadPersonNativeValidity(at),"Leader_validity"))return "";
        int id=Need(current.ReadPersonIdentity(at),"Leader_identity");
        var person=state.Officers.GetValueOrDefault(id) ?? throw Missing("Leader_owner");
        if(district is >=46 and <=50)
            return id is >=5101 and <=5200 or >=2062 and <=2066?person.Name:person.CurrentSurname ?? throw Missing("Leader_surname");
        return person.Name+ReadHistoryDataText(1,0x350);
        static T Need<T>(T? value,string field)where T:struct=>value ?? throw Missing(field);
        static InvalidDataException Missing(string field)=>new("district-information.name."+field);
    }

    //35F800 ->230350, registered District persons in status1..4.
    private static int ReadDistrictInformationPersonCount(GameState state,SourceWorldContext current,int district,bool? specialFilter=null)
    {
        int count=0;
        foreach(int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person_registry100"))
        {
            int at=Need(entry,"Person_pointer");
            byte owner=Need(current.ReadPersonDistrictSlot(at),"PersonE0");
            if(owner!=district)continue;
            byte status=Need(current.ReadPersonStatus(at),"PersonE6");
            if(status is <1 or >4)continue;
            if(specialFilter!=false && district is >=46 and <=50)
            {
                int id=Need(current.ReadPersonIdentity(at),"Person10");
                int first=Need(current.ReadPersonFirstLocation(at),"PersonE2");
                bool hasHome=first is >=1 and <=61;
                bool band=BattleRemakeCurrentRegionFactor.IsNativeBandPerson(id);
                if(!hasHome || band)
                {
                    int force=Need(current.ReadPersonOldForceSlot(at),"Person_current_Force");
                    bool special=force is >=46 and <=50;
                    if(!hasHome && special || band && hasHome && !special)continue;
                }
            }
            count++;
        }
        return count;
        static T Need<T>(T? value,string field)where T:struct=>value ?? throw Missing(field);
        static InvalidDataException Missing(string field)=>new("district-information.people."+field);
    }
}
