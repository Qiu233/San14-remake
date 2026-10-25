namespace San14.Data;

public sealed record DoctrinePanelReference(int? PhysicalSlot);

public interface IPostRegistrationLookupState
{
    TransferPersonReference? ReadTransferPersonTableReference(int tableSlot);
    ushort? ReadPersonIdentity(int physicalSlot);
    void WritePersonLookupReference(int id,TransferPersonReference reference);
    TransferPersonReference? ReadPersonLookupReference(int id);
    bool? ReadPersonNativeValidity(int physicalSlot);
    ForceReference? ReadPersonForce(int physicalSlot);
    bool? ReadForceNativeValidity(ForceReference force);
    bool AddForceDerivedPerson(ForceReference force,TransferPersonReference person);
    DoctrinePanelReference? ReadDoctrinePanelTableReference(int slot);
    byte? ReadDoctrinePanelRawField7F(DoctrinePanelReference reference);
    void WriteDoctrinePanelLookupReference(int id,DoctrinePanelReference reference);
}

/// <summary>Complete330155..330278 lookup/copy/association window, after the
/// twelve object registries and before31ECD0. No implicit map clear.</summary>
public static class PostRegistrationLookups
{
    //The main manager reserves these200 receivers independently of whether
    //their embedded ID/status currently describes a live generated Person.
    internal static int? HirePersonPhysicalSlot(int officerId) =>
        officerId is >= 4201 and <= 4400 ? 1651 + officerId - 4201 : null;

    internal static void BindHirePersonLookups(IPostRegistrationLookupState state)
    {
        for (int index = 0; index < 200; index++) BindHirePersonLookup(state, index);
    }

    private static TransferPersonReference BindHirePersonLookup(IPostRegistrationLookupState state, int index)
    {
        TransferPersonReference source = Person(state.ReadTransferPersonTableReference(1651 + index));
        state.WritePersonLookupReference(4201 + index, source);
        return source;
    }

    public static void RebuildPersonIds(IPostRegistrationLookupState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        for(int slot=0;slot<1851;slot++)
        {
            TransferPersonReference reference=Person(state.ReadTransferPersonTableReference(slot));
            ushort id=state.ReadPersonIdentity(Physical(reference))
                ??throw new InvalidDataException("Current person ID is unknown during lookup rebuild.");
            if(slot!=0 && id==0)continue;
            state.WritePersonLookupReference(id,reference);
        }
    }
    public static void Apply(int staticFlag,IPostRegistrationLookupState state)
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
            TransferPersonReference source=BindHirePersonLookup(state,index);
            if(!source.PhysicalSlot.HasValue)continue;
            if(!Known(state.ReadPersonNativeValidity(source.PhysicalSlot.Value),"person validity"))continue;
            TransferPersonReference current=Person(state.ReadPersonLookupReference(destination));
            ForceReference force=state.ReadPersonForce(Physical(current))
                ??throw new InvalidDataException("Current derived person force pointer is unknown.");
            if(!force.PhysicalSlot.HasValue || !Known(state.ReadForceNativeValidity(force),"force validity"))continue;
            // Receiver force is saved; the person destination is read again after validity.
            _=state.AddForceDerivedPerson(force,Person(state.ReadPersonLookupReference(destination)));
        }
        for(int slot=0;slot<80;slot++)
        {
            DoctrinePanelReference panel=state.ReadDoctrinePanelTableReference(slot)
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
    private static TransferPersonReference Person(TransferPersonReference? reference)=>reference
        ??throw new InvalidDataException("Current person table/lookup pointer is unknown.");
    private static int Physical(TransferPersonReference reference)=>reference.PhysicalSlot
        ??throw new InvalidDataException("Null current person pointer would be dereferenced by the native lookup stage.");
    private static bool Known(bool? value,string field)=>value
        ??throw new InvalidDataException($"Current post-registration {field} is unknown.");
}
