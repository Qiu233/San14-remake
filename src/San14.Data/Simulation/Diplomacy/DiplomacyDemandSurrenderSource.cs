using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //6E20B0. This is the command's target admission, not its outcome trial.
    internal bool IsDemandSurrenderTargetCurrent(GameState state, SourceWorldContext current, int own, int other)
    {
        if (own == other || other is >= 46 and <= 50) return false;
        var qualification = new DiplomacyCommandSource(state, current);
        qualification.RequireInteraction(own, other, requireIndependentRecipient: false);
        if (qualification.TreatyMonths(own, other) != 0 ||
            ReadDiplomacyRelationCurrent(state, current, own, other).Level <= 0) return false;
        return true;
    }

    internal void RequireDemandSurrenderSubmissionCurrent(GameState state, SourceWorldContext current,
        PrepareDiplomaticOrder order)
    {
        if (order.GiftGold != 0 || order.GiftSupplies != 0 || order.GiftItemId is not null ||
            order.GiftAreaId is not null || order.TargetCityId is not null)
            throw DiplomacyCommandSource.Invalid("surrender_has_no_offer");
        if (!IsDemandSurrenderTargetCurrent(state, current, order.ForceId, order.TargetForceId))
            throw DiplomacyCommandSource.Invalid("surrender_target");
    }

    //69F2F0/6B0EB0: adjacency first, then fewer Cities, higher relation,
    //then lower Force identity. The caller supplies registry-order candidates.
    internal IReadOnlyList<int> SortDemandSurrenderTargetsCurrent(GameState state, SourceWorldContext current,
        int own, IReadOnlyList<int> candidates)
    {
        var rows = candidates.Select(id => new
        {
            Id = id,
            Adjacent = (_captiveFreedomHomes ??= new(_scenario)).AreForceCitiesAdjacent(current, own, id),
            Cities = DNeed(PersonArmyCaptureSource.ReadOwnedCityCount(current, DiplomacyForce(current, id)), "surrender_target_Cities"),
            Relation = ReadDiplomacyRelationCurrent(state, current, own, id).Relation
        }).ToArray();
        return rows.OrderByDescending(row => row.Adjacent).ThenBy(row => row.Cities)
            .ThenByDescending(row => row.Relation).ThenBy(row => row.Id).Select(row => row.Id).ToArray();
    }

    internal DiplomacyTrialResult ResolveDemandSurrenderTrialCurrent(GameState state,
        SourceWorldContext current, PersonnelDiplomacyTask task)
    {
        var trial = new DiplomacyQuery(this, state, current, task).DemandSurrenderTrial();
        //8077D0 performs the reached trial before this independent override.
        if (!TalentForceIsPlayerCurrent(state, current, DiplomacyForce(current, task.ForceId))) return trial;
        int outcome = task.AdvisorOutcomeAtDispatch ?? throw DiplomacyMissing("surrender_dispatch_advisor_outcome");
        return outcome switch
        {
            -1 => trial,
            0 or 1 => trial with { Accepted = outcome == 1, Probability = outcome == 1 ? 100 : 0,
                Branch = "surrender-dispatch-advisor-outcome" },
            _ => throw DiplomacyMissing("surrender_dispatch_advisor_outcome_range")
        };
    }

    private sealed partial class DiplomacyQuery
    {
        //3249B0(kind7). The returned boolean is final; it is not an offer
        //probability, has no App random draw, and does not mutate the world.
        internal DiplomacyTrialResult DemandSurrenderTrial()
        {
            if (NativeKind != 7) throw DiplomacyNumericMissing("surrender_trial_kind");
            DiplomacyTrialResult End(bool accepted, string branch, int? sample = null)
                => new(accepted, accepted ? 100 : 0, sample, "surrender-" + branch);
            int ownCities = CityCount(Own), otherCities = CityCount(Other);
            var sampler = package.ReadArmyDestructionSampler(State, Current)
                ?? throw DiplomacyNumericMissing("surrender_World450_45C");
            if (sampler.Day450 <= 360) return End(false, "first-year");
            if (!package.SurrenderAdjacent(Current, OwnOrdinal, OtherOrdinal)) return End(false, "not-adjacent");
            bool third = false;
            foreach (var entry in Current.ReadRegistryReferences(RegistryKind.Force)
                ?? throw DiplomacyNumericMissing("surrender_Force_registry"))
            {
                int id = DNeed(entry?.PhysicalSlot, "surrender_Force_pointer");
                if (id != 51 && id is not (>= 46 and <= 50) && id != OwnOrdinal && id != OtherOrdinal)
                { third = true; break; }
            }
            if (!third) return End(false, "last-pair");
            if (ownCities < unchecked(4 * otherCities)) return End(false, "city-ratio");
            if (TotalTroops(Own) < unchecked(3 * TotalTroops(Other))) return End(false, "troop-ratio");
            if (otherCities >= 5) return End(false, "five-cities");
            int ownRuler = Ruler(Own), otherRuler = Ruler(Other);
            if (RawHit(otherRuler, ownRuler, false) || RawHit(otherRuler, Executor, false))
                return End(false, "disliked");
            int politics = Politics(true), affinity = 0;
            if (Valid(ownRuler) && Valid(otherRuler))
            {
                int difference = Math.Abs(DNeed(OwnedPersonRelationInputs.ReadCurrentAffinity(State, ownRuler), "surrender_first_BE") -
                    DNeed(OwnedPersonRelationInputs.ReadCurrentAffinity(State, otherRuler), "surrender_second_BE"));
                affinity = Math.Min(Math.Min(difference, 150 - difference), 75);
            }
            int charisma = DNeed(new TacticRemakeCurrentAbilities(Current, package._scenario, package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language)).ResolveDimension(State, Id(otherRuler), 4,
                    (force, effect) => package._baseCapacityInputs.ReadPanelModifier(State, force, effect)), "surrender_ruler_CHA");
            int emperor = SurrenderEmperorForce(State, Current) == OtherOrdinal ? 10 : 0;
            int relationship = Spouse(Executor, otherRuler) || Sworn(Executor, otherRuler) ? 20 :
                RawHit(otherRuler, Executor, true) ? 10 : 0;
            //The source explicitly performs single-precision arithmetic and
            //truncates the entire sum before dividing it for the sample bound.
            float score = emperor + (float)politics;
            score += relationship;
            score += ((float)ownCities - otherCities) / 5f;
            score += ((float)ownCities / otherCities) / 2f;
            score -= (float)affinity / 7f;
            int baseScore = DCvtt(score);
            int sampleBound = DCvtt((float)baseScore / 2f);
            int sample = TacticEffectSourceSamplerMath.Sample(sampleBound, 1, ExecutorId,
                Id(ownRuler), Id(otherRuler), 0, sampler);
            int pressure = package.SurrenderLocalPressure(Current, this, otherRuler, otherCities);
            int threshold = DCvtt(100.0f - (float)Relation(OwnOrdinal, OtherOrdinal) / 4f + (float)charisma / 5f);
            return End(unchecked(pressure + sample + sampleBound) >= threshold, "trial", sample);
        }
    }

    private bool SurrenderAdjacent(SourceWorldContext current, int first, int second)
        => (_captiveFreedomHomes ??= new(_scenario)).AreForceCitiesAdjacent(current, first, second);

    private static int SurrenderEmperorForce(GameState state, SourceWorldContext current)
    {
        int person = DiplomacyLookupPerson(current, DNeed(CityCaptureTerritoryInputs.ReadEmperor1654(state), "surrender_Emperor1654"));
        if (!DNeed(current.ReadPersonNativeValidity(person), "surrender_Emperor_validity")) return 0;
        var decoded = PersonPositionRepair.DecodeSecondLocation(DNeed(current.ReadPersonSecondLocation(person), "surrender_Emperor_E4"));
        int cityId = decoded.Kind == 5 ? decoded.Index : 0;
        var city = current.ReadCityTableReference(cityId is >= 0 and <= 51 ? cityId : 0)
            ?? throw DiplomacyNumericMissing("surrender_Emperor_City");
        if (!DNeed(current.ReadBaseNativeValidity(city), "surrender_Emperor_City_validity")) return 0;
        return DNeed(current.ReadBaseForceSlot(city), "surrender_Emperor_Force");
    }

    private int SurrenderLocalPressure(SourceWorldContext current, DiplomacyQuery q, int ruler, int cities)
    {
        var home = BaseLookup.Find(DNeed(current.ReadPersonFirstLocation(ruler), "surrender_target_ruler_E2"), current);
        var capitalCity = CurrentGateMapQueries.FindCity(home, current);
        int capital = DNeed(capitalCity.PhysicalSlot, "surrender_target_City_physical");
        int own = 0, other = 0, bases = 0;
        foreach (var location in q.ForceBases(q.Other))
            bases = unchecked(bases + DNeed(current.ReadBaseResource(location, BaseResourceKind.Troops), "surrender_target_Base3C"));
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw DiplomacyNumericMissing("surrender_Army_registry"))
        {
            int physical = DNeed(entry?.PhysicalSlot, "surrender_Army_pointer");
            var army = new ArmyUnitReference(physical);
            if (!DNeed(current.ReadArmyUnitNativeValidity(army), "surrender_Army_validity") ||
                DNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(q.State, current, physical), "surrender_Army_excluded")) continue;
            int rawHex = DNeed(current.ReadArmyUnitRawHex2A(army), "surrender_Army2A");
            var hex = current.ReadHexTableReference(rawHex is >= 0 and < 48400 ? rawHex : 0)
                ?? throw DiplomacyNumericMissing("surrender_Army_Hex");
            int rawArea = DNeed(current.ReadHexAreaSlot(hex), "surrender_Army_Area12");
            var area = current.ReadAreaTableReference(rawArea is >= 0 and <= 500 ? rawArea : 0)
                ?? throw DiplomacyNumericMissing("surrender_Army_Area");
            if (!DNeed(current.ReadAreaNativeValidity(area), "surrender_Area_validity") ||
                DNeed(current.ReadAreaCitySlot(area), "surrender_Area35") != capital) continue;
            int force = current.ReadArmyUnitRawForceSlot(army);
            int troops = DNeed(current.ReadArmyReturnResourceFields(army)?.Ready16, "surrender_Army16");
            if (force == q.OtherOrdinal) other = unchecked(other + troops);
            else if (force == q.OwnOrdinal) own = unchecked(own + troops);
        }
        int ratio = Math.Max(0, own / Math.Max(1, unchecked(bases + other)));
        return Math.Min(30, unchecked(unchecked(ratio * ratio * 10) / 10) / cities);
    }
}
