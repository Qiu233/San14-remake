using San14.Core;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>TC17D860's inner provocation equality followed by17D6E0's
    /// outer current-target/action gate (SC17D5D0/17D450). Both compare the
    /// descriptor kind and native receiver ordinal, not gameplay unit IDs.
    /// The caller has already checked status, available tactics and waiting;
    /// the selected-commander gate and all selection/trials remain after this.</summary>
    private bool? ReadAutomaticArmyTargetGate(GameState state, UnitState caster,
        int requestedKind, ushort requestedNativeId, out string? missingOwner)
    {
        missingOwner = null;
        if (caster.NativeArmyOrigin?.NativeSlotIndex is not int physical ||
            caster.NativeTacticRuntime is not { } native ||
            _definitions.ReadArmyUnitTableReference(physical) is not { } army ||
            _definitions.ReadRegistryTableReference(RegistryKind.Army, physical) is not { } reference)
        {
            missingOwner = "automatic-selection.target_current_Army_receiver";
            return null;
        }

        //17D860 checks word3E first. A known mismatch returns before even
        //the override kind is needed, and before the outer action37 read.
        if (native.Abnormal25 != 0)
        {
            if (_definitions.ReadTacticArmyTarget3E(army) is not ushort provokedId)
            {
                missingOwner = "automatic-selection.provocation_target3E";
                return null;
            }
            if (provokedId != requestedNativeId) return false;
            if (_definitions.ReadTacticClearByte(reference, 0x3C) is not byte provokedKind)
            {
                missingOwner = "automatic-selection.provocation_target3C";
                return null;
            }
            if (provokedKind != requestedKind) return false;
        }

        //17D6E0 freshly resolves the current expected target. The borrowed
        //owner may have changed during an earlier pair in this same phase.
        if (_definitions.ReadTacticClearByte(reference, 0x3C) is not byte kind)
        {
            missingOwner = "automatic-selection.override_target3C";
            return null;
        }
        ushort identity;
        if (kind != byte.MaxValue)
        {
            if (_definitions.ReadTacticArmyTarget3E(army) is not ushort overriddenId)
            {
                missingOwner = "automatic-selection.override_target3E";
                return null;
            }
            identity = overriddenId;
        }
        else
        {
            var target = _definitions.ReadArmyLifecycleTargetFields(physical);
            if (target?.Kind38 is not byte commandKind || target.Identity3A is not ushort commandId)
            {
                missingOwner = "automatic-selection.command_target38_3A";
                return null;
            }
            kind = commandKind;
            identity = commandId;
        }
        if (kind == requestedKind && identity == requestedNativeId) return true;
        if (native.Abnormal25 != 0) return false;
        if (ArmyConfigurationSourceAdapter.ReadAction(state, caster) is not byte action)
        {
            missingOwner = "automatic-selection.action37";
            return null;
        }
        return action is not (4 or 5 or 7);
    }
}
