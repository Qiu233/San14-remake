namespace San14.Data;

/// <summary>The supported default plugin chain preserves ordinary stencil
/// state. Other created modules require their own stencil behavior.</summary>
internal static class GBufferStencilConfiguration
{
    public static void RequireDefaultCallbacks(LoadedShaderContainer shader, bool allowSkippedModelModules)
    {
        int gbufferCount = 0;
        foreach (var configuration in shader.ReadConfigurations().Records)
        {
            if (configuration.Name == "GBuffer")
            {
                if (++gbufferCount != 1 || configuration.RawSecondWord != 2)
                    throw new InvalidDataException("Ordinary stencil requires one required GBuffer module.");
                // The initializer uses the first ShaderType value, defaulting to
                // zero, and rejects negative signed values. DepthInMRT and MRTNum
                // affect its own layout, not the default stencil callbacks.
                if (configuration.Find(0xB682502B)?.IntegerValue is < 0)
                    throw new InvalidDataException("GBuffer initialization rejects a negative ShaderType.");
            }
            else if (!allowSkippedModelModules || configuration.RawSecondWord != 0 ||
                configuration.Name is not ("PB2TexPack" or "AlphaTest" or "Instancing2"))
                throw new NotSupportedException($"Ordinary stencil does not support module {configuration.Name} in the default registry.");
            // These three optional model modules are absent from the default
            // startup registry and create no plugin. A changed registry remains
            // a separate current owner lifecycle condition.
        }
        if (gbufferCount != 1)
            throw new InvalidDataException("Ordinary stencil requires the created GBuffer module.");
    }
}
