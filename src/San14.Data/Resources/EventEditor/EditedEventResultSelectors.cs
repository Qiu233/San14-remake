using San14.Core;
using San14.Data.Formats;
using System.Collections.Immutable;

namespace San14.Data;

/// <summary>The same ordinary selector kernel over one result transaction's
/// current and the actual cast saved when its presentation was prepared.</summary>
internal sealed class EditedEventResultSelectors
{
    private readonly SourceWorldContext current;
    private readonly CampaignStorySelectionContext? selections;
    private readonly CampaignEventExecutionContext invocation;
    private readonly Func<CampaignEventExecutionInputs> readCurrentInputs;
    private bool actorsRestored;
    private bool dialogueRestored;

    internal EditedEventResultSelectors(SourceWorldContext current, CampaignStorySelectionContext? selections,
        Func<CampaignEventExecutionInputs> readCurrentInputs)
    {
        this.current = current;
        this.selections = selections;
        this.readCurrentInputs = readCurrentInputs;
        // Ability/scenery/camera queries are not selector dependencies. No
        // second World capture, presentation replay or candidate selection.
        invocation = new(new(current, (_, _) => null, _ => null, _ => null, false));
    }

    internal int Person(int selector)
    {
        if (selector is >= -14 and <= -10) RestoreActors();
        if (selector is >= -3 and <= -1 or >= -17 and <= -15)
        {
            if (selections is null) throw Missing("lazy_dialogue_context");
            if (!dialogueRestored)
            {
                invocation.RestoreDialogueCast(RestoreDialogue(selections.DialogueRoles));
                dialogueRestored = true;
            }
            // Preserve the actual adviser-pointer latch. Only a real first
            // use (or an unlatched previous generation) reads latest inputs.
            if (invocation.ExportDialogueCast()?.Adviser is null)
                invocation.RefreshResultInputs(readCurrentInputs());
        }
        return invocation.ResolvePerson(selector, TargetForce).PhysicalSlot;
    }

    internal ForceReference Force(int selector)
    {
        if (selector is >= -10 and <= -6) RestoreActors();
        return new(invocation.ResolveResultForce(selector, TargetForce).PhysicalSlot);
    }

    internal BaseReference Base(int selector) => invocation.ResolveResultBase(selector);

    // City3D9270 has its own selector domain. In particular -4 means the
    // Emperor's actual receiving City here, unlike the Base3DA380 domain.
    internal BaseReference City(int selector)
    {
        if (selector == -1) return Base(-1);
        int physical;
        if (selector == -4)
        {
            physical = 0;
            int emperor = Person(-9);
            if (Need(current.ReadPersonNativeValidity(emperor), "emperor_validity"))
            {
                var position = PersonPositionRepair.DecodeSecondLocation(
                    Need(current.ReadPersonSecondLocation(emperor), "emperor_position"));
                if (position.Kind == 5 && position.Index < 52) physical = position.Index;
            }
        }
        else if (selector is >= -9 and <= -2)
        {
            //3D2E50 resets these selected-city pointers; no supported
            //presentation writer changes them. Unsupported writers stop
            //before any prepared Story or business candidate is published.
            physical = 0;
        }
        else if (selector < 0) throw Missing("city_selector");
        else physical = selector < 52 ? selector : 0;
        return current.ReadCityTableReference(physical) ?? throw Missing("city_pointer");
    }

    private S14EventConditionForceReference? TargetForce(int selector)
    {
        if (selections is null) throw Missing("condition_target_forces");
        return selections.TargetForcePhysicalSlots[-selector - 2] is int slot ? new(slot) : null;
    }

    private void RestoreActors()
    {
        if (selections is null) throw Missing("selected_cast");
        if (actorsRestored) return;
        invocation.RestoreResultActors(selections.ActorIds);
        actorsRestored = true;
    }

    internal static CampaignStoryDialogueRoles? SaveDialogue(CampaignEventDialogueCast? cast,
        IS14PlayerEventConditionQueries queries)
    {
        if (cast is null) return null;
        S14EventConditionPersonReference?[] people = [cast.Adviser, cast.MilitaryOfficer, cast.CivilOfficer,
            cast.SameFamilyOfficer, cast.OtherFamilyOfficer, cast.RemainingOfficer];
        return new(cast.CachedForce?.PhysicalSlot, people.Select(person => person is null ? (int?)null :
            Need(queries.ReadConditionPersonCharacterId(person), "dialogue_saved_identity")).ToImmutableArray());
    }

    private CampaignEventDialogueCast? RestoreDialogue(CampaignStoryDialogueRoles? saved)
    {
        if (saved is null) return null;
        S14EventConditionPersonReference? PersonAt(int index) => saved.PersonIds[index] is int id
            ? (current.ReadConditionPersonTableSlot(id) ?? throw Missing("dialogue_saved_lookup")).Value
                ?? throw Missing("dialogue_saved_receiver") : null;
        return new(saved.ForcePhysicalSlot is int force ? new(force) : null,
            PersonAt(0), PersonAt(1), PersonAt(2), PersonAt(3), PersonAt(4), PersonAt(5));
    }

    private static T Need<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) =>
        new("edited_event.results.selection." + owner);
}
