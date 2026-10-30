using San14.Core;
using static San14.Data.FireDamageParameters;

namespace San14.Data;

/// <summary>Complete ordered burning-Hex transaction. Current, pool, Person
/// tasks and the ordered effect sink remain owned by this one operation.</summary>
internal sealed class FireDamageApplication
{
    private readonly ArmyCampaignRuleAdapter package;
    private readonly SourceWorldContext current;
    private readonly NativeArmyProgressStageMoment moment;
    private readonly NormalArmyAftermathInputs areaInputs;
    private readonly WorldIndexContext pool;
    private readonly List<CampaignEffectRequest> requests = [];
    private readonly HashSet<int> changedPeople = [];
    private readonly SortedDictionary<int, int> armyGross = [];
    private readonly Dictionary<BaseReference, int> baseGross = [];
    private readonly ArmyTraitFeedback traits;
    private readonly ActorlessArmyDamageApplication armyDamage;
    private GameState state;
    private bool personnelBound;

    internal FireDamageApplication(ArmyCampaignRuleAdapter package, GameState state,
        SourceWorldContext current, NativeArmyProgressStageMoment moment, NormalArmyAftermathInputs areaInputs)
    {
        this.package = package; this.state = state; this.current = current; this.moment = moment; this.areaInputs = areaInputs;
        pool = current.ReadOwnedCurrentListPool() ?? throw Missing("shared_pool");
        traits = new(state, current, moment, requests, () => this.state, EnsurePersonnel);
        armyDamage = new(package, current, moment, () => this.state, value => this.state = value,
            traits, EnsurePersonnel, PublishPeople, person => changedPeople.Add(person), requests, SamplerWorld);
        BindQueue();
    }

    internal GameState Run()
    {
        for (int tableSlot = 0; tableSlot < 48400; tableSlot++)
        {
            var hex = current.ReadHexTableReference(tableSlot) ?? throw Missing("Hex_table_pointer");
            int physical = Need(hex.PhysicalSlot, "Hex_word16_receiver");
            if (Need(current.ReadCaptureHexWord16(physical), "Hex_word16") == 0) continue;
            ApplyHex(tableSlot, hex);
        }
        Aggregate();
        state = ArmyReturnImagePublication.Publish(state, current, []);
        PublishPeople();
        if (personnelBound) state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
            ?? throw Missing("Person_manager_publication") };
        if (requests.Count != 0) state = state with { CampaignEffectRequests =
            (state.CampaignEffectRequests ?? throw Missing("effect_owner"))
                .Append(CampaignEffectSource.FireDamage, moment.CalendarCycleOrdinal, requests) };
        state = OwnedArmyLifecycleTargets.PublishCurrent(state.WithArmyLifecycle(
            OwnedArmyDestructorFlags.PublishCurrent(state.ReadArmyLifecycle()!, current) with { Indexes = pool.ExportOwnedState() }), current);
        return state with { ArmyMapOccupancy = current.ExportArmyMapOccupancy(),
            PendingCombatSettlements = current.OwnedDestructionQueue ?? throw Missing("manager140_publication") };
    }

    private void ApplyHex(int tableSlot, HexReference hex)
    {
        var selection = FieldHexOccupantSource.ReadCurrent(state, current, tableSlot);
        var selected = selection.Receipt ?? throw Missing(selection.UnresolvedOwner ?? "16EB20");
        ArmyUnitReference? army = null;
        BaseReference? targetBase = null;
        ObjectReference? obj = null;
        int ready = 0, wounded = 0, divisor = 1;
        int? person = null;
        if (selected.NativeKind == 27)
        {
            army = current.ReadArmyUnitTableReference(selected.NativePhysicalSlot < 501 ? selected.NativePhysicalSlot : 0)
                ?? throw Missing("selected_Army_pointer");
            if (Need(current.ReadArmyUnitNativeValidity(army), "selected_Army_validity") &&
                !Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current,
                    Need(army.PhysicalSlot, "selected_Army_physical")), "selected_Army_excluded"))
            {
                var fields = current.ReadArmyReturnResourceFields(army) ?? throw Missing("Army_resources");
                ready = Need(fields.Ready16, "Army_ready16"); wounded = Need(fields.Wounded18, "Army_wounded18");
                person = Person(Need(current.ReadArmyUnitCommanderId(army), "Army_Commander12"));
                if (Need(current.ReadPersonNativeValidity(person.Value), "Army_Commander_validity") &&
                    Need(TacticRemakeCurrentTraits.CommanderHasEffect(current, person.Value, 0x19), "Army_effect19")) ready = 0;
            }
        }
        else if (selected.NativeKind is 5 or 6)
        {
            bool city = selected.NativeKind == 5;
            divisor = P(city ? CityDivisor : GateDivisor);
            int identity = selected.NativePhysicalSlot;
            int location = city ? identity < 52 ? identity : 0 : identity is >= 1 and <= 10 ? identity + 51 : 0;
            targetBase = BaseLookup.Find(location, current);
            if (Need(current.ReadBaseNativeValidity(targetBase), "selected_Base_validity") &&
                !Need(OwnedArmyDestructionCurrent.ReadBaseExcluded2334B0(state, current, CanonicalBase(targetBase).Id), "selected_Base_excluded"))
            {
                ready = Need(current.ReadBaseResource(targetBase, BaseResourceKind.Troops), "Base_ready3C");
                wounded = Need(current.ReadArmyReturnBaseWounded40(targetBase), "Base_wounded40");
                ushort link = Need(current.ReadBaseObjectLink(targetBase), "Base_Object4E");
                obj = current.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw Missing("Base_Object_pointer");
                person = Person(Need(current.ReadArmyReturnBaseGovernor2E(targetBase), "Base_Governor2E"));
                if (Need(current.ReadPersonNativeValidity(person.Value), "Base_Commander_validity") &&
                    Need(TacticRemakeCurrentTraits.CommanderHasEffect(current, person.Value, 0x19), "Base_effect19")) ready = 0;
            }
            if (current.ArmyReturnTransferMode is not { Present: false }) throw Missing("challenge_570850_497FE0");
        }
        else if (selected.NativeKind == 11)
        {
            obj = current.ReadObjectTableReference(selected.NativePhysicalSlot < 3001 ? selected.NativePhysicalSlot : 0)
                ?? throw Missing("selected_Object_pointer");
            if (ObjectRetired(obj)) obj = null;
        }

        bool returned = false;
        if (ready > 0)
        {
            int randomFloor = unchecked(P(ReadyFixed) + Sample(P(LossRandomBound), hex, 1));
            int ratio = Truncate(((float)P(ReadyPercent) * ready) / P(ReadyDenominator));
            int loss = Truncate((float)Math.Max(randomFloor, ratio) / divisor);
            if (loss > 0)
            {
                if (person is int commander && Need(current.ReadPersonNativeValidity(commander), "damage_Person_validity"))
                {
                    int effect = Need(TacticRemakeCurrentTraits.PersonContribution(current, commander, 0x1A), "effect26_contributions");
                    int reduction = effect > 0 ? unchecked(unchecked(effect * P(TraitReductionScale)) * loss) / 100 : 0;
                    if (PermanentEffect38(commander)) loss = unchecked(loss * P(PermanentTraitMultiplier));
                    loss = ScaleEnvironment(unchecked(loss - reduction), false);
                }
                if (targetBase is not null && Need(current.ReadBaseNativeValidity(targetBase), "damage_floor_Base_validity"))
                {
                    int currentReady = Need(current.ReadBaseResource(targetBase, BaseResourceKind.Troops), "damage_floor_Base3C");
                    if (currentReady <= loss) loss = unchecked(currentReady - 1);
                }
                if (army is not null)
                    returned = armyDamage.Apply(army, loss, target => requests.Add(new(null, null)
                        { FireDamagePresentation = new(FireDamagePresentationOperation.ArmyTargetViewRefresh, ArmyId(target), null, null)
                            {SourceSnapshot=package.CaptureArmyDamagePresentationCurrent(state,current,target,false)} }));
                else if (targetBase is not null)
                {
                    var result = package.ApplyFireDamageBaseTarget(state, current, targetBase.Kind,
                        selected.NativePhysicalSlot, loss, SamplerWorld);
                    state = result.State; targetBase = result.Target; returned = result.NativeReturn;
                }
                if (army is not null && Need(current.ReadArmyUnitNativeValidity(army), "after_damage_Army_validity"))
                    wounded = Need(current.ReadArmyReturnResourceFields(army)?.Wounded18, "after_damage_Army18");
                else if (targetBase is not null && Need(current.ReadBaseNativeValidity(targetBase), "after_damage_Base_validity"))
                    wounded = Need(current.ReadArmyReturnBaseWounded40(targetBase), "after_damage_Base40");
                if (loss > 0)
                {
                    if (army is not null && Need(current.ReadArmyUnitNativeValidity(army), "gross_Army_validity"))
                    {
                        int key = Need(army.PhysicalSlot, "gross_Army_pointer");
                        armyGross[key] = unchecked(armyGross.GetValueOrDefault(key) + loss);
                    }
                    else if (targetBase is not null && Need(current.ReadBaseNativeValidity(targetBase), "gross_Base_validity"))
                        baseGross[targetBase] = unchecked(baseGross.GetValueOrDefault(targetBase) + loss);
                }
            }
        }
        if (!returned)
        {
            if (wounded > 0)
            {
                int randomFloor = unchecked(P(WoundedFixed) + Sample(P(LossRandomBound), hex, 2));
                int ratio = Truncate(((float)P(WoundedPercent) * wounded) / P(WoundedDenominator));
                int loss = Truncate((float)Math.Max(randomFloor, ratio) / divisor);
                int after = Math.Max(0, unchecked(wounded - loss));
                if (loss > 0)
                {
                    if (army is not null && Need(current.ReadArmyUnitNativeValidity(army), "direct_wounded_Army_validity"))
                    {
                        ushort stored = (ushort)Math.Clamp(after, 0, 20000);
                        current.WriteArmyReturnWounded(Need(army.PhysicalSlot, "direct_wounded_Army_pointer"), stored);
                        WriteImage(army, Image(army) with { WoundedTroops = stored });
                    }
                    else if (targetBase is not null && Need(current.ReadBaseNativeValidity(targetBase), "direct_wounded_Base_validity"))
                        state = package.WriteFireDamageBaseWounded(state, current, targetBase, after);
                }
            }
            if (obj is not null && Need(current.ReadObjectNativeValidity(obj), "HP_Object_validity") &&
                Need(current.ReadObjectRawField14(obj), "HP_Object14") != 0)
            {
                int loss = ScaleEnvironment(unchecked(P(DurabilityFixed) + Sample(P(DurabilityRandomBound), hex, 3)), true);
                if (targetBase is not null && Need(current.ReadBaseNativeValidity(targetBase), "HP_floor_Base_validity") &&
                    BasePostLoad.ReadLinkedField14(targetBase, current) <= loss)
                    loss = unchecked(BasePostLoad.ReadLinkedField14(targetBase, current) - 1);
                state = targetBase is null ? package.ApplyFireDamageFieldObjectTarget(state, current, obj, loss, requests)
                    : package.ApplyFireDamageBaseObjectTarget(state, current, obj, targetBase, loss, requests);
            }
        }
        ApplyArea(hex);
    }

    private void ApplyArea(HexReference hex)
    {
        ushort raw = Need(current.ReadHexAreaSlot(hex), "Area_Hex12");
        var area = current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw Missing("Area_pointer");
        if (!Need(current.ReadAreaNativeValidity(area), "Area_validity")) return;
        int areaId = Need(area.PhysicalSlot, "Area_identity");
        var force = AreaForce(area);
        int panel31 = 0;
        if (Need(current.ReadForceNativeValidity(force), "Area_Force_validity"))
        {
            int forceId = Need(current.ReadForcePhysicalSlot(force), "Area_Force_identity");
            var policies = state.Forces.GetValueOrDefault(forceId)?.PolicyEffects ?? throw Missing("policy31_bitmaps");
            if (Need(policies.ReadActivation(0x31), "policy31_activation") &&
                !Need(policies.ReadSuppression(0x31), "policy31_suppression")) throw Missing("active_unsuppressed_policy49_signed36_owner");
        }
        int salt = unchecked(Need(current.ReadHexPhysicalSlot(hex), "Area_source_Hex_identity") + moment.PreWriterThirdDate.Hour);
        if (!Percent(panel31, salt))
        {
            foreach (var field in new[] { CeAreaDevelopmentField.Commerce, CeAreaDevelopmentField.Agriculture, CeAreaDevelopmentField.Barracks })
            {
                var writer = new OwnedAreaDevelopmentContext(state, current, areaInputs);
                int before = Need(writer.ReadAreaProgress(area, field), "Area_progress");
                areaInputs.Development.WriteCurrentProgress(area, field,
                    Math.Clamp(unchecked(before - A(SimulationParameterKeys.NormalAftermath.AreaDevelopmentReduction)), 0, 1000), 1, writer);
                state = writer.State;
            }
        }
        var saved = state.Areas.GetValueOrDefault(areaId) ?? throw Missing("Area_owner");
        int security = Need(saved.Security, "Area_security54");
        float probability = (float)A(SimulationParameterKeys.NormalAftermath.SecurityLossBaseMultiplier) * A(SimulationParameterKeys.NormalAftermath.SecurityLossProbabilityNumerator);
        probability = probability / ((float)security + A(SimulationParameterKeys.NormalAftermath.SecurityLossDenominatorOffset));
        probability = probability / Math.Max(1, A(SimulationParameterKeys.NormalAftermath.SecurityLossProbabilityDivisor));
        probability *= 100f;
        if (Percent(probability, salt))
        {
            int after = Math.Clamp(unchecked(security - 1), 0, 100);
            int identity = Need(current.ReadForcePhysicalSlot(AreaForce(area)), "security_Force_identity");
            if (identity is < 46 or > 50)
                state = state with { Areas = new(state.Areas) { [areaId] = saved with { Security = after } } };
        }
        int fixedLoss = A(SimulationParameterKeys.NormalAftermath.AreaQuantityFixedReduction);
        int sample = Sample(A(SimulationParameterKeys.NormalAftermath.AreaQuantityRandomReductionBound), salt, 0, 0, 0);
        saved = state.Areas[areaId];
        int beforeQuantity = Need(saved.NativeNormalAftermath?.Quantity52, "Area_population52");
        int delta = unchecked(unchecked(sample + fixedLoss) * unchecked(100 - panel31)) / 100;
        ushort quantity = (ushort)Math.Clamp(Math.Max(5000, unchecked(beforeQuantity - delta)), 0, 60000);
        state = state with { Areas = new(state.Areas) { [areaId] = saved with
            { NativeNormalAftermath = saved.NativeNormalAftermath! with { Quantity52 = quantity } } } };
    }

    private ForceReference AreaForce(AreaReference area)
    {
        ushort raw = Need(current.ReadAreaHexIndex(area), "Area_representative_Hex3A");
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("Area_representative_Hex_pointer");
        byte force = Need(current.ReadHexForceSlot(hex), "Area_representative_Force14");
        return current.ReadForceTableReference(force <= 51 ? force : 0) ?? throw Missing("Area_Force_pointer");
    }
    private bool Percent(float value, int salt)
    {
        float capped = value <= 100f ? value : 100f;
        return capped > 0f && Sample(10000, salt, 0, 0, 0) < Truncate((capped * 10000f) / 100f);
    }
    private int ScaleEnvironment(int value, bool durability)
    {
        int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "environment_E58");
        if (selector != 0)
        {
            int mode = Need(BattleRemakeCurrentSettings.ReadInt32(state, 0xE8), "environment_E8");
            return mode == 1 && value > 0 ? unchecked(value * P(durability ? DurabilityNormalPercent : ReadyNormalPercent)) / 100 : value;
        }
        if (!Need(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "advanced_enabled")) return value;
        return unchecked(Need(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 11), "advanced11") * value) / 100;
    }
    private bool PermanentEffect38(int person)
    {
        for (int index = 0; index < 9; index++)
        {
            var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, index) ?? throw Missing("effect38_pointer");
            if (!Need(current.ReadSkillPersonalityValidity(trait), "effect38_validity")) continue;
            byte first = Need(current.ReadPersonalityByte(trait, 0xB6), "effect38_first");
            if (first != 0x38 && Need(current.ReadPersonalityByte(trait, 0xBA), "effect38_second") != 0x38) continue;
            return Need(current.ReadPersonalityWord(trait, 0xB4), "effect38_duration") == ushort.MaxValue;
        }
        return false;
    }
    private bool ObjectRetired(ObjectReference obj)
    {
        var receiver = (current.CurrentMapObjectDirectory ?? throw Missing("Object_directory"))
            .ReceiverAtPhysical(Need(obj.PhysicalSlot, "F0_Object_pointer"));
        return (pool.PendingObjectRetirements ?? throw Missing("F0_owner")).Any(entry => entry == receiver);
    }
    private void Aggregate()
    {
        foreach (var (physical, amount) in armyGross)
            requests.Add(new(null, null) { FireDamagePresentation =
                new(FireDamagePresentationOperation.ArmyGrossDamage, ArmyId(new(physical)), null, amount)
                    {SourceSnapshot=package.CaptureArmyDamagePresentationCurrent(state,current,new(physical),true)} });
        if (baseGross.Keys.Select(key => key.Kind).Distinct().Skip(1).Any())
            throw Missing("base_gross_mixed_receiver_order:consumer_presence_unresolved");
        foreach (var (target, amount) in baseGross.OrderBy(item => item.Key.PhysicalSlot))
            requests.Add(new(null, null) { FireDamagePresentation =
                new(FireDamagePresentationOperation.BaseGrossDamage, null, CanonicalBase(target), amount)
                    {SourceSnapshot=package.CaptureBaseDamagePresentationCurrent(state,current,target)} });
    }
    private static SettlementBase CanonicalBase(BaseReference target) => new(
        target.Kind == BaseKind.City ? SettlementBaseKind.City : SettlementBaseKind.Gate,
        Need(target.PhysicalSlot, "canonical_Base_pointer") + (target.Kind == BaseKind.Gate ? 51 : 0));
    private int ArmyId(ArmyUnitReference army) => Image(army).Id;
    private UnitState Image(ArmyUnitReference army)
    {
        var arena = state.ReadArmyLifecycle() ?? throw Missing("Army_lifecycle");
        var receiver = arena.Receivers.Find(arena.Slots[Need(army.PhysicalSlot, "Army_pointer")].ReceiverId) ?? throw Missing("Army_receiver");
        return receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) ?? throw Missing("Army_active_image")
            : receiver.PendingImage ?? throw Missing("Army_pending_image");
    }
    private void WriteImage(ArmyUnitReference army, UnitState image)
    {
        var arena = state.ReadArmyLifecycle() ?? throw Missing("Army_image_lifecycle");
        var receiver = arena.Receivers.Find(arena.Slots[Need(army.PhysicalSlot, "Army_image_pointer")].ReceiverId) ?? throw Missing("Army_image_receiver");
        state = receiver.ActiveUnitId is int id ? state with { Units = new(state.Units) { [id] = image } }
            : state.WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(receiver with { PendingImage = image }) });
    }
    private int Person(int raw) => (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
        ?? throw Missing("Person_pointer");
    private int P(string key) => Read(state, key);
    private int A(string key) => areaInputs.ReadCurrent(state, key) ?? throw Missing("Area_parameter_" + key);
    private int Sample(int bound, HexReference hex, int ordinal) => Sample(bound,
        Need(current.ReadHexPhysicalSlot(hex), "sample_Hex_identity"), moment.PreWriterThirdDate.Hour, ordinal, 0);
    private int Sample(int bound, int first, int second, int third, int fourth) => bound < 2 ? 0 :
        TacticEffectSourceSamplerMath.Sample(bound, 1, first, second, third, fourth, SamplerWorld());
    private TacticSourceSamplerWorld SamplerWorld() => package.ReadCurrentEnvironmentSampler(state, moment, 23);
    private void BindQueue() => current.BindOwnedDestructionQueue(state.PendingCombatSettlements ?? throw Missing("manager140"), state);
    private void EnsurePersonnel()
    {
        if (personnelBound) return;
        if (current.ReadOwnedPersonCommandManager() is null)
        {
            if (state.PersonnelCommands is null) throw Missing("Person_manager");
            PersonReturnCommands.BindCurrent(state, current);
        }
        personnelBound = true;
    }
    private void PublishPeople()
    {
        if (changedPeople.Count == 0) return;
        var people = new Dictionary<int, OfficerState>(state.Officers);
        foreach (int physical in changedPeople)
        {
            int id = Need(current.ReadPersonIdentity(physical), "Person_publication_identity");
            var person = people.GetValueOrDefault(id) ?? throw Missing("Person_publication_owner");
            if (person.TraitActivation is not { DurationSlotCount: 9, ActiveSlots: not null }) throw Missing("full9_activation_owner");
            var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
            if (activation is not { DurationSlotCount: 9, ActiveSlots: not null }) throw Missing("lossless_activation");
            people[id] = person with { TraitActivation = activation };
        }
        state = state with { Officers = people };
    }
}
