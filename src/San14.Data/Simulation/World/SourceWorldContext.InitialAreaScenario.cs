namespace San14.Data;

public sealed partial class SourceWorldContext
{
    // A current owns only its explicit changes, including explicit unknowns.
    // Detached home/governor drafts do not borrow this channel: their earlier
    // contracts did not carry scenario auxiliaries or neighbor knowledge.
    private InitialAreaScenarioValues? _initialAreaScenarioValues;
}
