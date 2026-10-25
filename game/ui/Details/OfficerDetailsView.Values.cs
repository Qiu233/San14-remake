using San14.Data;

public partial class OfficerDetailsView
{
    private Dictionary<int, string> PageValues(int page)
    {
        var person = publication.Overview;
        var values = new Dictionary<int, string> { [15] = TextValue(person.Age), [16] = TextValue(person.Sex) };
        if (page < 2)
        {
            values[23] = TextValue(person.Force); values[27] = TextValue(person.District);
            values[30] = TextValue(person.ServiceStatus); values[33] = TextValue(person.Home);
            values[36] = TextValue(person.Location);
        }
        if (page == 0)
        {
            values[71] = TextValue(person.Office); values[76] = TextValue(person.CommandCapacity);
            values[81] = TextValue(person.Salary); values[86] = VisibleValue(person.Loyalty, person.LoyaltyVisible);
            values[91] = TextValue(person.ServiceYears); values[96] = TextValue(person.ItemCount);
            values[102] = VisibleValue(person.MeritRank, person.MeritVisible);
            values[107] = VisibleValue(person.Merit, person.MeritVisible);
            values[112] = TextValue(person.Task); values[117] = TextValue(person.TaskTarget);
            values[122] = TextValue(person.TaskDuration);
        }
        else if (page == 1)
        {
            var military = publication.Military;
            values[60] = TextValue(military.Doctrine); values[65] = TextValue(military.Policy);
            values[67] = TextValue(military.PolicyCost);
            if (military.PolicyCost is { Available: true, Value: int cost } &&
                military.PolicyCostBeforeDiscount is { Available: true, Value: int before } && cost < before)
                values[67] = Number(cost) + "(" + Number(unchecked(cost - before)) + ")";
        }
        else
        {
            var relations = publication.Relations;
            values[29] = RelatedName(relations.Father); values[36] = RelatedName(relations.Mother);
            values[44] = RelatedName(relations.Spouse);
            int[] brothers = [52, 59, 65], likes = [71, 72, 73, 74, 76, 77, 78, 79], dislikes = [85, 86, 87, 88, 90, 91, 92, 93];
            BindNames(brothers, relations.SwornBrothers);
            BindNames(likes, relations.Likes); BindNames(dislikes, relations.Dislikes);
            values[98] = TextValue(relations.Biography);
            void BindNames(int[] nodes, IReadOnlyList<OfficerDetailRelatedPerson> rows)
            { for (int index = 0; index < nodes.Length; index++) values[nodes[index]] = index < rows.Count ? RelatedName(rows[index]) : ""; }
        }
        return values;
    }

    private string VisibleValue<T>(AreaAdministrationQuery<T> value, AreaAdministrationQuery<bool?> visible)
        => !visible.Available ? currentText.Get("detail_unknown") : visible.Value == true ? TextValue(value) : "";
    private string RelatedName(OfficerDetailRelatedPerson person) => person.Name.Length != 0 ? person.Name :
        person.UnresolvedOwner is null ? "" : currentText.Get("detail_unknown");
    private static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static (int Node, int Message)[] PageCaptions(int page) => page switch
    {
        0 => [(14,0x42A),(32,0x219),(26,0x1FE),(22,0x1FD),(95,0x246),(85,0x21D),
            (111,0x221),(121,0x450),(70,0x21C),(29,0x212),(80,0x429),(90,0x21E),
            (75,0x3D2),(116,0x299),(35,0x21A),(101,0x822),(106,0x821),(38,0x89D)],
        1 => [(14,0x42A),(32,0x219),(26,0x1FE),(22,0x1FD),(29,0x212),(35,0x21A),
            (59,0x244),(64,0x241),(66,0x386),(71,0x234),(84,0x22B),(98,0x411)],
        2 => [(14,0x42A),(28,0x42C),(35,0x42D),(43,0x42E),(58,0x2BF),(69,0x2BC),(83,0x2BD),(97,0x297)],
        _ => throw new ArgumentOutOfRangeException(nameof(page))
    };
}
