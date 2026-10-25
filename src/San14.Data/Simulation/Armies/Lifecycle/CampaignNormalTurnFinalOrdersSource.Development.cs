using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnFinalOrdersSource
    {
        //28F390 closes this Turn's signed development subtotal into the
        //cumulative score; annual28/76 later classifies the actual doctrines.
        private void AccumulateDevelopment()
        {
            var player = Player;
            var emperorCity = City(0);
            int emperor = Person(N(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654"));
            if (PersonValid(emperor))
            {
                var position = PersonPositionRepair.DecodeSecondLocation(N(current.ReadPersonSecondLocation(emperor), "EmperorE4"));
                if (position.Kind == 5) emperorCity = City(position.Index);
            }
            var emperorForce = Valid(emperorCity) ? Force(emperorCity) : Force(0);
            bool laterYear = Date.Year != N(package._scenario.Snapshot.WorldParameters?.RawWorldField2E, "scenario_start_year");
            bool clearWarning = laterYear && Date.Month == 1 && Date.Day == 1;
            int quarterBit = laterYear && Date.Month > 3 ? Date.Month < 7 ? 2 : Date.Month < 10 ? 4 : 8 : 0;
            int? minimumScore = null, minimumDevelopment = null;
            int playerScore = 0, playerDevelopment = 0;
            for (int slot = 0; slot < 52; slot++)
            {
                var force = Force(slot);
                if (!Ordinary(force)) continue;
                int id = Id(force);
                byte flags = Flags(id);
                if ((flags & 8) != 0 && (flags & 0x20) == 0)
                {
                    bool trigger = Date.Month == 12;
                    if (Date.Month is >= 2 and <= 11)
                    {
                        int contribution = N(current.ReadForce164(force), "bonus_Force164");
                        int years = ForceWord11E(force);
                        var home = RulerHome(force);
                        int hex = current.ReadBaseCurrentHexIndex(home);
                        int ruler = N(current.ReadForceRecord(id)?.RawRuler10, "bonus_Force10");
                        trigger = Sample(100, ruler, years, contribution, hex) < P("doctrine-bonus-chance", 50);
                    }
                    if (trigger) ApplyDoctrineResourceBonus(force);
                }
                int security = 0, count = 0;
                foreach (var area in Areas(force))
                {
                    if (!N(current.ReadAreaNativeValidity(area), "security_Area_validity")) continue;
                    security = unchecked(security + N(state.Areas.GetValueOrDefault(N(area.PhysicalSlot, "security_Area"))?.Security, "security_Area54"));
                    count++;
                }
                if (count > 0 && security > 0 && security / count > 0) AddDevelopment(id, security / count);
                int wounds = 0;
                foreach (var home in Bases(force))
                    if (Valid(home)) wounds = unchecked(wounds + N(current.ReadArmyReturnBaseWounded40(home), "wounded_Base40"));
                foreach (int physical in Registry(RegistryKind.Army))
                {
                    var army = new ArmyUnitReference(physical);
                    if ((current.ReadArmyUnitForceReference(army) ?? throw Missing("wounded_Army_Force")) != force) continue;
                    if (N(current.ReadArmyUnitNativeValidity(army), "wounded_Army_validity"))
                        wounds = unchecked(wounds + N(current.ReadArmyReturnResourceFields(army)?.Wounded18, "wounded_Army18"));
                }
                if (wounds / 1000 > 0) AddDevelopment(id, -(wounds / 1000));
                byte rank = N(current.ReadMoraleWaveForceRank47(id), "rank47");
                if (rank == 10) AddDevelopment(id, P("yellow-emperor-development", 20));
                if (force == emperorForce) AddDevelopment(id, P("han-emperor-development", 20));
                if (rank == 1) AddDevelopment(id, P("other-emperor-development", 20));
                short subtotal = Development(id);
                int policy = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, 0x49, true).Value;
                if (subtotal > 0 && policy > 0) AddDevelopment(id, unchecked(policy * subtotal) / 100);
                int final = Development(id);
                AddScore(id, final);
                int score = Score(id);
                minimumScore = minimumScore.HasValue ? Math.Min(minimumScore.Value, score) : score;
                minimumDevelopment = minimumDevelopment.HasValue ? Math.Min(minimumDevelopment.Value, final) : final;
                if (force == player) { playerScore = score; playerDevelopment = final; }
                if (clearWarning) WriteForce(id, value => value with {
                    DevelopmentWarningFlags = (byte)(N(value.DevelopmentWarningFlags, "warning17C") & 0xF1) });
                //The subtotal is reset after it has been included in every
                //minimum/player comparison, even if no warning will be shown.
                current.SetRulerReleasePopularity1BE(id, 0);
                WriteForce(id, value => value with { NormalAftermath = (value.NormalAftermath ?? throw Missing("development_owner")) with { Development1BE = 0 } });
            }
            if (!Valid(player) || quarterBit == 0 || playerDevelopment != minimumDevelopment && playerScore != minimumScore) return;
            int playerId = Id(player);
            byte warnings = N(Owner(playerId).DevelopmentWarningFlags, "player_warning17C");
            if ((warnings & quarterBit) != 0) return;
            WriteForce(playerId, value => value with { DevelopmentWarningFlags = (byte)(warnings | quarterBit) });
            var primary = ForcePrimaryDistrictSource.Resolve(current, player);
            if (!N(current.ReadDistrictNativeValidity(primary), "warning_primary_validity")) return;
            int speaker = Speaker();
            if (!PersonValid(speaker)) return;
            Sound(6); Conditional(speaker, Format(0x5EEC, new(Person0: PersonId(speaker))));
        }

        private void ApplyDoctrineResourceBonus(ForceReference force)
        {
            int id = Id(force);
            int contribution = N(current.ReadForce164(force), "bonus_Force164");
            int previousPopulation = N(Owner(id).PopulationTotal, "bonus_Force1C0");
            int result = Sample(100, Day, previousPopulation, contribution, ForceWord11E(force));
            bool first = result < P("doctrine-bonus-first-threshold", 50);
            int second = !first && result < P("doctrine-bonus-second-threshold", 75) ? 1 : 0;
            int doctrine = Doctrine(force);
            int kind = doctrine switch { 1 or 6 => first ? 3 : second + 1, 2 or 3 => first ? 1 : second + 2,
                4 or 5 => first ? 2 : second != 0 ? 1 : 3, _ => 0 };
            var home = RulerHome(force);
            int amount = 0, root = 0, nativeKind = 0, after = 0;
            if (kind != 0)
            {
                BaseResourceKind resource;
                if (kind == 1) { resource = BaseResourceKind.Gold; amount = P("doctrine-bonus-gold", 5000); root = 0x5EE4; nativeKind = 0x2A; }
                else if (kind == 2) { resource = BaseResourceKind.Supplies; amount = P("doctrine-bonus-supplies", 20000); root = 0x5EE5; nativeKind = 0x21; }
                else { resource = BaseResourceKind.Troops; amount = P("doctrine-bonus-troops", 3000); root = 0x5EE3; nativeKind = 0x17; }
                state = package.AddNormalTurnBaseResourceCurrent(state, current, home, resource, amount);
                after = N(current.ReadBaseResource(home, resource), "bonus_resource_after");
            }
            WriteForce(id, value => value with { Flags12 = (byte)(Flags(id) | 0x20) });
            if (IsPlayer(force) && root != 0 && nativeKind != 0)
                state = package.AppendNormalTurnFinalOrderResourceCurrent(state, id,
                    N(current.ReadBaseObjectLink(home), "bonus_Base4E"), nativeKind, root, amount, after);
        }

        private BaseReference RulerHome(ForceReference force)
        {
            //22EA10 reads Force10 directly without adding Force-validity.
            int person = Person(N(current.ReadForceRecord(Id(force))?.RawRuler10, "home_Force10"));
            return BaseLookup.Find(N(current.ReadPersonFirstLocation(person), "home_PersonE2"), current);
        }
        private int ForceWord11E(ForceReference force)
        {
            int value = N(Owner(Id(force)).FactionOfficerId, "doctrine_bonus_current_Force11E");
            return value is >= 0 and <= 65535 ? value : throw Missing("doctrine_bonus_Force11E_width");
        }
        private IEnumerable<BaseReference> Bases(ForceReference force)
        {
            foreach (var home in current.ReadRegisteredBases() ?? throw Missing("Base_registry"))
                if (home is not null && Force(home) == force) yield return home;
        }
    }
}
