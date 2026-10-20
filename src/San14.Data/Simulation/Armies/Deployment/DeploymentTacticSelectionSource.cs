using San14.Core;

namespace San14.Data;

internal static class DeploymentTacticSelectionSource
{
    internal static int[] ReadCandidates(SourceWorldContext current, int id, int physical,
        int land, Func<OfficerAbilities> readAbilities, DeploymentAutomaticStyle style = DeploymentAutomaticStyle.FieldAttack)
    {
        if (!Known(current.ReadPersonNativeValidity(physical), "person_validity")) return [];
        var list = new List<TacticReference>();
        for (int index = 0; index < 10; index++)
        {
            var tactic = ReadCurrentSlot(current, id, physical, index);
            if (!Valid(tactic) || land is <= 0 or >= 21) continue;
            uint formationMask = 0;
            for (int part = 0; part < 4; part++) formationMask |= (uint)Known(current.ReadSkillTacticRawByte(tactic, 0x6C + part), "tactic_formation_mask") << (part * 8);
            if ((formationMask & (1u << land)) != 0) list.Add(tactic);
        }
        uint mainMask = style switch
        { DeploymentAutomaticStyle.SiegeTroops => 0x1BA080,
          DeploymentAutomaticStyle.SiegeDurability => 0x188200,
          DeploymentAutomaticStyle.Mobility or DeploymentAutomaticStyle.Occupation => 0x188408,
          _ => 0x19801C };
        uint firstMask = style switch
        { DeploymentAutomaticStyle.SiegeDurability => 0x40000,
          DeploymentAutomaticStyle.Mobility or DeploymentAutomaticStyle.Occupation => 0x20,
          _ => 2 };
        Partition(t => InMask(current.ReadTacticEffectOpcode(t, 1), mainMask));
        Partition(t => InMask(current.ReadTacticEffectOpcode(t, 1), firstMask));
        Partition(t => InMask(current.ReadTacticEffectOpcode(t, 0), mainMask));
        Partition(t => InMask(current.ReadTacticEffectOpcode(t, 0), firstMask));
        var effective = readAbilities();
        int preference = effective.Strength > effective.Intelligence + 10 ? 1 : effective.Intelligence > effective.Strength + 10 ? 2 : 0;
        if (preference != 0) Partition(t => Factor(t) == preference);
        Partition(t => Priority(Known(t.PhysicalSlot, "tactic_identity")));
        return list.Select(t => Known(t.PhysicalSlot, "tactic_identity")).ToArray();

        bool Valid(TacticReference tactic) => Known(current.ReadSkillTacticValidity(tactic), "tactic_validity");
        void Partition(Func<TacticReference, bool> criterion) => list = list.OrderByDescending(criterion).ToList();
        byte Factor(TacticReference tactic)
        {
            ulong end = Known(current.ReadTacticParameterEnd78(tactic), "tactic_vector_end");
            var first = current.ReadTacticParameterBegin70(tactic) ?? throw Missing("tactic_vector_begin");
            if ((unchecked(end - first.ByteAddress) & ~3UL) != 40)
                return Known(current.ReadSkillTacticRawByte(tactic, 0x6A), "tactic_factor");
            var begin = current.ReadTacticParameterBegin70(tactic) ?? throw Missing("tactic_vector_begin");
            var decoder = current.CaptureTacticParameterDecoderTable(tactic) ?? throw Missing("tactic_decoder");
            byte value = Known(current.ReadTacticParameterEncodedByte(new(begin, 9, unchecked(begin.ByteAddress + 36))), "tactic_factor_encoded");
            return Known(decoder.Decode(tactic, value, 7, Known(tactic.PhysicalSlot, "tactic_identity")), "tactic_factor_decode");
        }
    }

    internal static TacticReference ReadCurrentSlot(SourceWorldContext current, int id, int physical, int index)
    {
        int requested = Known(current.ReadPersonTacticSlot168(physical, index), "person_tactics");
        var tactic = current.ReadSkillTacticReference(requested < 201 ? requested : 0) ?? throw Missing("tactic_pointer");
        if (unchecked((uint)(id - 5101)) > 99 && unchecked((uint)(id - 4101)) > 99 &&
            Known(current.ReadSkillTacticValidity(tactic), "tactic_validity") &&
            (Known(current.ReadSkillTacticFlags(tactic), "tactic_qualification") & 2) == 0)
        {
            bool unlocked = false;
            for (int gem = 0; gem < 31; gem++)
            {
                var item = current.ReadSkillGemReference(gem) ?? throw Missing("gem_pointer");
                if (!Known(current.ReadSkillGemValidity(item), "gem_validity")) continue;
                if (Known(current.ReadSkillGemTactic52(item), "gem_tactic") != tactic.PhysicalSlot) continue;
                unlocked = true; break;
            }
            if (!unlocked) tactic = current.ReadSkillTacticReference(0) ?? throw Missing("tactic_pointer");
        }
        return tactic;
    }

    internal static DeploymentTacticSlots CreateDefaults(SourceWorldContext current, int id,
        int physical, int land, Func<OfficerAbilities> readAbilities)
    {
        var slots = new int[3];
        int[] candidates = ReadCandidates(current, id, physical, land, readAbilities);
        Array.Copy(candidates, slots, Math.Min(3, candidates.Length));
        return NormalizeThree(current, DeploymentTacticSlots.From(slots));
    }

    internal static DeploymentTacticSlots CoverCandidates(SourceWorldContext current, int id,
        int physical, int land, DeploymentTacticSlots previous, Func<OfficerAbilities> readAbilities)
    {
        int[] slots = previous.ToArray();
        int[] candidates = ReadCandidates(current, id, physical, land, readAbilities);
        Array.Copy(candidates, slots, Math.Min(3, candidates.Length));
        return DeploymentTacticSlots.From(slots);
    }

    internal static DeploymentTacticSlots FilterAndNormalize(SourceWorldContext current,
        int land, DeploymentTacticSlots previous)
    {
        int[] slots = previous.ToArray();
        for (int index = 0; index < slots.Length; index++)
        {
            //7C69C0 consumes receiver0's current mask too; validity is not a prefilter.
            var tactic = Reference(current, slots[index]);
            if (land == 7 || (uint)land >= 21 || (Mask(current, tactic) & (1u << land)) == 0)
                slots[index] = 0;
        }
        return NormalizeThree(current, DeploymentTacticSlots.From(slots));
    }

    internal static DeploymentTacticSlots NormalizeThree(SourceWorldContext current, DeploymentTacticSlots previous)
    {
        int[] slots = previous.ToArray();
        var values = slots.Select(id => Reference(current, id))
            .OrderByDescending(t => Priority(Known(t.PhysicalSlot, "tactic_identity")))
            .ThenBy(t => Known(t.PhysicalSlot, "tactic_identity"));
        int at = 0;
        foreach (var tactic in values)
            if (Known(current.ReadSkillTacticValidity(tactic), "tactic_validity"))
                slots[at++] = Known(tactic.PhysicalSlot, "tactic_identity");
        //7F20E0 deliberately leaves the unwritten tail intact and keeps duplicates.
        return DeploymentTacticSlots.From(slots);
    }

    private static TacticReference Reference(SourceWorldContext current, int requested)
    {
        if (requested < 0) throw Missing("negative_tactic_index");
        return current.ReadSkillTacticReference(requested < 201 ? requested : 0) ?? throw Missing("tactic_pointer");
    }
    private static uint Mask(SourceWorldContext current, TacticReference tactic)
    {
        uint mask = 0;
        for (int part = 0; part < 4; part++)
            mask |= (uint)Known(current.ReadSkillTacticRawByte(tactic, 0x6C + part), "tactic_formation_mask") << (8 * part);
        return mask;
    }
    private static bool InMask(byte code, uint mask) => (mask & (1u << code)) != 0;
    private static bool Priority(int tactic) => tactic is >= 31 and <= 130 or >= 140 and <= 200;
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("deployment.tactic_selection." + field);
}
