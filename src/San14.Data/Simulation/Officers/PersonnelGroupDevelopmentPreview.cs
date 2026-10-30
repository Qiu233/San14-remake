using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>341D30's four normal/full-effort pairs for the ordinary
    /// personnel-group page. This page has no Domestic selected-Area owner.
    /// One reader-local result supplies both display and normal-value sorting;
    /// it neither creates an Area draft nor publishes temporary World writes.</summary>
    internal IReadOnlyList<PersonnelGroupPersonCell> ReadPersonnelGroupDevelopmentPreviewCurrent(
        GameState state, SourceWorldContext current, int physical)
    {
        var preview = new AreaAdministrationPreviewSource(state, current, _scenario,
            Seed.Language, _baseCapacityInputs, true);
        var columns = new[] { 132, 133, 134, 139 };
        bool valid;
        try { valid = current.ReadPersonNativeValidity(physical) ?? throw PreviewMissing("Person_validity"); }
        catch (InvalidDataException error)
        { return columns.Select(column => Unknown(column, error.Message)).ToArray(); }
        if (!valid) return columns.Select(column => Cell(column,
            AreaAdministrationQuery<int?>.Known(0), AreaAdministrationQuery<int?>.Known(0))).ToArray();

        int Count(int person) => PersonAssignmentPrecallInputs.ReadCurrentAreaAssignmentActiveCount(state, person)
            ?? throw PreviewMissing("Person_task_count1E8");
        var result = new PersonnelGroupPersonCell[4];
        for (int index = 0; index < 3; index++)
        {
            int focus = index + 1;
            //3424A0 substitutes the actual World AreaTable0 pointer when
            //DAT14277F3E8 is null. Its saved three-word owner is independent
            //of playable Areas; the query never creates a synthetic Area.
            var area = current.ReadAreaTableReference(0);
            if (area?.PhysicalSlot is not int physicalArea)
            { result[index] = Unknown(columns[index], PreviewMissing("Area0_pointer").Message); continue; }
            //Use the real candidate's City trait2D/policy1D, current effective
            //ability, doctrine, away state, neighbours and Area representative
            //Force. Normal and full differ only in the source task multiplier.
            result[index] = Cell(columns[index],
                preview.AutoDevelopment(physicalArea, physical, focus, Count),
                preview.AutoDevelopment(physicalArea, physical, focus, Count, fullEffort: true));
        }
        //3450F0 sorts the actual normal266390(person,null). In this no-Area
        //context3558D0 also computes the full difference directly, without
        //city-center bonus or a Domestic candidate reassignment.
        result[3] = Cell(139, preview.AutoOccupation(physical, Count),
            preview.AutoOccupation(physical, Count, fullEffort: true));
        return result;

        PersonnelGroupPersonCell Cell(int column, AreaAdministrationQuery<int?> normal,
            AreaAdministrationQuery<int?> full)
        {
            if (normal.Value is not int number)
                return Unknown(column, normal.UnresolvedInput ?? PreviewMissing("normal_value").Message);
            if (full.Value is not int baseline)
                return new(column, null, number, number, null, false,
                    full.UnresolvedInput ?? PreviewMissing("full_effort_value").Message) { SortGroup = 0 };
            string sourceText = number == baseline
                ? ReadHistoryDataText(1, 0x38c).Replace("%d", number.ToString(CultureInfo.InvariantCulture))
                //309990(7,0) emits ^07~1~;309990(0,0) emits ^00~0~.
                //Keep the actual source's explicit minus in this no-Area
                //occupation getter, even when its difference is zero/negative.
                : column == 139
                    ? "+" + number.ToString(CultureInfo.InvariantCulture) + "(^07~1~-" +
                        unchecked(baseline - number).ToString(CultureInfo.InvariantCulture) + "^00~0~)"
                    : "+" + number.ToString(CultureInfo.InvariantCulture) + "(^07~1~" +
                        unchecked(number - baseline).ToString(CultureInfo.InvariantCulture) + "^00~0~)";
            // Keep the visible numeric text for data consumers and the exact
            // controls for the source row's body-colour run consumer.
            bool styled = number != baseline;
            string text = styled ? sourceText.Replace("^07~1~", "", StringComparison.Ordinal)
                .Replace("^00~0~", "", StringComparison.Ordinal) : sourceText;
            return new(column, text, number, number, null, false, null)
            {
                SortGroup = 0,
                SourceFormattedText = sourceText
            };
        }
        static PersonnelGroupPersonCell Unknown(int column, string missing) =>
            new(column, null, null, null, null, false, missing) { SortUnresolvedOwner = missing };
    }

    private static InvalidDataException PreviewMissing(string field) =>
        new("personnel.groups.preview_341D30." + field);
}
