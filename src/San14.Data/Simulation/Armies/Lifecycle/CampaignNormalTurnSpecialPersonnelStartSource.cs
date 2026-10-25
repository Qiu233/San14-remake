using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState ApplyNormalTurnSpecialPersonnelStartCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new MonthlyCampaignMaintenanceSource(this, state, current, pool).InitializeSpecialPersonnel();

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        //27BCD0 runs once at the exact start period. Its synthetic BEFORE=3
        //only delivers the existing level4/5 personnel; it never changes13.
        internal GameState InitializeSpecialPersonnel()
        {
            if (ReadCommanderSelectionPeriod(state) != P(SimulationParameterKeys.ForceRelations.SpecialPersonnelStartPeriod))
                return state;
            foreach (int ordinary in Registry(RegistryKind.Force))
            {
                var own = Force(ordinary);
                if (!Valid(own)) continue;
                int id = Physical(own);
                if (id is >= 46 and <= 51) continue;
                for (int slot = 46; slot <= 50; slot++)
                {
                    var special = current.ReadForceTableReference(slot) ?? throw Missing("initial_special_Force_pointer");
                    if (!Valid(special) || Need(current.ReadConditionForceDependence(new(slot)), "initial_special_Force194") != 0) continue;
                    int level = package.ReadDiplomacyRelationCurrent(state, current, ordinary, slot).Level;
                    if (level <= 3) continue;
                    ForceRelationChangeCurrent.ApplyInitialSpecialPersonnel(() => state, current, own, special,
                        level, P, ConsumeSpecialPersonnel, package.ReadApplicationGameplay(),
                        count => Narration(Format(0x324C, new(Force0: Physical(special), Integers: [count]))),
                        removedPeople: RecallSpecialPersonnel);
                }
            }
            return state;
        }
    }
}
