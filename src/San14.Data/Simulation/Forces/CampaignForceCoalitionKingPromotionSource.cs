using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2D4480 keeps its existing rank2 NPC contract;58 shares the same writer.
    internal GameState ApplyNormalTurnCoalitionKingPromotionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference force)
        => ApplyNormalTurnForceRankPromotionCurrent(state, current, pool, force, 2);

    //The existing normal maintenance callers are deliberately NPC-only;
    //ordinary Story owns the player choice/messages around the shared kernel.
    internal GameState ApplyNormalTurnForceRankPromotionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference force, int rank,
        int kingdomNameChoice = 0)
    {
        if (!KingPromotionNeed(current.ReadForceNativeValidity(force), "Force_validity")) return state;
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw KingPromotionMissing("current_pool_binding");
        if (TalentForceIsPlayerCurrent(state, current, force)) throw KingPromotionMissing("NPC_scope");
        if (rank is < 1 or > 8) throw KingPromotionMissing("NPC_rank_scope");
        var context = ReadForceRankWorldContext(state, current, pool, force, rank);
        if (context is null) return state;
        if (rank == 1)
        {
            var named = PrepareNormalTurnEmpireNameCurrent(state, current, pool, force, kingdomNameChoice);
            state = named.State;
            if (!named.Valid) return state;
        }
        var effects = new List<CampaignEffectRequest>();
        state = ApplyForceRankWorldCurrent(state, current, context, effects);
        state = ApplyNormalTurnRankPromotionReportsCurrent(state, current, pool, force,
            rank, context.EmperorPhysicalSlot, context.SelfProclaimed);
        if (effects.Count != 0)
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw KingPromotionMissing("experience_effect_owner"))
                .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw KingPromotionMissing("experience_effect_revision"), effects) };
        return state;
    }

    private static bool KingPromotionProtectedKin(GameState state, SourceWorldContext current, int person, int ruler)
    {
        //2C9FA0 sworn group,2C8E90 spouse, and2C8E40 parents in both
        //directions are separate predicates; siblings and likes do not enter.
        int personId = KingPromotionNeed(current.ReadPersonIdentity(person), "kin_Person10");
        int rulerId = KingPromotionNeed(current.ReadPersonIdentity(ruler), "kin_ruler10");
        ushort group = OfficeWord(state, person, 0xBC);
        if (group != 0 && group == OfficeWord(state, ruler, 0xBC)) return true;
        if (OfficeWord(state, person, 0xBA) is var spouse && spouse != 0 && spouse == rulerId) return true;
        return Parent(person, rulerId) || Parent(ruler, personId);
        bool Parent(int child, int parent) => OfficeWord(state, child, 0xB4) is var father && father != 0 && father == parent ||
            OfficeWord(state, child, 0xB6) is var mother && mother != 0 && mother == parent;
    }

    private static int KingPromotionDoctrine(GameState state, SourceWorldContext current, int person)
    {
        int id = KingPromotionNeed(current.ReadPersonIdentity(person), "doctrine_Person10");
        var owner = state.Officers.GetValueOrDefault(id) ?? throw KingPromotionMissing("doctrine_Person_owner");
        if (current.ResolveOfficerSource(id) != person) throw KingPromotionMissing("doctrine_Person_binding");
        int raw = owner.DoctrineId ?? owner.AbilityState?.ObservedDoctrineId ?? throw KingPromotionMissing("Person14C");
        return (uint)raw <= 255 ? raw : throw KingPromotionMissing("Person14C_width");
    }

    private static int KingPromotionLookup(SourceWorldContext current, int raw) =>
        KingPromotionNeed((raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?.PhysicalSlot, "Person_lookup_pointer");
    private static T KingPromotionNeed<T>(T? value, string field) where T : struct => value ?? throw KingPromotionMissing(field);
    private static InvalidDataException KingPromotionMissing(string field) => new("normal-turn.coalition.king-promotion." + field);
}
