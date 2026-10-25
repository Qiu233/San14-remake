using San14.Core;

namespace San14.Data;

internal sealed record StrategicCommandCandidate(int Id, string Name, int? ForceId, int? HomeId, int? HexId)
{
    internal int PhysicalSlot { get; init; }
    internal bool? Enabled { get; init; }
    internal bool? Recommended { get; init; }
    internal int? TravelDays { get; init; }
    internal string? DisabledReason { get; init; }
    internal string? UnresolvedOwner { get; init; }
}

internal sealed record StrategicCommandRules(GameState State, SourceWorldContext Current,
    int ForceId, int Kind, int DistrictId, int Books, int Gold, int BookCost, int BaseGoldCost,
    IReadOnlyList<StrategicCommandCandidate> Executors, IReadOnlyList<StrategicCommandCandidate> Targets)
{
    internal int GoldCost { get; init; }
    internal int? MilitaryIntelligence { get; init; }
    internal int? MinimumMilitaryIntelligence { get; init; }
    internal int? MaximumMilitaryIntelligence { get; init; }
    internal int? MilitaryIntelligenceStep { get; init; }
    internal IReadOnlyList<StrategicCommandCandidate> SecondForces { get; init; } = [];
    internal Dictionary<(int? Target,int? Second),IReadOnlyList<StrategicCommandCandidate>> ExecutorViews { get; } = [];
}

internal sealed record StrategicCommandSelection(int? GoldCost, int? ChancePercent,
    IReadOnlyList<StrategicCommandPreviewEffect> Effects, string? UnresolvedOwner)
{
    internal int? RequiredMilitaryIntelligence { get; init; }
    internal int? MinimumMilitaryIntelligence { get; init; }
    internal int? MaximumMilitaryIntelligence { get; init; }
    internal int? MilitaryIntelligenceStep { get; init; }
    internal int? TravelDays { get; init; }
    internal IReadOnlyList<StrategicCommandCandidate> Executors { get; init; } = [];
    internal IReadOnlyList<StrategicCommandCandidate> SecondForces { get; init; } = [];
    internal StrategicCommandTargetDetails TargetDetails { get; init; } = new(null,null,null,null,null);
    internal string? Guide { get; init; }
    internal int? AdvisorOfficerId { get; init; }
    internal bool? CanContinue { get; init; }
    internal string? AdviceText { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private const string StrategicParameterPrefix = "simulation.strategic-command.";

    internal StrategicCommandRules CaptureStrategicCommandRules(GameState state,
        SourceWorldContext current, int forceId, int kind)
    {
        RequireStrategicKind(kind);
        var force = current.ReadForceTableReference(forceId) ?? throw StrategyMissing("Force_pointer");
        if (!StrategyNeed(current.ReadForceNativeValidity(force), "Force_validity") || force.PhysicalSlot != forceId)
            throw StrategyMissing("Force_control");
        int district = StrategyNeed(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "primary_District");
        var owner = state.Districts.GetValueOrDefault(district) ?? throw StrategyMissing("District_owner");
        int books = owner.OrdersRemaining ?? throw StrategyMissing("District14");
        int gold = BaseCurrencyAdditionSource.ReadCurrentGold(ReadPersonnelHireTreasuryCurrent(current, district), current);
        int intelligence = ReadDiplomacyDistrictBudget(current, district);
        var executors = ReadStrategicExecutorsCurrent(state,current,forceId,district);
        var targets = ReadStrategicTargetsCurrent(state,current,forceId,kind);
        int baseGold = ReadStrategicBaseGoldCurrent(state,kind);
        return new(state,current,forceId,kind,district,books,gold,
            StrategicParameter(state,$"kind-{kind}.command-books",1),baseGold,executors,targets)
        {
            GoldCost = ReadStrategicGoldCostCurrent(state,current,forceId,kind),
            MilitaryIntelligence = intelligence, MinimumMilitaryIntelligence = 0,
            MaximumMilitaryIntelligence = intelligence, MilitaryIntelligenceStep = 1,
            SecondForces = kind == 10 ? ReadStrategicForceTargetsCurrent(state,current,forceId,null) : []
        };
    }

    internal StrategicCommandSelection QueryStrategicCommandSelectionCurrent(StrategicCommandRules rules,
        int? executorId, int? targetId, int? secondForceId, int military, int independence)
    {
        var second = rules.Kind == 10
            ? ReadStrategicForceTargetsCurrent(rules.State,rules.Current,rules.ForceId,targetId)
            : Array.Empty<StrategicCommandCandidate>();
        if(!rules.ExecutorViews.TryGetValue((targetId,secondForceId),out var executors))
            rules.ExecutorViews[(targetId,secondForceId)] = executors = ReadStrategicExecutorRecommendationsCurrent(rules,targetId,secondForceId);
        var details = ReadStrategicTargetDetailsCurrent(rules.State,rules.Current,rules.Kind,targetId,secondForceId);
        var result = new StrategicCommandSelection(rules.GoldCost,null,[],null)
        {
            Executors = executors, SecondForces = second, TargetDetails = details,
            AdvisorOfficerId = ReadStrategicAdvisorIdentityCurrent(rules),
            MinimumMilitaryIntelligence = rules.MinimumMilitaryIntelligence,
            MaximumMilitaryIntelligence = rules.MaximumMilitaryIntelligence,
            MilitaryIntelligenceStep = rules.MilitaryIntelligenceStep
        };
        if (executorId is null || targetId is null || rules.Kind == 10 && secondForceId is null) return result;
        var executor = rules.Executors.FirstOrDefault(row => row.Id == executorId)
            ?? throw StrategyMissing("selected_executor_not_candidate");
        var target = rules.Targets.FirstOrDefault(row => row.Id == targetId)
            ?? throw StrategyMissing("selected_target_not_candidate");
        if (executor.Enabled != true || target.Enabled != true) return result with
            { UnresolvedOwner = executor.UnresolvedOwner ?? target.UnresolvedOwner ?? "strategic-command.selected_disabled" };
        int? secondPhysical = null;
        if (rules.Kind == 10)
            secondPhysical = (second.FirstOrDefault(row => row.Id == secondForceId)
                ?? throw StrategyMissing("selected_second_Force_not_candidate")).PhysicalSlot;
        if (military < 0 || rules.MilitaryIntelligence is not int available || military > available) throw StrategyMissing("military_amount");
        if (rules.Kind == 11 && independence is not (0 or 10 or 30)) throw StrategyMissing("independence_choice");
        try
        {
            var numeric = QueryStrategicCommandNumericCurrent(rules.State,rules.Current,rules.Kind,
                executor.PhysicalSlot,target.PhysicalSlot,secondPhysical,military,independence);
            int required = ReadStrategicRequiredMilitaryCurrent(rules.State,rules.Current,rules.Kind,
                executor.PhysicalSlot,target.PhysicalSlot,secondPhysical,independence);
            return result with { ChancePercent = numeric.ChancePercent, Effects = numeric.Effects,
                RequiredMilitaryIntelligence = required,
                MaximumMilitaryIntelligence = Math.Min(rules.MilitaryIntelligence!.Value,required),
                TravelDays = StrategicCommandTravelSource.Resolve(rules.State,rules.Current,executor.PhysicalSlot,
                    new(rules.Kind switch {10=>StrategicCommandTargetKind.Force,11=>StrategicCommandTargetKind.City,
                        12 or 24=>StrategicCommandTargetKind.Person,_=>StrategicCommandTargetKind.Area},target.Id),
                    secondForceId, rules.State.NativeMapSeason?.ThirdDate ?? throw StrategyMissing("preview_date")).TravelDays,
                CanContinue = ReadStrategicCanContinueCurrent(rules) };
        }
        catch (InvalidDataException error) { return result with { UnresolvedOwner = error.Message }; }
    }

    //6D5270 and6DDA00 share their actual rule definitions with submission.
    internal int ReadStrategicGoldCostCurrent(GameState state, SourceWorldContext current, int forceId, int kind)
    {
        var force = current.ReadForceTableReference(forceId) ?? throw StrategyMissing("cost_Force_pointer");
        var advantage = PersonReturnPathSource.ReadForceAdvantage(state,current,force,11,100);
        int multiplier = advantage.Value ?? throw StrategyMissing(advantage.UnresolvedOwner ?? "cost_State_advantage11");
        int denominator = StrategicParameter(state,"gold-cost-percent-denominator",100);
        if (denominator == 0) throw StrategyMissing("cost_percent_denominator");
        //The actual IMUL is Int32 before signed division.
        return unchecked(multiplier * ReadStrategicBaseGoldCurrent(state,kind)) / denominator;
    }

    private static int ReadStrategicBaseGoldCurrent(GameState state, int kind) => StrategicParameter(state,
        $"kind-{kind}.base-gold",kind switch { 10 or 11 => 900,12 or 20 => 500,13 => 800,14 => 300,24 => 1000,
            _ => throw StrategyMissing("unsupported_kind") });

    private static int StrategicParameter(GameState state, string name, int original)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw StrategyMissing("parameter_owner");
        string key = StrategicParameterPrefix + name;
        if (parameters.Float32Values.ContainsKey(key)) throw StrategyMissing("parameter_type_" + name);
        return parameters.Int32Values.TryGetValue(key,out int value) ? value : original;
    }

    private static void RequireStrategicKind(int kind)
    { if (kind is not (10 or 11 or 12 or 13 or 14 or 20 or 24)) throw StrategyMissing("unsupported_kind"); }

    private StrategicCommandTargetDetails ReadStrategicTargetDetailsCurrent(GameState state,
        SourceWorldContext current, int kind, int? target, int? secondForce)
    {
        if (target is null) return new(null,null,null,null,null);
        int? owner = null, loyalty = null; string? governor = null,homeName = null;
        if (kind is 13 or 14 or 20) owner = StrategyNeed(current.ReadAreaCurrentForceSlot(new(target)),"details_Area_Force");
        else if (kind == 11)
        {
            var city = current.ReadCityTableReference(target.Value) ?? throw StrategyMissing("details_City_pointer");
            owner = StrategyNeed(current.ReadBaseForceSlot(city),"details_City_Force");
            int id = StrategyNeed(current.ReadArmyReturnBaseGovernor2E(city),"details_City_governor");
            governor = state.Officers.GetValueOrDefault(id)?.Name;
        }
        else if (kind is 12 or 24)
        {
            var person = state.Officers.GetValueOrDefault(target.Value) ?? throw StrategyMissing("details_Person_owner");
            owner = person.ForceId;
            if (kind == 12) loyalty = person.Loyalty;
            else
            {
                int physical=current.ResolveOfficerSource(target.Value) ?? throw StrategyMissing("details_Person_pointer");
                var home=BaseLookup.Find(StrategyNeed(current.ReadPersonFirstLocation(physical),"details_Person_E2"),current);
                if (StrategyNeed(current.ReadBaseNativeValidity(home),"details_Base_validity"))
                    homeName=home.Kind==BaseKind.City ? state.Cities.GetValueOrDefault(StrategyNeed(home.PhysicalSlot,"details_City"))?.Name
                        : state.Gates.GetValueOrDefault(StrategyNeed(home.PhysicalSlot,"details_Gate")+51)?.Name;
            }
        }
        else if (kind == 10) owner = target;
        return new(owner is int force ? state.Forces.GetValueOrDefault(force)?.Name : null,governor,loyalty,null,null) { HomeLocationName=homeName };
    }

    private int? ReadStrategicAdvisorIdentityCurrent(StrategicCommandRules rules)
    {
        int raw=StrategyNeed(rules.Current.ReadAssignmentForceAdvisor52(rules.ForceId),"adviser_Force52");
        var person=rules.Current.ReadPersonLookupReference(raw<=6000?raw:0) ?? throw StrategyMissing("adviser_pointer");
        int physical=StrategyNeed(person.PhysicalSlot,"adviser_physical");
        return StrategyNeed(rules.Current.ReadPersonNativeValidity(physical),"adviser_validity")
            ? StrategyNeed(rules.Current.ReadPersonIdentity(physical),"adviser_identity") : null;
    }

    private bool ReadStrategicCanContinueCurrent(StrategicCommandRules rules)
    {
        //7911F0/1440/1800/1A40 evaluate the next command against the
        //opening budget. Rumor uses6BE260(threshold2),6BEEB0(threshold1).
        if(rules.Books-rules.BookCost<=0 || rules.Gold-rules.GoldCost<rules.GoldCost)return false;
        if(rules.Kind!=24)return rules.Executors.Count-1>0;
        int minimum=StrategicParameter(rules.State,"rumor.minimum-executors",1)+1;
        int targets=StrategicParameter(rules.State,"rumor.minimum-targets",1);
        return rules.Executors.Count>=minimum && rules.Targets.Count>=targets;
    }

    private static T StrategyNeed<T>(T? value,string owner) where T:struct => value ?? throw StrategyMissing(owner);
    private static InvalidDataException StrategyMissing(string owner) => new("strategic-command." + owner);
}
