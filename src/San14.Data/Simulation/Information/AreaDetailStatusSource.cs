using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

internal sealed record AreaDetailStatusPublication(
    IReadOnlyDictionary<int, string> Text,
    IReadOnlyDictionary<int, string> UnresolvedFields,
    IReadOnlySet<int> HiddenNodes,
    IReadOnlyDictionary<int, MapCornerColors> TextColors,
    IReadOnlyDictionary<int, int> SpriteIds,
    IReadOnlyList<AreaInformationTrait> Traits);

public sealed partial class ArmyCampaignRuleAdapter
{
    //902610. These are fixed authored control identities, not trait IDs.
    //Offline source table1414B0BB0 contains exactly these five int32 values.
    private static readonly int[] AreaStatusTraitNodes = [92, 93, 94, 95, 96];

    internal AreaDetailStatusPublication ReadAreaDetailStatusCurrent(
        GameState state, SourceWorldContext current, int areaId)
    {
        var text = new Dictionary<int, string>();
        var unresolved = new Dictionary<int, string>();
        var hidden = new HashSet<int>();
        var colors = new Dictionary<int, MapCornerColors>();
        var sprites = new Dictionary<int, int>();
        var traits = new List<AreaInformationTrait>();
        var area = current.ReadAreaTableReference(areaId)
            ?? throw AreaDetailStatusMissing("Area_pointer");
        var palettes = MapSpritePalettes.Create();

        //The three labels are authored in the normal layout. The caller
        //changes only their color and their independent art marker.
        Disaster(66, 67, 1, 0x221, 12);
        Disaster(69, 70, 4, 0x221, 12);
        Disaster(72, 73, 0x10, 0x21C, 11);

        HexListHandle? temporary = null;
        try
        {
            int rawPerson = AreaStatusNeed(current.ReadAssignmentArea36(areaId), "Area36_officer");
            var person = (rawPerson <= 6000 ? current.ReadPersonLookupReference(rawPerson)
                : current.ReadTransferPersonTableReference(0)) ?? throw AreaDetailStatusMissing("Person_lookup");
            int physical = AreaStatusNeed(person.PhysicalSlot, "Person_pointer");
            if (!AreaStatusNeed(current.ReadPersonNativeValidity(physical), "Person_validity"))
            {
                foreach (int node in AreaStatusTraitNodes) { hidden.Add(node); text[node] = ""; }
            }
            else
            {
                //2307D0 scans all nine selector slots, preserving duplicates
                //and omitting only invalid personalities or failed appends.
                temporary = current.CreateTemporaryHexList() ?? throw AreaDetailStatusMissing("trait_list");
                var admitted = new List<(int Id, PersonalityReference Receiver)>();
                for (int slot = 0; slot < 9; slot++)
                {
                    int raw = AreaStatusNeed(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(
                        current, physical, slot), "Person_trait_slot_" + slot);
                    var trait = current.ReadPersonalityTableReference(raw is >= 0 and <= 355 ? raw : 0)
                        ?? throw AreaDetailStatusMissing("Personality_pointer");
                    if (AreaStatusNeed(current.ReadSkillPersonalityValidity(trait), "Personality_validity") &&
                        current.AppendSkillTemporaryPersonality(temporary, trait)) admitted.Add((raw, trait));
                }
                for (int slot = 0; slot < AreaStatusTraitNodes.Length; slot++)
                {
                    int node = AreaStatusTraitNodes[slot];
                    if (slot >= admitted.Count) { hidden.Add(node); text[node] = ""; continue; }
                    var entry = admitted[slot];
                    try
                    {
                        string name = current.ReadPersonalityName(entry.Receiver)
                            ?? throw AreaDetailStatusMissing("Personality_name");
                        text[node] = name;
                        byte style = AreaStatusNeed(current.ReadPersonalityByte(entry.Receiver, 0xBE), "Personality_BE_style");
                        traits.Add(new(node, entry.Id, name, style));
                    }
                    catch (InvalidDataException error) { unresolved[node] = error.Message; }
                }
            }
        }
        catch (InvalidDataException error)
        {
            //An unknown assignment/list is not a known absent officer. Keep
            //the independently resolved disaster values and report each slot.
            foreach (int node in AreaStatusTraitNodes)
                if (!hidden.Contains(node) && !traits.Any(entry => entry.Node == node)) unresolved.TryAdd(node, error.Message);
        }
        finally
        {
            if (temporary is not null)
            {
                current.ClearTemporaryHexList(temporary);
                current.ReleaseTemporaryHexList(temporary);
            }
        }
        return new(new ReadOnlyDictionary<int, string>(text), new ReadOnlyDictionary<int, string>(unresolved),
            hidden, new ReadOnlyDictionary<int, MapCornerColors>(colors), new ReadOnlyDictionary<int, int>(sprites),
            traits.AsReadOnly());

        void Disaster(int iconNode, int labelNode, int mask, int activeSprite, int activeColor)
        {
            try
            {
                bool active = (AreaStatusNeed(current.ReadAreaFlags38(area), "Area38_flags") & mask) != 0;
                sprites[iconNode] = active ? activeSprite : 0x21D;
                colors[labelNode] = palettes[active ? activeColor : 49];
            }
            catch (InvalidDataException error)
            {
                unresolved[iconNode] = error.Message;
                unresolved[labelNode] = error.Message;
            }
        }
    }

    private static T AreaStatusNeed<T>(T? value, string field) where T : struct
        => value ?? throw AreaDetailStatusMissing(field);
    private static InvalidDataException AreaDetailStatusMissing(string field)
        => new("area-detail.status." + field);
}
