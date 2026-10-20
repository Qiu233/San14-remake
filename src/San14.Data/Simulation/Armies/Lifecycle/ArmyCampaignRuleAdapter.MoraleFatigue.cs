using San14.Core;

namespace San14.Data;

public sealed record ArmyMoraleFatigueReceipt(int ArmyId, int ReceiverSlot, byte Before, byte After,
    int TerrainAmount, int RequestedLoss, bool Connected, int? ProtectionSample);
public sealed record ArmyMoraleFatigueResult(GameState? State, ArmyMoraleFatigueReceipt[] Receipts, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Every fifth native day at22h, the complete current manager
    /// pays field morale fatigue. The existing real morale callback owns its
    /// caps/traits and unresolved zero-morale routing; this stage cannot skip it.</summary>
    public ArmyMoraleFatigueResult TryAdvanceArmyMoraleFatigue(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.ArmyMoraleFatigue ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Army morale fatigue requires its reached stage18 boundary.");
        if (moment.PreWriterThirdDate.Hour != 22 || moment.PreWriterThirdDate.Day % 5 != 0)
            return new(state, [], null);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            bool panelsBound = _baseCapacityInputs.BindCurrent(state, current);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            current.MaterializeFixedScenery(_landmarkDefinitions);
            // Old or missing current flags cannot fall back to the fixed row.
            for (int i = 0; i < 101; i++) current.SetSceneryFlags47(i, state.LandmarkInvestments?.GetValueOrDefault(i)?.Flags);
            var registry = current.ReadRegistryReferences(RegistryKind.Army) ?? throw FatigueMissing("complete_army_manager");
            var working = state;var receipts = new List<ArmyMoraleFatigueReceipt>();
            TacticSourceSamplerWorld? sampler = null;
            foreach (var entry in registry)
            {
                int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int slot } ? slot : throw FatigueMissing("army_pointer");
                var army = current.ReadArmyUnitTableReference(physical) ?? throw FatigueMissing("army_table");
                if (!FatigueKnown(current.ReadArmyUnitNativeValidity(army), "army_validity")) continue;
                var queries = new ArmyMapGroupingCurrentQueries(working, current, _scenario, _governorInputs, Seed.Language, SimulationRuleIdentity.Version);
                if (FatigueKnown(queries.IsAtHome2C96E0(physical), "at_home")) continue;
                if (FatigueKnown(queries.IsExcluded233560(physical), "excluded")) continue;
                var receiver = working.ReadArmyLifecycle()!.ReceiverSlots()[physical];
                if (receiver.ActiveUnitId is not int id || !working.Units.TryGetValue(id, out var image)) throw FatigueMissing("active_army_image");
                byte status = image.NativeTacticRuntime?.Status10 ?? throw FatigueMissing("army_status");
                if (unchecked((byte)(status - 1)) > 1) continue;
                ushort hex = FatigueKnown(ArmyMarchSourceAdapter.ReadCurrentHex(working, image), "current_hex");
                int terrain = ArmyNativeFormationQueries.Terrain(_map, _terrain, hex).Terrain.RawField40;
                if (FatigueKnown(BattleRemakeCurrentSettings.ReadAdvancedSettings(working), "advanced_settings"))
                    terrain = unchecked(terrain * FatigueKnown(BattleRemakeCurrentSettings.ReadAdvancedParameter(working, 27), "terrain_percent")) / 100;
                int? sample = null;int amount = terrain;
                var force = current.ReadArmyUnitForceReference(army) ?? throw FatigueMissing("force_pointer");
                if (FatigueKnown(current.ReadForceNativeValidity(force), "force_validity"))
                {
                    if (!panelsBound) throw FatigueMissing("current_force_panels");
                    int policy = FatigueKnown(current.ReadBaseForceModifier(force, 43), "force_panel43");
                    sampler ??= ReadFireStageSampler(working, moment);
                    sample = TacticEffectSourceSamplerMath.Sample(100, 1, hex, moment.PreWriterThirdDate.Hour, 0, 0, sampler);
                    if (sample.Value < policy && ProtectedArea(hex)) amount = 0;
                }
                bool connected = (image.NativeTacticRuntime!.Flags26 & 2) != 0;
                if (!connected)
                {
                    var bank = working.NativeArmySimulation?.Parameters ?? throw FatigueMissing("parameter_bank");
                    string key = SimulationParameterKeys.Morale.FieldFatigueBase;
                    if (bank.Float32Values.ContainsKey(key) || !bank.Int32Values.TryGetValue(key, out int baseline)) throw FatigueMissing("field_fatigue_base");
                    amount = unchecked(amount + baseline);
                }
                if (!panelsBound) throw FatigueMissing("current_force_caps");
                var applied = ArmyMoraleNonzeroCurrent.Apply(working, current, army, unchecked(-amount));
                if (applied.NativeReturn is null || applied.Receipt is not { } receipt) throw FatigueMissing(applied.UnresolvedOwner ?? "morale_callback");
                working = working with { Units = new(working.Units) { [id] = ArmyMoraleSourceAdapter.WithCurrent(image, receipt.After1A) } };
                receipts.Add(new(id, physical, receipt.Before1A, receipt.After1A, terrain, amount, connected, sample));
            }
            return new(working, receipts.ToArray(), null);

            bool ProtectedArea(int ownHex)
            {
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.Scenery) ?? throw FatigueMissing("scenery_registry"))
                {
                    if (entry is not { Kind: RegistryKind.Scenery, PhysicalSlot: int slot }) throw FatigueMissing("scenery_pointer");
                    var scenery = new SceneryReference(slot);
                    if (!FatigueKnown(current.ReadSceneryNativeValidity(scenery), "scenery_validity") ||
                        (FatigueKnown(current.ReadSceneryFlags47(scenery), "scenery_flags") & 1) == 0) continue;
                    int target = FatigueKnown(current.ReadSceneryHexIndex(scenery), "scenery_hex");
                    var targetHex = current.ReadHexTableReference(target < 48400 ? target : 0) ?? throw FatigueMissing("scenery_hex_pointer");
                    int area = targetHex.PhysicalSlot.HasValue ? FatigueKnown(current.ReadHexAreaSlot(targetHex), "scenery_area") : 0;
                    var own = current.ReadHexTableReference(ownHex < 48400 ? ownHex : 0) ?? throw FatigueMissing("own_hex_pointer");
                    if (area == FatigueKnown(current.ReadHexAreaSlot(own), "own_area")) return true;
                }
                return false;
            }
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }
    private static T FatigueKnown<T>(T? value, string field) where T : struct => value ?? throw FatigueMissing(field);
    private static InvalidDataException FatigueMissing(string field) => new("army.progress.morale-fatigue." + field);
}
