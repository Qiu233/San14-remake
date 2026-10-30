namespace San14.Data;

public static class RegisteredOfficerNewGameLayouts
{
    public static LoadedLayout LoadMenu(GameResourceRepository repository) =>
        MapMarkerLayout.LoadNamed(repository, "02_04_00_btn_set_offset_00_ed_pk");
    public static LoadedLayout LoadHeading(GameResourceRepository repository) =>
        MapMarkerLayout.LoadNamed(repository, "02_05_00_offset_01");
    public static LoadedLayout LoadForces(GameResourceRepository repository) =>
        MapMarkerLayout.LoadNamed(repository, "02_04_02_edit_new");
    public static LoadedLayout LoadForceParts(GameResourceRepository repository) =>
        MapMarkerLayout.LoadNamed(repository, "02_04_02_offset_00");
}
