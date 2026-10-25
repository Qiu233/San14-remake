using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //428360 ->279760 is the special-person health recovery pass. It
    //complements27CB30, whose ordinary recovery path excludes these people.
    //The scheduler-facing name is retained; no personnel task is retired here.
    internal GameState ApplyNormalTurnSpecialPersonnelReleasesCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new CampaignNormalTurnPersonnelAgingSource(this, state, current, pool).RunSpecialPersonnelRecovery();

    internal sealed partial class CampaignNormalTurnPersonnelAgingSource
    {
        internal GameState RunSpecialPersonnelRecovery()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("special_recovery.shared_pool");
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("special_recovery.player_pointer");
            HexListHandle? recovered = null, excluded = null;
            try
            {
                //Creation and destruction preserve the source's two actual
                //temporary descriptors, even when no special people qualify.
                recovered = HealthTemporary();
                excluded = HealthTemporary();
                new PersonAppearanceSource(package, state, current, pool).FillExcluded(excluded,
                    IsSpecialRecoveryPerson, skipEmptyMembers: true);
                foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots()
                    ?? throw Missing("special_recovery.Person100"))
                {
                    int person = Need(entry, "special_recovery.Person_pointer");
                    if (!IsSpecialRecoveryPerson(person) || HealthPeople(excluded).Contains(person)) continue;
                    //26A130's validity/status/busy/flag, player difficulty,
                    //policy41D690(effect29) and stationed healer calculation
                    //are the same owner used by the ordinary health pass.
                    int probability = HealthRecoveryProbability(person, knownSpecialPerson: true);
                    int sample = HealthSample(1, HealthIdentity(person), 0);
                    if (sample >= probability) continue;
                    HealthWrite(person, Math.Clamp(HealthByte(person) - 1, 0, 3));
                    if (HealthIsPlayerForce(player, HealthForce(person))) HealthAppend(recovered, person);
                }
                //Reports follow the complete scan in recovered-list order.
                //The existing writer preserves5D80, category0, current task
                //position, World450, person payload and mark bit4.
                foreach (int person in HealthPeople(recovered)) HealthReport(person, 0x5D80);
                return state;
            }
            finally
            {
                if (excluded is not null) current.ClearPersonInjuryTemporary(excluded, true);
                if (recovered is not null) current.ClearPersonInjuryTemporary(recovered, true);
            }
        }

        private bool IsSpecialRecoveryPerson(int person)
            => HealthSpecialForce(person) || HealthBandAtOrdinaryBase(person);
    }
}
