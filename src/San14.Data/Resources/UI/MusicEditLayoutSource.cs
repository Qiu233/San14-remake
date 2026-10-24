namespace San14.Data;

/// <summary>Resolves the original editor layouts through the normal Common
/// declarations and repository override/cache lifetime.</summary>
public static class MusicEditLayoutSource
{
    public static LoadedLayout LoadEditor(GameResourceRepository repository) => Load(repository, "02_12_00_BGM_edit");
    public static LoadedLayout LoadSelection(GameResourceRepository repository) => Load(repository, "02_07_04_BGM");
    public static LoadedLayout LoadMenu(GameResourceRepository repository) => Load(repository, "02_05_00_edit_register");
    public static LoadedLayout LoadRow(GameResourceRepository repository) => Load(repository, "02_12_00_offset_00");
    public static LoadedLayout LoadMenuButtons(GameResourceRepository repository) => Load(repository, "02_04_00_btn_set_offset_00_new");
    public static LoadedLayout LoadChangeButton(GameResourceRepository repository) => Load(repository, "02_12_00_offset_01");
    public static LoadedLayout LoadSelectionRow(GameResourceRepository repository) => Load(repository, "02_07_04_offset_00");
    public static LoadedLayout LoadPreviewButton(GameResourceRepository repository) => Load(repository, "02_12_00_offset_01");
    private static LoadedLayout Load(GameResourceRepository repository, string name)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return MapMarkerLayout.LoadNamed(repository, name);
    }
}
