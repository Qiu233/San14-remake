using San14.Core;
using System.Globalization;

namespace San14.Data;

/// <summary>The real doctrine-level scene operands. This is neither a
/// chronology record nor a receipt that its announcement or choice was shown.</summary>
internal sealed record ForcePolicyLevelUpSourceOperands(int ForceId, int PreviousLevel,
    int CurrentLevel, int AdditionalPolicySlots, int AdditionalCostLimit);

public sealed partial class ArmyCampaignRuleAdapter : IForceExperiencePresentationRules
{
    public GameState CompleteExperiencePresentation(GameState state,
        TalentForceExperiencePresentationRequest request, int previousDisplayLevel)
    {
        if (request.Operation != TalentForceExperiencePresentationOperation.ExperienceGain ||
            request.ForceId is < 1 or > 51 || request.Amount <= 0 || previousDisplayLevel is < 0 or > 10)
            throw PolicyLevelMissing("experience_completion_operand");
        var current = CaptureWritableTransportRouteCurrent(state);
        _force164Inputs.BindCurrent(state, current);
        var force = current.ReadForceTableReference(request.ForceId)
            ?? throw PolicyLevelMissing("experience_Force_pointer");
        int value = PolicyLevelNeed(current.ReadForce164(force), "experience_current_value");
        int level = PolicyLevelNeed(OwnedForce164Inputs.ReadLevel(state, value), "experience_current_level");
        //8977A0 reaches36D0D0 only after increasing the displayed level to
        //the target. The caller supplies that actual old display operand.
        if (level <= previousDisplayLevel) return state;
        var operands = CaptureForcePolicyLevelUpCurrent(state, current, force, previousDisplayLevel);
        if (operands is null) return state;
        var announcement = FormatPolicyLevelUpMessageCurrent(state, current, 0x57EE,
            new(Integers: [operands.AdditionalPolicySlots, operands.AdditionalCostLimit]));
        var confirmation = FormatPolicyLevelUpMessageCurrent(announcement.State, current, 0x3163, new());
        var scene = new ForcePolicyLevelUpRequest(operands.ForceId, operands.PreviousLevel,
            operands.CurrentLevel, operands.AdditionalPolicySlots, operands.AdditionalCostLimit,
            announcement.Text, confirmation.Text);
        return AppendPolicyLevelRequests(confirmation.State,
            [new(null, null) { ForcePolicyLevelUp = scene }]);
    }

    public ForcePolicyLevelUpResult AdvancePolicyLevelUp(GameState state,
        ForcePolicyLevelUpRequest request, ForcePolicyLevelUpInput input)
    {
        if (request.ForceId is < 1 or > 51 || !Enum.IsDefined(request.Stage) || !Enum.IsDefined(input.Kind))
            throw PolicyLevelMissing("scene_input");
        if (request.Stage == ForcePolicyLevelUpStage.Announcement &&
            input.Kind == ForcePolicyLevelUpInputKind.AnnouncementCompleted)
        {
            bool available = input.PolicyEditorAvailable ?? throw PolicyLevelMissing("actual_strategy_host_presence");
            return available ? new(state, request with { Stage = ForcePolicyLevelUpStage.PolicyDecision })
                : new(FinishPolicyLevelUpCurrent(state, request), null);
        }
        if (request.Stage == ForcePolicyLevelUpStage.PolicyDecision)
        {
            if (input.Kind == ForcePolicyLevelUpInputKind.OpenPolicy)
                return new(state, request with { Stage = ForcePolicyLevelUpStage.PolicyEditor });
            if (input.Kind == ForcePolicyLevelUpInputKind.DeclinePolicy)
                return new(FinishPolicyLevelUpCurrent(state, request), null);
        }
        if (request.Stage == ForcePolicyLevelUpStage.PolicyEditor &&
            input.Kind == ForcePolicyLevelUpInputKind.PolicyEditorReturned)
            return new(FinishPolicyLevelUpCurrent(state, request), null);
        throw PolicyLevelMissing("scene_input_stage");
    }

    private GameState FinishPolicyLevelUpCurrent(GameState state, ForcePolicyLevelUpRequest request)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        _force164Inputs.BindCurrent(state, current);
        var force = current.ReadForceTableReference(request.ForceId)
            ?? throw PolicyLevelMissing("history_Force_pointer");
        int ordinal = PolicyLevelNeed(current.ReadForcePhysicalSlot(force), "history_Force60");
        var main = FormatPolicyLevelUpMessageCurrent(state, current, 0x3162, new(Force0: ordinal));
        int ruler = TalentRulerCurrent(current, force);
        var attachment = FormatPolicyLevelUpMessageCurrent(main.State, current, 0x3155, new(Person0: ruler));
        int payload = TalentRulerCurrent(current, force);
        int day = (ReadArmyDestructionSampler(attachment.State, current)
            ?? throw PolicyLevelMissing("history_day450")).Day450;
        var primary = ForcePrimaryDistrictSource.Resolve(current, force);
        int district = PolicyLevelNeed(current.ReadDistrictPhysicalSlot(primary), "history_primary_District60");
        var effects = new List<CampaignEffectRequest>();
        var working = WriteHistory(attachment.State, current,
            new(5, district, 0, day, main.Text, attachment.Text, payload, MarkBit0: 1), effects);
        return AppendPolicyLevelRequests(working, effects);
    }

    private static GameState AppendPolicyLevelRequests(GameState state, IReadOnlyList<CampaignEffectRequest> effects)
    {
        if (effects.Count == 0) return state;
        long cycle = state.NativeMapSeason?.AdvancedCalendarSteps ?? throw PolicyLevelMissing("calendar_owner");
        var pending = state.CampaignEffectRequests ?? throw PolicyLevelMissing("pending_owner");
        return state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.ForcePolicyLevelUp, cycle, effects) };
    }

    private HistoryTextResult FormatPolicyLevelUpMessageCurrent(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands)
    {
        var latest = state;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var people = new DeploymentNarrationCurrentPeople(state, current, abilities, operands.Person0, operands.Person1,
            panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(latest, force, effect),
            appearanceRepository: _repository, appearanceScenario: _scenario, appearanceLanguage: Seed.Language);
        var reports = new ReportCurrent(this, () => latest, current, people, operands);
        var context = new PolicyLevelMessageCurrent(reports, () => latest, current, operands);
        int Draw(int bound)
        {
            if (bound < 2) return 0;
            var saved = latest.CampaignAppRandom32 ?? throw PolicyLevelMissing("App32_owner");
            var random = new SharedSourceRandom32(saved.State);
            int value = random.Next(bound);
            latest = latest with { CampaignAppRandom32 = new(random.State) };
            return value;
        }
        var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, San14.Data.Formats.MessageKind.Game).Messages);
        string text = vm.ResolveTalentReport(context, Draw, root);
        return new(latest, text);
    }

    //These two additional operands belong to the policy-level messages, not
    //a new general resource VM. All ordinary report fields use the same owner.
    private sealed class PolicyLevelMessageCurrent(INarrationCurrentContext inner, Func<GameState> latest,
        SourceWorldContext current, HistoryMessageOperands operands) : INarrationCurrentContext
    {
        public int PersonValue(int argument, int field)
        {
            if (field != 'r') return inner.PersonValue(argument, field);
            int id = inner.PersonValue(argument, '2');
            if (id == 0) return 0;
            var person = latest().Officers.GetValueOrDefault(id) ?? throw PolicyLevelMissing("r_Person_owner");
            int raw = person.DoctrineId ?? person.AbilityState?.ObservedDoctrineId ?? throw PolicyLevelMissing("r_Person14C");
            return (uint)raw <= 255 ? raw : throw PolicyLevelMissing("r_Person14C_width");
        }
        public string ForceText(int argument, int field)
        {
            if (field != 'A') return inner.ForceText(argument, field);
            if (argument != 0) throw PolicyLevelMissing("A_Force_argument");
            int raw = operands.Force0 ?? throw PolicyLevelMissing("A_Force_operand");
            var force = current.ReadForceTableReference(raw is >= 0 and < 52 ? raw : 0)
                ?? throw PolicyLevelMissing("A_Force_pointer");
            if (!PolicyLevelNeed(current.ReadForceNativeValidity(force), "A_Force_validity")) return string.Empty;
            int at = PolicyLevelNeed(current.ReadForcePhysicalSlot(force), "A_Force60");
            if (at is >= 46 and <= 50) return string.Empty;
            int value = PolicyLevelNeed(current.ReadForce164(force), "A_Force164");
            int level = PolicyLevelNeed(OwnedForce164Inputs.ReadLevel(latest(), value), "A_Force_level");
            return level.ToString(CultureInfo.InvariantCulture);
        }
        public string PersonText(int a, int f) => inner.PersonText(a, f);
        public int Relationship(int f, int a, int b) => inner.Relationship(f, a, b);
        public int Personality(int a, Func<int> f) => inner.Personality(a, f);
        public string AreaText(int a, int f) => inner.AreaText(a, f);
        public int ArmyValue(int a, int f) => inner.ArmyValue(a, f);
        public string ArmyText(int a, int f) => inner.ArmyText(a, f);
        public int StringValue(int a, int f) => inner.StringValue(a, f);
        public string StringText(int a, int f) => inner.StringText(a, f);
        public int IntegerValue(int a) => inner.IntegerValue(a);
        public int CityValue(int a, int f) => inner.CityValue(a, f);
        public int BaseArgumentValidity(int a) => inner.BaseArgumentValidity(a);
        public string CityText(int a, int f) => inner.CityText(a, f);
        public int ForceValue(int a, int f) => inner.ForceValue(a, f);
    }

    /// <summary>36D0D0 admission followed by the numeric operands consumed by
    /// CDoctrineLvUpStagingCmd. The caller owns the existing presentation queue
    /// and invokes this at the completed experience-header transition.</summary>
    internal ForcePolicyLevelUpSourceOperands? CaptureForcePolicyLevelUpCurrent(GameState state,
        SourceWorldContext current, ForceReference force, int previousLevel)
    {
        //The actual gate order is Force nativevalid, player, current level>1.
        if (!PolicyLevelNeed(current.ReadForceNativeValidity(force), "Force_validity")) return null;
        if (!TalentForceIsPlayerCurrent(state, current, force)) return null;
        int experience = PolicyLevelNeed(current.ReadForce164(force), "Force_experience");
        int level = PolicyLevelNeed(OwnedForce164Inputs.ReadLevel(state, experience), "current_level");
        if (level <= 1) return null;

        int physical = PolicyLevelNeed(current.ReadForcePhysicalSlot(force), "Force_identity");
        if (previousLevel is < 0 or > 10 || level > 10)
            throw PolicyLevelMissing("level_operand");

        //The normal resource repository already owns the immutable ExCe table.
        //No original executable, current-process value or new raw bank is used.
        var experienceData = _repository.LoadExperienceData();
        if (state.ContentHashes.GetValueOrDefault(experienceData.Source.ResourceUri) != experienceData.Source.Sha256)
            throw PolicyLevelMissing("experience_resource_identity");
        var rows = experienceData.Doctrine.Records;
        if (rows.Count != 11) throw PolicyLevelMissing("experience_table_profile");
        var before = rows[previousLevel];
        var after = rows[level];
        //36E0F0 phase2 reads242990(byte14) then242860(byte15), and formats
        //Game57EE with these signed differences as Integer0/Integer1.
        int slots = after.RawAdditionalSlots - before.RawAdditionalSlots;
        int cost = after.RawCostLimitAddition - before.RawCostLimitAddition;
        return new(physical, previousLevel, level, slots, cost);
    }

    private static T PolicyLevelNeed<T>(T? value, string field) where T : struct
        => value ?? throw PolicyLevelMissing(field);
    private static InvalidDataException PolicyLevelMissing(string field)
        => new("force.policy-level-up." + field);
}
