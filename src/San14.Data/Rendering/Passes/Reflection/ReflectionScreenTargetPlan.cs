namespace San14.Data;

public sealed record ReflectionScreenTarget(uint OutputSlot, uint? RawDescriptorIndex,
    uint? ArrayLayer, uint? InheritedFramebufferSlot);

/// <summary>The final or fallback descriptor selection over the actual current
/// terminal texture. Logical RR is not an authority to replace its proxy owner.
/// An invalid nonnull descriptor never falls back to a current attachment.</summary>
public sealed record ReflectionScreenTargetPlan(bool Active, bool RawSliceDescriptorsPresent,
    IReadOnlyList<ReflectionScreenTarget> Targets)
{
    public static ReflectionScreenTargetPlan Prepare(RenderTextureRuntimeDescriptor actualCurrentTerminal,
        bool active)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentTerminal);
        if (actualCurrentTerminal.Kind != RenderTextureKind.Texture2DArray ||
            actualCurrentTerminal.ArrayDepth is not > 0 || actualCurrentTerminal.LevelNum != 1 ||
            actualCurrentTerminal.MinMipLevel != 0 || actualCurrentTerminal.MultiSample != 0 ||
            actualCurrentTerminal.NeedSliceTargets is not (0 or 1))
            throw new InvalidDataException("reflection.screen.current_target_descriptor");
        bool slices = actualCurrentTerminal.NeedSliceTargets == 1;
        var targets = new ReflectionScreenTarget[3];
        for (uint slot = 0; slot < 3; slot++)
        {
            if (!slices)
            {
                // 959740 substitutes each null item by its corresponding
                // current slot. The fallback makes three separate count1
                // calls, so all three read current slot0.
                targets[slot] = new(slot, null, null, active ? slot : 0);
                continue;
            }
            uint multiplier = slot + (active ? 3u : 1u);
            uint layer = multiplier - 1;
            if (layer >= actualCurrentTerminal.ArrayDepth.Value)
                throw new InvalidDataException("reflection.screen.raw_descriptor_outside_current_owner." + multiplier);
            targets[slot] = new(slot, multiplier, layer, null);
        }
        return new(active, slices, Array.AsReadOnly(targets));
    }
}
