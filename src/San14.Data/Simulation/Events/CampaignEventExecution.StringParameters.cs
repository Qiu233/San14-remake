using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignEventMessageStringParameters(int OperationIndex,int SourceOffset,
    IReadOnlyList<string> Names):CampaignEventCommand(OperationIndex,SourceOffset,248);

public static partial class CampaignEventExecution
{
    private sealed partial class StagingRuntime
    {
        private int StageMessageStringParameters(S14ConditionInstruction operation)
        {
            var resolve=storyInputs?.ResolveMessageStringParameters
                ??throw new InvalidDataException("story.string_parameters.actual_formatter_owner");
            var bank=new CampaignStoryMessageContext(Parameters?.ToArray()??new int?[5],Names?.ToArray(),cityParameters?.ToArray())
            {Forces=forceParameters?.ToArray(),Areas=areaParameters?.ToArray(),Items=itemParameters?.ToArray(),Numbers=numberParameters?.ToArray(),Provinces=provinceParameters?.ToArray()};
            var values=resolve(bank,operation.RawArguments.Take(5).ToArray());
            if(values.Count!=5||values.Any(value=>value is null))
                throw new InvalidDataException("story.string_parameters.formatter_result");
            Names=Array.AsReadOnly(values.ToArray());
            Commands.Add(new CampaignEventMessageStringParameters(operation.Index,operation.Offset,Names));
            //The original handler writes its bank but returns false.
            return 0;
        }
    }
}
