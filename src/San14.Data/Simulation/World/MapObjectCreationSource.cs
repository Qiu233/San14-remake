using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

internal sealed record MapObjectCreationResult(GameState State, ObjectReference? Object);

/// <summary>2D8E20 over the existing reusable campaign receivers and actual
/// lookup sequence. The caller owns its current pool and whole transaction.</summary>
internal static class MapObjectCreationSource
{
    internal static MapObjectCreationResult CreateFacility(GameState state, SourceWorldContext current,
        int hex, ObjectDefinitionReference definition, int rawForce)
    {
        ObjectReference? selected = null;
        // The source retains its last pointer after exhausting the table, then
        // rechecks validity. Preserve that read rather than inventing a new slot.
        for (int lookup = 0; lookup < 3001; lookup++)
        {
            selected = current.ReadObjectTableReference(lookup) ?? throw Missing("create_lookup_pointer");
            int physical = K(selected.PhysicalSlot, "create_Object_pointer");
            if (physical == 0 || physical is >= 1 and <= 51 || physical is >= 52 and <= 61) continue;
            if (!K(current.ReadObjectNativeValidity(selected), "create_candidate_validity")) break;
        }
        if (selected is null || K(current.ReadObjectNativeValidity(selected), "create_selected_revalidation")) return new(state, null);
        int at = K(selected.PhysicalSlot, "create_selected_identity");
        if (at is < 62 or > 3000) throw Missing("create_exhausted_lookup_Base_fallthrough");
        var source = CampaignSourceKnowledge.From(state.ResourceState).MapObjectSource ?? throw Missing("create_source_owner");
        var directory = current.CurrentMapObjectDirectory ?? throw Missing("create_directory");
        var owner = state.MapObjects ?? throw Missing("create_domain_owner");
        var id = directory.ReceiverAtPhysical(at);
        var receiver = owner.Find(id) ?? throw Missing("create_receiver");
        int kind = Math.Clamp(K(definition.PhysicalSlot, "create_definition_identity"), 0, 30);
        int position = Math.Clamp(hex, 0, 48399);
        byte faction = checked((byte)Math.Clamp(rawForce, 0, 51));
        bool knownFaction = faction == 0 || state.Forces.ContainsKey(faction);
        // The construction packet writes byte17=0 BEFORE clearing activation.
        //17 is the high byte of this saved16 word, not independent storage.
        ushort fields16 = (ushort)(K(current.ReadMapObjectFields16(selected), "create_existing_fields16") & 0x00ff);
        var structures = new Dictionary<int, StructureState>(state.Structures ?? []);
        int structure = structures.Count == 0 ? 1 : checked(structures.Keys.Max() + 1);
        if (receiver.Subject is { LocationId: not null }) throw Missing("create_nonBase_subject");
        if (receiver.Subject?.StructureId is int previous) structures.Remove(previous);
        structures.Add(structure, new(structure, MapObjectSourceDirectory.DefinitionKey(kind), position,
            faction == 0 || !knownFaction ? null : faction, 1, MaxDurability: null));
        var created = receiver with { Subject = new(OwnedMapObjectLifecycle.Category(Read(current, definition).Category),
                MapObjectSourceDirectory.DefinitionKey(kind), null, structure),
            Pose = new(MapObjectPoseKind.Hex, position), BaseMapFaction = null };
        source = source.WithConstructedReceiver(at, fields16, faction, knownFaction);
        var working = state with { MapObjects = owner.With(created), Structures = structures,
            ResourceState = CampaignSourceKnowledge.From(state.ResourceState).WithMapObjectSource(source) };
        // Rebind only the Object projection. Active Person temporaries and the
        //same operation's owned pool remain live and are not re-imported.
        OwnedMapObjectLifecycle.BindCurrent(working, current);
        if (!ObjectRegistration.Register(new(RegistryKind.Object, at), current))
        {
            structures.Remove(structure);
            working = working with { MapObjects = working.MapObjects!.With(created with
                { Subject = null, Pose = new(MapObjectPoseKind.None), BaseMapFaction = null }), Structures = structures,
                ResourceState = CampaignSourceKnowledge.From(working.ResourceState).WithMapObjectSource(source.Reset(at)) };
            OwnedMapObjectLifecycle.BindCurrent(working, current);
            return new(working, null);
        }
        return new(working, selected);
    }
}
