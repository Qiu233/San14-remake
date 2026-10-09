using System.Runtime.CompilerServices;

namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    // Each loaded native generation imports the immutable manager key pair.
    // A reload creates a new native object and repeats the original source
    // checks. Current slot selection and actual device samplers are not cached.
    private static readonly ConditionalWeakTable<LoadedDeferredSamplerNativeSource,
        MapPostEffect3ManagerSamplerSources> LoadedPostEffect3ManagerSamplers = new();
}
