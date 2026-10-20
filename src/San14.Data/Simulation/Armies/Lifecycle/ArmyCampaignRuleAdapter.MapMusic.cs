using San14.Core;

namespace San14.Data;

public sealed record MapMusicStageResult(GameState? State, AudioMusicSourceSlot? Selection, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Source stage1 selects a theme at the start of each ten-day
    /// period. It does not access application settings or play audio. The saved
    /// intent is visible only with the successful simulation transaction.</summary>
    public MapMusicStageResult TryAdvanceMapMusicSelection(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.MapMusic ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Map music selection requires progress stage1.");
        var date = moment.PreWriterThirdDate;
        if (date.Hour != 0 || date.Day % 10 != 1) return new(state, null, null);
        try
        {
            var current = CaptureMapArmyGroupingCurrent(state) ?? throw OccupationMissing("music_current_world");
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw OccupationMissing("music_player");
            bool valid = OccupationKnown(current.ReadForceNativeValidity(player), "music_player_validity");
            //333F00 only reads a bit whose result4039F0 discards on this exit.
            bool useDoctrine = valid && HasCurrentPlayerActivity();
            int? doctrine = useDoctrine ? ReadCurrentRulerDoctrine() : null;
            var selected = AudioMapMusicSelector.SelectSourceSlot(new(useDoctrine, valid, doctrine));
            return new(selected.ShouldSubmit ? state with { PendingMapMusicIntent = new(moment.CalendarCycleOrdinal,
                selected.SourceSlot!.Value, true, 1000, 1000) } : state, selected, null);

            bool HasCurrentPlayerActivity()
            {
                int playerPhysical = OccupationKnown(current.ReadForcePhysicalSlot(player), "music_player_identity");
                var armies = MapArmyGroupingQueries(state, current);
                foreach (int physical in armies.ReadRegistry() ?? throw OccupationMissing("music_army_registry"))
                {
                    var force = armies.ReadForce(physical) ?? throw OccupationMissing("music_army_force");
                    if (!IsPlayerForce(force)) continue;
                    if (OccupationKnown(armies.IsAtHome2C96E0(physical), "music_army_at_home")) continue;
                    if (!OccupationKnown(armies.IsExcluded233560(physical), "music_army_excluded")) return true;
                }
                var lifecycle = state.ReadArmyLifecycle() ?? throw OccupationMissing("music_lifecycle");
                var areas = lifecycle.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Area)
                    ?? throw OccupationMissing("music_area_registry");
                foreach (var area in areas)
                {
                    if (area.Kind != WorldEntityKind.Area || area.Id is not int physical)
                        throw OccupationMissing("music_area_receiver");
                    int rawForce = OccupationKnown(current.ReadAreaCurrentForceSlot(new(physical)), "music_area_representative_owner");
                    var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                        ?? throw OccupationMissing("music_area_force");
                    if (!IsPlayerForce(force)) continue;
                    for (int h = AreaHexIterator.FindNextIndex(0, physical, current); h < 48400;
                        h = AreaHexIterator.FindNextIndex(h + 1, physical, current))
                    {
                        short occupant = unchecked((short)OccupationKnown(armies.ReadHex(h).Raw1C, "music_area_occupant"));
                        if (occupant is < 1 or > 500) continue;
                        if (!OccupationKnown(armies.ReadReceiver(occupant).IsValid, "music_area_army_validity")) continue;
                        var receiver = lifecycle.ReceiverSlots()[occupant];
                        var army = receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : receiver.PendingImage;
                        byte flags = OccupationKnown(army?.NativeTacticRuntime?.Flags26, "music_area_army_flags");
                        //232570 does not test occupant allegiance, exclusion,
                        //at-home state or status1 after native validity.
                        if ((flags & 1) != 0) return true;
                    }
                }
                return false;

                bool IsPlayerForce(ForceReference force) =>
                    OccupationKnown(current.ReadForceNativeValidity(force), "music_candidate_force_validity") &&
                    playerPhysical != 0 && OccupationKnown(current.ReadForcePhysicalSlot(force), "music_candidate_force_identity") == playerPhysical;
            }
            int ReadCurrentRulerDoctrine()
            {
                int physical = OccupationKnown(current.ReadForcePhysicalSlot(player), "music_player_identity");
                ushort ruler = OccupationKnown(current.ReadForceRecord(physical)?.RawRuler10, "music_current_ruler");
                var person = current.ReadPersonLookupReference(ruler <= 6000 ? ruler : 0)
                    ?? throw OccupationMissing("music_ruler_lookup");
                int personPhysical = OccupationKnown(person.PhysicalSlot, "music_ruler_receiver");
                int identity = OccupationKnown(current.ReadPersonIdentity(personPhysical), "music_ruler_identity");
                var officer = state.Officers.GetValueOrDefault(identity) ?? throw OccupationMissing("music_current_ruler_doctrine");
                int raw = officer.DoctrineId ?? officer.AbilityState?.ObservedDoctrineId
                    ?? throw OccupationMissing("music_current_ruler_doctrine");
                if (raw is < 0 or > 255) throw OccupationMissing("music_ruler_doctrine_byte");
                if (raw is 0 or >= 7) return 0;
                //233F10: a nonzero physical definition additionally needs a
                //nonempty current name. The domain owns these editable definitions.
                var definition = state.Doctrines.GetValueOrDefault(raw) ?? throw OccupationMissing("music_doctrine_definition");
                return definition.Name.Length != 0 ? raw : 0;
            }
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
}
