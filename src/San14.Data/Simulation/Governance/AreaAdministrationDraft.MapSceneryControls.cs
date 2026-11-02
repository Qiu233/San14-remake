namespace San14.Data;

/// <summary>One actual Scenery layout receiver's Domestic enable store.
/// ShouldWrite=false is the source native-invalid early return, not disabled.
/// Enabled is null only when that store was not reached.</summary>
public sealed record AreaAdministrationMapSceneryControl(int SceneryId, bool ShouldWrite, bool? Enabled);

public sealed partial class AreaAdministrationDraft
{
    /// <summary>Read the held-area and raw-Force admission used by Domestic's
    /// existing Scenery controls. The caller supplies its actual physical
    /// layout receivers; this does not enumerate World or remap those receivers.</summary>
    public AreaAdministrationQuery<IReadOnlyList<AreaAdministrationMapSceneryControl>> QueryMapSceneryControls(
        IReadOnlyList<int> physicalReceivers)
    {
        ArgumentNullException.ThrowIfNull(physicalReceivers);
        try
        {
            EnsureOpen();
            var result = new List<AreaAdministrationMapSceneryControl>(physicalReceivers.Count);
            foreach (int physical in physicalReceivers)
            {
                // The layout already holds this physical data receiver.
                // A second Scenery table lookup would apply aliases twice.
                var scenery = new SceneryReference(physical);
                string field = $"map_scenery[{physical}]";
                if (!K(_current.ReadSceneryNativeValidity(scenery), field + ".validity"))
                {
                    result.Add(new(physical, false, null));
                    continue;
                }

                // 22A060: resolve Scenery44 -> Hex12 -> Area, retaining the
                // original source-format lookup fallbacks at this boundary.
                ushort rawHex = K(_current.ReadSceneryHexIndex(scenery), field + ".Hex44");
                var hex = _current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
                    ?? throw Missing(field + ".Hex.pointer");
                int rawArea = hex.PhysicalSlot.HasValue
                    ? K(_current.ReadHexAreaSlot(hex), field + ".Hex.Area12") : 0;
                var area = _current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0)
                    ?? throw Missing(field + ".Area.pointer");
                bool enabled = false;
                if (area.PhysicalSlot is int areaPhysical && _held.Contains(areaPhysical))
                {
                    // This existing direct reader is exactly 22CD40's
                    // Area3A -> representative Hex14, without validity gates.
                    byte areaForce = K(_current.ReadAreaCurrentForceSlot(area), field + ".Area.Force14");
                    // 22CFC0 returns a raw Force byte, not a Force-table
                    // receiver or City. A known failed gate selects Force0;
                    // an unknown flag/ownership field remains unresolved.
                    byte sceneryForce = 0;
                    if (hex.PhysicalSlot.HasValue &&
                        (K(_current.ReadSceneryFlags47(scenery), field + ".flags47") & 1) != 0)
                        sceneryForce = K(_current.ReadHexForceSlot(hex), field + ".Hex.Force14");
                    enabled = sceneryForce == areaForce;
                }
                result.Add(new(physical, true, enabled));
            }
            return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationMapSceneryControl>>.Known(result.AsReadOnly());
        }
        catch (InvalidDataException unknown)
        {
            // No partially computed batch is published as a complete result.
            return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationMapSceneryControl>>.Unknown(unknown.Message);
        }
    }
}
