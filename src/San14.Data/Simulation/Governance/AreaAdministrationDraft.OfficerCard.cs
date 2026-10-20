namespace San14.Data;

public sealed partial class AreaAdministrationDraft
{
    /// <summary>The current child card uses this draft's projection. Reading it
    /// does not enter, cancel, restore or otherwise stage an appointment.</summary>
    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard()
    {
        try
        {
            int area = Active();
            int physical = PersonAreaAppointmentSource.Resolve(_current, Person(area));
            bool valid = K(_current.ReadPersonNativeValidity(physical), "card_person_validity");
            return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(
                _officerCardSource.Read(area, Revision, physical, valid));
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(unknown.Message); }
    }
    /// <summary>Lazy current marker lookup. Known null hides an invalid
    /// definition; known empty is valid empty text. Unknown is independent
    /// of the already published card's trait names and backgrounds.</summary>
    public AreaAdministrationQuery<string?> QueryOfficerTraitTooltip(int lookupTraitId)
    {
        try
        {
            _ = Active();
            return _officerCardSource.ReadTraitTooltip(lookupTraitId);
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<string?>.Unknown(unknown.Message); }
    }
}
