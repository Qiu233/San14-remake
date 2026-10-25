using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState RefreshNormalTurnCollectionServiceCurrent(GameState state, SourceWorldContext current)
    {
        //Bind actual application facts without demanding a profile for a
        //collection predicate that fails before any service write.
        BindApplicationServiceCurrent(state, current);
        var working = state;
        RunNormalTurnCollectionChecksCurrent(state, current, flag =>
            working = PrepareApplicationServiceCurrent(working, current,
                service => SetNormalTurnCollectionAchievementCurrent(current, service, flag)));
        return working;
    }

    //Complete service candidate used by the normal-operation owner when it
    //has the real profile/clock bindings. The input is never mutated; the
    //caller must preserve and commit its actual monotone service changes.
    internal TransferServiceState PrepareNormalTurnCollectionServiceCurrent(GameState state,
        SourceWorldContext current, TransferServiceState service)
    {
        var candidate = service.Copy();
        RunNormalTurnCollectionChecksCurrent(state, current,
            flag => SetNormalTurnCollectionAchievementCurrent(current, candidate, flag));
        return candidate;
    }

    private static TransferServiceState SetNormalTurnCollectionAchievementCurrent(SourceWorldContext current,
        TransferServiceState candidate, int flag)
    {
        if (candidate.PlatformPolicy != TransferServicePlatformPolicy.Absent)
            throw ClosingMissing("collection_platform_preparation");
        TransferService.SetFlag(flag, current, candidate);
        for (int bit = 1; bit <= 37; bit++)
            if (!ClosingNeed(candidate.ReadPrimaryFlag(bit), "collection_completion_bit")) return candidate;
        TransferService.SetFlag(0, current, candidate);
        return candidate;
    }

    //428F10 retains the four calls and their independent guards in source
    //order: item50, three Area level5 checks, ten landmarks, item-kind26.
    private void RunNormalTurnCollectionChecksCurrent(GameState state, SourceWorldContext current,
        Action<int> setAchievement)
    {
        CheckItems(false);
        CheckAreaLevels();
        CheckLandmarks();
        CheckItems(true);

        bool Eligible() => ClosingNeed(current.ReadTransferServiceEligibility(), "collection_3342B0_eligibility");
        ForceReference? ValidPlayer()
        {
            var force = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw ClosingMissing("collection_player_pointer");
            return ClosingNeed(current.ReadForceNativeValidity(force), "collection_player_validity") ? force : null;
        }
        void CheckItems(bool special)
        {
            if (!Eligible()) return;
            var player = ValidPlayer();
            if (player is null) return;
            int ordinal = ClosingNeed(current.ReadForcePhysicalSlot(player), "collection_player_ordinal");
            int count = 0;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item) ?? throw ClosingMissing("collection_Item110"))
            {
                var item = entry ?? throw ClosingMissing("collection_Item_pointer");
                if (!ClosingNeed(current.ReadRegistryObjectNativeValidity(item), "collection_Item_validity")) continue;
                int physical = ClosingNeed(item.PhysicalSlot, "collection_Item_physical");
                int raw = ClosingNeed(current.ReadCaptureItemOwner4A(physical), "collection_Item4A");
                var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                    ?? throw ClosingMissing("collection_Item_holder_pointer");
                int holder = ClosingNeed(person.PhysicalSlot, "collection_Item_holder_physical");
                if (!ClosingNeed(current.ReadPersonNativeValidity(holder), "collection_Item_holder_validity")) continue;
                var force = current.ReadPersonForce(holder) ?? throw ClosingMissing("collection_Item_holder_Force");
                if (ClosingNeed(current.ReadForcePhysicalSlot(force), "collection_Item_holder_ordinal") != ordinal) continue;
                if (special)
                {
                    var itemSource = _scenario.Snapshot.ScenarioItems?.Items.SingleOrDefault(row => row.Slot == physical)
                        ?? throw ClosingMissing("collection_Item_definition44");
                    //238790's immutable44..49 window is serialized at58..63.
                    //S14ItemDefinition.RawKind names45, not this44 category.
                    if (itemSource.RecordStride != 65 || itemSource.RawBytes.Length != 65)
                        throw ClosingMissing("collection_Item_definition44_format");
                    if (itemSource.RawBytes[58] != 26) continue;
                }
                count++;
                if (special && count > 4) break;
            }
            if (special ? count > 4 : count > 49) setAchievement(special ? 52 : 12);
        }
        void CheckAreaLevels()
        {
            //3EA580 checks current player validity before any per-field service
            //gate. A reached level5 delegates to its own guarded achievement.
            var player = ValidPlayer();
            if (player is null) return;
            int ordinal = ClosingNeed(current.ReadForcePhysicalSlot(player), "collection_Area_player_ordinal");
            if (ordinal is < 1 or > 51) return;
            bool commerce = false, agriculture = false, barracks = false;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Area) ?? throw ClosingMissing("collection_AreaB0"))
            {
                int physical = ClosingNeed(entry?.PhysicalSlot, "collection_Area_pointer");
                var area = new AreaReference(physical);
                if (ClosingNeed(current.ReadAreaCurrentForceSlot(area), "collection_Area_Force") != ordinal) continue;
                if (!commerce && Level(physical, CeAreaDevelopmentField.Commerce) == 5)
                    commerce = CheckLevelAchievement(31);
                if (!agriculture && Level(physical, CeAreaDevelopmentField.Agriculture) == 5)
                    agriculture = CheckLevelAchievement(32);
                if (!barracks && Level(physical, CeAreaDevelopmentField.Barracks) == 5)
                    barracks = CheckLevelAchievement(33);
            }
        }
        bool CheckLevelAchievement(int flag)
        {
            if (!Eligible()) return false;
            setAchievement(flag); return true;
        }
        int Level(int physical, CeAreaDevelopmentField field)
        {
            var development = state.Areas.GetValueOrDefault(physical)?.Development ?? throw ClosingMissing("collection_Area_development");
            int value = ClosingNeed(field switch {
                CeAreaDevelopmentField.Commerce => development.Commerce,
                CeAreaDevelopmentField.Agriculture => development.Agriculture,
                CeAreaDevelopmentField.Barracks => development.Barracks,
                _ => null }, "collection_Area_progress");
            if (value is < 0 or > ushort.MaxValue) throw ClosingMissing("collection_Area_progress_range");
            return CeAreaIncomeMath.Level((ushort)value);
        }
        void CheckLandmarks()
        {
            if (!Eligible()) return;
            var player = ValidPlayer();
            if (player is null) return;
            int ordinal = ClosingNeed(current.ReadForcePhysicalSlot(player), "collection_landmark_player_ordinal");
            //3E9830 reads the complete101-pointer table, including the
            //invalid placeholder, and skips invalid definitions before kind.
            current.MaterializeFixedScenery(_landmarkDefinitions);
            for (int slot = 0; slot < 101; slot++)
            {
                var scenery = current.ReadConditionSceneryTableSlot(slot) ?? throw ClosingMissing("collection_landmark_pointer");
                if (!ClosingNeed(current.ReadConditionSceneryValidity(scenery), "collection_landmark_validity")) continue;
                int physical = ClosingNeed(scenery.PhysicalSlot, "collection_landmark_physical");
                if (physical is not (7 or 19 or 26 or 29 or 37 or 43 or 57 or 65 or 81 or 85)) continue;
                //The flag comes from the saved lifecycle owner, never from
                //fixed scenery initialization or a stale scenario snapshot.
                current.SetSceneryFlags47(physical, state.LandmarkInvestments?.GetValueOrDefault(physical)?.Flags);
                if ((ClosingNeed(current.ReadConditionSceneryFlags(scenery), "collection_landmark_flags47") & 1) == 0) return;
                if (ClosingNeed(current.ReadConditionSceneryForceSlot(scenery), "collection_landmark_Force") != ordinal) return;
            }
            setAchievement(48);
        }
    }
}
