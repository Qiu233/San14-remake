using San14.Core;

namespace San14.Data;

internal sealed record StrategicCommandAdviceResult(GameState State, string? AdviceText,
    int? AdvisorOfficerId, string? Guide, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    //79BDE0/79BFD0/79C200/79C3D0 call2F39D0 for the four target shapes.
    //792ED0 only enables the second-Force picker; it does not produce advice.
    //Call this on an accepted selection input and retain the result. The
    //source sampler is pure, but196CC0's real Function3 formatting can draw
    //App32, so rendering and formal command validation must not call it.
    internal StrategicCommandAdviceResult ReadStrategicCommandAdviceCurrent(GameState state,
        SourceWorldContext current, int forceId, int kind, int executorPhysical, int targetPhysical,
        int? secondForcePhysical, int military, int independence)
    {
        //22EAC0's Force0 result is the source's neutral speaker identity.
        const int unaffiliatedRulerIdentity = 5014;
        try
        {
            RequireStrategicKind(kind);
            var force = current.ReadForceTableReference(forceId < 52 ? forceId : 0)
                ?? throw StrategyMissing("advice_Force_pointer");
            int advisor = Lookup(StrategyNeed(current.ReadAssignmentForceAdvisor52(ForceId(force)), "advice_Force52"));
            if (!Valid(advisor)) return new(state, null, null, null, null);
            int accuracy = 0;
            if (StrategyNeed(current.ReadForceNativeValidity(force), "advice_Force_validity"))
            {
                int fresh = Lookup(StrategyNeed(current.ReadAssignmentForceAdvisor52(ForceId(force)), "advice_fresh_Force52"));
                if (Valid(fresh))
                {
                    var abilities = SearchAbilities(current);
                    int intelligence = StrategyNeed(abilities.ResolveDimension(state, Identity(fresh), 2,
                        (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)), "advice_Advisor_INT");
                    accuracy = Math.Clamp(unchecked(60 + Math.Max(0, intelligence - 70) + Math.Max(0, intelligence - 90)), 0, 100);
                }
            }
            int executorId = Identity(executorPhysical);
            int targetSalt = kind is 12 or 24 ? Identity(targetPhysical) : targetPhysical;
            var sampler = ReadArmyDestructionSampler(state, current) ?? throw StrategyMissing("advice_sampler450_45C");
            bool accurate = TacticEffectSourceSamplerMath.Sample(100, 10, executorId, targetSalt, 0, 0, sampler) < accuracy;
            int advisorId = Identity(advisor), ownRuler = RulerIdentity(PersonForce(executorPhysical));
            int root;
            HistoryMessageOperands operands;

            //2F39D0's two user-selection branches ask a zero-military trial.
            //Ordinary campaigns use the separately cached numeric chance,
            //including its military amount, and the2B5310 message bands.
            if ((kind is 13 or 20) && StrategyNeed(ReadApplicationInformationRestriction(state), "advice_information_service_mode"))
            {
                bool succeeds = ResolveStrategicCommandTrialCurrent(state, current, kind, executorPhysical,
                    targetPhysical, secondForcePhysical, 0, independence) != 0;
                bool positive = succeeds == accurate;
                root = kind == 13 ? positive ? 0x2E28 : 0x2E38 : positive ? 0x3326 : 0x3336;
                operands = new(Person0: advisorId, Person1: ownRuler, Person2: executorId, Area0: targetPhysical);
                if (kind == 20) operands = operands with { Person3 = RulerIdentity(AreaForce(targetPhysical)) };
            }
            else if ((kind is 11 or 12) && StrategicCommandBlockedCurrent(state, current, kind,
                executorPhysical, targetPhysical, secondForcePhysical))
            {
                root = 0x3475;
                if (kind == 11)
                {
                    var city = City(targetPhysical);
                    operands = new(Person0: advisorId, Person1: ownRuler,
                        Person2: StrategyNeed(current.ReadArmyReturnBaseGovernor2E(city), "advice_City_governor2E"),
                        Person3: RulerIdentity(BaseForce(city)));
                }
                else operands = new(Person0: advisorId, Person1: ownRuler,
                    Person2: Identity(targetPhysical), Person3: RulerIdentity(PersonForce(targetPhysical)));
            }
            else if (kind == 13 && StrategicCommandBlockedCurrent(state, current, kind,
                executorPhysical, targetPhysical, secondForcePhysical))
            {
                root = 0x346F;
                operands = new(Person0: advisorId, Area0: targetPhysical);
            }
            else
            {
                int chance = QueryStrategicCommandNumericCurrent(state, current, kind, executorPhysical,
                    targetPhysical, secondForcePhysical, military, independence).ChancePercent;
                if (chance != 100)
                {
                    //2B5310 has no mutable random draw. Its second argument is
                    //the cached chance in EDX, omitted by the decompiler at
                    //the2F3E59/2F40AA callsites.
                    root = chance switch { 0 => 0x347B, >= 1 and <= 50 => 0x3481,
                        >= 51 and <= 80 => 0x3487, >= 81 and <= 99 => 0x348D,
                        _ => throw StrategyMissing("advice_chance_band") };
                    operands = new(Person0: advisorId, Person1: ownRuler);
                }
                else
                {
                    operands = new(Person0: advisorId, Person1: ownRuler, Person2: executorId);
                    switch (kind)
                    {
                        case 10:
                            root = 0x2D9E;
                            operands = operands with { Person3 = RulerIdentity(ForceAt(targetPhysical)),
                                Person4 = RulerIdentity(ForceAt(secondForcePhysical ?? throw StrategyMissing("advice_second_Force"))) };
                            break;
                        case 11:
                            root = 0x2DBE;
                            var city = City(targetPhysical);
                            operands = operands with { Person3 = StrategyNeed(current.ReadArmyReturnBaseGovernor2E(city), "advice_City_governor2E"),
                                Base0 = StrategyNeed(current.ReadBaseObjectLink(city), "advice_City4E") };
                            break;
                        case 12:
                            root = 0x2DED;
                            operands = operands with { Person3 = Identity(targetPhysical), Person4 = RulerIdentity(PersonForce(targetPhysical)) };
                            break;
                        case 24:
                            root = 0x3429;
                            var targetForce = PersonForce(targetPhysical);
                            int related = StrategyNeed(current.ReadForceNativeValidity(targetForce), "advice_related_Force_validity")
                                ? Lookup(StrategyNeed(current.ReadForceRecord(ForceId(targetForce))?.RawRuler10, "advice_related_Force10")) : Lookup(0);
                            operands = operands with { Person3 = Identity(targetPhysical), Person4 = Identity(related) };
                            break;
                        default:
                            root = kind switch { 13 => 0x2E28, 14 => 0x2E48, 20 => 0x3326,
                                _ => throw StrategyMissing("advice_kind") };
                            operands = operands with { Person3 = RulerIdentity(AreaForce(targetPhysical)), Area0 = targetPhysical };
                            break;
                    }
                }
            }
            var formatted = FormatStrategicCommandReportCurrent(state, current, root, operands);
            return new(formatted.State, formatted.Text, string.IsNullOrEmpty(formatted.Text) ? null : advisorId, null, null);
        }
        catch (InvalidDataException error) { return new(state, null, null, null, error.Message); }

        int Lookup(ushort identity) => (identity <= 6000 ? current.ReadPersonLookupReference(identity) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw StrategyMissing("advice_Person_lookup");
        bool Valid(int person) => StrategyNeed(current.ReadPersonNativeValidity(person), "advice_Person_validity");
        ushort Identity(int person) => StrategyNeed(current.ReadPersonIdentity(person), "advice_Person10");
        ForceReference PersonForce(int person) => current.ReadPersonForce(person) ?? throw StrategyMissing("advice_Person_Force");
        ForceReference ForceAt(int physical) => current.ReadForceTableReference(physical < 52 ? physical : 0) ?? throw StrategyMissing("advice_Force_pointer");
        int ForceId(ForceReference force) => StrategyNeed(current.ReadForcePhysicalSlot(force), "advice_Force_identity");
        int RulerIdentity(ForceReference force) => ForceId(force) == 0 ? unaffiliatedRulerIdentity : StrategyNeed(current.ReadForceRecord(ForceId(force))?.RawRuler10, "advice_Force10");
        BaseReference City(int physical) => current.ReadCityTableReference(physical < 52 ? physical : 0) ?? throw StrategyMissing("advice_City_pointer");
        ForceReference BaseForce(BaseReference value) => ForceAt(StrategyNeed(current.ReadBaseForceSlot(value), "advice_Base_Force"));
        ForceReference AreaForce(int physical) => ForceAt(StrategyNeed(current.ReadAreaCurrentForceSlot(new(physical)), "advice_Area_Force"));
    }
}
