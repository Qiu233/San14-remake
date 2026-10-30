namespace San14.Data;

/// <summary>The ordinary registered-officer editor's original named layouts.</summary>
public static class RegisteredOfficerEditorLayoutSource
{
    public static LoadedLayout LoadEditor(GameResourceRepository repository) => Load(repository, "02_05_01_edit_busho");
    public static LoadedLayout LoadBackground(GameResourceRepository repository) => Load(repository, "02_05_01_00_offset_00");
    public static LoadedLayout LoadMenu(GameResourceRepository repository) => Load(repository, "02_05_00_edit_register");
    public static LoadedLayout LoadMenuButtons(GameResourceRepository repository) => Load(repository, "02_04_00_btn_set_offset_00");
    public static LoadedLayout LoadSectionButton(GameResourceRepository repository) => Load(repository, "02_04_00_btn_offset_00");
    public static LoadedLayout LoadTraitMarker(GameResourceRepository repository) => Load(repository, "cmn_mrker");
    public static LoadedLayout LoadTab(GameResourceRepository repository, int index) => Load(repository, index switch
    {
        0 => "02_05_01_00_tab1_offset_00",
        1 => "02_05_01_00_tab2_offset_00",
        2 => "02_05_01_00_tab3_offset_00",
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    });
    public static LoadedLayout LoadNames(GameResourceRepository repository) => Load(repository, "02_05_01_00_window_offset_00_ot");
    public static LoadedLayout LoadWindow(GameResourceRepository repository, int section) => Load(repository, section switch
    {
        0 => "02_05_01_00_window_offset_00_ot",
        1 => "02_05_01_00_window_offset_01",
        2 => "02_05_01_00_window_offset_02",
        4 => "02_05_01_00_window_offset_06",
        7 => "02_05_01_00_window_offset_03",
        8 => "02_05_01_00_window_offset_04",
        11 => "02_05_01_00_tab3_offset_00",
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    });
    private static LoadedLayout Load(GameResourceRepository repository, string name)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return MapMarkerLayout.LoadNamed(repository, name);
    }
}
