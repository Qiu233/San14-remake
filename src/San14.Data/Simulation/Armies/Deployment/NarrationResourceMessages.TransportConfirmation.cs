namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveTransportFoodConfirmation(INarrationCurrentContext context)
    {
        _prefix = "transport.food-confirmation";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, _ => throw Failure("unexpected_random"),
                0x57AC, Get(0x57AC, true), _ => false, true, null).Text;
        }
        finally { _report = previous; }
    }
}
