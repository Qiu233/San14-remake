using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // The selected actor and each 2A78D0 neighbor have a distinct pair predicate.
    // It is neither actor eligibility4950 nor the earlier contact2CA7B0 test.
    private bool IsDuelOpponentEligible(GameState state, SourceWorldContext current,
        ArmyUnitReference actor, ArmyUnitReference opponent, TacticRemakeCurrentAbilities abilities)
    {
        if (!(current.ReadArmyUnitNativeValidity(actor) ?? throw DuelMissing("opponent_actor_validity")) ||
            !(current.ReadArmyUnitNativeValidity(opponent) ?? throw DuelMissing("opponent_Army_validity"))) return false;
        if (current.IsArmyUnitMapWriteSkipped(opponent)) return false;
        int physical = opponent.PhysicalSlot ?? throw DuelMissing("opponent_Army_receiver");
        if (current.ReadArmyExclusionForQuerySnapshot(state, physical) ?? throw DuelMissing("opponent_excluded")) return false;
        if (DuelPreviouslyPaired(state, current, actor, opponent)) return false;

        var actorForce = current.ReadArmyUnitForceReference(actor);
        var opponentForce = current.ReadArmyUnitForceReference(opponent);
        if (!(current.ReadForceNativeValidity(actorForce) ?? throw DuelMissing("opponent_actor_Force_validity")) ||
            !(current.ReadForceNativeValidity(opponentForce) ?? throw DuelMissing("opponent_Force_validity"))) return false;
        int otherForce = current.ReadForcePhysicalSlot(opponentForce) ?? throw DuelMissing("opponent_Force_identity");
        if (otherForce == 51 || unchecked((uint)(otherForce - 46)) <= 4) return false;
        int ownForce = current.ReadForcePhysicalSlot(actorForce) ?? throw DuelMissing("opponent_actor_Force_identity");
        if (!(ArmyLogisticsPreparation.ReadNativeRelationGate(state, ownForce, otherForce)
            ?? throw DuelMissing("opponent_relation2CA650"))) return false;

        ushort rawHex = current.ReadArmyUnitRawHex2A(opponent) ?? throw DuelMissing("opponent_Hex2A");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw DuelMissing("opponent_Hex_pointer");
        if (hex.PhysicalSlot is not int hexPhysical) return false;
        byte surface = current.ReadHexSurfaceCode(hex) ?? throw DuelMissing("opponent_Hex_surface");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
            ?? throw DuelMissing("opponent_Terrain_pointer");
        if (terrain.PhysicalSlot is not int terrainPhysical) return false;
        int flags = PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrainPhysical)
            ?? throw DuelMissing("opponent_Terrain_flags");
        var arena = state.ReadArmyLifecycle() ?? throw DuelMissing("opponent_saved_arena");
        var slot = arena.ReceiverSlots()[physical];
        var image = (slot.ActiveUnitId is int unitId ? state.Units.GetValueOrDefault(unitId) : slot.PendingImage)
            ?? throw DuelMissing("opponent_saved_image");
        //22B2D0 selects current land/naval FORMATION. It is not Action37.
        byte formation = ((flags & 2) != 0 ? ArmyConfigurationSourceAdapter.ReadNaval(state, image) :
            ArmyConfigurationSourceAdapter.ReadCurrentLand(state, image)) ?? throw DuelMissing("opponent_effective_formation");
        if (formation == 4 || (flags & 2) != 0) return false;
        if ((current.ReadCaptureHexWord16(hexPhysical) ?? throw DuelMissing("opponent_Hex_fire")) != 0 || terrainPhysical == 7) return false;

        int? actorPerson = DuelCommander(current, actor);
        int? opponentPerson = DuelCommander(current, opponent);
        if (actorPerson is not int first || opponentPerson is not int second) return false;
        int opponentId = current.ReadPersonIdentity(second) ?? throw DuelMissing("opponent_Person_identity");
        if (BattleRemakeCurrentRegionFactor.IsNativeBandPerson(opponentId))
        {
            ushort firstLocation = current.ReadPersonFirstLocation(second) ?? throw DuelMissing("opponent_band_first_location");
            if (firstLocation is >= 1 and <= 61)
            {
                var force = current.ReadPersonForce(second) ?? throw DuelMissing("opponent_band_Force_pointer");
                int forceId = current.ReadForcePhysicalSlot(force) ?? throw DuelMissing("opponent_band_Force_identity");
                if (unchecked((uint)(forceId - 46)) > 4) return false;
            }
        }
        if ((OfficerHealthSourceAdapter.ReadByte(current, second) ?? throw DuelMissing("opponent_health")) >= 2) return false;
        int actorId = current.ReadPersonIdentity(first) ?? throw DuelMissing("opponent_actor_Person_identity");
        int actorStrength = Strength(actorId);
        short adjustment = state.Officers.GetValueOrDefault(actorId)?.DuelCondition?.QualificationAdjustment
            ?? throw DuelMissing("opponent_actor_adjustment");
        //16C already belongs to the saved allegiance observation. ReadCurrent
        //does not re-read the resource or bind/scan every Person for each pair.
        int disposition = _personDestructionInputs.ReadCurrent(state, first)?.AllegianceDisposition?.Value
            ?? throw DuelMissing("opponent_actor_allegiance_disposition");
        int opponentStrength = Strength(opponentId);
        int divisor = DuelOpponentParameters.Read(state, "adjustment-divisor");
        int margin = adjustment / divisor;
        if (disposition >= 0) margin = unchecked(margin + DuelOpponentParameters.Read(state, "nonnegative-disposition-bonus"));
        return opponentStrength <= unchecked(actorStrength + margin);

        int Strength(int identity) => abilities.ResolveDimension(state, identity, 1,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect))
            ?? throw DuelMissing("opponent_effective_strength");
    }
}

internal static class DuelOpponentParameters
{
    private const string Prefix = "duel-opportunity:opponent:";
    // Existing TC file-backed catalog, installed only by genuine fresh creation.
    // Independently extracted SC file-backed words through the actual
    // paired campaign consumer. This table does not borrow the TC values.
    private static readonly (string Name, int Value)[] FreshSc =
    [
        ("adjustment-divisor", 4),
        ("nonnegative-disposition-bonus", 5),
    ];
    internal static void AddFresh(string language, Dictionary<string, int> values,
        IReadOnlyDictionary<string, float> floating)
    {
        if (language == "SC")
        {
            foreach (var row in FreshSc) Add(row.Name, row.Value);
            return;
        }
        if (language != "TC") return;
        Add("adjustment-divisor", 4);
        Add("nonnegative-disposition-bonus", 5);
        void Add(string name, int value)
        {
            string key = Prefix + name;
            if (values.ContainsKey(key) || floating.ContainsKey(key)) throw DuelOpportunityParameters.Missing("fresh_opponent_parameter_owner");
            values.Add(key, value);
        }
    }
    internal static int Read(GameState state, string name) => DuelOpportunityParameters.Read(state, Prefix + name);
}
