using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2783B0 with actual R8=0/R9=1, reached inside240DB0. Its producer
    //already selected and cleared the first real196/19C pair and applied the
    //current-player gate. This callback does not repeat or weaken those gates.
    internal GameState ApplyPersonAssociationRemovalReports(GameState state, SourceWorldContext current,
        int personPhysical, PersonAssignmentPrecallResult result, Func<int> readDay450,
        List<CampaignEffectRequest> effects)
    {
        InvalidDataException Missing(string field) => new("person-association-removal.report." + field);
        T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        var working = state;
        foreach (var report in result.Reports)
        {
            if (report.NativeMessageCode is >= 0x31FC and <= 0x31FF)
            {
                working = ApplyAssignmentRemovalReportCurrent(working, current, personPhysical, report, readDay450, effects);
                continue;
            }
            if (report.NativeMessageCode != 0x3452)
                throw Missing("message_" + report.NativeMessageCode.ToString("X4"));
            int target = Need(report.TargetForcePhysical, "target_Force_virtual60");
            //Actual30DC80/30D9E0 operands captured by2783B0. The normal
            //formatter evaluates target Force0.4 before Person0.3, in the
            //language resource's actual order; no domain-name substitution.
            var text = FormatHistoryReport(working, current, 0x3452,
                new(Person0: report.PersonId, Force0: target));
            working = text.State;
            //ASM278530..278548 reads E2 before World450 and32C7D0.
            ushort first = Need(current.ReadPersonFirstLocation(personPhysical), "PersonE2");
            int day = readDay450();
            var home = BaseLookup.Find(first, current);
            byte rawDistrict = Need(current.ReadPersonDistrictSlot(personPhysical), "PersonE0");
            var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
                ?? throw Missing("District_lookup");
            //No extra Base/District validity or current Force filtering: the
            //source directly calls22D640,22DEF0,22DD20 on these operands.
            var hex = current.ReadReportBaseHex(home) ?? throw Missing("Base_Hex22D640");
            int hexId = Need(current.ReadReportMapCellPhysicalSlot(hex), "Hex_ordinal22DEF0");
            int districtId = Need(current.ReadDistrictPhysicalSlot(district), "District_ordinal22DD20");
            //9354F0 category0,payload5012 and all optional mark inputs zero.
            //Keep the real default supplemental owner and conditional postwrite
            //HistoryNotification. The caller publishes its actual ordered batch.
            working = WriteHistory(working, current, new(0, districtId, hexId, day,
                text.Text, CampaignHistoryWriter.Supplemental(working), 5012), effects);
        }
        return working;
    }

    private GameState ApplyAssignmentRemovalReportCurrent(GameState state, SourceWorldContext current,
        int person, PersonAssignmentPrecallReport report, Func<int> readDay450, List<CampaignEffectRequest> effects)
    {
        int root = report.NativeMessageCode;
        ushort? baseOperand = null;
        if (root != 0x31FC)
        {
            var city = new BaseReference(BaseKind.City, TalentNeed(report.CityPhysical, "assignment_report_City"));
            baseOperand = TalentNeed(current.ReadBaseObjectLink(city), "assignment_report_City4E");
        }
        var text = FormatTalentReportCurrent(state, current, root, new(Person0: report.PersonId,
            Base0: baseOperand, Area0: root == 0x31FF ? TalentNeed(report.AreaPhysical, "assignment_report_Area") : null));
        //29C430 reads World450 before the current home and District lookups.
        int day = readDay450();
        var home = BaseLookup.Find(TalentNeed(current.ReadPersonFirstLocation(person), "assignment_report_E2"), current);
        byte raw = TalentNeed(current.ReadPersonDistrictSlot(person), "assignment_report_E0");
        var district = current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
            ?? throw TalentCommandMissing("assignment_report_District_lookup");
        var hex = current.ReadReportBaseHex(home) ?? throw TalentCommandMissing("assignment_report_Base_Hex");
        int hexId = TalentNeed(current.ReadReportMapCellPhysicalSlot(hex), "assignment_report_Hex_ordinal");
        int districtId = TalentNeed(current.ReadDistrictPhysicalSlot(district), "assignment_report_District_ordinal");
        return WriteHistory(text.State, current, new(0, districtId, hexId, day, text.Text,
            CampaignHistoryWriter.Supplemental(text.State), 5012, MarkBit4: root == 0x31FC ? 0 : 1), effects);
    }
}
