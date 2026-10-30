using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record NormalTurnStoryEventPreparation(GameState State, int[] EventIds,
    CampaignEventOccurrenceDate Date);
internal sealed record NormalTurnStoryEventSlice(GameState State, CampaignStoryRequest Story,
    CampaignStoryExecutionCursor Cursor, CampaignStoryOutcomes? Outcomes);

public sealed partial class ArmyCampaignRuleAdapter
{
    //3D8850 scans once before its ordered playback loop. The caller retains
    //the remaining IDs while a real CampaignStoryRequest is being consumed.
    internal NormalTurnStoryEventPreparation PrepareNormalTurnStoryEventsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, CampaignStoryBindings? bindings = null)
    {
        var season = state.NativeMapSeason ?? throw PostEconomyMissing("event_calendar");
        var date = new CampaignEventOccurrenceDate((ushort)Math.Clamp((int)season.ThirdDate.Year, 0, 350),
            (byte)Math.Clamp((int)season.ThirdDate.Month, 1, 12), (byte)Math.Clamp((int)season.ThirdDate.Day, 1, 30));
        var applicationContext = ReadApplicationGameplay();
        var mode = bindings?.ExternalMode ?? (applicationContext?.TutorialServicePresent switch
        {
            false => new TransferModeState(false, null),
            true => applicationContext.TutorialActive is bool active ? new TransferModeState(true, active ? 1 : 0) : null,
            _ => null
        }) ?? throw PostEconomyMissing("event_570850_owner");
        if (mode.Present && PostEconomyNeed(mode.Value, "event_570850_value") != 0)
            return new(state, [], date);
        bool application = PostEconomyNeed(bindings?.ApplicationPresent ?? applicationContext?.UserSelectionServicePresent,
            "event_application_pointer");
        int user = application ? PostEconomyNeed(bindings?.ApplicationUserIndex ?? state.CampaignEventWorld?.CurrentUserIndex,
            "event_application_user_index") : -1;
        var source = _campaignEvents.Value;
        if (state.ContentHashes.GetValueOrDefault(source.Source.ResourceUri) != source.Source.Sha256)
            throw PostEconomyMissing("event_resource_revision");
        BindCurrentCampaignEventQueries(state, current);
        current.ConditionApplicationPresent = application;
        var scan = S14EventEligibilityEvaluator.Scan(source.Document, current, new(0, 0, user),
            _ => new S14PlayerEventConditionRuntime(current, _ => ReadOrdinaryEventDispatchInputs(state), current));
        var blocked = Enumerable.Range(0, 992).Select(i => PostEconomyNeed(current.ReadManagerEventFlag(i), "event_manager_result")).ToArray();
        int selector = PostEconomyNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "event_settings_selector");
        int excludeHistory = PostEconomyNeed(BattleRemakeCurrentSettings.ReadInt32(state,
            selector == 0 ? 0x14C : 0x3C), "event_history_setting");
        var ids = scan.Results.Where(result => result.Status == S14EventEligibilityStatus.Satisfied &&
            BinaryPrimitives.ReadInt32LittleEndian(result.Record.RawBytes.AsSpan(8)) == 2 &&
            (excludeHistory == 0 || BinaryPrimitives.ReadInt32LittleEndian(result.Record.RawBytes.AsSpan(4)) != 1))
            .Select(result => result.Record.Id).ToArray();
        var working = state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
        return new(working with { CampaignEventEligibility = state.CampaignEventEligibility! with { Blocked = blocked, HasAvailableResult = scan.HasAvailableResult } }, ids, date);
    }

    private LoadedEvents? normalTurnStoryProgramOwner;
    private readonly Dictionary<int, CampaignEventExecutionProgram> normalTurnStoryPrograms = [];

    private CampaignEventExecutionProgram ReadNormalTurnStoryProgram(int eventId)
    {
        var loaded = _campaignEvents.Value;
        if (!ReferenceEquals(normalTurnStoryProgramOwner, loaded))
        {
            normalTurnStoryPrograms.Clear();
            normalTurnStoryProgramOwner = loaded;
        }
        if (!normalTurnStoryPrograms.TryGetValue(eventId, out var program))
            normalTurnStoryPrograms.Add(eventId, program = CampaignEventExecutionProgram.ReadLoadedEvent(loaded, eventId));
        return program;
    }

    //This is only the actual pre-execution identity barrier. It executes no
    //instruction, clears no invocation manager and touches no live audio.
    private CampaignStoryRequest PrepareNormalTurnStoryEntryCurrent(int eventId, long sequence)
    {
        var program = ReadNormalTurnStoryProgram(eventId);
        return new(sequence, program.Source, program.Revision, [], 0)
        { Scene = CampaignStoryScene.Empty with { OwnedEffects = [] } };
    }

    internal NormalTurnStoryEventSlice PrepareNormalTurnStoryEventCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int eventId, CampaignStoryBindings bindings, long sequence,
        CampaignStoryRequest? previous = null, CampaignStoryExecutionCursor? cursor = null, CampaignStoryOutcomes? outcomes = null)
    {
        //The original batch was selected once. Resuming an execution cursor
        //never reruns that scan, earlier leaves, or completed presentation.
        if (previous is null && cursor is null)
            state = CampaignHistoryPlaybackControl.Enter(state, executionMode: 0, eventId);
        BindCurrentCampaignEventQueries(state, current);
        var latest = state;
        _personRelationInputs.BindCurrent(() => latest, current);
        var inputs = BindCampaignStoryInputs(state, current, false);
        inputs = inputs with { ResolveMessageStringParameters = (bank, roots) =>
        {
            var prepared = PrepareStoryStringParametersCurrent(latest, current, bank, roots);
            latest = prepared.State;
            return prepared.Names;
        } };
        var program = ReadNormalTurnStoryProgram(eventId);
        if (previous is not null && (previous.Source != program.Source || previous.ContentRevision != program.Revision ||
            previous.Sequence != sequence || previous.NextStep != previous.Steps.Length))
            throw PostEconomyMissing("event_execution_resume_owner");
        int Relationship(int code, int first, int second, int third)
        {
            if (code == 418) return PersonSwornRelationsSource.CanCreate(latest, current, first, second, third) ? 1 : 0;
            var applied = PersonSwornRelationsSource.Apply(latest, current, first, second, third);
            latest = applied.State;
            return applied.NativeReturn;
        }
        var classification = new StoryPersonMutationClassification(current, previous is null ? CampaignStoryOutcomes.Empty : outcomes);
        int? Business(S14ConditionInstruction operation, CampaignEventExecutionContext invocation,
            Func<int, S14EventConditionForceReference?> targetForce)
        {
            int code = operation.Code;
            if (latest.StoryForceTransferContinuation is { } retainedTransfer && retainedTransfer.Code == code)
            {
                var resumedTransfer = ResumeStoryForceTransferOuterCurrent(latest, current, pool,
                    (force, person) => classification.RecordJoined(new(force), person),
                    new(null, null, new(null, null, eventId == 311 ? 1 : 0)));
                latest = resumedTransfer.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return resumedTransfer.NativeReturn;
            }

            var arguments = operation.RawArguments;
            if (code == 366)
            {
                var force = new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot);
                if (!PostEconomyNeed(current.ReadForceNativeValidity(force), "event_tribal_request_Force_validity")) return 0;
                var changed = ApplyStoryTribalRequestCurrent(latest, current, pool, force, Math.Max(arguments[1], 1));
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                //The source wrapper tailcalls2E8B70. Do not invent a constant1.
                return changed.NativeReturn;
            }
            if (code is 151 or 320 or 443)
            {
                BaseReference? selectedBase = code == 151 ? invocation.ResolveResultBase(arguments[0]) : null;
                int? firstPerson = code != 151 ? invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot : null;
                int? secondPerson = code == 320 ? invocation.ResolvePerson(arguments[1], targetForce).PhysicalSlot : null;
                var changed = ApplyStoryAppointmentFieldsCurrent(latest, current, code, selectedBase, firstPerson, secondPerson);
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is 352 or 349 or 350 or 356 or 302 or 303 or 304 or 301)
            {
                var changed = ApplyStoryArmyBusinessCurrent(latest, current, code, arguments,
                    selector => invocation.ResolvePerson(selector, targetForce).PhysicalSlot,
                    selector => new ForceReference(invocation.ResolveResultForce(selector, targetForce).PhysicalSlot),
                    code is 349 or 350 ? invocation.StoryCitySelections : null,
                    code is 302 or 303 or 304 ? invocation.ReadStoryAreaSelections() : null);
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is 137 or 138 or 284 or 439)
            {
                ForceReference? force = code == 284
                    ? new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot) : null;
                var changed = ApplyStoryCoalitionBusinessCurrent(latest, current, code, arguments, force,
                    code == 439 ? invocation.ReadStoryForceSelections() : null);
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code == 368)
            {
                if (PostEconomyNeed(current.ReadEventManagerPresent(), "event_clear_alliance_manager_presence"))
                    classification.ClearAllianceResults();
                return 1;
            }
            if (code == 384)
            {
                var force = new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot);
                if (PostEconomyNeed(current.ReadForceNativeValidity(force), "event_clear_joined_Force_validity") &&
                    PostEconomyNeed(current.ReadEventManagerPresent(), "event_clear_joined_manager_presence"))
                    classification.ClearJoinedResultsForForce(force);
                return 1;
            }
            if (code is 112 or 113 or 114)
            {
                int? person = code == 112 ? invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot : null;
                BaseReference? home = code == 113 ? invocation.ResolveResultBase(arguments[0]) : null;
                var changed = ApplyStoryEmperorCurrent(latest, current, code, person, home);
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code == 382)
            {
                var city = invocation.ResolveStoryCitySelector(arguments[0]);
                if (!PostEconomyNeed(current.ReadBaseNativeValidity(city), "event_forced_Emperor_City_validity")) return 0;
                int person = invocation.ResolvePerson(arguments[1], targetForce).PhysicalSlot;
                //This method runs the actual ordinary execution-mode0 stream.
                //83C100 installs this invocation's +10 before reaching business;
                //the source event identity is already part of the saved owner.
                var emperorInputs = new StoryForcedEmperorCaptureInputs(null, null, eventId == 311 ? 1 : 0);
                var changed = ApplyStoryEmperorOwnershipCurrent(latest, current, pool, city, person, arguments[2],
                    (force, joined) => classification.RecordJoined(new(force), joined), emperorInputs);
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is 141 or 150 or 251)
            {
                BaseReference? firstCity = null, secondCity = null;
                ForceReference? firstForce = null, secondForce = null;
                if (code == 141) firstCity = invocation.ResolveStoryCitySelector(arguments[1]);
                else if (code == 150)
                {
                    firstCity = invocation.ResolveStoryCitySelector(arguments[0]);
                    secondCity = invocation.ResolveStoryCitySelector(arguments[1]);
                }
                else
                {
                    firstForce = new(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot);
                    secondForce = new(invocation.ResolveResultForce(arguments[1], targetForce).PhysicalSlot);
                }
                var changed = ApplyStoryBaseRemapCurrent(latest, current, pool, code, arguments,
                    firstCity, secondCity, firstForce, secondForce,
                    (force, joined) => classification.RecordJoined(new(force), joined));
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is 170 or 254 or 330 or 380 or 445)
            {
                var changed = ApplyStoryPersonItemStateCurrent(latest, current, code, arguments,
                    selector => invocation.ResolvePerson(selector, targetForce).PhysicalSlot,
                    selector => new ForceReference(invocation.ResolveResultForce(selector, targetForce).PhysicalSlot));
                latest = changed.State;
                foreach (var captured in changed.OrderedCaptured)
                    classification.RecordCaptured(captured.Force, captured.PersonPhysicalSlot);
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is >= 127 and <= 131)
                return invocation.SetStoryGovernmentRepresentative(code,
                    ReadStoryGovernmentRepresentativeCurrent(latest, current, code));
            if (code is 164 or 165 or 319)
            {
                int firstPerson = invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot;
                int secondPerson = code == 165 ? 0 : invocation.ResolvePerson(arguments[1], targetForce).PhysicalSlot;
                var changed = ApplyStoryPersonRelationsCurrent(latest, current, code, firstPerson, secondPerson);
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code == 315)
            {
                //Each source lookup precedes its own manager removal. Do not
                //collapse repeated operands or pre-resolve all six pointers.
                for (int index = 0; index < 6; index++)
                {
                    int person = invocation.ResolvePerson(arguments[index], targetForce).PhysicalSlot;
                    if (PostEconomyNeed(current.ReadPersonNativeValidity(person), "event_remove_result_Person_validity") &&
                        PostEconomyNeed(current.ReadEventManagerPresent(), "event_remove_result_manager_presence"))
                        classification.RemovePersonResults(person);
                }
                return 1;
            }
            if (code is 163 or 270 or 256 or 287)
            {
                int? person = code == 163 ? invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot : null;
                ForceReference? force = code != 163
                    ? new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot) : null;
                Func<IReadOnlyList<int>>? preferred = code == 270 ? () => arguments.Skip(1).Take(7)
                    .Select(value => invocation.ResolvePerson(value, targetForce).PhysicalSlot).ToArray() : null;
                var changed = ApplyStoryPersonAppointmentCurrent(latest, current, code, arguments, person, force,
                    code == 256 ? invocation.Candidates.Select(value => value.PhysicalSlot).ToArray() : null,
                    preferred, code == 270 ? invocation.StoryCitySelections : null,
                    classification.RecordFreed, classification.RecordJoined);
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is 171 or 359 or 351 or 323 or 286 or 211 or 212 or 213)
            {
                ForceReference? force = code is 351 or 323 or 212
                    ? new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot) : null;
                BaseReference? target = code == 286 ? invocation.ResolveResultBase(arguments[0]) : null;
                var changed = ApplyStoryCityArmyCurrent(latest, current, code, arguments,
                    value => invocation.ResolvePerson(value, targetForce).PhysicalSlot, force, target,
                    code is 211 or 212 or 213 ? invocation.StoryCitySelections : null,
                    code is 351 or 323 ? invocation.ReadStoryAreaSelections() : null,
                    (joinedForce, person) => classification.RecordJoined(new(joinedForce), person));
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is >= 184 and <= 187)
            {
                var force = new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot);
                ForceReference? leader = code == 186
                    ? new ForceReference(invocation.ResolveResultForce(arguments[1], targetForce).PhysicalSlot) : null;
                var members = invocation.ReadStoryForceSelections() ?? throw PostEconomyMissing("event_Force_list");
                var changed = ApplyStoryForceListRelationsCurrent(latest, current, code, arguments,
                    force, leader, members, classification.RecordAlliance);
                latest = changed.State;
                if (changed.Effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw PostEconomyMissing("event_Force_list_effect_owner")).Append(
                            CampaignEffectSource.MonthlyMaintenance,
                            latest.NativeClock?.UpdateOrdinal ?? throw PostEconomyMissing("event_Force_list_effect_revision"),
                            changed.Effects) };
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is 116 or 117 or 118 or 119)
            {
                ForceReference lifecycleFirst;
                ForceReference? lifecycleSecond = null;
                int? person = null;
                if (code == 117 && latest.StoryForceAbsorptionTail is { } savedAbsorption)
                {
                    lifecycleFirst = new(savedAbsorption.OriginalForcePhysicalSlot);
                    lifecycleSecond = new(savedAbsorption.RecipientForcePhysicalSlot);
                }
                else
                {
                    lifecycleFirst = new(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot);
                    if (code is 116 or 117)
                        lifecycleSecond = new(invocation.ResolveResultForce(arguments[1], targetForce).PhysicalSlot);
                    else person = invocation.ResolvePerson(arguments[1], targetForce).PhysicalSlot;
                }
                var changed = ApplyStoryForceLifecycleCurrent(latest, current, code, arguments,
                    lifecycleFirst, lifecycleSecond, person, classification.RecordFreed, classification.RecordJoined,
                    new(null, null, new(null, null, eventId == 311 ? 1 : 0)));
                latest = changed.State;
                if (code == 117 && latest.StoryForceAbsorptionTail is not null) return null;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is >= 193 and <= 202)
            {
                ForceReference? force = code == 201
                    ? new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot) : null;
                var changed = ApplyStoryAreaBusinessCurrent(latest, current, pool, code, arguments,
                    invocation.ReadStoryAreaSelections(), force);
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is 149 or 299)
            {
                BaseReference? targetBase = null;
                int? person = null;
                ForceReference? oldForce = null, newForce = null;
                IReadOnlyList<int>? areas = null;
                if (code == 149)
                {
                    targetBase = invocation.ResolveResultBase(arguments[0]);
                    person = invocation.ResolvePerson(arguments[1], targetForce).PhysicalSlot;
                }
                else
                {
                    oldForce = new(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot);
                    newForce = new(invocation.ResolveResultForce(arguments[1], targetForce).PhysicalSlot);
                    areas = invocation.ReadStoryAreaSelections();
                }
                var territory = ApplyStoryTerritoryBusinessCurrent(latest, current, pool, code, arguments,
                    targetBase, person, oldForce, newForce, areas,
                    (force, joined) => classification.RecordJoined(new(force), joined));
                latest = territory.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return territory.NativeReturn;
            }
            if (code == 162)
            {
                var pending = invocation.SinglePersonDeathContinuation;
                int person = pending?.PersonPhysicalSlot ?? invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot;
                var death = ApplyStorySinglePersonDeathCurrent(latest, current, person, pending, classification);
                latest = death.State;
                invocation.SinglePersonDeathContinuation = death.Continuation;
                if (death.NativeReturn is null) return null;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return death.NativeReturn;
            }
            if (code is 153 or 154 or 155 or 156)
            {
                //Resolve each operand in the source call order.155 uses two Bases;
                //156 uses two People and its writer reads the second current Home.
                int? firstPerson = null, secondPerson = null;
                BaseReference? firstBase = null, secondBase = null;
                ForceReference? force = null;
                if (code == 153)
                {
                    firstPerson = invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot;
                    force = new(invocation.ResolveResultForce(arguments[1], targetForce).PhysicalSlot);
                }
                else if (code == 154)
                {
                    firstBase = invocation.ResolveResultBase(arguments[0]);
                    firstPerson = invocation.ResolvePerson(arguments[1], targetForce).PhysicalSlot;
                }
                else if (code == 155)
                {
                    firstBase = invocation.ResolveResultBase(arguments[0]);
                    secondBase = invocation.ResolveResultBase(arguments[1]);
                }
                else
                {
                    firstPerson = invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot;
                    secondPerson = invocation.ResolvePerson(arguments[1], targetForce).PhysicalSlot;
                }
                var moved = ApplyStoryPersonDirectMovementCurrent(latest, current, code, firstPerson, secondPerson,
                    firstBase, secondBase, force, classification.RecordJoined);
                latest = moved.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return moved.NativeReturn;
            }
            if (code is 245 or 252 or 285 or 294 or 295)
            {
                ForceReference? force = code is 252 or 294
                    ? new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot) : null;
                BaseReference? home = code is 285 or 295 ? invocation.ResolveResultBase(arguments[0]) : null;
                latest = ApplyStoryUnitLifecycleCurrent(latest, current, code, arguments,
                    selector => invocation.ResolvePerson(selector, targetForce).PhysicalSlot, force, home);
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return 1;
            }
            if (code is 158 or 159 or 160)
            {
                int person = invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot;
                var changed = ApplyStoryPersonValuesCurrent(latest, current, code, arguments, person);
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is >= 232 and <= 238)
            {
                int? person = code is 234 or 236
                    ? invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot : null;
                BaseReference? home = code == 235 ? invocation.ResolveResultBase(arguments[0]) : null;
                var changed = ApplyStoryPersonListMutationCurrent(latest, current, code, arguments,
                    invocation.Candidates.Select(value => value.PhysicalSlot).ToArray(), person, home,
                    invocation.PersonListContinuation, classification);
                latest = changed.State;
                invocation.PersonListContinuation = changed.Continuation;
                if (changed.OrderedPeople is { } ordered) invocation.ReplaceOrderedPeople(ordered);
                if (changed.NativeReturn is null) return null;
                //Current field overrides stay attached to the same operation;
                //only lazily evaluated ability/service readers need fresh inputs.
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code == 120)
            {
                var force = new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot);
                int kind = ReadCampaignServiceRecordKind(_campaignEvents.Value.Document, eventId);
                bool modal = kind != 1 && PostEconomyNeed(bindings.Audio.ReadWorldField1AC(), "event_force_rank_modal_suppression") == 0 &&
                    PostEconomyNeed(bindings.ModalParentPresent, "event_force_rank_modal_parent");
                var rank = ApplyStoryForceRankCurrent(latest, current, arguments, force, eventId, kind, modal,
                    invocation.ForceRankContinuation);
                latest = rank.State;
                invocation.ForceRankContinuation = rank.Continuation is { } request
                    ? request with { Source = program.Source, StorySequence = sequence,
                        OperationIndex = operation.Index, SourceOffset = operation.Offset } : null;
                if (rank.Effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw PostEconomyMissing("event_force_rank_effect_owner")).Append(CampaignEffectSource.MonthlyMaintenance,
                            latest.NativeClock?.UpdateOrdinal ?? throw PostEconomyMissing("event_force_rank_effect_revision"), rank.Effects) };
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return rank.NativeReturn;
            }
            if (code is 263 or 267 or 289)
            {
                BaseReference? home = code == 289 ? null : invocation.ResolveResultBase(arguments[0]);
                var released = ApplyStoryPrisonerReleaseCurrent(latest, current, code, arguments, home,
                    code == 289 ? invocation.StoryCitySelections : null);
                latest = released.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return released.NativeReturn;
            }
            if (code is 115 or 446)
            {
                int ruler = invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot;
                var home = invocation.ResolveStoryCitySelector(arguments[1]);
                var created = ApplyStoryForceCreationCurrent(latest, current, pool, code, arguments, ruler, home,
                    classification.RecordRaisedArmy);
                latest = created.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return created.NativeReturn;
            }
            if (code == 121)
            {
                var force = new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot);
                var experience = ApplyStoryForceExperienceCurrent(latest, current, arguments, force);
                latest = experience.State;
                if (experience.Effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw PostEconomyMissing("event_force_experience_effect_owner")).Append(
                            CampaignEffectSource.MonthlyMaintenance,
                            latest.NativeClock?.UpdateOrdinal ?? throw PostEconomyMissing("event_force_experience_effect_revision"),
                            experience.Effects) };
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return experience.NativeReturn;
            }
            if (code is 209 or 210 or >= 305 and <= 310 or 312 or 383)
            {
                BaseReference? target = code is 308 or 309 or 310 ? invocation.ResolveStoryCitySelector(arguments[0]) : null;
                var resources = ApplyStoryCityListResourcesCurrent(latest, current, code, arguments, invocation.StoryCitySelections, target);
                latest = resources.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return resources.NativeReturn;
            }
            if (code is 168 or 259 or 260 or 449 or 450)
            {
                int? person = code is 259 or 449 or 450
                    ? invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot : null;
                ForceReference? recovery = code == 168
                    ? new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot) : null;
                IReadOnlyList<int> people = code == 260
                    ? invocation.Candidates.Select(value => value.PhysicalSlot).ToArray() : Array.Empty<int>();
                var health = ApplyStoryPersonHealthCurrent(latest, current, code, arguments, person, recovery, people);
                latest = health.State;
                foreach (var change in health.OrderedStatusChanges) classification.RecordStatus(change.PersonPhysicalSlot, change.Status);
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return health.NativeReturn;
            }
            if (code is 166 or 377 or 426 or 447 or 448)
            {
                var item = ApplyStoryItemMutationCurrent(latest, current, code, arguments,
                    selector => invocation.ResolvePerson(selector, targetForce).PhysicalSlot);
                latest = item.State;
                foreach (var gain in item.OrderedItemGains) classification.RecordItemGain(gain.PersonPhysicalSlot, gain.ItemPhysicalSlot);
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return item.NativeReturn;
            }
            if (code is 152 or 247 or 365)
            {
                BaseReference? city = code == 247 ? null : invocation.ResolveStoryCitySelector(arguments[0]);
                var changed = ApplyStoryCityBusinessCurrent(latest, current, code, arguments, city, classification.RecordTroopIncrease);
                latest = changed.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return changed.NativeReturn;
            }
            if (code is 428 or 430)
            {
                int? person = code == 428 && arguments[1] > 0
                    ? invocation.ResolvePerson(arguments[0], targetForce).PhysicalSlot : null;
                var merit = ApplyStoryPersonMeritCurrent(latest, current, code, arguments, person,
                    invocation.Candidates.Select(value => value.PhysicalSlot).ToArray());
                latest = merit.State;
                foreach (int changedPerson in merit.OrderedMeritPersonSlots) classification.RecordMerit(changedPerson);
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return merit.NativeReturn;
            }
            if (code is >= 122 and <= 126)
            {
                var firstForce = new ForceReference(invocation.ResolveResultForce(arguments[0], targetForce).PhysicalSlot);
                ForceReference? secondForce = code == 122 ? null :
                    new ForceReference(invocation.ResolveResultForce(arguments[1], targetForce).PhysicalSlot);
                var relation = ApplyStoryForceRelationsCurrent(latest, current, code, arguments, firstForce, secondForce,
                    classification.RecordAlliance);
                latest = relation.State;
                invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
                _personRelationInputs.BindCurrent(() => latest, current);
                return relation.NativeReturn;
            }
            var first = invocation.ResolveStoryResourceBase(code, arguments[0]);
            BaseReference? second = code is 146 or 147 or 148 ? invocation.ResolveResultBase(arguments[1]) : null;
            var resource = ApplyStoryBaseResourcesCurrent(latest, current, code, arguments, first, second);
            latest = resource.State;
            invocation.RefreshResultInputs(BindCampaignStoryInputs(latest, current, false));
            _personRelationInputs.BindCurrent(() => latest, current);
            return resource.NativeReturn;
        }
        var slice = CampaignEventExecution.StageUntilRelationship(program, inputs,
            _ => ReadOrdinaryEventDispatchInputs(latest), bindings.Audio, cursor, Relationship, Business,
            classification.RecordCurrentPlayerCityIncrease);
        var chunk = CampaignStorySemantics.Bind(slice.Plan, sequence, allowEmpty: true);
        var story = previous is null ? chunk : previous with
        {
            Steps = previous.Steps.Concat(chunk.Steps).ToArray(), PreparedStep = null
            //Scene, prepared retained text, actors, owned effects, music and
            //TextLog remain the exact state consumed by the real host.
        };
        return new(latest, story, slice.Cursor, classification.Current);
    }

    internal GameState CompleteNormalTurnStoryEventCurrent(GameState state, SourceWorldContext current,
        int eventId, CampaignEventOccurrenceDate date)
    {
        //83C100's real playback exit maintenance precedes the caller's
        //occurrence/profile tail. Manual command completion uses this same tail.
        state = RefreshAreaOccupationClaimsCurrent(state, current);
        var history = state.CampaignEvents ?? throw PostEconomyMissing("event_history");
        if (eventId is < 0 or >= 1024) throw PostEconomyMissing("event_id");
        var occurred = history.Occurred.ToArray();
        var dates = history.OccurrenceDates.ToArray();
        occurred[eventId] = true; dates[eventId] = date;
        current.SetWorldEventFlag(eventId, true);
        current.EventConditions.SetOccurrenceDate(eventId, new(date.Year, date.Month, date.Day));
        return state with { CampaignEvents = history with { Occurred = occurred, OccurrenceDates = dates } };
    }
}
