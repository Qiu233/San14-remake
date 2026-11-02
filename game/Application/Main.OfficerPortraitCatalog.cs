using San14.Data;

public partial class Main
{
    private OfficerEditorPortraitCatalog? _newGamePortraitCatalog;
    private string? _newGamePortraitLocale;
    private object? _newGamePortraitProfile,_newGamePortraitLibrary;
    private int _newGamePortraitResourceVersion;
    private OfficerEditorPortraitCatalog GetNewGamePortraitCatalog(string locale)
    {
        var profile=GetApplicationProfile().Current;var library=GetRegisteredOfficerStore().Current;
        int resourceVersion=_repository!.PortraitResourceVersion;
        if(_newGamePortraitCatalog is not null&&_newGamePortraitLocale==locale&&
            _newGamePortraitResourceVersion==resourceVersion&&
            ReferenceEquals(_newGamePortraitProfile,profile)&&ReferenceEquals(_newGamePortraitLibrary,library))return _newGamePortraitCatalog;
        var catalog=OfficerEditorPortraitCatalog.Load(_repository!,locale,CollectionOfficerAdmission.FromProfile(profile),library);
        _newGamePortraitCatalog=catalog;_newGamePortraitLocale=locale;_newGamePortraitProfile=profile;_newGamePortraitLibrary=library;
        _newGamePortraitResourceVersion=resourceVersion;
        return catalog;
    }
}
