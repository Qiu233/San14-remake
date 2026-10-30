using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class GateInformationCurrent
    {
        private readonly Dictionary<int, GateForceDisplay?> forceDisplays = [];
        private Dictionary<(int Home, int Mode), int>? personCounts;
        private List<(int Home, int Person)>? freePeople;
        private readonly Dictionary<int, int> freeGateCounts = [];

        private PreparedCell BasicCell(Gate gate, int column) => column switch
        {
            0 => MakeCell(() => gate.Owner.Name, () => new NameKey(NameSort(gate))),
            1 => ForceCell(gate),
            2 => MakeCell(() => DistrictText(gate), () => DistrictKey(gate)),
            3 => MakeCell(() => Need(Current.ReadPersonNativeValidity(Governor(gate)), "Governor_validity")
                    ? PersonText(Governor(gate)) : "", () => PersonKey(Governor(gate))),
            4 => MakeCell(() => Number(LinkedCityResource(gate, BaseResourceKind.Gold)),
                () => new NumberKey(LinkedCityResource(gate, BaseResourceKind.Gold))),
            5 => MakeCell(() => Number(LinkedCityResource(gate, BaseResourceKind.Supplies)),
                () => new NumberKey(LinkedCityResource(gate, BaseResourceKind.Supplies))),
            6 => MakeCell(() => Number(Need(Current.ReadBaseResource(gate.Receiver, BaseResourceKind.Troops), "Gate3C_Troops")),
                () => new NumberKey(Need(Current.ReadBaseResource(gate.Receiver, BaseResourceKind.Troops), "Gate3C_Troops"))),
            7 => MakeCell(() => Number(People(gate, 0)), () => new NumberKey(People(gate, 0))),
            8 => MakeCell(() => Number(FreeCount(gate)), () => new NumberKey(FreeCount(gate))),
            9 => MakeCell(() => Number(People(gate, 1)), () => new NumberKey(People(gate, 1))),
            10 => MakeCell(() => Number(Need(Current.ReadArmyReturnBaseWounded40(gate.Receiver), "Gate40_Wounded")),
                () => new NumberKey(Need(Current.ReadArmyReturnBaseWounded40(gate.Receiver), "Gate40_Wounded"))),
            11 => MakeCell(() => Number(Need(Current.ReadBaseMorale44(gate.Receiver), "Gate44_Morale")),
                () => new NumberKey(Need(Current.ReadBaseMorale44(gate.Receiver), "Gate44_Morale"))),
            12 => MakeCell(() => Number(BasePostLoad.ReadLinkedField14(gate.Receiver, Current)),
                () => new NumberKey(BasePostLoad.ReadLinkedField14(gate.Receiver, Current))),
            13 => MakeCell(() => Tactic(gate) is int id ? state.Tactics.GetValueOrDefault(id)?.Name
                    ?? throw Missing("Tactic_current_name") : "",
                () => Tactic(gate) is int id ? new NameKey(rules._skills.Formations.Tactics[id].Pinyin) : new NameKey("", true)),
            14 or 15 => MakeCell(() => AbnormalDays(gate, column) is int days && days > 0 ? Number(days) : "",
                () => new NumberKey(AbnormalDays(gate, column))),
            16 => MakeCell(() => Data(1, Nearby(gate) ? 0x338 : 0x339), () => new NumberKey(Nearby(gate) ? 1 : 0)),
            _ => throw Missing("column." + column)
        };

        private string NameSort(Gate gate)
        {
            var rows = rules._scenario.Snapshot.Gates;
            if ((uint)gate.PhysicalSlot >= (uint)rows.Count || rows[gate.PhysicalSlot].Slot != gate.PhysicalSlot)
                throw Missing("Gate_name_sort_definition");
            return rows[gate.PhysicalSlot].SecondaryText ?? throw Missing("Gate_name_sort1A");
        }
        private int LinkedCityResource(Gate gate, BaseResourceKind kind)
        {
            //Gate v90/vA0 (22EB10/22C8D0) delegate to22A940's actual
            //current City. Gate34/38 are not the values of these getters.
            var city = CurrentGateMapQueries.FindCity(gate.Receiver, Current);
            return Need(Current.ReadBaseResource(city, kind), "linked_City_resource." + kind);
        }
        private int Governor(Gate gate) => Person(Need(Current.ReadArmyReturnBaseGovernor2E(gate.Receiver), "Governor2E"));
        private int Person(int raw) => Need((raw is >= 0 and <= 6000 ? Current.ReadPersonLookupReference(raw)
            : Current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        private string PersonText(int physical)
        {
            int id = Need(Current.ReadPersonIdentity(physical), "Person_identity");
            if (state.Officers.TryGetValue(id, out var owner))
                return (owner.CurrentSurname ?? throw Missing("Person_surname12")) +
                    (owner.CurrentGivenName ?? throw Missing("Person_given20"));
            if (physical != 0) throw Missing("Person_name_owner");
            var rows = rules._scenario.Snapshot.Characters;
            if (rows.Count == 0 || rows[0].Slot != 0) throw Missing("Person0_definition");
            return (rows[0].FamilyName ?? throw Missing("Person0_surname12")) + (rows[0].GivenName ?? throw Missing("Person0_given20"));
        }
        private string PersonSort(int physical, bool surnameOnly = false)
        {
            int id = Need(Current.ReadPersonIdentity(physical), "sort_Person_identity");
            if (state.Officers.TryGetValue(id, out var owner))
                return (surnameOnly ? owner.CurrentSurnameSortKey : owner.CurrentNameSortKey) ?? throw Missing("Person_sort34_5A");
            if (physical != 0) throw Missing("Person_sort_owner");
            var rows = rules._scenario.Snapshot.Characters;
            if (rows.Count == 0 || rows[0].Slot != 0) throw Missing("Person0_definition");
            return (surnameOnly ? rows[0].SurnameSortKey : rows[0].NameSortKey) ?? throw Missing("Person0_sort34_5A");
        }
        private NameKey PersonKey(int person) => Need(Current.ReadPersonNativeValidity(person), "Governor_sort_validity")
            ? new(PersonSort(person)) : new("", true);

        private PreparedCell ForceCell(Gate gate)
        {
            var cell = MakeCell(() => DisplayForce(gate)?.Text ?? "", () =>
            {
                var force = Force(gate.Receiver);
                if (!Need(Current.ReadForceNativeValidity(force), "Force_sort_validity")) return new NameKey("", true);
                int physical = Need(force.PhysicalSlot, "Force_sort_receiver");
                int person = Person(physical == 0 ? 5014 : Need(Current.ReadForceRecord(physical)?.RawRuler10, "Force_ruler10"));
                //3497D0 calls230FB0, not the City's349680 direct231080.
                if (!Need(Current.ReadPersonNativeValidity(person), "Force_sort_ruler_validity")) return new NameKey("");
                int id = Need(Current.ReadPersonIdentity(person), "Force_sort_ruler_identity");
                bool surname = physical is >= 46 and <= 50 && id is not (>= 5101 and <= 5200 or >= 2062 and <= 2066);
                return new NameKey(PersonSort(person, surname));
            });
            try { return cell with { Display = cell.Display with { MarkerColour = DisplayForce(gate)?.Colour } }; }
            catch (InvalidDataException error)
            { return cell with { Display = cell.Display with { UnresolvedOwner = cell.Display.UnresolvedOwner ?? error.Message } }; }
        }
        private GateForceDisplay? DisplayForce(Gate gate)
        {
            if (forceDisplays.TryGetValue(gate.Id, out var saved)) return saved;
            var force = Force(gate.Receiver);
            if (!Need(Current.ReadForceNativeValidity(force), "Force_display_validity")) return forceDisplays[gate.Id] = null;
            int physical = Need(force.PhysicalSlot, "Force_receiver");
            string body;
            if (physical is >= 46 and <= 50)
            {
                var city = Current.ReadCityTableReference(physical + 1) ?? throw Missing("Force_special_City");
                body = state.Cities.GetValueOrDefault(Need(city.PhysicalSlot, "Force_special_City_receiver"))?.Name
                    ?? throw Missing("Force_special_City_name");
            }
            else
            {
                int person = Person(Need(Current.ReadForceRecord(physical)?.RawRuler10, "Force_ruler10"));
                if (!Need(Current.ReadPersonNativeValidity(person), "Force_ruler_validity")) return forceDisplays[gate.Id] = null;
                body = PersonText(person);
            }
            byte colour = Need(Current.ReadForceReportColor(force), "Force49_colour");
            return forceDisplays[gate.Id] = new(Data(1, 0x571) + body, MapSpritePalettes.ForceFill(colour));
        }
        private sealed record GateForceDisplay(string Text, AreaMarkerColor Colour);

        private DistrictReference District(Gate gate)
        {
            int raw = Need(Current.ReadBaseDistrictSlot(gate.Receiver), "Gate_virtual_District");
            return Current.ReadDistrictTableReference(raw < 52 ? raw : 0) ?? throw Missing("District_lookup");
        }
        private string DistrictText(Gate gate)
        {
            var district = District(gate);
            if (!Need(Current.ReadDistrictNativeValidity(district), "District_display_validity")) return "";
            int number = Need(Current.ReadDistrictNumber(Need(district.PhysicalSlot, "District_receiver")), "District_number11");
            return FormatSceneryPanelNumber(Data(1, 0x494), number);
        }
        private NumberKey DistrictKey(Gate gate) => Need(Current.ReadBaseDistrictSlot(gate.Receiver), "Gate_virtual_District") == 0
            ? new(0, true) : new(Need(Current.ReadDistrictNumber(Need(District(gate).PhysicalSlot, "District_receiver")), "District_number11"));

        private void PreparePersonCounts()
        {
            if (personCounts is not null) return;
            var counts = new Dictionary<(int, int), int>(); var free = new List<(int, int)>();
            foreach (int? pointer in Current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100_registry"))
            {
                int person = Need(pointer, "Person100_pointer");
                int home = Need(Current.ReadPersonFirstLocation(person), "Person_E2");
                if (home is < 1 or > 61) continue;
                int status = Need(Current.ReadPersonStatus(person), "Person_E6");
                int mode = status is >= 1 and <= 4 ? 0 : status == 6 ? 1 : status == 5 ? 2 : -1;
                if (mode < 0) continue;
                counts[(home, mode)] = counts.GetValueOrDefault((home, mode)) + 1;
                if (mode == 2) free.Add((home, person));
            }
            personCounts = counts; freePeople = free;
        }
        private int People(Gate gate, int mode)
        {
            PreparePersonCounts();
            int link = Need(Current.ReadBaseObjectLink(gate.Receiver), "Gate4E");
            return personCounts!.GetValueOrDefault((link, mode));
        }
        private int FreeCount(Gate gate)
        {
            if (freeGateCounts.TryGetValue(gate.Id, out int count)) return count;
            PreparePersonCounts();
            var city = CurrentGateMapQueries.FindCity(gate.Receiver, Current);
            int home = Need(Current.ReadBaseObjectLink(city), "free_City4E");
            count = 0;
            foreach (var person in freePeople!)
            {
                if (person.Home != home) continue;
                var at = PersonPositionRepair.DecodeSecondLocation(Need(Current.ReadPersonSecondLocation(person.Person), "free_PersonE4"));
                if (at.Kind == 6 && at.Index == gate.PhysicalSlot) count++;
                count++; //The original349B00 has both increments.
            }
            freeGateCounts[gate.Id] = count; return count;
        }
        private int? Tactic(Gate gate)
        {
            byte raw = Need((Current.ReadCaptureBaseThreatFields(gate.Receiver) ?? throw Missing("Gate_threat_owner")).Byte48, "Gate48_Tactic");
            var tactic = Current.ReadSkillTacticReference(raw <= 200 ? raw : 0) ?? throw Missing("Tactic_lookup");
            return Need(Current.ReadSkillTacticValidity(tactic), "Tactic_validity") ? Need(tactic.PhysicalSlot, "Tactic_receiver") : null;
        }
        private int AbnormalDays(Gate gate, int column)
        {
            var fields = Current.ReadCaptureBaseThreatFields(gate.Receiver) ?? throw Missing("Gate_threat_owner");
            int raw = Need(column == 14 ? fields.Byte4B : fields.Byte49, "Gate_abnormal_counter");
            return (raw + 3) / 4; //Gate sort zero is0, unlike the City's -1.
        }
        private static bool Nearby(Gate gate) => Need(gate.Owner.HasNearbyEnemyArmy, "Gate67_nearby_enemy");
    }
}
