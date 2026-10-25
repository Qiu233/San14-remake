using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2955A0's nonzero-mode gold quote. It borrows the caller's
    /// current world and primary pool; it neither pays nor develops an Area.</summary>
    internal int ReadHireAreaAdministrationGoldExpenseCurrent(GameState state,
        SourceWorldContext current, BaseReference city)
    {
        int location = HireAdministrationK(current.ReadBaseObjectLink(city), "City4E");
        if (location > 51) return 0;
        int forceId = HireAdministrationK(current.ReadBaseForceSlot(city), "City_B0");
        if (forceId == 0) return 0;
        _ = state.NativeMapSeason?.ThirdDate ?? throw HireAdministrationMissing("World34_date");

        //CFootholdPersonIterator(mode0): Base4E1..61, PersonE2 and status1..4.
        //The resulting maxima are used only by quote0, but these current
        //queries precede the Area temporary even in the nonzero quote mode.
        if (location is >= 1 and <= 61)
        {
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots()
                ?? throw HireAdministrationMissing("Person_manager100"))
            {
                int person = entry ?? throw HireAdministrationMissing("Person_pointer");
                if (HireAdministrationK(current.ReadPersonFirstLocation(person), "PersonE2") != location ||
                    HireAdministrationK(current.ReadPersonStatus(person), "PersonE6") is not (>= 1 and <= 4)) continue;
                if (PersonCurrentBusySource.Read(current, person,
                    field => HireAdministrationMissing("resident_" + field))) continue;
                _ = HireAdministrationK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x2D), "resident_effect45");
                _ = HireAdministrationK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x4A), "resident_effect74");
            }
        }

        var temporary = current.CreateTemporaryHexList()
            ?? throw HireAdministrationMissing("Area_temporary_descriptor");
        var selected = new List<(AreaReference Area, HexListNodeReference Node)>();
        try
        {
            int cityPhysical = HireAdministrationK(city.PhysicalSlot, "City_physical");
            //223350 uses the registered Area chain and only Area35 membership;
            //City0 has no members. Do not substitute the UI selected roster.
            if (cityPhysical is >= 1 and <= 51)
                foreach (var entry in current.ReadRegisteredAreaReferences()
                    ?? throw HireAdministrationMissing("Area_managerB0"))
                {
                    var area = entry ?? throw HireAdministrationMissing("Area_pointer");
                    if (HireAdministrationK(current.ReadAreaCitySlot(area), "Area35") != cityPhysical) continue;
                    int cityForceId = HireAdministrationK(current.ReadBaseForceSlot(city), "City_B0_repeated");
                    if (HireAdministrationK(current.ReadAreaCurrentForceSlot(area), "Area_Force") != cityForceId) continue;
                    int physical = HireAdministrationK(area.PhysicalSlot, "Area_physical");
                    if (HireAdministrationK(current.ReadAssignmentArea36(physical), "Area36") == 0) continue;
                    byte focus = HireAdministrationK(current.ReadAssignmentArea72(physical), "Area72");
                    bool develop = focus is >= 1 and <= 3 && DevelopmentLevel(physical, focus) < 5;
                    int security = state.Areas.GetValueOrDefault(physical)?.Security
                        ?? throw HireAdministrationMissing("Area54_security");
                    if (security is < 0 or > 255) throw HireAdministrationMissing("Area54_byte");
                    if (security >= 100 && !develop) continue;
                    //Actual node exhaustion suppresses this candidate.
                    var node = current.AllocateHexListNode(temporary);
                    if (node.Identity is not int cursor) continue;
                    current.WriteCurrentListPayload(cursor, area);
                    selected.Add((area, node));
                }

            //24B550 is stable backward insertion: City-center Areas first,
            //then actual physical Area identity ascending. Reorder the nodes
            //as well as the values so destruction returns them in source order.
            for (int i = 1; i < selected.Count; i++)
            {
                var item = selected[i];
                int j = i;
                while (j > 0 && Before(item.Area, selected[j - 1].Area))
                { selected[j] = selected[j - 1]; j--; }
                selected[j] = item;
            }
            if (temporary.Identity.HasValue)
                current.ReorderCurrentList(temporary, selected.Select(row => row.Node).ToArray());

            var cityForce = Force(HireAdministrationK(current.ReadBaseForceSlot(city), "City_policy_Force"));
            _ = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, cityForce, 0x1D, true);
            _ = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current,
                Force(HireAdministrationK(current.ReadBaseForceSlot(city), "City_policy_Force_repeated")), 0x1E, true);
            int total = 0;
            foreach (var row in selected)
            {
                int physical = HireAdministrationK(row.Area.PhysicalSlot, "quoted_Area_physical");
                int person = PersonAreaAppointmentSource.Resolve(current,
                    HireAdministrationK(current.ReadAssignmentArea36(physical), "quoted_Area36"));
                //2661E0 obtains policy38 through Area3A -> Hex14, not Area35's
                //City Force. Its scratch is real even when the cost is unused.
                ushort rawHex = HireAdministrationK(current.ReadAreaHexIndex(row.Area), "quoted_Area3A");
                var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
                    ?? throw HireAdministrationMissing("quoted_Hex_pointer");
                int rawForce = HireAdministrationK(current.ReadHexForceSlot(hex), "quoted_Hex14");
                var coefficients = AreaAdministrationUpkeep.ReadCurrent(state);
                int policy = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, Force(rawForce), 0x26, true).Value;
                int personalities = HireAdministrationK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x27), "quoted_effect39");
                int fee = AreaAdministrationUpkeep.Calculate(coefficients, policy, personalities);
                if (HireAdministrationK(current.ReadBaseResource(city, BaseResourceKind.Gold), "City_virtual90_gold") < fee) break;
                //Quote1 never subtracts a cost from City gold. Each comparison
                //above therefore sees the same current stock, not a budget.
                total = unchecked(total + fee);
            }
            //The final Force170 comparison is unsigned against quote1's
            //unchanged zero level-sum, so no experience writer can be reached.
            _ = HireAdministrationK(current.ReadForceNativeValidity(
                Force(HireAdministrationK(current.ReadBaseForceSlot(city), "tail_City_Force"))), "tail_Force_validity");
            return total;
        }
        finally
        {
            current.ClearTemporaryHexList(temporary);
            current.ReleaseTemporaryHexList(temporary);
        }

        ForceReference Force(int raw) => current.ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw HireAdministrationMissing("Force_pointer");

        int DevelopmentLevel(int physical, byte focus)
        {
            var development = state.Areas.GetValueOrDefault(physical)?.Development
                ?? throw HireAdministrationMissing("Area_development");
            int? progress = focus switch { 1 => development.Commerce,
                2 => development.Agriculture, 3 => development.Barracks, _ => null };
            if (progress is not (>= 0 and <= 65535)) throw HireAdministrationMissing("Area_development_word");
            return CeAreaIncomeMath.Level((ushort)progress.Value);
        }

        bool Before(AreaReference first, AreaReference second)
        {
            if (IsOccupationCityArea(current, first) && !IsOccupationCityArea(current, second)) return true;
            if (!IsOccupationCityArea(current, first) && IsOccupationCityArea(current, second)) return false;
            return HireAdministrationK(first.PhysicalSlot, "sort_Area_identity") <
                HireAdministrationK(second.PhysicalSlot, "sort_Area_identity");
        }
    }

    private static T HireAdministrationK<T>(T? value, string field) where T : struct =>
        value ?? throw HireAdministrationMissing(field);
    private static InvalidDataException HireAdministrationMissing(string field) =>
        new("personnel-hire.economy.area-administration." + field);
}
