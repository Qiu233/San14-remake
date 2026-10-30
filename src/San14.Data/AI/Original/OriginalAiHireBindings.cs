namespace San14.Data.AI.Original;

/// <summary>Hire execution and its actual independent-template inputs.</summary>
public static class OriginalAiHireBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        var a = node.Arguments;
        switch (node.TypeId)
        {
            case 0xF758192B:
                if (a.Length != 2) { unresolved = "Hire Foothold/Person-list parameter shape"; return false; }
                execute = operation => operation.ExecuteHireTemplates(a[0], a[1]); return true;
            case 0xF08D3D6B:
                if (a.Length != 2) { unresolved = "OfficerCandidate Foothold/output-list parameter shape"; return false; }
                //This is the ordinary6D8600 City/Gates Person producer, already
                //closed with the normal appointment selector. It is not offers.
                execute = operation => operation.FootholdPeople(a[0], a[1], officerCandidates: true); return true;
            case 0x9CDB6038 when a.Length == 2 && a[0] == 38:
                execute = operation => operation.MyselfHireOffers(a[1]); return true;
            case 0xEBBF8C69 when a.Length == 3 && a[1] == 38:
                execute = operation => operation.VariableHireOffers(a[0], a[2]); return true;
            case 0x0BB9F92D when a.Length == 3 && a[1] == 27:
                execute = operation => operation.PersonAbilitySum(a[0], a[2]); return true;
            default: return false;
        }
    }
}
