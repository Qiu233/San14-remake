namespace San14.Data;

public static class KeyboardBindingLayoutSource
{
    public static LoadedLayout LoadPage(GameResourceRepository repository) =>
        MapMarkerLayout.LoadNamed(repository, "02_03_02_04_setup_keyconfig_new");
    public static LoadedLayout LoadList(GameResourceRepository repository) =>
        MapMarkerLayout.LoadNamed(repository, "02_03_02_setup_offset_08_new");
    public static LoadedLayout LoadRow(GameResourceRepository repository) =>
        MapMarkerLayout.LoadNamed(repository, "02_03_02_setup_offset_09");
}
