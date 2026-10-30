namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    private OriginalAiStrategyContextBank? contextBank;

    internal void UseContextBank(OriginalAiStrategyContextBank bank)
    {
        if (contextBank is not null && !ReferenceEquals(contextBank, bank))
            throw new InvalidOperationException("An operation cannot replace its policy context bank.");
        contextBank = bank;
    }

    internal OriginalAiNodeStatus TryBorrowDistrictListAlias(int district, uint key, uint output)
    {
        var bank = contextBank ?? throw new InvalidDataException("original_ai.district_list_alias.context_bank");
        if (Actor.AgentKind == OriginalAiAgentKind.District && Actor.DistrictId == district)
            return TryCopyListVariable(key, output) ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
        if (!bank.TryReadDistrictList(district, key, out var list)) return OriginalAiNodeStatus.Failure;
        integers.Remove(output); entities.Remove(output); ClearCandidateNull(output);
        candidateLists[output] = list;
        return OriginalAiNodeStatus.Success;
    }
}
