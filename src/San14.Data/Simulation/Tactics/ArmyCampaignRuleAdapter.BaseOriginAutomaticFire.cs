using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record BaseOriginAutomaticFirePrefix(int Duration, int SourceForce);
internal sealed record BaseOriginAutomaticFireResult(GameState State, bool TargetResultMarker,
    FieldHexOccupantReceipt? MarkerTarget);

public sealed partial class ArmyCampaignRuleAdapter
{
    //TC177C60's instruction prefix executes even for an empty vector.
    //The Base branch of16D900 is literally1; no Army waiting/chain exists.
    internal BaseOriginAutomaticFirePrefix BeginBaseOriginAutomaticFireCurrent(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, BaseReference source,
        TacticSourceRow row, S14TacticEffectInstruction instruction)
    {
        var scale = context.ResolveBaseOriginAutomaticScale(state, source, row, instruction);
        int duration = TacticEffectSourceMath.Scale(scale);
        float policyFactor = 0f;
        int forceId = 0;
        if (FireNeed(current.ReadBaseNativeValidity(source), "source_Base_validity"))
        {
            forceId = FireNeed(current.ReadBaseForceSlot(source), "source_ForceB0");
            var force = current.ReadForceTableReference(forceId < 52 ? forceId : 0)
                ?? throw FireMissing("source_Force_pointer");
            if (FireNeed(current.ReadForceNativeValidity(force), "source_Force_validity"))
            {
                int identity = FireNeed(current.ReadForcePhysicalSlot(force), "source_Force60");
                if (identity is not (>= 46 and <= 51))
                {
                    var owner = state.Forces.GetValueOrDefault(identity) ?? throw FireMissing("source_Force_owner");
                    int policy = FireNeed(BattleRemakeCurrentForcePolicies.ReadForceEffect(state, owner, 19, current), "source_policy19");
                    //The independently recovered SC/TC catalog preserves
                    //an explicit saved override for this mechanism scalar.
                    int multiplier = TacticRemakeCurrentContext.OriginStatic(state, "fire-policy-multiplier");
                    policyFactor = (float)unchecked(multiplier * policy) / 100f;
                }
            }
        }
        int additional = FireTruncate(policyFactor * (float)duration);
        return new(unchecked(duration + additional), forceId);
    }

    //Call before16EB20: fire checks both the actual Object and Army even
    //when that later decoder returns255 or gives a Base priority over Army.
    internal BaseOriginAutomaticFireResult ApplyBaseOriginAutomaticFireHexCurrent(GameState state,
        SourceWorldContext current, BaseOriginAutomaticFirePrefix prefix, int nodeHex, int primaryHex,
        NativeMapSeasonDate date, long revision, List<CampaignEffectRequest> effects)
    {
        return ApplyAutomaticFireHexCurrent(state, current, prefix.Duration, prefix.SourceForce,
            nodeHex, primaryHex, reached => (ReadArmyDestructionSampler(reached, current)
                ?? throw FireMissing("report_Day450")).Day450, revision, effects,date);
    }

    private static T FireNeed<T>(T? value, string owner) where T : struct => value ?? throw FireMissing(owner);
    private static InvalidDataException FireMissing(string owner) => new("automatic_Base_origin.fire." + owner);
    private static int FireTruncate(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f
        ? int.MinValue : (int)value;
}
