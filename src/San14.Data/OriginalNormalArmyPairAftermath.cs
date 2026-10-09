using San14.Core;

namespace San14.Data;

public enum OriginalNormalArmyPairAftermathOutcome { CurrentInputsUnresolved, ResolvedWithoutWrite, Committed }
public sealed record OriginalNormalArmyAreaAftermathReceipt(int AreaId,
    OriginalCeAreaDevelopmentWriteResult[] DevelopmentWrites, int SecurityBefore, int SecurityAfter,
    ushort QuantityBefore52, ushort QuantityAfter52);
public sealed record OriginalNormalArmyRelationAftermathReceipt(int FirstForceId, int SecondForceId,
    byte FirstBefore13, byte SecondBefore13, byte FirstAfter13, byte SecondAfter13);
public sealed record OriginalNormalArmyPairAftermathResult(OriginalNormalArmyPairAftermathOutcome Outcome,
    GameState State, OriginalNormalArmyAreaAftermathReceipt? Area,
    OriginalNormalArmyRelationAftermathReceipt? Relations,
    OriginalNormalArmyCombatSamplerDraw[] Draws, string? MissingOwner);

/// <summary>TC174EC0 numeric stage8. It consumes the captured target Hex14,
/// not a damage frame. Ordinary Force relation13 is separate from EA/treaties;
/// native presentation objects and the special-force27B0F0 tail are not created.</summary>
public static class OriginalNormalArmyPairAftermath
{
    public static OriginalNormalArmyPairAftermathResult TryApply(GameState input,
        OriginalNormalArmyCombatPair pair, OriginalEventInitializationContext current,
        OriginalTacticRemakeCurrentContext context, OriginalNormalArmyAftermathInputs inputs)
    {
        var working = input;
        var draws = new List<OriginalNormalArmyCombatSamplerDraw>();
        OriginalNormalArmyAreaAftermathReceipt? areaReceipt = null;
        OriginalNormalArmyRelationAftermathReceipt? relations = null;
        try
        {
            var hex = Pointer(current.ReadHexTableReference(pair.Target.NativeHexC is >= 0 and <= 48399
                ? pair.Target.NativeHexC : 0), "current.target_Hex14");
            int areaId = Need(current.ReadHexAreaSlot(hex), "current.target_Hex_Area12");
            var areaReference = Pointer(current.ReadAreaTableReference(areaId <= 500 ? areaId : 0), "current.target_Area");
            if (Need(current.ReadAreaNativeValidity(areaReference), "current.target_Area_validity")) ApplyArea();

            if (!ForceValid(pair.Attacker.NativeForce10) || !ForceValid(pair.Target.NativeForce10)) return Done();
            if (!CommanderOrdinary(pair.Attacker) || !CommanderOrdinary(pair.Target)) return Done();
            int first = pair.Attacker.NativeForce10, second = pair.Target.NativeForce10;
            if (first == second) return Done(); //Actual2704F0 native no-op.
            if (first is >= 46 and <= 50 || second is >= 46 and <= 50)
                throw Missing("current.special_Force_27B0F0_relation_tail");
            byte beforeFirst = Relation(first, second), beforeSecond = Relation(second, first);
            int ceiling = P(SimulationParameterKeys.NormalAftermath.HostileRelationCeiling), adjustment = P(SimulationParameterKeys.NormalAftermath.HostileRelationAdjustment);
            if (beforeFirst > ceiling) WriteRelations(first, second, unchecked(ceiling - beforeFirst));
            WriteRelations(first, second, adjustment);
            relations = new(first, second, beforeFirst, beforeSecond, Relation(first, second), Relation(second, first));
            return Done();

            void ApplyArea()
            {
                if (!working.Areas.TryGetValue(areaId, out var area) || area.NativeNormalAftermath?.Quantity52 is not ushort quantity)
                    throw Missing("current.Area_quantity52");
                bool progressEnabled = true;
                if (area.Administration is not { } administration) throw Missing("current.Area_officer36");
                if (administration.OverseerId is int officer && officer != 0)
                {
                    var person = Pointer(current.ReadPersonLookupReference(officer), "current.Area_overseer_Person");
                    if (person.PhysicalSlot is int physical && Need(current.ReadPersonNativeValidity(physical), "current.overseer_validity"))
                        progressEnabled = Need(OriginalTacticRemakeCurrentTraits.CommanderEffectSum(current, physical, 0x7D),
                            "current.overseer_effect7D") <= 0;
                }
                var developmentWrites = new List<OriginalCeAreaDevelopmentWriteResult>();
                int firstSalt = unchecked((pair.Attacker.NativePoolSlot + 5) * pair.Target.NativeHexC);
                int secondSalt = unchecked(pair.Attacker.NativeHexC * 2);
                if (progressEnabled)
                {
                    int effect = ForceValid(pair.Attacker.NativeForce10)
                        ? Need(current.ReadBaseForceModifier(new(pair.Attacker.NativeForce10), 0x30),
                            "current.attacker_Force_panel30") : 0;
                    float percent = Math.Min(100f, (float)effect);
                    if (percent <= 0f || Sample("development-protection", 10000, (int)((percent * 10000f) / 100f)) >=
                        (int)((percent * 10000f) / 100f))
                    {
                        int reduction = P(SimulationParameterKeys.NormalAftermath.AreaDevelopmentReduction);
                        var adapter = new Development(working, current, inputs);
                        foreach (var field in new[] { OriginalCeAreaDevelopmentField.Commerce,
                            OriginalCeAreaDevelopmentField.Agriculture, OriginalCeAreaDevelopmentField.Barracks })
                        {
                            int before = Need(adapter.ReadAreaProgress(areaReference, field), "current.Area_progress");
                            try { developmentWrites.Add(inputs.Development.WriteCurrentProgress(areaReference, field,
                                Math.Clamp(unchecked(before - reduction), 0, 1000), 1, adapter)); }
                            catch (InvalidDataException error) { throw Missing(error.Message); }
                        }
                        working = adapter.State;
                    }
                }
                area = working.Areas[areaId];
                int security = Need(area.Security, "current.Area_security54");
                int divisor = Math.Max(1, P(SimulationParameterKeys.NormalAftermath.SecurityLossProbabilityDivisor));
                float probability = (((float)P(SimulationParameterKeys.NormalAftermath.SecurityLossBaseMultiplier) * (float)P(SimulationParameterKeys.NormalAftermath.SecurityLossProbabilityNumerator)) /
                    ((float)security + (float)P(SimulationParameterKeys.NormalAftermath.SecurityLossDenominatorOffset)) / (float)divisor) * 100f;
                float capped = probability <= 100f ? probability : 100f;
                int securityAfter = security;
                if (capped > 0f && Sample("security-loss", 10000, (int)((capped * 10000f) / 100f)) <
                    (int)((capped * 10000f) / 100f))
                {
                    int force = Need(current.ReadAreaCurrentForceSlot(areaReference), "current.Area_representative_Force");
                    //17A160 checks the representative Hex's force, not the
                    //two pair actors' captured forces.
                    if (force is < 46 or > 50) securityAfter = Math.Clamp(security - 1, 0, 100);
                }
                int draw = Sample("quantity-loss", P(SimulationParameterKeys.NormalAftermath.AreaQuantityRandomReductionBound), null);
                int after = unchecked(unchecked((int)quantity - draw) - P(SimulationParameterKeys.NormalAftermath.AreaQuantityFixedReduction));
                ushort quantityAfter = (ushort)Math.Clamp(Math.Max(5000, after), 0, 60000);
                working = working with { Areas = new(working.Areas) { [area.Id] = area with { Security = securityAfter,
                    NativeNormalAftermath = area.NativeNormalAftermath! with { Quantity52 = quantityAfter } } } };
                areaReceipt = new(area.Id, developmentWrites.ToArray(), security, securityAfter, quantity, quantityAfter);
                int Sample(string phase, int bound, int? threshold)
                {
                    var world = context.ReadCurrentSamplerWorld(working) ?? throw Missing("current.same_cycle_sampler");
                    int result = OriginalTacticEffectSourceSamplerMath.Sample(bound, 1, firstSalt, secondSalt, 0, 0, world);
                    draws.Add(new(phase, bound, firstSalt, secondSalt, 0, 0, result, threshold)); return result;
                }
            }
        }
        catch (Unknown error) { return new(OriginalNormalArmyPairAftermathOutcome.CurrentInputsUnresolved, input, null, null, [], error.Message); }

        int P(string key) => Need(inputs.ReadCurrent(working, key), "current.stage8_parameter:" + key);
        bool ForceValid(int id) => Need(current.ReadForceNativeValidity(Pointer(current.ReadForceTableReference(
            id is >= 0 and <= 51 ? id : 0), "current.Force_pointer")), "current.Force_validity");
        bool CommanderOrdinary(OriginalNormalArmyCombatDescriptor descriptor)
        {
            //Source reads the current pooled receiver even after an enqueue.
            if (!working.Units.TryGetValue(descriptor.ArmyId, out var army) ||
                army.NativeArmyOrigin?.NativeSlotIndex != descriptor.NativePoolSlot) throw Missing("current.stage8_Army_receiver");
            if (!Need(context.ArmyValid(working, army), "current.stage8_Army_validity")) return false;
            var person = Pointer(current.ReadPersonLookupReference(army.OfficerId), "current.stage8_commander");
            if (person.PhysicalSlot is not int physical || !Need(current.ReadPersonNativeValidity(physical), "current.commander_validity")) return false;
            int force = Need(current.ReadPersonOldForceSlot(physical), "current.commander_Force");
            if (force == 51) return false;
            int firstLocation = Need(current.ReadPersonFirstLocation(physical), "current.commander_first_location");
            return firstLocation is >= 1 and <= 61 || force is < 46 or > 50;
        }
        byte Relation(int first, int second) => working.NativeArmySimulation!.Forces[first].NormalAftermath?.Relations13 is { Length: 52 } values
            ? values[second] : throw Missing("current.Force_relation13");
        void WriteRelations(int first, int second, int requested)
        {
            var simulation = working.NativeArmySimulation!; var forces = new Dictionary<int, NativeArmyForceState>(simulation.Forces);
            foreach ((int own, int other) in new[] { (first, second), (second, first) })
            {
                var before = forces[own]; var fields = before.NormalAftermath!;
                byte[] bytes = fields.Relations13!.ToArray(); bytes[other] = (byte)Math.Clamp(unchecked(bytes[other] + requested), 0, 200);
                forces[own] = before with { NormalAftermath = fields with { Relations13 = bytes } };
            }
            working = working with { NativeArmySimulation = simulation with { Forces = forces } };
        }
        OriginalNormalArmyPairAftermathResult Done()
        {
            NativeNormalArmyAftermathStateValidation.Validate(working);
            return new(ReferenceEquals(working, input) ? OriginalNormalArmyPairAftermathOutcome.ResolvedWithoutWrite :
                OriginalNormalArmyPairAftermathOutcome.Committed, working, areaReceipt, relations, draws.ToArray(), null);
        }
    }

    private sealed class Development(GameState state, OriginalEventInitializationContext current,
        OriginalNormalArmyAftermathInputs inputs) : IOriginalCeAreaDevelopmentWriteContext
    {
        public GameState State { get; private set; } = state;
        public ushort? ReadAreaProgress(OriginalAreaReference area, OriginalCeAreaDevelopmentField field)
        {
            var values = State.Areas[area.PhysicalSlot!.Value].Development;
            int? value = field switch { OriginalCeAreaDevelopmentField.Commerce => values?.Commerce,
                OriginalCeAreaDevelopmentField.Agriculture => values?.Agriculture, _ => values?.Barracks };
            return value is >= 0 and <= 65535 ? (ushort)value : null;
        }
        public void WriteAreaProgress(OriginalAreaReference area, OriginalCeAreaDevelopmentField field, ushort value)
        {
            var old = State.Areas[area.PhysicalSlot!.Value]; var development = old.Development!;
            var after = field switch { OriginalCeAreaDevelopmentField.Commerce => development with { Commerce = value },
                OriginalCeAreaDevelopmentField.Agriculture => development with { Agriculture = value }, _ => development with { Barracks = value } };
            State = State with { Areas = new(State.Areas) { [old.Id] = old with { Development = after } } };
        }
        public ushort? ReadAreaRepresentativeHexIndex(OriginalAreaReference area) => current.ReadAreaHexIndex(area);
        public OriginalHexReference? ReadHexTableReference(int slot) => current.ReadHexTableReference(slot);
        public byte? ReadHexForceSlot(OriginalHexReference hex) => current.ReadHexForceSlot(hex);
        public OriginalForceReference? ReadForceTableReference(int slot) => current.ReadForceTableReference(slot);
        public bool? ReadForceNativeValidity(OriginalForceReference force) => current.ReadForceNativeValidity(force);
        public int? ReadForcePhysicalIdentity(OriginalForceReference force) => current.ReadForcePhysicalSlot(force);
        public short? ReadForceDevelopmentWord1BE(OriginalForceReference force) =>
            force.PhysicalSlot is int slot ? State.NativeArmySimulation!.Forces.GetValueOrDefault(slot)?.NormalAftermath?.Development1BE : null;
        public int? ReadCurrentDevelopmentLevelChangeCoefficient() => inputs.ReadCurrent(State, SimulationParameterKeys.NormalAftermath.AreaDevelopmentLevelChangeCoefficient);
        public void WriteForceDevelopmentWord1BE(OriginalForceReference force, short value)
        {
            var simulation = State.NativeArmySimulation!; int slot = force.PhysicalSlot!.Value; var old = simulation.Forces[slot];
            State = State with { NativeArmySimulation = simulation with { Forces = new(simulation.Forces)
                { [slot] = old with { NormalAftermath = old.NormalAftermath! with { Development1BE = value } } } } };
        }
    }
    private sealed class Unknown(string owner) : Exception(owner);
    private static Unknown Missing(string owner) => new(owner);
    private static T Need<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static T Pointer<T>(T? value, string owner) where T : class => value ?? throw Missing(owner);
}
