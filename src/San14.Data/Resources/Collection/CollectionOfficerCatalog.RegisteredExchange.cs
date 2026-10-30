namespace San14.Data;

public sealed partial class CollectionOfficerCatalog
{
    /// <summary>Uses the title prototype and the same captured application /
    /// content admission as ordinary portraits; resource existence is not an unlock.</summary>
    public AreaAdministrationQuery<int> ResolveRegisteredImportPortrait(int appearance, byte sex, OfficerEditorPortraitCatalog? portraitCatalog = null)
    {
        portraitCatalog ??= CreateEditorPortraitCatalog();
        if (appearance >= 1300) return Admit(appearance);
        AreaAdministrationQuery<int> Admit(int id) => portraitCatalog.Contains(id)
            ? AreaAdministrationQuery<int>.Known(id)
            : AreaAdministrationQuery<int>.Unknown("registered-officer.import.portrait_not_admitted:" + id);
        var person = people.Values.Where(person => person.Id is >= 2001 and <= 2088 &&
            person.Appearance == appearance).OrderBy(person => person.Id).FirstOrDefault();
        if (person is null) return Admit(appearance);
        return ReadBonusAdmission(person.Id) switch
        {
            true => Admit(appearance),
            false => Admit(sex == 0 ? 2000 : 2050),
            null => AreaAdministrationQuery<int>.Unknown("registered-officer.import.portrait_admission." + person.Id)
        };
    }
}
