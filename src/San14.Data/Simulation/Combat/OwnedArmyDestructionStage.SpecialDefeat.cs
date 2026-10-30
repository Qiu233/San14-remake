using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    //TC259360's bVar2 is captured before trait cleanup/Person callbacks.
    //It is not232860: a native special commander has no real Base1..61.
    private static bool ReadSpecialDefeatReclaimCurrent(SourceWorldContext current,
        ForceReference capturedVictimForce, int commanderPhysical)
    {
        if (capturedVictimForce.PhysicalSlot is null) return false;
        int force = Known(current.ReadForcePhysicalSlot(capturedVictimForce), "special_defeat_captured_Force60");
        if (force == 51) return true;
        if (force is < 46 or > 50) return false;
        ushort first = Known(current.ReadPersonFirstLocation(commanderPhysical), "special_defeat_PersonE2");
        if (first is >= 1 and <= 61) return false;
        var personForce = current.ReadPersonForce(commanderPhysical)
            ?? throw Missing("special_defeat_Person_Force22CB40");
        int own = Known(current.ReadForcePhysicalSlot(personForce), "special_defeat_Person_Force60");
        return own is >= 46 and <= 50;
    }

    //25C180 has already admitted the Army to pending70. Only local1418's
    //known true branch reaches this Person unregister/reset; a special
    //winner or a special-looking Person ID is not sufficient.
    private GameState ReclaimSpecialDefeatedCommanderCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int commanderPhysical)
        => package.ReclaimArmyDefeatSpecialPersonCurrent(state, current, pool, commanderPhysical);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ReclaimArmyDefeatSpecialPersonCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int physical)
        => new PersonDeathSource(this, state, current, pool).ReclaimArmyDefeatSpecialPersonCurrent(physical);

    internal sealed partial class PersonDeathSource
    {
        //259360: current Person manager100 contains-check/32B890 first,
        //then shared226480 reset. No cause4, death, recall status7,
        //freedom placement, source-date update or new random draw runs.
        internal GameState ReclaimArmyDefeatSpecialPersonCurrent(int physical)
        {
            RemoveRegistration(physical);
            ResetPerson(physical);
            return latest;
        }
    }
}
