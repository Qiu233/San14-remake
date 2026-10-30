using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    // SActSetMessageParamForce reads all five operands. Positive operands name
    // native people/rulers, not Force IDs. Selected actors use a different
    // selector numbering here than the ordinary person-message actions.
    public int ForceMessageParameter(int selector, Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var force = ResolveForce(selector, readTargetForce);
        return force is not null && Known(Queries.ReadConditionForceValidity(force), "force parameter validity")
            ? Known(Queries.ReadConditionForceIdentity(force), "force parameter identity") : 0;
    }

    public int AreaMessageParameter(int selector)
    {
        // The original negative raw index dereferences outside its area table;
        // reject that unsupported resource input at this format boundary.
        if (selector < 0) throw new InvalidDataException("Negative area message parameter has no safe source-table interpretation.");
        var area = Queries.ReadConditionAreaTableSlot(selector < 501 ? selector : 0)
            ?? throw Missing("area parameter pointer");
        return Known(Queries.ReadConditionAreaValidity(area), "area parameter validity")
            ? Known(Queries.ReadConditionAreaIdentity(area), "area parameter identity") : 0;
    }
}
