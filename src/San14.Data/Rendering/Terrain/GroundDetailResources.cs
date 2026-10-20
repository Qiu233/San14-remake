namespace San14.Data;

/// <summary>Imported templates and settled populations for one scene. Current
/// camera selection and wind updates belong to their live Native owners.</summary>
public sealed class GroundDetailResources
{
    private GroundDetailResources(GroundDetailTemplateResources templates,
        GroundDetailPopulation population, GroundDetailWindResources wind,
        uint placementFlags, uint frustumKind, ProceduralViewSettings view)
        => (Templates, Population, Wind, PlacementFlags, FrustumKind, View) =
            (templates, population, wind, placementFlags, frustumKind, view);

    public GroundDetailTemplateResources Templates { get; }
    public GroundDetailPopulation Population { get; }
    public GroundDetailWindResources Wind { get; }
    public uint PlacementFlags { get; }
    public uint FrustumKind { get; }
    public ProceduralViewSettings View { get; }

    public static GroundDetailResources Prepare(GameResourceRepository repository,
        LoadedSceneResources scene, bool reversedDepth, string locale)
    {
        var declaration = MapProceduralPlacementSource.ReadCeSceneDeclaration(scene);
        var templates = GroundDetailTemplateResources.Load(repository, scene, declaration, reversedDepth);
        // Placement callbacks sample the landscape Field in map coordinates.
        // The camera's centered Field has an additional position edit; sharing
        // that sampler shifts tree height lookups by half the landscape extent.
        var field = MapStaticModelGroundHeightSource.PrepareCurrentField(repository, scene,
            UnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated, true, []);
        var population = GroundDetailPopulation.Prepare(declaration, field,
            templates.Metadata.Select(template => template.Bounds).ToArray());
        var common = repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId);
        uint placement = CommonRenderResources.ReadScalar(common, 0xF4B6AF8D, 5) | 1u;
        uint frustum = CommonRenderResources.ReadScalar(common, 0xA1A63083, 5);
        return new(templates, population, GroundDetailWindResources.Load(repository, locale),
            placement, frustum, ProceduralViewSettings.FreshSelected(common));
    }

    public void WriteNativeInputs(string inputRoot, uint currentAppFlags)
        => GroundDetailInputEncoding.WriteNativeInputs(inputRoot, this, currentAppFlags);
}
