using San14.Core;

namespace San14.Data;

/// <summary>The shared command-capacity value and the authored first-nonzero
/// colour callbacks. Callers retain their independent display/sort validity gates.</summary>
internal static class CurrentPersonCommandCapacitySource
{
    internal static int ReadCapacity(GameState state,SourceWorldContext current,int physical,
        Func<string,InvalidDataException> missing)
    {
        var capacity=OfficerCommandCapacity.Resolve(state,current,physical);
        return capacity.Capacity ?? throw new InvalidDataException(capacity.UnresolvedInput ?? missing("information_command_capacity").Message);
    }
    internal static int? ReadPalette(GameState state,SourceWorldContext current,int physical,
        Func<int> creditBonus,Func<int,int,int?> forceModifier,Func<string,InvalidDataException> missing)
    {
        if(creditBonus()>0)return 65;
        var force=current.ReadPersonForce(physical)??throw missing("information_Person_Force_pointer");
        if(Need(current.ReadForceNativeValidity(force),"information_command_colour_Force_validity"))
        {
            int slot=Need(current.ReadForcePhysicalSlot(force),"information_command_colour_Force");
            if(Need(forceModifier(slot,0x37),"information_command_colour_Force_effect37")>0)return 65;
        }
        if(Need(current.ReadPersonNativeValidity(physical),"information_Person_validity")&&
            Need(TacticRemakeCurrentTraits.CommanderEffectSum(current,physical,0x4D),"information_command_colour_Person_effect4D")>0)return 65;
        return null;
        T Need<T>(T? value,string name)where T:struct=>value??throw missing(name);
    }
}
