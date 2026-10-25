using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //42BCB0→2B7500 is the special-force raid operation, not a tribute
    //negotiation. Its orders and announcements do not ask the player Yes/No.
    internal GameState ApplyNormalTurnSpecialForceDemandsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new NormalTurnSpecialForceRaidsSource(this, state, current, pool).Run();

    private sealed partial class NormalTurnSpecialForceRaidsSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        private readonly List<CampaignEffectRequest> effects = [];

        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("shared_current_pool");
            if (!AdmitRaids()) return state;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_Force");
            if (!K(current.ReadForceNativeValidity(player), "player_validity")) return state;
            var leader = Forces().FirstOrDefault(force => (Flags(force) & 1) != 0);
            if (leader is null || Ordinal(leader) is < 1 or > 51) return state;
            int enemy = Ordinal(leader), own = Ordinal(player);
            var candidates = Temporary();
            try
            {
                foreach (var force in Forces())
                {
                    int id = Ordinal(force);
                    if (id is not (>= 46 and <= 50) || K(current.ReadConditionForceDependence(new(id)), "Force194") == own) continue;
                    var home = Home(force);
                    if (!K(current.ReadBaseNativeValidity(home), "special_Home_validity")) return state;
                    var people = Temporary();
                    try
                    {
                        var excluded = Temporary();
                        try
                        {
                            FillHealthyHomePeople(home, people, excluded);
                            if (Count(people) < P("minimum-available-officers", 6)) continue;
                            if (K(current.ReadBaseResource(home, BaseResourceKind.Troops), "special_Home3C") < P("minimum-home-troops", 150000)) continue;
                            var relation = state.NativeArmySimulation?.Forces.GetValueOrDefault(id)?.NormalAftermath?.Relations13
                                is { Length: 52 } values ? values : throw Missing("special_Relations13");
                            int friendship = own < 52 ? relation[own] : 0;
                            if (friendship >= P("maximum-player-friendship", 50)) continue;
                            var neighbors = Temporary();
                            try
                            {
                                FillNeighbors(home, neighbors);
                                if (Bases(neighbors).Any(IsPlayerBase)) Append(candidates, home);
                            }
                            finally { Release(neighbors); }
                        }
                        finally { Release(excluded); }
                    }
                    finally { Release(people); }
                }
                if (Count(candidates) == 0) return state;
                var targets = Temporary();
                try
                {
                    foreach (var entry in current.ReadRegisteredBases() ?? throw Missing("Base80_registry"))
                    {
                        var at = entry ?? throw Missing("Base80_pointer");
                        if (!IsPlayerBase(at)) continue;
                        var neighbors = Temporary();
                        try
                        {
                            FillNeighbors(at, neighbors);
                            if (Bases(neighbors).Any(other => K(current.ReadBaseForceSlot(other), "target_neighbor_Force") == enemy))
                                Append(targets, at);
                        }
                        finally { Release(neighbors); }
                    }
                    if (Count(targets) == 0) return state;
                    foreach (var home in Bases(candidates))
                    {
                        if (Count(targets) == 0) return state;
                        if (Count(targets) >= 2) SortTargets(targets, home);
                        var target = Bases(targets).First();
                        var people = Temporary();
                        try
                        {
                            var excluded = Temporary();
                            try
                            {
                                FillHealthyHomePeople(home, people, excluded);
                                if (Count(people) == 0) return state;
                                if (Count(people) >= 2) SortCommanders(people);
                                int made = 0;
                                foreach (int person in Persons(people))
                                {
                                    if (made >= P("maximum-armies-per-force", 5)) break;
                                    int force = K(current.ReadBaseForceSlot(home), "raid_Force");
                                    if (force is not (>= 46 and <= 50)) continue;
                                    //CUnitData: commander,20000 troops, land14,
                                    //naval11, special tactic131..135. The target
                                    //packet independently selects City4/Gate5.
                                    state = LaunchRaidCurrent(person, home, target,
                                        P("army-troops", 20000), 14, 11, force + 85);
                                    made++;
                                }
                            }
                            finally { Release(excluded); }
                        }
                        finally { Release(people); }
                    }
                    PublishRaids(player, leader, candidates);
                }
                finally { Release(targets); }
            }
            finally { Release(candidates); }
            return state;
        }

        private bool AdmitRaids()
        {
            if (Setting(0x374, 0xF8) != 0 || Setting(0x3A4, 0x100) != 0) return false;
            var now = state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");
            var cooldown = state.NativeArmySimulation?.World.PlayerCoalitionCooldownDate ?? throw Missing("World17BC_date");
            if (DateValue(cooldown) > DateValue(now)) return false;
            if ((K(current.ReadWorldTransferServiceFlags(), "World16A8") & 0x800000) != 0) return false;
            foreach (var force in Forces())
            {
                if ((Flags(force) & 1) == 0) continue;
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("gate_player_Force");
                var sampler = package.ReadArmyDestructionSampler(state, current) ?? throw Missing("gate_sampler");
                int sample = TacticEffectSourceSamplerMath.Sample(100, 1, Ordinal(player),
                    unchecked(now.Year + now.Month + now.Day), 0, 0, sampler);
                int difficulty = Setting(0x134, 0x28);
                int probability = difficulty switch { <= 1 => P("probability-easy-normal", 5),
                    2 => P("probability-hard", 6), 3 => P("probability-very-hard", 8), _ => P("probability-super", 10) };
                if (probability < sample) return false;
                int id = Ordinal(force);
                foreach (var entry in current.ReadRegisteredBases() ?? throw Missing("gate_Base80_registry"))
                {
                    var at = entry ?? throw Missing("gate_Base80_pointer");
                    if (K(current.ReadBaseForceSlot(at), "gate_Base_Force") != id) continue;
                    var neighbors = Temporary();
                    try
                    {
                        FillNeighbors(at, neighbors);
                        if (Bases(neighbors).Any(IsPlayerBase)) return true;
                    }
                    finally { Release(neighbors); }
                }
                return false;
            }
            return false;
        }

        private GameState LaunchRaidCurrent(int person, BaseReference home, BaseReference target,
            int troops, int landFormation, int navalFormation, int tactic)
            => package.CreateSpecialForceRaidArmyCurrent(state, current, pool, person,
                home, target, troops, landFormation, navalFormation, tactic);

        private int Setting(int advanced, int ordinary) => K(BattleRemakeCurrentSettings.ReadInt32(state,
            K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector") == 0 ? advanced : ordinary), "setting");
        private int P(string name, int original)
        {
            if (package.Seed.Language != "TC") throw Missing("locale_static_rules");
            return StrategicParameter(state, "special-force-raids." + name, original);
        }
        private static long DateValue(NativeMapSeasonDate date) =>
            (long)date.Year * 8640 + (date.Month - 1) * 720 + (date.Day - 1) * 24 + date.Hour;
        private int Ordinal(ForceReference force) => K(current.ReadForcePhysicalSlot(force), "Force_physical");
        private ushort Flags(ForceReference force) => state.NativeArmySimulation?.Forces.GetValueOrDefault(Ordinal(force))?.DiplomacyFlags178
            ?? throw Missing("Force178");
        private IEnumerable<ForceReference> Forces()
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("ForceC0_registry"))
                yield return entry is { Kind: RegistryKind.Force, PhysicalSlot: int id } ? new(id) : throw Missing("ForceC0_payload");
        }
        private int Person(int id) => K((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        private int Identity(int person) => K(current.ReadPersonIdentity(person), "Person10");
        private BaseReference Home(ForceReference force) => BaseLookup.Find(K(current.ReadPersonFirstLocation(
            Person(K(current.ReadForceRecord(Ordinal(force))?.RawRuler10, "Force10"))), "ruler_E2"), current);
        private bool IsPlayerBase(BaseReference at)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("Base_player_Force");
            if (!K(current.ReadForceNativeValidity(player), "Base_player_validity")) return false;
            int raw = K(current.ReadBaseForceSlot(at), "Base_Force");
            return current.ReadForceTableReference(raw < 52 ? raw : 0) == player;
        }
        private void FillNeighbors(BaseReference at, HexListHandle output) =>
            (package._captiveFreedomHomes ??= new(package._scenario)).FillBases22F720(current, at, output);
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal-turn.special-force-raids." + field);
    }
}
