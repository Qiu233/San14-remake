using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

internal sealed partial class TrapEffectApplication
{
    /// <summary>TC191920 categories2/4. The source is the actual Object(kind11),
    /// while178EC0 application, callbacks and defeat admission remain with the
    /// shared target-only application owner.</summary>
    private void ApplyDamage(ObjectReference facility, int kind, int channel, HexListHandle targets, int category = 4)
    {
        var record = Current.ReadPhysicalObject(Need(facility.PhysicalSlot, "damage_Object_pointer"))
            ?? throw Missing("damage_Object_record");
        int definitionLookup = record.RawField12 <= 30 ? record.RawField12 : 0;
        var reference = Current.ReadObjectDefinitionReference(definitionLookup)
            ?? throw Missing("damage_definition_pointer");
        if (!Need(Current.ReadObjectDefinitionValidity(reference), "damage_definition_validity")) return;
        //Object12 is a lookup key, not22DDA0's definition identity. Resolve
        //that pointer first so an aliased lookup keeps its actual kind/speech.
        if (kind != Need(reference.PhysicalSlot, "damage_definition_identity") ||
            channel is < 0 or > 1 ||
            !(category == 2 && kind == 5 || category == 4 && kind is (11 or 12 or 13 or 14 or 17)))
            throw Missing("damage_definition_kind_channel");
        var definition = Current.ReadFacilityEffectDefinition(reference) ?? throw Missing("damage_definition_effects");
        var effect = definition.Channel(channel);
        if (effect.Effect != 1) throw Missing("damage_channel_effect");
        int hp = Need(Current.ReadObjectRawField14(facility), "damage_Object_HP14");
        int basis = Need(Current.ReadObjectDefinitionBaseDurability(reference), "damage_definition_base_HP");
        float fraction = (float)hp / basis;
        //MINSS has the ratio as its second operand, preserving NaN rather
        //than silently choosing1 for an unsupported zero/zero definition.
        float attack = MathF.Min(1f, fraction);
        attack *= effect.Amount;
        attack *= (float)Read(State, AttackScale);
        //191920 actually reads/computes this argument even though16A260
        //uses the Object's own HP and independent ObjectTroopScale instead.
        float unusedTroopArgument = (float)Need(Current.ReadObjectRawField14(facility), "damage_Object_HP14_argument");
        unusedTroopArgument *= (float)Read(State, ArgumentDurabilityScale);
        unusedTroopArgument /= 100f;
        _ = unusedTroopArgument;

        foreach (var hex in FacilityEffectTargets.ReadHexes(Current, targets))
        {
            int at = Need(hex.PhysicalSlot, "damage_Hex_pointer");
            if (Current.ReadHexTableReference(at) != hex) throw Missing("damage_Hex_alias_16EB20");
            var selected = FieldHexOccupantSource.ReadCurrent(State, Current, at);
            var occupant = selected.Receipt ?? throw Missing(selected.UnresolvedOwner ?? "damage_16EB20");
            if (occupant.NativeKind != 27) continue;
            var army = Current.ReadArmyUnitTableReference(occupant.NativePhysicalSlot <= 500 ? occupant.NativePhysicalSlot : 0)
                ?? throw Missing("damage_Army_pointer");
            if (!Need(Current.ReadArmyUnitNativeValidity(army), "damage_Army_validity")) continue;
            int physical = Need(army.PhysicalSlot, "damage_Army_physical");
            if (Need(Current.ReadArmyExclusionForQuerySnapshot(State, physical), "damage_Army233560")) continue;
            int commanderId = Need(Current.ReadArmyUnitCommanderId(army), "damage_Commander12");
            var commander = (commanderId <= 6000 ? Current.ReadPersonLookupReference(commanderId)
                : Current.ReadTransferPersonTableReference(0))
                ?? throw Missing("damage_Commander_pointer");
            int commanderPhysical = Need(commander.PhysicalSlot, "damage_Commander_physical");
            if (!Need(Current.ReadPersonNativeValidity(commanderPhysical), "damage_Commander_validity")) continue;

            var image = Image(army);
            //The provider performs the reached Common(divisor1, region and
            //neighbor), actual siege attack and actual defense, in that order.
            var stats = TargetStats(image);
            _ = stats.SiegeTroopAttack;
            int sourceTroops = unchecked(Need(Current.ReadObjectRawField14(facility), "damage_Object_HP14_surrogate") *
                Read(State, ObjectTroopScale)) / 100;
            var parameters = DamageParameters();
            var strike = new TroopDamageSourceInput(sourceTroops, image.Troops, attack, stats.Defense,
                image.NativeTacticRuntime?.Status10 == 2, null, false);
            //Only a positive pre-half result reaches the pure commander56
            //query. This private arithmetic view does not alter saved values.
            var uncapped = parameters.WithCurrentValues(parameters.CurrentValues with { DamageCap = int.MaxValue },
                parameters.CurrentRevision + ":trap-pre-half");
            int preHalf = BattleDamageSource.CalculateTroopDamage(uncapped, strike);
            bool half = preHalf > 0 && Need(BattleRemakeCurrentDamageBodyQueries.ReadFirstMatchingPermanentEffect(
                Current, commanderPhysical, 56), "damage_Commander56");
            int calculated = BattleDamageSource.CalculateTroopDamage(parameters, strike with { TargetHalfDamageTrait = half });
            //16A260 writes i32.191C25..1C3B converts it to float32, converts
            //back withCVTTSS2SI, then retains the signed maximum with1.
            int basisDamage = Math.Max(1, Truncate((float)calculated));
            int policy = ReadTrapDamagePolicy20(facility);
            int scaledPolicy = unchecked(Read(State, PolicyDamageScale) * policy);
            //191C77/1C7F use wrapping i32 IMUL twice, then signed /100.
            //The policy contribution does not pass through float32.
            int policyDamage = unchecked(scaledPolicy * basisDamage) / 100;
            //The category2 panel is read before either target reduction.
            //Category4 never reaches either category2 query.
            int panelDamage = category == 2 ? FacilitySourcePanelDamage(facility, basisDamage) : 0;
            int traitDamage = 0;
            if (category == 4)
            {
                int[] traits = TacticRemakeCurrentTraits.ArmyContributions(Current, commanderPhysical,
                    ArmyTraitSourceAdapter.Encode(State, image)) ?? throw Missing("damage_target_traits127");
                if (traits.Length != 127) throw Missing("damage_target_traits127_count");
                //191D04..1D27: float32 (trait * base) * scale /100.
                float reduction = (float)traits[27] * (float)basisDamage;
                reduction *= (float)Read(State, TraitDamageScale);
                reduction /= 100f;
                traitDamage = reduction > 0f && reduction < 1f ? 1
                    : reduction < 0f && reduction > -1f ? -1 : Truncate(reduction);
            }
            int personDamage = category == 2 ? FacilityTargetPersonDamage(army, basisDamage) : 0;
            //191DBC..1DCE preserves each wrapping i32 operation before max0.
            int gross = unchecked(panelDamage - personDamage);
            gross = unchecked(gross - traitDamage);
            gross = unchecked(gross + policyDamage);
            gross = Math.Max(0, unchecked(gross + basisDamage));
            ApplyObjectDamage(army, gross, facility);
            //These native speech calls follow178EC0, even if that call has
            //marked or immediately retired the victim. Do not gate on survival.
            if (category == 2) Feedback.Speech(army, CombatSpeechEvent.ArmyUnderFacilityAttack, facility, kind);
            else if (kind == 14) Feedback.Speech(army, CombatSpeechEvent.TrapPitfall, facility);
            else if (kind == 17) Feedback.Speech(army, CombatSpeechEvent.TrapRockfall, facility);
            if (Need(Current.ReadArmyUnitNativeValidity(army), "damage_Army_post_validity")) RecordGross(army, gross);
            AddVictimForce(Current.ReadArmyUnitForceReference(army));
        }
    }

    private int ReadTrapDamagePolicy20(ObjectReference facility)
    {
        int raw = Need(Current.ReadObjectForceSlot13(facility), "damage_source_Force13");
        var force = Current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("damage_source_Force_pointer");
        if (!Need(Current.ReadForceNativeValidity(force), "damage_source_Force_validity")) return 0;
        int physical = Need(Current.ReadForcePhysicalSlot(force), "damage_source_Force_physical");
        var image = State.Forces.GetValueOrDefault(physical) ?? throw Missing("damage_source_Force_image");
        return Need(BattleRemakeCurrentForcePolicies.ReadForceEffect(State, image, 20, Current), "damage_source_policy20");
    }
}
