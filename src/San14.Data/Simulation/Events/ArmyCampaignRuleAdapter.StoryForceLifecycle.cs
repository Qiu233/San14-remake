using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary116..119, after the same invocation has resolved its
    /// actual Force/Person pointers. No editor or command-menu admission.</summary>
    internal (GameState State, int NativeReturn) ApplyStoryForceLifecycleCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments,
        ForceReference first, ForceReference? second, int? targetPerson,
        Action<int>? recordFreed = null, Action<ForceReference, int>? recordJoined = null,
        AutomaticEmperorCaptureInputs? emperorInputs = null)
    {
        if (arguments.Count != 8 || code is < 116 or > 119) throw StoryForceLifecycleMissing("operation");
        var pool = current.ReadOwnedCurrentListPool() ?? throw StoryForceLifecycleMissing("current_pool");
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw StoryForceLifecycleMissing("current_pool_binding");
        var working = state;
        var effects = new List<CampaignEffectRequest>();
        if (code == 117 && working.StoryForceAbsorptionTail is { } resumed)
            return ResumeStoryForceAbsorptionCallerCurrent(working, current, pool, resumed, first,
                second ?? throw StoryForceLifecycleMissing("resume_second_Force"), recordJoined, emperorInputs);
        if (code is 116 or 117)
        {
            var recipient = second ?? throw StoryForceLifecycleMissing("second_Force");
            if (Valid(first) && Valid(recipient))
            {
                BindStoryForceLifecycleCurrent(working, current);
                if (code == 116)
                {
                    var date = working.NativeMapSeason?.ThirdDate ?? throw StoryForceLifecycleMissing("source_date");
                    working = new ForceUnregisterSource(this).ApplyStoryDestructionCurrent(
                        working, current, pool, first, recipient, date, effects);
                }
                else
                {
                    bool manager = StoryForceLifecycleNeed(current.ReadEventManagerPresent(), "event_manager_presence");
                    if (manager) SetRecordingGate(true);
                    //The mature2A2760 owns its ordered effect sink. It receives
                    //the exact source flag1: suppress elimination presentation.
                    working = ApplyNormalTurnForceAbsorptionCurrent(working, current, pool,
                        first, recipient, suppressEliminationPresentation: true, emperorInputs: emperorInputs,
                        eventPlaybackPresent: true);
                    if (HasPendingForceAbsorptionCurrent(working))
                        return (working with { StoryForceAbsorptionTail = new(
                            StoryForceLifecycleNeed(first.PhysicalSlot, "paused_first_Force"),
                            StoryForceLifecycleNeed(recipient.PhysicalSlot, "paused_second_Force"), manager) }, 0);
                    //3A4A80 reaches28E7F0, not a second28E6D0. Borrow its real
                    //map-object retirement owner, including visual detachment.
                    working = RetireDiplomacyAreaObjects(working, current, pool, effects);
                    if (manager) SetRecordingGate(false);
                }
                //The source constructs this mode0 iterator AFTER unregister/
                //absorption. Do not classify a saved before-list or a World diff.
                int force = StoryForceLifecycleNeed(current.ReadForcePhysicalSlot(first), "notification_Force60");
                var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person)
                    ?? throw StoryForceLifecycleMissing("notification_Person100");
                var node = current.ReadRegistryHead(descriptor) ?? throw StoryForceLifecycleMissing("notification_head");
                while (node.Identity.HasValue)
                {
                    var payload = current.ReadRegistryNodePayload(node) ?? throw StoryForceLifecycleMissing("notification_payload");
                    if (payload is not { Kind: RegistryKind.Person, PhysicalSlot: int person })
                        throw StoryForceLifecycleMissing("notification_Person_pointer");
                    if (force is >= 1 and <= 51 && StoryForceLifecycleNeed(current.ReadPersonOldForceSlot(person), "notification_Person_Force") == force &&
                        StoryForceLifecycleNeed(current.ReadPersonStatus(person), "notification_PersonE6") is >= 1 and <= 4 &&
                        StoryForceLifecycleNeed(current.ReadPersonNativeValidity(person), "notification_Person_validity") &&
                        StoryForceLifecycleNeed(current.ReadEventManagerPresent(), "notification_manager_presence"))
                    {
                        if (code == 116) (recordFreed ?? throw StoryForceLifecycleMissing("free_person_recorder"))(person);
                        else (recordJoined ?? throw StoryForceLifecycleMissing("joined_person_recorder"))(recipient, person);
                    }
                    node = current.ReadRegistryNodeNext(node) ?? throw StoryForceLifecycleMissing("notification_next");
                }
            }
            //Both outer handlers call33E2E0 even for invalid operands and
            //after a true27A6B0 terminal. No invented early-return guard.
            working = RefreshEditedEventRegionStatistics(working, current);
            working = PublishNormalTurnCurrent(working, current, pool);
            working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
            if (effects.Count != 0)
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw StoryForceLifecycleMissing("effect_queue")).Append(CampaignEffectSource.MonthlyMaintenance,
                        working.NativeClock?.UpdateOrdinal ?? throw StoryForceLifecycleMissing("effect_revision"), effects) };
            return (working, 1);
        }

        int selected = targetPerson ?? throw StoryForceLifecycleMissing("selected_Person");
        if (code == 118)
        {
            if (Valid(first) && StoryForceLifecycleNeed(current.ReadPersonNativeValidity(selected), "new_ruler_validity"))
            {
                BindStoryForceLifecycleCurrent(working, current);
                ushort identity = StoryForceLifecycleNeed(current.ReadForceRecord(StoryForceLifecycleNeed(current.ReadForcePhysicalSlot(first), "ruler_Force60"))?.RawRuler10,
                    "old_ruler_identity");
                int old = StoryForceLifecycleNeed((identity <= 6000 ? current.ReadPersonLookupReference(identity) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
                    "old_ruler_pointer");
                var retained = current.CreateTemporaryHexList() ?? throw StoryForceLifecycleMissing("ruler_Person_temporary");
                try { working = new PersonDeathSource(this, working, current, pool).ApplyStoryRulerTransfer(old, selected, retained); }
                finally { current.ClearTemporaryHexList(retained); current.ReleaseTemporaryHexList(retained); }
                working = PublishNormalTurnCurrent(working, current, pool);
                working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
            }
        }
        else if (StoryForceLifecycleNeed(current.ReadPersonNativeValidity(selected), "advisor_Person_validity"))
        {
            //3A83F0 has NO Force validity, membership, office, equality or
            //GovernanceRoster refresh gate. Only current intelligence qualifies.
            _baseCapacityInputs.BindCurrent(working, current);
            ushort id = StoryForceLifecycleNeed(current.ReadPersonIdentity(selected), "advisor_Person10");
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            int intelligence = StoryForceLifecycleNeed(abilities.ResolveDimension(working, id, 2,
                (owner, effect) => _baseCapacityInputs.ReadPanelModifier(working, owner, effect)), "advisor_current_intelligence");
            int threshold = StoryForceLifecycleNeed(GovernanceParameter(working, GovernanceRosterParameters.AdvisorIntelligenceMinimum), "advisor_intelligence_minimum");
            if (intelligence >= threshold)
            {
                int force = StoryForceLifecycleNeed(current.ReadForcePhysicalSlot(first), "advisor_Force60");
                var native = working.NativeArmySimulation ?? throw StoryForceLifecycleMissing("advisor_saved_owner");
                var values = new Dictionary<string, int>(native.Parameters.Int32Values);
                ushort advisor = (ushort)Math.Min((int)id, 6000);
                Write(0x52, advisor);
                current.SetAssignmentForceAdvisor52(force, advisor);
                for (int group = 0; group < 5; group++)
                    for (int column = 0; column < 7; column++)
                    {
                        string key = BattleRemakeCurrentForcePolicies.DepartmentPersonKey(force, group, column);
                        if (!values.TryGetValue(key, out int existing) || existing is < 0 or > ushort.MaxValue)
                            throw StoryForceLifecycleMissing("advisor_department_word:" + key);
                        if (existing == id) Write(0x54 + group * 30 + column * 2, 0);
                    }
                working = working with { NativeArmySimulation = working.NativeArmySimulation! with
                    { Parameters = native.Parameters with { Int32Values = values } } };
                void Write(int offset, int value)
                {
                    values[BattleRemakeCurrentForcePolicies.ForceFieldKey(force, offset)] = value;
                    if (working.NativeForceUnregisterCurrent is { } reset && reset.ForceResetImages.TryGetValue(force, out var before))
                    {
                        var image = before.ToArray(); image[offset] = (byte)value; image[offset + 1] = (byte)(value >> 8);
                        working = working with { NativeForceUnregisterCurrent = reset with
                            { ForceResetImages = new(reset.ForceResetImages) { [force] = image } } };
                    }
                }
            }
        }
        return (working, 1);

        bool Valid(ForceReference force) => StoryForceLifecycleNeed(current.ReadForceNativeValidity(force), "Force_validity");
        void SetRecordingGate(bool blocked)
        {
            current.ReportBlockerState = new(true, blocked ? 1 : 0);
            working = working with { CampaignEventEligibility = (working.CampaignEventEligibility
                ?? throw StoryForceLifecycleMissing("event_manager_owner")) with { HistoryRecordingSuppressed = blocked } };
        }
    }

    private void BindStoryForceLifecycleCurrent(GameState state, SourceWorldContext current)
    {
        //Only the reached operation's omitted business windows. Never restore
        //a second pool over the live Story invocation or its retained lists.
        _personDestructionInputs.BindCurrent(state, current);
        _personRelationInputs.BindCurrent(() => state, current);
        _force164Inputs.BindCurrent(state, current);
        _moraleWaveInputs.BindCurrent(state, current);
        PersonArmyCaptureInputs.BindCurrent(state, current);
        RulerCaptureReleaseSource.BindCurrent(state, current);
        CaptureBaseThreatInputs.BindCurrent(state, current);
        _personAssignmentPrecallInputs.BindCurrent(state, current);
        _baseCapacityInputs.BindCurrent(state, current);
        PersonAdvisorResetSource.BindCurrent(state, current);
        ForceUnregisterInputs.BindCurrent(state, current);
        BaseCaptureCurrentInputs.BindCurrent(state, current);
        OwnedArmyReturnServiceInputs.BindCurrent(state, current);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        if (current.ReadOwnedPersonCommandManager() is null) PersonReturnCommands.BindCurrent(state, current);
        current.BaseForcePolicyEffectReader = (force, effect) => ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, effect, true).Value;
    }

    internal sealed partial class PersonDeathSource
    {
        internal GameState ApplyStoryRulerTransfer(int oldRuler, int newRuler, HexListHandle retainedPeople)
        {
            ApplyRulerSuccessionTransfer(oldRuler, newRuler, retainedPeople, refreshForce: true);
            return latest;
        }
    }

    private static T StoryForceLifecycleNeed<T>(T? value, string field) where T : struct => value ?? throw StoryForceLifecycleMissing(field);
    private static InvalidDataException StoryForceLifecycleMissing(string field) => new("story.force_lifecycle." + field);
}
