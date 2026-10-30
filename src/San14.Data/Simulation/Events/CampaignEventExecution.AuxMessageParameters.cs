using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignEventDoctrineNameParameters(int OperationIndex, int SourceOffset,
    IReadOnlyList<string> Names) : CampaignEventCommand(OperationIndex, SourceOffset, 435);

public static partial class CampaignEventExecution
{
    private sealed partial class StagingRuntime
    {
        private int StageAuxMessageParameters(S14ConditionInstruction operation)
        {
            if (operation.RawArguments.Count != 8)
                throw new InvalidDataException("story.aux-message-parameters.operands");
            var arguments = operation.RawArguments;
            if (operation.Code == 327)
            {
                for (int index = 0; index < 5; index++)
                {
                    //3A6280 uses the ordinary Person selector family. An
                    //invalid resolved receiver leaves its Number register
                    //unchanged; it does not write the age of Person0 or zero.
                    var person = ResolvePerson(arguments[index]);
                    bool valid = persons.ReadConditionPersonValidity(person)
                        ?? throw new InvalidDataException("story.person-age-parameters.Person_validity");
                    if (!valid) continue;
                    int age = storyInputs?.ReadCurrentPersonAge?.Invoke(person)
                        ?? throw new InvalidDataException("story.person-age-parameters.current_age");
                    numberParameters ??= new int?[5];
                    numberParameters[index] = age;
                }
                //The Number bank is captured by the actual cursor and each
                //subsequent message, as for the existing103/104 handlers.
                return 1;
            }
            if (operation.Code != 435)
                throw new InvalidDataException("story.aux-message-parameters.opcode");
            var readName = storyInputs?.ReadDoctrineMessageName
                ?? throw new InvalidDataException("story.doctrine-parameters.current_name_owner");
            var values = new string[5];
            for (int index = 0; index < 5; index++)
                values[index] = readName(arguments[index])
                    ?? throw new InvalidDataException("story.doctrine-parameters.current_name");
            Names = Array.AsReadOnly(values);
            Commands.Add(new CampaignEventDoctrineNameParameters(operation.Index, operation.Offset, Names));
            return 1;
        }
    }
}
