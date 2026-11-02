using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record PersonnelCreditEpisodeLine(int SpeakerId, int CounterpartId, int MessageRoot);
internal sealed record PersonnelCreditEpisodeSelection(int Slot,
    TalentRecruitConversationContext Actors, IReadOnlyList<PersonnelCreditEpisodeLine> Lines);
internal sealed record PersonnelCreditRankupPresentationSource(int ForceId,
    IReadOnlyList<PersonnelCreditEpisodeSelection> Episodes,
    IReadOnlyList<int> RankupPeople, IReadOnlyList<int> ChoiceOnlyPeople);

public sealed partial class ArmyCampaignRuleAdapter
{
    private LoadedPersonnelCreditEpisodes? _personnelCreditEpisodes;

    private S14EpisodeCatalog CreditEpisodes()
    {
        if (_personnelCreditEpisodes is null)
        {
            var loaded = _repository.LoadPersonnelCreditEpisodes(_scenario.FixedDataSource.SourcePath);
            if (loaded.Source.ResourceUri != _scenario.FixedDataSource.ResourceUri ||
                loaded.Source.Sha256 != _scenario.FixedDataSource.Sha256)
                throw CreditMissing("Episode_fixed_resource_owner");
            _personnelCreditEpisodes = loaded;
        }
        return _personnelCreditEpisodes.Catalog;
    }

    private PersonnelCreditRankupPresentationSource PreparePersonnelCreditRankupPresentationCurrent(
        GameState state, CreditCommandCurrentRules rules, IReadOnlyList<OfficerCreditChoice> choices)
    {
        var current = rules.Current;
        var force = current.ReadForceTableReference(rules.ForceId) ?? throw CreditMissing("Episode_Force_pointer");
        int ruler = TalentRulerCurrent(current, force);
        var episodes = new List<PersonnelCreditEpisodeSelection>();
        var rankup = new List<int>(); var choiceOnly = new List<int>();
        //Presentation groups preserve the accepted UI chain. Only the later
        //formal numerical map is consumed in ascending Person identity order.
        foreach (var choice in choices)
        {
            var row = rules.People.First(person => person.Id == choice.OfficerId);
            if (row.NextStage <= row.CurrentStage)
            {
                if (choice.ChoiceCode != CreditChoiceSource.Original(row)) choiceOnly.Add(row.Id);
                continue;
            }
            var episode = row.NextStage == 6
                ? PersonnelCreditEpisodeSource.Find(CreditEpisodes(), checked((ushort)row.Id), checked((ushort)ruler))
                : null;
            if (episode is null) { rankup.Add(row.Id); continue; }
            var actors = CaptureCreditEpisodeActors(current, state, ruler, episode.PersonId);
            var lines = new List<PersonnelCreditEpisodeLine>();
            for (int index = 0; index < episode.SpeakerPersonIds.Count; index++)
            {
                ushort rawSpeaker = episode.SpeakerPersonIds[index];
                var reference = current.ReadPersonLookupReference(rawSpeaker <= 6000 ? rawSpeaker : 0)
                    ?? throw CreditMissing("Episode_speaker_pointer");
                int physical = CreditNeed(reference.PhysicalSlot, "Episode_speaker_physical");
                //The real five-row loop stops at the first native-invalid
                //speaker or a wrapped zero root, not merely raw index<=0.
                if (!CreditNeed(current.ReadPersonNativeValidity(physical), "Episode_speaker_validity")) break;
                int root = PersonnelCreditEpisodeSource.MessageRoot(episode.MessageIndices[index]);
                if (root == 0) break;
                int speaker = CreditNeed(current.ReadPersonIdentity(physical), "Episode_speaker_identity");
                if (speaker != ruler && speaker != episode.PersonId)
                    throw CreditMissing("Episode_two_portrait_speaker");
                lines.Add(new(speaker, speaker == ruler ? episode.PersonId : ruler, root));
            }
            episodes.Add(new(episode.Slot, actors, lines.AsReadOnly()));
        }
        return new(rules.ForceId, episodes.AsReadOnly(), rankup.AsReadOnly(), choiceOnly.AsReadOnly());
    }

    private GameState ApplyPersonnelCreditRankupPresentationCurrent(GameState state, SourceWorldContext current,
        List<CampaignEffectRequest> sink, GameState sourceState, PersonnelCreditRankupPresentationSource source)
    {
        //The formal numerical writer and resource payment have succeeded.
        //3EA090 only notifies the original application achievement service;
        //Steam interoperability is outside this standalone product's scope.
        PersonnelAdministrationSound(sink, 5);
        foreach (var episode in source.Episodes)
            foreach (var row in episode.Lines)
            {
                var line = FormatPersonnelAdministrationCurrent(state, current, row.MessageRoot,
                    new(Person0: row.SpeakerId, Person1: row.CounterpartId));
                if (line.Voices.Count != 0) throw CreditMissing("Episode_unexpected_voice_request");
                sink.Add(new(null, null)
                {
                    TalentCommandPresentation = new(TalentCommandPresentationOperation.OfficerText,
                        null, row.SpeakerId, episode.Actors.TargetOfficerId, null, line.Text)
                    { RecruitConversation = episode.Actors, CreditEpisodeSlot = episode.Slot }
                });
                state = line.State;
            }
        state = ApplyPersonnelCreditReportsCurrent(state, current, source.ForceId, source.RankupPeople,
            true, sink, sourceState, emitInitialSound: false);
        return ApplyPersonnelCreditReportsCurrent(state, current, source.ForceId, source.ChoiceOnlyPeople,
            true, sink, sourceState, emitInitialSound: false, choiceOnly: true);
    }

    private static TalentRecruitConversationContext CaptureCreditEpisodeActors(SourceWorldContext current,
        GameState state, int ruler, int person)
    {
        return new(ruler, person, CreditEpisodeActorName(state, ruler), CreditEpisodeActorName(state, person),
            Appearance(ruler), Appearance(person));
        ushort? Appearance(int id) => current.ReadPersonPortraitAppearance(current.ResolveOfficerSource(id)
            ?? throw CreditMissing("Episode_source_person_pointer"));
    }

    //Names and portrait knowledge belong to the pre-commit selected actors.
    private static string CreditEpisodeActorName(GameState state, int id)
    {
        var person = state.Officers.GetValueOrDefault(id) ?? throw CreditMissing("Episode_source_person");
        return (person.CurrentSurname ?? throw CreditMissing("Episode_source_surname")) +
            (person.CurrentGivenName ?? throw CreditMissing("Episode_source_given_name"));
    }
}
