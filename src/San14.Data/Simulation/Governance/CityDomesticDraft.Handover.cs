using San14.Core;

namespace San14.Data;

public enum CityDomesticHandoverKind { SourceCancel, Narration }
public sealed record CityDomesticHandover(CityDomesticHandoverKind Kind, long DraftRevision,
    CityDomesticNarration? Narration);
public sealed record CityDomesticNarration(int SpeakerRawIdentity, int AddresseeRawIdentity,
    ushort CityRawLink, string SpeakerName, ushort PortraitAppearanceCode,
    DeploymentNarrationPortraitRoute PortraitRoute, int RootMessageId, int MessageId,
    IReadOnlyList<NarrationMessageChoice> Choices, string Text, IReadOnlyList<string> Pages);

public sealed partial class CityDomesticDraft
{
    private AreaAdministrationQuery<CityDomesticHandover>? _handover;
    private long _handoverRevision = -1;
    private string? _handoverLocale;

    public AreaAdministrationQuery<CityDomesticHandover> PrepareHandover(string locale,
        Func<int, int> appPresentationRandom)
    {
        ArgumentNullException.ThrowIfNull(appPresentationRandom);
        try
        {
            EnsureOpen();
            if (locale != _language) throw Missing("handover.locale");
            if (_handover is not null && _handoverRevision == Revision && _handoverLocale == locale)
                return _handover;
            _handoverRevision = Revision; _handoverLocale = locale;
            try { return _handover = AreaAdministrationQuery<CityDomesticHandover>.Known(Prepare()); }
            catch (InvalidDataException unknown)
            { return _handover = AreaAdministrationQuery<CityDomesticHandover>.Unknown(unknown.Message); }
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<CityDomesticHandover>.Unknown(unknown.Message); }

        CityDomesticHandover Prepare()
        {
            var city = City();
            int governor = K(_current.ReadArmyReturnBaseGovernor2E(city), "handover.governor");
            if (governor is < 1 or > 6000) governor = Ruler();
            int root, speaker;
            if (_policy is >= CityDomesticPolicy.Commerce and <= CityDomesticPolicy.Delegated)
            { root = 0x5A80 + ((int)_policy - 1) * 8; speaker = governor; }
            else
            {
                int recruiter = Resolve(RoleRaw(CityDomesticDuty.Recruiter));
                int trainer = Resolve(RoleRaw(CityDomesticDuty.Trainer));
                int oldRecruiter = Resolve(_original.Recruiter), oldTrainer = Resolve(_original.Trainer);
                //The supported ordinary host has no tutorial notification service.
                //Both incoming checks complete before selection. Preserve roles, not a set of people.
                var roles = new List<int>();
                if (recruiter != oldRecruiter && Valid(recruiter)) roles.Add(0);
                if (trainer != oldTrainer && Valid(trainer)) roles.Add(1);
                if (roles.Count != 0)
                {
                    int role = Select(roles);
                    speaker = K(_current.ReadPersonIdentity(role == 0 ? recruiter : trainer), "handover.speaker_identity");
                    root = role == 0 ? 0x5763 : 0x577B;
                }
                else
                {
                    if (recruiter != oldRecruiter && !Valid(recruiter)) roles.Add(2);
                    if (trainer != oldTrainer && !Valid(trainer)) roles.Add(3);
                    if (roles.Count == 0) return new(CityDomesticHandoverKind.SourceCancel, Revision, null);
                    int role = Select(roles);
                    speaker = role == 2 ? _original.Recruiter : _original.Trainer;
                    root = role == 2 ? 0x575D : 0x5775;
                }
            }
            int addressee = Ruler();
            ushort cityLink = K(_current.ReadBaseObjectLink(city), "handover.city_argument");
            var source = _narrationSource ?? throw Missing("handover.resource_owner");
            return new(CityDomesticHandoverKind.Narration, Revision,
                source.Prepare(locale, root, speaker, addressee, cityLink, appPresentationRandom));

            int Resolve(ushort raw) => PersonAreaAppointmentSource.Resolve(_current, raw);
            bool Valid(int physical) => K(_current.ReadPersonNativeValidity(physical), "handover.person_validity");
            int Select(List<int> choices)
            {
                int index = choices.Count < 2 ? 0 : appPresentationRandom(choices.Count);
                if ((uint)index >= (uint)choices.Count) throw Missing("handover.speaker_random_range");
                return choices[index];
            }
            int Ruler()
            {
                var force = Force(city);
                ushort raw = _current.ReadForceRecord(K(force.PhysicalSlot, "handover.force_identity"))?.RawRuler10
                    ?? throw Missing("handover.force_ruler");
                return K(_current.ReadPersonIdentity(Resolve(raw)), "handover.ruler_identity");
            }
        }
    }
}
