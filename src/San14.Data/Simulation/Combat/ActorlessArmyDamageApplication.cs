using San14.Core;

namespace San14.Data;

/// <summary>The actual target-only178EC0 continuation shared by environment
/// stages22/23. State, ordered feedback and all mutable current owners remain
/// with the enclosing operation; there is no substitute attacker or new pool.</summary>
internal sealed class ActorlessArmyDamageApplication
{
    private readonly ArmyCampaignRuleAdapter package;
    private readonly SourceWorldContext current;
    private readonly NativeArmyProgressStageMoment moment;
    private readonly Func<GameState> latestState;
    private readonly Action<GameState> replaceState;
    private readonly ArmyTraitFeedback traits;
    private readonly Action ensurePersonnel;
    private readonly Action publishChangedPeople;
    private readonly Action<int> personChanged;
    private readonly List<CampaignEffectRequest> sink;
    private readonly Func<TacticSourceSamplerWorld> sampler;
    private readonly WorldIndexContext pool;

    internal ActorlessArmyDamageApplication(ArmyCampaignRuleAdapter package, SourceWorldContext current,
        NativeArmyProgressStageMoment moment, Func<GameState> latestState, Action<GameState> replaceState,
        ArmyTraitFeedback traits, Action ensurePersonnel, Action publishChangedPeople, Action<int> personChanged,
        List<CampaignEffectRequest> sink, Func<TacticSourceSamplerWorld> sampler)
    {
        this.package = package; this.current = current; this.moment = moment;
        this.latestState = latestState; this.replaceState = replaceState;
        this.traits = traits; this.ensurePersonnel = ensurePersonnel;
        this.publishChangedPeople = publishChangedPeople; this.personChanged = personChanged;
        this.sink = sink; this.sampler = sampler;
        pool = current.ReadOwnedCurrentListPool() ?? throw Missing("shared_pool");
    }

    /// <summary>Return178EC0's actual killed flag. A target skip returnsfalse;
    /// current-input failures remain Unknown and roll back the outer operation.
    /// The callback occupies the original1A3B20 point before1745B0.</summary>
    internal bool Apply(ArmyUnitReference army, int signedRequest, Action<ArmyUnitReference> refreshTarget)
    {
        int target = ArmyId(army);
        var atEntry = latestState();
        if (!atEntry.Units.ContainsKey(target) &&
            current.ReadArmyUnitNativeValidity(army) == true &&
            OwnedArmyDestructionCurrent.ReadArmyExcluded233560(atEntry, current,
                Need(army.PhysicalSlot, "Army_pointer")) == true)
            return false;
        var body = package.ApplyEnvironmentTargetNumbers(latestState(), current, target, signedRequest, moment,
            (state, image, adjustment) =>
            {
                //The body has already written ready/wounded. Route using that
                //latest image, not the operation's opening State snapshot.
                replaceState(state); BindQueue();
                var result = ArmyMoraleZeroRoutingCurrent.Apply(latestState(), current, army, adjustment,
                    package.TrapMobility(), Immediate);
                replaceState(result.State ?? throw Missing(result.UnresolvedOwner ?? "Object_morale_callback"));
                BindQueue();
                return (latestState(), result.Receipt ?? throw Missing("Object_morale_receipt"));
            });
        if (body.Outcome == NormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved)
            throw Missing(body.MissingPhase ?? "Object_target_body");
        replaceState(body.PendingCleanupState ?? body.State);
        BindQueue();
        if (body.Outcome == NormalArmyApplicationPrefixOutcome.NativeTargetSkipped) return false;
        bool killed = body.Receipt?.Killed ?? throw Missing("Object_killed_flag");
        if (!killed) ReactToDamage(army);
        refreshTarget(army);
        if (killed)
        {
            var state = latestState();
            var queue = state.PendingCombatSettlements ?? throw Missing("fatal_manager140");
            var request = new UnitDefeatRequest(target, null, null);
            var admission = CombatSettlementRules.Admit(queue, request, ArmyDestructionQueue.BindCurrent(state, current));
            if (admission.Kind is null) throw Missing(admission.MissingInput ?? "fatal1745B0");
            replaceState(admission.Kind == CombatSettlementAdmissionKind.Full ? Immediate(state, request)
                : state with { PendingCombatSettlements = admission.Queue });
            BindQueue();
        }
        return killed;
    }

    private void BindQueue()
    {
        var state = latestState();
        current.BindOwnedDestructionQueue(state.PendingCombatSettlements ?? throw Missing("manager140"), state);
    }

    private GameState Immediate(GameState value, UnitDefeatRequest request)
    {
        replaceState(value);
        publishChangedPeople();
        //This callback must preserve an already initialized/current manager.
        //Only the enclosing lazy personnel owner may initialize a missing one.
        ensurePersonnel();
        var state = latestState() with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
            ?? throw Missing("immediate_Person_manager") };
        replaceState(state);
        package.BindActorlessDestructionInputs(state, current);
        state = new OwnedArmyDestructionStage(package).ApplyBattleDefeatCurrent(state, current, pool,
            request, sampler(), moment.PreWriterThirdDate, sink, traits.Refresh, personChanged);
        replaceState(state);
        return state;
    }

    private int ArmyId(ArmyUnitReference army)
    {
        int physical = Need(army.PhysicalSlot, "Army_pointer");
        var arena = latestState().ReadArmyLifecycle() ?? throw Missing("Army_lifecycle");
        var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId) ?? throw Missing("Army_receiver");
        return receiver.ActiveUnitId ?? receiver.PendingImage?.Id ?? throw Missing("Army_campaign_identity");
    }

    private int Person(int raw) =>
        (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw Missing("Person_pointer");

    private void ReactToDamage(ArmyUnitReference army)
    {
        int person = Person(Need(current.ReadArmyUnitCommanderId(army), "reaction_Commander12"));
        if (!Need(current.ReadPersonNativeValidity(person), "reaction_Commander_validity")) return;
        for (int index = 0; index < 9; index++)
        {
            var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, index)
                ?? throw Missing("reaction_trait_pointer");
            if (!Need(current.ReadSkillPersonalityValidity(trait), "reaction_trait_validity") ||
                Need(current.ReadPersonalityByte(trait, 0xA4), "reaction_A4") != 30) continue;
            if (Need(current.ReadPersonTraitExtra1BE(person, index), "reaction_timer") != 0) continue;
            ushort mask = Need(current.ReadPersonTraitMask1BC(person), "reaction_mask");
            if ((mask & (1 << index)) != 0 || (Need(current.ReadSkillPersonalityFlags(trait), "reaction_flags") & 4) == 0) continue;
            ushort duration = Need(current.ReadPersonalityWord(trait, 0xB4), "reaction_duration");
            current.WritePersonTraitMask1BC(person, (ushort)(mask | (1 << index)));
            current.WritePersonTraitExtra1BE(person, index, (ushort)Math.Clamp((int)duration, 0, 720));
            personChanged(person);
            traits.RefreshAndFeedback(army, trait);
        }
    }

    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("army.progress.actorless-damage." + field);
}
