using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class AreaInformationCurrent
    {
        private readonly Dictionary<int,ForceDisplay?> forceDisplays=[];
        private PreparedCell IdentityCell(Area area,int column)=>column switch
        {
            0=>MakeCell(()=>Need(Current.ReadAreaNativeValidity(area.Receiver),"Area_name_validity")?area.Owner.Name:"",
                ()=>new NameKey(AreaSortText(area))),
            1=>MakeCell(()=>CityName(area),()=>new NameKey(rules.ReadCityDetailNameSortKeyCurrent(Current,CitySlot(area)))),
            2=>ForceCell(area),
            3=>MakeCell(()=>DistrictNumber(area) is int number?FormatSceneryPanelNumber(Data(1,0x494),number):"",
                ()=>DistrictNumber(area) is int number?new NumberKey(number):new NumberKey(0,true)),
            4=>MakeCell(()=>CoverageText(area),()=>new CoverageKey(CoverageRatio(area),area.PhysicalSlot)),
            5=>MakeCell(()=>OverseerName(area),()=>new NameKey(OverseerSort(area))),
            _=>throw Missing("identity_column."+column)
        };
        private string AreaSortText(Area area)
        {
            var rows=rules._scenario.Snapshot.Areas;
            if((uint)area.PhysicalSlot>=(uint)rows.Count || rows[area.PhysicalSlot].Slot!=area.PhysicalSlot)
                throw Missing("Area_sort_definition");
            return rows[area.PhysicalSlot].SecondaryText??throw Missing("Area_sort_text1A");
        }
        private int CitySlot(Area area)
        {
            int raw=Need(Current.ReadAreaCitySlot(area.Receiver),"Area35_City");
            return raw<52?raw:0;
        }
        private BaseReference City(Area area)=>Current.ReadCityTableReference(CitySlot(area))??throw Missing("City_lookup");
        private string CityName(Area area)
        {
            int raw=Need(Current.ReadAreaCitySlot(area.Receiver),"Area35_City");
            if(raw==0)raw=Need(Current.ReadAreaValidityCode(area.Receiver),"Area34_original_City");
            if(raw==0)return "";
            var city=Current.ReadCityTableReference(raw<52?raw:0)??throw Missing("City_name_lookup");
            int physical=Need(city.PhysicalSlot,"City_name_receiver");
            if(physical!=0)return state.Cities.GetValueOrDefault(physical)?.Name??throw Missing("City_current_name");
            var rows=rules._scenario.Snapshot.Cities;
            if(rows.Count==0 || rows[0].Slot!=0)throw Missing("City0_definition");
            return rows[0].Name;
        }
        private int? DistrictNumber(Area area)
        {
            if(!Need(Current.ReadForceNativeValidity(Force(area.Receiver)),"District_Force_validity"))return null;
            int raw=Need(Current.ReadBaseDistrictSlot(City(area)),"City30_District");
            var district=Current.ReadDistrictTableReference(raw<52?raw:0)??throw Missing("District_lookup");
            if(!Need(Current.ReadDistrictNativeValidity(district),"District_validity"))return null;
            return Need(Current.ReadDistrictNumber(Need(district.PhysicalSlot,"District_receiver")),"District11_number");
        }
        private string CoverageText(Area area)
        {
            if(!Need(Current.ReadForceNativeValidity(Force(area.Receiver)),"coverage_Force_validity"))return "";
            int total=Need(Current.ReadAreaRawField76(area.Receiver),"Area76_terrain_count");
            if(total==0)return "";
            int owned=Need(Current.ReadAreaRawField68(area.Receiver),"Area68_owned_count");
            return Number(owned).PadLeft(3)+"/"+Number(total).PadLeft(3);
        }
        private int CoverageRatio(Area area)
        {
            if(!Need(Current.ReadForceNativeValidity(Force(area.Receiver)),"coverage_Force_validity"))return -1;
            int total=Need(Current.ReadAreaRawField76(area.Receiver),"Area76_terrain_count");
            if(total==0)return -1;
            return Need(Current.ReadAreaRawField68(area.Receiver),"Area68_owned_count")*1000/total;
        }
        private sealed record CoverageKey(int Ratio,int AreaPhysical):SortKey
        {
            internal override bool Before(SortKey other,bool ascending)
            {
                var right=(CoverageKey)other;
                if(!ascending)return right.Before(this,true);
                //353A60's equal-ratio tie is descending native Area ID.
                return Ratio!=right.Ratio?Ratio<right.Ratio:AreaPhysical>right.AreaPhysical;
            }
        }
        private string OverseerName(Area area)
        {int raw=Overseer(area);return raw==0?"":PersonText(Person(raw));}
        private string OverseerSort(Area area)
        {int raw=Overseer(area);return raw==0?"":PersonSortText(Person(raw));}
        private int Overseer(Area area)=>Need(Current.ReadAssignmentArea36(area.PhysicalSlot),"Area36_overseer");
        private int Person(int raw)=>Need((raw is >=0 and <=6000?Current.ReadPersonLookupReference(raw)
            :Current.ReadTransferPersonTableReference(0))?.PhysicalSlot,"Person_lookup");
        private string PersonText(int physical)
        {
            int id=Need(Current.ReadPersonIdentity(physical),"Person_identity");
            if(state.Officers.TryGetValue(id,out var owner))
                return (owner.CurrentSurname??throw Missing("Person_current_surname12"))+(owner.CurrentGivenName??throw Missing("Person_current_given20"));
            if(physical!=0)throw Missing("Person_name_owner");
            var rows=rules._scenario.Snapshot.Characters;
            if(rows.Count==0 || rows[0].Slot!=0)throw Missing("Person0_definition");
            return (rows[0].FamilyName??throw Missing("Person0_surname12"))+(rows[0].GivenName??throw Missing("Person0_given20"));
        }
        private string PersonSortText(int physical)
        {
            int id=Need(Current.ReadPersonIdentity(physical),"Person_identity");
            if(state.Officers.TryGetValue(id,out var owner))return owner.CurrentNameSortKey??throw Missing("Person_current_sort34_5A");
            if(physical!=0)throw Missing("Person_sort_owner");
            var rows=rules._scenario.Snapshot.Characters;
            if(rows.Count==0 || rows[0].Slot!=0)throw Missing("Person0_definition");
            return rows[0].NameSortKey??throw Missing("Person0_sort34_5A");
        }
        private PreparedCell ForceCell(Area area)
        {
            var cell=MakeCell(()=>DisplayForce(area)?.Text??"",()=>
            {
                var force=Force(area.Receiver);
                if(!Need(Current.ReadForceNativeValidity(force),"Force_sort_validity"))return new NameKey("",true);
                int raw=Need(Current.ReadForceRecord(Need(force.PhysicalSlot,"Force_receiver"))?.RawRuler10,"Force_ruler10");
                return new NameKey(PersonSortText(Person(raw)));
            });
            try{return cell with {Display=cell.Display with {MarkerColour=DisplayForce(area)?.Colour}};}
            catch(InvalidDataException error)
            {return cell with {Display=cell.Display with {UnresolvedOwner=cell.Display.UnresolvedOwner??error.Message}};}
        }
        private ForceDisplay? DisplayForce(Area area)
        {
            if(forceDisplays.TryGetValue(area.Id,out var saved))return saved;
            var force=Force(area.Receiver);
            if(!Need(Current.ReadForceNativeValidity(force),"Force_display_validity"))return forceDisplays[area.Id]=null;
            int physical=Need(force.PhysicalSlot,"Force_receiver");string body;
            if(physical is >=46 and <=50)
            {
                int city=Need(Current.ReadCityTableReference(physical+1)?.PhysicalSlot,"Force_special_City_receiver");
                body=state.Cities.GetValueOrDefault(city)?.Name??throw Missing("Force_special_City_name");
            }
            else
            {
                int person=Person(Need(Current.ReadForceRecord(physical)?.RawRuler10,"Force_ruler10"));
                if(!Need(Current.ReadPersonNativeValidity(person),"Force_ruler_validity"))return forceDisplays[area.Id]=null;
                body=PersonText(person);
            }
            byte colour=Need(Current.ReadForceReportColor(force),"Force49_colour");
            return forceDisplays[area.Id]=new(Data(1,0x571)+body,MapSpritePalettes.ForceFill(colour));
        }
        private sealed record ForceDisplay(string Text,AreaMarkerColor Colour);
    }
}
