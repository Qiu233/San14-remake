using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    internal ImmutableArray<int?> CaptureStoryActors() => Enumerable.Range(0, 5)
        .Select(index => _actors.GetValueOrDefault(-10 - index)?.PhysicalSlot).ToImmutableArray();

    internal CampaignStoryDialogueRoles? CaptureStoryDialogue()
    {
        if (_dialogueCast is not { } cast) return null;
        S14EventConditionPersonReference?[] people = [cast.Adviser, cast.MilitaryOfficer, cast.CivilOfficer,
            cast.SameFamilyOfficer, cast.OtherFamilyOfficer, cast.RemainingOfficer];
        return new(cast.CachedForce?.PhysicalSlot, people.Select(person => person is null ? (int?)null :
            Known(Queries.ReadConditionPersonCharacterId(person), "saved dialogue identity")).ToImmutableArray());
    }

    internal void RestoreExecutionContext(CampaignStoryExecutionCursor cursor)
    {
        _candidates.Clear();
        _candidates.AddRange(cursor.CandidatePersonPhysicalSlots.Select(person => new S14EventConditionPersonReference(person)));
        _actors.Clear();
        for (int index = 0; index < cursor.ActorPersonPhysicalSlots.Length; index++)
            if (cursor.ActorPersonPhysicalSlots[index] is int person) _actors[-10 - index] = new(person);
        PersonListBlocked = cursor.PersonListBlocked;
        if (cursor.DialogueRoles is not { } saved) _dialogueCast = null;
        else
        {
            S14EventConditionPersonReference? Read(int index) => saved.PersonIds[index] is int id
                ? (Queries.ReadConditionPersonTableSlot(id) ?? throw Missing("saved dialogue lookup")).Value
                    ?? throw Missing("saved dialogue receiver") : null;
            _dialogueCast = new(saved.ForcePhysicalSlot is int force ? new(force) : null,
                Read(0), Read(1), Read(2), Read(3), Read(4), Read(5));
        }
    }
}
