namespace San14.Data;

/// <summary>TC257670 reads Force160 through32C7D0's actual registered Base
/// lookup, then virtualB0. The selected byte is a Base4E key, not a Force
/// identity. The caller supplies this operation's current tables/list owner;
/// unavailable or reached unknown inputs remain unresolved.</summary>
public sealed class NormalArmyForceResultDefinitions(SourceWorldContext? current)
    : INormalArmyForceResultDefinitionQueries
{
    public int? ReadResultDefinitionIdentity(byte selectedBase4E)
    {
        if (current is null) return null;
        try
        {
            //32C7D0: key0 selects City0;1..61 scans real World80 by4E;
            //unmatched/out-of-range reacquires its current head. No validity
            //filter is performed before the source virtual call.
            BaseReference receiver = BaseLookup.Find(selectedBase4E, current);
            if (receiver.PhysicalSlot is null) return null;
            //City/CFoothold vB0=22CE50 reads the current District10/11.
            //Gate vB0=22CEC0 reads current representative-Hex byte14.
            return current.ReadBaseForceSlot(receiver);
        }
        catch (InvalidDataException)
        {
            //The reused native lookup/map leaf reports its specific missing
            //current field by this exception. Do not supply a guessed0.
            return null;
        }
    }
}
