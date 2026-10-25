using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class StrategicResolutionCurrent
    {
        //1FE560: the computed trial always precedes the retained-result read.
        private int ResolveSeparation()
        {
            int executor = Executor, target = Target;
            var own = Force(executor); int ownRuler = Ruler(own);
            var other = Force(target); int otherRuler = Ruler(other);
            int district = StrategyNeed(current.ReadPersonDistrictSlot(executor), "separation_DistrictE0");
            int result = SavedResult(Trial());
            if (Player(own))
                History(result == 0 ? 0x3104 : result == 2 ? 0x30F2 : 0x30FB,
                    new(Person0: Id(executor), Person1: Id(ownRuler), Person2: Id(target), Person3: Id(otherRuler)),
                    result == 0 ? 0x310C : 0x310B, new(Person0: Id(executor), Person1: Id(target)),
                    district, Id(executor), result != 0);
            else if (result != 0 && Player(other))
                History(result == 2 ? 0x310D : 0x310E,
                    new(Person0: 5012, Person1: Id(otherRuler), Person2: Id(target), Person3: Id(executor), Person4: Id(ownRuler)),
                    result == 2 ? 0x3110 : 0x3111,
                    new(Person0: Id(executor), Person1: Id(target), Person2: result == 2 ? null : Id(ownRuler)),
                    Primary(other), 5012, false, conditional: true, sound: result == 2 ? 7 : 6);

            if (result != 0)
            {
                int intelligence = Ability(executor);
                int trait = Trait(executor, 65) > 0 ? P("separation-trait-loss", 1) : 0;
                bool captive = StrategyNeed(current.ReadPersonStatus(target), "separation_target_status") == 6;
                int loss = unchecked((captive ? P("separation-captive-loss", 1) : 0) +
                    RandomQuotient(unchecked(intelligence - P("separation-intelligence-offset", 40)),
                        P(result == 2 ? "separation-great-divisor" : "separation-divisor", result == 2 ? 15 : 20)) +
                    trait + P("separation-base-loss", 4));
                if (StrategyNeed(BattleRemakeCurrentSettings.ReadAdvancedSettings(State), "separation_advanced_settings"))
                    loss = unchecked(loss * StrategyNeed(BattleRemakeCurrentSettings.ReadAdvancedParameter(State, 42), "separation_advanced42")) / 100;
                Loyalty(target, unchecked(Loyalty(target) - (loss & 255)));
                if (result != 1) return result;
            }
            if (StrategyNeed(current.ReadPersonStatus(target), "separation_relation_status") != 6)
                Relations(own, other, -P("separation-relation-loss", 5));
            return result;
        }

        private int ResolveRumor()
        {
            int executor = Executor, target = Target;
            var own = Force(executor); int ownRuler = Ruler(own);
            var other = Force(target);
            int otherRuler = StrategyNeed(current.ReadForceNativeValidity(other), "rumor_related_Force_validity")
                ? Ruler(other) : Person(0);
            int result = Trial();
            if (task.SavedResult != -1)
                result = StrategyNeed(current.ReadPersonNativeValidity(executor), "rumor_executor_validity") &&
                    StrategyNeed(current.ReadPersonNativeValidity(target), "rumor_target_validity") ? task.SavedResult : 0;
            if (Player(own))
                History(result == 0 ? 0x34BC : 0x34B7,
                    new(Person0: Id(executor), Person1: Id(ownRuler), Person2: Id(target), Person3: Id(otherRuler)),
                    result == 0 ? 0x34C2 : 0x34C1,
                    new(Person0: Id(executor), Person1: Id(target), Integers: result == 0 ? null : [task.MilitaryIntelligence]),
                    Primary(own), Id(executor), result != 0);
            else if (result != 0 && Player(other))
                History(0x34C3, new(Person0: 5012, Person1: Id(otherRuler), Person2: Id(target), Person3: Id(executor), Person4: Id(ownRuler)),
                    0x34C4, new(Person0: Id(executor), Person1: Id(target), Integers: [task.MilitaryIntelligence]),
                    Primary(other), 5012, false, conditional: true, sound: 6);

            //The Force relationship is changed even on a failed rumor.
            Relations(own, other, -P("rumor-relation-loss", 5));
            if (result != 0)
            {
                current.WritePersonResetByte1FE(target, (byte)Math.Clamp(ForceId(own), 0, 51));
                byte first = (byte)Math.Clamp(P("rumor-active-duration", 30), 0, 255);
                byte second = (byte)Math.Clamp(P("rumor-cooldown-duration", 60), 0, 255);
                current.WritePersonResetWord1FF(target, (ushort)(first | second << 8));
                //The source writes loyalty between1C7 and1C8. These are all
                //one current field set; publishing leaves no fabricated row.
                Loyalty(target, unchecked(Loyalty(target) - P("rumor-loyalty-loss", 1)));
                State = PersonAssignmentPrecallInputs.PublishDeathTouched(State, current, target, []);
                State = package.ApplyTalentPendingMeritCurrent(State, current, executor,
                    unchecked(P("rumor-merit-base", 150) * 10));
            }
            return result;
        }

        private int ResolveDiscord()
        {
            int executor = Executor;
            var own = Force(executor);
            var first = ForceAt(Target);
            var second = ForceAt(task.SecondForceId ?? throw StrategyMissing("discord_second_Force"));
            int ownRuler = Ruler(own), firstRuler = Ruler(first), secondRuler = Ruler(second);
            int district = StrategyNeed(current.ReadPersonDistrictSlot(executor), "discord_DistrictE0");
            int result = SavedResult(Trial());
            if (Player(own))
                History(result == 0 ? 0x30D1 : 0x30CC,
                    new(Person0: Id(executor), Person1: Id(ownRuler), Person2: Id(firstRuler), Person3: Id(secondRuler)),
                    result == 0 ? 0x30D7 : 0x30D6, new(Person0: Id(executor), Person1: Id(firstRuler)),
                    district, Id(executor), result != 0);
            else if (result != 0)
            {
                bool firstPlayer = Player(first);
                if (firstPlayer || Player(second))
                {
                    int playerRuler = firstPlayer ? firstRuler : secondRuler;
                    int oppositeRuler = firstPlayer ? secondRuler : firstRuler;
                    History(result == 2 ? 0x30D8 : 0x30D9,
                        new(Person0: 5012, Person1: Id(playerRuler), Person2: Id(ownRuler), Person3: Id(oppositeRuler)),
                        result == 2 ? 0x30DA : 0x30DB,
                        result == 2 ? new(Person0: Id(oppositeRuler)) : new(Person0: Id(executor), Person1: Id(oppositeRuler)),
                        Primary(firstPlayer ? first : second), 5012, false, conditional: true, sound: result == 2 ? 7 : 6);
                }
            }
            if (result != 0)
            {
                int intelligence = Ability(executor);
                int extra = Trait(executor, 65) > 0 ? P("discord-trait-loss", 1) : 0;
                int amount = unchecked(extra + RandomQuotient(intelligence,
                    P(result == 2 ? "discord-great-divisor" : "discord-divisor", result == 2 ? 9 : 12)) + P("discord-base-loss", 10));
                Relations(first, second, -amount);
                if (result != 1) return result;
            }
            Relations(own, first, -P("discord-detection-relation-loss", 8));
            return result;
        }

        private int Primary(ForceReference force) => StrategyNeed(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "resolution_primary_District");

        private void History(int introduction, HistoryMessageOperands introOperands, int root,
            HistoryMessageOperands operands, int district, int speaker, bool success,
            bool conditional = false, int? sound = null)
        {
            var source = State;
            var intro = package.FormatStrategicCommandReportCurrent(State, current, introduction, introOperands);
            State = intro.State;
            var main = package.FormatStrategicCommandReportCurrent(State, current, root, operands);
            State = main.State;
            int hex = StrategyNeed(ResolveStrategicTaskPositionCurrent(State, current, task).PhysicalSlot, "resolution_history_Hex");
            int day = (package.ReadArmyDestructionSampler(State, current) ?? throw StrategyMissing("resolution_Day450")).Day450;
            State = package.WriteHistory(State, current, new(4, district, hex, day, main.Text, intro.Text,
                speaker, MarkBit0: 1, MarkBit4: success ? 1 : 0), Effects);
            if (sound is int se)
                Effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue,
                    se, null, null, null, null) });
            Effects.Add(CreateStrategicCommandPresentation(current, source, speaker, intro, conditional));
        }
    }
}
