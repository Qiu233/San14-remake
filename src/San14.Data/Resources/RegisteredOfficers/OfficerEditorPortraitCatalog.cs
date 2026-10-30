namespace San14.Data;

/// <summary>The title editor's actual ordered portrait categories. Admission
/// and normal named resource ownership are captured once with the catalog.</summary>
public sealed class OfficerEditorPortraitCatalog
{
    private readonly IReadOnlySet<int> selectable;
    private readonly GameResourceRepository repository;
    public IReadOnlyList<HistoricalOfficerPortraitCategory> Categories { get; }
    public IReadOnlyList<RegisteredOfficerEditorChoice> Choices { get; }
    //5D93F0's test reaches5106.36FE80 accepts it;36FCB0 excludes it from
    //the content bitmap, so36F370 returns true and writes available=1.
    public bool BonusCategoryVisible => true;
    public bool Complete => Categories.All(category => category.UnresolvedOwners.Count == 0);

    internal OfficerEditorPortraitCatalog(HistoricalOfficerPortraitCategory[] categories, GameResourceRepository repository)
    {
        this.repository = repository;
        Categories = Array.AsReadOnly(categories);
        Choices = Array.AsReadOnly(categories.SelectMany(category => category.Choices).ToArray());
        selectable = Choices.Select(choice => choice.Id).ToHashSet();
    }

    public bool Contains(int id) => selectable.Contains(id);
    public void ValidateChoice(int id)
    {
        if (!Contains(id)) throw new InvalidDataException("officer-editor.portrait.not_admitted:" + id);
    }
    public AreaAdministrationQuery<ArmyMapPortraitNames> ResolveFace(int id) => Contains(id)
        ? AreaAdministrationQuery<ArmyMapPortraitNames>.Known(id is >= 7000 and <= 7999 ? repository.ResolveUserFacePortrait(id) : DlcPortraitDescriptor.ForAppearance(id) is not null
            ? repository.ResolveDlcFacePortrait(id) : RegisteredOfficerPortraitCatalog.FacePortrait(id))
        : AreaAdministrationQuery<ArmyMapPortraitNames>.Unknown("officer-editor.portrait.not_admitted:" + id);
    public AreaAdministrationQuery<DeploymentNarrationPortraitRoute> ResolveStanding(int id) => Contains(id)
        ? AreaAdministrationQuery<DeploymentNarrationPortraitRoute>.Known(id is >= 7000 and <= 7999 ? repository.ResolveUserStandingPortrait(id) : DlcPortraitDescriptor.ForAppearance(id) is not null
            ? repository.ResolveDlcStandingPortrait(id) : RegisteredOfficerPortraitCatalog.StandingPortrait(id))
        : AreaAdministrationQuery<DeploymentNarrationPortraitRoute>.Unknown("officer-editor.portrait.not_admitted:" + id);

    public static OfficerEditorPortraitCatalog Load(GameResourceRepository repository, string locale,
        CollectionOfficerAdmission? admission = null, San14.Core.RegisteredOfficerLibrary? registeredLibrary = null) =>
        repository.LoadCollectionOfficerCatalog(locale, admission).CreateEditorPortraitCatalog(repository, registeredLibrary);
}
