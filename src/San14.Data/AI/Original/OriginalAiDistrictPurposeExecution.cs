using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

internal readonly record struct OriginalAiDistrictPurposePreparation(bool Success, SetDistrictPurposeOrder? Order);

public sealed partial class ArmyCampaignRuleAdapter : IDistrictPurposeExecution
{
    internal OriginalAiDistrictPurposePreparation PrepareOriginalAiDistrictPurpose(SourceWorldContext current,
        OriginalAiActor actor, sbyte purpose)
    {
        OriginalAiEntity? district = ReadOriginalAiMyselfDistrict(current, actor);
        if (district is not { } selected || !ReadOriginalAiDistrictValidity(current, selected.Id))
            return new(true, null);
        OriginalAiEntity? force = ReadOriginalAiMyselfForce(current, actor);
        if (force is not { } owner || owner.Id != actor.ForceId)
            return new(false, null);
        return new(true, new(actor.ForceId, selected.Id, purpose));
    }

    CommandResult IDistrictPurposeExecution.ExecuteDistrictPurpose(GameState state, ForceState force,
        SetDistrictPurposeOrder order, CommandContext context)
    {
        // Shared Core dispatch already checks ordinary force/district/scope authority.
        // The accepted command owns its candidate. Publish only this district's saved field.
        if (state.NativeForceUnregisterCurrent is not { } owner)
            return CommandResult.RuleUnavailable("original_ai.district_purpose.owner", "Current district purpose owner is unknown.");
        if (!owner.DistrictWindows10.TryGetValue(order.DistrictId, out byte?[]? fields) || fields is not { Length: 28 })
            return CommandResult.RuleUnavailable("original_ai.district_purpose.window", "Current district source window is unknown.");
        byte value = unchecked((byte)order.Purpose);
        if (fields[5] == value) return CommandResult.Accept(state);
        var changed = (byte?[])fields.Clone();
        changed[5] = value;
        return CommandResult.Accept(state with
        {
            NativeForceUnregisterCurrent = owner with
            {
                DistrictWindows10 = new(owner.DistrictWindows10) { [order.DistrictId] = changed }
            }
        });
    }
}
