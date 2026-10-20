namespace San14.Data;

public sealed partial class AreaAdministrationDraft
{
    /// <summary>Prepare on an actual child identity refresh. The UI retains the
    /// returned pointer and publication across focus changes and redraws.</summary>
    public AreaAdministrationQuery<AreaAdministrationOfficerSpeechRefresh> PrepareOfficerSpeech(
        string locale, int? previousPersonPhysical, Func<int,int> appPresentationRandom)
    {
        ArgumentNullException.ThrowIfNull(appPresentationRandom);
        int area, physical;
        try
        {
            area = Active();
            physical = PersonAreaAppointmentSource.Resolve(_current, Person(area));
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<AreaAdministrationOfficerSpeechRefresh>.Unknown(unknown.Message); }

        if (previousPersonPhysical == physical) return Publish(null);
        try
        {
            if (!K(_current.ReadPersonNativeValidity(physical), "speech.person_validity"))
                return Publish(AreaAdministrationQuery<AreaAdministrationOfficerSpeechText?>.Known(null));

            // The preceding child refresh computed occupation before speech.
            // Consume only extent; its city-center lookup can lazily bind districts.
            var checkpoint = _current.CaptureAreaPreview([], [], captureCityDistricts: true);
            int extent;
            try
            {
                var occupation = _previewSource.Occupation(area, physical, _vectors.Count);
                if (!occupation.Available) throw new InvalidDataException(occupation.UnresolvedInput);
                extent = occupation.Value ?? throw Missing("speech.occupation_extent");
            }
            finally { _current.RestoreAreaPreview(checkpoint); }

            var traits = _narrationSource.ReadOfficerSpeechTraits(physical);
            var candidates = new SortedSet<int>(traits.Keys);
            if (extent >= 5) candidates.Add(6);
            if (_narrationSource.ReadOfficerSpeechAbility(physical, 2) >= 70) candidates.Add(7);
            if (_narrationSource.ReadOfficerSpeechAbility(physical, 3) >= 70) candidates.Add(8);
            if (_narrationSource.ReadOfficerSpeechAbility(physical, 1) >= 70) candidates.Add(9);
            if (_narrationSource.ReadOfficerSpeechAbility(physical, 0) >= 80) candidates.Add(10);
            var row = _state.Areas.GetValueOrDefault(area) ?? throw Missing("speech.area_current");
            int security = row.Security is >= 0 and <= 255 ? row.Security.Value : throw Missing("speech.security");
            if (security < 50) candidates.Add(11);
            int commerce = Progress(row, San14.Core.AreaDevelopmentFocus.Commerce) is >= 0 and <= 65535
                ? row.Development!.Commerce!.Value : throw Missing("speech.commerce");
            int agriculture = Progress(row, San14.Core.AreaDevelopmentFocus.Agriculture) is >= 0 and <= 65535
                ? row.Development!.Agriculture!.Value : throw Missing("speech.agriculture");
            int barracks = Progress(row, San14.Core.AreaDevelopmentFocus.Barracks) is >= 0 and <= 65535
                ? row.Development!.Barracks!.Value : throw Missing("speech.barracks");
            int minimum = Math.Min(commerce, Math.Min(agriculture, barracks));
            if (commerce != 1000 && commerce == minimum) candidates.Add(12);
            if (agriculture != 1000 && agriculture == minimum) candidates.Add(13);
            if (barracks != 1000 && barracks == minimum) candidates.Add(14);
            if (candidates.Count == 0) candidates.Add(15);

            // All candidates are known before the first UI random is consumed.
            int selected = candidates.Count < 2 ? 0 : appPresentationRandom(candidates.Count);
            if ((uint)selected >= (uint)candidates.Count) throw Missing("speech.advice_random_range");
            int advice = candidates.ElementAt(selected);
            traits.TryGetValue(advice, out var trait);
            var text = _narrationSource.PrepareOfficerSpeech(locale, physical, area, advice, trait,
                appPresentationRandom);
            return Publish(AreaAdministrationQuery<AreaAdministrationOfficerSpeechText?>.Known(text));
        }
        catch (InvalidDataException unknown)
        { return Publish(AreaAdministrationQuery<AreaAdministrationOfficerSpeechText?>.Unknown(unknown.Message)); }

        AreaAdministrationQuery<AreaAdministrationOfficerSpeechRefresh> Publish(
            AreaAdministrationQuery<AreaAdministrationOfficerSpeechText?>? update) =>
            AreaAdministrationQuery<AreaAdministrationOfficerSpeechRefresh>.Known(new(physical, update));
    }
}
