using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The seasonal population and idle-area writers reached by the
    /// normal city economy caller. The caller supplies its existing current
    /// world and invokes this branch on the actual first day of the month.</summary>
    internal GameState ApplyNormalTurnCityGrowthCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, BaseReference city)
        => new NormalTurnCityGrowthSource(this, state, current, pool, city).Run();

    private sealed class NormalTurnCityGrowthSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool, BaseReference city)
    {
        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("shared_current_pool");
            var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");
            int previousMonth = date.Month - 1;
            if (previousMonth < 1) previousMonth = 12;
            //333DE0 compares the two entries of the actual season table and
            //only admits the first ten days. Day==1 belongs to3032C0's caller.
            if (date.Day >= 11 || MapSeasonMath.SelectMonth(date.Month).SeasonIndex ==
                MapSeasonMath.SelectMonth(previousMonth).SeasonIndex) return state;
            if (Need(current.ReadBaseObjectLink(city), "Base4E") >= 52) return state;

            GrowPopulation();
            DecayIdleAreas();
            return state;
        }

        private IEnumerable<AreaReference> Areas()
        {
            int physical = Need(city.PhysicalSlot, "City_receiver");
            //223350/2234F0: ordered managerB0, with only Area35 membership.
            //The representative-Hex ownership test is a later, separate gate.
            foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw Missing("Area_managerB0"))
            {
                var area = entry ?? throw Missing("Area_pointer");
                if (Need(current.ReadAreaCitySlot(area), "Area35") == physical) yield return area;
            }
        }

        private void GrowPopulation()
        {
            //302CA0 deliberately includes areas owned by another force. Only
            //the appointed-person trait bonus depends on matching ownership.
            foreach (var area in Areas())
            {
                int physical = Need(area.PhysicalSlot, "population_Area_receiver");
                var owner = Area(physical);
                int population = Need(owner.NativeNormalAftermath?.Quantity52, "population_Area52");
                int security = Byte(owner.Security, "population_Area54");
                int basis = Math.Max(population, 5000);
                int securityBasis = Math.Max(security, 10);
                int random = Draw(3);
                int cap = Math.Min(Math.Max(unchecked(60000 - basis) / Nonzero(Rule("population-cap-divisor")), 1),
                    Rule("population-maximum-step"));
                int addition = unchecked(unchecked(unchecked(random + Rule("population-rate-base")) *
                    unchecked(securityBasis + 1)) * basis) / 10000;
                if (population >= Rule("population-step-cap-threshold")) addition = Math.Min(addition, cap);

                int bonus = 0;
                int cityForce = Need(current.ReadBaseForceSlot(city), "population_City_Force");
                if (Need(current.ReadAreaCurrentForceSlot(area), "population_Area_Force") == cityForce)
                {
                    int rawPerson = Need(current.ReadAssignmentArea36(physical), "population_Area36");
                    var person = current.ReadPersonLookupReference(rawPerson < 6001 ? rawPerson : 0)
                        ?? throw Missing("population_Person_lookup");
                    int personPhysical = Need(person.PhysicalSlot, "population_Person_receiver");
                    if (Need(current.ReadPersonNativeValidity(personPhysical), "population_Person_validity"))
                        bonus = Need(TacticRemakeCurrentTraits.PersonContribution(current, personPhysical, 0x23), "population_trait35");
                }
                int gain = unchecked(unchecked(Rule("population-trait-scale") * bonus) * addition) / 100;
                int after = Math.Clamp(unchecked(unchecked(population + addition) + gain), 0, 60000);
                state = state with { Areas = new(state.Areas) { [physical] = owner with {
                    NativeNormalAftermath = owner.NativeNormalAftermath! with { Quantity52 = (ushort)after } } } };
            }
        }

        private void DecayIdleAreas()
        {
            //28E3C0 tests the raw Base virtualB0 result, not Force validity.
            if (Need(current.ReadBaseForceSlot(city), "decay_City_Force") == 0) return;
            foreach (var area in Areas())
            {
                int physical = Need(area.PhysicalSlot, "decay_Area_receiver");
                if (Need(current.ReadAssignmentArea36(physical), "decay_Area36") != 0) continue;
                int cityForce = Need(current.ReadBaseForceSlot(city), "decay_City_Force");
                if (Need(current.ReadAreaCurrentForceSlot(area), "decay_Area_Force") != cityForce) continue;

                int multiplier = unchecked(Draw(Rule("idle-development-random-bound")) + 1);
                var writer = new OwnedAreaDevelopmentContext(state, current, package._normalAftermathInputs);
                CeAreaDevelopmentField[] fields = [CeAreaDevelopmentField.Commerce,
                    CeAreaDevelopmentField.Agriculture, CeAreaDevelopmentField.Barracks];
                //Read all three original levels before the first progress
                //setter; each setter's notify1 tail retains the same current.
                var progress = fields.Select(field => Need(writer.ReadAreaProgress(area, field), "idle_Area_progress")).ToArray();
                int[] levels = progress.Select(CeAreaIncomeMath.Level).ToArray();
                for (int index = 0; index < fields.Length; index++)
                {
                    int after = Math.Clamp(unchecked(progress[index] - unchecked(levels[index] * multiplier)), 0, 1000);
                    package._normalAftermathInputs.Development.WriteCurrentProgress(area, fields[index], after, 1, writer);
                }
                state = writer.State;

                //This draw occurs even when security or the resident count
                //will reject the following decline. Bound1 consumes no App draw.
                int loss = Draw(Rule("idle-security-random-bound"));
                int security = Byte(Area(physical).Security, "idle_Area54");
                int threshold = unchecked(Rule("idle-security-baseline") - Rule("idle-security-margin"));
                if (security <= threshold) continue;
                ushort link = Need(current.ReadBaseObjectLink(city), "idle_residents_Base4E");
                if (!EnumerateBaseResidents(current, link, Missing).Any()) continue;
                int afterSecurity = Math.Clamp(unchecked(security - unchecked(loss + 1)), 0, 100);
                int areaForce = Need(current.ReadAreaCurrentForceSlot(area), "idle_security_Area_Force");
                var force = current.ReadForceTableReference(areaForce < 52 ? areaForce : 0)
                    ?? throw Missing("idle_security_Force_lookup");
                int identity = Need(current.ReadForcePhysicalSlot(force), "idle_security_Force60");
                if (identity is >= 46 and <= 50) continue;
                var owner = Area(physical);
                state = state with { Areas = new(state.Areas) { [physical] = owner with { Security = afterSecurity } } };
            }
        }

        private AreaState Area(int physical) => state.Areas.GetValueOrDefault(physical) ?? throw Missing("Area_owner");
        private int Draw(int bound)
        {
            //3FCF40 is the mutable application stream, not the pure world
            //sampler. Preserve its early return for bounds below two.
            if (bound < 2) return 0;
            uint before = (state.CampaignAppRandom32 ?? throw Missing("App32_owner")).State;
            uint next = SourceRandom32Mix.Advance(before);
            state = state with { CampaignAppRandom32 = new(next) };
            return (int)((next & 0x7FFF_FFFFu) % (uint)bound);
        }
        private int Rule(string name)
        {
            string key = "simulation.normal-turn.city-growth." + name;
            var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("parameters");
            if (bank.Float32Values.ContainsKey(key)) throw Missing("integer_parameter_" + name);
            if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
            if (package.Seed.Language is not ("TC" or "SC")) throw Missing("locale_parameter_" + name);
            //Complete TC/SC population and idle-area bodies independently
            //read these same nine file-backed signed integers. Saved values
            //remain first; runtime reads no executable or execution cache.
            return name switch {
                "population-rate-base" => 3, "population-cap-divisor" => 4,
                "population-maximum-step" => 2000, "population-step-cap-threshold" => 40000,
                "population-trait-scale" => 1, "idle-development-random-bound" => 1,
                "idle-security-random-bound" => 3, "idle-security-baseline" => 60,
                "idle-security-margin" => 10, _ => throw Missing("parameter_" + name)
            };
        }
        private static int Nonzero(int value) => value != 0 ? value : throw Missing("population_zero_divisor");
        private static int Byte(int? value, string field) => value is >= 0 and <= 255 ? value.Value : throw Missing(field);
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal-turn.city-growth." + field);
    }
}
