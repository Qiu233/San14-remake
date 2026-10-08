namespace San14.Data;

public sealed record OriginalDoctrinePanelReference(int? PhysicalSlot);

public interface IOriginalPostRegistrationLookupState
{
    OriginalTransferPersonReference? ReadTransferPersonTableReference(int tableSlot);
    ushort? ReadPersonIdentity(int physicalSlot);
    void WritePersonLookupReference(int id,OriginalTransferPersonReference reference);
    OriginalTransferPersonReference? ReadPersonLookupReference(int id);
    bool? ReadPersonNativeValidity(int physicalSlot);
    OriginalForceReference? ReadPersonForce(int physicalSlot);
    bool? ReadForceNativeValidity(OriginalForceReference force);
    bool AddForceDerivedPerson(OriginalForceReference force,OriginalTransferPersonReference person);
    OriginalDoctrinePanelReference? ReadDoctrinePanelTableReference(int slot);
    byte? ReadDoctrinePanelRawField7F(OriginalDoctrinePanelReference reference);
    void WriteDoctrinePanelLookupReference(int id,OriginalDoctrinePanelReference reference);
}

/// <summary>Complete330155..330278 lookup/copy/association window, after the
/// twelve object registries and before31ECD0. No implicit map clear.</summary>
public static class OriginalPostRegistrationLookups
{
    public static void RebuildPersonIds(IOriginalPostRegistrationLookupState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        for(int slot=0;slot<1851;slot++)
        {
            OriginalTransferPersonReference reference=Person(state.ReadTransferPersonTableReference(slot));
            ushort id=state.ReadPersonIdentity(Physical(reference))
                ??throw new InvalidDataException("Current person ID is unknown during lookup rebuild.");
            if(slot!=0 && id==0)continue;
            state.WritePersonLookupReference(id,reference);
        }
    }
    public static void Apply(int staticFlag,IOriginalPostRegistrationLookupState state)
    {
        RebuildPersonIds(state);
        if(staticFlag!=0)
        {
            Copy(1401,3001,150);
            Copy(1551,5101,100);
        }
        for(int index=0;index<200;index++)
        {
            int destination=4201+index;
            OriginalTransferPersonReference source=Person(state.ReadTransferPersonTableReference(1651+index));
            state.WritePersonLookupReference(destination,source);
            if(!source.PhysicalSlot.HasValue)continue;
            if(!Known(state.ReadPersonNativeValidity(source.PhysicalSlot.Value),"person validity"))continue;
            OriginalTransferPersonReference current=Person(state.ReadPersonLookupReference(destination));
            OriginalForceReference force=state.ReadPersonForce(Physical(current))
                ??throw new InvalidDataException("Current derived person force pointer is unknown.");
            if(!force.PhysicalSlot.HasValue || !Known(state.ReadForceNativeValidity(force),"force validity"))continue;
            // Receiver force is saved; the person destination is read again after validity.
            _=state.AddForceDerivedPerson(force,Person(state.ReadPersonLookupReference(destination)));
        }
        for(int slot=0;slot<80;slot++)
        {
            OriginalDoctrinePanelReference panel=state.ReadDoctrinePanelTableReference(slot)
                ??throw new InvalidDataException("Current doctrine panel table pointer is unknown.");
            byte id=state.ReadDoctrinePanelRawField7F(panel)
                ??throw new InvalidDataException("Current doctrine panel ordering ID is unknown.");
            state.WriteDoctrinePanelLookupReference(id,panel);
        }
        void Copy(int sourceStart,int destinationStart,int count)
        {
            for(int index=0;index<count;index++)
                state.WritePersonLookupReference(destinationStart+index,Person(state.ReadTransferPersonTableReference(sourceStart+index)));
        }
    }
    private static OriginalTransferPersonReference Person(OriginalTransferPersonReference? reference)=>reference
        ??throw new InvalidDataException("Current person table/lookup pointer is unknown.");
    private static int Physical(OriginalTransferPersonReference reference)=>reference.PhysicalSlot
        ??throw new InvalidDataException("Null current person pointer would be dereferenced by the native lookup stage.");
    private static bool Known(bool? value,string field)=>value
        ??throw new InvalidDataException($"Current post-registration {field} is unknown.");
}
