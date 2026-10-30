using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    //Ordinary323/286 supply259360(Army,null,null,0,4). Unlike the
    //absorption caller they do not restrict the selected Army's Force ordinal.
    internal ArmyCause4Result ApplyStoryCause4Current(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int physical, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        try
        {
            var army = new ArmyUnitReference(physical);
            if (!Known(current.ReadArmyUnitNativeValidity(army), "story_cause4_Army_validity")) return new(state, null, null);
            //259360's early exit precedes commander/status/resource reads.
            if (ArmyPending70Admission.ContainsCurrentPointer(new(RegistryKind.Army, physical), new Pending(current)))
                return new(state, null, null);
            int commander = CommanderPhysical(current, army);
            if (Known(current.ReadPersonStatus(commander), "story_cause4_CommanderE6") == 5)
                return ApplyFreeCommanderCause4WithinOperationCurrent(state, current, pool, physical, date, effects);
            return ApplyEmployedNullWinnerCause4Current(state, current, pool, physical, date, effects,
                allowSpecialForce: true, sourceSampler: null, allowBanditForce: true);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
}
