using San14.Core;

namespace San14.Data;

internal sealed record EditedEventPersonMovementResult(GameState State, int NativeReturn);

public sealed partial class ArmyCampaignRuleAdapter
{
    //The enclosing result dispatcher owns this detached current and publishes
    //its final candidate atomically. No world capture or pool import occurs here.
    internal EditedEventPersonMovementResult ApplyEditedEventPersonMovementResultCurrent(
        GameState state, SourceWorldContext current, int code, IReadOnlyList<int> arguments,
        EditedEventResultSelectors selectors)
        => new EventPersonMovementCurrent(this, state, current, selectors).Apply(code, arguments);

    private sealed partial class EventPersonMovementCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly SourceWorldContext current;
        private readonly EditedEventResultSelectors selectors;
        private GameState latest;

        internal EventPersonMovementCurrent(ArmyCampaignRuleAdapter rules, GameState input,
            SourceWorldContext current, EditedEventResultSelectors selectors)
        {
            this.rules = rules; latest = input; this.current = current; this.selectors = selectors;
        }
        private readonly List<CampaignEffectRequest> effects = [];
        private WorldIndexContext Pool => current.ReadOwnedCurrentListPool()
            ?? throw Missing("shared_registry_pool");
        private NativeMapSeasonDate Date => latest.NativeMapSeason?.ThirdDate
            ?? throw Missing("current_calendar");
        private int Day() => (rules.ReadArmyDestructionSampler(latest, current)
            ?? throw Missing("current_World450")).Day450;

        internal EditedEventPersonMovementResult Apply(int code, IReadOnlyList<int> args)
        {
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldAreaRemoval = current.AssignmentAreaOfficerRemoval;
            var oldPolicy = current.BaseForcePolicyEffectReader;
            var previousRefresh = current.TransferServiceRefresher;
            current.TransferServiceRefresher = (first, second, third) =>
                latest = rules.RefreshPersonnelMilestoneCurrent(latest, current, first, second, third);
            try
            {
                if (args.Count != 8) throw Missing("statement_arguments");
                int result = code switch
                {
                    400 => ChangeForce(args[0], args[1]),
                    401 => ChangeHome(args[0], args[1]),
                    _ => throw Missing("unexpected_opcode")
                };
                if (effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw Missing("effect_owner")).Append(CampaignEffectSource.ForceAssociationRemoval,
                            latest.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("effect_calendar"), effects.ToArray()) };
                return new(latest, result);
            }
            finally
            {
                current.TransferServiceRefresher = previousRefresh;
                current.AssignmentAdvisorResetCallbacks = oldAdvisor;
                current.AssignmentAreaOfficerRemoval = oldAreaRemoval;
                current.BaseForcePolicyEffectReader = oldPolicy;
            }
        }

        private int ChangeHome(int baseSelector, int personSelector)
        {
            var target = Base(baseSelector);
            int person = Person(personSelector);
            if (!Need(current.ReadBaseNativeValidity(target), "destination_validity") || !Eligible(person)) return 0;
            var old = BaseLookup.Find(Need(current.ReadPersonFirstLocation(person), "home_PersonE2"), current);
            if (old == target) return 0;
            byte status = Need(current.ReadPersonStatus(person), "home_PersonE6");
            //3B3860's unsigned (status-5)&~3 admits6, excludes5/7/8.
            if (status is 1 or 5 or 7 or 8 or 9) return 0;
            if (status is >= 1 and <= 4 && Need(current.ReadPersonOldForceSlot(person), "home_Person_Force") !=
                Need(current.ReadBaseForceSlot(target), "home_destination_Force")) return 0;
            if (Disappearing(person)) return 0;
            BindInputs();
            var caller = NewPersonList(person);
            try
            {
                //3D7A40 returns the effective commander Army first. Captives'
                //holding Armies are excluded by22A200(Person,1).
                var army = PersonArmyLocationSource.ReadCommanderArmy(current, person)
                    ?? throw Missing("home_commander_Army");
                if (Need(current.ReadArmyUnitNativeValidity(army), "home_Army_validity"))
                    ReturnArmy(army, target, 1);
                ConsumePending(); //28E6D0 precedes every linked-command query.
                ResetAssociated(person);
                MoveOne(person, target); //271320's real singleton list body.
                Publish();
                return 1;
            }
            finally { Release(caller); }
        }

        private int ChangeForce(int personSelector, int forceSelector)
        {
            int person = Person(personSelector);
            var target = Force(forceSelector);
            if (!Need(current.ReadForceNativeValidity(target), "target_Force_validity") || !Eligible(person)) return 0;
            var oldForce = current.ReadPersonForce(person) ?? throw Missing("old_Force_pointer");
            if (oldForce == target) return 0;
            byte status = Need(current.ReadPersonStatus(person), "force_PersonE6");
            if (status is 1 or 9 || Disappearing(person)) return 0;
            BindInputs();
            ResetAssociated(person); //3D8050, before the ordinary3ACC20.
            status = Need(current.ReadPersonStatus(person), "force_fresh_PersonE6");
            if (status is 5 or 7 or 8)
            {
                //3ACC20 passes22A940(Home), the representative Area's City.
                latest = rules.JoinNormalTurnSelfRecommendationCurrent(latest, current, person,
                    CurrentGateMapQueries.FindCity(ForceHome(target), current));
            }
            else
            {
                SplitTravelGroup(person); //26C580, then26CAF0.
                var home = ForceHome(target); //22EA10: actual ruler's current E2.
                var district = ForcePrimaryDistrictSource.Resolve(current, target);
                var joined = rules.ApplyRecruitJoinCurrent(latest, current, person, district,
                    home, Date, Day, effects);
                latest = joined.State;
                SnapIdlePosition(person); //3ACC20's post271C20 E4 fallback.
            }
            //3B15D0 looks up the post-change home and affiliation afresh.
            var freshHome = BaseLookup.Find(Need(current.ReadPersonFirstLocation(person), "force_after_PersonE2"), current);
            var freshForce = current.ReadPersonForce(person) ?? throw Missing("force_after_Force");
            status = Need(current.ReadPersonStatus(person), "force_after_PersonE6");
            if (status is >= 1 and <= 4)
            {
                var army = PersonArmyLocationSource.ReadCommanderArmy(current, person)
                    ?? throw Missing("force_after_Army");
                if (Need(current.ReadArmyUnitNativeValidity(army), "force_after_Army_validity"))
                {
                    //This existing entry is the same259360(null,null,0,4)
                    //employed-commander body; it does not kill the Person.
                    var destroyed = new OwnedArmyDestructionStage(rules).ApplyAbsorptionCommanderCause4Current(
                        latest, current, Pool, Need(army.PhysicalSlot, "force_after_Army_pointer"), Date, effects);
                    latest = destroyed.State ?? throw Missing(destroyed.UnresolvedOwner ?? "force_after_Army_cause4");
                    ConsumePending(); //28E6D0 precedes274D70's live Army maintenance.
                    latest = rules.MaintainEditedDeathArmyCommandsCurrent(latest, current);
                }
                if (Need(current.ReadBaseNativeValidity(freshHome), "force_after_Home_validity") &&
                    Need(current.ReadForceNativeValidity(freshForce), "force_after_Force_validity"))
                {
                    if (Need(current.ReadBaseForceSlot(freshHome), "force_after_Home_Force") !=
                        Need(current.ReadForcePhysicalSlot(freshForce), "force_after_Force60"))
                    {
                        int rawHomeForce = Need(current.ReadBaseForceSlot(freshHome), "home_ForceB0");
                        var homeForce = current.ReadForceTableReference(rawHomeForce <= 51 ? rawHomeForce : 0)
                            ?? throw Missing("home_Force_pointer");
                        var primary = ForcePrimaryDistrictSource.Resolve(current, homeForce);
                        var assignments = new List<PersonAssignmentPrecallResult>();
                        PersonDistrictTransfer.Apply(person, Need(primary.PhysicalSlot, "home_primary_District"),
                            ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, assignments,
                                assignment => ReceiveAssignment(person, assignment)));
                        if (assignments.Count != 0)
                            latest = PersonAssignmentPrecallInputs.PublishDeathTouched(latest, current, person, assignments);
                        Publish();
                        return 0; //The original stores survive this native return0.
                    }
                    SnapIdlePosition(person);
                    Publish();
                    return 1;
                }
            }
            latest = rules.ApplyCaptiveFreedomCurrent(latest, current, Pool, person,
                Date, Day, () => rules.ReadArmyDestructionSampler(latest, current)
                    ?? throw Missing("freedom_sampler"), effects);
            Publish();
            return 0;
        }

        private void BindInputs()
        {
            //Install supporting readers on the existing world. In particular,
            //do not restore a saved arena over the live registry/manager.
            rules._personDestructionInputs.BindCurrent(latest, current);
            rules._personRelationInputs.BindCurrent(latest, current);
            rules._force164Inputs.BindCurrent(latest, current);
            PersonAdvisorResetSource.BindCurrent(latest, current);
            ForceUnregisterInputs.BindCurrent(latest, current);
            OwnedArmyReturnServiceInputs.BindCurrent(latest, current);
            current.ArmyReturnTransferMode = rules.ReadApplicationTransferModeCurrent();
            current.TransferService ??= new();
            current.BaseForcePolicyEffectReader = (force, effect) =>
                ForcePolicyScratchCurrent.ReadBeforeFreedom(latest, current, force, effect, true).Value;
            current.AssignmentAdvisorResetCallbacks = force =>
            {
                var reset = PersonAdvisorResetSource.ApplyWithinPersonCapture(latest, current, Pool, force);
                latest = reset.State ?? throw Missing(reset.UnresolvedOwner ?? "advisor_reset");
                return true;
            };
            current.AssignmentAreaOfficerRemoval = (area, oldId) =>
            {
                var removed = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, oldId);
                if (removed.NativeReturn is null) throw Missing(removed.UnresolvedOwner ?? "area_assignment_removal");
                if (removed.NativeReturn != 0 && removed.PersonPhysical is int touched)
                    latest = PersonAssignmentPrecallInputs.PublishDeathTouched(latest, current, touched, [], publishVector: true);
                return removed.NativeReturn != 0;
            };
        }

        private bool Eligible(int person)
        {
            if (!Need(current.ReadPersonNativeValidity(person), "Person_validity")) return false;
            ushort id = Need(current.ReadPersonIdentity(person), "Person10");
            if (unchecked((ushort)(id - 4001)) <= 1099) return false;
            ushort first = Need(current.ReadPersonFirstLocation(person), "PersonE2");
            if ((first is < 1 or > 61) && DuelAftermathForce(current, person) is >= 46 and <= 50) return false;
            if (DuelAftermathSpecialPerson(current, person)) return false;
            return (Need(current.ReadOwnedReturnPersonFlags(person), "Person15E") & 0x4000) == 0;
        }

        private bool Disappearing(int person)
        {
            var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("Person_association");
            return Need(current.ReadLinkedEntityNativeValidity(linked), "association_validity") && Kind(linked) == 16;
        }

        private int Kind(PersonLinkedEntityReference linked) => Need(
            new PersonReturnGovernorQueries(latest, rules._governorInputs, current)
                .ReadCurrentAssociatedVirtual98(linked), "association_virtual98");

        private void ResetAssociated(int person)
        {
            //3D8050 tests the pointer and the old Home, then335CD0's actual
            //cancel,32B190's ordered members and a second retirement request.
            var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("reset_association");
            if (linked.Identity is not int receiver) return;
            var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(person), "reset_PersonE2"), current);
            if (!Need(current.ReadBaseNativeValidity(home), "reset_Home_validity")) return;
            latest = PersonReturnCommands.CancelAssociatedCommandCurrent(latest, current, receiver, Date, rules);
            if (storyImmediateRetirement)
                latest = PersonReturnCommands.RetireAssociatedCommandImmediatelyCurrent(latest, current, receiver);
            linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("reset_fresh_association");
            if (linked.Identity is not int fresh) return;
            if (Kind(linked) is 1 or 15 or 18)
            {
                var moved = rules.ApplyPersonReturnArrivalCurrent(latest, current, Pool, fresh, home,
                    new PersonReturnGovernorQueries(latest, rules._governorInputs, current), true, Date);
                latest = moved.State ?? throw Missing(moved.UnresolvedOwner ?? "reset_32B190");
            }
            if (storyImmediateRetirement)
                latest = PersonReturnCommands.RetireAssociatedCommandImmediatelyCurrent(latest, current, fresh);
            else
            {
                var owner = current.ReadOwnedPersonCommandManager() ?? throw Missing("reset_manager");
                owner = PersonReturnCommands.QueueRetirementCurrent(owner, current, fresh);
                latest = latest with { PersonnelCommands = owner };
            }
        }

        private void SplitTravelGroup(int person)
        {
            var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("split_association");
            if (!Need(current.ReadLinkedEntityNativeValidity(linked), "split_association_validity")) return;
            int kind = Kind(linked);
            if (kind is 17 or 18)
            {
                SplitForeignTradeGroup(linked);
                //26CAF0 starts with another actual22AC00 lookup.
                linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("split_fresh_association");
                if (!Need(current.ReadLinkedEntityNativeValidity(linked), "split_fresh_validity")) return;
                kind = Kind(linked);
            }
            if (kind != 15) return;
            int receiver = Need(linked.Identity, "split_Return_pointer");
            var owner = current.ReadOwnedPersonCommandManager() ?? throw Missing("split_manager");
            var members = current.ReadLinkedEntityMemberIds(linked) ?? throw Missing("split_members");
            if (storyImmediateRetirement)
                latest = PersonReturnCommands.RetireAssociatedCommandImmediatelyCurrent(latest, current, receiver);
            else
            {
                owner = PersonReturnCommands.QueueRetirementCurrent(owner, current, receiver);
                latest = latest with { PersonnelCommands = owner };
            }
            foreach (uint? raw in members)
            {
                int member = Person(unchecked((int)Need(raw, "split_member_ID")));
                if (!Need(current.ReadPersonNativeValidity(member), "split_member_validity")) continue;
                var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(member), "split_member_E2"), current);
                if (!Need(current.ReadBaseNativeValidity(home), "split_member_Home_validity")) continue;
                current.WritePersonSecondLocation(member, Math.Min(Need(current.ReadBaseObjectLink(home), "split_member_Base4E"), (ushort)1061));
            }
        }

        private void SplitForeignTradeGroup(PersonLinkedEntityReference linked)
        {
            int receiver = Need(linked.Identity, "split_foreign_receiver");
            var owner = current.ReadOwnedPersonCommandManager() ?? throw Missing("split_foreign_manager");
            var task = owner.Caravans?.GetValueOrDefault(receiver) ?? throw Missing("split_foreign_receiver_owner");
            if (task.NativeKind is not (17 or 18)) throw Missing("split_foreign_receiver_kind");
            var list = current.CreateTemporaryHexList() ?? throw Missing("split_foreign_descriptor");
            if (!list.Identity.HasValue) throw Missing("split_foreign_descriptor_allocation");
            var people = new List<int>();
            try
            {
                foreach (int id in task.MemberIds)
                {
                    int member = Person(id);
                    if (Need(current.ReadPersonNativeValidity(member), "split_foreign_member_validity") &&
                        current.AppendPersonReturnTemporary(list, member)) people.Add(member);
                }
                //26C580 removes all matching values from the retained receiver,
                //not from a newly exposed association. Keep duplicate visits.
                foreach (int member in people)
                {
                    int id = Need(current.ReadPersonIdentity(member), "split_foreign_member_ID");
                    owner = current.ReadOwnedPersonCommandManager() ?? throw Missing("split_foreign_live_manager");
                    task = owner.Caravans?.GetValueOrDefault(receiver) ?? throw Missing("split_foreign_live_receiver");
                    task = task with { MemberIds = task.MemberIds.Where(value => value != id).ToArray() };
                    latest = StoreForeignTradeTaskCurrent(latest, current, task);
                }
                foreach (int member in people)
                {
                    var next = current.ReadPersonLinkedEntity(member) ?? throw Missing("split_foreign_member_association");
                    if (Need(current.ReadLinkedEntityNativeValidity(next), "split_foreign_member_association_validity")) continue;
                    //26C580 already selected native-valid members and removed
                    //their old links. Its orphan Return entry has no serving
                    //status gate; cancellation's stricter admission is separate.
                    latest = PersonReturnCommands.ScheduleReturnCurrent(latest, current,
                        [Need(current.ReadPersonIdentity(member), "split_foreign_singleton_ID")], Date, task.ReportToPlayer);
                }
                //The actual335CD0 runs even if an earlier3D8050 already queued
                //this receiver. Do not use the manager's retiring fast path.
                owner = current.ReadOwnedPersonCommandManager() ?? throw Missing("split_foreign_cancel_manager");
                task = owner.Caravans?.GetValueOrDefault(receiver) ?? throw Missing("split_foreign_cancel_receiver");
                latest = rules.CancelForeignTradeAssociatedCurrent(latest, current, task, Date);
                if (storyImmediateRetirement)
                    latest = PersonReturnCommands.RetireAssociatedCommandImmediatelyCurrent(latest, current, receiver);
            }
            finally { Release(list); }
        }

        private void MoveOne(int person, BaseReference target)
        {
            var operation = rules.CreateCaptureDistrictOperationQueries(current, () => latest,
                state => latest = state, Date, effects);
            var moved = ArmyReturnCommanderHomeUpdater.ApplyPersonCurrent(person, target, current, Pool,
                operation, assignment =>
                {
                    ReceiveAssignment(person, assignment);
                    latest = PersonAssignmentPrecallInputs.PublishDeathTouched(latest, current, person, [assignment]);
                });
            latest = ArmyReturnCommanderHomeUpdater.PublishPersonCurrent(latest, current,
                moved.Packet ?? throw Missing(moved.UnresolvedOwner ?? "home_271320"));
            latest = PersonReturnDistrictReorganizationSource.PublishCurrent(latest, current, operation);
            latest = PersonReturnDistrictTransferSource.PublishAreaAssignments(latest, current,
                operation.ChangedAreas, operation.ChangedGates);
        }

        private void ReturnArmy(ArmyUnitReference army, BaseReference target, int mode)
        {
            //29F220's initial foreign-Force gate has no writes. The current
            //ordinary-return engine owns the entire reached same-Force body.
            ushort commander = Need(current.ReadArmyUnitCommanderId(army), "return_Commander12");
            int person = Person(commander);
            if (!Need(current.ReadPersonNativeValidity(person), "return_Commander_validity")) return;
            if (Need(current.ReadPersonOldForceSlot(person), "return_Commander_Force") !=
                Need(current.ReadBaseForceSlot(target), "return_target_Force")) return;
            byte status = Need(current.ReadArmyReturnUnitStatus10(army), "return_Army10");
            byte? transport = status == 2 ? Need(latest.Units.Values.SingleOrDefault(unit =>
                unit.NativeArmyOrigin?.NativeSlotIndex == army.PhysicalSlot)?.NativeTacticRuntime?.Flags26,
                    "return_transport_Flags26") : null;
            var prepared = rules.PrepareOwnedArmyReturnCurrent(latest, army, target, mode, current, Pool,
                rules.CreateCaptureGovernorQueries(latest), new(false, false), transport, operationDate: Date);
            latest = OwnedArmyReturnWorld.Borrow(latest, current, Pool).CommitPrepared(latest,
                prepared.Packet ?? throw Missing(prepared.UnresolvedOwner ?? "home_29F220"))
                ?? throw Missing("home_29F220_publication");
        }

        private void ReceiveAssignment(int person, PersonAssignmentPrecallResult assignment)
        {
            if (assignment.NativeReturn is null) throw Missing(assignment.UnresolvedOwner ?? "assignment_cleanup");
            latest = rules.ApplyPersonAssociationRemovalReports(latest, current, person, assignment, Day, effects);
        }

        private void SnapIdlePosition(int person)
        {
            var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("idle_association");
            if (Need(current.ReadLinkedEntityNativeValidity(linked), "idle_association_validity")) return;
            var army = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw Missing("idle_Army");
            if (Need(current.ReadArmyUnitNativeValidity(army), "idle_Army_validity")) return;
            current.WritePersonSecondLocation(person, Math.Min(Need(current.ReadPersonFirstLocation(person), "idle_PersonE2"), (ushort)1061));
        }

        private void ConsumePending() => latest = rules.ConsumeArmyLifecyclePendingCurrent(latest, current, Pool)
            ?? throw Missing("pending70_cleanup");

        private void Publish()
        {
            latest = OwnedArmyReturnCurrentFields.PublishCurrent(latest, current);
            latest = OwnedArmyLifecycleDetachedInputs.PublishCurrent(latest, current);
            latest = latest with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
                ?? throw Missing("published_manager") };
            latest = latest.WithArmyLifecycle((latest.ReadArmyLifecycle() ?? throw Missing("published_lifecycle"))
                with { Indexes = Pool.ProjectOwnedRoles() });
        }

        private BaseReference ForceHome(ForceReference force)
        {
            int slot = Need(current.ReadForcePhysicalSlot(force), "Force60");
            ushort ruler = Need(current.ReadForceRecord(slot)?.RawRuler10, "Force10");
            return BaseLookup.Find(Need(current.ReadPersonFirstLocation(Person(ruler)), "ruler_PersonE2"), current);
        }

        private int Person(int selector) => selectors.Person(selector);
        private BaseReference Base(int selector) => selectors.Base(selector);
        private ForceReference Force(int selector) => selectors.Force(selector);
        private HexListHandle NewPersonList(int person)
        {
            var list = current.CreateTemporaryHexList() ?? throw Missing("caller_descriptor");
            if (!list.Identity.HasValue) throw Missing("caller_descriptor_allocation");
            if (!current.AppendPersonReturnTemporary(list, person))
            { Release(list); throw Missing("caller_node_allocation"); }
            return list;
        }
        private void Release(HexListHandle list)
        { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private static T Need<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
        private static InvalidDataException Missing(string owner) => new("edited_event.results.person_movement." + owner);
    }
}
