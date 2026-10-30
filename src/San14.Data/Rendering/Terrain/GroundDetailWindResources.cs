namespace San14.Data;

/// <summary>Creator contents, Compute program and one-mip volume sampler.
/// The live frame writes the first wind constants before any dispatch.</summary>
public sealed class GroundDetailWindResources
{
    private GroundDetailWindResources(Tree2InitialWindCpuResources initial,
        byte[] compute, byte[] sampler, Tree2ViewInteractionDeclaration viewInteraction)
        => (Initial, ComputeProgram, SamplerDescriptor, ViewInteraction) =
            (initial, compute, sampler, viewInteraction);

    internal Tree2InitialWindCpuResources Initial { get; }
    public ReadOnlyMemory<byte> ComputeProgram { get; }
    public ReadOnlyMemory<byte> SamplerDescriptor { get; }
    public Tree2ViewInteractionDeclaration ViewInteraction { get; }

    public static GroundDetailWindResources Load(GameResourceRepository repository, string locale)
    {
        var declarations = repository.LoadObjects("system", 0x718CCB8B);
        var provider = ModelShaderResources.ReadDeclaredShader(declarations, 0xB2DE925C);
        var shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
        var program = shader.Container.StageTables.Single(table => table.StageCode == 5).Entries.Single();
        var samplers = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, locale);
        byte[] key = RenderSamplerSourceAdapters.RenderTextureKey(samplers, 1);
        var viewInteraction = Tree2ViewInteractionSource.LoadFreshNormal(
            DefaultView0Source.LoadDeclarationDefaults(repository),
            MainMapContextOwnerSource.LoadFreshHostDefaults(locale));
        return new(Tree2WindFrameSource.CreateInitialCpuResources(),
            shader.Container.GetProgramBytes(shader.Source.Bytes, program.ProgramIndex).ToArray(),
            RenderSamplerSourceAdapters.DecodeKey(samplers, key).EncodeD3D11Descriptor(), viewInteraction);
    }
}
