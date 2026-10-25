namespace San14.Data;

/// <summary>The current ordinary Effect2 callback's scalar feature inputs.
/// Shadow receive/local types belong to the selected Light, while direction
/// scale and shadow camera mode belong to the current View.</summary>
public sealed record G1EffectGpuFeatureInputs(uint TableFeatureWord, uint FogMode,
    bool SelectedLightPresent, int SelectedLightGroup, byte ShadowReceiveType,
    byte LocalShadowType, float ShadowDirectionScale, uint ShadowCameraMode);

public static class G1EffectGpuFeatureSource
{
    /// <summary>Effect2 selects its runtime bank independently from the row's
    /// pass. The low-resolution contribution requires all three real owners.</summary>
    public static int SelectEffect2RuntimeBank(uint currentContextFlags,
        bool lowResolutionOwnerPresent, bool lowResolutionOwnerEnabled, bool sceneInputPresent)
    {
        uint bits = currentContextFlags >> 17 & 1;
        if (lowResolutionOwnerPresent && lowResolutionOwnerEnabled && sceneInputPresent) bits |= 2;
        return checked((int)(bits == 0 ? 0 : bits + 3));
    }

    public static G1EffectGpuFeatureRequest Prepare(G1EffectGpuFeatureInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        uint flags = 0, shadow = 0, shadowMode = 0, localShadow = 0;
        if (input.SelectedLightPresent && input.SelectedLightGroup > 0)
        {
            byte source = input.ShadowReceiveType;
            // The normal receive getter admits 1,2,3,8,9,108. Other stored
            // values explicitly select the source no-shadow branch.
            shadow = source == 108 || source < 10 && ((0x30Eu >> source) & 1) != 0 ? source : 0u;
            if (shadow != 0)
            {
                if (shadow == 108) { shadow = 8; flags = 0x4000; }
                if (shadow is 8 or 9 && input.ShadowDirectionScale <= 0) shadow = 1;
                shadowMode = MapSourceCreatedF103Feature.ProjectC5Bed0(input.ShadowCameraMode);
                localShadow = input.LocalShadowType;
                if (shadow < 4) flags |= 0x100;
                flags |= 0x400;
            }
        }
        return new(flags, input.TableFeatureWord, 0, 0, 0, 1, input.FogMode,
            input.FogMode == 7 ? 2u : 1u, shadow, shadowMode, localShadow, 3);
    }
}
