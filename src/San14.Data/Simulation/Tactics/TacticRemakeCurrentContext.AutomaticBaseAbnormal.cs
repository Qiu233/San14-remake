using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record BaseTacticAbnormalCounterReceipt(int NativeIndex, int RequestedStrength,
    int AdjustedStrength, int CauseCode, bool CounterWritten, byte? CounterBefore, byte? CounterAfter,
    bool IncreasingVisual, bool HadTraitAdjustment)
{
    internal int? NotificationDistrict { get; init; }
    internal bool StartsStatus { get; init; }
}
internal sealed record BaseTacticAbnormalReceipt(int TacticSlot, byte Opcode, BaseReference Target,
    bool TargetValid, bool GateAllowed, int? RequestedStrength, bool TargetResultMarker,
    int? NotificationDistrict, BaseTacticAbnormalCounterReceipt[] Counters,
    CaptureBaseThreatFields? Before, CaptureBaseThreatFields? After)
{
    //These are source-time semantic notification requirements, not formatted
    //or played acknowledgements. Presentation consumes no gameplay RNG here.
    internal bool GeneralImmunityNotice { get; init; }
    internal bool CombinedApplicationNotice { get; init; }
}
internal sealed record BaseTacticAbnormalApplication(GameState State, BaseTacticAbnormalReceipt Receipt);

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Actual Army-to-Base175FF0/178500, then2EB7E0/2EF210.
    /// sourceHexAtInstructionStart is the live Army2A captured by that wrapper,
    /// not172EB0's earlier descriptor Hex. Caller retains the shared current,
    /// primary pool and vector walk. Missing invalidates the unpublished operation.</summary>
    internal BaseTacticAbnormalApplication ApplyAutomaticBaseAbnormal(GameState state,
        UnitState caster, BaseReference target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, int stageOrdinal, int sourceHexAtInstructionStart,
        TacticSourceSamplerWorld? sampler)
        => ApplyAutomaticBaseAbnormalCore(state, caster, null, target, source, instruction,
            stageOrdinal, sourceHexAtInstructionStart, sampler);

    //2EF210's target writer is shared; a real Base source contributes no
    //Army trait98/99 or source-Army District. Its governor remains16D840's
    //ability receiver and16F9A0's effect98 early-pass receiver.
    private BaseTacticAbnormalApplication ApplyAutomaticBaseAbnormalCore(GameState state,
        UnitState? caster, BaseReference? baseCaster, BaseReference target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, int stageOrdinal, int sourceHexAtInstructionStart,
        TacticSourceSamplerWorld? sampler)
    {
        if (instruction.Opcode is not (2 or 3 or 4 or 23)) throw BaseAbnormalMissing("opcode");
        if (!Need(_definitions.ReadBaseNativeValidity(target), "Base_validity"))
            return Result(state, false, false, null, false, null, [], null, null);
        if (!Bound(state) || (baseCaster is null
            ? caster is null || ArmyValid(state, caster) != true || caster.NativeArmyOrigin?.NativeSlotIndex is null
            : !Need(_definitions.ReadBaseNativeValidity(baseCaster), "source_Base_validity")))
            throw BaseAbnormalMissing("current_caster");
        //Current CE SC175D50/178260 ->2EB700/2EF100 is paired with
        //TC175FF0/178500 ->2EB7E0/2EF210. Both gates use the explicit
        //-1 lower coefficient, and both Base writers keep the same slots.
        var army = baseCaster is null ? _definitions.ReadArmyUnitTableReference(caster!.NativeArmyOrigin!.NativeSlotIndex!.Value)
            ?? throw BaseAbnormalMissing("caster_pointer") : null;
        int commander = baseCaster is null ? CommanderPhysical(state, caster!) ?? throw BaseAbnormalMissing("caster_Commander")
            : OriginGovernor(baseCaster);
        bool early = Need(TacticRemakeCurrentTraits.CommanderHasEffect(_definitions, commander, 98), "caster_effect98");
        if (!early)
        {
            var gateAbilities = SelectedAbilities();
            int baseline = P(SimulationParameterKeys.Tactics.AbnormalGateBaseline);
            bool higher = gateAbilities.Source > gateAbilities.Target;
            //The formal unused operand is not a claimed current value.16F9A0
            //reads only the reached higher coefficient or lower denominator.
            int higherCoefficient = higher ? P(SimulationParameterKeys.Tactics.AbnormalGateHigherAbilityScale) : 0;
            int lowerDenominator = higher ? 1 : P(SimulationParameterKeys.Tactics.AbnormalGateLowerAbilityScale);
            var gate = new TacticEffectGateSourceInput(gateAbilities.Source, gateAbilities.Target,
                baseline, higherCoefficient,
                lowerDenominator, baseCaster is null ? CasterTrait(99) : 0,
                baseCaster is null ? P(SimulationParameterKeys.Tactics.AbnormalGateTraitRate) : 0);
            var tactic = _definitions.ReadSkillTacticReference(source.Slot) ?? throw BaseAbnormalMissing("current_Tactic_pointer");
            int tacticPhysical = Need(_definitions.ReadSkillTacticPhysicalSlot(tactic), "current_Tactic_physical");
            int liveSource = baseCaster is null ? HexPhysical(Need(_definitions.ReadArmyUnitRawHex2A(army!), "caster_2A"))
                : HexPhysical(_definitions.ReadBaseCurrentHexIndex(baseCaster));
            int targetHex = BaseHex();
            bool allowed = TacticEffectSourceGateMath.AllowsTc(gate, tacticPhysical, liveSource, targetHex, sampler)
                ?? throw BaseAbnormalMissing("gate_sampler_World450_45C");
            if (!allowed) return Result(state, true, false, null, false, null, [], null, null);
        }
        //The allowed wrapper next enters16B820, which independently calls
        //16D840/16E820. Do not reuse its earlier gate ability observations.
        var factorAbilities = SelectedAbilities();
        int divisor = P(factorAbilities.Selector == 1 ? SimulationParameterKeys.Tactics.StrengthDifferenceDivisor
            : SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor);
        if (divisor == 0) throw BaseAbnormalMissing("zero_factor_divisor");
        float factor = (float)unchecked(factorAbilities.Source - factorAbilities.Target) / (float)divisor;
        factor += 1f;
        factor = MathF.Max(factor, (float)P(SimulationParameterKeys.Tactics.EffectFactorFloorPercent) / 100f);
        int shared = 100;
        if (baseCaster is null)
        {
            var runtime = caster!.NativeTacticRuntime ?? throw BaseAbnormalMissing("caster_runtime");
            shared = runtime.Waiting21 != 0 ? 100 : P(runtime.Waiting22 == 0
                ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent);
        }
        var scale = new TacticEffectScaleSourceInput(instruction.EffectValue, factor, stageOrdinal,
            P(SimulationParameterKeys.Tactics.EffectStageScale), (float)shared / 100f);
        int requested;
        if (instruction.Opcode == 23)
        {
            int force = baseCaster is null
                ? Need(_definitions.ReadForcePhysicalSlot(_definitions.ReadArmyUnitForceReference(army!)), "caster_Force")
                : Need(_definitions.ReadBaseForceSlot(baseCaster), "caster_Base_Force");
            byte flags = state.NativeArmySimulation.Forces.GetValueOrDefault(force)?.Flags193
                ?? throw BaseAbnormalMissing("caster_Force193");
            int special = (flags & 4) != 0 ? P(SimulationParameterKeys.Tactics.SpecialForceEffectPercent) : 100;
            requested = TacticEffectSourceMath.Opcode23RequestedStrength(scale, special);
        }
        else requested = TacticEffectSourceMath.Scale(scale); //Base178500 has no Force193 multiplier.
        var before = _definitions.ReadCaptureBaseThreatFields(target);
        var working = state;
        var receipts = new List<BaseTacticAbnormalCounterReceipt>();
        if (instruction.Opcode == 23)
        {
            //2EB7E0 reads each old byte before2EF210, skipping index1 entirely.
            foreach (int index in new[] { 0, 2 })
            {
                receipts.Add(ApplyStatus(index));
            }
        }
        else receipts.Add(ApplyStatus(instruction.Opcode - 2));
        int? district = null;
        if (instruction.Opcode == 23) district = NotificationDistrict();
        else district = receipts.Select(value => value.NotificationDistrict).FirstOrDefault(value => value.HasValue);
        var receipt = new BaseTacticAbnormalReceipt(source.Slot, instruction.Opcode, target,
            true, true, requested, true, district, receipts.ToArray(), before,
            _definitions.ReadCaptureBaseThreatFields(target))
        {
            GeneralImmunityNotice = instruction.Opcode == 23 && district.HasValue && receipts.Any(value => value.CauseCode == 21),
            CombinedApplicationNotice = instruction.Opcode == 23 && district.HasValue &&
                !receipts.Any(value => value.CauseCode is 21 or 22 or 24)
        };
        return new(working, receipt);

        BaseTacticAbnormalCounterReceipt ApplyStatus(int index)
        {
            byte? observedBefore = instruction.Opcode == 23 ? Counter(Fields(), index) : null;
            var defender = Governor();
            int? noticeDistrict = null;
            int adjusted = requested, cause = 0;
            bool traitAdjustment = false;
            if (defender.Valid)
            {
                if (Need(TacticRemakeCurrentTraits.CommanderImmune(_definitions, defender.Physical, 21), "governor_general_immunity"))
                    return new(index, requested, 0, 21, false, observedBefore, observedBefore, false, false)
                    { NotificationDistrict = instruction.Opcode == 23 ? null : NotificationDistrict() };
                if ((index is 0 or 2) && Need(TacticRemakeCurrentTraits.CommanderImmune(_definitions, defender.Physical,
                    index == 0 ? 22 : 24), "governor_specific_immunity"))
                    return new(index, requested, 0, index == 0 ? 22 : 24, false, observedBefore, observedBefore, false, false)
                    { NotificationDistrict = instruction.Opcode == 23 ? null : NotificationDistrict() };
                int general = GovernorTrait(defender.Physical, 9);
                int specific = index is 0 or 2 ? GovernorTrait(defender.Physical, 10 + index) : 0;
                int generalPart = unchecked(general * P(SimulationParameterKeys.Tactics.AbnormalGeneralResistanceRate));
                int specificPart = index == 1 ? 0 : unchecked(specific * P(index == 0
                    ? SimulationParameterKeys.Tactics.AbnormalType0ResistanceRate : SimulationParameterKeys.Tactics.AbnormalType2ResistanceRate));
                int sourcePart = baseCaster is null
                    ? unchecked(CasterTrait(98) * P(SimulationParameterKeys.Tactics.AbnormalSourceTraitRate)) : 0;
                float change = (float)unchecked(unchecked(unchecked(generalPart + specificPart) + sourcePart) * requested);
                change /= 100f;
                traitAdjustment = MathF.Abs(change) >= TacticEffectSourceAbnormal.AdjustmentEpsilon;
                if (traitAdjustment)
                {
                    int subtraction = Truncate(change);
                    if (subtraction == 0) subtraction = change < 0 ? -1 : 1;
                    adjusted = unchecked(adjusted - subtraction);
                    //Native cause attribution is reached only for a player ruler-led District.
                    noticeDistrict = NotificationDistrict();
                    if (noticeDistrict.HasValue)
                    {
                        if (Matches(sourcePart, change)) cause = 98;
                        else if (Matches(generalPart, change)) cause = 9;
                        else if (Matches(specificPart, change)) cause = 10 + index;
                    }
                }
            }
            var fields = Fields();
            byte previous = Counter(fields, index);
            int sum = unchecked(previous + adjusted);
            int upper = P(SimulationParameterKeys.SubdayStatus.BaseAbnormalUpper);
            byte after = unchecked((byte)(sum < 0 ? 0 : Math.Min(sum, upper)));
            fields = index switch { 0 => fields with { Byte49 = after }, 1 => fields with { Byte4A = after },
                _ => fields with { Byte4B = after } };
            _definitions.SetCaptureBaseThreatFields(target, fields);
            working = Publish(working, index, after, null, null);
            if (index == 2 && adjusted > 0)
            {
                int sourceHex = HexPhysical(sourceHexAtInstructionStart);
                var selected = FieldHexOccupantSource.ReadCurrent(working, _definitions, sourceHex);
                var entity = selected.Receipt ?? throw BaseAbnormalMissing(selected.UnresolvedOwner ?? "owner_16EB20");
                if (entity.NativeKind != 255)
                {
                    byte kind = (byte)Math.Clamp(entity.NativeKind, 0, 47);
                    ushort id = (ushort)Math.Clamp(entity.NativePhysicalSlot, 0, ushort.MaxValue);
                    fields = fields with { Byte62 = kind, Word64 = id };
                    _definitions.SetCaptureBaseThreatFields(target, fields);
                    working = Publish(working, index, after, kind, id);
                }
            }
            //The visual event15/16 uses the original pre-clamp sum, not whether
            //the stored byte increased. It requires an actual valid governor.
            bool visual = defender.Valid && sum > previous && (index is 0 or 2);
            bool starts = previous == 0 && sum > 0;
            if (instruction.Opcode != 23 && starts) noticeDistrict = NotificationDistrict();
            return new(index, requested, adjusted, cause, true, previous, after, visual, traitAdjustment)
                { NotificationDistrict = noticeDistrict, StartsStatus = starts };
        }
        (byte Selector, int Source, int Target) SelectedAbilities()
        {
            byte selector = FactorSelector(source) ?? throw BaseAbnormalMissing("current_factor_selector");
            int dimension = selector == 1 ? 1 : 2;
            int sourceAbility = baseCaster is null ? Ability(caster!.OfficerId, dimension) : OriginAbility(state, baseCaster, dimension);
            var governor = Governor();
            //16E820's real invalid-governor result is literal50, with no ability query.
            int targetAbility = governor.Valid
                ? Ability(Need(_definitions.ReadPersonIdentity(governor.Physical), "governor_Person10"), dimension) : 50;
            return (selector, sourceAbility, targetAbility);
        }
        int Ability(int id, int consumedDimension)
        {
            if (_abilities is TacticRemakeCurrentAbilities current)
                return current.ResolveDimension(state, id, consumedDimension)
                    ?? throw BaseAbnormalMissing("current_ability_dimension");
            //Only a different explicitly supplied interface provider retains its
            //generic contract; the real current provider never requests all5.
            var value = _abilities?.Resolve(state, id) ?? throw BaseAbnormalMissing("ability_provider");
            return consumedDimension == 1 ? value.Strength : value.Intelligence;
        }
        int CasterTrait(int effect) => (TacticRemakeCurrentTraits.ArmyContributions(_definitions, commander,
            ArmyTraitSourceAdapter.Encode(state, caster!)) ?? throw BaseAbnormalMissing("caster_traits"))[effect];
        (int Physical, bool Valid) Governor()
        {
            ushort raw = Need(_definitions.ReadArmyReturnBaseGovernor2E(target), "Base_governor2E");
            var person = raw <= 6000 ? _definitions.ReadPersonLookupReference(raw) : _definitions.ReadTransferPersonTableReference(0);
            int physical = Need(person?.PhysicalSlot, "governor_pointer");
            return (physical, Need(_definitions.ReadPersonNativeValidity(physical), "governor_validity"));
        }
        int GovernorTrait(int person, int effect)
        {
            int total = 0;
            for (int slot = 0; slot < 9; slot++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(_definitions, person, slot)
                    ?? throw BaseAbnormalMissing("governor_trait_pointer");
                if (!Need(_definitions.ReadSkillPersonalityValidity(trait), "governor_trait_validity")) continue;
                for (int pair = 0; pair < 2; pair++)
                {
                    byte code = Need(_definitions.ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA), "governor_trait_code");
                    if (code != effect) continue;
                    if (Need(_definitions.ReadPersonalityWord(trait, 0xB4), "governor_trait_condition") != ushort.MaxValue) break;
                    short value = unchecked((short)Need(_definitions.ReadPersonalityWord(trait, pair == 0 ? 0xB8 : 0xBC), "governor_trait_value"));
                    total = unchecked(total + Math.Clamp((int)value, -100, 2000)); break;
                }
            }
            return total;
        }
        int? NotificationDistrict()
        {
            var district = _definitions.ReadBaseDistrictReference(target) ?? throw BaseAbnormalMissing("target_notification_District");
            if (BaseCaptureRewardSource.ReadPlayerRulerDistrict(_definitions, district, BaseAbnormalMissing))
                return Need(district.PhysicalSlot, "target_notification_District_identity");
            if (baseCaster is not null) return null; //2EF210 param2 is actual null.
            byte raw = Need(_definitions.ReadPersonNativeValidity(commander), "source_notification_Person_validity")
                ? Need(_definitions.ReadPersonDistrictSlot(commander), "source_notification_PersonE0") : (byte)0;
            district = _definitions.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw BaseAbnormalMissing("source_notification_District");
            return BaseCaptureRewardSource.ReadPlayerRulerDistrict(_definitions, district, BaseAbnormalMissing)
                ? Need(district.PhysicalSlot, "source_notification_District_identity") : null;
        }
        CaptureBaseThreatFields Fields() => _definitions.ReadCaptureBaseThreatFields(target)
            ?? throw BaseAbnormalMissing("current_Base_abnormal_fields");
        byte Counter(CaptureBaseThreatFields fields, int index) => Need(index switch
            { 0 => fields.Byte49, 1 => fields.Byte4A, _ => fields.Byte4B }, "current_Base_counter");
        int HexPhysical(int raw)
        {
            var hex = _definitions.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0)
                ?? throw BaseAbnormalMissing("current_Hex_pointer");
            return Need(_definitions.ReadHexPhysicalSlot(hex), "current_Hex_physical");
        }
        int BaseHex()
        {
            int link = Need(_definitions.ReadBaseObjectLink(target), "Base4E");
            var obj = _definitions.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw BaseAbnormalMissing("Base_Object");
            return HexPhysical(Need(_definitions.ReadObjectNativeValidity(obj), "Base_Object_validity")
                ? Need(_definitions.ReadObjectHexIndex(obj), "Base_Object10") : 0);
        }
        int P(string key)
        {
            var bank = state.NativeArmySimulation!.Parameters;
            return !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
                ? value : throw BaseAbnormalMissing(key);
        }
        GameState Publish(GameState input, int index, byte value, byte? kind, ushort? id)
        {
            var simulation = input.NativeArmySimulation ?? throw BaseAbnormalMissing("saved_parameter_owner");
            if (simulation.Parameters.Int32Values.GetValueOrDefault(CaptureBaseThreatInputs.VersionKey) != 1)
                throw BaseAbnormalMissing("saved_Base_fields_owner");
            var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
                { [CaptureBaseThreatInputs.Key(target, 0x49 + index)] = value };
            if (kind.HasValue) values[CaptureBaseThreatInputs.Key(target, 0x62)] = kind.Value;
            if (id.HasValue) values[CaptureBaseThreatInputs.Key(target, 0x64)] = id.Value;
            return input with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
        }
        BaseTacticAbnormalApplication Result(GameState input, bool valid, bool allowed, int? strength,
            bool marker, int? notification, BaseTacticAbnormalCounterReceipt[] counters,
            CaptureBaseThreatFields? oldFields, CaptureBaseThreatFields? newFields)
            => new(input, new(source.Slot, instruction.Opcode, target, valid, allowed, strength,
                marker, notification, counters, oldFields, newFields));
        T Need<T>(T? value, string field) where T : struct => value ?? throw BaseAbnormalMissing(field);
        bool Matches(int part, float change) => part != 0 && (part > 0) == (change > 0);
        int Truncate(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f ? int.MinValue : (int)value;
    }
    private static InvalidDataException BaseAbnormalMissing(string field) => new("automatic_Base_abnormal." + field);
}
