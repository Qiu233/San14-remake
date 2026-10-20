using San14.Core;

namespace San14.Data;

internal static class DuelOpportunityEligibility
{
    internal static bool IsEligible(GameState state, SourceWorldContext current, ArmyUnitReference army,
        TacticRemakeCurrentAbilities abilities, OwnedBaseCapacityInputs panels)
    {
        //The source calls validity here but discards its result.
        _ = current.ReadArmyUnitNativeValidity(army);
        int upper = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
        byte morale = K(current.ReadArmyReturnResourceFields(army)?.Morale1A, "eligibility_morale");
        if (Math.Min(upper, morale) <= 0) return false;
        if (current.IsArmyUnitMapWriteSkipped(army)) return false;
        int physical = K(army.PhysicalSlot, "eligibility_Army_receiver");
        if (K(current.ReadArmyExclusionForQuerySnapshot(state, physical), "eligibility_excluded")) return false;
        var force = current.ReadArmyUnitForceReference(army);
        if (!K(current.ReadForceNativeValidity(force), "eligibility_Force_validity")) return false;
        if (K(current.ReadForcePhysicalSlot(force), "eligibility_Force_first") == 51) return false;
        int forceSlot = K(current.ReadForcePhysicalSlot(force), "eligibility_Force_second");
        if (unchecked((uint)(forceSlot - 46)) <= 4) return false;
        if (K(current.ReadArmyReturnUnitStatus10(army), "eligibility_status") != 1) return false;
        var slot = state.ReadArmyLifecycle()?.ReceiverSlots().ElementAtOrDefault(physical)
            ?? throw Missing("eligibility_Army_owner");
        var image = slot.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : slot.PendingImage;
        if (image?.NativeTacticRuntime is not { } runtime) throw Missing("eligibility_abnormal_owner");
        if (runtime.Abnormal23 != 0) return false;
        if (runtime.Abnormal25 != 0) return false;
        if (runtime.Abnormal24 != 0) return false;
        ushort rawHex = K(current.ReadArmyUnitRawHex2A(army), "eligibility_Hex2A");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw Missing("eligibility_Hex_pointer");
        if (hex.PhysicalSlot is not int hexPhysical) return false;
        byte rawTerrain = K(current.ReadHexSurfaceCode(hex), "eligibility_Hex_surface");
        var terrain = current.ReadSurfaceTypeTableReference(rawTerrain < 31 ? rawTerrain : 0)
            ?? throw Missing("eligibility_Terrain_pointer");
        if (terrain.PhysicalSlot is not int terrainPhysical) return false;
        if ((K(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrainPhysical), "eligibility_Terrain_flags") & 2) != 0) return false;
        if (K(current.ReadCaptureHexWord16(hexPhysical), "eligibility_Hex_fire16") != 0) return false;
        if (terrainPhysical == 7) return false;
        ushort commander = K(current.ReadArmyUnitCommanderId(army), "eligibility_Commander12");
        var person = (commander <= 6000 ? current.ReadPersonLookupReference(commander) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("eligibility_Person_pointer");
        if (person.PhysicalSlot is not int personPhysical ||
            !K(current.ReadPersonNativeValidity(personPhysical), "eligibility_Person_validity")) return false;
        int identity = K(current.ReadPersonIdentity(personPhysical), "eligibility_Person_identity");
        if (BattleRemakeCurrentRegionFactor.IsNativeBandPerson(identity))
        {
            ushort first = K(current.ReadPersonFirstLocation(personPhysical), "eligibility_band_first_location");
            if (first != 0 && unchecked((uint)(first - 1)) <= 60)
            {
                var personForce = current.ReadPersonForce(personPhysical) ?? throw Missing("eligibility_band_Force");
                int personForceSlot = K(current.ReadForcePhysicalSlot(personForce), "eligibility_band_Force_identity");
                if (unchecked((uint)(personForceSlot - 46)) > 4) return false;
            }
        }
        if (K(OfficerHealthSourceAdapter.ReadByte(current, personPhysical), "eligibility_health") >= 2) return false;
        var officer = state.Officers.GetValueOrDefault(identity) ?? throw Missing("eligibility_Person_owner");
        short cachedAdjustment = K(officer.DuelCondition?.QualificationAdjustment, "eligibility_adjustment");
        int strength = K(abilities.ResolveDimension(state, identity, 1,
            (owner, effect) => panels.ReadPanelModifier(state, owner, effect)), "eligibility_effective_strength");
        int threshold = DuelOpportunityParameters.Read(state, DuelOpportunityParameters.MinimumEffectiveStrength);
        if (unchecked(threshold - cachedAdjustment) > strength) return false;
        //Fresh source read of16A precedes the F1 read. The channels are independent.
        short adjustment = K(state.Officers.GetValueOrDefault(identity)?.DuelCondition?.QualificationAdjustment, "eligibility_adjustment_again");
        byte readiness = K(state.Officers.GetValueOrDefault(identity)?.DuelCondition?.Readiness, "eligibility_readiness");
        return 80 - adjustment <= readiness;
    }

    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => DuelOpportunityParameters.Missing(field);
}
