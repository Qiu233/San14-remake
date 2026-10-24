using San14.Core;
using System.Collections.Immutable;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyPersonnelMediationReportsCurrent(GameState state, SourceWorldContext current,
        int forceId, IReadOnlyList<int> ids, bool marriage, List<CampaignEffectRequest> sink, GameState sourceState)
    {
        int a = ids[0], b = ids[1];
        int? c = ids.Count == 3 ? ids[2] : null;
        int ruler = PersonnelAdministrationRuler(state, forceId);
        int Physical(int id) => current.ResolveOfficerSource(id)
            ?? throw PersonnelAdministrationReportMissing("mediation_person_pointer");
        int Need(int? value, string field) => value ?? throw PersonnelAdministrationReportMissing(field);
        var force = current.ReadForceTableReference(forceId)
            ?? throw PersonnelAdministrationReportMissing("mediation_force_pointer");
        var primary = ForcePrimaryDistrictSource.Resolve(current, force);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var selector = new PersonDeathHistorySelection(state, current,
            current.ReadOwnedCurrentListPool() ?? throw PersonnelAdministrationReportMissing("mediation_pool"),
            abilities, _governorInputs, (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect));
        var historySpeaker = selector.Select(primary);
        int payload = Need(current.ReadPersonIdentity(Need(historySpeaker.PhysicalSlot, "mediation_history_speaker")),
            "mediation_history_speaker_identity");
        int mainRoot = marriage ? 0x58A6 : c.HasValue ? 0x58A8 : 0x58A7;
        var operands = new HistoryMessageOperands(Person0: a, Person1: b, Person2: c);
        var main = FormatPersonnelDevelopmentCurrent(state, current, mainRoot, operands);
        var extra = FormatPersonnelDevelopmentCurrent(main.State, current, mainRoot + 3, operands);
        var sampler = ReadArmyDestructionSampler(extra.State, current)
            ?? throw PersonnelAdministrationReportMissing("mediation_day450");
        var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(Physical(a)), "mediation_homeE2"), current);
        var hex = current.ReadReportBaseHex(home)
            ?? throw PersonnelAdministrationReportMissing("mediation_home_hex");
        int hexId = Need(current.ReadReportMapCellPhysicalSlot(hex), "mediation_hex_ordinal");
        int districtId = Need(current.ReadPersonDistrictSlot(Physical(a)), "mediation_districtE0");
        var working = WriteHistory(extra.State, current,
            new(1, districtId, hexId, sampler.Day450, main.Text, extra.Text, payload, MarkBit0: 1), sink);

        //These non-branching event scenes are prepared in source order. The
        //outbox retains each actual line; preparing it never acknowledges it.
        if (marriage)
        {
            int advisorId = Need(current.ReadAssignmentForceAdvisor52(forceId), "mediation_advisor52");
            var advisor = current.ReadPersonLookupReference(advisorId)
                ?? throw PersonnelAdministrationReportMissing("mediation_advisor_lookup");
            int advisorPhysical = Need(advisor.PhysicalSlot, "mediation_advisor_pointer");
            bool advisorValid = current.ReadPersonNativeValidity(advisorPhysical)
                ?? throw PersonnelAdministrationReportMissing("mediation_advisor_validity");
            if (!advisorValid)
            {
                advisor = current.ReadPersonLookupReference(5014)
                    ?? throw PersonnelAdministrationReportMissing("mediation_advisor_fallback5014");
                advisorPhysical = Need(advisor.PhysicalSlot, "mediation_advisor_fallback_pointer");
            }
            int v = Need(current.ReadPersonIdentity(advisorPhysical), "mediation_advisor_identity");
            Line(0x58B1, v, new(Person0: v, Person1: ruler, Person2: a, Person3: b));
            Line(0x58B5, a == ruler || b == ruler ? v : ruler, new(Person0: ruler, Person1: a, Person2: b));
            int principal = b == ruler ? b : a;
            int partner = b == ruler ? a : b;
            Line(0x58BE, principal, new(Person0: a, Person1: ruler, Person2: b));
            Line(0x58C2, principal, new(Person0: principal, Person1: partner));
            Line(0x58C3, b == ruler ? a : b, new(Person0: b, Person1: a));
            Line(0x58C4, null, new(Person0: 0, Person1: a, Person2: b));
        }
        else
        {
            bool female = state.Officers[a].Sex == OfficerSex.Female;
            int? rawForce = female ? current.ReadPersonOldForceSlot(Physical(a))
                ?? throw PersonnelAdministrationReportMissing("mediation_female_force_operand") : null;
            if (c is not int third)
            {
                Line(female ? 0x58EB : 0x58D2, a, new(Person0: a, Person1: ruler));
                Line(female ? 0x58EC : 0x58D3, a, new(Person0: a, Person1: b));
                Line(female ? 0x58ED : 0x58D4, b, new(Person0: b, Person1: a));
                Line(female ? 0x58EE : 0x58D5, ruler, new(Person0: ruler, Person1: a, Force0: rawForce));
                Line(female ? 0x58F6 : 0x58DD, null, new(Person0: 0, Person1: a, Person2: b, Force0: rawForce));
            }
            else
            {
                Line(female ? 0x58DE : 0x58C5, a, new(Person0: a, Person1: ruler));
                Line(female ? 0x58DF : 0x58C6, a, new(Person0: a, Person1: b, Person2: third));
                Line(female ? 0x58E0 : 0x58C7, b, new(Person0: b, Person1: a, Person2: third));
                Line(female ? 0x58E1 : 0x58C8, third, new(Person0: third, Person1: a, Person2: b));
                Line(female ? 0x58E2 : 0x58C9, ruler, new(Person0: ruler, Person1: a, Force0: rawForce));
                Line(female ? 0x58EA : 0x58D1, null, new(Person0: 0, Person1: a, Person2: b, Person3: third));
            }
        }
        if (ids.Contains(ruler))
        {
            var rulerHome = BaseLookup.Find(Need(current.ReadPersonFirstLocation(Physical(ruler)), "mediation_news_rulerE2"), current);
            var newsHex = current.ReadReportBaseHex(rulerHome)
                ?? throw PersonnelAdministrationReportMissing("mediation_news_home_hex");
            string FullName(OfficerState person, bool full)
                => (person.CurrentSurname ?? throw PersonnelAdministrationReportMissing("mediation_news_surname")) +
                    (full ? person.CurrentGivenName ?? throw PersonnelAdministrationReportMissing("mediation_news_given_name") : string.Empty);
            var labels = ids.Select(id => FullName(working.Officers[id], true)).ToList();
            while (labels.Count < 3) labels.Add(string.Empty);
            working = new CampaignChronicleWriter(HistoryWriter).Write(working, current,
                new(marriage ? 0x148 : c.HasValue ? 0x14A : 0x149, 1, sampler.Day450,
                    HistoryWriter.CustomFirstName(working, current, force, FullName),
                    Need(current.ReadForceReportColor(force), "mediation_news_color"),
                    marriage ? 0x61u : c.HasValue ? 0x63u : 0x62u, labels.ToImmutableArray(),
                    ImmutableArray.Create(Need(current.ReadReportMapCellPhysicalSlot(newsHex), "mediation_news_hex"), 48400, 48400, 48400)));
        }
        return working;

        void Line(int root, int? speaker, HistoryMessageOperands args)
        {
            var formatted = FormatPersonnelDevelopmentCurrent(working, current, root, args);
            working = formatted.State;
            if (speaker is int id)
                sink.Add(PersonnelAdministrationOfficerLine(current, sourceState, id, ruler, formatted));
            else PersonnelAdministrationAnnouncement(sink, formatted.Text);
        }
    }

    private HistoryTextResult FormatPersonnelDevelopmentCurrent(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands)
        => FormatReportMessage(state, current, root, operands, ReportMessageOrigin.PersonnelDevelopment,
            null, _personnelNarrationVoice);

    internal GameState ApplyPersonnelLearnReportCurrent(GameState state, SourceWorldContext current,
        int officerId, string tacticName, List<CampaignEffectRequest> sink)
    {
        var notice = FormatPersonnelDevelopmentCurrent(state, current, 0x598D,
            new(Person0: officerId, String0: tacticName));
        PersonnelAdministrationSound(sink, 4);
        PersonnelAdministrationAnnouncement(sink, notice.Text);
        return notice.State;
    }

    internal GameState ApplyPersonnelHireReportsCurrent(GameState state, SourceWorldContext current,
        int destinationLocationId, int selectedCount, List<CampaignEffectRequest> sink)
    {
        var destination = BaseLookup.Find(destinationLocationId, current);
        ushort raw = current.ReadBaseObjectLink(destination)
            ?? throw PersonnelAdministrationReportMissing("hire_destination4E");
        var notice = FormatPersonnelDevelopmentCurrent(state, current, 0x5CE6,
            new(Base0: raw, Integers: [selectedCount]));
        PersonnelAdministrationSound(sink, 4);
        PersonnelAdministrationAnnouncement(sink, notice.Text);
        return notice.State;
    }
}
