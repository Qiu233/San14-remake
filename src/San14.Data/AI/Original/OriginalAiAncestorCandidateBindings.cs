using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>Source-backed producers and guards surrounding the domestic region.</summary>
public static class OriginalAiAncestorCandidateBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        var a = node.Arguments;
        execute = null; unresolved = null;
        switch (node.TypeId)
        {
            case 0x513BAD95 when a.Length == 3 && a[1] == 42:
                execute = operation => operation.FootholdPeople(a[0], a[2], officerCandidates: false); return true;
            case 0xF08D3D6B when a.Length == 2:
                execute = operation => operation.FootholdPeople(a[0], a[1], officerCandidates: true); return true;
            case 0xECF09FB7 when a.Length == 3 && a[0] is 4 or 6:
                var kind = a[0] == 4 ? WorldEntityKind.Officer : WorldEntityKind.Area;
                execute = operation => operation.CandidateListLength(a[1], a[2], kind); return true;
            default:
                unresolved = "ancestor candidate native type or parameter shape"; return false;
        }
    }

    public static bool TryBindCondition(OriginalAiConditionQuery query,
        out Func<OriginalAiOperation, bool>? predicate, out string? unresolved)
    {
        var r = query.Record;
        predicate = null; unresolved = null;
        switch (r.QueryId)
        {
            case 24 when r.Parameters.Length == 3 && r.Parameters[0] is 4 or 6:
                var kind = r.Parameters[0] == 4 ? WorldEntityKind.Officer : WorldEntityKind.Area;
                predicate = operation => operation.CandidateListContains(r.Parameters[1], r.Parameters[2], kind); return true;
            case 26 when r.Parameters.Length == 1:
                predicate = operation => operation.ActorDistrictPurpose(unchecked((int)r.Parameters[0])); return true;
            case 65 when r.Parameters.Length == 1:
                predicate = operation => operation.DistrictIsCurrentPlayer(r.Parameters[0]); return true;
            case 116 when r.Parameters.Length == 2:
                predicate = operation => operation.DistrictUpdateEligible(r.Parameters[0], unchecked((int)r.Parameters[1])); return true;
            default:
                unresolved = $"ancestor condition {r.QueryId} or parameter shape"; return false;
        }
    }
}
