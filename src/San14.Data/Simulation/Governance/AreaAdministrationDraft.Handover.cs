namespace San14.Data;

public sealed partial class AreaAdministrationDraft
{
    /// <summary>Prepare once for the parent's final intent. The caller retains
    /// both known and unknown results at this revision; drawing never retries
    /// presentation randoms. This method does not stage, restore or submit.</summary>
    public AreaAdministrationQuery<AreaAdministrationHandover> PrepareHandover(
        string locale, Func<int,int> appPresentationRandom)
    {
        ArgumentNullException.ThrowIfNull(appPresentationRandom);
        try
        {
            EnsureOpen();
            if (_child is not null) throw Missing("handover.child_still_open");
            var incoming = new List<int>();
            var outgoing = new List<int>();
            int changed = 0;
            for (int i = 0; i < _held.Length; i++)
            {
                ushort current = Person(_held[i]), before = _parent.People[i];
                if (current == before)
                {
                    if (Focus(_held[i]) != _parent.Focus[i]) changed++;
                    continue;
                }
                changed++;
                if (current != 0) incoming.Add(PersonAreaAppointmentSource.Resolve(_current, current));
                outgoing.Add(PersonAreaAppointmentSource.Resolve(_current, before));
            }
            if (incoming.Count == 0 && outgoing.Count == 0)
                return AreaAdministrationQuery<AreaAdministrationHandover>.Known(new(
                    AreaAdministrationHandoverKind.DirectCommit, Revision, changed, null));

            //Base force, then Force.ruler, then Person lookup and identity.
            //This is the City's force ruler, not its governor or speaker's force.
            var city = _current.ReadCityTableReference(_cityId) ?? throw Missing("handover.city_pointer");
            int rawForce = K(_current.ReadBaseForceSlot(city), "handover.city_force");
            var force = _current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                ?? throw Missing("handover.city_force_pointer");
            int forcePhysical = K(force.PhysicalSlot, "handover.city_force_identity");
            ushort ruler = _current.ReadForceRecord(forcePhysical)?.RawRuler10
                ?? throw Missing("handover.city_force_ruler");
            int addressee = K(_current.ReadPersonIdentity(PersonAreaAppointmentSource.Resolve(_current, ruler)),
                "handover.addressee_identity");

            bool appointing = incoming.Count != 0;
            var candidates = appointing ? incoming : outgoing;
            int selected = candidates.Count < 2 ? 0 : appPresentationRandom(candidates.Count);
            if ((uint)selected >= (uint)candidates.Count) throw Missing("handover.speaker_random_range");
            int speaker = K(_current.ReadPersonIdentity(candidates[selected]), "handover.speaker_identity");
            int? matched = null;
            if (appointing)
            {
                int physicalCity = K(city.PhysicalSlot, "handover.city_identity");
                if (physicalCity is >= 1 and <= 51)
                    foreach (int area in RegisteredAreas(_current))
                        if (K(_current.ReadAreaCitySlot(new(area)), "handover.iterator_city") == physicalCity && Person(area) == speaker)
                        { matched = area; break; }
            }
            else
                for (int i = 0; i < _held.Length; i++)
                    if (_parent.People[i] == speaker) { matched = _held[i]; break; }
            int selectedArea = matched ?? throw Missing("handover.speaker_area_match");
            var narration = _narrationSource.Prepare(locale, speaker, addressee, selectedArea,
                appointing, appPresentationRandom);
            return AreaAdministrationQuery<AreaAdministrationHandover>.Known(new(
                AreaAdministrationHandoverKind.Narration, Revision, changed, narration));
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<AreaAdministrationHandover>.Unknown(unknown.Message); }
    }
}
