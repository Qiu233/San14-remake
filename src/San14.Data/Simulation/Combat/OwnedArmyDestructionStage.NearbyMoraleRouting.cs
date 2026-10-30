using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    //All four259360 nearby negative callers pass(null-source,mode1,arg5=0).
    //Category39's temporary cap is cleared before either value loop repeats;
    //it is not a deferred-admission argument to2709A0.
    private GameState ApplyNearbyMoraleWithRouting(GameState state, SourceWorldContext current,
        ArmyUnitReference army, int adjustment, NativeMapSeasonDate? sourceDate,
        TacticSourceSamplerWorld? sourceSampler, List<CampaignEffectRequest> feedback)
    {
        //The enclosing defeat may already own newly admitted settlements.
        //Use that actual queue when the shared zero-routing query binds it.
        state = state with { PendingCombatSettlements = current.OwnedDestructionQueue
            ?? state.PendingCombatSettlements ?? throw Missing("current.nearby_morale_queue") };
        var applied = ArmyMoraleZeroRoutingCurrent.ApplyImmediate(state, current, army, adjustment,
            package.TrapMobility(), Immediate);
        return applied.State ?? throw Missing(applied.UnresolvedOwner ?? "current.nearby_morale_2709A0");

        GameState Immediate(GameState value, UnitDefeatRequest request)
        {
            var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("current.nearby_morale_pool");
            //The outer259360 already bound every destruction input. Borrow
            //that current image and manager; do not overlay its old prefix.
            value = value with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
                ?? throw Missing("current.nearby_morale_personnel") };
            var changedPeople = new HashSet<int>();
            var result = ApplyBattleDefeatCurrent(value, current, pool, request,
                sourceSampler ?? package.ReadArmyDestructionSampler(value, current)
                    ?? throw Missing("current.nearby_morale_sampler"),
                sourceDate ?? value.NativeMapSeason?.ThirdDate
                    ?? throw Missing("current.nearby_morale_date"),
                feedback, Refresh, person => changedPeople.Add(person));
            if (changedPeople.Count == 0) return result;
            //Use the established immediate-consumer publication for current
            //trait cleanup callbacks, including every related Commander.
            var people = new Dictionary<int, OfficerState>(result.Officers);
            foreach (int physical in changedPeople)
            {
                int id = Known(current.ReadPersonIdentity(physical), "nearby_morale_changed_Person10");
                var person = people.GetValueOrDefault(id) ?? throw Missing("current.nearby_morale_changed_Person");
                var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
                if (activation is not { DurationSlotCount: 9, ActiveSlots: not null })
                    throw Missing("current.nearby_morale_changed_activation");
                people[id] = person with { TraitActivation = activation };
            }
            return result with { Officers = people };

            void Refresh(ArmyUnitReference target) => feedback.Add(new(null, null)
            { ArmyTraitViewRefresh = new(ArmyDestructionQueue.ResolveUnitId(value, target.PhysicalSlot)
                ?? throw Missing("current.nearby_morale_refresh_Army")) });
        }
    }
}
