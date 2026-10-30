using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>Load-time typed bindings for the closed domestic candidate family.
/// No handler interprets a resource graph or obtains candidates from a planner.</summary>
public static class OriginalAiDomesticBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        var a = node.Arguments;
        execute = null; unresolved = null;
        if (node.TypeId == 0x78F9C619 && a.Length == 4 && a[0] == 0 && a[1] == 8 && a[2] == 0)
        {
            execute = current => current.LoopActorCities(a[3], children);
            return true;
        }
        if (node.TypeId == 0x78F9C619)
        {
            if (a.Length != 4 || a[0] != 2 || a[1] is not (1 or 7))
            { unresolved = "loop source mode/list kind or serialized parameter shape"; return false; }
            WorldEntityKind kind = a[1] == 1 ? WorldEntityKind.Officer : WorldEntityKind.Area;
            execute = current => current.LoopCandidates(a[2], a[3], kind, children);
            return true;
        }
        if (children.Count != 0)
        { unresolved = "non-loop domestic native child ownership"; return false; }
        switch (node.TypeId)
        {
            case 0x543EFF68 when a.Length == 2 && TryKind(a[0], out var kind):
                execute = current => current.ClearCandidates(a[1], kind); break;
            case 0x26623630 when a.Length == 2:
                execute = current => current.AddCandidate(a[0], a[1], WorldEntityKind.Area, false); break;
            case 0xD8D3B168 when a.Length == 2:
                execute = current => current.AddCandidate(a[0], a[1], WorldEntityKind.Officer, false); break;
            case 0xC75809C0 when a.Length == 2:
                execute = current => current.AddCandidate(a[0], a[1], WorldEntityKind.Officer, true); break;
            case 0xECB0696B when a.Length == 2:
                execute = current => current.RemoveCandidate(a[0], a[1], WorldEntityKind.Officer); break;
            case 0x7928C3BA when a.Length == 2:
                execute = current => current.SetCandidateEvaluation(a[0], a[1], WorldEntityKind.Area); break;
            case 0x74E23C32 when a.Length == 2:
                execute = current => current.SetCandidateEvaluation(a[0], a[1], WorldEntityKind.Officer); break;
            case 0x32A550F8 when a.Length == 3 && TryKind(a[0], out var sortedKind):
                execute = current => current.SortCandidates(a[1], sortedKind, a[2] == 0); break;
            case 0x73B8B0C8 when a.Length == 4 && TryKind(a[0], out var indexedKind):
                execute = current => current.CandidateByIndex(a[1], a[2], a[3], indexedKind); break;
            case 0x513BAD95 when a.Length == 3 && a[1] == 29:
                //The source blackboard can hold either concrete foothold kind.
                execute = current => current.TryEntity(a[0], out var foothold) && foothold.Kind is WorldEntityKind.City or WorldEntityKind.Gate
                    ? current.DomesticInformation(a[0], 29, a[2], foothold.Kind) : OriginalAiNodeStatus.Failure; break;
            case 0x6AB07E75 when a.Length == 3 && a[1] is 7 or 14:
                execute = current => current.DomesticInformation(a[0], (int)a[1], a[2], WorldEntityKind.Area); break;
            case 0x0BB9F92D when a.Length == 3 && a[1] is 11 or 25 or 36 or 37 or 38:
                execute = current => current.DomesticInformation(a[0], (int)a[1], a[2], WorldEntityKind.Officer); break;
            default:
                unresolved = "domestic native type or serialized parameter shape"; return false;
        }
        return true;
    }

    public static bool TryBindCondition(OriginalAiConditionQuery query,
        out Func<OriginalAiOperation, bool>? predicate, out string? unresolved)
    {
        var r = query.Record;
        bool shape = r.QueryId switch
        {
            2 => r.Parameters.Length == 2 && r.Parameters[0] == 6,
            11 => r.Parameters.Length is 2 or 3,
            12 or 22 or 32 or 33 or 34 => r.Parameters.Length == 1,
            112 or 113 or 114 or 118 => r.Parameters.Length == 2,
            _ => false
        };
        if (!shape)
        {
            predicate = null; unresolved = $"domestic condition {r.QueryId} or serialized parameter shape";
            return false;
        }
        predicate = current => current.DomesticCondition(r.QueryId, r.Parameters);
        unresolved = null; return true;
    }

    private static bool TryKind(uint raw, out WorldEntityKind kind)
    {
        kind = raw == 4 ? WorldEntityKind.Officer : WorldEntityKind.Area;
        return raw is 4 or 6;
    }
}
