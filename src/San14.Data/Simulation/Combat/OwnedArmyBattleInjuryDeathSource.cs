using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //27DE20..27DE5E:295EE0(person,killer,valid(killer)?1:0,0,1,0,0,1,0).
    //The injury leaf has already released its personality temporary. This
    //writer borrows the enclosing259360 current/pool and owns no Army exit.
    internal (GameState State, int? NativeReturn) ApplyBattleInjuryDeathCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, PersonArmyInjuryDeathRequest request)
        => new PersonDeathSource(this, state, current, pool).ApplyCombatDeath(
            request.PersonPhysical, request.Killer, request.Cause, false, true, true, false);
}
