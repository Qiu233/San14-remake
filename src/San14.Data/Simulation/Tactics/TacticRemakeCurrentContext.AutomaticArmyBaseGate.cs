using San14.Core;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>TC17D6E0/17D860's ordinary Army permission for an actual
    /// City/Gate descriptor. False is a known native skip; true only permits
    /// the later automatic candidate selector. Null preserves missing inputs.
    /// This does not select, sample, dispatch, write frame24 or touch RNG.</summary>
    internal bool? ResolveAutomaticArmyBaseGate(GameState state, UnitState caster,
        int requestedKind, ushort requestedNativeId)
    {
        if (requestedKind is not (5 or 6) || requestedNativeId < 1 ||
            requestedNativeId > (requestedKind == 5 ? 51 : 10) ||
            ArmyValid(state, caster) != true || caster.NativeTacticRuntime is not { } native)
            return null;
        if (native.Status10 != 1) return false;
        if (EffectiveFormation(state, caster) is not int formation ||
            AvailableTactics(state, caster, formation) is not int[] available) return null;
        if (available.Length == 0 || native.Waiting20 != 0 || native.Abnormal23 != 0) return false;

        int physical = caster.NativeArmyOrigin!.NativeSlotIndex!.Value;
        //Use this operation's current override owner, which may already have
        //been changed by an earlier pair, rather than reconstructing a target
        //from a Mission or comparing a Core location to a native Gate ID.
        if (native.Abnormal25 != 0)
        {
            bool? same = OverrideMatches();
            if (same != true) return same;
        }
        else
        {
            byte? action = ArmyConfigurationSourceAdapter.ReadAction(state, caster);
            if (action is null) return null;
            //With25==0, either equality outcome passes actions outside4/5/7.
            if (action is 4 or 5 or 7)
            {
                if (_definitions.ReadRegistryTableReference(RegistryKind.Army, physical) is not { } reference ||
                    _definitions.ReadTacticClearByte(reference, 0x3C) is not byte overrideKind) return null;
                bool? same;
                if (overrideKind != 255) same = OverrideMatches();
                else
                {
                    var target = _definitions.ReadArmyLifecycleTargetFields(physical);
                    same = target?.Kind38 is byte kind && target.Identity3A is ushort identity
                        ? kind == requestedKind && identity == requestedNativeId : null;
                }
                if (same != true) return same;
            }
        }
        return ReadSelectedCommanderAutomaticGate(state, caster);

        bool? OverrideMatches()
        {
            if (_definitions.ReadRegistryTableReference(RegistryKind.Army, physical) is not { } reference ||
                _definitions.ReadArmyUnitTableReference(physical) is not { } army ||
                _definitions.ReadTacticClearByte(reference, 0x3C) is not byte kind ||
                _definitions.ReadTacticArmyTarget3E(army) is not ushort identity) return null;
            return kind == requestedKind && identity == requestedNativeId;
        }
    }
}
