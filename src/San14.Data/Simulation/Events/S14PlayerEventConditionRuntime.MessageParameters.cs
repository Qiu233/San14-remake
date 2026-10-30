namespace San14.Data.Formats;

public interface IS14MessageParameterConditionQueries
{
    byte? ReadConditionPersonIllness(S14EventConditionPersonReference person);
}

public sealed partial class S14PlayerEventConditionRuntime
{
    internal static bool IsMessageParameterConditionCode(int code) => code is 261 or 281;

    internal int InvokeMessageParameterCondition(S14ConditionContext context)
    {
        var arguments = context.Operation.RawArguments;
        if (arguments.Count != 8) throw new InvalidDataException("message-condition.operands");
        if (context.Operation.Code == 261)
        {
            var person = _storyActorResolver is { } resolve ? resolve(arguments[0]) : RawPersonPointer(arguments[0]);
            if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "illness Person validity")) return 0;
            byte value = Known((_queries as IS14MessageParameterConditionQueries)?.ReadConditionPersonIllness(person),
                "illness Person161");
            return Compare(value, arguments[1], arguments[2]) ? 1 : 0;
        }
        if (context.Operation.Code != 281) throw new InvalidDataException("message-condition.opcode");
        var city = ResolveCityPointer(arguments[0]);
        var receiver = new BaseReference(BaseKind.City, city.PhysicalSlot);
        if (!BaseValid(receiver)) return 0;
        int supplies = Known(_queries.ReadConditionBaseSupplies(receiver), "City supplies virtualA0");
        return Compare(supplies, arguments[1], arguments[2]) ? 1 : 0;
    }
}
