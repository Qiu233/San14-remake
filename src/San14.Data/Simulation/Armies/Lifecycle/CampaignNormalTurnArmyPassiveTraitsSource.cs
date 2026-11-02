using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>NormalTurn431030: collect the three actual Army lists before
    /// applying recovery, morale and local security in that order.</summary>
    internal GameState ApplyNormalTurnArmyPassiveTraitsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        // Recovery reads the same current office/rank owner as departure.
        // The normal Turn borrows a bare writable route context.
        _moraleWaveInputs.BindCurrent(state, current);
        return new NormalTurnArmyPassiveTraits(this, state, current, pool).Apply();
    }

    private sealed class NormalTurnArmyPassiveTraits(ArmyCampaignRuleAdapter package,
        GameState initial, SourceWorldContext current, WorldIndexContext pool)
    {
        //TC typed/data.tsv's file-backed static definitions, all signed i32.
        //Recovery is a percent; the other two values scale their own units.
        private const int WoundedRecoveryPercentPerPoint = 1; //1A4AC64
        private const int MoralePerPoint = 1;                 //1A4AC70
        private const int SecurityLossPerPoint = 1;           //1A4AC7C
        private GameState state = initial;
        private readonly List<CampaignEffectRequest> effects = [];
        private readonly List<CampaignEffectRequest> refreshes = [];
        private readonly HashSet<int> changedPeople = [];
        private TacticSourceSamplerWorld? sampler;

        internal GameState Apply()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !ReferenceEquals(current.ReadOwnedCurrentListPool(), pool))
                throw Missing("shared_current_pool");
            HexListHandle? recovery = null, morale = null, security = null;
            try
            {
                recovery = Temporary(); morale = Temporary(); security = Temporary();
                var manager = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw Missing("Army60_descriptor");
                var node = current.ReadRegistryHead(manager) ?? throw Missing("Army60_head");
                while (node.Identity.HasValue)
                {
                    var army = Army(node);
                    if (Valid(army))
                    {
                        var values = Contributions(army);
                        if (values[62] != 0) AppendDistinct(recovery, army);
                        if (values[80] != 0) AppendDistinct(morale, army);
                        if (values[95] != 0) AppendDistinct(security, army);
                        //2C8DA0 tests cached36 effect presence, independently
                        //of its signed contribution and commander activation.
                        if (CachedHasEffect(army, 62)) AppendDistinct(recovery, army);
                        if (CachedHasEffect(army, 80)) AppendDistinct(morale, army);
                    }
                    node = current.ReadRegistryNodeNext(node) ?? throw Missing("Army60_next");
                }
                foreach (var army in Armies(recovery)) RecoverWounded(army);
                foreach (var army in Armies(morale)) AdjustMorale(army);
                foreach (var army in Armies(security)) AdjustSecurity(army);
            }
            finally
            {
                //The native routine releases95,80,62 descriptors in this order.
                if (security is not null) Release(security);
                if (morale is not null) Release(morale);
                if (recovery is not null) Release(recovery);
            }
            PublishPeople();
            if (effects.Count != 0 || refreshes.Count != 0)
            {
                var pending = state.CampaignEffectRequests ?? throw Missing("effect_owner");
                long revision = state.NativeClock?.UpdateOrdinal ?? throw Missing("effect_revision");
                if (effects.Count != 0) pending = pending.Append(CampaignEffectSource.ArmyBattleDefeat, revision, effects);
                if (refreshes.Count != 0) pending = pending.Append(CampaignEffectSource.ArmyTraitMaintenance, revision, refreshes);
                state = state with { CampaignEffectRequests = pending };
            }
            return state;
        }

        private int[] Contributions(ArmyUnitReference army)
        {
            //26A740 zeros all127 entries before its Army-validity test. It
            //does not add a validity test for the commander's Person pointer.
            if (!Valid(army)) return new int[127];
            int commander = Person(K(current.ReadArmyUnitCommanderId(army), "Army12"));
            return TacticRemakeCurrentTraits.ArmyContributions(current, commander, Traits(army).Traits64)
                ?? throw Missing("Army_26A740_mode0_param5zero");
        }
        private ArmyReturnTraitFields Traits(ArmyUnitReference army)
            => current.ReadArmyReturnTraitFields(army) ?? throw Missing("Army_cached36");
        private PersonalityReference Trait(ushort raw)
            => current.ReadPersonalityTableReference(raw < 356 ? raw : 0) ?? throw Missing("cached_trait_lookup");
        private bool TraitHasEffect(PersonalityReference trait, int effect)
        {
            if (!K(current.ReadSkillPersonalityValidity(trait), "cached_trait_validity")) return false;
            return K(current.ReadPersonalityByte(trait, 0xb6), "cached_trait_B6") == effect ||
                K(current.ReadPersonalityByte(trait, 0xba), "cached_trait_BA") == effect;
        }
        private bool CachedHasEffect(ArmyUnitReference army, int effect)
        {
            foreach (ushort raw in Traits(army).Traits64)
                if (TraitHasEffect(Trait(raw), effect)) return true;
            return false;
        }

        private void RecoverWounded(ArmyUnitReference army)
        {
            int contribution = Contributions(army)[62];
            var before = current.ReadArmyReturnResourceFields(army) ?? throw Missing("recovery_Army_resources");
            int ready = K(before.Ready16, "recovery_ready16"), wounded = K(before.Wounded18, "recovery_wounded18");
            int amount = 0;
            int percentage = unchecked(contribution * WoundedRecoveryPercentPerPoint);
            if (percentage > 0)
            {
                amount = unchecked(wounded * contribution * WoundedRecoveryPercentPerPoint) / 100;
                int commander = Person(K(current.ReadArmyUnitCommanderId(army), "recovery_Commander12"));
                int capacityRoom = 0;
                if (K(current.ReadPersonNativeValidity(commander), "recovery_Commander_validity"))
                {
                    var capacity = OfficerCommandCapacity.Resolve(state, current, commander);
                    capacityRoom = unchecked((capacity.Capacity ?? throw Missing(capacity.UnresolvedInput ?? "recovery_command_capacity")) - ready);
                }
                //The original signed min may be negative when ready exceeds
                //capacity. That moves troops back to wounded before the stores.
                amount = Math.Min(Math.Min(amount, wounded), capacityRoom);
            }
            ushort afterReady = (ushort)Math.Clamp(unchecked(ready + amount), 0, 65535);
            ushort afterWounded = (ushort)Math.Clamp(unchecked(wounded - amount), 0, 20000);
            int physical = K(army.PhysicalSlot, "recovery_Army_receiver");
            current.BindArmyReturnResourceFields(physical, before with { Ready16 = afterReady, Wounded18 = afterWounded });
            PublishTroops(physical, afterReady, afterWounded);
            current.AcknowledgeArmyReturnNumerics(physical, wounded: true);
        }

        private void PublishTroops(int physical, int ready, int wounded)
        {
            var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("recovery_lifecycle");
            if ((uint)physical >= lifecycle.Slots.Count) throw Missing("recovery_source_slot");
            var receiver = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId) ?? throw Missing("recovery_receiver");
            var image = receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : receiver.PendingImage;
            if (image is null) throw Missing("recovery_image");
            current.RequireArmyReturnImage(physical, receiver);
            image = image with { Troops = ready, WoundedTroops = wounded };
            state = receiver.ActiveUnitId is int active
                ? state with { Units = new(state.Units) { [active] = image } }
                : state.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.WithEntries([receiver with { PendingImage = image }]) });
        }

        private void AdjustMorale(ArmyUnitReference army)
        {
            int adjustment = unchecked(Contributions(army)[80] * MoralePerPoint);
            if (adjustment == 0) return;
            ArmyUnitReference? source = adjustment < 0 ? NearestMoraleSource(army) : null;
            var applied = source is not null && Valid(source)
                ? ArmyMoraleZeroRoutingCurrent.ApplyImmediateFromHex(state, current, army, adjustment, Hex(source), package.TrapMobility(), Immediate)
                : ArmyMoraleZeroRoutingCurrent.ApplyImmediate(state, current, army, adjustment, package.TrapMobility(), Immediate);
            state = applied.State ?? throw Missing(applied.UnresolvedOwner ?? "morale_2709A0");
        }

        private ArmyUnitReference? NearestMoraleSource(ArmyUnitReference victim)
        {
            ArmyUnitReference? selected = null;
            var cached = Traits(victim);
            for (int index = 0; index < 36; index++)
            {
                if (!TraitHasEffect(Trait(cached.Traits64[index]), 80)) continue;
                int person = Person(cached.PersonsAC[index]);
                if (!K(current.ReadPersonNativeValidity(person), "morale_source_Person_validity")) continue;
                var candidate = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw Missing("morale_source_22A200");
                if (!Valid(candidate)) continue;
                if (selected is null || !Valid(selected)) { selected = candidate; continue; }
                int center = K(current.ReadArmyUnitRawHex2A(victim), "morale_victim_Hex2A");
                int oldDistance = ArmyRouteSearch.Distance(K(current.ReadArmyUnitRawHex2A(selected), "morale_selected_Hex2A"), center);
                int distance = ArmyRouteSearch.Distance(K(current.ReadArmyUnitRawHex2A(candidate), "morale_candidate_Hex2A"), center);
                if (distance < oldDistance || distance == oldDistance &&
                    K(candidate.PhysicalSlot, "morale_candidate_identity") < K(selected.PhysicalSlot, "morale_selected_identity")) selected = candidate;
            }
            return selected;
        }

        private GameState Immediate(GameState value, UnitDefeatRequest request)
        {
            state = value; PublishPeople();
            if (current.ReadOwnedPersonCommandManager() is null)
            {
                if (state.PersonnelCommands is null) throw Missing("defeat_personnel_manager");
                PersonReturnCommands.BindCurrent(state, current);
            }
            state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()! };
            package.BindActorlessDestructionInputs(state, current);
            sampler ??= package.Context(current).ReadCurrentSamplerWorld(state) ?? throw Missing("defeat_sampler");
            state = new OwnedArmyDestructionStage(package).ApplyBattleDefeatCurrent(state, current, pool,
                request, sampler, state.NativeMapSeason?.ThirdDate ?? throw Missing("defeat_World34"), effects,
                army => refreshes.Add(new(null, null) { ArmyTraitViewRefresh = new(
                    ArmyDestructionQueue.ResolveUnitId(state, army.PhysicalSlot) ?? throw Missing("refresh_Army_identity")) }),
                person => changedPeople.Add(person));
            PublishPeople();
            return state;
        }

        private void PublishPeople()
        {
            if (changedPeople.Count == 0) return;
            var people = new Dictionary<int, OfficerState>(state.Officers);
            foreach (int physical in changedPeople)
            {
                int id = K(current.ReadPersonIdentity(physical), "changed_Person10");
                var person = people.GetValueOrDefault(id) ?? throw Missing("changed_Person_owner");
                var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
                if (activation is not { DurationSlotCount: 9, ActiveSlots: not null }) throw Missing("changed_Person_activation");
                people[id] = person with { TraitActivation = activation };
            }
            state = state with { Officers = people }; changedPeople.Clear();
        }

        private void AdjustSecurity(ArmyUnitReference army)
        {
            int loss = unchecked(Contributions(army)[95] * SecurityLossPerPoint);
            var hex = Hex(army);
            int surface = K(current.ReadHexSurfaceCode(hex), "security_Hex10");
            if (surface is not (>= 15 and <= 18)) return;
            int raw = K(current.ReadHexAreaSlot(hex), "security_Hex12");
            var area = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw Missing("security_Area_lookup");
            int physical = K(area.PhysicalSlot, "security_Area_receiver");
            var before = state.Areas.GetValueOrDefault(physical) ?? throw Missing("security_Area_owner");
            int after = Math.Clamp(unchecked(K(before.Security, "security_Area54") - loss), 0, 100);
            int owner = K(current.ReadAreaCurrentForceSlot(area), "security_Area_Force");
            if (owner is >= 46 and <= 50) return;
            state = state with { Areas = new(state.Areas) { [physical] = before with { Security = after } } };
            current.ConditionAreaSecurityReader = at => state.Areas.GetValueOrDefault(at)?.Security is int value && value is >= 0 and <= 255 ? (byte)value : null;
        }

        private HexReference Hex(ArmyUnitReference army)
        {
            int raw = K(current.ReadArmyUnitRawHex2A(army), "Army_Hex2A");
            return current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("Army_Hex_lookup");
        }
        private int Person(int raw) => K((raw <= 6000 ? current.ReadPersonLookupReference(raw)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        private bool Valid(ArmyUnitReference army) => K(current.ReadArmyUnitNativeValidity(army), "Army_validity");
        private ArmyUnitReference Army(RegistryListNodeReference node)
        {
            var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("Army_list_payload");
            return payload.Kind == RegistryKind.Army ? new(payload.PhysicalSlot) : throw Missing("Army_list_payload_kind");
        }
        private IEnumerable<ArmyUnitReference> Armies(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("temporary_head");
            while (node.Identity.HasValue)
            {
                yield return Army(node);
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("temporary_next");
            }
        }
        private void AppendDistinct(HexListHandle list, ArmyUnitReference army)
        {
            //Only successful allocations enter this chain and suppress a
            //subsequent duplicate. No parallel HashSet invents membership.
            foreach (var value in Armies(list)) if (value == army) return;
            Lock(true);
            try
            {
                var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Army, army.PhysicalSlot));
            }
            finally { Lock(false); }
        }
        private HexListHandle Temporary()
        {
            Lock(true);
            try { return current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor"); }
            finally { Lock(false); }
        }
        private void Release(HexListHandle list)
        {
            Lock(true);
            try { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
            finally { Lock(false); }
        }
        private void Lock(bool enter)
        {
            if (!K(current.ReadRegistryLockEnabled(), "registry_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal-turn.Army_passive_traits." + field);
    }
}
