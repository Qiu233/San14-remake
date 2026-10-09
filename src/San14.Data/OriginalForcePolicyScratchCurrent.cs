using San14.Core;

namespace San14.Data;

/// <summary>Opaque normalized reference to this operation's sequential
/// CForcePolicyData stack scratch. No source consumer compares/registers its
/// address. A returned free node retains this reference, not ownership of the
/// destroyed stack object; imported free references have unknown fields.</summary>
public sealed record OriginalForcePolicyScratchReference(int? Identity);
public sealed record OriginalForcePolicyScratchFields(int? Force8, int? EffectC, int? RawLevel10);
public sealed record OriginalForcePolicyScratchReceipt(int ForcePhysical, int Effect, int RawLevel,
    int Value, bool Mapped, int? Descriptor, int? Node);

public static class OriginalForcePolicyScratchCurrent
{
    public static OriginalForcePolicyScratchReceipt ReadBeforeFreedom(GameState state,
        OriginalEventInitializationContext current, OriginalForceReference force, int effect, bool mapped)
    {
        int physical = force.PhysicalSlot ?? throw Missing("Force_pointer");
        if (current.ReadForceNativeValidity(force) is not bool valid) throw Missing("Force_validity");
        if (!valid) return new(physical, effect, 0, 0, mapped, null, null);
        if (!state.Forces.TryGetValue(physical, out var owner)) throw Missing("Force_saved_owner");
        var scratch = new OriginalForcePolicyScratchReference(0);
        current.SetForcePolicyScratchFields(scratch, new(physical, effect, 0));
        var handle = current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
        try
        {
            var node = current.AllocateHexListNode(handle);
            if (node.Identity.HasValue) current.SetForcePolicyScratchPayload(node, scratch);
            //The source helper has no receiver if descriptor/node allocation
            //fails; the initialized stack level stays0. No native Person or
            //policy writer is hidden in its unused pure contribution queries.
            int raw = node.Identity.HasValue
                ? OriginalBattleRemakeCurrentForcePolicies.ReadForceRawLevelBeforeFreedomCurrent(state, owner, effect, current)
                    ?? throw Missing("26AEA0_current_policy_contribution") : 0;
            current.SetForcePolicyScratchFields(scratch, new(physical, effect, raw));
            int value = mapped ? OriginalBattleRemakeCurrentForcePolicies.MapEffectAtLevel(state, effect, raw)
                ?? throw Missing("3130E0_effect_value") : Math.Min(raw, 10);
            return new(physical, effect, raw, value, mapped, handle.Identity, node.Identity);
        }
        finally { current.ClearTemporaryHexList(handle); current.ReleaseTemporaryHexList(handle); }
    }
    private static InvalidDataException Missing(string name) => new("current.PolicyScratch_" + name);
}

public sealed partial class OriginalEventInitializationContext
{
    private readonly Dictionary<int, OriginalForcePolicyScratchFields> _forcePolicyScratch = [];
    public void SetForcePolicyScratchFields(OriginalForcePolicyScratchReference reference, OriginalForcePolicyScratchFields fields)
    { _forcePolicyScratch[reference.Identity ?? throw new InvalidDataException("Current policy scratch is null.")] = fields; }
    public OriginalForcePolicyScratchFields? ReadForcePolicyScratchFields(OriginalForcePolicyScratchReference reference) =>
        reference.Identity is int id ? _forcePolicyScratch.GetValueOrDefault(id) : null;
    internal void SetForcePolicyScratchPayload(OriginalHexListNodeReference node, OriginalForcePolicyScratchReference reference) =>
        WriteCurrentListPayload(NodeIdentity(node), reference);
}
