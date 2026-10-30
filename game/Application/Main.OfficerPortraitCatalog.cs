using San14.Data;

public partial class Main
{
    private OfficerEditorPortraitCatalog? _newGamePortraitCatalog;
    private string? _newGamePortraitLocale;
    private object? _newGamePortraitProfile,_newGamePortraitLibrary;
    private OfficerEditorPortraitCatalog GetNewGamePortraitCatalog(string locale)
    {
        var profile=GetApplicationProfile().Current;var library=GetRegisteredOfficerStore().Current;
        if(_newGamePortraitCatalog is not null&&_newGamePortraitLocale==locale&&
            ReferenceEquals(_newGamePortraitProfile,profile)&&ReferenceEquals(_newGamePortraitLibrary,library))return _newGamePortraitCatalog;
        var catalog=OfficerEditorPortraitCatalog.Load(_repository!,locale,CollectionOfficerAdmission.FromProfile(profile),library);
        _newGamePortraitCatalog=catalog;_newGamePortraitLocale=locale;_newGamePortraitProfile=profile;_newGamePortraitLibrary=library;
        return catalog;
    }
}
