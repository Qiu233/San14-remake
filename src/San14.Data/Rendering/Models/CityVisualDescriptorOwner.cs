namespace San14.Data;

/// <summary>A source-created current CUnitCity category publication. Date and
/// render frame do not recreate it. Changed city bindings need the actual
/// current descriptor writer; a capacity/policy writer can invalidate its
/// affected slot until the current damage tier is published.</summary>
public sealed class CityVisualDescriptorOwner
{
    private sealed record Entry(CityVisualState State,CityVisualSelection Selection);
    private readonly Dictionary<int,Entry> _entries=new();
    private readonly object _gate=new();
    public string Locale{get;}
    public string ImplementationVersion{get;}
    public CityVisualEvidence InitialSourceEvidence{get;}

    internal CityVisualDescriptorOwner(LoadedCityOpeningPostLoadSource source,
        CeCityVariantTable variants)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(variants);
        Locale=source.Locale;ImplementationVersion=source.ImplementationVersion;InitialSourceEvidence=source.Evidence;
        foreach(var record in source.Cities.Values)
        {
            var state=new CityVisualState(record.Slot,record.HexId,record.SizeCode,
                record.ForcePhysicalSlot==0?null:record.ForcePhysicalSlot,record.Durability);
            var selected=source.SelectSourceCreatedOpening(variants,state);
            _entries.Add(record.Slot,new(state,selected.Selection??
                throw new InvalidDataException("Source-created City descriptor could not bind its actual postload input.")));
        }
    }

    public CityVisualSelectionResult SelectCurrentDescriptor(CeCityVariantTable variants,CityVisualState city)
    {
        RequireSource(variants);
        lock(_gate)
        {
            if(!_entries.TryGetValue(city.Slot,out var entry))return new(null,"current-city-descriptor-unpublished");
            if(entry.State!=city)return new(null,"current-city-descriptor-needs-actual-writer");
            return new(entry.Selection,null);
        }
    }

    /// <summary>Publish the actual current City66 writer's output. This API
    /// consumes its explicit tier; it does not infer current policy, settings
    /// or capacity from an earlier opening.</summary>
    public void PublishCurrentDescriptor(CeCityVariantTable variants,CityVisualState city,
        CityVisualDamageInput actualCurrentDamage)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentDamage);RequireSource(variants);
        if(actualCurrentDamage.DamageTier is null)
            throw new InvalidDataException("Current City descriptor writer needs its actual City66 damage tier.");
        var selected=CityVisualSelector.Select(variants,city,actualCurrentDamage);
        if(selected.Selection is not { } selection)
            throw new InvalidDataException(selected.UnavailableReason??"Current City descriptor input is unresolved.");
        lock(_gate)_entries[city.Slot]=new(city,selection);
    }

    /// <summary>A real capacity/policy/settings producer has changed this
    /// city's descriptor input but has not supplied its current City66 yet.</summary>
    public void InvalidateCity(int physicalSlot)
    {
        if(physicalSlot is <1 or >51)throw new ArgumentOutOfRangeException(nameof(physicalSlot));
        lock(_gate)_entries.Remove(physicalSlot);
    }

    private void RequireSource(CeCityVariantTable variants)
    {
        ArgumentNullException.ThrowIfNull(variants);
        if(variants.Locale!=Locale||variants.ImplementationVersion!=ImplementationVersion)
            throw new InvalidDataException("Current City descriptor and native selector source differ.");
    }
}
