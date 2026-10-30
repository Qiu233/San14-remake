using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Projects only the actual caller's retained, ordered identities.
    /// One current serves all slots; no full detail/economy query or new list
    /// membership, sorting, deduplication or world enumeration is performed.</summary>
    public IReadOnlyList<InformationDetailSelectionItem> QueryInformationDetailSelection(
        GameState state,InformationListKind kind,IReadOnlyList<int> ids,int? playerForceId=null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(ids);
        if(kind is not(InformationListKind.Cities or InformationListKind.Gates or InformationListKind.Forces or
            InformationListKind.Areas or InformationListKind.Districts or InformationListKind.Officers or InformationListKind.Items))
            throw new ArgumentOutOfRangeException(nameof(kind),"Only ordinary City/Gate/Force/Area/District/Officer/Item detail selections have this source contract.");
        if(ids.Count==0)return [];
        //Item captions/keys are independent saved definition owners. Do not
        //capture all current people/Forces just to prepare this retained list.
        if(kind==InformationListKind.Items)return ReadItemDetailSelectionCurrent(state,ids,Seed.Language);
        SourceWorldContext current;
        try { current=CaptureDetailForceInputs(state) ?? throw Missing("current_name_owner"); }
        catch(InvalidDataException error)
        { return ids.Select(id=>new InformationDetailSelectionItem(id,null,error.Message){SortUnresolvedOwner=error.Message}).ToArray(); }
        var rows=new InformationDetailSelectionItem[ids.Count];
        //The application may supply its actual current player. Otherwise
        //only the existing saved current-player owner may supply the source.
        var player=new Lazy<ForceReference>(()=>
        {
            if(playerForceId is int explicitPlayer)
            {
                Check(explicitPlayer,52,"sort_player_Force_index");
                return current.ReadForceTableReference(explicitPlayer) ?? throw Missing("sort_player_Force_pointer");
            }
            return OwnedCurrentPlayerForce.ReadCurrentReference(state,current) ?? throw Missing("sort_player_Force_owner");
        });
        for(int index=0;index<ids.Count;index++)
        {
            int id=ids[index];
            string? caption=null,unresolved=null;
            int? icon=null;
            try
            {
                caption=Head(kind switch
                {
                    InformationListKind.Cities=>City(id),
                    InformationListKind.Gates=>Gate(id),
                    InformationListKind.Forces=>Force(id),
                    InformationListKind.Areas=>Area(id),
                    InformationListKind.Officers=>OfficerName(id),
                    _=>District(id)
                });
            }
            catch(InvalidDataException error){unresolved=error.Message;}
            if(kind==InformationListKind.Areas)
            {
                try { icon=AreaIcon(id); }
                catch(InvalidDataException error)
                { unresolved=unresolved is null?error.Message:unresolved+"; "+error.Message; }
            }
            if(kind==InformationListKind.Districts)
            {
                try { icon=DistrictIcon(id); }
                catch(InvalidDataException error)
                { unresolved=unresolved is null?error.Message:unresolved+"; "+error.Message; }
            }
            string? sortText=null,sortMissing=null;
            int? sortForce=null,sortNumber=null;
            bool? isPlayer=null;
            try
            {
                switch(kind)
                {
                    case InformationListKind.Cities: sortText=ReadCityDetailNameSortKeyCurrent(current,id); break;
                    case InformationListKind.Gates: sortText=GateSort(id); break;
                    case InformationListKind.Areas: sortText=AreaSort(id); break;
                    case InformationListKind.Officers: sortText=OfficerSort(id); break;
                    case InformationListKind.Forces:
                        //The visible cell and header sorting cell are both230FB0.
                        sortText=caption ?? throw new InvalidDataException(unresolved ?? "information-detail.selection.sort_Force_name_key");
                        break;
                    default:
                        Check(id,52,"sort_District_index");
                        var district=current.ReadDistrictTableReference(id) ?? throw Missing("sort_District_pointer");
                        int physical=Need(district.PhysicalSlot,"sort_District_physical");
                        int raw=Need(current.ReadDistrictForceId(physical),"sort_District_Force10");
                        var force=current.ReadForceTableReference(raw<52?raw:0) ?? throw Missing("sort_District_Force_pointer");
                        sortForce=Need(current.ReadForcePhysicalSlot(force),"sort_District_Force_physical");
                        sortNumber=Need(current.ReadDistrictNumber(physical),"sort_District_number11");
                        isPlayer=Need(current.ReadForceNativeValidity(player.Value),"sort_player_Force_validity") &&
                            Need(current.ReadForcePhysicalSlot(player.Value),"sort_player_Force_physical")==sortForce;
                        break;
                }
            }
            catch(InvalidDataException error){sortMissing=error.Message;}
            rows[index]=new(id,caption,unresolved){IconSpriteId=icon,SortText=sortText,
                SortForceId=sortForce,SortDistrictNumber=sortNumber,SortForceIsPlayer=isPlayer,SortUnresolvedOwner=sortMissing};
        }
        return rows;

        TransferPersonReference Officer(int id)
        {
            Check(id,6001,"Person_table_index");
            return current.ReadPersonLookupReference(id) ?? throw Missing("Person_pointer");
        }
        string OfficerName(int id)
        {
            var person=Officer(id);
            int physical=Need(person.PhysicalSlot,"Person_physical");
            int identity=Need(current.ReadPersonIdentity(physical),"Person_current_identity");
            //7796D0 ->22F020 reads current display name12+20 directly.
            //It is not the separate231080 sorting key34+5A.
            return state.Officers.GetValueOrDefault(identity)?.Name ?? throw Missing("Person_current_name12_20");
        }
        string OfficerSort(int id)
        {
            var person=Officer(id);
            //34D300 returns a known empty key for a known-invalid Person;
            //an unavailable native-validity input remains unknown.
            if(person.PhysicalSlot is not int physical)return "";
            if(!Need(current.ReadPersonNativeValidity(physical),"sort_Person_validity"))return "";
            int identity=Need(current.ReadPersonIdentity(physical),"sort_Person_current_identity");
            return state.Officers.GetValueOrDefault(identity)?.CurrentNameSortKey
                ?? throw Missing("Person_current_name_key34_5A");
        }
        string City(int id)
        {
            Check(id,52,"City_table_index");
            var city=current.ReadCityTableReference(id) ?? throw Missing("City_pointer");
            int physical=Need(city.PhysicalSlot,"City_physical");
            //35E9F0 directly returns this current City's name12. It does
            //not impose the Area getter's extra native-validity condition.
            return state.Cities.GetValueOrDefault(physical)?.Name ?? throw Missing("City_current_name12");
        }
        string Area(int id)
        {
            Check(id,501,"Area_table_index");
            var area=current.ReadAreaTableReference(id) ?? throw Missing("Area_pointer");
            if(!Need(current.ReadAreaNativeValidity(area),"Area_validity"))return "";
            int physical=Need(area.PhysicalSlot,"Area_physical");
            return state.Areas.GetValueOrDefault(physical)?.Name ?? throw Missing("Area_current_name10");
        }
        string AreaSort(int id)
        {
            Check(id,501,"sort_Area_index");
            var area=current.ReadAreaTableReference(id) ?? throw Missing("sort_Area_pointer");
            int physical=Need(area.PhysicalSlot,"sort_Area_physical");
            var rows=_scenario.Snapshot.Areas;
            if((uint)physical>=(uint)rows.Count || rows[physical].Slot!=physical)throw Missing("sort_Area_definition");
            return rows[physical].SecondaryText ?? throw Missing("sort_Area_fixed_text1A");
        }
        int? AreaIcon(int id)
        {
            Check(id,501,"Area_icon_table_index");
            var area=current.ReadAreaTableReference(id) ?? throw Missing("Area_icon_pointer");
            int raw=Need(current.ReadAreaHexIndex(area),"Area_icon_representative_hex3A");
            var hex=current.ReadHexTableReference(raw<48400?raw:0) ?? throw Missing("Area_icon_Hex_pointer");
            return Need(current.ReadHexSurfaceCode(hex),"Area_icon_surface10") switch
            {16=>0x1C3,17=>0x198,18=>0x1D6,_=>null};
        }
        string Force(int id)
        {
            Check(id,52,"Force_table_index");
            var force=current.ReadForceTableReference(id) ?? throw Missing("Force_pointer");
            if(!Need(current.ReadForceNativeValidity(force),"Force_validity"))return "";
            int physical=Need(current.ReadForcePhysicalSlot(force),"Force_physical");
            ushort raw=physical==0?(ushort)5014:Need(current.ReadForceRecord(physical)?.RawRuler10,"Force_current_ruler10");
            var ruler=(raw<=6000?current.ReadPersonLookupReference(raw):current.ReadTransferPersonTableReference(0))
                ?? throw Missing("Force_ruler_lookup");
            int person=Need(ruler.PhysicalSlot,"Force_ruler_physical");
            if(!Need(current.ReadPersonNativeValidity(person),"Force_ruler_validity"))return "";
            int identity=Need(current.ReadPersonIdentity(person),"Force_ruler_identity");
            var owner=state.Officers.GetValueOrDefault(identity) ?? throw Missing("Force_current_ruler_owner");
            //230FB0 reads the231080 name-key pair34+5A. The existing
            //CurrentNameSortKey is precisely that current concatenation;
            //using the Force name/kingdom or display-name12+20 is different.
            if(physical is >=46 and <=50 && identity is not(>=5101 and <=5200 or >=2062 and <=2066))
                return owner.CurrentSurnameSortKey ?? throw Missing("Force_ruler_first_name_key34");
            return owner.CurrentNameSortKey ?? throw Missing("Force_ruler_current_name_key34_5A");
        }
        BaseReference GateReceiver(int id)
        {
            if(id is <52 or >61)throw Missing("Gate_identity");
            return current.ReadGateTableReference(id-51) ?? throw Missing("Gate_pointer");
        }
        string Gate(int id)
        {
            int physical=Need(GateReceiver(id).PhysicalSlot,"Gate_physical");
            //35E9F0 reads Name12 directly; no new native-validity gate.
            return state.Gates.GetValueOrDefault(physical+51)?.Name ?? throw Missing("Gate_current_name12");
        }
        string GateSort(int id)
        {
            int physical=Need(GateReceiver(id).PhysicalSlot,"sort_Gate_physical");
            var rows=_scenario.Snapshot.Gates;
            if((uint)physical>=(uint)rows.Count || rows[physical].Slot!=physical)throw Missing("sort_Gate_definition");
            return rows[physical].SecondaryText ?? throw Missing("sort_Gate_fixed_text1A");
        }
        int? DistrictIcon(int id)
        {
            Check(id,52,"District_icon_index");
            var district=current.ReadDistrictTableReference(id) ?? throw Missing("District_icon_pointer");
            if(!Need(current.ReadDistrictNativeValidity(district),"District_icon_validity"))return null;
            byte number=Need(current.ReadDistrictNumber(Need(district.PhysicalSlot,"District_icon_receiver")),"District_icon_number11");
            return ArmyMapUiSources.DistrictIcon(number).SpriteId;
        }
        string District(int id)
        {
            Check(id,52,"District_table_index");
            var district=current.ReadDistrictTableReference(id) ?? throw Missing("District_pointer");
            if(!Need(current.ReadDistrictNativeValidity(district),"District_validity"))return "";
            int physical=Need(district.PhysicalSlot,"District_physical");
            //309990(9,enum) is an inline sprite-bank marker, not a Data
            //message section. Publish the real icon separately above.
            return ReadDistrictInformationLeaderCaption(state,current,physical);
        }
        static string Head(string value)=>value.IndexOf('\0') is int index && index>=0?value[..index]:value;
        static void Check(int id,int count,string field){if((uint)id>=(uint)count)throw Missing(field);}
        static T Need<T>(T? value,string field)where T:struct=>value ?? throw Missing(field);
        static InvalidDataException Missing(string field)=>new("information-detail.selection."+field);
    }
}
