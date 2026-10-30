using San14.Core;

namespace San14.Data;

/// <summary>Current content for one registered landmark map label. Identity
/// follows actual child construction order; zero is a real neutral owner.</summary>
public sealed record LandmarkMapEntry(int LandmarkId, string Name, int HexId, int? OwnerForceId);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Read-only current map ownership shared by Scenery models and
    /// labels. Restores the saved registry/Army arena before binding the actual
    /// immutable Scenery definitions and saved flags; no fresh fields are seeded.</summary>
    public SourceWorldContext CaptureSceneryMapCurrent(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var current = CaptureWritableTransportRouteCurrent(state);
        //Current Rank47 precedes the committed unregistration reset image,
        //matching the ordinary owned-current binding order.
        _moraleWaveInputs.BindCurrent(state, current);
        ForceUnregisterInputs.BindCurrent(state, current);
        current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        current.MaterializeFixedScenery(_landmarkDefinitions);
        //The fixed44 placement also supplies Hex1E. Rebuild that static
        //overlay for this capture; saved47 remains the independent owner.
        current.BindFixedConstructionSceneryLinks();
        for (int i = 0; i < 101; i++)
            current.SetSceneryFlags47(i, state.LandmarkInvestments?.GetValueOrDefault(i)?.Flags);
        return current;
    }

    /// <summary>One read-only publication for the map's landmark controller.
    /// Null means membership or position is unknown; an empty list is a known empty registry.
    /// An unknown owner retains the known label with OwnerForceId=null.
    /// This query does not install the application presentation lifecycle.</summary>
    public IReadOnlyList<LandmarkMapEntry>? ReadLandmarkMapEntries(GameState state)
    {
        try
        {
            return ReadLandmarkMapEntries(CaptureSceneryMapCurrent(state));
        }
        catch (InvalidDataException) { return null; }
    }

    public static IReadOnlyList<LandmarkMapEntry>? ReadLandmarkMapEntries(SourceWorldContext current)
    {
        var registered = current.ReadRegisteredSceneryReferences();
        if (registered is null) return null;
        var entries = new List<LandmarkMapEntry>();
        foreach (var scenery in registered)
        {
            if (scenery is null || current.ReadSceneryNativeValidity(scenery) is not bool valid)
                return null;
            if (!valid) continue;
            if (scenery.PhysicalSlot is not int physical || current.SceneryDefinitions is not { } definitions)
                return null;
            string name = definitions.Sceneries[physical].Name;
            if (current.ReadSceneryHexIndex(scenery) is not ushort rawHex)
                return null;
            var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0);
            if (hex?.PhysicalSlot is not int physicalHex)
                return null;
            //The name/position label exists independently of47bit0. That bit
            //only controls the22CBC0 owner getter, whose failed gate selectsForce0.
            int? owner = ReadLandmarkMapOwner(current, scenery);
            //Child construction first removes its existing exactScenery
            //receiver, then appends the fresh child. Repeated registry pointers
            //therefore keep their final occurrence's position in the label list.
            int previous = entries.FindIndex(entry => entry.LandmarkId == physical);
            if (previous >= 0) entries.RemoveAt(previous);
            entries.Add(new(physical, name, physicalHex, owner));
        }
        return entries.AsReadOnly();
    }

    private static int? ReadLandmarkMapOwner(SourceWorldContext current, SceneryReference scenery)
    {
        if (current.ReadSceneryHexIndex(scenery) is not ushort rawHex) return null;
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0);
        if (hex is null) return null;
        int forceSlot = 0;
        if (hex.PhysicalSlot.HasValue)
        {
            if (current.ReadSceneryFlags47(scenery) is not byte flags) return null;
            if ((flags & 1) != 0)
            {
                if (current.ReadHexForceSlot(hex) is not byte rawForce) return null;
                if (rawForce < 52) forceSlot = rawForce;
            }
        }
        var force = current.ReadForceTableReference(forceSlot);
        //An unsafe explicit-nullForce cannot supply an owner identity; it
        //does not erase this independently known landmark name and position.
        return force?.PhysicalSlot is null ? null : current.ReadForcePhysicalSlot(force);
    }
}
