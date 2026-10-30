namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    //3B9BC0 compares this invocation's actual ptr_list<Person> size. Repeated
    //payload pointers each contribute one, and no Person validity is reread.
    internal bool CompareCandidateCount(IReadOnlyList<int> arguments)
    {
        if (arguments.Count != 8) throw Missing("person count condition operands");
        return CompareStoryForceValue(_candidates.Count, arguments[0], arguments[1]);
    }
}
