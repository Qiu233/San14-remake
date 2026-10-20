using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>CE7F2F30/7C66D0's current unsubmitted draft header. Each group
    /// has independent missing inputs. This neither reserves resources nor
    /// validates or submits a deployment command.</summary>
    public DeploymentDraftResources QueryDeploymentDraftResources(GameState state, int locationId,
        IReadOnlyList<DeploymentDraftRow> rows)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rows);
        SourceWorldContext current;
        BaseReference sourceBase;
        try
        {
            sourceBase = locationId switch
            {
                >= 1 and <= 51 when state.Cities.TryGetValue(locationId, out var city) && city.Id == locationId =>
                    new(BaseKind.City, locationId),
                >= 52 and <= 61 when state.Gates.TryGetValue(locationId, out var gate) && gate.Id == locationId =>
                    new(BaseKind.Gate, locationId - 51),
                _ => throw DeploymentResourceMissing("base_identity")
            };
            current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        }
        catch (InvalidDataException error) { return new(null, null, null, error.Message, error.Message, error.Message); }
        DeploymentTroopResources? troops = null;
        DeploymentGoldResources? gold = null;
        DeploymentCommandBookResources? books = null;
        int? decisionCommandBooks = null;
        string? troopsMissing = null, goldMissing = null, booksMissing = null;
        try { troops = ReadTroops(); } catch (InvalidDataException error) { troopsMissing = error.Message; }
        try { gold = ReadGold(); } catch (InvalidDataException error) { goldMissing = error.Message; }
        try { books = ReadBooks(); } catch (InvalidDataException error) { booksMissing = error.Message; }
        return new(troops, gold, books, troopsMissing, goldMissing, booksMissing)
            {DecisionCommandBooks=decisionCommandBooks};

        DeploymentTroopResources ReadTroops()
        {
            int available = DeploymentResourceNeed(current.ReadBaseResource(sourceBase, BaseResourceKind.Troops), "current_base_troops");
            int assigned = 0;
            foreach (var row in rows) assigned = unchecked(assigned + row.Request.Troops);
            //The original sums low32 bits, subtracts low32 bits, THEN applies
            //a signed max with0. A wide or saturating sum changes its result.
            return new(available, assigned, Math.Max(0, unchecked(available - assigned)));
        }
        DeploymentGoldResources ReadGold()
        {
            //Base.v90: City reads its34. Gate first resolves22A940's current
            //owner City and calls that City's v90; Gate.raw34 is not consumed.
            BaseReference owner = sourceBase.Kind == BaseKind.City ? sourceBase : CurrentGateMapQueries.FindCity(sourceBase, current);
            if (owner.PhysicalSlot is not int cityId || !state.Cities.ContainsKey(cityId))
                throw DeploymentResourceMissing("current_gold_city_owner");
            int available = DeploymentResourceNeed(current.ReadBaseResource(owner, BaseResourceKind.Gold), "current_city_gold");
            int cost = 0;
            var formationCosts = new Dictionary<int, ushort>();
            foreach (var row in rows)
            {
                int requested = row.Request.FormationId;
                //A negative source index addresses before the native table;
                //it is not the >=21 fallback to its actual default receiver.
                if (requested < 0) throw DeploymentResourceMissing("negative_formation_index");
                int formation = requested < 21 ? requested : 0;
                if (!formationCosts.TryGetValue(formation, out ushort value))
                {
                    var parameters = state.NativeArmySimulation?.Parameters;
                    if (parameters is null || parameters.Language != Seed.Language ||
                        parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                        throw DeploymentResourceMissing("formation_rule_profile");
                    value = DeploymentResourceNeed(TacticRemakeCurrentFormations.ReadRawWord(state, formation, 0x9A), "current_formation_cost");
                    formationCosts.Add(formation, value);
                }
                cost = unchecked(cost + value);
            }
            //No max with0: the source displays negative signed funds in red.
            return new(available, cost, unchecked(available - cost));
        }
        DeploymentCommandBookResources ReadBooks()
        {
            var district = current.ReadBaseDistrictReference(sourceBase)
                ?? throw DeploymentResourceMissing("current_base_district");
            if (district.PhysicalSlot is not int physical)
                return new(null, false, null, rows.Count, null);
            if (physical != 0 && !state.Districts.ContainsKey(physical))
                throw DeploymentResourceMissing("current_district_owner");
            if(state.Districts.GetValueOrDefault(physical)?.OrdersRemaining is int raw && raw is >=0 and <=255)
                decisionCommandBooks=raw;
            if (!DeploymentResourceNeed(current.ReadDistrictNativeValidity(physical), "current_district_validity"))
                return new(physical, false, null, rows.Count, null);
            int available = decisionCommandBooks??throw DeploymentResourceMissing("current_command_books");
            return new(physical, true, available, rows.Count, unchecked(available - rows.Count));
        }
    }
    private static T DeploymentResourceNeed<T>(T? value, string field) where T : struct => value ?? throw DeploymentResourceMissing(field);
    private static InvalidDataException DeploymentResourceMissing(string field) => new("deployment.resources." + field);
}
