using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class CityInformationCurrent
    {
        private readonly Dictionary<int,ForceDisplay?> forceDisplays = [];
        private Dictionary<(int Home,int Mode),int>? personCounts;
        private PreparedCell BasicCell(City city,int column) => column switch
        {
            0 => MakeCell(() => city.Owner.Name, () => new NameKey(rules.ReadCityDetailNameSortKeyCurrent(Current,city.Id))),
            1 => ForceCell(city),
            2 => MakeCell(() => DistrictText(city), () => DistrictKey(city)),
            3 => MakeCell(() => GovernorName(city), () => PersonKey(Governor(city))),
            4 => MakeCell(() => SizeText(city), () => SpecialHome(city) ? new NumberKey(0,true)
                : new NumberKey(Need(Current.ReadCitySizeB8(city.Receiver),"CityB8_size"))),
            5 => MakeCell(() => Special(city) ? Data(1,0x353) : Number(Resource(city,BaseResourceKind.Gold)),
                () => Special(city) ? new NumberKey(0,true) : new NumberKey(Resource(city,BaseResourceKind.Gold))),
            6 => MakeCell(() => Special(city) ? Data(1,0x353) : Number(Resource(city,BaseResourceKind.Supplies)),
                () => Special(city) ? new NumberKey(0,true) : new NumberKey(Resource(city,BaseResourceKind.Supplies))),
            7 => MakeCell(() => Number(Resource(city,BaseResourceKind.Troops)), () => new NumberKey(Resource(city,BaseResourceKind.Troops))),
            8 => MakeCell(() => Number(People(city,0)), () => new NumberKey(People(city,0))),
            9 => PeopleCell(city,2),
            10 => PeopleCell(city,1),
            11 => MakeCell(() => EmperorHome(city) ? Data(1,0x338) : "",
                () => EmperorHome(city) ? new NumberKey(1) : new NumberKey(0,true)),
            12 => MakeCell(() => Number(Wounded(city)), () => new NumberKey(Wounded(city))),
            13 => MakeCell(() => Number(Need(Current.ReadBaseMorale44(city.Receiver),"Morale44")),
                () => new NumberKey(Need(Current.ReadBaseMorale44(city.Receiver),"Morale44"))),
            14 => MakeCell(() => Number(BasePostLoad.ReadLinkedField14(city.Receiver,Current)),
                () => new NumberKey(BasePostLoad.ReadLinkedField14(city.Receiver,Current))),
            15 => MakeCell(() => Tactic(city) is int id ? state.Tactics.GetValueOrDefault(id)?.Name
                    ?? throw Missing("Tactic_current_name") : "",
                () => Tactic(city) is int id ? new NameKey(rules._skills.Formations.Tactics[id].Pinyin) : new NameKey("",true)),
            16 or 17 => MakeCell(() => AbnormalText(city,column), () => new NumberKey(AbnormalKey(city,column))),
            18 => MakeCell(() => Data(1,Nearby(city) ? 0x338 : 0x339), () => new NumberKey(Nearby(city) ? 1 : 0)),
            _ => throw Missing("basic_column."+column)
        };

        private int Resource(City city,BaseResourceKind kind) => Need(Current.ReadBaseResource(city.Receiver,kind),"resource."+kind);
        private int Wounded(City city) => Need(Current.ReadArmyReturnBaseWounded40(city.Receiver),"Wounded40");
        private int Governor(City city) => Person(Need(Current.ReadArmyReturnBaseGovernor2E(city.Receiver),"Governor2E"));
        private string GovernorName(City city)
        {
            ushort raw = Need(Current.ReadArmyReturnBaseGovernor2E(city.Receiver),"Governor2E");
            return raw == 0 ? "" : PersonText(Person(raw));
        }
        private int Person(int raw) => Need((raw is >= 0 and <= 6000 ? Current.ReadPersonLookupReference(raw)
            : Current.ReadTransferPersonTableReference(0))?.PhysicalSlot,"Person_lookup");
        private string PersonText(int physical)
        {
            int id = Need(Current.ReadPersonIdentity(physical),"Person_identity");
            if (state.Officers.TryGetValue(id,out var owner))
                return (owner.CurrentSurname ?? throw Missing("Person_current_surname12"))
                    +(owner.CurrentGivenName ?? throw Missing("Person_current_given20"));
            if (physical != 0) throw Missing("Person_current_name_owner");
            var rows = rules._scenario.Snapshot.Characters;
            if (rows.Count == 0 || rows[0].Slot != 0) throw Missing("Person0_definition");
            return (rows[0].FamilyName ?? throw Missing("Person0_surname12"))
                +(rows[0].GivenName ?? throw Missing("Person0_given20"));
        }
        private string PersonSortText(int physical)
        {
            int id = Need(Current.ReadPersonIdentity(physical),"Person_identity");
            if (state.Officers.TryGetValue(id,out var owner)) return owner.CurrentNameSortKey ?? throw Missing("Person_current_sort34_5A");
            if (physical != 0) throw Missing("Person_current_sort_owner");
            var rows = rules._scenario.Snapshot.Characters;
            if (rows.Count == 0 || rows[0].Slot != 0) throw Missing("Person0_definition");
            return rows[0].NameSortKey ?? throw Missing("Person0_sort34_5A");
        }
        private NameKey PersonKey(int physical) => Need(Current.ReadPersonNativeValidity(physical),"Person_sort_validity")
            ? new(PersonSortText(physical)) : new("",true);

        private PreparedCell ForceCell(City city)
        {
            var cell = MakeCell(() => DisplayForce(city)?.Text ?? "", () =>
            {
                var force = Force(city.Receiver);
                if (!Need(Current.ReadForceNativeValidity(force),"Force_sort_validity")) return new NameKey("",true);
                int physical = Need(force.PhysicalSlot,"Force_receiver");
                int ruler = Need(Current.ReadForceRecord(physical)?.RawRuler10,"Force_ruler10");
                //349680 calls231080 even if that ruler Person is invalid.
                return new NameKey(PersonSortText(Person(ruler)));
            });
            try { return cell with { Display = cell.Display with { MarkerColour = DisplayForce(city)?.Colour } }; }
            catch (InvalidDataException error)
            { return cell with { Display = cell.Display with { UnresolvedOwner = cell.Display.UnresolvedOwner ?? error.Message } }; }
        }
        private ForceDisplay? DisplayForce(City city)
        {
            if (forceDisplays.TryGetValue(city.Id,out var saved)) return saved;
            var force = Force(city.Receiver);
            if (!Need(Current.ReadForceNativeValidity(force),"Force_display_validity")) return forceDisplays[city.Id] = null;
            int physical = Need(force.PhysicalSlot,"Force_receiver");
            string body;
            if (physical is >= 46 and <= 50)
            {
                var reference = Current.ReadCityTableReference(physical+1) ?? throw Missing("Force_special_City_lookup");
                int at = Need(reference.PhysicalSlot,"Force_special_City_receiver");
                body = state.Cities.GetValueOrDefault(at)?.Name ?? throw Missing("Force_special_City_current_name");
            }
            else
            {
                int raw = Need(Current.ReadForceRecord(physical)?.RawRuler10,"Force_ruler10");
                int person = Person(raw);
                if (!Need(Current.ReadPersonNativeValidity(person),"Force_ruler_validity")) return forceDisplays[city.Id] = null;
                body = PersonText(person);
            }
            byte colour = Need(Current.ReadForceReportColor(force),"Force49_colour");
            return forceDisplays[city.Id] = new(Data(1,0x571)+body,MapSpritePalettes.ForceFill(colour));
        }
        private sealed record ForceDisplay(string Text,AreaMarkerColor Colour);

        private DistrictReference District(City city)
        {
            int raw = Need(Current.ReadBaseDistrictSlot(city.Receiver),"District30");
            return Current.ReadDistrictTableReference(raw < 52 ? raw : 0) ?? throw Missing("District_lookup");
        }
        private string DistrictText(City city)
        {
            var district = District(city);
            if (!Need(Current.ReadDistrictNativeValidity(district),"District_display_validity")) return "";
            int number = Need(Current.ReadDistrictNumber(Need(district.PhysicalSlot,"District_receiver")),"District11_number");
            return FormatSceneryPanelNumber(Data(1,0x494),number);
        }
        private NumberKey DistrictKey(City city)
        {
            if (Need(Current.ReadBaseDistrictSlot(city.Receiver),"District30") == 0) return new(0,true);
            int physical = Need(District(city).PhysicalSlot,"District_receiver");
            return new(Need(Current.ReadDistrictNumber(physical),"District11_number"));
        }
        private bool SpecialHome(City city)
        {
            int link = Need(Current.ReadBaseObjectLink(city.Receiver),"City4E");
            if (link > 51) return false;
            int force = Need(Force(city.Receiver).PhysicalSlot,"Force_receiver");
            if (force is < 46 or > 50) return false;
            int selected = Need(Current.ReadCityTableReference(link)?.PhysicalSlot,"special_City_receiver");
            return selected == force+1;
        }
        private string SizeText(City city)
        {
            if (Need(Current.ReadBaseObjectLink(city.Receiver),"City4E") > 51) return "";
            if (SpecialHome(city)) return Data(1,0x6DF);
            int size = Need(Current.ReadCitySizeB8(city.Receiver),"CityB8_size");
            int[] messages = [0x46C,0x46C,0x46D,0x46E,0x46F];
            return size < messages.Length ? Data(1,messages[size]) : throw Missing("CityB8_size_range");
        }
        private PreparedCell PeopleCell(City city,int mode) => MakeCell(
            () => Special(city) ? Data(1,0x353) : Number(People(city,mode)),
            () => Special(city) ? new NumberKey(0,true) : new NumberKey(People(city,mode)));
        private int People(City city,int mode)
        {
            if (personCounts is null)
            {
                var counts = new Dictionary<(int,int),int>();
                foreach (int? pointer in Current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100_registry"))
                {
                    int physical = Need(pointer,"Person100_pointer");
                    int home = Need(Current.ReadPersonFirstLocation(physical),"Person_E2");
                    if (home is < 1 or > 61) continue;
                    int status = Need(Current.ReadPersonStatus(physical),"Person_E6");
                    int category = status is >= 1 and <= 4 ? 0 : status == 6 ? 1 : status == 5 ? 2 : -1;
                    if (category < 0) continue;
                    counts[(home,category)] = counts.GetValueOrDefault((home,category))+1;
                }
                personCounts = counts;
            }
            int link = Need(Current.ReadBaseObjectLink(city.Receiver),"City4E");
            return personCounts.GetValueOrDefault((link,mode));
        }
        private bool EmperorHome(City city)
        {
            int person = Person(Need(Current.ReadEmperorId(),"World1654_Emperor"));
            int home = Need(Current.ReadPersonFirstLocation(person),"Emperor_E2");
            return home is >= 1 and <= 51 && home == city.PhysicalSlot;
        }
        private int? Tactic(City city)
        {
            byte raw = Need((Current.ReadCaptureBaseThreatFields(city.Receiver) ?? throw Missing("Base_threat_owner")).Byte48,"Base48_Tactic");
            var tactic = Current.ReadSkillTacticReference(raw <= 200 ? raw : 0) ?? throw Missing("Tactic_lookup");
            return Need(Current.ReadSkillTacticValidity(tactic),"Tactic_validity") ? Need(tactic.PhysicalSlot,"Tactic_receiver") : null;
        }
        private string AbnormalText(City city,int column)
        { int days = AbnormalDays(city,column); return days > 0 ? Number(days) : ""; }
        private int AbnormalKey(City city,int column)
        { int days = AbnormalDays(city,column); return days > 0 ? days : -1; }
        private int AbnormalDays(City city,int column)
        {
            var fields = Current.ReadCaptureBaseThreatFields(city.Receiver) ?? throw Missing("Base_threat_owner");
            int raw = Need(column == 16 ? fields.Byte4B : fields.Byte49,"Base_abnormal_counter");
            return (raw+3)/4;
        }
        private static bool Nearby(City city) => Need(city.Owner.HasNearbyEnemyArmy,"City67_nearby_enemy");
    }
}
