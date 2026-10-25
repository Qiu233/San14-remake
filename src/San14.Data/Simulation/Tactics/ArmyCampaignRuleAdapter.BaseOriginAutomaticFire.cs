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
        var hex = current.ReadHexTableReference(nodeHex) ?? throw FireMissing("target_Hex_pointer");
        if (hex.PhysicalSlot is not int physical) return new(state, false, null);
        ushort link = FireNeed(current.ReadHexObjectLink(hex), "target_Hex1A");
        //2C6CE0 tests nonzero Hex1A, not an Object definition/category.
        if (link != 0)
        {
            var obj = current.ReadObjectTableReference(link < 3001 ? link : 0)
                ?? throw FireMissing("target_Object_pointer");
            if (FireNeed(current.ReadObjectNativeValidity(obj), "target_Object_validity") &&
                !Allowed(FireNeed(current.ReadObjectForceSlot13(obj), "target_Object13")))
                return new(state, false, null);
        }
        //2C6D20's real1..500 signed lookup gate precedes native validity.
        int armyIndex = unchecked((short)FireNeed(current.ReadHexRaw1C(hex), "target_Hex1C"));
        if (armyIndex is >= 1 and <= 500)
        {
            var army = current.ReadArmyUnitTableReference(armyIndex) ?? throw FireMissing("target_Army_pointer");
            if (FireNeed(current.ReadArmyUnitNativeValidity(army), "target_Army_validity") &&
                !Allowed(FireNeed(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army)), "target_Army_Force22CD80")))
                return new(state, false, null);
        }
        var changes = new List<HexFireChange>();
        var announcements = new List<ArmyFireAnnouncement>();
        var working = IgniteCurrentHex(state, current, physical, prefix.Duration, true,
            reached => (ReadArmyDestructionSampler(reached, current)
                ?? throw FireMissing("report_Day450")).Day450, revision, changes, announcements, effects);
        if (announcements.Count != 0)
            working = working with { PendingArmyFireAnnouncements =
                [.. working.PendingArmyFireAnnouncements, .. announcements] };
        //Hex16 stays in the operation's current write set. The enclosing
        //execution publishes it once before its normal return or save pause.
        if (physical != primaryHex) return new(working, false, null);
        var selected = FieldHexOccupantSource.ReadCurrent(working, current, physical);
        var marker = selected.Receipt ?? throw FireMissing(selected.UnresolvedOwner ?? "marker16EB20");
        //17A1C0 excludes only255. Kind11 is an actual Object marker;
        //the enclosing presentation translates it to its real receiver ID.
        return new(working, marker.NativeKind != 255, marker.NativeKind == 255 ? null : marker);

        bool Allowed(int targetForce) => FireNeed(
            ArmyLogisticsPreparation.ReadNativeRelationGate(state, prefix.SourceForce, targetForce), "relation2CA650");
    }

    private static T FireNeed<T>(T? value, string owner) where T : struct => value ?? throw FireMissing(owner);
    private static InvalidDataException FireMissing(string owner) => new("automatic_Base_origin.fire." + owner);
    private static int FireTruncate(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f
        ? int.MinValue : (int)value;
}
