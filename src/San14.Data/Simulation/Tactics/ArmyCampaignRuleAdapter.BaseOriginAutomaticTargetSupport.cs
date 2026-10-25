using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record BaseOriginAutomaticBaseSupportApplication(GameState State,
    bool TargetResultMarker, ushort MarkerAmount, PlayableTacticEffectKind? EffectKind,
    int EffectAmount);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>1763B0/177990/1782F0/177ED0's actual live Base branches.
    /// Selection still excludes Bases from opcode20's original vector. An
    /// earlier instruction may change its Hex occupant before this dispatch;
    ///177ED0 then consumes the real Base instead of the former Army.</summary>
    internal BaseOriginAutomaticBaseSupportApplication ApplyBaseOriginAutomaticBaseSupportCurrent(
        GameState state, SourceWorldContext current, TacticRemakeCurrentContext context,
        BaseReference origin, FieldHexOccupantReceipt target, TacticSourceRow row,
        S14TacticEffectInstruction instruction, TacticEffectScaleSourceInput? scale)
    {
        if (instruction.Opcode is not (15 or 16 or 19 or 20)) throw SupportMissing("opcode");
        var kind = instruction.Opcode switch
        {
            19 => PlayableTacticEffectKind.ClearAbnormalStatuses,
            20 => PlayableTacticEffectKind.ArmyHealing,
            _ => PlayableTacticEffectKind.NativeMoraleChange
        };
        //Each of these wrappers handles only Army27 and Base5/6 after16EB20.
        //There is no generic Object writer or marker for another live kind.
        if (target.NativeKind is not (5 or 6)) return new(state, false, 0, kind, 0);
        var receiver = new BaseReference(target.NativeKind == 5 ? BaseKind.City : BaseKind.Gate,
            target.NativePhysicalSlot);
        if (!Need(current.ReadBaseNativeValidity(receiver), "target_validity"))
            return new(state, false, 0, kind, 0);
        var working = state;
        if (instruction.Opcode == 19)
        {
            var fields = current.ReadCaptureBaseThreatFields(receiver) ?? throw SupportMissing("target_counters");
            for (int index = 0; index < 3; index++)
            {
                //Three fresh231480 calls, each after the previous actual
                //49/4A/4B write. Cooldown4C and forced target62/64 survive.
                int upper = P(SimulationParameterKeys.Tactics.AbnormalCounterUpperBound);
                byte after = upper < 0 ? unchecked((byte)upper) : (byte)0;
                fields = index switch { 0 => fields with { Byte49 = after },
                    1 => fields with { Byte4A = after }, _ => fields with { Byte4B = after } };
                current.SetCaptureBaseThreatFields(receiver, fields);
                working = PublishOneBaseTacticCounter(working, receiver, 0x49 + index, after);
            }
            //1782F0 has no1B4BD0 status-feedback call. Its result is solely
            //the caller's17A1C0 zero marker, not a fabricated abnormal speech.
            return new(working, true, 0, kind, 0);
        }
        if (instruction.Opcode is 15 or 16)
        {
            int requested, before;
            if (instruction.Opcode == 15)
            {
                int amount = TacticEffectSourceMath.Scale(RequireScale());
                int forceSlot = Need(current.ReadBaseForceSlot(receiver), "morale_ForceB0");
                var force = current.ReadForceTableReference(forceSlot <= 51 ? forceSlot : 0)
                    ?? throw SupportMissing("morale_Force_pointer");
                int upper = BasePostLoad.ReadMoraleUpper(force, current);
                before = Need(current.ReadBaseMorale44(receiver), "morale44");
                requested = Math.Min(upper, unchecked(before + amount));
            }
            else
            {
                int loss = context.ResolveBaseOriginAutomaticMoraleLoss(working, origin, target, row, instruction);
                before = Need(current.ReadBaseMorale44(receiver), "morale_loss44");
                requested = Math.Max(0, unchecked(before + loss));
            }
            bool written = BasePostLoad.WriteMoraleCurrent(receiver, requested, current);
            working = PublishCurrentBaseCombatQuantities(working, current, receiver, false, false, written);
            int after = Need(current.ReadBaseMorale44(receiver), "morale44_after");
            return new(working, true, 0, kind, after - before);
        }

        //177ED0's Base transfer retains signed32 arithmetic. Its first cap
        //limits the transfer; the two3C and two40 setter caps are independent
        //fresh queries. Army's ushort/20000 limits do not apply to a Base.
        var healingScale = RequireScale();
        int wounded = Need(current.ReadArmyReturnBaseWounded40(receiver), "healing40");
        int readyForTransfer = Need(current.ReadBaseResource(receiver, BaseResourceKind.Troops), "healing3C_transfer");
        float value = (float)wounded * healingScale.AbilityFactor;
        value /= (float)Math.Max(1, 20 - instruction.EffectValue);
        int minimum = P(SimulationParameterKeys.Tactics.HealingMinimum);
        if (value <= (float)minimum) value = minimum;
        value *= (float)unchecked(healingScale.StageOrdinal * healingScale.CurrentStageStepPercent) / 100f + 1f;
        value *= healingScale.SharedFactor;
        int room = unchecked(Capacity() - readyForTransfer);
        if ((float)wounded <= value) value = wounded;
        int transfer = Math.Min(room, Truncate(value));
        int beforeReady = Need(current.ReadBaseResource(receiver, BaseResourceKind.Troops), "healing3C_write");
        int first = Bound(unchecked(beforeReady + transfer), Capacity());
        current.SetBaseResource(receiver, BaseResourceKind.Troops, Bound(first, Capacity()));
        int beforeWounded = Need(current.ReadArmyReturnBaseWounded40(receiver), "healing40_write");
        first = Bound(unchecked(beforeWounded - transfer), Capacity());
        current.SetArmyReturnBaseWounded40(receiver, Bound(first, Capacity()));
        working = PublishCurrentBaseCombatQuantities(working, current, receiver, true, true, false);
        return new(working, true, unchecked((ushort)transfer), kind, transfer);

        int Capacity() => BasePostLoad.ReadTroopCapacity(receiver, current);
        int P(string key) => TacticRemakeCurrentContext.OriginParameter(working, key);
        TacticEffectScaleSourceInput RequireScale() => scale is { } captured &&
            captured.EffectValue == instruction.EffectValue ? captured : throw SupportMissing("instruction_scale");
        static int Bound(int value, int upper) => value < 0 ? 0 : Math.Min(value, upper);
        static int Truncate(float value) => !float.IsFinite(value) || value < int.MinValue ||
            value >= 2147483648f ? int.MinValue : (int)value;
        static T Need<T>(T? value, string name) where T : struct => value ?? throw SupportMissing(name);
        static InvalidDataException SupportMissing(string name) => TacticRemakeCurrentContext.OriginAutomaticMissing("Base_support." + name);
    }
}
