using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>Typed bindings for the source city recruitment and training branch.</summary>
public static class OriginalAiCityDutyBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        var a = node.Arguments;
        execute = null; unresolved = null;
        switch (node.TypeId)
        {
            // Both remove nodes serialize two keys, but their update only reads
            // the first. The second key is not a required runtime input.
            case 0xD10625E5 when a.Length == 2:
                execute = current => current.CityDuty(a[0], null, CityDutyKind.Recruitment); break;
            case 0x16BF303F when a.Length == 2:
                execute = current => current.CityDuty(a[0], a[1], CityDutyKind.Recruitment); break;
            case 0xE5CB8893 when a.Length == 2:
                execute = current => current.CityDuty(a[0], null, CityDutyKind.Training); break;
            case 0x5733C979 when a.Length == 2:
                execute = current => current.CityDuty(a[0], a[1], CityDutyKind.Training); break;
            case 0x513BAD95 when a.Length == 3 && a[1] is 20 or 21 or 22 or 34 or 36:
                execute = current => current.CityDutyInformation(a[0], (int)a[1], a[2], false); break;
            // FootholdInfo38/39 are saved planning facts, bound by the
            // strategy-memory family after the original native-validity gate.
            case 0x0BB9F92D when a.Length == 3 && a[1] is 7 or 8:
                execute = current => current.CityDutyInformation(a[0], (int)a[1], a[2], true); break;
            case 0x7B9D3E14 when a.Length == 2 && a[0] == 5:
                execute = current => current.AncestorMyselfDistrictInformation(5, a[1]); break;
            default:
                unresolved = "city duty native type or serialized parameter shape";
                return false;
        }
        return true;
    }

    public static bool TryBindCondition(OriginalAiConditionQuery query,
        out Func<OriginalAiOperation, bool>? predicate, out string? unresolved)
    {
        var r = query.Record;
        if (r.QueryId != 108 || r.Parameters.Length != 2)
        {
            predicate = null; unresolved = "city duty condition 108 or serialized parameter shape";
            return false;
        }
        predicate = current => current.CityDutyCondition108(r.Parameters);
        unresolved = null;
        return true;
    }
}
