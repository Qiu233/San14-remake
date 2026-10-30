using San14.Core;
using ListKind = San14.Data.AI.Original.OriginalAiOperation.CandidateListKind;

namespace San14.Data.AI.Original;

/// <summary>Typed bindings for ordinary entity and hex lists. The serialized
/// list discriminator and the loop resolver are separate source enums.</summary>
public static class OriginalAiListBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        var a = node.Arguments;
        bool isFamily = node.TypeId is 0x78F9C619 or 0x543EFF68 or 0xECF09FB7 or 0x73B8B0C8 or 0x32A550F8 ||
            TryAddKind(node.TypeId, out _, out _) || TryRemoveKind(node.TypeId, out _) || TryEvaluationKind(node.TypeId, out _);
        if (!isFamily) return false;
        if (node.TypeId == 0x78F9C619)
        {
            if (a.Length != 4 || a[0] > 2)
            { unresolved = "list loop source mode or four-word parameter shape"; return false; }
            // The loop resolver enum is independent of the list factory enum.
            // Native11/12 always consume the retained object list, regardless
            // of the mode word; no global roster is synthesized for them.
            if (a[1] == 15)
            { execute = operation => operation.LoopGemDefinitions((int)a[0], a[2], a[3], children); return true; }
            if (a[1] is 11 or 12)
            { execute = operation => operation.LoopObjectCandidates(a[2], a[3], children); return true; }
            if (a[0] == 2 && TryLoopKind(a[1], out var loopKind))
            { execute = operation => operation.LoopTypedCandidates(a[2], a[3], loopKind, children); return true; }
            if ((a[0] == 0 && a[1] is 1 or 3 or 6 or 7 or 10) ||
                (a[0] == 1 && a[1] == 3))
            { execute = operation => operation.LoopSourceCandidates((int)a[0], (int)a[1], a[3], children); return true; }
            unresolved = "list loop source resolver/actor roster is not bound: mode=" + a[0] + ", resolver=" + a[1];
            return false;
        }
        if (children.Count != 0)
        { unresolved = "ordinary list node has no native child owner"; return false; }
        if (node.TypeId == 0x543EFF68 && a.Length == 2 && TryKind(a[0], out var clearKind))
            execute = operation => operation.ClearTypedCandidates(a[1], clearKind);
        else if (node.TypeId == 0xECF09FB7 && a.Length == 3 && TryKind(a[0], out var lengthKind))
            execute = operation => operation.TypedCandidateLength(a[1], a[2], lengthKind);
        else if (node.TypeId == 0x73B8B0C8 && a.Length == 4 && TryKind(a[0], out var indexKind))
            execute = operation => operation.TypedCandidateByIndex(a[1], a[2], a[3], indexKind);
        else if (node.TypeId == 0x32A550F8 && a.Length == 3 && TryKind(a[0], out var sortKind))
        {
            if (sortKind is ListKind.Caution or ListKind.Troops)
            { unresolved = "list sort source evaluation/comparator unsupported for " + sortKind; return false; }
            execute = operation => operation.SortTypedCandidates(a[1], sortKind, a[2] == 0);
        }
        else if (a.Length == 2 && TryAddKind(node.TypeId, out var addKind, out bool unique))
            execute = operation => operation.AddTypedCandidate(a[0], a[1], addKind, unique);
        else if (a.Length == 2 && TryRemoveKind(node.TypeId, out var removeKind))
            execute = operation => operation.RemoveTypedCandidate(a[0], a[1], removeKind);
        else if (a.Length == 2 && TryEvaluationKind(node.TypeId, out var evaluationKind))
            execute = operation => operation.SetTypedCandidateEvaluation(a[0], a[1], evaluationKind);
        else
        { unresolved = "ordinary list native type, element kind or serialized parameter shape"; return false; }
        return true;
    }

    public static bool TryBindCondition(OriginalAiConditionQuery query,
        out Func<OriginalAiOperation, bool>? predicate, out string? unresolved)
    {
        var r = query.Record;
        predicate = null; unresolved = null;
        if (r.QueryId != 24) return false;
        if (r.QueryId == 24 && r.Parameters.Length == 3 &&
            r.Parameters[0] is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 12 or 13 or 15 &&
            TryKind(r.Parameters[0], out var kind))
        { predicate = operation => operation.TypedCandidateContains(r.Parameters[1], r.Parameters[2], kind); return true; }
        unresolved = "ordinary typed-list membership condition or parameter shape";
        return false;
    }

    private static bool TryKind(uint raw, out ListKind kind)
    {
        kind = raw switch
        {
            0 => ListKind.Force, 1 => ListKind.District, 2 => ListKind.Troops, 3 => ListKind.Army,
            4 => ListKind.Officer, 5 => ListKind.Foothold, 6 => ListKind.Area, 7 => ListKind.Hex,
            8 or 9 => ListKind.Foothold, 10 => ListKind.MapObject, 11 => ListKind.CautionGroup,
            12 => ListKind.Formation, 13 => ListKind.Tactic, 15 => ListKind.Caution, 16 => ListKind.Facility, 17 => ListKind.Gem, 18 => ListKind.Landmark,
            _ => default
        };
        return raw is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 15 or 16 or 17 or 18;
    }

    private static bool TryLoopKind(uint raw, out ListKind kind)
    {
        kind = raw switch
        {
            1 => ListKind.Officer, 2 => ListKind.Army, 3 => ListKind.Troops, 4 => ListKind.District,
            5 => ListKind.Force, 6 => ListKind.Hex, 7 => ListKind.Area,
            8 or 9 or 10 => ListKind.Foothold, 13 => ListKind.Formation, 14 => ListKind.Tactic,
            16 => ListKind.MapObject, 17 => ListKind.CautionGroup,
            _ => default
        };
        return raw is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 13 or 14 or 16 or 17;
    }

    private static bool TryAddKind(uint type, out ListKind kind, out bool unique)
    {
        unique = type is 0xF5C1D1E0 or 0x8E682F08 or 0x888447D8 or 0xE7B02D30 or 0xC75809C0 or 0x011813F8 or 0xB245A1B9;
        kind = type switch
        {
            0xCD0CCD48 or 0xF5C1D1E0 => ListKind.Force,
            0x84836D20 or 0x8E682F08 => ListKind.Army,
            0xEAD69E50 or 0x888447D8 => ListKind.Foothold,
            0xAD7B8BF8 or 0xE7B02D30 => ListKind.Hex,
            0xD8D3B168 or 0xC75809C0 => ListKind.Officer,
            0x26623630 or 0x011813F8 => ListKind.Area,
            0x1C8A2F8F or 0xB245A1B9 => ListKind.District,
            0xDC87DF58 => ListKind.Tactic,
            _ => default
        };
        return type is 0xCD0CCD48 or 0xF5C1D1E0 or 0x84836D20 or 0x8E682F08 or
            0xEAD69E50 or 0x888447D8 or 0xAD7B8BF8 or 0xE7B02D30 or 0xD8D3B168 or
            0xC75809C0 or 0x26623630 or 0x011813F8 or 0x1C8A2F8F or 0xB245A1B9 or 0xDC87DF58;
    }

    private static bool TryRemoveKind(uint type, out ListKind kind)
    {
        kind = type switch
        {
            0x6E83418B => ListKind.Force, 0x43F672B3 => ListKind.Army,
            0xEF1F7383 => ListKind.Foothold, 0xE603C4DB => ListKind.Hex,
            0xECB0696B => ListKind.Officer, 0x48BE4FA3 => ListKind.Area,
            0xBE92E5E4 => ListKind.District, _ => default
        };
        return type is 0x6E83418B or 0x43F672B3 or 0xEF1F7383 or 0xE603C4DB or
            0xECB0696B or 0x48BE4FA3 or 0xBE92E5E4;
    }

    private static bool TryEvaluationKind(uint type, out ListKind kind)
    {
        kind = type switch
        {
            0x1DA4240A => ListKind.Force, 0x9E22D65A => ListKind.Foothold,
            0x6F68FEAC => ListKind.Hex, 0x74E23C32 => ListKind.Officer,
            0x7928C3BA => ListKind.Area, 0x51F79E63 => ListKind.Formation, _ => default
        };
        return type is 0x1DA4240A or 0x9E22D65A or 0x6F68FEAC or 0x74E23C32 or 0x7928C3BA or 0x51F79E63;
    }
}
