namespace San14.Data;

public sealed partial class AreaAdministrationDraft
{
    private AreaAdministrationMapEffectSources? _mapEffectSources;

    /// <summary>Read the held source facts for Domestic's two normal effects
    /// once at entry. Positions, matrices, effect handles and colour remain
    /// with the actual map presentation; no World or model is created here.</summary>
    public AreaAdministrationQuery<AreaAdministrationMapEffectSources> QueryMapEffectSources()
    {
        try
        {
            EnsureOpen();
            if (_mapEffectSources is { } cached)
                return AreaAdministrationQuery<AreaAdministrationMapEffectSources>.Known(cached);

            var rows = new List<AreaAdministrationMapEffectSource>(_held.Length);
            foreach (int physical in _held)
            {
                // The held chain already contains physical Area receivers.
                // Applying the Area pointer table again would alias twice.
                string field = $"map_effect[{physical}]";
                ushort rawHex = K(_current.ReadAreaHexIndex(new(physical)), field + ".Area3A");
                var hex = _current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
                    ?? throw Missing(field + ".Hex.pointer");
                int hexPhysical = K(hex.PhysicalSlot, field + ".Hex.known_null_pointer");
                ushort rawLink = K(_current.ReadHexObjectLink(hex), field + ".Hex1A");

                // 22FC70 is the Object table lookup, independently of the
                // later City/Gate lookups of this same raw Hex1A word.
                var obj = _current.ReadObjectTableReference(rawLink < 3001 ? rawLink : 0)
                    ?? throw Missing(field + ".Object.pointer");
                int objectPhysical = K(obj.PhysicalSlot, field + ".Object.known_null_pointer");
                var record = _current.ReadPhysicalObject(objectPhysical)
                    ?? throw Missing(field + ".Object.fields");
                var definition = _current.ReadObjectDefinitionReference(record.RawField12 < 31 ? record.RawField12 : 0)
                    ?? throw Missing(field + ".ObjectDefinition.pointer");
                _ = K(definition.PhysicalSlot, field + ".ObjectDefinition.known_null_pointer");
                byte category = K(_current.ReadObjectDefinitionRaw2C(definition), field + ".ObjectDefinition2C");

                AreaAdministrationMapEffectBase? city = null, gate = null;
                if (category == 1)
                {
                    // These are independent raw1A table choices, not an
                    // inverse of Object.PhysicalSlot or a Base4E lookup.
                    int cityLookup = rawLink <= 51 ? rawLink : 0;
                    var cityReference = _current.ReadCityTableReference(cityLookup)
                        ?? throw Missing(field + ".City.pointer");
                    city = new(cityLookup, cityReference.PhysicalSlot,
                        K(_current.ReadBaseNativeValidity(cityReference), field + ".City.native_validity"));

                    int gateLookup = rawLink is >= 52 and <= 61 ? rawLink - 51 : 0;
                    var gateReference = _current.ReadGateTableReference(gateLookup)
                        ?? throw Missing(field + ".Gate.pointer");
                    gate = new(gateLookup, gateReference.PhysicalSlot,
                        K(_current.ReadBaseNativeValidity(gateReference), field + ".Gate.native_validity"));
                }
                rows.Add(new(physical, rawHex, hexPhysical, rawLink, category, city, gate));
            }
            _mapEffectSources = new(Revision, rows.AsReadOnly());
            return AreaAdministrationQuery<AreaAdministrationMapEffectSources>.Known(_mapEffectSources);
        }
        catch (InvalidDataException unknown)
        {
            // A partial held chain cannot become a complete effect source.
            return AreaAdministrationQuery<AreaAdministrationMapEffectSources>.Unknown(unknown.Message);
        }
    }
}
