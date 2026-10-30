using San14.Core;

namespace San14.Data;

internal sealed record PersonnelTalentStartResult(GameState? State,
    PersonnelTalentTask? Task, string? UnresolvedOwner);

public static partial class PersonReturnCommands
{
    internal const string TalentSearchBookCostKey = "simulation.talent.search-command-books";
    internal const string TalentRecruitBookCostKey = "simulation.talent.recruit-command-books";

    /// <summary>Registers the actual talent receiver before adding its member.
    /// On Unknown the enclosing command must discard its unpublished current.
    /// Advisor target is Area physical for Search, Person physical for Recruit.</summary>
    internal static PersonnelTalentStartResult StartTalentTask(GameState state,
        SourceWorldContext current, PreparedTalentOrder prepared, NativeMapSeasonDate sourceDate,
        Func<GameState, BaseReference, GameState>? repairGovernor = null,
        Func<GameState, int, int?, TalentOrderKind, TalentAdvisorSampleResult>? advisorSample = null,
        ApplicationGameplayContext? application = null)
    {
        try
        {
            if (!Enum.IsDefined(prepared.Kind) || (prepared.Kind == TalentOrderKind.Search ? prepared.RecruitMode != 0 :
                prepared.RecruitMode is not (0 or 10 or 30)))
                throw TalentMissing("recruit_mode");
            if (state.NativeArmySimulation?.Parameters.Language is not ("TC" or "SC"))
                throw TalentMissing("edition_creation_contract");
            var before = CurrentCommandOwner(state, current);
            if (before.Talents is null) throw TalentMissing("saved_talent_owner");
            if (before.AvailableDispatchSlots.Length == 0) throw TalentMissing("dispatch_allocation1DE290");
            int actor = Person(prepared.ExecutorOfficerId);
            if (!Get(current.ReadPersonNativeValidity(actor), "talent executor validity"))
                throw TalentMissing("executor_native_validity");
            if (PersonCurrentBusySource.Read(current, actor, TalentMissing))
                throw TalentMissing("executor_busy232370");
            ushort personId = Get(current.ReadPersonIdentity(actor), "talent executor Person10");
            ushort sourceLocation = Get(current.ReadPersonFirstLocation(actor), "talent executor E2");
            var sourceBase = BaseLookup.Find(sourceLocation, current);
            if (!Get(current.ReadBaseNativeValidity(sourceBase), "talent source Base validity"))
                throw TalentMissing("source_Base_validity");
            int districtId = Get(current.ReadBaseDistrictSlot(sourceBase), "talent source District");
            var force = current.ReadPersonForce(actor) ?? throw TalentMissing("executor_Force");
            int forceId = Get(force.PhysicalSlot, "talent executor Force physical");
            if (sourceLocation != prepared.CityId || districtId != prepared.DistrictId || forceId != prepared.ForceId)
                throw TalentMissing("prepared_source_owner_changed");
            if (!Get(current.ReadForceNativeValidity(force), "talent Force validity"))
                throw TalentMissing("executor_Force_validity");

            AreaReference? area = null;
            int? targetPerson = null, targetOriginalForce = null, targetOriginalHome = null;
            int? targetHex = null;
            if (prepared.Kind == TalentOrderKind.Search)
            {
                int requested = prepared.TargetHexId ?? throw TalentMissing("Search_target_Hex");
                var hex = current.ReadHexTableReference(requested is >= 0 and < 48400 ? requested : 0)
                    ?? throw TalentMissing("Search_Hex_pointer");
                ushort rawArea = Get(current.ReadHexAreaSlot(hex), "talent Search Hex Area");
                area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0)
                    ?? throw TalentMissing("Search_Area_pointer");
                if (!Get(current.ReadAreaNativeValidity(area), "talent Search Area validity"))
                    throw TalentMissing("Search_Area_validity");
                byte owner = Get(current.ReadAreaCurrentForceSlot(area), "talent Search Area Force");
                if (owner != forceId) throw TalentMissing("Search_Area_ownership");
                ushort representative = Get(current.ReadAreaHexIndex(area), "talent Search Area3A");
                targetHex = Get((current.ReadHexTableReference(representative < 48400 ? representative : 0)
                    ?? throw TalentMissing("Search_target_Hex_pointer")).PhysicalSlot, "talent Search target Hex physical");
            }
            else if (prepared.Kind == TalentOrderKind.Recruit)
            {
                targetPerson = Person(prepared.TargetOfficerId ?? throw TalentMissing("Employ_target"));
                if (!Get(current.ReadPersonNativeValidity(targetPerson.Value), "talent Employ target validity"))
                    throw TalentMissing("Employ_target_validity");
                targetOriginalForce = Get((current.ReadPersonForce(targetPerson.Value)
                    ?? throw TalentMissing("Employ_target_Force")).PhysicalSlot, "talent target Force identity");
                //The actual Employ7C capture is the low byte of targetE2.
                targetOriginalHome = unchecked((byte)Get(current.ReadPersonFirstLocation(targetPerson.Value), "talent target HomeE2"));
            }
            else throw TalentMissing("command_kind");

            int id = before.NextTaskId, slot = before.AvailableDispatchSlots[0];
            //Fresh48=0 and advisor sample=-1 are the actual constructor /
            //2F39D0 initialized locals, not invented completed-task values.
            var task = new PersonnelTalentTask(id, slot, [], prepared.Kind, forceId,
                sourceLocation, districtId, targetHex, prepared.TargetOfficerId, 48400, 48400,
                sourceDate, new(0, 1, 1, 0), -1, false, false, targetOriginalForce,
                targetOriginalHome, prepared.RecruitMode, false);
            var ownerState = before with { NextTaskId = checked(id + 1),
                AvailableDispatchSlots = before.AvailableDispatchSlots[1..],
                Talents = new(before.Talents) { [id] = task } };
            var handle = current.OwnedIndexes?.AssociatedTaskHandle ?? throw TalentMissing("association_index");
            if (!current.AppendPersonReturnAssociated(new(handle.Identity), id))
                throw TalentMissing("association_registration");
            current.SetOwnedPersonCommandManager(ownerState);
            current.SetLinkedEntityRawState58(id, slot);
            current.SetLinkedEntityMemberIds(id, []);
            current.SetAssociatedPayloadPostLoadCallback(id, PostLoadCallback.NoOp);

            ushort flags = Get(current.ReadArmyReturnPersonFlags196(actor), "talent executor15E");
            current.WriteArmyReturnPersonFlags196(actor, (ushort)(flags | 1));
            var position = ResolvePersonPosition(state, current, actor, sourceDate);
            ushort currentArea = Get(current.ReadHexAreaSlot(position), "talent executor current Area");
            current.WritePersonSecondLocation(actor, currentArea is >= 1 and <= 500
                ? (ushort)(currentArea + 561) : (ushort)0);
            ownerState = CurrentCommandOwner(state, current);
            task = task with { MemberIds = [prepared.ExecutorOfficerId] };
            ownerState = ownerState with { Talents = new(ownerState.Talents!) { [id] = task } };
            current.SetOwnedPersonCommandManager(ownerState);
            current.SetLinkedEntityMemberIds(id, [(uint?)personId]);
            var working = state with { PersonnelCommands = ownerState };

            var advice = advisorSample is not null
                ? advisorSample(working, actor, prepared.Kind == TalentOrderKind.Search ? area!.PhysicalSlot : targetPerson, prepared.Kind)
                : new TalentAdvisorSampleResult(working, ReadNoAdvisorSample());
            working = advice.State;
            var route = TalentTravelSource.Resolve(working, current, prepared.Kind, actor,
                sourceBase, area, targetPerson, force, sourceDate);
            task = task with { SavedSample = advice.Sample, StartHexId = route.StartHexId, EndHexId = route.EndHexId,
                DueDate = DateFromOrdinal(unchecked(DateOrdinal(sourceDate) + route.TravelDays)) };
            ownerState = CurrentCommandOwner(working, current);
            ownerState = ownerState with { Talents = new(ownerState.Talents!) { [id] = task } };
            current.SetOwnedPersonCommandManager(ownerState);
            working = working with { PersonnelCommands = ownerState };

            var district = working.Districts.GetValueOrDefault(districtId) ?? throw TalentMissing("source_District");
            int books = district.OrdersRemaining ?? throw TalentMissing("source_District14");
            int cost = ReadTalentBookCost(working, prepared.Kind);
            working = working with { Districts = new(working.Districts) { [districtId] = district with
                { OrdersRemaining = Math.Clamp(unchecked(books - cost), 0, 200) } } };
            if (prepared.Kind == TalentOrderKind.Recruit &&
                ApplicationCommandAccountingSource.HasSelectedUser(working, application?.UserSelectionServicePresent) &&
                BaseCaptureRewardSource.ReadPlayerRulerDistrict(current,
                    current.ReadDistrictTableReference(districtId) ?? throw TalentMissing("App_payment_District"), TalentMissing))
                working = ApplicationCommandAccountingSource.Use(working, current, cost, application?.UserSelectionServicePresent);
            //334A50 is an actual reached governor repair, not a blanket
            //reselection or a no-op because this command owns only one member.
            if (Get(current.ReadArmyReturnBaseGovernor2E(sourceBase), "talent source Governor2E") == personId)
                working = repairGovernor is not null ? repairGovernor(working, sourceBase)
                    : throw TalentMissing("source_Governor282BD0");
            var officer = working.Officers.GetValueOrDefault(prepared.ExecutorOfficerId)
                ?? throw TalentMissing("executor_domain_owner");
            working = working with { Officers = new(working.Officers) { [officer.Id] = officer with
                { Behavior = current.ReadPersonBehavior(actor) } } };
            working = OfficerLocationSourceAdapter.PublishPosition(working, officer.Id, current);
            BindCurrent(working, current);
            return new(working, task, null);

            int ReadNoAdvisorSample()
            {
                ushort raw = Get(current.ReadAssignmentForceAdvisor52(forceId), "talent advisor Force52");
                var advisor = raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0);
                int p = Get(advisor?.PhysicalSlot, "talent advisor pointer");
                if (!Get(current.ReadPersonNativeValidity(p), "talent advisor validity")) return -1;
                throw TalentMissing("advisor2F39D0");
            }
            int Person(int logical) => current.ResolveOfficerSource(logical)
                ?? throw TalentMissing("Person_source_identity");
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    internal static int ReadTalentBookCost(GameState state, TalentOrderKind kind)
    {
        string key = kind == TalentOrderKind.Search ? TalentSearchBookCostKey :
            kind == TalentOrderKind.Recruit ? TalentRecruitBookCostKey : throw TalentMissing("command_kind");
        var bank = state.NativeArmySimulation?.Parameters ?? throw TalentMissing("current_parameters");
        return !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
            ? value : throw TalentMissing(key);
    }

    private static InvalidDataException TalentMissing(string field) => new("talent.creation." + field);
}
