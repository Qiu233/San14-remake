using San14.Core;

namespace San14.Data;

internal sealed record ForceIndependenceCurrentResult(GameState State, bool Created);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal StrategicCommandResolutionResult ResolveStrategicIndependenceWaitCurrent(GameState state,
        SourceWorldContext current, PersonnelIndependenceTask waiting)
    {
        //A read-only resolution view of the actual Wait task. It is never
        //registered, saved, or substituted into the command manager.
        var context = new PersonnelStrategicTask(waiting.Id, waiting.DispatchSlot, waiting.MemberIds,
            11, waiting.ForceId, waiting.SourceLocationId, waiting.SourceDistrictId,
            new(StrategicCommandTargetKind.City, waiting.TargetCityId), null, waiting.CapturedGovernorId,
            waiting.TargetOriginalForceId, waiting.SourceForceId, waiting.StartHexId, waiting.EndHexId,
            waiting.StartDate, waiting.DueDate, waiting.SavedResult, waiting.MilitaryIntelligence,
            0, waiting.Started, waiting.Cancelled);
        return new StrategicResolutionCurrent(this, state, current, context).RunIndependenceWait();
    }

    private sealed partial class StrategicResolutionCurrent
    {
        internal StrategicCommandResolutionResult RunIndependenceWait()
        {
            int result = ResolveIndependenceCore(waiting: true);
            if (Effects.Count != 0)
                State = State with { CampaignEffectRequests = (State.CampaignEffectRequests
                    ?? throw StrategyMissing("independence_wait_pending")).Append(CampaignEffectSource.StrategicCommands,
                    State.NativeMapSeason?.AdvancedCalendarSteps ?? throw StrategyMissing("independence_wait_calendar"), Effects) };
            //1FFA90 returns a bool; unlike1FEEA0 it does not enter1EC0E0.
            return new(State, result == 0 ? 0 : 1);
        }
        //TC1FEEA0: the target is a City; the task actor is the executor.
        private int ResolveIndependence() => ResolveIndependenceCore(waiting: false);

        private int ResolveIndependenceCore(bool waiting)
        {
            int executor = Executor;
            var city = current.ReadCityTableReference(Target) ?? throw StrategyMissing("independence_City");
            var original = ForceAt(StrategyNeed(current.ReadBaseForceSlot(city), "independence_original_Force"));
            int originalRuler = Ruler(original);
            int governor = Person(StrategyNeed(current.ReadArmyReturnBaseGovernor2E(city), "independence_governor"));
            var own = Force(executor); int ownRuler = Ruler(own);
            int district = StrategyNeed(current.ReadPersonDistrictSlot(executor), "independence_executor_District");
            int link = StrategyNeed(current.ReadBaseObjectLink(city), "independence_City4E");
            int trial = Trial();
            int result = !waiting ? SavedResult(trial) : task.SavedResult > 0
                ? Predicates.Blocked(11, executor, Target, null) ? 0 : task.SavedResult
                : trial;
            if (Player(own))
                History(result == 0 ? 0x30E6 : result == 2 ? 0x30DC : 0x30E1,
                    new(Person0: Id(executor), Person1: Id(ownRuler), Person2: Id(originalRuler), Person3: Id(governor), Base0: (ushort)link),
                    result == 0 ? 0x30ED : result == 2 ? 0x30EB : 0x30EC,
                    new(Person0: Id(executor), Person1: Id(originalRuler), Base0: (ushort)link),
                    district, Id(executor), result != 0);
            else if (result != 0 && Player(original))
                History(result == 2 ? 0x30EE : 0x30EF,
                    new(Person0: 5012, Person1: Id(originalRuler), Person2: Id(ownRuler), Person3: Id(governor), Base0: (ushort)link),
                    result == 2 ? 0x30F0 : 0x30F1,
                    result == 2 ? new(Person0: Id(originalRuler), Base0: (ushort)link)
                        : new(Person0: Id(executor), Person1: Id(originalRuler), Base0: (ushort)link),
                    Primary(original), 5012, false, conditional: true, sound: result == 2 ? 7 : 6);

            if (result == 2)
            {
                var pool = current.ReadOwnedCurrentListPool() ?? throw StrategyMissing("independence_pool");
                _independenceOriginalForce = ForceId(original);
                _independenceRebel = governor;
                var changed = package.ApplyForceIndependenceCurrent(State, current, pool, original,
                    governor, null, State.NativeMapSeason?.ThirdDate ?? throw StrategyMissing("independence_date"),
                    Effects, IndependenceReport);
                State = changed.State;
                if (changed.Created) return result;
                result = 1;
            }
            if (result == 1)
            {
                int intelligence = Ability(executor);
                int bonus = Trait(executor, 65) > 0 ? P("independence-trait-loss", 1) : 0;
                int loss = unchecked(P("independence-base-loss", 4) +
                    RandomQuotient(unchecked(intelligence - P("independence-intelligence-offset", 40)),
                        P("independence-intelligence-divisor", 15)) + bonus);
                Loyalty(governor, unchecked(Loyalty(governor) - (loss & 255)));
            }
            Relations(own, original, -P("independence-relation-loss", 8));
            return result;
        }

        //Reports are called at their native write point, before the following
        //person/Force transfer changes the formatter's current operands.
        private GameState IndependenceReport(GameState input, int root, int person)
        {
            State = input;
            var city = current.ReadCityTableReference(Target) ?? throw StrategyMissing("independence_report_City");
            int rebel = _independenceRebel ?? throw StrategyMissing("independence_source_rebel");
            int sourceForce = StrategyNeed(current.ReadBaseForceSlot(city), "independence_report_Force");
            //The old Force must remain the original task target's source even
            //after the City has already transferred to the new faction.
            if (_independenceOriginalForce is int retained) sourceForce = retained;
            int home = StrategyNeed(current.ReadPersonFirstLocation(rebel), "independence_report_Home");
            HistoryMessageOperands operands = root switch
            {
                0x5E43 => new(Person0: Id(rebel), Force0: sourceForce),
                0x5E49 or 0x5E4A => new(Person0: Id(rebel), Force0: sourceForce, Base0: (ushort)home),
                0x5E4B or 0x5E59 => new(Person0: Id(person), Person1: Id(rebel)),
                0x5E58 => new(Person0: Id(person), Person1: Id(rebel), Force0: ForceId(Force(person))),
                0x5E6F => new(Person0: Id(person), Person1: Id(Ruler(Force(person))), Person2: Id(rebel)),
                _ => throw StrategyMissing("independence_report_root")
            };
            if (root == 0x5E4B)
                Effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 6, null, null, null, null) });
            var source = State;
            var formatted = package.FormatStrategicCommandReportCurrent(State, current, root, operands);
            State = formatted.State;
            if (root == 0x5E49)
                Effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement,
                    null, null, null, null, formatted.Text) });
            else if (root is not (0x5E4A or 0x5E58))
                Effects.Add(CreateStrategicCommandPresentation(current, source, Id(person), formatted));
            if (root is 0x5E4A or 0x5E58)
                IndependenceHistory(root, person, rebel, sourceForce, home, formatted.Text);
            return State;
        }
        private int? _independenceOriginalForce;
        private int? _independenceRebel;

        private void IndependenceHistory(int introRoot, int person, int rebel, int sourceForce, int home, string introduction)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(State, current) ?? throw StrategyMissing("independence_history_player");
            int ruler = Ruler(player);
            if (!StrategyNeed(current.ReadPersonNativeValidity(ruler), "independence_history_player_validity")) return;
            int speaker = 5012;
            if (introRoot == 0x5E4A)
            {
                int rawAdvisor = StrategyNeed(current.ReadAssignmentForceAdvisor52(ForceId(player)), "independence_history_advisor");
                int advisor = Person(rawAdvisor);
                if (StrategyNeed(current.ReadPersonNativeValidity(advisor), "independence_history_advisor_validity")) speaker = Id(advisor);
            }
            int root = introRoot == 0x5E4A ? 0x525B : 0x526C;
            var operands = introRoot == 0x5E4A
                ? new HistoryMessageOperands(Person0: speaker, Person1: Id(ruler), Person2: Id(rebel), Base0: (ushort)home, Force0: sourceForce)
                : new HistoryMessageOperands(Person0: 5012, Person1: Id(ruler), Person2: Id(person), Person3: Id(rebel), Force0: ForceId(Force(person)));
            var text = package.FormatStrategicCommandReportCurrent(State, current, root, operands); State = text.State;
            int day = (package.ReadArmyDestructionSampler(State, current) ?? throw StrategyMissing("independence_history_day")).Day450;
            var date = State.NativeMapSeason?.ThirdDate ?? throw StrategyMissing("independence_history_date");
            int hex = StrategyNeed(PersonReturnCommands.ResolvePersonPosition(State,current,rebel,date).PhysicalSlot, "independence_history_Hex");
            if (current.ReadOwnedPersonCommandManager() is { } commands) State = State with { PersonnelCommands = commands };
            int district = Primary(ForceAt(sourceForce));
            State = package.WriteHistory(State, current, new(1, district, hex, day, introduction, text.Text,
                introRoot == 0x5E4A ? speaker : 5012, MarkBit0: 1, MarkBit4: 0), Effects);
        }
    }

    //TC2E6010's domain operation is independent of a strategy task. Report
    //formatting is supplied by the real caller so normal Turn can reuse it.
    internal ForceIndependenceCurrentResult ApplyForceIndependenceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference original, int rebel,
        int? excludedPerson, NativeMapSeasonDate date, List<CampaignEffectRequest> effects,
        Func<GameState,int,int,GameState> reportAtSource)
    {
        int homeCode = StrategyNeed(current.ReadPersonFirstLocation(rebel), "independence_create_Home");
        var home = BaseLookup.Find(homeCode, current);
        int originalId = StrategyNeed(current.ReadForcePhysicalSlot(original), "independence_scene_original_Force");
        int rebelId = StrategyNeed(current.ReadPersonIdentity(rebel), "independence_scene_Person");
        int day = (ReadArmyDestructionSampler(state, current) ?? throw StrategyMissing("independence_scene_Day450")).Day450;
        effects.Add(new(null, null) { ForceIndependenceScene = new(originalId, rebelId, homeCode, day) });
        var created = CreateStrategicIndependenceForceCurrent(state, current, pool, rebel, home, date, effects);
        var working = created.State;
        if (!StrategyNeed(current.ReadForceNativeValidity(created.Force), "independence_created_Force_validity"))
            return new(working, false);
        int hex = current.ReadBaseCurrentHexIndex(home);
        effects.Add(new(null, null) { ForceCoalitionVisual = new(ForceCoalitionVisualOperation.CameraFocus, hex) });
        effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue,
            IsIndependencePlayer(working, current, original) ? 7 : 8, null, null, null, null) });
        working = reportAtSource(working, 0x5E43, rebel);
        effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 21, null, null, null, null) });
        effects.Add(new(null, null) { ForceCoalitionVisual = new(ForceCoalitionVisualOperation.NarrationEnter, null) });
        working = reportAtSource(working, 0x5E49, rebel);
        effects.Add(new(null, null) { ForceCoalitionVisual = new(ForceCoalitionVisualOperation.NarrationExit, null) });
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current) ?? throw StrategyMissing("independence_source_player");
        int playerRuler = StrategyNeed(current.ReadForceRecord(StrategyNeed(current.ReadForcePhysicalSlot(player), "independence_source_player_slot"))?.RawRuler10,
            "independence_source_player_ruler");
        int pp = StrategyNeed((current.ReadPersonLookupReference(playerRuler <= 6000 ? playerRuler : 0))?.PhysicalSlot, "independence_source_player_person");
        if (StrategyNeed(current.ReadPersonNativeValidity(pp), "independence_source_player_validity"))
            working = reportAtSource(working, 0x5E4A, rebel);
        working = ApplyStrategicIndependenceFollowersCurrent(working, current, pool, original, rebel,
            excludedPerson, home, created.Force, date, effects, reportAtSource);
        effects.Add(new(null, null) { ForceCoalitionVisual = new(ForceCoalitionVisualOperation.CameraRestore, null) });
        return new(working, true);
    }

    private static bool IsIndependencePlayer(GameState state, SourceWorldContext current, ForceReference force)
    {
        int player = StrategyNeed(current.ReadForcePhysicalSlot(OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw StrategyMissing("independence_player")), "independence_player_slot");
        return player != 0 && player == StrategyNeed(current.ReadForcePhysicalSlot(force), "independence_compared_Force");
    }
}
