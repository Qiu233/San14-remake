using San14.Data.Formats;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    public SourceProvenance? PersonalityDefinitionSource { get; private set; }
    public SourceProvenance? PersonalityStateSource { get; private set; }
    public string? PersonalityDefinitionDecodedSha256 { get; private set; }

    public void MaterializeFixedPersonalities(LoadedPersonalities loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ApplyPersonalitySource(loaded.Catalog,fixedData:true);
        PersonalityDefinitionSource=loaded.Source;
        PersonalityDefinitionDecodedSha256=loaded.Catalog.DecodedSha256;
    }
    private void MaterializeStaticPersonalityState(LoadedScenario loaded)
    {
        S14ScenarioWorldDocument world=loaded.Snapshot.WorldParameters!;
        if(world.SourceBoundary is null)
        {
            for(int physical=0;physical<S14PersonalityReader.Count;physical++)
                foreach(S14PersonalityField field in S14PersonalityReader.StateFields)
                    for(int index=0;index<field.Bytes;index++)SetPersonalityByte(physical,field.ObjectOffset+index,null);
            PersonalityStateSource=null;
            return;
        }
        ApplyPersonalitySource(S14PersonalityReader.ReadState(world),fixedData:false);
        PersonalityStateSource=loaded.ScenarioSource;
    }
    private void ApplyPersonalitySource(S14PersonalityCatalog catalog,bool fixedData)
    {
        int stride=fixedData ? S14PersonalityReader.FixedStride : S14PersonalityReader.StateStride;
        if(catalog.FixedData!=fixedData || catalog.Rows.Count!=S14PersonalityReader.Count || catalog.RecordStride!=stride ||
            (fixedData && catalog.RecordOffset!=S14PersonalityReader.FixedStart) ||
            catalog.Rows.Where((row,index)=>row.Slot!=index || row.RawBytes.Length!=stride).Any())
            throw new InvalidDataException("Current officer trait source catalog has incomplete or contradictory row boundaries.");
        var fields=fixedData ? S14PersonalityReader.FixedFields : S14PersonalityReader.StateFields;
        foreach(S14PersonalitySourceRow row in catalog.Rows)
        {
            SetStaticTablePointer(0x7DD18,row.Slot,row.Slot);
            foreach(S14PersonalityField field in fields)
            {
                if(fixedData && field.ObjectOffset==0xBF)
                    AppendPersonalitySourceCode(row.Slot,row.RawBytes[field.SourceOffset]);
                for(int index=0;index<field.Bytes;index++)SetPersonalityByte(row.Slot,field.ObjectOffset+index,row.RawBytes[field.SourceOffset+index]);
            }
        }
        //Fixed BF is committed after the same-object C8 append. The scenario
        //state path has no BF/C8 and must preserve that independent storage.
    }
}
