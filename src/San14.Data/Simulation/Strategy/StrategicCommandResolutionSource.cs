using San14.Core;

namespace San14.Data;

internal sealed record StrategicCommandResolutionResult(GameState State, int ResultCode);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal StrategicCommandResolutionResult ResolveStrategicCommandCurrent(GameState state,
        SourceWorldContext current, PersonnelStrategicTask task)
        => new StrategicResolutionCurrent(this, state, current, task).Run();

    internal bool StrategicCommandBlockedCurrent(GameState state, SourceWorldContext current,
        int kind, int executorPhysical, int targetPhysical, int? secondForcePhysical = null)
        => new StrategicResolutionPredicates(this, state, current).Blocked(kind,
            executorPhysical, targetPhysical, secondForcePhysical);

    private sealed partial class StrategicResolutionCurrent
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly PersonnelStrategicTask task;
        private GameState State;
        private readonly List<CampaignEffectRequest> Effects = [];
        internal StrategicResolutionCurrent(ArmyCampaignRuleAdapter package, GameState state,
            SourceWorldContext current, PersonnelStrategicTask task)
        { this.package = package; this.current = current; this.task = task; State = state; }
        private int Executor => Person(task.MemberIds.Length != 0 ? task.MemberIds[0] :
            throw StrategyMissing("resolution_empty_members"));
        private int Target => task.Target.Kind == StrategicCommandTargetKind.Person ? Person(task.Target.Id) : task.Target.Id;
        private StrategicResolutionPredicates Predicates => new(package, State, current);

        internal StrategicCommandResolutionResult Run()
        {
            var pool = current.ReadOwnedCurrentListPool() ?? throw StrategyMissing("resolution_shared_pool");
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw StrategyMissing("resolution_pool_owner");
            int result = task.Kind switch
            {
                10 => ResolveDiscord(),
                11 => ResolveIndependence(),
                12 => ResolveSeparation(),
                13 => ResolvePoison(),
                14 => ResolveIncitement(),
                20 => ResolveSabotage(),
                24 => ResolveRumor(),
                _ => 0
            };
            //The original1EC0E0 tail only accumulates the platform-achievement
            //counter and unlocks its five achievement bits. It has no gameplay,
            //random or return consumer. Steam achievements are outside product
            //scope; no fabricated bitmap or World17C8 value is installed here.
            if (Effects.Count != 0)
                State = State with { CampaignEffectRequests = (State.CampaignEffectRequests
                    ?? throw StrategyMissing("resolution_pending_owner")).Append(CampaignEffectSource.StrategicCommands,
                    State.NativeMapSeason?.AdvancedCalendarSteps ?? throw StrategyMissing("resolution_calendar"), Effects) };
            return new(State, result);
        }

        private int Trial()
            => package.ResolveStrategicCommandTrialCurrent(State, current, task.Kind, Executor,
                Target, task.SecondForceId, task.MilitaryIntelligence, task.IndependenceChoice);

        private int SavedResult(int trial)
        {
            if (task.SavedResult == -1) return trial;
            int result = task.SavedResult;
            if (Predicates.Blocked(task.Kind, Executor, Target, task.SecondForceId)) result = 0;
            return result;
        }

        private int Person(int identity) => (identity <= 6000 ? current.ReadPersonLookupReference(identity)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot ?? throw StrategyMissing("resolution_Person_pointer");
        private ushort Id(int physical) => StrategyNeed(current.ReadPersonIdentity(physical), "resolution_Person10");
        private ForceReference Force(int physical) => current.ReadPersonForce(physical) ?? throw StrategyMissing("resolution_Person_Force");
        private int ForceId(ForceReference force) => StrategyNeed(current.ReadForcePhysicalSlot(force), "resolution_Force_identity");
        private ForceReference ForceAt(int physical) => current.ReadForceTableReference(physical <= 51 ? physical : 0)
            ?? throw StrategyMissing("resolution_Force_pointer");
        private int Ruler(ForceReference force) => Person(StrategyNeed(current.ReadForceRecord(ForceId(force))?.RawRuler10,
            "resolution_Force_ruler"));
        private bool Player(ForceReference force)
        {
            int player = ForceId(OwnedCurrentPlayerForce.ReadCurrentReference(State, current)
                ?? throw StrategyMissing("resolution_player"));
            return player != 0 && player == ForceId(force);
        }
        private int Ability(int person, bool trait = false)
        {
            if (!StrategyNeed(current.ReadPersonNativeValidity(person), "resolution_ability_validity")) return 0;
            var abilities = new TacticRemakeCurrentAbilities(current, package._scenario,
                package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
            int value = StrategyNeed(abilities.ResolveDimension(State, Id(person), 2,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(State, force, effect)), "resolution_Intelligence");
            return trait ? unchecked(value + unchecked(Trait(person, 65) * value * P("trait-intelligence-percent", 1)) / 100) : value;
        }
        private int Trait(int person, int effect) => StrategyNeed(
            TacticRemakeCurrentTraits.PersonContribution(current, person, effect), "resolution_Person_trait_" + effect);
        private int P(string name, int original) => StrategicParameter(State, "resolution." + name, original);
        private int RandomQuotient(int dividend, int divisor)
        {
            if (divisor == 0 || dividend == int.MinValue && divisor == -1) throw StrategyMissing("resolution_divisor");
            return App(dividend / divisor);
        }
        private int App(int bound)
        {
            var saved = State.CampaignAppRandom32 ?? throw StrategyMissing("resolution_App32");
            var random = new SharedSourceRandom32(saved.State);
            int value = random.Next(bound);
            State = State with { CampaignAppRandom32 = new(random.ReadCurrentState()) };
            return value;
        }
        private void Loyalty(int person, int after)
        {
            byte value = (byte)Math.Clamp(after, 0, 255);
            if ((StrategyNeed(current.ReadArmyReturnPersonFlags196(person), "resolution_loyalty_flags15E") & 0x2000) != 0)
                value = 255;
            current.SetPersonDestructionLoyaltyE8(person, value);
            State = ArmyDestructionLoyalty.PublishCurrentRawE8(State, current, person, value);
        }
        private int Loyalty(int person) => StrategyNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(State,
            current, person, package._governorInputs), "resolution_Person_E8");
        private void Relations(ForceReference first, ForceReference other, int change)
            => State = ForceRelationChangeCurrent.Apply(State, current, ForceId(first), ForceId(other), () => change,
                key => StrategyNeed(ArmyDestructionParameterSeed.ReadCurrent(State, key), "resolution_" + key));

    }
}
