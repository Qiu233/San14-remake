using San14.Data;

public sealed partial class OriginalNativeDeferredBufferOwner
{
    public static Task<OriginalNativeDeferredBufferOwner> BuildSelectedAsync(OriginalDeferredPixelProgram program,
        IReadOnlyList<LoadedOriginalDeferredBufferSources> explicitCurrentPopulatedSources)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(explicitCurrentPopulatedSources);
        var selected = program.SourceSelection ?? throw new InvalidDataException("Selected buffers require the exact compiled F103 source selection.");
        _ = program.ReadValidatedSelectedSourceDeviceBindings();
        var layout = OriginalDeferredBufferSources.ReadSelectedLayout(selected);
        var needed = program.DescriptorBindings.Bindings.Where(d => d.DescriptorKind == "TextureBuffer" &&
            layout.Declarations.Any(b => b.SourceBindPoint == d.Source.BindPoint)).Select(d => d.Source.BindPoint).ToHashSet();
        if (explicitCurrentPopulatedSources.Any(s => s is null) ||
            explicitCurrentPopulatedSources.SelectMany(s => s.Buffers).Any(b => !needed.Contains(b.Declaration.SourceBindPoint)))
            throw new InvalidDataException("Selected populated groups supply an absent or source-null specialized buffer.");
        return BuildCoreAsync(program, explicitCurrentPopulatedSources, layout);
    }
}
